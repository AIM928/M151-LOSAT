using UnityEngine;
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private void GuideManualSight(TrackedMissile tracked)
        {
            // Opt-in only for an unassigned PLAYER launch. Never alter a native
            // AI shot whose target binding failed, or a locked missile's arc.
            SalvoWeapon source;
            if(!tracked.ManualSightGuidance || tracked.GuidanceReleased || tracked.SourceWeapon==null ||
                !SalvoWeapons.TryGetValue(tracked.SourceWeapon,out source) || !IsPlayerWeapon(source) ||
                source.Vehicle==null || source.Weapon.FCS==null) return;
            // The sight axis is not the body-fixed 3-degree tube, nor a barrel
            // direction with ballistic superelevation. It follows the reticle.
            Transform sight=source.Weapon.FCS.AimTransform;
            if(sight==null) return;
            Vector3 origin=sight.position,direction=sight.forward;
            if(direction.sqrMagnitude<.5f) return;
            if(Time.time>=tracked.NextManualAim)
            {
                tracked.NextManualAim=Time.time+.1f;
                tracked.ManualAimValid=false;
                if(!ThermalSmokeAtOrigin(origin))
                {
                    int count=Physics.RaycastNonAlloc(origin,direction,_lockHits,TargetLockRules.MaximumRange,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide);
                    if(count<_lockHits.Length)
                    {
                        float nearest=float.PositiveInfinity;bool surface=false;
                        for(int i=0;i<count;i++)
                        {
                            Collider collider=_lockHits[i].collider;
                            if(collider==null) continue;
                            LockBlocker kind;var owner=LockRayOwner(collider,out kind);
                            if(owner==source.Vehicle || collider.transform.IsChildOf(source.Vehicle.transform) ||
                                (owner==null && kind==LockBlocker.Ignore)) continue;
                            if(_lockHits[i].distance<nearest)
                            {nearest=_lockHits[i].distance;surface=owner!=null || kind==LockBlocker.Cover;}
                        }
                        tracked.ManualAimValid=surface && ManualSteeringRules.ReliableDistance(nearest);
                        tracked.ManualAimDistance=nearest;
                    }
                }
            }
            Vector3 goal=ManualArcDirection(tracked,origin,direction);
            if(goal.sqrMagnitude>.01f) tracked.Round.GoalVector=goal.normalized;
        }
        private static Vector3 ManualArcDirection(TrackedMissile tracked,Vector3 origin,Vector3 axis)
        {
            Vector3 position=tracked.RoundTransform.position;
            Vector3 direct=ManualSightDirection(origin,axis,position,tracked.FlightSpeed,
                tracked.ManualAimValid,tracked.ManualAimDistance);
            if(tracked.ManualArcEnded) return direct;
            if(!tracked.ManualAimValid || !ManualSteeringRules.ReliableDistance(tracked.ManualAimDistance))
            {tracked.ManualArcEnded=true;return direct;}
            Vector3 endpoint=origin+axis*tracked.ManualAimDistance;
            Vector3 span=endpoint-tracked.LaunchPosition;float range=span.magnitude;
            float height=ManualSteeringRules.ArcHeight(range);
            if(height==0f) {tracked.ManualArcEnded=true;return direct;}
            float along=Vector3.Dot(position-tracked.LaunchPosition,span/range);
            if(!tracked.ManualArcStarted)
            {
                tracked.ManualArcStarted=true;
                tracked.ManualArcAxis=axis;tracked.ManualArcRange=range;
            }
            // End, rather than restart, the climb after a big reticle/range
            // change, loss of ranging, or terminal entry. No late second hump.
            if(!ManualSteeringRules.StableAim(Vector3.Dot(axis,tracked.ManualArcAxis),tracked.ManualArcRange,range) ||
                ManualSteeringRules.Terminal(along/range,range-along,tracked.FlightSpeed))
            {tracked.ManualArcEnded=true;return direct;}
            Vector3 baseline=Vector3.Lerp(tracked.LaunchPosition,endpoint,Mathf.Clamp01(along/range));
            if(position.y>baseline.y+height+.5f)
            {tracked.ManualArcEnded=true;return direct;} // Native finite-turn overshoot: stop requesting climb.
            float progress=Mathf.Clamp01((along+ManualSteeringRules.Lead(tracked.FlightSpeed))/range);
            Vector3 goal=Vector3.Lerp(tracked.LaunchPosition,endpoint,progress);
            goal.y+=ManualSteeringRules.ArcOffset(progress,range);
            return goal-position;
        }
        private static Vector3 ManualSightDirection(Vector3 origin,Vector3 axis,Vector3 position,
            float speed,bool reliable,float distance)
        {
            // Cache RANGE only, not a vehicle or world point. Moving the reticle
            // moves both missiles immediately; manual fire never becomes a lock.
            if(reliable && ManualSteeringRules.ReliableDistance(distance))
            {
                Vector3 toPoint=origin+axis*distance-position;
                if(Vector3.Dot(toPoint,axis)>0f) return toPoint;
            }
            // No reliable surface (sky, dense hit buffer, smoke) or already past
            // the point: steer toward the sight LINE with a speed-scaled forward
            // lookahead. No fictitious range endpoint, added climb, or U-turn.
            float along=Mathf.Max(0f,Vector3.Dot(position-origin,axis));
            return origin+axis*(along+ManualSteeringRules.Lead(speed))-position;
        }
    }
}
