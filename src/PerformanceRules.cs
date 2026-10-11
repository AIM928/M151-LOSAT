using System;

namespace M151LOSATTest
{
    internal static class PerformanceRules
    {
        internal static bool SuppressPlatoonMessage(string message)
        {
            return message != null && message.StartsWith("[PlatoonAim] ",StringComparison.Ordinal) &&
                message.EndsWith(": skipped (HasTarget)",StringComparison.Ordinal);
        }
    }
}
