using System.Collections.Generic;
using GHPC.Camera;
using GHPC.Crew;
using GHPC.Vehicle;
using UnityEngine;
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private sealed class CabinView
        {internal CameraSlot Slot;internal Renderer[] Driver;internal bool[] Before;internal bool Hidden;}
        private readonly List<CabinView> _cabinViews=new List<CabinView>();
        private void RegisterCabinView(CameraSlot slot,Vehicle vehicle)
        {
            var renderers=new List<Renderer>();
            CrewManager manager=vehicle.GetComponentInChildren<CrewManager>(true);
            if(manager!=null && manager.CrewMembers!=null)
                foreach(var member in manager.CrewMembers)
                {
                    if(member==null || member.Position!=CrewPosition.Driver || member.AutoBoneRig==null || member.AutoBoneRig.CharacterRoot==null) continue;
                    renderers.AddRange(member.AutoBoneRig.CharacterRoot.GetComponentsInChildren<Renderer>(true));
                }
            _cabinViews.Add(new CabinView {Slot=slot,Driver=renderers.ToArray(),Before=new bool[renderers.Count]});
        }
        private static void RestoreCabinDriver(CabinView v)
        {
            if(!v.Hidden) return;
            for(int i=0;i<v.Driver.Length;i++) if(v.Driver[i]!=null) v.Driver[i].enabled=v.Before[i];
            v.Hidden=false;
        }
        private void MaintainCabinViews()
        {
            CameraSlot active=CameraSlot.ActiveInstance;
            // Restore all old viewpoints BEFORE hiding the newly selected one;
            // both head slots share a driver, so saved state cannot be false.
            CabinView selected=null;
            foreach(CabinView v in _cabinViews)
                if(active!=null && v.Slot!=null && (active==v.Slot || active.transform.IsChildOf(v.Slot.transform))) {selected=v;break;}
            foreach(CabinView v in _cabinViews) if(v!=selected) RestoreCabinDriver(v);
            if(selected==null || selected.Hidden) return;
            for(int i=0;i<selected.Driver.Length;i++)
                if(selected.Driver[i]!=null) {selected.Before[i]=selected.Driver[i].enabled;selected.Driver[i].enabled=false;}
            selected.Hidden=true;
            // Only presentation renderers, never animation, bones, hit zones or
            // vehicle scale. External views restore the soldier immediately.
        }
        private void ResetCabinViews()
        {foreach(CabinView v in _cabinViews) RestoreCabinDriver(v);_cabinViews.Clear();}
    }
}
