using System;
using System.IO;
using UnityEngine;
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private static Mesh[] _cabinMeshes;
        private static Material[] _cabinMaterials;
        private static bool LegacyCabinVertex(Vector3 p)
        {
            bool wheel=p.x>=.49f && p.x<=.89f && p.y>=1.05f && p.y<=1.43f && p.z>=-.77f && p.z<=-.60f;
            bool gauges=p.x>=.13f && p.x<=.53f && p.y>=1.16f && p.y<=1.34f && p.z>=-.94f && p.z<=-.77f;
            return wheel || gauges;
        }
        private static bool LegacyCabinTriangle(Vector3 a,Vector3 b,Vector3 c)
        {return LegacyCabinVertex(a) && LegacyCabinVertex(b) && LegacyCabinVertex(c);}
        private static void AddCabinInterior(Transform root,int layer)
        {
            if(_cabinMeshes==null)
            {
                Shader shader=Shader.Find("Standard");
                if(shader==null) throw new InvalidOperationException("Cabin Standard shader unavailable");
                _cabinMaterials=new[] {
                    MakeBodyDetailMaterial(shader,"Cabin_Satin_Charcoal_Trim",new Color(.075f,.087f,.080f),.03f,.25f),
                    MakeBodyDetailMaterial(shader,"Cabin_Charcoal_Vinyl_Rubber",new Color(.085f,.10f,.092f),0f,.22f),
                    MakeBodyDetailMaterial(shader,"Cabin_Satin_Control_Metal",new Color(.28f,.30f,.28f),.65f,.33f),
                    MakeBodyDetailMaterial(shader,"Cabin_Recessed_Gauge_Faces",new Color(.018f,.025f,.023f),.02f,.45f),
                    MakeBodyDetailMaterial(shader,"Cabin_Ivory_Dial_Markings",new Color(.68f,.72f,.64f),0f,.12f),
                    MakeBodyDetailMaterial(shader,"Cabin_Amber_Indicators",new Color(.55f,.32f,.08f),0f,.22f),
                    MakeBodyDetailMaterial(shader,"Cabin_Green_Status_Display",new Color(.09f,.28f,.16f),0f,.28f)
                };
                ApplySurfaceFinish(_cabinMaterials[0],SurfaceFinishRules.Paint,.20f);
                ApplySurfaceFinish(_cabinMaterials[1],SurfaceFinishRules.Rubber,.25f);
                ApplySurfaceFinish(_cabinMaterials[2],SurfaceFinishRules.Metal,.10f);
                _cabinMaterials[6].EnableKeyword("_EMISSION");
                _cabinMaterials[6].SetColor("_EmissionColor",new Color(.06f,.22f,.10f));
                using(Stream stream=typeof(Mod).Assembly.GetManifestResourceStream("M151LOSATTest.Humvee.Cabin"))
                using(BinaryReader r=stream==null ? null : new BinaryReader(stream))
                {
                    if(r==null || r.ReadInt32()!=151260 || r.ReadInt32()!=7) throw new InvalidDataException("Cabin geometry missing");
                    _cabinMeshes=new Mesh[7];
                    for(int j=0;j<7;j++)
                    {
                        int material=r.ReadInt32(),count=r.ReadInt32();
                        if(material<0 || material>=7 || _cabinMeshes[material]!=null || count<3 || count>65532 || count%3!=0)
                            throw new InvalidDataException("Cabin mesh section invalid");
                        var p=new Vector3[count];var n=new Vector3[count];var uv=new Vector2[count];var triangles=new int[count];
                        for(int i=0;i<count;i++) {p[i]=ReadHumveeVector(r);triangles[i]=i;}
                        for(int i=0;i<count;i++) n[i]=ReadHumveeVector(r);
                        for(int i=0;i<count;i++) uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());
                        Mesh mesh=new Mesh {name="Humvee_Detailed_Cabin_"+material,vertices=p,normals=n,uv=uv,triangles=triangles};
                        mesh.RecalculateBounds();mesh.RecalculateTangents();UnityEngine.Object.DontDestroyOnLoad(mesh);
                        _cabinMeshes[material]=mesh;
                    }
                    if(stream.Position!=stream.Length) throw new InvalidDataException("Cabin geometry trailing bytes");
                }
            }
            for(int i=0;i<_cabinMeshes.Length;i++)
            {
                var node=new GameObject("Humvee_Detailed_Cabin_"+i);node.layer=layer;node.transform.SetParent(root,false);
                node.AddComponent<MeshFilter>().sharedMesh=_cabinMeshes[i];
                node.AddComponent<MeshRenderer>().sharedMaterial=_cabinMaterials[i];
            }
            // Cached cosmetic geometry only: no colliders, crew bones or Update loop.
        }
    }
}
