using System.Collections.Generic;
using GHPC.Crew;
using GHPC.Vehicle;
using UnityEngine;
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private sealed class CrewSeat
        {internal Vehicle Vehicle;internal CrewManager.CrewMember Member;internal Transform Visual,Head;internal Transform[] Roots;internal Vector3[] BasePositions,AppliedPositions;internal Vector3 Seat,AppliedCorrection;internal float NextHeadBinding;}
        private readonly List<CrewSeat> _crewSeats=new List<CrewSeat>();
        private static Transform FindCrewHead(CrewManager.CrewMember member)
        {
            if(member.AutoBoneRig!=null && member.AutoBoneRig.AllCharacterBones!=null)
                foreach(Transform bone in member.AutoBoneRig.AllCharacterBones)
                {
                    if(bone==null) continue;
                    string name=bone.name.ToLowerInvariant();
                    if(name=="head" || name=="catrighead" || name.EndsWith(":head") || name.EndsWith("_head") || name.EndsWith(" head")) return bone;
                }
            return null; // Never guess from a helmet/renderer centroid.
        }
        private static bool CrewRigReady(Vehicle vehicle)
        {
            CrewManager manager=vehicle.GetComponentInChildren<CrewManager>(true);
            if(manager==null || manager.CrewMembers==null) return true; // Preserve crewless AI vehicles.
            foreach(CrewManager.CrewMember member in manager.CrewMembers) {
                if(member==null || member.AutoBoneRig==null || !member.AutoBoneRig.isActiveAndEnabled) continue;
                if(member.AutoBoneRig.AllCharacterBones==null || member.AutoBoneRig.AllCharacterBones.Count==0) return false;
            }
            return true;
        }
        private void RegisterCrewSeat(Vehicle vehicle,Transform visual,CrewManager.CrewMember member,List<Transform> roots,Vector3 seat)
        {
            if(member.Position!=CrewPosition.Gunner && member.Position!=CrewPosition.Driver && member.Position!=CrewPosition.Commander) return;
            foreach(CrewSeat existing in _crewSeats) if(existing.Vehicle==vehicle && object.ReferenceEquals(existing.Member,member)) return;
            var cached=new CrewSeat {Vehicle=vehicle,Member=member,Visual=visual,Head=FindCrewHead(member),Roots=roots.ToArray(),BasePositions=new Vector3[roots.Count],AppliedPositions=new Vector3[roots.Count],Seat=seat};
            for(int i=0;i<roots.Count;i++) cached.AppliedPositions[i]=cached.BasePositions[i]=roots[i].localPosition;
            _crewSeats.Add(cached);
        }
        private void MaintainCrewSeats()
        {
            foreach(CrewSeat seat in _crewSeats)
            {
                if(seat.Vehicle==null || seat.Vehicle.BaseUnit==null || seat.Vehicle.BaseUnit.Destroyed || seat.Visual==null) continue;
                if(seat.Head==null) {
                    if(Time.unscaledTime<seat.NextHeadBinding) continue;
                    seat.NextHeadBinding=Time.unscaledTime+.25f;
                    seat.Head=FindCrewHead(seat.Member);
                    if(seat.Head==null) continue;
                }
                // Seat root stays in vehicle coordinates; articulation and bones
                // remain native. Correct all independent replay/hit-zone roots
                // together, never just hide the soldier or scale his hit zones.
                bool drift=false;
                for(int i=0;i<seat.Roots.Length;i++) if(seat.Roots[i]!=null &&
                    (seat.Roots[i].localPosition-seat.AppliedPositions[i]).sqrMagnitude>.000001f) {drift=true;break;}
                if(drift)
                {
                    // Native root motion changed a seated root. Restore the
                    // aligned baseline once, not on every unchanged late frame.
                    for(int i=0;i<seat.Roots.Length;i++) if(seat.Roots[i]!=null)
                    {
                        if((seat.Roots[i].localPosition-seat.BasePositions[i]).sqrMagnitude>.000001f)
                            seat.Roots[i].localPosition=seat.BasePositions[i];
                        seat.AppliedPositions[i]=seat.BasePositions[i];
                    }
                    seat.AppliedCorrection=Vector3.zero;
                }
                Vector3 head=seat.Visual.InverseTransformPoint(seat.Head.position)-seat.AppliedCorrection;
                Vector3 correction=new Vector3(
                    CabinMobilityRules.SeatCorrection(head.x,seat.Seat.x-.18f,seat.Seat.x+.18f),
                    // Native pane top is ~1.63m. Leave ~13cm above the head
                    // bone for the skull/helmet instead of capping only the bone.
                    CabinMobilityRules.SeatCorrection(head.y,1.20f,seat.Member.Position==CrewPosition.Gunner ? 1.50f : 1.69f),
                    CabinMobilityRules.SeatCorrection(head.z,-.53f,.10f));
                Vector3 delta=correction-seat.AppliedCorrection;
                if(delta.sqrMagnitude<.000001f) continue;
                Vector3 world=seat.Visual.TransformVector(delta);
                for(int i=0;i<seat.Roots.Length;i++) if(seat.Roots[i]!=null)
                {seat.Roots[i].position+=world;seat.AppliedPositions[i]=seat.Roots[i].localPosition;}
                seat.AppliedCorrection=correction;
            }
        }
    }
}
