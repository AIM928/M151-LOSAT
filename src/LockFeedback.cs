using System;
using System.Runtime.InteropServices;
using MelonLoader;

namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private FMOD.Sound _lockTone;
        private bool _lockToneFailed;
        private void PlayLockConfirmation()
        {
            if(_lockToneFailed) return;
            try
            {
                FMOD.System core=FMODUnity.RuntimeManager.CoreSystem;
                if(!core.hasHandle()) return;
                if(!_lockTone.hasHandle())
                {
                    byte[] wave=LockToneRules.BuildWave();
                    var info=new FMOD.CREATESOUNDEXINFO {cbsize=Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),length=(uint)wave.Length};
                    CheckLockAudio(core.createSound(wave,FMOD.MODE.OPENMEMORY|FMOD.MODE._2D|FMOD.MODE.CREATESAMPLE|FMOD.MODE.LOOP_OFF,ref info,out _lockTone));
                }
                FMOD.Channel channel;
                CheckLockAudio(core.playSound(_lockTone,default(FMOD.ChannelGroup),true,out channel));
                CheckLockAudio(channel.setVolume(.50f));
                CheckLockAudio(channel.setPaused(false));
            }
            catch(Exception ex)
            {
                _lockToneFailed=true;
                MelonLogger.Warning(Prefix+"lock confirmation audio unavailable; locking remains active: "+ex.Message);
            }
        }
        private static void CheckLockAudio(FMOD.RESULT result)
        {if(result!=FMOD.RESULT.OK) throw new InvalidOperationException(result.ToString());}
        private void ResetLockConfirmation()
        {
            if(_lockTone.hasHandle()) _lockTone.release();
            _lockTone=default(FMOD.Sound);_lockToneFailed=false;
        }
        public override void OnDeinitializeMelon() {ResetSectorVoice();ResetLockConfirmation();ResetLOSATOptics();ResetLaunchAudio();ResetLaunchEffects();ResetCabinViews();}
    }
}
