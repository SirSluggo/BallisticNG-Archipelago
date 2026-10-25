using System;
using System.Collections.Generic;
using NgContent;
using NgData;
using NgSp;
namespace BallisticNGArchipelago {
public static class ShipRules {
 public static string Required(string key) { switch(key) {
 case "bng06:21591053": return "Gtek";
 case "bng06:21591054": return "Scorpio";
 case "bng06:21591055": return "Hyperion";
 case "bng06:21591056": return "Omnicom";
 case "bng06:21591057": return "Diavolt";
 case "bng06:21591058": return "Wyvern";
 case "bng06:21591059": return "Nexus";
 case "bng06:21591060": return "Tenrai";
 case "bng06:21591061": return "Gtek";
 case "bng06:21591062": return "Hyperion";
 case "bng06:21591063": return "Omnicom";
 case "bng08:21591071": return "Zoneship";
 case "bng08:21591072": return "Zoneship";
 case "bng08:21591073": return "Zoneship";
 case "bng08:21591074": return "Zoneship";
 case "bng08:21591075": return "Zoneship";
 case "bng08:21591076": return "Zoneship";
 case "bng08:21591077": return "Zoneship";
 case "bng08:21591078": return "Zoneship";
 case "bng08:21591079": return "Zoneship";
 case "bng08:21591080": return "Zoneship";
 case "bng08:21591081": return "Zoneship";
 case "bng08:21591082": return "Zoneship";
 default: return null; } }
 public static string ItemName(string ship) { switch(ship) {
 case "Gtek": return "G-Tek Ship";
 case "Wyvern": return "Wyvern Ship";
 case "Hyperion": return "Hyperion Ship";
 case "Scorpio": return "Scorpio Ship";
 case "Omnicom": return "Omnicom Ship";
 case "Diavolt": return "Diavolt Ship";
 case "Nexus": return "Nexus Ship";
 case "Tenrai": return "Tenrai Ship";
 case "Caliburn": return "Caliburn Ship";
 case "BarracudaPrototype": return "Barracuda Model 0 Ship";
 case "Orbitronix": return "Orbitronix Ship";
 case "Mako": return "Mako Ship";
 case "Protonic": return "Protonic AG4 Ship";
 case "Mtechp1": return "MTech P1 Ship";
 case "Nx2000": return "NX2000 Ship";
 case "Tempest": return "C.Y.G.O.N Ship";
 case "Zoneship": return "Barracuda S Ship";
 default: return "unsupported ship"; } }
 public static bool Owned(string ship,HashSet<string> access) { return (ItemName(ship)!="unsupported ship" && access.Contains("Ship:"+ship)); }
 public static bool Allowed(ShipEntry ship,int variant,HashSet<string> access) {
  return !string.IsNullOrEmpty(ship.DisplayName) && !ship.IsCustomShip && Owned(ship.Ship.ToString(),access) && (variant==0 || (variant==1 && ship.Ship!=EShips.Tempest));
 }
 public static bool ValidateEvent(EventSpec spec,NgCampaignEvent e) {
  string required=Required(spec.Key);var modifier=e.Settings.ModifierSettings;
  if(modifier.ForceShip!=(required!=null)) return false;
  if(required==null) return true;
  ShipEntry ship=Ships.GetShipFromString(modifier.ForcedPlayerShip);
  return !ship.IsCustomShip && !string.IsNullOrEmpty(ship.DisplayName) && ship.Ship.ToString()==required && modifier.ForcedPlayerVariant==0;
 }
}
}
