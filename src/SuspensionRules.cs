using System;

namespace M151LOSATTest
{
    // Pure, idempotent rules. Do not overwrite a stronger third-party damper.
    internal static class SuspensionRules
    {
        internal static float DampingFloor(float current, bool rebound)
        {
            if (float.IsNaN(current) || float.IsInfinity(current) || current <= 0f) return current;
            return Math.Max(current, rebound ? 1237.5f : 1012.5f);
        }

        internal static float VisualBlend(float elapsed)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0f || elapsed > .25f) return 1f;
            return (float)(1d - Math.Exp(-elapsed / .04d));
        }
    }
}
