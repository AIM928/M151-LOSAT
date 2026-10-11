using System;
using System.IO;
using System.Runtime.InteropServices;
using MelonLoader;
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private FMOD.Sound _turnLeftVoice,_turnRightVoice;
        private FMOD.Channel _sectorVoiceChannel;
        private bool _sectorVoiceFailed;
        private int _lastSectorDirection;
        private SectorVoiceTiming _sectorVoiceTiming=new SectorVoiceTiming();
        private FMOD.Sound LoadSectorVoice(FMOD.System core,string resource)
        {
            byte[] wave;
            using(Stream s=typeof(Mod).Assembly.GetManifestResourceStream(resource))
            using(var m=new MemoryStream()) {if(s==null) throw new FileNotFoundException(resource);s.CopyTo(m);wave=m.ToArray();}
            var info=new FMOD.CREATESOUNDEXINFO {cbsize=Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),length=(uint)wave.Length};
            FMOD.Sound sound;
            CheckLockAudio(core.createSound(wave,FMOD.MODE.OPENMEMORY|FMOD.MODE._2D|FMOD.MODE.CREATESAMPLE|FMOD.MODE.LOOP_OFF,ref info,out sound));
            return sound;
        }
        private bool EnsureSectorVoice()
        {
            if(_turnLeftVoice.hasHandle() && _turnRightVoice.hasHandle()) return true;
            if(_sectorVoiceFailed) return false;
            try
            {
                FMOD.System core=FMODUnity.RuntimeManager.CoreSystem;if(!core.hasHandle()) return false;
                if(!_turnLeftVoice.hasHandle()) _turnLeftVoice=LoadSectorVoice(core,"M151LOSATTest.Voice.Left");
                if(!_turnRightVoice.hasHandle()) _turnRightVoice=LoadSectorVoice(core,"M151LOSATTest.Voice.Right");
                return true;
            }
            catch(Exception ex)
            {
                _sectorVoiceFailed=true;StopSectorVoice();
                if(_turnLeftVoice.hasHandle()) _turnLeftVoice.release();if(_turnRightVoice.hasHandle()) _turnRightVoice.release();
                _turnLeftVoice=default(FMOD.Sound);_turnRightVoice=default(FMOD.Sound);
                MelonLogger.Warning(Prefix+"female sector voice unavailable; visual guidance remains: "+ex.Message);return false;
            }
        }
        private void UpdateSectorVoice()
        {
            OpticsUpgrade sight=CurrentLockSight();bool ready=sight!=null && LockInputReady();
            int direction=ready ? SectorDirection(sight) : 0;
            if(direction!=_lastSectorDirection) {StopSectorVoice();_lastSectorDirection=direction;}
            int cue=_sectorVoiceTiming.Poll(direction,UnityEngine.Time.unscaledTime,ready);
            if(cue==0 || !EnsureSectorVoice()) return;
            try
            {
                StopSectorVoice();
                CheckLockAudio(FMODUnity.RuntimeManager.CoreSystem.playSound(cue>0 ? _turnRightVoice : _turnLeftVoice,default(FMOD.ChannelGroup),true,out _sectorVoiceChannel));
                CheckLockAudio(_sectorVoiceChannel.setVolume(.85f));CheckLockAudio(_sectorVoiceChannel.setPaused(false));
            }
            catch(Exception ex) {_sectorVoiceFailed=true;StopSectorVoice();MelonLogger.Warning(Prefix+"sector voice stopped safely: "+ex.Message);}
        }
        private void StopSectorVoice()
        {if(_sectorVoiceChannel.hasHandle()) _sectorVoiceChannel.stop();_sectorVoiceChannel=default(FMOD.Channel);}
        private void ResetSectorVoice()
        {
            StopSectorVoice();if(_turnLeftVoice.hasHandle()) _turnLeftVoice.release();if(_turnRightVoice.hasHandle()) _turnRightVoice.release();
            _turnLeftVoice=default(FMOD.Sound);_turnRightVoice=default(FMOD.Sound);_sectorVoiceFailed=false;
            _lastSectorDirection=0;_sectorVoiceTiming=new SectorVoiceTiming();
        }
    }
}
