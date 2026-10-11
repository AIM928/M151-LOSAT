using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MelonLoader;
using GHPC.Player;
using UnityEngine;

namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private static FieldInfo _platoonAimTargetField;
        private static FieldInfo _platoonAimStartField;
        private static string _platoonNotReady;
        private void InstallPerformanceCompatibility()
        {
            // Optional in-memory compatibility, never overwrite ImprovedAI.dll.
            Type marker = AccessTools.TypeByName("ImprovedAI.HitscanMarker");
            MethodInfo method = marker == null ? null : AccessTools.Method(marker,"UpdatePlatoonAiming");
            if (method == null) return;
            try
            {
                _platoonAimTargetField=AccessTools.Field(marker,"PlatoonAimTarget");
                _platoonAimStartField=AccessTools.Field(marker,"PlatoonAimStartTime");
                if(_platoonAimTargetField==null || _platoonAimStartField==null)
                    throw new MissingFieldException("ImprovedAI platoon command fields unavailable");
                HarmonyInstance.Patch(method,prefix:new HarmonyMethod(typeof(Mod),nameof(PlatoonReadinessPrefix)),
                    transpiler:new HarmonyMethod(typeof(Mod),nameof(FilterPlatoonDebugLogs)));
            }
            catch(Exception ex) { MelonLogger.Warning(Prefix+"Optional AI log filter unavailable: "+ex.Message); }
        }

        private static bool PlatoonReadinessPrefix()
        {
            // Nullable<Vector3> boxes to null when there is no active command.
            if(_platoonAimTargetField.GetValue(null)==null) {_platoonNotReady=null;return true;}
            float started=(float)_platoonAimStartField.GetValue(null);
            // Always allow original expiry/driver-brain cleanup, even if unavailable.
            if(Time.time-started>30f) {_platoonNotReady=null;return true;}
            PlayerInput input=PlayerInput.Instance;
            GHPC.Unit unit=input==null ? null : input.CurrentPlayerUnit;
            string reason=null;
            if(input==null) reason="No PlayerInput";
            else if(unit==null) reason="No CurrentPlayerUnit";
            else if(!unit.PlatoonInitialized) reason="Platoon not initialized";
            else if(unit.Platoon==null || unit.Platoon.Units==null) reason="Platoon.Units is null";
            if(reason==null) {_platoonNotReady=null;return true;}
            if(reason!=_platoonNotReady)
                MelonLogger.Warning(Prefix+"ImprovedAI command waiting: "+reason+" (repeated per-frame calls skipped until ready).");
            _platoonNotReady=reason;
            return false; // Native would only print the same error and return here.
        }

        private static IEnumerable<CodeInstruction> FilterPlatoonDebugLogs(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo original=AccessTools.Method(typeof(MelonLogger),"Msg",new[] {typeof(string)});
            MethodInfo replacement=AccessTools.Method(typeof(Mod),nameof(PlatoonDebugMessage));
            foreach(CodeInstruction instruction in instructions)
            {
                if(instruction.opcode==OpCodes.Call && Equals(instruction.operand,original))
                    instruction.operand=replacement; // Keep labels/exception blocks intact.
                yield return instruction;
            }
        }

        private static void PlatoonDebugMessage(string message)
        {
            if(PerformanceRules.SuppressPlatoonMessage(message)) return;
            MelonLogger.Msg(message);
        }
    }
}
