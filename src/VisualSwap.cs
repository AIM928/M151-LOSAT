using System;
using System.Collections.Generic;
using System.IO;
using GHPC;
using GHPC.Crew;
using GHPC.PhysicsHelpers;
using GHPC.Vehicle;
using GHPC.Weapons;
using MelonLoader;
using NWH.WheelController3D;
using UnityEngine;

namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private const float SourceWheelRadius = 0.48095f;
        private const float HumveeVisualScale = 1.0f;
        private const float LauncherMinElevation = -12f; // Sensor, not the fixed tubes.
        private const float LauncherMaxElevation = 35f;
        private const float LauncherAnchorHeight = 1.90f;
        private const int LauncherMaterialCount = 8;
        private const float LauncherAnchorSourceZ = .35f;
        private static readonly Vector3 LauncherModelOffset = new Vector3(0f, 0f, .15f);
        private static readonly Vector3 SourceWheelAverage = new Vector3(0f, 0.4753481f, -0.1651179f);
        private static readonly int[] SourceWheelPartForNative = { 4, 3, 2, 1 };
        private static HumveeSection[] _humveeSections;
        private static Material _humveeMaterial;
        private static Material _interiorMaterial;
        private static Material _plateMaterial;
        private static Material _bodyHardwareMaterial;
        private static Material _headlightMaterial;
        private static Material _indicatorMaterial;
        private static Material _wheelRubberMaterial;
        private static Material _wheelHubMaterial;
        private static Material _launcherBodyMaterial;
        private static Material _launcherDarkMaterial;
        private static Material _launcherLensMaterial;
        private static Material _launcherHardwareMaterial;
        private static Material _launcherLightPaintMaterial;
        private static Material _launcherStructureMaterial;
        private static Material _launcherStencilMaterial;
        private static Material _launcherCautionMaterial;
        private static Texture2D _launcherFinishTexture;
        private static Texture2D _wheelRubberTexture;
        private static Texture2D _wheelHubTexture;
        private static Mesh _detailedWheelMesh;
        private static Mesh _launcherFixedMesh;
        private static Mesh _launcherMovingMesh;
        private static Mesh _launcherBaseMesh;
        private static Vector3 _launcherPivot;
        private static readonly System.Reflection.FieldInfo WheelVisualAngleField=typeof(WheelController).GetField("visRotAngle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
        private static Texture2D _humveeTexture;
        private readonly List<HumveeSwap> _humveeSwaps = new List<HumveeSwap>();

        private sealed class HumveeSection
        {
            public int Part;
            public Vector3 Center;
            public Mesh Mesh;
            public int[] CollisionTriangles;
        }

        private sealed class HumveeSwap
        {
            public Vehicle Vehicle;
            public Transform Root;
            public MeshRenderer BodyRenderer;
            public WheelController[] Controllers;
            public Transform[] WheelTargets;
            public Vector3[] WheelTargetBaseLocalPositions;
            public Vector3[] WheelSourceCenters;
            public Transform[] WheelNodes;
            public Transform LauncherPivot;
            public Transform LauncherYaw;
            public Transform SightHead;
            public WeaponSystem LauncherWeapon;
            public AimablePlatform LauncherElevation;
            public Transform LauncherAimReference;
            public float NextVisualUpdate;
            public float LastWheelVisualTime;
            public float[] SmoothedWheelTravel;
            public float[] LastNativeSpin,VisualSpin;
        }

        private bool InstallHumveeVisual(Vehicle vehicle, WeaponSystem weapon)
        {
            try
            {
                WheelController[] controllers = FindFourWheels(vehicle);
                if (controllers == null)
                {
                    if(ShotDiagnostics) MelonLogger.Warning(Prefix + "M151 M232 four-wheel suspension is not ready; retrying.");
                    return false;
                }
                UniformArmor nativeBodyArmor = FindNativeBodyArmor(vehicle);
                if (nativeBodyArmor == null)
                {
                    MelonLogger.Warning(Prefix + "native M151 body armor is missing; visual swap skipped.");
                    return false;
                }

                // Snapshot the original presentation before creating or moving anything.
                // Only this snapshot may be hidden later; replacement geometry can never
                // accidentally enter the hide pass.
                Renderer[] nativeRendererSnapshot = vehicle.GetComponentsInChildren<Renderer>(true);
                PrepareHumveeAssets();
                Transform root = new GameObject("LOSAT_Humvee_VisualRoot").transform;
                root.SetParent(vehicle.transform, false);
                root.gameObject.layer = vehicle.gameObject.layer;
                root.localRotation = Quaternion.Euler(0f, 180f, 0f);

                float radius = 0f;
                Vector3 nativeAverage = Vector3.zero;
                for (int i = 0; i < controllers.Length; i++)
                {
                    float currentRadius = controllers[i].TireRadius > .05f ? controllers[i].TireRadius : controllers[i].Radius;
                    radius += currentRadius;
                    // WheelController.Position is already expressed in the controller's
                    // simulation space on this vehicle. Treating it as world space sent the
                    // replacement body kilometres away. The animated visual is a real scene
                    // transform, so its world position can be converted reliably here.
                    Transform visualTarget = controllers[i].Visual != null
                        ? controllers[i].Visual.transform
                        : controllers[i].transform;
                    nativeAverage += vehicle.transform.InverseTransformPoint(visualTarget.position);
                }
                radius /= controllers.Length;
                nativeAverage /= controllers.Length;
                float scale = HumveeVisualScale;
                root.localScale = Vector3.one * scale;
                Vector3 rotatedSourceAverage = root.localRotation * (SourceWheelAverage * scale);
                Vector3 alignedRootPosition = nativeAverage - rotatedSourceAverage;
                // Keep the full-size Humvee tyres on the same ground plane as the
                // smaller native M151 tyres instead of sinking the larger tyres.
                float nativeGroundY = nativeAverage.y - radius;
                alignedRootPosition.y = nativeGroundY +
                    (SourceWheelRadius - SourceWheelAverage.y) * scale;
                root.localPosition = alignedRootPosition;

                HumveeSection body = Section(0);
                GameObject bodyNode = CreateMeshNode("Humvee_Body", root, body.Mesh, vehicle.gameObject.layer);
                MeshRenderer bodyRenderer = bodyNode.GetComponent<MeshRenderer>();
                bodyRenderer.enabled = true;
                AddRoundedLampLenses(root, vehicle.gameObject.layer);
                AddBodyGlassAndPlates(root, vehicle.gameObject.layer);
                AddCabinInterior(root, vehicle.gameObject.layer);
                Transform[] wheelNodes = new Transform[4];
                Transform[] wheelTargets = new Transform[4];
                Vector3[] wheelTargetBaseLocalPositions = new Vector3[4];
                Vector3[] wheelSourceCenters = new Vector3[4];
                for (int i = 0; i < 4; i++)
                {
                    HumveeSection wheel = Section(SourceWheelPartForNative[i]);
                    Transform node = CreateMeshNode("Humvee_Wheel_" + (i + 1), root,
                        DetailedWheelMesh(), vehicle.gameObject.layer).transform;
                    Transform visualTarget = controllers[i].Visual != null
                        ? controllers[i].Visual.transform
                        : controllers[i].transform;
                    node.position = root.TransformPoint(wheel.Center);
                    node.rotation = visualTarget.rotation;
                    wheelTargets[i] = visualTarget;
                    wheelTargetBaseLocalPositions[i] = vehicle.transform.InverseTransformPoint(visualTarget.position);
                    wheelSourceCenters[i] = wheel.Center;
                    wheelNodes[i] = node;
                }

                AimablePlatform elevationPlatform = FindLauncherElevation(vehicle, weapon);
                if (elevationPlatform == null)
                    throw new InvalidOperationException("Native launcher elevation platform is missing; cannot apply mechanical stop.");
                ConfigureLauncherTravel(elevationPlatform);
                Transform nativeAimReference = weapon.MuzzleIdentity;
                Transform movingWeaponRoot = MoveWeaponToRoof(vehicle, weapon, root, scale);
                List<Transform> crewVisualRoots = PlaceCrewInside(vehicle, root);
                ConfigureCabinCameras(vehicle, root);
                int hidden = HideNativeVisuals(nativeRendererSnapshot, movingWeaponRoot, weapon,
                    crewVisualRoots, controllers);
                int hiddenLauncher = HideNativeLauncherMesh(movingWeaponRoot);
                Transform sightHead;
                Transform launcherPivot = BuildFourTubeLauncherVisual(vehicle, movingWeaponRoot,out sightHead);
                // FCS alignment follows the independently rotating sight reference,
                // not the new stationary barrel identities. Native damage gates stay.
                if(weapon.FCS!=null) weapon.FCS.FireGateOverrideTransform=nativeAimReference;
                // The body renderer is deliberately reasserted after the hide pass. This is
                // also a guard against third-party renderer-management mods toggling it once.
                root.gameObject.SetActive(true);
                bodyNode.SetActive(true);
                bodyRenderer.enabled = true;
                MeshCollider collision = ReplaceNativeBodyArmorMesh(vehicle, root, body.Mesh,
                    body.CollisionTriangles, nativeBodyArmor);

                // Change only damping on this converted vehicle; no global physics patch,
                // spring/radius/anchor/mass/centre-of-mass edits or forced body orientation.
                foreach (WheelController controller in controllers)
                {
                    if(controller==null) continue;
                    if(controller.fFriction!=null) {
                        controller.fFriction=(WheelController.Friction)MemberwiseCloneMethod.Invoke(controller.fFriction,null);
                        controller.fFriction.forceCoefficient=FixedLauncherRules.GripFloor(controller.fFriction.forceCoefficient,false);
                    }
                    if(controller.sFriction!=null) {
                        controller.sFriction=(WheelController.Friction)MemberwiseCloneMethod.Invoke(controller.sFriction,null);
                        controller.sFriction.forceCoefficient=FixedLauncherRules.GripFloor(controller.sFriction.forceCoefficient,true);
                    }
                    if (controller.damper == null) continue;
                    controller.damper.unitBumpForce = SuspensionRules.DampingFloor(controller.damper.unitBumpForce, false);
                    controller.damper.unitReboundForce = SuspensionRules.DampingFloor(controller.damper.unitReboundForce, true);
                }

                ConfigureHumveeShellProtection(vehicle);
                ConfigureCabinMobility(vehicle);
                _humveeSwaps.Add(new HumveeSwap
                {
                    Vehicle = vehicle,
                    Root = root,
                    BodyRenderer = bodyRenderer,
                    Controllers = controllers,
                    WheelTargets = wheelTargets,
                    WheelTargetBaseLocalPositions = wheelTargetBaseLocalPositions,
                    WheelSourceCenters = wheelSourceCenters,
                    WheelNodes = wheelNodes,
                    SmoothedWheelTravel = new float[4],
                    LastWheelVisualTime = Time.time,
                    LauncherPivot = launcherPivot,
                    LauncherYaw = movingWeaponRoot,
                    SightHead = sightHead,
                    LauncherWeapon = weapon,
                    LauncherElevation = elevationPlatform,
                    LauncherAimReference = nativeAimReference,
                    LastNativeSpin = new float[4], VisualSpin = new float[4]
                });
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error(Prefix + "Humvee visual conversion failed safely: " + ex);
                return false;
            }
        }

        private static WheelController[] FindFourWheels(Vehicle vehicle)
        {
            WheelController[] all = vehicle.GetComponentsInChildren<WheelController>(true);
            if (all == null || all.Length < 4) return null;
            List<WheelController> candidates = new List<WheelController>(all);
            candidates.Sort((left, right) =>
            {
                Vector3 a = WheelLocalPosition(vehicle, left);
                Vector3 b = WheelLocalPosition(vehicle, right);
                int longitudinal = b.z.CompareTo(a.z);
                return longitudinal != 0 ? longitudinal : a.x.CompareTo(b.x);
            });
            if (candidates.Count > 4) candidates.RemoveRange(4, candidates.Count - 4);

            WheelController[] result = candidates.ToArray();
            // Normalize each axle as left (negative local X), then right.
            for (int axle = 0; axle < 2; axle++)
            {
                int first = axle * 2;
                Vector3 a = WheelLocalPosition(vehicle, result[first]);
                Vector3 b = WheelLocalPosition(vehicle, result[first + 1]);
                if (a.x > b.x)
                {
                    WheelController temp = result[first];
                    result[first] = result[first + 1];
                    result[first + 1] = temp;
                }
            }
            return result;
        }

        private static Vector3 WheelLocalPosition(Vehicle vehicle, WheelController controller)
        {
            Transform target = controller != null && controller.Visual != null
                ? controller.Visual.transform
                : controller != null ? controller.transform : null;
            return target == null ? Vector3.zero : vehicle.transform.InverseTransformPoint(target.position);
        }

        private static void PrepareHumveeAssets()
        {
            if (_humveeSections != null && _humveeMaterial != null &&
                _interiorMaterial != null && _plateMaterial != null &&
                _bodyHardwareMaterial != null && _headlightMaterial != null && _indicatorMaterial != null &&
                _wheelRubberMaterial != null && _wheelHubMaterial != null &&
                _launcherBodyMaterial != null && _launcherDarkMaterial != null &&
                _launcherLensMaterial != null && _launcherHardwareMaterial != null &&
                _launcherLightPaintMaterial != null && _launcherStructureMaterial != null &&
                _launcherStencilMaterial != null && _launcherCautionMaterial != null &&
                _launcherFixedMesh != null && _launcherMovingMesh != null && _launcherBaseMesh != null &&
                _wheelRubberTexture != null &&
                _wheelHubTexture != null &&
                _detailedWheelMesh != null) return;
            using (Stream stream = typeof(Mod).Assembly.GetManifestResourceStream("M151LOSATTest.Humvee.Mesh"))
            using (BinaryReader reader = stream == null ? null : new BinaryReader(stream))
            {
                if (reader == null) throw new InvalidOperationException("Embedded Humvee geometry is missing.");
                if (reader.ReadInt32() != 151232) throw new InvalidDataException("Humvee geometry header mismatch.");
                int sectionCount = reader.ReadInt32();
                if (sectionCount != 5) throw new InvalidDataException("Humvee section count mismatch.");
                HumveeSection[] sections = new HumveeSection[sectionCount];
                for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
                {
                    int part = reader.ReadInt32();
                    Vector3 center = ReadHumveeVector(reader);
                    int count = reader.ReadInt32();
                    if (part < 0 || part > 4 || count < 3 || count > 100000 || count % 3 != 0)
                        throw new InvalidDataException("Humvee mesh section is invalid.");
                    Vector3[] vertices = new Vector3[count];
                    Vector3[] normals = new Vector3[count];
                    Vector2[] uvs = new Vector2[count];
                    int[] triangles = new int[count];
                    for (int i = 0; i < count; i++) { vertices[i] = ReadHumveeVector(reader); triangles[i] = i; }
                    for (int i = 0; i < count; i++) normals[i] = ReadHumveeVector(reader);
                    for (int i = 0; i < count; i++) uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    Mesh mesh = new Mesh();
                    mesh.name = "Humvee_FBX_Part_" + part;
                    mesh.vertices = vertices;
                    mesh.normals = normals;
                    if (part == 0)
                    {
                        List<int> exteriorTriangles = new List<int>();
                        List<int> interiorTriangles = new List<int>();
                        List<int> plateTriangles = new List<int>();
                        List<int>[] detailTriangles = { new List<int>(), new List<int>(), new List<int>() };
                        int removedWiperTriangles = 0;
                        for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                        {
                            Vector3 triangleCenter = (vertices[triangle] + vertices[triangle + 1] +
                                vertices[triangle + 2]) / 3f;
                            Vector3 normal = (normals[triangle] + normals[triangle + 1] +
                                normals[triangle + 2]).normalized;
                            bool wiper = IsWiperTriangle(vertices[triangle], vertices[triangle + 1],
                                vertices[triangle + 2]);
                            // Omit the actual wiper faces from rendering, not merely their material.
                            // The original collision triangle list below stays untouched.
                            if (wiper) { removedWiperTriangles++; continue; }
                            // Replace only the legacy cockpit's coarse wheel and
                            // hexagonal instruments. Collision keeps the source mesh.
                            if(LegacyCabinTriangle(vertices[triangle],vertices[triangle+1],vertices[triangle+2])) continue;
                            bool plate = IsNumberPlateTriangle(vertices[triangle], vertices[triangle + 1],
                                vertices[triangle + 2]);
                            if (plate) continue; // Replaced by black/yellow accessory plates.
                            int detail = plate ? -1 : BodyDetailMaterial(triangleCenter, normal);
                            if (detail == 1) continue; // Replaced by a round glass lens and bezel below.
                            bool interior = !plate && detail < 0 && IsInteriorTriangle(triangleCenter, normal);
                            List<int> destination = detail >= 0 ? detailTriangles[detail] : plate ? plateTriangles :
                                interior ? interiorTriangles : exteriorTriangles;
                            for (int corner = 0; corner < 3; corner++)
                            {
                                int index = triangle + corner;
                                destination.Add(index);
                                // Keep the baked, continuous atlas UVs across body surfaces.
                            }
                        }
                        mesh.subMeshCount = 6;
                        mesh.SetTriangles(exteriorTriangles, 0);
                        mesh.SetTriangles(interiorTriangles, 1);
                        mesh.SetTriangles(plateTriangles, 2);
                        for (int detail = 0; detail < 3; detail++) mesh.SetTriangles(detailTriangles[detail], detail + 3);
                    }
                    else
                    {
                        List<int> rubberTriangles = new List<int>();
                        List<int> hubTriangles = new List<int>();
                        for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                        {
                            Vector3 triangleCenter = (vertices[triangle] + vertices[triangle + 1] +
                                vertices[triangle + 2]) / 3f;
                            float radial = Mathf.Sqrt(triangleCenter.y * triangleCenter.y +
                                triangleCenter.z * triangleCenter.z);
                            List<int> destination = radial > .31f ? rubberTriangles : hubTriangles;
                            destination.Add(triangle);
                            destination.Add(triangle + 1);
                            destination.Add(triangle + 2);
                        }
                        mesh.subMeshCount = 2;
                        mesh.SetTriangles(rubberTriangles, 0);
                        mesh.SetTriangles(hubTriangles, 1);
                    }
                    mesh.uv = uvs;
                    mesh.RecalculateTangents();
                    mesh.RecalculateBounds();
                    UnityEngine.Object.DontDestroyOnLoad(mesh);
                    sections[part] = new HumveeSection
                    {
                        Part = part,
                        Center = center,
                        Mesh = mesh,
                        CollisionTriangles = part == 0 ? triangles : null
                    };
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Humvee geometry has trailing data.");
                _humveeSections = sections;
            }

            using (Stream stream = typeof(Mod).Assembly.GetManifestResourceStream("M151LOSATTest.Humvee.Texture"))
            {
                if (stream == null) throw new InvalidOperationException("Embedded Humvee texture is missing.");
                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int amount = stream.Read(bytes, read, bytes.Length - read);
                    if (amount <= 0) throw new EndOfStreamException("Humvee texture read failed.");
                    read += amount;
                }
                _humveeTexture = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
                if (!ImageConversion.LoadImage(_humveeTexture, bytes, true))
                    throw new InvalidDataException("Humvee texture decode failed.");
                _humveeTexture.name = "Humvee_Continuous_Woodland_Atlas";
                _humveeTexture.wrapMode = TextureWrapMode.Repeat;
                _humveeTexture.filterMode = FilterMode.Trilinear;
                _humveeTexture.anisoLevel = 4;
                UnityEngine.Object.DontDestroyOnLoad(_humveeTexture);
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader is unavailable.");
            _humveeMaterial = new Material(shader);
            _humveeMaterial.name = "Humvee_Weathered_Satin_Paint";
            _humveeMaterial.mainTexture = _humveeTexture;
            _humveeMaterial.color = Color.white;
            _humveeMaterial.SetFloat("_Metallic", .04f);
            _humveeMaterial.SetFloat("_Glossiness", .14f);
            _humveeMaterial.SetTexture("_BumpMap", LoadHumveeSurfaceMap("Normal"));
            _humveeMaterial.SetFloat("_BumpScale", .80f);
            _humveeMaterial.EnableKeyword("_NORMALMAP");
            _humveeMaterial.SetTexture("_OcclusionMap", LoadHumveeSurfaceMap("Occlusion"));
            _humveeMaterial.SetFloat("_OcclusionStrength", .76f);
            _humveeMaterial.SetTexture("_MetallicGlossMap", LoadHumveeSurfaceMap("Finish"));
            _humveeMaterial.SetFloat("_GlossMapScale", 1.10f);
            _humveeMaterial.EnableKeyword("_METALLICGLOSSMAP");
            // Shared shader detail normal, not another atlas or per-frame work.
            Texture2D bodyGrain=BuildLauncherPaintGrain(true);
            _humveeMaterial.SetTexture("_DetailNormalMap",bodyGrain);
            _humveeMaterial.SetTextureScale("_DetailAlbedoMap",new Vector2(12f,12f));
            _humveeMaterial.SetFloat("_UVSec",0f);
            _humveeMaterial.SetFloat("_DetailNormalMapScale",.33f);
            _humveeMaterial.EnableKeyword("_DETAIL_MULX2");
            _humveeMaterial.SetInt("_Cull", 0);
            UnityEngine.Object.DontDestroyOnLoad(_humveeMaterial);

            _interiorMaterial = new Material(shader);
            _interiorMaterial.name = "Humvee_Charcoal_Interior_Lining";
            _interiorMaterial.color = new Color(.055f, .064f, .060f, 1f);
            _interiorMaterial.SetFloat("_Metallic", .03f);
            _interiorMaterial.SetFloat("_Glossiness", .18f);
            _interiorMaterial.SetTexture("_BumpMap",bodyGrain);
            _interiorMaterial.SetFloat("_BumpScale",.18f);
            _interiorMaterial.EnableKeyword("_NORMALMAP");
            ApplySurfaceFinish(_interiorMaterial,SurfaceFinishRules.Rubber,.20f);
            _interiorMaterial.SetInt("_Cull", 0);
            UnityEngine.Object.DontDestroyOnLoad(_interiorMaterial);

            _plateMaterial = new Material(shader);
            _plateMaterial.name = "Humvee_Plain_Number_Plate_No_Camouflage";
            _plateMaterial.color = new Color(.42f, .44f, .41f, 1f);
            _plateMaterial.SetFloat("_Metallic", .15f);
            _plateMaterial.SetFloat("_Glossiness", .25f);
            _plateMaterial.SetInt("_Cull", 0);
            UnityEngine.Object.DontDestroyOnLoad(_plateMaterial);
            _bodyHardwareMaterial = MakeBodyDetailMaterial(shader, "Humvee_Grille_Mirror_And_Hardware", new Color(.17f, .20f, .18f), .35f, .20f);
            _headlightMaterial = MakeBodyDetailMaterial(shader, "Humvee_Clear_Headlight_Lenses", new Color(.53f, .58f, .56f), .08f, .86f);
            _indicatorMaterial = MakeBodyDetailMaterial(shader, "Humvee_Amber_Indicator_Lenses", new Color(.68f, .27f, .055f), .06f, .58f);

            _wheelRubberMaterial = new Material(shader);
            _wheelRubberMaterial.name = "Humvee_Textured_Offroad_Rubber";
            _wheelRubberTexture = BuildWheelTexture(256, false);
            _wheelRubberMaterial.mainTexture = _wheelRubberTexture;
            _wheelRubberMaterial.color = Color.white;
            _wheelRubberMaterial.SetFloat("_Metallic", .02f);
            _wheelRubberMaterial.SetFloat("_Glossiness", .12f);
            _wheelRubberMaterial.SetTexture("_BumpMap",SurfaceFinishMap(SurfaceFinishRules.Rubber,1));
            _wheelRubberMaterial.SetFloat("_BumpScale",.20f);
            _wheelRubberMaterial.EnableKeyword("_NORMALMAP");
            _wheelRubberMaterial.SetTexture("_MetallicGlossMap",SurfaceFinishMap(SurfaceFinishRules.Rubber,2));
            _wheelRubberMaterial.SetFloat("_GlossMapScale",.65f);
            _wheelRubberMaterial.EnableKeyword("_METALLICGLOSSMAP");
            UnityEngine.Object.DontDestroyOnLoad(_wheelRubberMaterial);

            _wheelHubMaterial = new Material(shader);
            _wheelHubMaterial.name = "Humvee_Textured_Dark_Steel_Hub";
            _wheelHubTexture = BuildWheelTexture(128, true);
            _wheelHubMaterial.mainTexture = _wheelHubTexture;
            _wheelHubMaterial.color = Color.white;
            _wheelHubMaterial.SetFloat("_Metallic", .42f);
            _wheelHubMaterial.SetFloat("_Glossiness", .24f);
            UnityEngine.Object.DontDestroyOnLoad(_wheelHubMaterial);

            _launcherBodyMaterial = new Material(shader);
            _launcherBodyMaterial.name = "LOSAT_Forest_Green_Powder_Coat";
            _launcherBodyMaterial.color = new Color(.19f, .265f, .205f, 1f);
            _launcherBodyMaterial.SetFloat("_Metallic", .045f);
            _launcherBodyMaterial.SetFloat("_Glossiness", .24f);
            _launcherFinishTexture = BuildLauncherFinishTexture();
            _launcherBodyMaterial.SetTexture("_MetallicGlossMap", _launcherFinishTexture);
            _launcherBodyMaterial.SetFloat("_GlossMapScale", 1f);
            _launcherBodyMaterial.EnableKeyword("_METALLICGLOSSMAP");
            UnityEngine.Object.DontDestroyOnLoad(_launcherBodyMaterial);

            _launcherDarkMaterial = new Material(shader);
            _launcherDarkMaterial.name = "Humvee_4Tube_Charcoal_Seals";
            _launcherDarkMaterial.color = new Color(.075f, .09f, .085f, 1f);
            _launcherDarkMaterial.SetFloat("_Metallic", .02f);
            _launcherDarkMaterial.SetFloat("_Glossiness", .17f);
            UnityEngine.Object.DontDestroyOnLoad(_launcherDarkMaterial);

            _launcherLensMaterial = new Material(shader);
            _launcherLensMaterial.name = "Humvee_4Tube_Recessed_Optical_Glass";
            _launcherLensMaterial.color = new Color(.14f, .24f, .265f, 1f);
            _launcherLensMaterial.SetFloat("_Metallic", .30f);
            _launcherLensMaterial.SetFloat("_Glossiness", .81f);
            UnityEngine.Object.DontDestroyOnLoad(_launcherLensMaterial);

            _launcherHardwareMaterial = new Material(shader);
            _launcherHardwareMaterial.name = "Humvee_4Tube_Satin_Steel_Fasteners";
            _launcherHardwareMaterial.color = new Color(.31f, .34f, .33f, 1f);
            _launcherHardwareMaterial.SetFloat("_Metallic", .72f);
            _launcherHardwareMaterial.SetFloat("_Glossiness", .42f);
            UnityEngine.Object.DontDestroyOnLoad(_launcherHardwareMaterial);

            _launcherLightPaintMaterial = MakeBodyDetailMaterial(shader, "LOSAT_Field_Green_Pod_Covers", new Color(.26f, .315f, .24f), .045f, .25f);
            _launcherStructureMaterial = MakeBodyDetailMaterial(shader, "LOSAT_Graphite_Mechanical_Base", new Color(.10f, .135f, .12f), .045f, .22f);
            _launcherStencilMaterial = MakeBodyDetailMaterial(shader, "LOSAT_Faded_Ivory_Identification", new Color(.66f, .68f, .53f), 0f, .15f);
            _launcherCautionMaterial = MakeBodyDetailMaterial(shader, "LOSAT_Muted_Ochre_Handling_Marks", new Color(.63f, .50f, .21f), 0f, .17f);
            foreach (Material paint in new[] {_launcherBodyMaterial,_launcherLightPaintMaterial,_launcherStructureMaterial})
            {
                ApplySurfaceFinish(paint,SurfaceFinishRules.Paint,.34f);
            }
            ApplySurfaceFinish(_launcherDarkMaterial,SurfaceFinishRules.Rubber,.18f);
            ApplySurfaceFinish(_launcherHardwareMaterial,SurfaceFinishRules.Metal,.12f);
            ApplySurfaceFinish(_bodyHardwareMaterial,SurfaceFinishRules.Metal,.12f);

            LoadBlenderLauncherMeshes();

            _detailedWheelMesh = BuildDetailedWheelMesh();
            UnityEngine.Object.DontDestroyOnLoad(_detailedWheelMesh);
        }

        private static Texture2D BuildLauncherFinishTexture()
        {
            return SurfaceFinishMap(SurfaceFinishRules.Paint,2);
        }

        private static Texture2D BuildLauncherPaintGrain(bool normalMap)
        {
            return SurfaceFinishMap(SurfaceFinishRules.Paint,normalMap ? 1 : 0);
        }

        private static Texture2D BuildWheelTexture(int size, bool hub)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            texture.name = hub ? "Humvee_Dark_Steel_Hub_Texture" : "Humvee_Offroad_Tread_Texture";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = (y + .5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + .5f) / size;
                    float grain = Mathf.PerlinNoise(u * 53f + 2.1f, v * 47f + 4.7f) - .5f;
                    float shade;
                    if (hub)
                    {
                        float dx = u - .5f;
                        float dy = v - .5f;
                        float ring = Mathf.Sqrt(dx * dx + dy * dy);
                        shade = .26f + grain * .055f;
                        if (ring > .36f && ring < .43f) shade *= .76f;
                        if (ring < .13f) shade *= .65f;
                    }
                    else
                    {
                        // U runs around the circumference. V crosses the tread and
                        // sidewalls of the existing toroidal tyre mesh.
                        float crown = Mathf.Cos(v * Mathf.PI * 2f);
                        float stripe = Mathf.Abs(Mathf.Repeat(u * 24f + (v - .5f) * 6f, 1f) - .5f);
                        bool groove = crown > .45f && stripe < .105f;
                        shade = .135f + grain * .028f;
                        if (crown > .45f) shade += .025f;
                        if (groove) shade *= .52f;
                        if (crown < -.25f) shade *= .82f;
                    }
                    float dust=hub ? 0f : Mathf.Clamp01((grain+.12f)*2.5f)*.014f;
                    // Subtle warm soil in existing tread valleys, not a white,
                    // uniformly dirty tyre. Existing UVs and texture budget stay fixed.
                    shade+=dust;
                    byte value = (byte)Mathf.Clamp(Mathf.RoundToInt(shade * 255f), 0, 255);
                    pixels[y * size + x] = hub
                        ? new Color32(value, value, (byte)Mathf.Min(255, value + 2), 255)
                        : new Color32((byte)Mathf.Min(255,value+Mathf.RoundToInt(dust*120f)),value,(byte)Mathf.Max(0,value-Mathf.RoundToInt(dust*80f)),255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            UnityEngine.Object.DontDestroyOnLoad(texture);
            return texture;
        }

        private static bool IsInteriorTriangle(Vector3 center, Vector3 normal)
        {
            if (Mathf.Abs(center.x) >= 1.12f || center.y <= .44f || center.y >= 1.84f ||
                center.z <= -1.13f || center.z >= 1.03f) return false;
            // Rear cabin partition faces forward into the cabin. Its atlas UV
            // must not show woodland paint; outward-facing hull stays camo.
            if(center.z>.45f && normal.z<-.25f) return true;
            // Side door faces must point into the cabin; exterior panels keep camo.
            if (Mathf.Abs(center.x) > .68f)
                return normal.x * center.x < -.15f;
            return center.y < 1.40f || normal.y < -.32f;
        }

        private static bool IsWiperTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            return IsWiperVertex(a) && IsWiperVertex(b) && IsWiperVertex(c);
        }

        private static bool IsWiperVertex(Vector3 point)
        {
            if (point.z <= -.87f || point.z >= -.83f ||
                point.y <= 1.55f || point.y >= 1.78f) return false;
            return (point.x > .58f && point.x < .94f) ||
                   (point.x > -.53f && point.x < -.17f);
        }

        private static bool IsNumberPlateTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            return IsNumberPlateVertex(a) && IsNumberPlateVertex(b) && IsNumberPlateVertex(c);
        }

        private static bool IsNumberPlateVertex(Vector3 point)
        {
            // Bounds of the source front plate, including raised lettering and fasteners.
            // Source Humvee faces -Z; the visual root rotates it to vehicle-forward +Z.
            bool front = Mathf.Abs(point.x) < .40f && point.y > .515f && point.y < .805f &&
                point.z > -2.465f && point.z < -2.275f;
            bool rearRecess = Mathf.Abs(point.x) < .475f && point.y > .955f && point.y < 1.180f &&
                point.z > 2.150f && point.z < 2.162f;
            return front || rearRecess;
        }

        private static Material MakeBodyDetailMaterial(Shader shader, string name, Color color, float metal, float gloss)
        {
            Material material = new Material(shader);
            material.name = name;
            material.color = color;
            material.SetFloat("_Metallic", metal);
            material.SetFloat("_Glossiness", gloss);
            material.SetInt("_Cull", 0);
            UnityEngine.Object.DontDestroyOnLoad(material);
            return material;
        }

        private static int BodyDetailMaterial(Vector3 p, Vector3 n)
        {
            float headX = Mathf.Abs(p.x) - .497f;
            if (p.z < -2.212f && n.z < -.4f &&
                headX * headX + (p.y - .959f) * (p.y - .959f) < .081f * .081f) return 1;
            float signalX = Mathf.Abs(p.x) - .971f;
            if (p.z < -2.202f && n.z < -.4f &&
                signalX * signalX + (p.y - .967f) * (p.y - .967f) < .073f * .073f) return 2;
            bool grille = Mathf.Abs(p.x) < .36f && p.y > .855f && p.y < 1.052f && p.z < -2.216f;
            bool louver = Mathf.Abs(p.x) < .405f && p.y > 1.165f && p.y < 1.285f &&
                p.z > -2.195f && p.z < -1.60f && n.z < -.45f;
            bool mirror = Mathf.Abs(p.x) > .99f && p.z > -1.045f && p.z < -1.00f && p.y > 1.49f;
            return grille || louver || mirror ? 0 : -1;
        }

        private static void AddRoundedLampLenses(Transform root, int layer)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                AddLampLens(root, layer, "Humvee_Round_Headlight_" + side,
                    new Vector3(side * .497f, .959f, -2.246f), new Vector3(.148f, .148f, .012f), _headlightMaterial);
                AddHeadlightBezel(root, layer, side);
                AddLampLens(root, layer, "Humvee_Round_Indicator_" + side,
                    new Vector3(side * .971f, .967f, -2.231f), new Vector3(.120f, .120f, .009f), _indicatorMaterial);
            }
        }

        private static void AddLampLens(Transform root, int layer, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.name = name;
            lens.layer = layer;
            lens.transform.SetParent(root, false);
            lens.transform.localPosition = position;
            lens.transform.localScale = scale;
            lens.GetComponent<MeshRenderer>().sharedMaterial = material;
            Collider collider = lens.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; UnityEngine.Object.Destroy(collider); }
        }

        private static Mesh DetailedWheelMesh()
        {
            if (_detailedWheelMesh == null) _detailedWheelMesh = BuildDetailedWheelMesh();
            return _detailedWheelMesh;
        }

        private static void AddHeadlightBezel(Transform root, int layer, int side)
        {
            const int segments = 48;
            const int crossSegments = 8;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int ring = 0; ring < segments; ring++)
            {
                float a = ring * Mathf.PI * 2f / segments;
                Vector3 radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int cross = 0; cross < crossSegments; cross++)
                {
                    float b = cross * Mathf.PI * 2f / crossSegments;
                    vertices.Add(radial * (.084f + .009f * Mathf.Cos(b)) + Vector3.forward * (.009f * Mathf.Sin(b)));
                    normals.Add(radial * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b));
                }
            }
            for (int ring = 0; ring < segments; ring++)
                for (int cross = 0; cross < crossSegments; cross++)
                    AddQuad(triangles, ring * crossSegments + cross,
                        ((ring + 1) % segments) * crossSegments + cross,
                        ((ring + 1) % segments) * crossSegments + (cross + 1) % crossSegments,
                        ring * crossSegments + (cross + 1) % crossSegments);
            Mesh mesh = new Mesh();mesh.name = "Humvee_Rounded_Headlight_Bezel";
            mesh.vertices = vertices.ToArray();mesh.normals = normals.ToArray();mesh.triangles = triangles.ToArray();mesh.RecalculateBounds();
            GameObject bezel = new GameObject("Humvee_Headlight_Bezel_" + side);
            bezel.layer = layer;bezel.transform.SetParent(root, false);
            bezel.transform.localPosition = new Vector3(side * .497f, .959f, -2.244f);
            bezel.AddComponent<MeshFilter>().sharedMesh = mesh;
            bezel.AddComponent<MeshRenderer>().sharedMaterial = _bodyHardwareMaterial;
        }

        private static Mesh BuildDetailedWheelMesh()
        {
            const int radialSegments = 48;
            const int sideSegments = 14;
            const float majorRadius = .350f;
            const float radialThickness = .131f;
            const float halfWidth = .171f;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> rubber = new List<int>();
            List<int> hub = new List<int>();

            int torusStart = vertices.Count;
            for (int ring = 0; ring < radialSegments; ring++)
            {
                float theta = ring * Mathf.PI * 2f / radialSegments;
                float cosTheta = Mathf.Cos(theta);
                float sinTheta = Mathf.Sin(theta);
                for (int side = 0; side < sideSegments; side++)
                {
                    float phi = side * Mathf.PI * 2f / sideSegments;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);
                    float crown = cosPhi > 0f ? Mathf.Pow(cosPhi, .20f) : cosPhi;
                    float radius = majorRadius + crown * radialThickness;
                    vertices.Add(new Vector3(sinPhi * halfWidth, cosTheta * radius, sinTheta * radius));
                    float slope = cosPhi > .0001f ? sinPhi * radialThickness * .20f * Mathf.Pow(cosPhi, -.80f) : sinPhi * radialThickness;
                    normals.Add(new Vector3(slope,
                        cosTheta * cosPhi * halfWidth,
                        sinTheta * cosPhi * halfWidth).normalized);
                    uvs.Add(new Vector2((float)ring / radialSegments, (float)side / sideSegments));
                }
            }
            for (int ring = 0; ring < radialSegments; ring++)
            {
                int nextRing = (ring + 1) % radialSegments;
                for (int side = 0; side < sideSegments; side++)
                {
                    int nextSide = (side + 1) % sideSegments;
                    int a = torusStart + ring * sideSegments + side;
                    int b = torusStart + nextRing * sideSegments + side;
                    int c = torusStart + nextRing * sideSegments + nextSide;
                    int d = torusStart + ring * sideSegments + nextSide;
                    AddQuad(rubber, a, b, c, d);
                }
            }

            AppendCylinder(vertices, normals, uvs, hub, .228f, .140f, 48);
            AppendCylinder(vertices, normals, uvs, hub, .202f, .153f, 48);
            AppendCylinder(vertices, normals, uvs, hub, .075f, .164f, 32);
            for (int side = -1; side <= 1; side += 2)
                for (int bolt = 0; bolt < 8; bolt++)
                {
                    float angle = bolt * Mathf.PI / 4f;
                    int start = vertices.Count;
                    AppendCylinder(vertices, normals, uvs, hub, .011f, .006f, 6);
                    Vector3 offset = new Vector3(side * .161f, Mathf.Cos(angle) * .157f, Mathf.Sin(angle) * .157f);
                    for (int index = start; index < vertices.Count; index++) vertices[index] += offset;
                }

            // Two rows of low chevron lugs instead of oversized paddle-like blocks.
            for (int tread = 0; tread < radialSegments; tread++)
            {
                float theta = tread * Mathf.PI * 2f / radialSegments;
                Vector3 outward = new Vector3(0f, Mathf.Cos(theta), Mathf.Sin(theta));
                Vector3 tangent = new Vector3(0f, -Mathf.Sin(theta), Mathf.Cos(theta));
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 center = outward * (SourceWheelRadius - .002f);
                    center.x = side * .063f;
                    float angle = side * 32f * Mathf.Deg2Rad;
                    Vector3 across = Vector3.right * Mathf.Cos(angle) + tangent * Mathf.Sin(angle);
                    Vector3 along = -Vector3.right * Mathf.Sin(angle) + tangent * Mathf.Cos(angle);
                    AppendOrientedBox(vertices, normals, uvs, rubber, center,
                        across, along, outward, .058f, .018f, .009f);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "Humvee_48_Segment_Chevron_Tread_And_8_Bolt_Hub";
            mesh.vertices = vertices.ToArray();
            mesh.normals = normals.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.subMeshCount = 2;
            mesh.SetTriangles(rubber, 0);
            mesh.SetTriangles(hub, 1);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendCylinder(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, float radius, float halfWidth, int segments)
        {
            int sideStart = vertices.Count;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                float y = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                Vector3 normal = new Vector3(0f, y, z).normalized;
                vertices.Add(new Vector3(-halfWidth, y, z)); normals.Add(normal); uvs.Add(new Vector2(0f, (float)segment / segments));
                vertices.Add(new Vector3(halfWidth, y, z)); normals.Add(normal); uvs.Add(new Vector2(1f, (float)segment / segments));
            }
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                AddQuad(triangles, sideStart + segment * 2, sideStart + next * 2,
                    sideStart + next * 2 + 1, sideStart + segment * 2 + 1);
            }
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -halfWidth : halfWidth;
                Vector3 normal = side == 0 ? Vector3.left : Vector3.right;
                int centerIndex = vertices.Count;
                vertices.Add(new Vector3(x, 0f, 0f)); normals.Add(normal); uvs.Add(new Vector2(.5f, .5f));
                int ringStart = vertices.Count;
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment * Mathf.PI * 2f / segments;
                    float y = Mathf.Cos(angle) * radius;
                    float z = Mathf.Sin(angle) * radius;
                    vertices.Add(new Vector3(x, y, z)); normals.Add(normal);
                    uvs.Add(new Vector2(.5f + y / radius * .5f, .5f + z / radius * .5f));
                }
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    if (side == 0)
                    {
                        triangles.Add(centerIndex); triangles.Add(ringStart + next); triangles.Add(ringStart + segment);
                    }
                    else
                    {
                        triangles.Add(centerIndex); triangles.Add(ringStart + segment); triangles.Add(ringStart + next);
                    }
                }
            }
        }

        private static void AppendOrientedBox(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Vector3 center, Vector3 axisX,
            Vector3 axisY, Vector3 axisZ, float halfX, float halfY, float halfZ)
        {
            Vector3 x = axisX * halfX;
            Vector3 y = axisY * halfY;
            Vector3 z = axisZ * halfZ;
            AddBoxFace(vertices, normals, uvs, triangles, center + z, axisZ,
                center - x - y + z, center + x - y + z, center + x + y + z, center - x + y + z);
            AddBoxFace(vertices, normals, uvs, triangles, center - z, -axisZ,
                center + x - y - z, center - x - y - z, center - x + y - z, center + x + y - z);
            AddBoxFace(vertices, normals, uvs, triangles, center + x, axisX,
                center + x - y + z, center + x - y - z, center + x + y - z, center + x + y + z);
            AddBoxFace(vertices, normals, uvs, triangles, center - x, -axisX,
                center - x - y - z, center - x - y + z, center - x + y + z, center - x + y - z);
            AddBoxFace(vertices, normals, uvs, triangles, center + y, axisY,
                center - x + y + z, center + x + y + z, center + x + y - z, center - x + y - z);
            AddBoxFace(vertices, normals, uvs, triangles, center - y, -axisY,
                center - x - y - z, center + x - y - z, center + x - y + z, center - x - y + z);
        }

        private static void AddBoxFace(List<Vector3> vertices, List<Vector3> normals,
            List<Vector2> uvs, List<int> triangles, Vector3 center, Vector3 normal,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            normals.Add(normal); normals.Add(normal); normals.Add(normal); normals.Add(normal);
            uvs.Add(Vector2.zero); uvs.Add(Vector2.right); uvs.Add(Vector2.one); uvs.Add(Vector2.up);
            AddQuad(triangles, start, start + 1, start + 2, start + 3);
        }

        private static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private static Vector3 ReadHumveeVector(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        private static HumveeSection Section(int part)
        {
            HumveeSection section = _humveeSections[part];
            if (section == null || section.Part != part) throw new InvalidDataException("Humvee part " + part + " is missing.");
            return section;
        }

        private static GameObject CreateMeshNode(string name, Transform parent, Mesh mesh, int layer)
        {
            GameObject node = new GameObject(name);
            node.layer = layer;
            node.transform.SetParent(parent, false);
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = node.AddComponent<MeshRenderer>();
            if (name.StartsWith("Humvee_Wheel_", StringComparison.Ordinal))
                renderer.sharedMaterials = new Material[] { _wheelRubberMaterial, _wheelHubMaterial };
            else if (mesh != null && mesh.subMeshCount == 6)
                renderer.sharedMaterials = new Material[] { _humveeMaterial, _interiorMaterial, _plateMaterial,
                    _bodyHardwareMaterial, _headlightMaterial, _indicatorMaterial };
            else if (mesh != null && mesh.subMeshCount == 2)
                renderer.sharedMaterials = new Material[] { _humveeMaterial, _interiorMaterial };
            else renderer.sharedMaterial = _humveeMaterial;
            return node;
        }

        private static AimablePlatform FindLauncherElevation(Vehicle vehicle, WeaponSystem weapon)
        {
            Transform muzzle = weapon.MuzzleIdentity != null ? weapon.MuzzleIdentity : weapon.transform;
            AimablePlatform selected = null;
            foreach (AimablePlatform platform in vehicle.GetComponentsInChildren<AimablePlatform>(true))
            {
                if (platform == null || platform.Transform == null || platform.BallTurret ||
                    platform.Axis != GHPC.Utility.EnumHelpers.Axis3.X) continue;
                if (!muzzle.IsChildOf(platform.Transform) && muzzle != platform.Transform &&
                    !weapon.transform.IsChildOf(platform.Transform) && weapon.transform != platform.Transform) continue;
                if (selected == null || platform.Transform.IsChildOf(selected.Transform)) selected = platform;
            }
            return selected;
        }

        private static void ConfigureLauncherTravel(AimablePlatform platform)
        {
            Vector2 limits = platform.ReverseLocalEuler ? new Vector2(LauncherMinElevation, LauncherMaxElevation) :
                new Vector2(-LauncherMaxElevation, -LauncherMinElevation);
            if (limits.x >= limits.y)
                throw new InvalidOperationException("Native elevation limits are incompatible with the LOSAT depression stop.");
            platform.LocalEulerLimits = limits;
        }

        private static Transform MoveWeaponToRoof(Vehicle vehicle, WeaponSystem weapon, Transform visualRoot, float scale)
        {
            AimablePlatform yaw = null;
            foreach (AimablePlatform platform in vehicle.GetComponentsInChildren<AimablePlatform>(true))
            {
                if (platform == null || platform.Transform == null || platform.Axis != GHPC.Utility.EnumHelpers.Axis3.Y) continue;
                if (weapon.transform.IsChildOf(platform.Transform) || weapon.transform == platform.Transform)
                {
                    yaw = platform;
                    break;
                }
            }
            Transform movingRoot = yaw != null && yaw.Transform != null ? yaw.Transform : weapon.transform;
            if(yaw==null) throw new InvalidOperationException("Independent sight yaw platform is missing.");
            yaw.LocalEulerLimits=Vector2.zero; // Equal endpoints mean continuous native 360-degree yaw.
            Transform anchor = new GameObject("Humvee_Roof_WeaponMount").transform;
            anchor.SetParent(vehicle.transform, false);
            anchor.position = visualRoot.TransformPoint(new Vector3(0f, LauncherAnchorHeight, LauncherAnchorSourceZ));
            anchor.localRotation = Quaternion.identity;
            movingRoot.SetParent(anchor, false);
            movingRoot.localPosition = Vector3.zero;
            movingRoot.localRotation = Quaternion.identity;
            return movingRoot;
        }

        private static int HideNativeLauncherMesh(Transform movingRoot)
        {
            int hidden = 0;
            foreach (Renderer renderer in movingRoot.GetComponentsInChildren<Renderer>(true))
            {
                // Keep scripts, aiming nodes, muzzle and particle systems intact.
                // Only the old launcher meshes are replaced by the four-tube model.
                if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) ||
                    !renderer.enabled) continue;
                renderer.enabled = false;
                hidden++;
            }
            return hidden;
        }

        private static Transform BuildFourTubeLauncherVisual(Vehicle vehicle, Transform movingRoot,out Transform sightHead)
        {
            Material[] materials = { _launcherBodyMaterial, _launcherDarkMaterial,
                _launcherLensMaterial, _launcherHardwareMaterial, _launcherLightPaintMaterial,
                _launcherStructureMaterial, _launcherStencilMaterial, _launcherCautionMaterial };
            Vector3 offset = LauncherModelOffset;
            Transform baseMount = new GameObject("LOSAT_Stationary_Lower_Flange").transform;
            baseMount.gameObject.layer = vehicle.gameObject.layer;
            baseMount.SetParent(movingRoot.parent, false);
            baseMount.localPosition = offset;
            baseMount.gameObject.AddComponent<MeshFilter>().sharedMesh = _launcherBaseMesh;
            baseMount.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
            Transform fixedMount = new GameObject("LOSAT_Independent_Rotating_Sight").transform;
            fixedMount.gameObject.layer = vehicle.gameObject.layer;
            fixedMount.SetParent(movingRoot, false);
            // Mesh is re-centred on the optical head's actual trunnion axis.
            // Stationary spindle meets this pivot through the full pitch range.
            fixedMount.localPosition = _launcherPivot + offset + new Vector3(0f,.175f,.03f);
            fixedMount.gameObject.AddComponent<MeshFilter>().sharedMesh = _launcherFixedMesh;
            fixedMount.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
            sightHead=fixedMount;

            Transform pivot = new GameObject("LOSAT_Four_Canisters_Centred_Trunnions").transform;
            pivot.gameObject.layer = vehicle.gameObject.layer;
            pivot.SetParent(movingRoot.parent, false); // Body-fixed, never inherits sight yaw.
            pivot.localPosition = _launcherPivot + offset;
            pivot.localRotation = Quaternion.Euler(-FixedLauncherRules.TubeElevation,0f,0f);
            pivot.gameObject.AddComponent<MeshFilter>().sharedMesh = _launcherMovingMesh;
            pivot.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return pivot;
        }

        private static void LoadBlenderLauncherMeshes()
        {
            if (_launcherFixedMesh != null && _launcherMovingMesh != null && _launcherBaseMesh != null) return;
            List<Vector3>[] vertices = { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
            List<Vector3>[] normals = { new List<Vector3>(), new List<Vector3>(), new List<Vector3>() };
            List<Vector2>[] uvs = { new List<Vector2>(), new List<Vector2>(), new List<Vector2>() };
            List<int>[][] triangles = new List<int>[3][];
            for (int part = 0; part < 3; part++)
            {
                triangles[part] = new List<int>[LauncherMaterialCount];
                for (int material = 0; material < LauncherMaterialCount; material++)
                    triangles[part][material] = new List<int>();
            }
            using (Stream stream = typeof(Mod).Assembly.GetManifestResourceStream("M151LOSATTest.Launcher.Mesh"))
            using (BinaryReader reader = stream == null ? null : new BinaryReader(stream))
            {
                if (reader == null || reader.ReadInt32() != 151240)
                    throw new InvalidDataException("Embedded Blender launcher geometry is missing or invalid.");
                int sectionCount = reader.ReadInt32();
                if (sectionCount < 3 || sectionCount > 3 * LauncherMaterialCount)
                    throw new InvalidDataException("Blender launcher section count is invalid.");
                _launcherPivot = ReadHumveeVector(reader);
                for (int section = 0; section < sectionCount; section++)
                {
                    int part = reader.ReadInt32();
                    int material = reader.ReadInt32();
                    int count = reader.ReadInt32();
                    if (part < 0 || part > 2 || material < 0 || material >= LauncherMaterialCount ||
                        count < 3 || count % 3 != 0 || count > 65532 ||
                        vertices[part].Count + count > 65532)
                        throw new InvalidDataException("Blender launcher mesh section is invalid.");
                    int start = vertices[part].Count;
                    for (int i = 0; i < count; i++)
                    {
                        vertices[part].Add(ReadHumveeVector(reader));
                        triangles[part][material].Add(start + i);
                    }
                    for (int i = 0; i < count; i++) normals[part].Add(ReadHumveeVector(reader));
                    for (int i = 0; i < count; i++)
                        uvs[part].Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
                }
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Blender launcher geometry has trailing data.");
            }
            Mesh[] meshes = new Mesh[3];
            for (int part = 0; part < 3; part++)
            {
                if (vertices[part].Count < 3)
                    throw new InvalidDataException("Blender launcher fixed or moving mesh is empty.");
                Mesh mesh = new Mesh();
                mesh.name = part == 0 ? "LOSAT_Independent_Optical_Head" :
                    part == 1 ? "LOSAT_Fixed_Four_Canisters" : "LOSAT_Roof_Load_Rails_And_Sensor_Base";
                mesh.vertices = vertices[part].ToArray();
                mesh.normals = normals[part].ToArray();
                mesh.uv = uvs[part].ToArray();
                mesh.subMeshCount = LauncherMaterialCount;
                for (int material = 0; material < LauncherMaterialCount; material++)
                    mesh.SetTriangles(triangles[part][material], material);
                mesh.RecalculateTangents(); // Required by the paint normal map; once, shared by all vehicles.
                mesh.RecalculateBounds();
                UnityEngine.Object.DontDestroyOnLoad(mesh);
                meshes[part] = mesh;
            }
            _launcherFixedMesh = meshes[0];
            _launcherMovingMesh = meshes[1];
            _launcherBaseMesh = meshes[2];
        }

        private List<Transform> PlaceCrewInside(Vehicle vehicle, Transform visualRoot)
        {
            List<Transform> keep = new List<Transform>();
            CrewManager manager = vehicle.GetComponentInChildren<CrewManager>(true);
            if (manager == null || manager.CrewMembers == null) return keep;
            int detached = 0;
            foreach (CrewManager.CrewMember member in manager.CrewMembers)
            {
                if (member == null) continue;
                Transform character = member.AutoBoneRig != null && member.AutoBoneRig.CharacterRoot != null
                    ? member.AutoBoneRig.CharacterRoot.transform
                    : member.BodyPartRegistry != null ? member.BodyPartRegistry.transform : null;
                if (character == null) continue;
                keep.Add(character);
                List<Transform> roots = new List<Transform>();
                AddCrewRoot(roots, member.AutoBoneRig != null ? member.AutoBoneRig.transform : null);
                AddCrewRoot(roots, character);
                AddCrewRoot(roots, member.BodyPartRegistry != null ? member.BodyPartRegistry.transform : null);
                AddCrewRoot(roots, member.AutoBoneRig != null && member.AutoBoneRig.ZonesRoot != null
                    ? member.AutoBoneRig.ZonesRoot.transform : null);
                if (member.AnimationTriggers != null && member.AnimationTriggers.Animator != null)
                    AddCrewRoot(roots, member.AnimationTriggers.Animator.transform);
                if (member.AllAarVisuals != null)
                    foreach (AarVisual aar in member.AllAarVisuals)
                        if (aar != null) AddCrewRoot(roots, aar.transform);
                Vector3 sourceSeat;
                switch (member.Position)
                {
                    // These are desired visual-bounds centers, not rig origins. The source
                    // Humvee faces -Z and is rotated 180 degrees by visualRoot.
                    case CrewPosition.Driver: sourceSeat = new Vector3(.498f, .68f, -.30f); break;
                    case CrewPosition.Commander: sourceSeat = new Vector3(-.42f, 1.06f, -.35f); break;
                    case CrewPosition.Gunner: sourceSeat = new Vector3(-.60f, 1.06f, -.29f); break;
                    default: sourceSeat = new Vector3(-.18f, .76f, .15f); break;
                }
                Vector3 destination = visualRoot.TransformPoint(sourceSeat);
                Bounds visualBounds;
                Vector3 center = TryGetVisualBounds(character, out visualBounds)
                    ? visualBounds.center : character.position;
                Vector3 delta = destination - center;
                int movedRoots = 0;
                List<Transform> seatedRoots=new List<Transform>();
                // AAR replay meshes, bone hit zones and the rendered soldier are not
                // always children of AutoBoneRig. Move each independent root together.
                foreach (Transform candidate in roots)
                {
                    if (candidate == vehicle.transform) continue;
                    bool childOfAnother = false;
                    foreach (Transform other in roots)
                        if (candidate != other && candidate.IsChildOf(other)) { childOfAnother = true; break; }
                    if (childOfAnother) continue;
                    if (candidate.parent != vehicle.transform)
                    {
                        candidate.SetParent(vehicle.transform, true);
                        detached++;
                    }
                    candidate.position += delta;
                    seatedRoots.Add(candidate);
                    movedRoots++;
                }
                RegisterCrewSeat(vehicle,visualRoot,member,seatedRoots,sourceSeat);
            }
            return keep;
        }

        private void ConfigureCabinCameras(Vehicle vehicle, Transform visualRoot)
        {
            foreach(GHPC.Camera.CameraSlot slot in vehicle.GetComponentsInChildren<GHPC.Camera.CameraSlot>(true))
            {
                bool driver=string.Equals(slot.name,"driver head",StringComparison.OrdinalIgnoreCase);
                bool commander=string.Equals(slot.name,"commander head",StringComparison.OrdinalIgnoreCase);
                if(!driver && !commander) continue; // Never move either fire-control optic.
                Transform anchor=new GameObject(driver ? "LOSAT_Driver_Cabin_Eye" : "LOSAT_Commander_Cabin_Eye").transform;
                anchor.SetParent(vehicle.transform,false);
                // Both requested head views occupy the driver's seat, behind
                // the windshield instead of the old gunner/roof position.
                anchor.position=visualRoot.TransformPoint(new Vector3(.498f,1.55f,-.30f));
                anchor.rotation=vehicle.transform.rotation;
                slot.transform.SetParent(anchor,false);
                slot.transform.localPosition=Vector3.zero;
                slot.transform.localRotation=Quaternion.identity;
                slot.AutoCenterTransform=anchor;
                RequiredSet(slot,"_reparentTarget",anchor);
                slot.IsExterior=false;
                // Unit.Awake / PlayerInput.SetPlayerUnit exclude DirectAttachOnly
                // slots from cycling. Main head views must remain selectable;
                // leave the native NOD child flags untouched.
                slot.DirectAttachOnly=false;
                foreach(GHPC.Camera.CameraSlot child in slot.GetComponentsInChildren<GHPC.Camera.CameraSlot>(true))
                    child.IsExterior=false;
                RegisterCabinView(slot,vehicle);
            }
        }

        private static void AddCrewRoot(List<Transform> roots, Transform candidate)
        {
            if (candidate != null && !roots.Contains(candidate)) roots.Add(candidate);
        }

        private static bool TryGetVisualBounds(Transform root, out Bounds bounds)
        {
            bounds = new Bounds();
            if (root == null) return false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }

        private static int HideNativeVisuals(Renderer[] nativeRenderers, Transform movingWeaponRoot, WeaponSystem weapon,
            List<Transform> crewRoots, WheelController[] controllers)
        {
            HashSet<Renderer> wheelRenderers = new HashSet<Renderer>();
            foreach (WheelController controller in controllers)
                if (controller != null && controller.Visual != null)
                    foreach (Renderer renderer in controller.Visual.GetComponentsInChildren<Renderer>(true))
                        wheelRenderers.Add(renderer);

            int hidden = 0;
            foreach (Renderer renderer in nativeRenderers)
            {
                if (renderer == null) continue;
                bool keep = IsRendererPartOf(renderer, movingWeaponRoot) ||
                            IsRendererPartOf(renderer, weapon.transform);
                if (!keep)
                    foreach (Transform crew in crewRoots)
                        if (IsRendererPartOf(renderer, crew)) { keep = true; break; }
                if (keep) continue;
                if (wheelRenderers.Contains(renderer) || renderer.enabled)
                {
                    renderer.enabled = false;
                    hidden++;
                }
            }
            return hidden;
        }

        private static bool IsRendererPartOf(Renderer renderer, Transform visualRoot)
        {
            if (renderer == null || visualRoot == null) return false;
            Transform rendererTransform = renderer.transform;
            return rendererTransform == visualRoot || rendererTransform.IsChildOf(visualRoot);
        }

        private static string TransformPath(Transform transform)
        {
            string path = transform == null ? string.Empty : transform.name;
            while (transform != null && transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private static UniformArmor FindNativeBodyArmor(Vehicle vehicle)
        {
            int raycastLayer = GHPC.Utility.CodeUtils.LAYER_INDEX_RAYCAST_ONLY;
            UniformArmor meshFallback = null;
            UniformArmor armorFallback = null;
            int matchingUnit = 0;
            int matchingMesh = 0;
            // The stock armor rig may be reparented at runtime. A child-only
            // search can miss it even though the M151 itself is present.
            foreach (UniformArmor armor in Resources.FindObjectsOfTypeAll<UniformArmor>())
            {
                if (armor == null || armor.gameObject == null ||
                    !armor.gameObject.scene.IsValid() ||
                    armor.gameObject.layer != raycastLayer ||
                    !object.ReferenceEquals(armor.Unit, vehicle.BaseUnit)) continue;
                matchingUnit++;
                if (armorFallback == null) armorFallback = armor;
                if (armor.GetComponent<MeshCollider>() == null) continue;
                matchingMesh++;
                if (armor.name.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return armor;
                }
                if (meshFallback == null) meshFallback = armor;
            }
            UniformArmor selected = meshFallback ?? armorFallback;
            return selected;
        }

        private static MeshCollider ReplaceNativeBodyArmorMesh(Vehicle vehicle, Transform visualRoot,
            Mesh bodyMesh, int[] collisionTriangles, UniformArmor nativeArmor)
        {
            MeshCollider collider = nativeArmor.GetComponent<MeshCollider>();
            if (collider == null || collider.sharedMesh == null)
                throw new InvalidOperationException("Native M151 body mesh collider is missing.");
            if (!object.ReferenceEquals(nativeArmor.Unit, vehicle.BaseUnit) ||
                collider.gameObject.layer != GHPC.Utility.CodeUtils.LAYER_INDEX_RAYCAST_ONLY)
                throw new InvalidOperationException("Native M151 body armor does not belong to this vehicle.");

            RaycastColliderGroup group = vehicle.GetComponent<RaycastColliderGroup>();
            if (group == null)
                group = vehicle.GetComponentInChildren<RaycastColliderGroup>(true);
            if (group == null) throw new InvalidOperationException("Native M151 armor group is missing.");
            System.Reflection.FieldInfo boundsField = typeof(RaycastColliderGroup)
                .GetField("boundingCollider", AnyInstance);
            SphereCollider sphere = boundsField == null
                ? null : boundsField.GetValue(group) as SphereCollider;
            if (sphere == null)
                throw new InvalidOperationException("Native M151 raycast bound is not a sphere.");

            // Keep the original armor component, collider, LateFollow rig and
            // raycast-group membership. Only change the collider's geometry.
            // Moving the Humvee vertices into the native collider's local space
            // makes damage and visuals coincide without a second hit surface.
            Vector3[] originalVertices = bodyMesh.vertices;
            if (originalVertices == null || originalVertices.Length == 0)
                throw new InvalidOperationException("Humvee body mesh has no vertices.");
            Matrix4x4 toNative = nativeArmor.transform.worldToLocalMatrix * visualRoot.localToWorldMatrix;
            Vector3[] nativeVertices = new Vector3[originalVertices.Length];
            Bounds world = new Bounds(visualRoot.TransformPoint(originalVertices[0]), Vector3.zero);
            float alignmentError = 0f;
            for (int i = 0; i < originalVertices.Length; i++)
            {
                nativeVertices[i] = toNative.MultiplyPoint3x4(originalVertices[i]);
                Vector3 visualWorld = visualRoot.TransformPoint(originalVertices[i]);
                world.Encapsulate(visualWorld);
                alignmentError = Mathf.Max(alignmentError,
                    Vector3.Distance(visualWorld,
                        nativeArmor.transform.TransformPoint(nativeVertices[i])));
            }
            if (alignmentError > .01f)
                throw new InvalidOperationException("Humvee native armor mesh alignment exceeds 1 cm.");
            if (collisionTriangles == null || collisionTriangles.Length != originalVertices.Length)
                throw new InvalidOperationException("Humvee collision triangles do not match source vertices.");
            // Rendering may split triangles into camo, interior and number-plate
            // materials; damage always retains the untouched source triangle list.
            int[] triangles = (int[])collisionTriangles.Clone();
            if (toNative.determinant < 0f)
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int reverse = triangles[i + 1];
                    triangles[i + 1] = triangles[i + 2];
                    triangles[i + 2] = reverse;
                }
            Mesh replacement = new Mesh();
            replacement.name = "Humvee_Native_M151_Body_Armor";
            replacement.vertices = nativeVertices;
            replacement.triangles = triangles;
            replacement.RecalculateBounds();

            float radius = sphere.radius;
            for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = world.center + Vector3.Scale(world.extents,
                            new Vector3(x, y, z));
                        radius = Mathf.Max(radius, Vector3.Distance(sphere.center,
                            sphere.transform.InverseTransformPoint(corner)) + .1f);
                    }
            Mesh originalMesh = collider.sharedMesh;
            collider.sharedMesh = null;
            collider.sharedMesh = replacement;
            if (collider.sharedMesh != replacement)
            {
                collider.sharedMesh = originalMesh;
                throw new InvalidOperationException("Native M151 body collider rejected the Humvee mesh.");
            }
            sphere.radius = radius;
            Physics.SyncTransforms();
            return collider;
        }

        private static Texture2D LoadHumveeSurfaceMap(string suffix)
        {
            using (Stream stream = typeof(Mod).Assembly.GetManifestResourceStream("M151LOSATTest.Humvee." + suffix))
            {
                if (stream == null) throw new InvalidDataException("Missing Humvee surface map: " + suffix);
                byte[] bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int count = stream.Read(bytes, read, bytes.Length - read);
                    if (count <= 0) throw new EndOfStreamException("Incomplete Humvee surface map: " + suffix);
                    read += count;
                }
                // Data maps must be linear, unlike the sRGB camouflage atlas.
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true);
                if (!ImageConversion.LoadImage(texture, bytes, true)) throw new InvalidDataException("Invalid surface map: " + suffix);
                texture.name = "Humvee_Paint_" + suffix;
                texture.wrapMode = TextureWrapMode.Repeat;
                texture.filterMode = FilterMode.Trilinear;
                texture.anisoLevel = 4;
                UnityEngine.Object.DontDestroyOnLoad(texture);
                return texture;
            }
        }

        private void UpdateHumveeVisuals(WeaponSystem onlyWeapon = null)
        {
            var mainCamera = GHPC.Camera.CameraManager.MainCam;
            Vector3 cameraPosition = mainCamera == null ? Vector3.zero : mainCamera.transform.position;
            Unit playerUnit = GHPC.Player.PlayerInput.Instance == null ? null : GHPC.Player.PlayerInput.Instance.CurrentPlayerUnit;
            for (int swapIndex = _humveeSwaps.Count - 1; swapIndex >= 0; swapIndex--)
            {
                HumveeSwap swap = _humveeSwaps[swapIndex];
                if (swap.Vehicle == null || swap.Root == null)
                {
                    _humveeSwaps.RemoveAt(swapIndex);
                    continue;
                }
                if (onlyWeapon != null && swap.LauncherWeapon != onlyWeapon) continue;
                if (onlyWeapon == null && mainCamera != null && swap.Vehicle.BaseUnit != playerUnit &&
                    (swap.Vehicle.transform.position-cameraPosition).sqrMagnitude > 40000f)
                {
                    if (Time.unscaledTime < swap.NextVisualUpdate) continue;
                    swap.NextVisualUpdate = Time.unscaledTime + .05f;
                }
                if (!swap.Root.gameObject.activeSelf) swap.Root.gameObject.SetActive(true);
                if (swap.BodyRenderer != null && !swap.BodyRenderer.enabled)
                    swap.BodyRenderer.enabled = true;
                if (swap.LauncherPivot != null && swap.LauncherYaw != null && swap.LauncherWeapon != null)
                {
                    // Never read the replacement muzzle as the aim source: it is a
                    // child of the cosmetic pivot and would create a feedback loop.
                    Transform muzzle = swap.LauncherAimReference;
                    if (muzzle == null) muzzle = swap.LauncherWeapon.transform;
                    Vector3 direction = swap.LauncherYaw.InverseTransformDirection(muzzle.forward);
                    float horizontal = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
                    float elevation = Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
                    float limitedElevation = Mathf.Clamp(elevation, LauncherMinElevation, LauncherMaxElevation);
                    if (Mathf.Abs(elevation - limitedElevation) > .001f && swap.LauncherElevation != null)
                    {
                        // Keep native virtual aim, physical muzzle and cosmetic rig together,
                        // including an initial low pose or a forced AI/stow aim that bypasses limits.
                        Vector3 stopped = Quaternion.AngleAxis(elevation - limitedElevation,
                            swap.LauncherYaw.right) * swap.LauncherElevation.Transform.forward;
                        swap.LauncherElevation.ForceAimVectorNow(stopped);
                        direction = swap.LauncherYaw.InverseTransformDirection(muzzle.forward);
                        horizontal = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
                        elevation = Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg;
                    }
                    elevation = Mathf.Clamp(elevation, LauncherMinElevation, LauncherMaxElevation);
                    if(swap.SightHead!=null) swap.SightHead.localRotation = Quaternion.Euler(-elevation,0f,0f);
                }
                if (onlyWeapon != null) continue; // A firing check needs one launcher, not all wheels.
                Matrix4x4 vehicleToLocal = swap.Vehicle.transform.worldToLocalMatrix;
                Matrix4x4 vehicleToWorld = swap.Vehicle.transform.localToWorldMatrix;
                Matrix4x4 rootToWorld = swap.Root.localToWorldMatrix;
                float visualBlend = SuspensionRules.VisualBlend(Time.time - swap.LastWheelVisualTime);
                swap.LastWheelVisualTime = Time.time;
                for (int i = 0; i < 4; i++)
                {
                    WheelController controller = swap.Controllers[i];
                    Transform target = swap.WheelTargets[i];
                    Transform wheel = swap.WheelNodes[i];
                    if (controller == null || target == null || wheel == null) continue;
                    Vector3 currentTargetLocal = vehicleToLocal.MultiplyPoint3x4(target.position);
                    Vector3 suspensionDeltaLocal = currentTargetLocal - swap.WheelTargetBaseLocalPositions[i];
                    float oldTravel = swap.SmoothedWheelTravel[i];
                    float newTravel = suspensionDeltaLocal.y;
                    // Short vertical-only interpolation removes fixed-step visual chatter.
                    // Steering/spin stay native, large relocations snap instead of trailing.
                    if (Mathf.Abs(newTravel - oldTravel) > .5f) oldTravel = newTravel;
                    swap.SmoothedWheelTravel[i] = Mathf.Lerp(oldTravel, newTravel, visualBlend);
                    suspensionDeltaLocal.y = swap.SmoothedWheelTravel[i];
                    wheel.position = rootToWorld.MultiplyPoint3x4(swap.WheelSourceCenters[i]) +
                        vehicleToWorld.MultiplyVector(suspensionDeltaLocal);
                    if(WheelVisualAngleField==null) wheel.rotation=target.rotation;
                    else {
                        float nativeSpin=(float)WheelVisualAngleField.GetValue(controller);
                        float ratio=FixedLauncherRules.SpinRatio(controller.TireRadius,SourceWheelRadius*HumveeVisualScale);
                        swap.VisualSpin[i]+=(nativeSpin-swap.LastNativeSpin[i])*ratio;
                        swap.LastNativeSpin[i]=nativeSpin;
                        wheel.rotation=target.rotation*Quaternion.Inverse(controller.Rotation)*
                            Quaternion.AngleAxis(swap.VisualSpin[i]-nativeSpin,controller.Right)*controller.Rotation;
                    }
                }
            }
        }
    }
}
