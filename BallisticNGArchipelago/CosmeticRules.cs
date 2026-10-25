using System;
using System.Collections.Generic;
using NgContent;
using NgShips;

namespace BallisticNGArchipelago {
 // Unlocks belong to selector-local livery clones. Native assets and save unlocks are untouched.
 public static class CosmeticRules {
  private static HashSet<string> owned=new HashSet<string>();
  private static readonly Dictionary<string,ShipUnlock> locks=new Dictionary<string,ShipUnlock>();
  public static int Revision { get; private set; }
  private static readonly Dictionary<string,string> keys=new Dictionary<string,string> {
   {"Wyvern:alt","Wyvern:Alt"},
   {"Wyvern:neon","Wyvern:Neon"},
   {"Wyvern:clean","Wyvern:Clean"},
   {"Wyvern:stealth","Wyvern:Stealth"},
   {"Hyperion:alt","Hyperion:Alt"},
   {"Hyperion:neon","Hyperion:Neon"},
   {"Hyperion:clean","Hyperion:Clean"},
   {"Hyperion:stealth","Hyperion:Stealth"},
   {"Scorpio:alt","Scorpio:Alt"},
   {"Scorpio:neon","Scorpio:Neon"},
   {"Scorpio:clean","Scorpio:Clean"},
   {"Scorpio:stealth","Scorpio:Stealth"},
   {"Omnicom:alt","Omnicom:Alt"},
   {"Omnicom:neon","Omnicom:Neon"},
   {"Omnicom:clean","Omnicom:Clean"},
   {"Omnicom:stealth","Omnicom:Stealth"},
   {"Diavolt:alt","Diavolt:Alt"},
   {"Diavolt:neon","Diavolt:Neon"},
   {"Diavolt:clean","Diavolt:Clean"},
   {"Diavolt:stealth","Diavolt:Stealth"},
   {"Nexus:alt","Nexus:Alt"},
   {"Nexus:neon","Nexus:Neon"},
   {"Nexus:clean","Nexus:Clean"},
   {"Nexus:stealth","Nexus:Stealth"},
   {"Tenrai:alt","Tenrai:Alt"},
   {"Tenrai:neon","Tenrai:Neon"},
   {"Tenrai:clean","Tenrai:Clean"},
   {"Tenrai:stealth","Tenrai:Stealth"},
   {"Caliburn:alt","Caliburn:Alt"},
   {"Caliburn:neon","Caliburn:Neon"},
   {"Caliburn:clean","Caliburn:Clean"},
   {"Caliburn:stealth","Caliburn:Stealth"},
   {"BarracudaPrototype:alt","BarracudaPrototype:Alt"},
   {"BarracudaPrototype:neon","BarracudaPrototype:Neon"},
   {"BarracudaPrototype:clean","BarracudaPrototype:Clean"},
   {"BarracudaPrototype:stealth","BarracudaPrototype:Stealth"},
   {"Orbitronix:alt","Orbitronix:Alt"},
   {"Orbitronix:neon","Orbitronix:Neon"},
   {"Orbitronix:clean","Orbitronix:Clean"},
   {"Orbitronix:stealth","Orbitronix:Stealth"},
   {"Mako:alt","Mako:Alt"},
   {"Mako:neon","Mako:Neon"},
   {"Mako:clean","Mako:Clean"},
   {"Mako:stealth","Mako:Stealth"},
   {"Protonic:alt","Protonic:Alt"},
   {"Protonic:neon","Protonic:Neon"},
   {"Protonic:clean","Protonic:Clean"},
   {"Protonic:stealth","Protonic:Stealth"},
   {"Mtechp1:alt","Mtechp1:Alt"},
   {"Mtechp1:neon","Mtechp1:Neon"},
   {"Mtechp1:clean","Mtechp1:Clean"},
   {"Mtechp1:stealth","Mtechp1:Stealth"},
   {"Nx2000:alt","Nx2000:Alt"},
   {"Nx2000:neon","Nx2000:Neon"},
   {"Nx2000:clean","Nx2000:Clean"},
   {"Nx2000:stealth","Nx2000:Stealth"},
   {"Tempest:alt","Tempest:Alt"},
   {"Tempest:neon","Tempest:Neon"},
   {"Tempest:clean","Tempest:Clean"},
   {"Tempest:stealth","Tempest:Stealth"},
   {"Zoneship:neon","Zoneship:Neon"},
   {"Zoneship:clean","Zoneship:Clean"},
   {"Zoneship:stealth","Zoneship:Stealth"},
   {"Gtek:alt","Gtek:Alt"},
   {"Gtek:neon","Gtek:Neon"},
   {"Gtek:clean","Gtek:Clean"},
   {"Gtek:stealth","Gtek:Stealth"}
  };
  private static readonly Dictionary<string,string> names=new Dictionary<string,string> {
   {"Wyvern:Alt","Wyvern Alt Livery"},
   {"Wyvern:Neon","Wyvern Neon Livery"},
   {"Wyvern:Clean","Wyvern Clean Livery"},
   {"Wyvern:Stealth","Wyvern Stealth Livery"},
   {"Hyperion:Alt","Hyperion Alt Livery"},
   {"Hyperion:Neon","Hyperion Neon Livery"},
   {"Hyperion:Clean","Hyperion Clean Livery"},
   {"Hyperion:Stealth","Hyperion Stealth Livery"},
   {"Scorpio:Alt","Scorpio Alt Livery"},
   {"Scorpio:Neon","Scorpio Neon Livery"},
   {"Scorpio:Clean","Scorpio Clean Livery"},
   {"Scorpio:Stealth","Scorpio Stealth Livery"},
   {"Omnicom:Alt","Omnicom Alt Livery"},
   {"Omnicom:Neon","Omnicom Neon Livery"},
   {"Omnicom:Clean","Omnicom Clean Livery"},
   {"Omnicom:Stealth","Omnicom Stealth Livery"},
   {"Diavolt:Alt","Diavolt Alt Livery"},
   {"Diavolt:Neon","Diavolt Neon Livery"},
   {"Diavolt:Clean","Diavolt Clean Livery"},
   {"Diavolt:Stealth","Diavolt Stealth Livery"},
   {"Nexus:Alt","Nexus Alt Livery"},
   {"Nexus:Neon","Nexus Neon Livery"},
   {"Nexus:Clean","Nexus Clean Livery"},
   {"Nexus:Stealth","Nexus Stealth Livery"},
   {"Tenrai:Alt","Tenrai Alt Livery"},
   {"Tenrai:Neon","Tenrai Neon Livery"},
   {"Tenrai:Clean","Tenrai Clean Livery"},
   {"Tenrai:Stealth","Tenrai Stealth Livery"},
   {"Caliburn:Alt","Caliburn Alt Livery"},
   {"Caliburn:Neon","Caliburn Neon Livery"},
   {"Caliburn:Clean","Caliburn Clean Livery"},
   {"Caliburn:Stealth","Caliburn Stealth Livery"},
   {"BarracudaPrototype:Alt","Barracuda Model 0 Alt Livery"},
   {"BarracudaPrototype:Neon","Barracuda Model 0 Neon Livery"},
   {"BarracudaPrototype:Clean","Barracuda Model 0 Clean Livery"},
   {"BarracudaPrototype:Stealth","Barracuda Model 0 Stealth Livery"},
   {"Orbitronix:Alt","Orbitronix Alt Livery"},
   {"Orbitronix:Neon","Orbitronix Neon Livery"},
   {"Orbitronix:Clean","Orbitronix Clean Livery"},
   {"Orbitronix:Stealth","Orbitronix Stealth Livery"},
   {"Mako:Alt","Mako Alt Livery"},
   {"Mako:Neon","Mako Neon Livery"},
   {"Mako:Clean","Mako Clean Livery"},
   {"Mako:Stealth","Mako Stealth Livery"},
   {"Protonic:Alt","Protonic AG4 Alt Livery"},
   {"Protonic:Neon","Protonic AG4 Neon Livery"},
   {"Protonic:Clean","Protonic AG4 Clean Livery"},
   {"Protonic:Stealth","Protonic AG4 Stealth Livery"},
   {"Mtechp1:Alt","MTech P1 Alt Livery"},
   {"Mtechp1:Neon","MTech P1 Neon Livery"},
   {"Mtechp1:Clean","MTech P1 Clean Livery"},
   {"Mtechp1:Stealth","MTech P1 Stealth Livery"},
   {"Nx2000:Alt","NX2000 Alt Livery"},
   {"Nx2000:Neon","NX2000 Neon Livery"},
   {"Nx2000:Clean","NX2000 Clean Livery"},
   {"Nx2000:Stealth","NX2000 Stealth Livery"},
   {"Tempest:Alt","C.Y.G.O.N Alt Livery"},
   {"Tempest:Neon","C.Y.G.O.N Neon Livery"},
   {"Tempest:Clean","C.Y.G.O.N Clean Livery"},
   {"Tempest:Stealth","C.Y.G.O.N Stealth Livery"},
   {"Zoneship:Neon","Barracuda S Neon Livery"},
   {"Zoneship:Clean","Barracuda S Clean Livery"},
   {"Zoneship:Stealth","Barracuda S Stealth Livery"},
   {"Gtek:Alt","G-Tek Alt Livery"},
   {"Gtek:Neon","G-Tek Neon Livery"},
   {"Gtek:Clean","G-Tek Clean Livery"},
   {"Gtek:Stealth","G-Tek Stealth Livery"}
  };
  private static string Key(string ship,string livery) {
   string key;return livery!=null && keys.TryGetValue(ship+":"+livery.ToLowerInvariant(),out key) ? key:null;
  }
  public static string ItemName(string ship,string livery) {
   string key=Key(ship,livery);return key==null ? null:names[key];
  }
  public static void Update(HashSet<string> access) {
   if(owned.SetEquals(access) && locks.Count==names.Count) return;
   owned=new HashSet<string>(access);Revision++;
   foreach(string key in names.Keys) {
    if(!locks.ContainsKey(key)) {
     string captured=key,reference="BallisticNGArchipelago.Cosmetic."+key;
     ShipUnlock state=ShipUnlock.GetUnlock(reference);
     if(state==null) {
      state=new ShipUnlock(delegate { return owned.Contains("Cosmetic:"+captured); },"");
      ShipUnlock.RegisterNewUnlock(reference,state);
     }
     locks.Add(key,state);
    }
    locks[key].CheckUnlockState();
   }
  }
  public static void Reset() {
   owned.Clear();Revision++;
   foreach(ShipUnlock state in locks.Values) state.CheckUnlockState();
  }
  private static ShipEntry Entry(ShipEntry source,string ship) {
   ShipEntry result=source;
   if(source.ShipLiveries==null) return result;
   result.ShipLiveries=new List<ShipLivery>();
   foreach(ShipLivery livery in source.ShipLiveries) {
    string key=livery==null ? null:Key(ship,livery.LiveryName);
    if(key==null) { result.ShipLiveries.Add(livery);continue; }
    // Native clone preserves DLC metadata; retain render data omitted by the frontend clone helper.
    ShipLivery copy=ShipLivery.CreateFrontendVersion(livery);
    copy.Schemes=livery.Schemes;copy.MaterialOverride=livery.MaterialOverride;
    copy.IllumSwap=livery.IllumSwap;copy.ReflectionSwap=livery.ReflectionSwap;copy.UgcLivery=livery.UgcLivery;
    copy.CustomUnlockReference="";copy.UnlockReference="BallisticNGArchipelago.Cosmetic."+key;
    copy.UnlockText="Archipelago: requires "+names[key];
    result.ShipLiveries.Add(copy);
   }
   return result;
  }
  public static ShipEntry Apply(ShipEntry source) {
   string ship=source.Ship.ToString();ShipEntry result=Entry(source,ship);
   result.UnlockText="Archipelago: cosmetic selector copy";
   if(source.Variants!=null) {
    result.Variants=new List<ShipVariantEntry>();
    foreach(ShipVariantEntry variant in source.Variants)
     result.Variants.Add(variant==null ? null:new ShipVariantEntry { Name=variant.Name,Entry=Entry(variant.Entry,ship) });
   }
   return result;
  }
 }
}
