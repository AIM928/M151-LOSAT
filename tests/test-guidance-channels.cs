using System;
using M151LOSATTest;
class GuidanceChannelTest
{
    static void Check(bool ok,string label) {if(!ok) throw new Exception(label);}
    static void Main()
    {
        var channels=new GuidanceChannels();
        for(int i=0;i<10000;i++)
        {
            int a=i*3,b=a+1,c=a+2;
            Check(channels.Register(a) && channels.Register(b),"Two shared manual/locked slots");
            Check(!channels.Register(c) && channels.Available==0,"Third missile rejected");
            Check(channels.Register(a) && channels.Count==2,"Duplicate fired event is idempotent");
            channels.Release(-a-1);Check(channels.Count==2,"Stale and unrelated round cannot free a slot");
            channels.Release(a);Check(channels.Available==1,"First hit immediately frees its slot");
            Check(channels.Register(c) && channels.Count==2,"Third can launch while second still guided");
            channels.Release(a);Check(channels.Count==2,"Repeated impact cannot free another slot");
            channels.Release(b);channels.Release(c);Check(channels.Count==0,"Each completion independent");
        }
        for(int i=0;i<10000;i++) Check(GuidanceDisplayRules.LampOn(true,i*.001f),"Ready light always green");
        for(int i=0;i<10;i++) {
            Check(GuidanceDisplayRules.LampOn(false,i+.1f),"Blocked light on phase");
            Check(!GuidanceDisplayRules.LampOn(false,i+.7f),"Blocked light off phase");
        }
        float timeout=TargetLockRules.ObstructionGrace+TargetLockRules.ReacquireSeconds;
        Check(TargetLockRules.Reacquiring(TargetLockRules.ObstructionGrace) && TargetLockRules.Reacquiring(timeout-.01f) && !TargetLockRules.Reacquiring(timeout),"Exactly three seconds reacquiring after longer grace");
        Check(!TargetLockRules.MustRelease(true,true,timeout-.01f) && TargetLockRules.MustRelease(true,true,timeout),"Release only after reacquire timeout");
        Check(TargetLockRules.FrameOn(true,.1f) && !TargetLockRules.FrameOn(true,.3f) && TargetLockRules.FrameOn(false,.3f),"Only lost green frame blinks, live lock steady");
        Check(SectorPresentationRules.TurnDirection(30)==1 && SectorPresentationRules.TurnDirection(-30)==-1,"Physical hull directions");
        Console.WriteLine("10000_TWO_SLOT_ROLLING_LIFECYCLES_POOL_ID_SAFETY_GREEN_LAMP_AND_HULL_DIRECTION_PASSED_NOT_GAMEPLAY");
    }
}
