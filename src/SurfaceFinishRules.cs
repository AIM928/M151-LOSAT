using System;
namespace M151LOSATTest
{
    // Pure, periodic finish samples. Used only when shared assets are prepared,
    // never by Update, projectile or impact callbacks.
    internal static class SurfaceFinishRules
    {
        internal const int Paint=0,Rubber=1,Metal=2,Resolution=256;
        internal struct Sample
        {
            internal byte Shade,NormalX,NormalY,NormalZ,Metallic,Smoothness;
        }
        private static double Hash(int x,int y,int period,uint seed)
        {
            x=(x%period+period)%period;y=(y%period+period)%period;
            unchecked {
                uint n=(uint)x*374761393u+(uint)y*668265263u+seed*2246822519u;
                n=(n^(n>>13))*1274126177u;n^=n>>16;
                return (n&0xffffffu)/16777215d;
            }
        }
        private static double Noise(double u,double v,int period,uint seed)
        {
            double x=u*period,y=v*period;int ix=(int)Math.Floor(x),iy=(int)Math.Floor(y);
            double a=x-ix,b=y-iy;a=a*a*(3d-2d*a);b=b*b*(3d-2d*b);
            double lo=Hash(ix,iy,period,seed)*(1d-a)+Hash(ix+1,iy,period,seed)*a;
            double hi=Hash(ix,iy+1,period,seed)*(1d-a)+Hash(ix+1,iy+1,period,seed)*a;
            return lo*(1d-b)+hi*b;
        }
        private static double Height(int profile,double u,double v)
        {
            return Noise(u,v,64,(uint)(profile+17))*.68+Noise(u,v,16,23)*.22+Noise(u,v,4,31)*.10;
        }
        private static byte Byte(double value)
        {return (byte)Math.Max(0,Math.Min(255,(int)Math.Round(value*255d)));}
        internal static Sample At(int profile,int x,int y)
        {
            double u=x/(double)Resolution,v=y/(double)Resolution,step=1d/Resolution;
            double fine=Noise(u,v,64,(uint)(profile+17)),macro=Noise(u,v,4,31);
            double strength=profile==Rubber ? .54 : profile==Metal ? .20 : .36;
            double nx=(Height(profile,u-step,v)-Height(profile,u+step,v))*strength;
            double ny=(Height(profile,u,v-step)-Height(profile,u,v+step))*strength;
            double length=Math.Sqrt(1d+nx*nx+ny*ny);nx/=length;ny/=length;
            double shade=.91+(macro-.5)*.075+(fine-.5)*.035;
            double smooth=(profile==Rubber ? .17 : profile==Metal ? .36 : .27)+(macro-.5)*.075+(fine-.5)*.025;
            if(profile==Paint) {
                // Low-contrast sparse abrasion of powder coat; no rust stripes,
                // replacement camouflage or increased texture/material count.
                double worn=Math.Max(0d,(Noise(u,v,32,79)-.70d)/.30d);
                shade+=worn*.020d;smooth+=worn*.045d;
            }
            return new Sample {Shade=Byte(shade),NormalX=Byte(nx*.5+.5),NormalY=Byte(ny*.5+.5),
                NormalZ=Byte((1d/length)*.5+.5),Metallic=Byte(profile==Metal ? .68 : profile==Rubber ? 0d : .035),Smoothness=Byte(smooth)};
        }
    }
}
