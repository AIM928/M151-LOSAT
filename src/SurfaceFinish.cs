using UnityEngine;
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private static readonly Texture2D[,] _surfaceFinishMaps=new Texture2D[3,3];
        private static Texture2D SurfaceFinishMap(int profile,int kind)
        {
            Texture2D cached=_surfaceFinishMaps[profile,kind];if(cached!=null) return cached;
            int size=SurfaceFinishRules.Resolution;
            var diffuse=new Color32[size*size];var normal=new Color32[size*size];var finish=new Color32[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++)
            {
                var s=SurfaceFinishRules.At(profile,x,y);
                int i=y*size+x;diffuse[i]=new Color32(s.Shade,s.Shade,s.Shade,255);
                normal[i]=new Color32(s.NormalX,s.NormalY,s.NormalZ,255);finish[i]=new Color32(s.Metallic,0,0,s.Smoothness);
            }
            for(int map=0;map<3;map++)
            {
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,map!=0);
                texture.name="LOSAT_Shared_Surface_"+profile+"_"+map;
                texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=4;
                texture.SetPixels32(map==0 ? diffuse : map==1 ? normal : finish);texture.Apply(true,true);
                UnityEngine.Object.DontDestroyOnLoad(texture);_surfaceFinishMaps[profile,map]=texture;
            }
            return _surfaceFinishMaps[profile,kind];
        }
        private static void ApplySurfaceFinish(Material material,int profile,float bumpScale)
        {
            material.mainTexture=SurfaceFinishMap(profile,0);
            material.SetTexture("_BumpMap",SurfaceFinishMap(profile,1));material.SetFloat("_BumpScale",bumpScale);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap",SurfaceFinishMap(profile,2));material.SetFloat("_GlossMapScale",1f);
            material.EnableKeyword("_METALLICGLOSSMAP");
        }
    }
}
