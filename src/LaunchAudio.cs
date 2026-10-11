using System;
using System.IO;
using System.Runtime.InteropServices;
using GHPC.Weapons;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private FMOD.Sound _launchSound;
        private bool _launchAudioFailed;
        private readonly FMOD.Channel[] _launchChannels=new FMOD.Channel[8];
        private int _launchChannelIndex;
        private void InstallLaunchAudioHook()
        {
            HarmonyInstance.Patch(AccessTools.Method(typeof(WeaponAudio),"FinalStartLoop"),
                prefix:new HarmonyMethod(typeof(Mod),nameof(NativeLaunchAudioPrefix)));
        }
        private static bool NativeLaunchAudioPrefix(WeaponAudio __instance)
        {
            if(SalvoOwner==null) return true;
            foreach(SalvoWeapon s in SalvoWeapons.Values)
                if(s.Weapon!=null && s.Weapon.WeaponSound==__instance)
                    return !SalvoOwner.EnsureLaunchSound();
            return true;
        }
        private bool EnsureLaunchSound()
        {
            if(_launchSound.hasHandle()) return true;
            if(_launchAudioFailed) return false;
            try
            {
                FMOD.System core=FMODUnity.RuntimeManager.CoreSystem;
                if(!core.hasHandle()) return false;
                byte[] wave;
                using(Stream s=typeof(Mod).Assembly.GetManifestResourceStream("M151LOSATTest.Launch.Wave"))
                using(var m=new MemoryStream()) {if(s==null) throw new FileNotFoundException("Embedded launch sound");s.CopyTo(m);wave=m.ToArray();}
                var info=new FMOD.CREATESOUNDEXINFO {cbsize=Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)),length=(uint)wave.Length};
                CheckLockAudio(core.createSound(wave,FMOD.MODE.OPENMEMORY|FMOD.MODE._3D|FMOD.MODE._3D_INVERSEROLLOFF|
                    FMOD.MODE.CREATESAMPLE|FMOD.MODE.LOOP_OFF,ref info,out _launchSound));
                return true;
            }
            catch(Exception e) {_launchAudioFailed=true;MelonLogger.Warning(Prefix+"custom launch audio unavailable; native fallback: "+e.Message);return false;}
        }
        private void PlayLaunchAudio(Vector3 position)
        {
            if(!EnsureLaunchSound()) return;
            try
            {
                int i=_launchChannelIndex++%_launchChannels.Length;
                if(_launchChannels[i].hasHandle()) _launchChannels[i].stop();
                FMOD.Channel channel;
                CheckLockAudio(FMODUnity.RuntimeManager.CoreSystem.playSound(_launchSound,default(FMOD.ChannelGroup),true,out channel));
                _launchChannels[i]=channel;
                FMOD.VECTOR p=new FMOD.VECTOR {x=position.x,y=position.y,z=position.z},v=default(FMOD.VECTOR);
                CheckLockAudio(channel.set3DAttributes(ref p,ref v));
                CheckLockAudio(channel.set3DMinMaxDistance(40f,4000f));
                CheckLockAudio(channel.setVolume(GetLaunchAudioVolume()));
                CheckLockAudio(channel.setPaused(false));
            }
            catch(Exception e) {_launchAudioFailed=true;ResetLaunchChannels();if(_launchSound.hasHandle()) _launchSound.release();_launchSound=default(FMOD.Sound);MelonLogger.Warning(Prefix+"launch audio stopped safely: "+e.Message);}
        }
        private void ResetLaunchChannels()
        {for(int i=0;i<_launchChannels.Length;i++) {if(_launchChannels[i].hasHandle()) _launchChannels[i].stop();_launchChannels[i]=default(FMOD.Channel);}}
        private void ResetLaunchAudio()
        {
            ResetLaunchChannels();if(_launchSound.hasHandle()) _launchSound.release();
            _launchSound=default(FMOD.Sound);_launchAudioFailed=false;_launchChannelIndex=0;
        }
    }
}
