using GHPC.Weapons;
using HarmonyLib;
using UnityEngine;
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private static readonly System.Reflection.MethodInfo ReloadSoundStart=AccessTools.Method(typeof(AmmoFeed),"StartAutoloaderAudio");
        private static readonly System.Reflection.MethodInfo ReloadSoundStop=AccessTools.Method(typeof(AmmoFeed),"StopAutoloaderAudio");
        private static readonly System.Reflection.MethodInfo ReloadStageSound=AccessTools.Method(typeof(AmmoFeed),"HandleStageAudio");
        private void InstallReloadAudioHook()
        {
            HarmonyInstance.Patch(AccessTools.Method(typeof(AmmoFeed),"FeedNewClip"),
                postfix:new HarmonyMethod(typeof(Mod),nameof(ReloadSoundsBeginPostfix)));
        }
        private static void ReloadSoundsBeginPostfix(AmmoFeed __instance)
        {
            SalvoWeapon state;
            if(__instance!=null && __instance.Reloading && SalvoFeeds.TryGetValue(__instance,out state)) state.ReloadSounds.Begin();
        }
        private static void UpdateReloadAudio(SalvoWeapon state)
        {
            if(state.ReloadAudioFailed) return;
            try { UpdateReloadAudioCore(state); }
            catch(System.Exception error)
            {
                state.ReloadAudioFailed=true;
                MelonLoader.MelonLogger.Warning(Prefix+"Reload audio repeats disabled for this launcher: "+error.GetBaseException().Message);
            }
        }
        private static void UpdateReloadAudioCore(SalvoWeapon state)
        {
            AmmoFeed feed=state.Weapon.Feed;
            if(feed==null || ClipFeedTimeField==null) return;
            if(!feed.Reloading) {state.ReloadSounds.Advance(false,false,0f,0f);return;}
            if(!state.ReloadSounds.Advance(true,feed.ForcePauseReload,
                (float)ClipFeedTimeField.GetValue(feed),feed.TotalReloadTime)) return;
            // Native first play + three quarter-progress repeats. Only replay
            // audio, never FeedNewClip/DoReloadStage, animations or ammunition.
            if(ReloadSoundStart==null || ReloadSoundStop==null || ReloadStageSound==null) return;
            ReloadSoundStop.Invoke(feed,null);
            ReloadSoundStart.Invoke(feed,null);
            AmmoFeed.ReloadStage stage=null;
            if(feed.ClipReloadStages!=null)
            {
                foreach(AmmoFeed.ReloadStage candidate in feed.ClipReloadStages)
                    if(candidate!=null && (candidate.StageAudio!=null || !candidate.StageEventReference.IsNull || !string.IsNullOrEmpty(candidate.StageAudioEvent))) {stage=candidate;break;}
                if(stage==null && feed.ClipReloadStages.Length>0) stage=feed.ClipReloadStages[0];
            }
            if(stage!=null) ReloadStageSound.Invoke(feed,new object[] {stage,true});
        }
    }
}
