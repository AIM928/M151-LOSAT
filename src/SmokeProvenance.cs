using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GHPC.Weaponry;
using HarmonyLib;
using UnityEngine;
namespace M151LOSATTest
{
    // Attached only to a real deployed smoke grenade's effect instance. Never
    // modify shared smoke prefabs, particle emitters, native AI or global LOS.
    internal sealed class LockSmokeSource:MonoBehaviour {}
    public sealed partial class Mod
    {
        [ThreadStatic] private static bool _deployingSmoke;
        private void InstallSmokeSourceHook()
        {
            HarmonyInstance.Patch(AccessTools.Method(typeof(BasicGrenadeExplosionBehaviour),"Explode"),
                prefix:new HarmonyMethod(typeof(Mod),nameof(SmokeDeployPrefix)),
                transpiler:new HarmonyMethod(typeof(Mod),nameof(TrackSmokeInstantiation)),
                finalizer:new HarmonyMethod(typeof(Mod),nameof(SmokeDeployFinalizer)));
        }
        private static void SmokeDeployPrefix(Grenade __0,out bool __state)
        {__state=_deployingSmoke;_deployingSmoke=__0 is SmokeGrenade;}
        private static void SmokeDeployFinalizer(bool __state) {_deployingSmoke=__state;}
        private static IEnumerable<CodeInstruction> TrackSmokeInstantiation(IEnumerable<CodeInstruction> instructions)
        {
            int replaced=0;
            foreach(CodeInstruction op in instructions)
            {
                MethodInfo call=op.operand as MethodInfo;
                if(op.opcode==OpCodes.Call && call!=null && call.DeclaringType==typeof(UnityEngine.Object) &&
                    call.Name=="Instantiate" && call.IsGenericMethod && call.GetGenericArguments()[0]==typeof(GameObject) &&
                    call.GetParameters().Length==3 && call.GetParameters()[1].ParameterType==typeof(Vector3) &&
                    call.GetParameters()[2].ParameterType==typeof(Quaternion))
                {op.operand=AccessTools.Method(typeof(Mod),nameof(InstantiateSmokeEffect));replaced++;}
                yield return op;
            }
            if(replaced!=1) throw new InvalidOperationException("Native smoke effect instantiation changed; source tagging cannot be verified.");
        }
        private static GameObject InstantiateSmokeEffect(GameObject prefab,Vector3 position,Quaternion rotation)
        {
            GameObject effect=UnityEngine.Object.Instantiate(prefab,position,rotation);
            if(_deployingSmoke && effect!=null) effect.AddComponent<LockSmokeSource>();
            return effect;
        }
    }
}
