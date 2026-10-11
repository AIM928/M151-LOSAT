using System;
using GHPC;
using GHPC.Vehicle;
using UnityEngine;

namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private const float HumveeShellProtectionMm = 15f;

        // Runs once during this vehicle's conversion. Keep native components,
        // geometry, compartment links, damage state and all internal resistance.
        internal static bool UpgradeShellPlate(UniformArmor armor, object owner)
        {
            if (armor == null || owner == null ||
                !object.ReferenceEquals(armor.Unit, owner) ||
                !armor.gameObject.scene.IsValid() ||
                armor.gameObject.layer != GHPC.Utility.CodeUtils.LAYER_INDEX_RAYCAST_ONLY ||
                armor.IsEra || armor.IsSlatArmor) return false;
            string name = armor.Name;
            if (!string.Equals(name, "body", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "hood", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "grill plate", StringComparison.OrdinalIgnoreCase)) return false;
            armor.PrimarySabotRha = Math.Max(armor.PrimarySabotRha, HumveeShellProtectionMm);
            armor.PrimaryHeatRha = Math.Max(armor.PrimaryHeatRha, HumveeShellProtectionMm);
            return true;
        }

        private static void ConfigureHumveeShellProtection(Vehicle vehicle)
        {
            // Follow-aware armor rigs may not be children of the vehicle.
            // Exact Unit identity prevents changing stock M151s or other mods.
            foreach (UniformArmor armor in Resources.FindObjectsOfTypeAll<UniformArmor>())
                UpgradeShellPlate(armor, vehicle.BaseUnit);
        }
    }
}
