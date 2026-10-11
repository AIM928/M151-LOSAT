using System;
using System.Reflection;
using GHPC;

namespace M151LOSATTest
{
    internal static class VehicleDisplayName
    {
        // Vehicle derives from Unit. GetField on the runtime Vehicle type cannot
        // see Unit's private field; resolve its declaring type explicitly once.
        private static readonly FieldInfo FriendlyNameField=typeof(Unit).GetField(
            "_friendlyName",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);

        internal static bool TryApply(Unit unit)
        {
            if(unit==null || FriendlyNameField==null || FriendlyNameField.FieldType!=typeof(string)) return false;
            try {
                FriendlyNameField.SetValue(unit,"HMMWV LOSAT");
                return true;
            } catch(Exception) {
                // A cosmetic label must never restart ammo/model installation.
                // The caller retains one warning; gameplay stays operational.
                return false;
            }
        }
    }
}
