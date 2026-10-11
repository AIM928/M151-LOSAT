using System;
using System.Reflection;
using M151LOSATTest;

namespace GHPC {
    public class Unit {
#if MISSING_NAME
        private string _label="M151 M232";
        public string FriendlyName {get{return _label;}}
#else
        private string _friendlyName="M151 M232";
        public string FriendlyName {get{return _friendlyName;}}
#endif
        private string _uniqueName="M151M232";
        public string UniqueName {get{return _uniqueName;}}
    }
}
namespace GHPC.Vehicle {
    public class Vehicle : GHPC.Unit {public int Ammo=4;public int Installations;}
    public class DerivedVehicle : Vehicle {
        private string _friendlyName="derived-private-sentinel";
        public string ShadowName {get{return _friendlyName;}}
    }
}
class DisplayNameTest {
    static void Check(bool ok,string reason){if(!ok) throw new Exception(reason);}
    static void Main() {
        var vehicle=new GHPC.Vehicle.Vehicle();
#if MISSING_NAME
        for(int i=0;i<10000;i++) Check(!VehicleDisplayName.TryApply(vehicle)&&vehicle.FriendlyName=="M151 M232"&&vehicle.Ammo==4,
            "Missing native name field falls back without throwing or restarting ammo installation");
        Console.WriteLine("ACTUAL_MISSING_COSMETIC_FIELD_10000_SAFE_FALLBACKS_NO_EXCEPTION_OR_AMMO_MUTATION_PASSED");
#else
        Check(vehicle.GetType().GetField("_friendlyName",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)==null,
            "Fixture reproduces the original inherited-private-field failure");
        Check(VehicleDisplayName.TryApply(vehicle)&&vehicle.FriendlyName=="HMMWV LOSAT",
            "Real production helper renames an inherited Unit private field");
        Check(vehicle.UniqueName=="M151M232"&&vehicle.Ammo==4,"Native identity and ammo are unchanged");
        var derived=new GHPC.Vehicle.DerivedVehicle();
        Check(VehicleDisplayName.TryApply(derived)&&derived.FriendlyName=="HMMWV LOSAT"&&derived.ShadowName=="derived-private-sentinel",
            "Typed declaring-field lookup cannot accidentally rename a same-named derived field");
        Check(!VehicleDisplayName.TryApply(null),"Missing cosmetic name safely falls back");
        for(int i=0;i<10000;i++) Check(VehicleDisplayName.TryApply(vehicle)&&vehicle.UniqueName=="M151M232"&&vehicle.Ammo==4,
            "Repeated name writes cannot refill ammo or change native identity");
        var patched=new System.Collections.Generic.HashSet<GHPC.Vehicle.Vehicle>();
        for(int i=0;i<10000;i++) if(!patched.Contains(vehicle)) {
            VehicleDisplayName.TryApply(vehicle);vehicle.Installations++;patched.Add(vehicle);
        }
        Check(vehicle.Installations==1,"Successful name write permits install registration without perpetual discovery retry");
        Console.WriteLine("ACTUAL_UNIT_PRIVATE_BASE_FIELD_VEHICLE_DERIVATION_SHADOW_SAFETY_AND_10000_NAME_WRITES_PASSED_NOT_GAMEPLAY");
#endif
    }
}
