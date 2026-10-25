using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using NgContent;
using NgData;
using NgSp;
using NgShips;

namespace BallisticNGArchipelago {
 // Read-only diagnostics. Ship access is not modified by this build.
 public static class ShipCatalog {
  private static string Cell(object value) {
   return (Convert.ToString(value,CultureInfo.InvariantCulture) ?? "").Replace("\t"," ").Replace("\r"," ").Replace("\n"," ");
  }
  private static void Row(StringBuilder builder,params object[] cells) {
   for(int i=0;i<cells.Length;i++) { if(i>0) builder.Append('\t');builder.Append(Cell(cells[i])); }
   builder.Append('\n');
  }
  private static int AppendShips(StringBuilder builder,List<ShipEntry> entries,bool registered) {
   int count=0;
   if(entries==null) return count;
   for(int index=0;index<entries.Count;index++) {
    ShipEntry ship=entries[index];
    if(ship.IsCustomShip || ship.Ship==EShips.Custom || string.IsNullOrEmpty(ship.DisplayName)) continue;
    int variants=ship.Variants==null ? 0:ship.Variants.Count;
    for(int variant=0;variant<=variants;variant++) {
     if(variant>0 && ship.Variants[variant-1]==null) continue;
     ShipEntry entry=ship.GetVariantEntry(variant);
     string variantName=variant==0 ? (string.IsNullOrEmpty(ship.BaseVariantNameOverride)?"2159":ship.BaseVariantNameOverride) : ship.Variants[variant-1].Name;
     Row(builder,1,Catalog.Digest,registered,index,ship.Ship,(int)ship.Ship,ship.DisplayName,
      variant,variantName,entry.Ship,entry.DisplayName,entry.Category,entry.CategorySortClass,entry.Class,
      entry.UnlockRank,entry.UnlockEntry,entry.LockedInCampaign,entry.ShipLiveries==null?0:entry.ShipLiveries.Count);
     count++;
    }
   }
   return count;
  }
  // Read-only data for cosmetic-item design; does not unlock, clone or mutate liveries.
  public static string ExportAppearances(string directory) {
   ContentManager content=ContentManager.Instance;
   if(content==null || content.Ships==null || content.Ships.Count==0) return null;
   StringBuilder liveries=new StringBuilder(),variants=new StringBuilder();int total=0;
   Row(liveries,"schema","catalog_digest","content_index","base_ship_enum","base_name","variant_index","variant_name","livery_index","livery_name","scheme_count","unlock_reference","custom_unlock_reference","unlock_text","native_unlocked","ugc","custom_livery");
   Row(variants,"schema","catalog_digest","content_index","base_ship_enum","base_name","variant_index","variant_name","variant_ship_enum","category","sort_class","ship_class","acceleration_stat","speed_stat","handling_stat","shield_stat","firepower_stat","unlock_rank","locked_in_campaign","livery_count");
   for(int index=0;index<content.Ships.Count;index++) {
    ShipEntry ship=content.Ships[index];
    if(ship.IsCustomShip || string.IsNullOrEmpty(ship.DisplayName) || ShipRules.ItemName(ship.Ship.ToString())=="unsupported ship") continue;
    int count=ship.Variants==null ? 0:ship.Variants.Count;
    for(int variant=0;variant<=count;variant++) {
     if(variant>0 && ship.Variants[variant-1]==null) continue;
     ShipEntry entry=ship.GetVariantEntry(variant);
     string name=variant==0 ? (string.IsNullOrEmpty(ship.BaseVariantNameOverride)?"2159":ship.BaseVariantNameOverride):ship.Variants[variant-1].Name;
     Row(variants,1,Catalog.Digest,index,ship.Ship,ship.DisplayName,variant,name,entry.Ship,entry.Category,entry.CategorySortClass,entry.Class,
      entry.AccelerationStat,entry.SpeedStat,entry.HandlingStat,entry.ShieldStat,entry.FirepowerStat,entry.UnlockRank,entry.LockedInCampaign,entry.ShipLiveries==null ? 0:entry.ShipLiveries.Count);
     if(entry.ShipLiveries==null) continue;
     for(int livery=0;livery<entry.ShipLiveries.Count;livery++) {
      ShipLivery item=entry.ShipLiveries[livery];if(item==null) continue;
      Row(liveries,1,Catalog.Digest,index,ship.Ship,ship.DisplayName,variant,name,livery,item.LiveryName,item.SchemeCount,
       item.UnlockReference,item.CustomUnlockReference,item.GetUnlockText(),item.IsUnlocked,item.IsUgcLivery,item.IsCustomShipLivery);
      total++;
     }
    }
   }
   if(total==0) return null;
   string ready=RuntimeFiles.File(directory,"appearance-catalog-ready.txt");
   if(File.Exists(ready)) File.Delete(ready);
   File.WriteAllText(RuntimeFiles.File(directory,"appearance-catalog.tsv"),liveries.ToString(),Encoding.UTF8);
   File.WriteAllText(RuntimeFiles.File(directory,"appearance-variants.tsv"),variants.ToString(),Encoding.UTF8);
   string report="v0.31 appearances="+total+"; digest="+Catalog.Digest;
   File.WriteAllText(ready,report+Environment.NewLine,Encoding.UTF8);return report;
  }
  public static string Export(string directory,NgCampaignList[] lists) {
   ContentManager content=ContentManager.Instance;
   if(content==null || content.Ships==null || content.Ships.Count==0 || lists==null) return null;
   StringBuilder ships=new StringBuilder();
   Row(ships,"schema","catalog_digest","registered","content_index","base_ship_enum","base_ship_id","base_name",
    "variant_index","variant_name","variant_ship_enum","display_name","category","sort_class","ship_class",
    "unlock_rank","unlock_entry","locked_in_campaign","livery_count");
   int shipRows=AppendShips(ships,content.Ships,true)+AppendShips(ships,content.UnregisteredShips,false);
   if(shipRows==0) return null;
   StringBuilder events=new StringBuilder();
   Row(events,"schema","catalog_digest","check_id_key","campaign","event","group_index","event_index","mode",
    "force_player_ship","forced_ship_raw","forced_variant","forced_livery","forced_scheme","resolved_ship_enum",
    "resolved_name","resolved_variant_name","resolved_custom","variant_valid","barracuda_allowed");
   int eventRows=0;
   foreach(EventSpec spec in Catalog.Events) {
    NgCampaignList list=Array.Find(lists,delegate(NgCampaignList candidate) {
     return candidate!=null && !candidate.IsCustomList && !candidate.IsExpansion && candidate.Name.Trim()==spec.Campaign;
    });
    if(list==null || spec.Group<0 || spec.Group>=list.EventGroups.Length || spec.Index<0 || spec.Index>=list.EventGroups[spec.Group].Events.Length)
     throw new InvalidDataException("Missing event during ship catalog export: "+spec.Key);
    NgCampaignEvent e=list.EventGroups[spec.Group].Events[spec.Index];
    if(e.UiSettings.EventName!=spec.Name) throw new InvalidDataException("Event name mismatch: "+spec.Key);
    var modifier=e.Settings.ModifierSettings;
    string resolvedEnum="",resolvedName="",variantName="",custom="",variantValid="";
    if(modifier.ForceShip) {
     ShipEntry resolved=Ships.GetShipFromString(modifier.ForcedPlayerShip);
     if(string.IsNullOrEmpty(resolved.DisplayName)) throw new InvalidDataException("Unresolved forced ship for "+spec.Key);
     resolvedEnum=resolved.Ship.ToString();resolvedName=resolved.DisplayName;custom=resolved.IsCustomShip.ToString();
     int variant=modifier.ForcedPlayerVariant;
     bool valid=variant>=0 && (variant==0 || (resolved.Variants!=null && variant<=resolved.Variants.Count && resolved.Variants[variant-1]!=null));
     variantValid=valid.ToString();
     variantName=valid ? (variant==0 ? (string.IsNullOrEmpty(resolved.BaseVariantNameOverride)?"2159":resolved.BaseVariantNameOverride):resolved.Variants[variant-1].Name) : "INVALID";
    }
    Row(events,1,Catalog.Digest,spec.Key,spec.Campaign,spec.Name,spec.Group,spec.Index,spec.Mode,
     modifier.ForceShip,modifier.ForcedPlayerShip,modifier.ForcedPlayerVariant,modifier.ForcedPlayerLivery,
     modifier.ForcedPlayerScheme,resolvedEnum,resolvedName,variantName,custom,variantValid,list.BarracudaAllowed);
    eventRows++;
   }
   // Build both tables fully before publishing; ready marker is written last.
   string ready=RuntimeFiles.File(directory,"ship-catalog-ready.txt");
   if(File.Exists(ready)) File.Delete(ready);
   File.WriteAllText(RuntimeFiles.File(directory,"ship-catalog.tsv"),ships.ToString(),Encoding.UTF8);
   File.WriteAllText(RuntimeFiles.File(directory,"event-ship-requirements.tsv"),events.ToString(),Encoding.UTF8);
   string report="v0.15 read-only ship discovery; ships/variants="+shipRows+"; events="+eventRows+"; digest="+Catalog.Digest;
   File.WriteAllText(ready,report+Environment.NewLine,Encoding.UTF8);
   return report;
  }
 }
}
