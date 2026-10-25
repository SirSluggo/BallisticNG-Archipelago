using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using NgGame;
using NgUi.MenuUi;
using NgUi.MenuUi.Managers;
using UnityEngine;
using UnityEngine.EventSystems;
using NgUi.RaceUi.Menus;

namespace BallisticNGArchipelago {
 // Presentation only: scope filtering to the official overview; preserve campaign data and saves.
 public sealed class CampaignSummary {
  private sealed class Row {
   public NgCampaignSlideoutButton Button;
   public string Campaign,Original,Applied;
   public bool SelectedRich,UnselectedRich;
  }
  private readonly List<Row> rows=new List<Row>();
  private Dictionary<string,int> counts=new Dictionary<string,int>();
  private HashSet<string> blue=new HashSet<string>(),hinted=new HashSet<string>();
  private bool ready;
  private string rebuiltAccess;
  private HashSet<string> optionalOnly=new HashSet<string>();
  public bool OptionalOnly(string key) { return ready && optionalOnly.Contains(key); }
  private readonly Dictionary<GameObject,bool> hidden=new Dictionary<GameObject,bool>();
  private Action restoreNavigation;
  private Dictionary<string,int> remaining=new Dictionary<string,int>();
  public bool Ready { get { return ready; } }
  public bool Completed(string key) { return ready && remaining.ContainsKey(key) && remaining[key]==0; }
  private void Hide(GameObject target) {
   if(target==null) return;
   if(!hidden.ContainsKey(target)) hidden.Add(target,target.activeSelf);
   if(target.activeSelf) target.SetActive(false);
  }
  public bool Hinted(string key) { return ready && hinted.Contains(key); }
  private static void Rich(Row row,bool restore) {
   if(row.Button==null) return;
   if(row.Button.SelectedLabel!=null) row.Button.SelectedLabel.richText=restore ? row.SelectedRich:true;
   if(row.Button.UnselectedLabel!=null) row.Button.UnselectedLabel.richText=restore ? row.UnselectedRich:true;
  }
  public void Restore() {
   foreach(Row row in rows) {
    if(row.Button==null) continue;
    if(row.Button.GetTitle()==row.Applied) row.Button.SetTitle(row.Original);
    Rich(row,true);
   }
   rows.Clear();ready=false;rebuiltAccess=null;counts.Clear();blue.Clear();hinted.Clear();remaining.Clear();optionalOnly.Clear();
   bool changed=false;
   foreach(var entry in hidden) if(entry.Key!=null) { entry.Key.SetActive(entry.Value);changed=true; }
   hidden.Clear();
   if(changed && restoreNavigation!=null) restoreNavigation();
   restoreNavigation=null;
  }
  private bool Read(string path,string session) {
   if(!File.Exists(path)) return false;
   string text;
   using(FileStream file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete))
   using(StreamReader reader=new StreamReader(file)) text=reader.ReadToEnd();
   string[] lines=text.Replace("\r","").Split('\n');
   if(lines.Length<3 || lines[0]!=session || lines[1]!=Catalog.Digest) return false;
   var nextCounts=new Dictionary<string,int>();var nextBlue=new HashSet<string>();var nextHints=new HashSet<string>();bool hintHeader=false;var nextOptional=new HashSet<string>();var nextRemaining=new Dictionary<string,int>();
   for(int i=2;i<lines.Length;i++) {
    if(lines[i].Length==0) continue;string[] part=lines[i].Split('\t');
    if(part.Length==2 && part[0]=="hints" && (part[1]=="ready" || part[1]=="pending") && !hintHeader) { hintHeader=true;continue; }
    if(part.Length==4 && part[0]=="campaign") {
     int count;
     if(Array.IndexOf(Catalog.Campaigns,part[1])<0 || nextCounts.ContainsKey(part[1]) || !int.TryParse(part[2],out count) || count<0 || count>101 || (part[3]!="0" && part[3]!="1")) return false;
     nextCounts.Add(part[1],count);if(part[3]=="1" && count>0) nextBlue.Add(part[1]);continue;
    }
    if(part.Length==2 && part[0]=="hint" && Array.Exists(Catalog.Events,delegate(EventSpec e) { return e.Key==part[1]; })) { nextHints.Add(part[1]);continue; }
    if(part.Length==4 && part[0]=="event") {
     int count;
     if(nextRemaining.ContainsKey(part[1]) || !Array.Exists(Catalog.Events,delegate(EventSpec e) { return e.Key==part[1]; }) || !int.TryParse(part[2],out count) || count<0 || count>4 || (part[3]!="0" && part[3]!="1") || (part[3]=="1" && count==0)) return false;
     nextRemaining.Add(part[1],count);if(part[3]=="1") nextOptional.Add(part[1]);continue;
    }
    return false;
   }
   if(!hintHeader || nextCounts.Count!=Catalog.Campaigns.Length || nextRemaining.Count!=Catalog.Events.Length) return false;
   counts=nextCounts;blue=nextBlue;hinted=nextHints;remaining=nextRemaining;optionalOnly=nextOptional;ready=true;return true;
  }
  private static string PlainTitle(string title) {
   return Regex.Replace(title ?? "","<[^>]*>","").Trim().ToLowerInvariant();
  }
  private static string CampaignName(string title) {
   string plain=PlainTitle(title);
   foreach(string name in Catalog.Campaigns) {
    string prefix=name.ToLowerInvariant();
    if(plain==prefix || plain.StartsWith(prefix+" (",StringComparison.Ordinal)) return name;
   }
   // Some native labels use the event-list spelling rather than the catalog spelling.
   if(plain=="decension" || plain.StartsWith("decension (",StringComparison.Ordinal)) return "Descension";
   return null;
  }
  private static bool OutsideSeed(string title) {
   string plain=PlainTitle(title);
   return plain=="practice / tutorial" || plain=="2280 practice / tutorial" ||
    plain=="agl 2280" || plain=="agl2280" || plain=="aftermath" || plain=="aster league" || plain=="neon nights";
  }
  public void Refresh(string directory,string session,HashSet<string> access) {
   if(!Read(RuntimeFiles.File(directory,"activity-"+session+".txt"),session)) { Restore();return; }
   if(NgSceneManager.GetLoadedScene()!="Menu" || NgMenuManager.Instance==null) return;
   var ui=NgMenuManager.Instance.CampaignUiManager;
   if(ui==null || ui.TabManager==null || ui.InternalCampaignTabIndex<0 || ui.InternalCampaignTabIndex>=ui.TabManager.Tabs.Count) return;
   var container=ui.TabManager.Tabs[ui.InternalCampaignTabIndex].Container;
   if(container==null || !container.gameObject.activeInHierarchy) return;
   // A visible native header denotes an event list. Never filter rows there.
   if(ui.HeaderGraphic!=null && ui.HeaderGraphic.gameObject.activeInHierarchy) return;
   if(container.Context==null) return;
   bool changed=false;
   foreach(GameObject element in container.Context.Elements) {
    if(element==null) continue;
    bool expansionHeader=false;
    NgGraphicText graphic=element.GetComponent<NgGraphicText>();
    if(graphic!=null) {
     string text=graphic.TextMeshProComponent!=null ? graphic.TextMeshProComponent.text : (graphic.TextComponent!=null ? graphic.TextComponent.text:"");
     expansionHeader=PlainTitle(text)=="expansions";
    }
    NgCampaignSlideoutButton candidate=element.GetComponent<NgCampaignSlideoutButton>();
    // Explicit exclusions only: a title-format change must never hide a supported campaign.
    bool excluded=candidate!=null && OutsideSeed(candidate.GetTitle());
    if(expansionHeader || excluded) { changed=changed || element.activeSelf;Hide(element); }
    else if(hidden.ContainsKey(element)) {
     bool original=hidden[element];hidden.Remove(element);
     if(element.activeSelf!=original) { element.SetActive(original);changed=true; }
    }
   }
   // The native overview may have been built before a received campaign access item.
   // Its one lock-summary row represents only the first locked campaign, so waiting
   // for that row's item can leave later unlocked campaigns absent indefinitely.
   if(container.Context.Elements.Count>0) {
    var open=new List<string>();bool missing=false;
    foreach(string campaign in Catalog.Campaigns) if(access.Contains(campaign)) {
     open.Add(campaign);bool found=false;
     foreach(GameObject element in container.Context.Elements) {
      if(element==null) continue;
      NgCampaignSlideoutButton button=element.GetComponent<NgCampaignSlideoutButton>();
      if(button!=null && CampaignName(button.GetTitle())==campaign && element.activeSelf) { found=true;break; }
     }
     if(!found) missing=true;
    }
    string accessKey=string.Join("|",open.ToArray());
    if(missing && rebuiltAccess!=accessKey) {
     rebuiltAccess=accessKey;ui.GenerateInternalCampaignsList();return;
    }
   }
   restoreNavigation=delegate {
    if(container!=null && container.gameObject.activeInHierarchy && container.Context!=null) {
     container.Context.GenerateNavigationSingleDirectionActiveOnly();
     container.Context.RebuildScrollLayout(EventSystem.current==null ? null:EventSystem.current.currentSelectedGameObject);
    }
   };
   if(changed) {
    container.Context.GenerateNavigationSingleDirectionActiveOnly();
    if(EventSystem.current!=null) {
     GameObject selected=EventSystem.current.currentSelectedGameObject;
     if(selected==null || !selected.activeInHierarchy) {
      foreach(GameObject element in container.Context.Elements)
       if(element!=null && element.activeInHierarchy && element.GetComponent<NgCampaignSlideoutButton>()!=null) {
        EventSystem.current.SetSelectedGameObject(element);break;
       }
     }
    }
    container.Context.RebuildScrollLayout(EventSystem.current==null ? null:EventSystem.current.currentSelectedGameObject);
   }
   for(int i=rows.Count-1;i>=0;i--) {
    Row old=rows[i];if(old.Button==null || old.Button.GetTitle()!=old.Applied) { Rich(old,true);rows.RemoveAt(i); }
   }
   foreach(NgCampaignSlideoutButton button in container.GetComponentsInChildren<NgCampaignSlideoutButton>(false)) {
    Row row=rows.Find(delegate(Row r) { return r.Button==button; });
    if(row==null) {
     string campaign=CampaignName(button.GetTitle());
     if(campaign==null) continue;
     row=new Row();row.Button=button;row.Campaign=campaign;row.Original=PlainTitle(button.GetTitle());
     int originalSuffixIndex=row.Original.IndexOf(" (",StringComparison.Ordinal);
     if(originalSuffixIndex>=0) row.Original=row.Original.Substring(0,originalSuffixIndex);
     row.SelectedRich=button.SelectedLabel!=null && button.SelectedLabel.richText;
     row.UnselectedRich=button.UnselectedLabel!=null && button.UnselectedLabel.richText;rows.Add(row);
    }
    int count=counts[row.Campaign];
    string suffix="("+count+" "+(count==1 ? "event":"events")+" ready)";
    if(blue.Contains(row.Campaign)) suffix="<color=#66BFFF>"+suffix+"</color>";
    string color=count>0 ? "#78DD8B":"#A0A0A0";
    row.Applied="<color="+color+">"+row.Original+"</color> "+(blue.Contains(row.Campaign) ? suffix:"<color="+color+">"+suffix+"</color>");
    Rich(row,false);button.SetTitle(row.Applied);
   }
  }
 }
}
