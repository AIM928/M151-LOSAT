using System;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private static Mesh[] BodyDetailMeshes;
        private static Material[] BodyDetailMaterials;

        private static void AddBodyGlassAndPlates(Transform root, int layer)
        {
            if (BodyDetailMeshes == null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("Standard shader is missing for body details.");
                Material glass = MakeBodyDetailMaterial(shader,"Humvee_Inside_Clear_Tinted_Glass",new Color(.27f,.36f,.34f,.18f),.04f,.88f);
                glass.SetFloat("_Mode",3f);
                glass.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);
                glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
                glass.SetInt("_ZWrite",0);
                glass.DisableKeyword("_ALPHATEST_ON");
                glass.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                glass.EnableKeyword("_ALPHABLEND_ON");
                glass.SetOverrideTag("RenderType","Transparent");
                glass.renderQueue=3000;
                Material outside=new Material(glass) {name="Humvee_Outside_Natural_Tinted_Glass",color=new Color(.27f,.36f,.34f,.33f)};
                BodyDetailMaterials = new[] {
                    outside,
                    MakeBodyDetailMaterial(shader,"Humvee_Window_EPDM_Gaskets",new Color(.09f,.105f,.095f),0f,.14f),
                    MakeBodyDetailMaterial(shader,"Humvee_Black_Enamel_Plates",new Color(.035f,.045f,.037f),.10f,.48f),
                    MakeBodyDetailMaterial(shader,"Humvee_Yellow_Plate_Lettering",new Color(.88f,.68f,.19f),.05f,.38f),
                    MakeBodyDetailMaterial(shader,"Humvee_Plate_Screw_Heads",new Color(.27f,.29f,.27f),.55f,.53f),
                    glass,
                    MakeBodyDetailMaterial(shader,"Humvee_Field_Olive_Panel_And_Mount_Details",new Color(.22f,.265f,.195f),.035f,.27f),
                    MakeBodyDetailMaterial(shader,"Humvee_Satin_Fasteners_And_Bumper_Hardware",new Color(.24f,.265f,.24f),.55f,.32f)
                };
                ApplySurfaceFinish(BodyDetailMaterials[1],SurfaceFinishRules.Rubber,.16f);
                ApplySurfaceFinish(BodyDetailMaterials[6],SurfaceFinishRules.Paint,.30f);
                ApplySurfaceFinish(BodyDetailMaterials[7],SurfaceFinishRules.Metal,.12f);
                BodyDetailMeshes = new Mesh[BodyDetailMaterials.Length];
                using (Stream stream=typeof(Mod).Assembly.GetManifestResourceStream("M151LOSATTest.Humvee.Details"))
                using (BinaryReader reader=stream == null ? null : new BinaryReader(stream))
                {
                    if(reader == null || reader.ReadInt32()!=151250) throw new InvalidDataException("Body detail mesh is missing.");
                    int sections=reader.ReadInt32();
                    if(sections!=BodyDetailMaterials.Length) throw new InvalidDataException("Body detail material count is invalid.");
                    for(int section=0;section<sections;section++)
                    {
                        int material=reader.ReadInt32(),count=reader.ReadInt32();
                        if(material<0 || material>=sections || BodyDetailMeshes[material]!=null || count<3 || count>65532 || count%3!=0)
                            throw new InvalidDataException("Body detail mesh section is invalid.");
                        Vector3[] positions=new Vector3[count],normals=new Vector3[count];
                        Vector2[] uvs=new Vector2[count];int[] triangles=new int[count];
                        for(int i=0;i<count;i++) { positions[i]=ReadHumveeVector(reader);triangles[i]=i; }
                        for(int i=0;i<count;i++) normals[i]=ReadHumveeVector(reader);
                        for(int i=0;i<count;i++) uvs[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());
                        Mesh mesh=new Mesh { name="Humvee_Glass_And_Plates_"+material };
                        mesh.vertices=positions;mesh.normals=normals;mesh.uv=uvs;mesh.triangles=triangles;mesh.RecalculateBounds();mesh.RecalculateTangents();
                        UnityEngine.Object.DontDestroyOnLoad(mesh);BodyDetailMeshes[material]=mesh;
                    }
                    if(stream.Position!=stream.Length) throw new InvalidDataException("Body detail mesh has trailing bytes.");
                }
            }
            for(int i=0;i<BodyDetailMeshes.Length;i++)
            {
                GameObject node=new GameObject(i==0 ? "Humvee_Six_Outside_Clear_Panes" : i==5 ? "Humvee_Six_Inside_Clear_Panes" : "Humvee_Body_Detail_"+i);
                node.layer=layer;node.transform.SetParent(root,false);
                node.AddComponent<MeshFilter>().sharedMesh=BodyDetailMeshes[i];
                MeshRenderer renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=BodyDetailMaterials[i];
                if(i==0 || i==5) renderer.shadowCastingMode=ShadowCastingMode.Off;
                // Cosmetic panes/plates deliberately have no collider or crew hit-zone.
            }
        }
    }
}
