using System;
namespace M151LOSATTest
{
    internal static class CabinMobilityRules
    {
        internal static float Power(float original)
        {return original>0 && !float.IsNaN(original) && !float.IsInfinity(original) ? original*1.15f : original;}
        internal static float CenterY(float original)
        {return float.IsNaN(original)||float.IsInfinity(original) ? original : Math.Min(original,.28f);}
        internal static float SeatCorrection(float value,float minimum,float maximum)
        {return float.IsNaN(value)||float.IsInfinity(value) ? 0f : Math.Max(minimum,Math.Min(maximum,value))-value;}
    }
}
