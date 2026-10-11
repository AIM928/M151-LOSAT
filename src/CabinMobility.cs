using GHPC.Vehicle;
using NWH.VehiclePhysics;
using UnityEngine;
namespace M151LOSATTest
{
    public sealed partial class Mod
    {
        private readonly System.Collections.Generic.HashSet<int> _mobilityTuned=new System.Collections.Generic.HashSet<int>();
        private void ConfigureCabinMobility(Vehicle vehicle)
        {
            if(_mobilityTuned.Contains(vehicle.GetInstanceID())) return;
            VehicleController drive=vehicle.GetComponentInChildren<VehicleController>(true);
            if(drive==null) return;
            if(drive.engine!=null)
            {
                // Engine config can be shared; tune only this converted instance.
                drive.engine=(Engine)MemberwiseCloneMethod.Invoke(drive.engine,null);
                drive.engine.maxPower=CabinMobilityRules.Power(drive.engine.maxPower);
            }
            Rigidbody body=drive.vehicleRigidbody;
            if(body==null) body=vehicle.GetComponent<Rigidbody>();
            if(body!=null)
            {
                Vector3 center=body.centerOfMass;
                center.y=CabinMobilityRules.CenterY(center.y);
                body.centerOfMass=center;
            }
            // No mass, tyre radius, gearbox ratio, speed limit or gravity edits.
            _mobilityTuned.Add(vehicle.GetInstanceID());
        }
    }
}
