using System;
using System.Collections.Generic;
using NgContent;
using NgData;
using UnityEngine.UI;
using NgUi.MenuUi;
using NgUi.MenuUi.Managers;
using UnityEngine;

namespace BallisticNGArchipelago {
 // Patch selector-local copies only. ContentManager, ranks, unlock saves and AI roster are untouched.
 public sealed class ShipAccess {
  public Action<string> PreviewDiagnostic;
  private const string Marker="Archipelago: ";
  private sealed class View {
   public NgShipSelectorMenuManager Manager;
   public ShipSelector Selector;
   public bool OverrideUnlock;
   public bool Refreshing;
   public string PreviewFingerprint;
   public Action<string> Report;
   public NgMenuShip PreviewTarget;
   public bool PreviewOverride;
   public int PatchedRevision=-1,SelectedRevision=-1;
   public readonly Dictionary<string,ShipEntry> Patched=new Dictionary<string,ShipEntry>();
   public int Category=-1, LastSelected=-1;
   public HashSet<string> Access;
   public ShipSelector.OnCategoryUpdatedDelegate CategoryHandler;
   public ShipSelector.OnShipUpdatedDelegate ShipHandler;
   public readonly Dictionary<string,ShipEntry> Originals=new Dictionary<string,ShipEntry>();
  }
  private readonly List<View> views=new List<View>();
  private static string Key(ShipEntry entry) {
   return entry.Ship+"|"+entry.DisplayName+"|"+entry.CategorySortClass+"|"+entry.IsCustomShip+"|"+entry.FilePath;
  }
  private static bool Marked(ShipEntry entry) {
   return entry.UnlockText!=null && entry.UnlockText.StartsWith(Marker,StringComparison.Ordinal);
  }
  private static ShipEntry Locked(ShipEntry source) {
   ShipEntry result=source;
   string name=ShipRules.ItemName(source.Ship.ToString());
   string message=Marker+(source.IsCustomShip || name=="unsupported ship" ? "ship outside this AP roster" : "requires "+name);
   result.UnlockRank=int.MaxValue;result.UnlockText=message;
   // ShipEntry is a struct but Variants is shared reference data. Clone it before changing locks.
   if(source.Variants!=null) {
    result.Variants=new List<ShipVariantEntry>();
    foreach(ShipVariantEntry variant in source.Variants) {
     if(variant==null) { result.Variants.Add(null);continue; }
     ShipEntry entry=variant.Entry;entry.UnlockRank=int.MaxValue;entry.UnlockText=message;
     result.Variants.Add(new ShipVariantEntry { Name=variant.Name,Entry=entry });
    }
   }
   return result;
  }
  private static bool Selectable(View view,int index) {
   List<ShipEntry> list=view.Selector.CurrentShipList;
   return index>=0 && index<list.Count && ShipRules.Allowed(list[index],0,view.Access) && !list[index].IsLocked;
  }
  private static void PatchEntries(View view) {
   List<ShipEntry> list=view.Selector.CurrentShipList;
   if(view.PatchedRevision!=CosmeticRules.Revision) { view.Patched.Clear();view.PatchedRevision=CosmeticRules.Revision; }
   for(int i=0;i<list.Count;i++) {
    ShipEntry current=list[i],source;string key=Key(current);
    if(!Marked(current)) { source=current;view.Originals[key]=source; }
    else if(!view.Originals.TryGetValue(key,out source)) throw new InvalidOperationException("Missing original selector ship: "+current.DisplayName);
    ShipEntry patched;
    if(!view.Patched.TryGetValue(key,out patched)) {
     patched=ShipRules.Allowed(source,0,view.Access) ? CosmeticRules.Apply(source):Locked(source);
     view.Patched[key]=patched;
    }
    list[i]=patched;
   }
  }
  private static int FirstSelectable(View view) {
   for(int i=0;i<view.Selector.CurrentShipList.Count;i++) if(Selectable(view,i)) return i;
   return -1;
  }
  private static void RefreshRows(View view) {
   if(view.Manager.Mode!=NgShipSelectorMenuManager.EShipSelectorMode.Ship) return;
   foreach(NgNewShipSelectorMenu menu in Resources.FindObjectsOfTypeAll<NgNewShipSelectorMenu>()) {
    if(menu==null || menu.Manager!=view.Manager || menu.SelectorButtonTemplate==null) continue;
    Transform parent=menu.SelectorButtonTemplate.transform.parent;
    if(parent==null) continue;
    List<NgContentSelectorButton> visible=new List<NgContentSelectorButton>();
    foreach(NgContentSelectorButton button in parent.GetComponentsInChildren<NgContentSelectorButton>(true)) {
     if(button==null || button==menu.SelectorButtonTemplate || button==menu.CategoryListButtonTemplate || button.transform.parent!=parent) continue;
     int index=button.ContentIndex;
     bool show=Selectable(view,index);
     if(button.gameObject.activeSelf!=show) button.gameObject.SetActive(show);
     if(show) {
      if(index==view.Selector.Selection.ShipLocalIndex) {
       button.ConfigureShip(view.Selector,index,false,menu);
       ShipEntry entry=view.Selector.CurrentShipList[index].GetVariantEntry(view.Selector.Selection.VariantIndex);
       int liveryIndex=view.Selector.Selection.LiveryIndex;
       if(entry.ShipLiveries!=null && liveryIndex>=0 && liveryIndex<entry.ShipLiveries.Count) {
        var livery=entry.ShipLiveries[liveryIndex];
        string item=livery==null ? null:CosmeticRules.ItemName(view.Selector.CurrentShipList[index].Ship.ToString(),livery.LiveryName);
        if(item!=null && !livery.IsUnlocked && menu.ShipDescriptionText!=null)
         menu.ShipDescriptionText.text="Archipelago: requires "+item;
       }
      }
      button.SetLabelText(view.Selector.CurrentShipList[index].DisplayName.ToLowerInvariant());
      visible.Add(button);
     }
    }
    visible.Sort(delegate(NgContentSelectorButton a,NgContentSelectorButton b) { return a.ContentIndex.CompareTo(b.ContentIndex); });
    for(int i=0;i<visible.Count;i++) {
     Navigation navigation=visible[i].navigation;navigation.mode=Navigation.Mode.Explicit;
     navigation.selectOnUp=visible[(i+visible.Count-1)%visible.Count];
     navigation.selectOnDown=visible[(i+1)%visible.Count];
     visible[i].navigation=navigation;
    }
    foreach(NgContentSelectorButton button in visible)
     if(button.ContentIndex==view.Selector.Selection.ShipLocalIndex && UnityEngine.EventSystems.EventSystem.current!=null) {
      GameObject selected=UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
      // Only move focus when the old selected row has just been hidden.
      if(selected==null || !selected.activeInHierarchy) button.Select();
     }
   }
  }
  private static void RestorePreview(View view) {
   if(view.PreviewTarget!=null) view.PreviewTarget.OverrideUnlock=view.PreviewOverride;
   view.PreviewTarget=null;
  }
  private static void SyncPreviewLock(View view) {
   NgMenuShip target=view.Manager.TargetShip;
   if(target!=view.PreviewTarget) {
    RestorePreview(view);
    if(target!=null) { view.PreviewTarget=target;view.PreviewOverride=target.OverrideUnlock; }
   }
   if(target==null) return;
   bool cosmeticOwned=false;
   int index=view.Selector.Selection.ShipLocalIndex;
   if(Selectable(view,index)) {
    ShipEntry ship=view.Selector.CurrentShipList[index];
    ShipEntry entry=ship.GetVariantEntry(view.Selector.Selection.VariantIndex);
    int livery=view.Selector.Selection.LiveryIndex;
    if(!entry.IsLocked && entry.ShipLiveries!=null && livery>=0 && livery<entry.ShipLiveries.Count && entry.ShipLiveries[livery]!=null) {
     var appearance=entry.ShipLiveries[livery];
     string reference=appearance.UnlockReference,prefix="BallisticNGArchipelago.Cosmetic.";
     cosmeticOwned=appearance.IsUnlocked && reference!=null && reference.StartsWith(prefix,StringComparison.Ordinal) &&
      view.Access.Contains("Cosmetic:"+reference.Substring(prefix.Length));
    }
   }
   bool desired=view.PreviewOverride || cosmeticOwned;
   if(target.OverrideUnlock!=desired) {
    target.OverrideUnlock=desired;
    if(view.Report!=null) view.Report("target_preview_override="+desired+" owned_ap_cosmetic="+cosmeticOwned);
   }
  }
  private static void RefreshPreview(View view) {
   SyncPreviewLock(view);
   int index=view.Selector.Selection.ShipLocalIndex;
   if(!Selectable(view,index)) return;
   ShipEntry ship=view.Selector.CurrentShipList[index];
   int variant=view.Selector.Selection.VariantIndex,livery=view.Selector.Selection.LiveryIndex,scheme=view.Selector.Selection.SchemeIndex;
   ShipEntry entry=ship.GetVariantEntry(variant);
   if(entry.ShipLiveries==null || livery<0 || livery>=entry.ShipLiveries.Count || entry.ShipLiveries[livery]==null) return;
   var appearance=entry.ShipLiveries[livery];
   string fingerprint=Key(ship)+"|"+variant+"|"+livery+"|"+scheme+"|"+CosmeticRules.Revision+"|"+ship.IsLocked+"|"+entry.IsLocked+"|"+appearance.IsUnlocked;
   if(view.PreviewFingerprint==fingerprint) return;
   view.PreviewFingerprint=fingerprint;
   if(view.Report!=null) view.Report("ship="+ship.Ship+" variant="+variant+" livery="+appearance.LiveryName+" scheme="+scheme+
    " base_locked="+ship.IsLocked+" variant_locked="+entry.IsLocked+" livery_unlocked="+appearance.IsUnlocked+
    " cosmetic_revision="+CosmeticRules.Revision);
   // Synchronize the native selector's private ship copy, then queue the current preview last.
   // NgMenuShip preview override is scoped above; manager.OverrideUnlock remains false and Submit still checks AP copies.
   view.Selector.SetShip(index);
   view.Manager.UpdateRenderer(view.Selector.CurrentShipList[index]);
  }
  private static void Refresh(View view) {
   if(view.Refreshing || view.Manager==null || view.Manager.Selector!=view.Selector) return;
   view.Refreshing=true;
   try {
    int category=view.Selector.CurrentCategory;
    int count=view.Selector.ShipCategoryNames.Count;
    int direction=view.Category>=0 && category==(view.Category+count-1)%count ? -1:1;
    PatchEntries(view);
    if(view.Manager.Mode!=NgShipSelectorMenuManager.EShipSelectorMode.Ship) return;
    int first=FirstSelectable(view);
    // Keep native indices: visit other categories rather than deleting their backing arrays.
    for(int tries=0;first<0 && tries<count-1;tries++) {
     view.Selector.SetCategory(view.Selector.CurrentCategory+direction);
     PatchEntries(view);first=FirstSelectable(view);
    }
    view.Category=view.Selector.CurrentCategory;
    if(first>=0) {
     int selected=view.Selector.Selection.ShipLocalIndex;
     if(!Selectable(view,selected)) selected=first;
     string key=Key(view.Selector.CurrentShipList[selected]);
     // Refresh the selector's private ship copy after patching entries, without changing indices.
     if(view.LastSelected!=selected || Key(view.Selector.Selection.ShipEntry)!=key || view.SelectedRevision!=CosmeticRules.Revision)
      view.Selector.SetShip(selected);
     view.LastSelected=selected;view.SelectedRevision=CosmeticRules.Revision;
    }
    RefreshPreview(view);
    RefreshRows(view);
   } finally { view.Refreshing=false; }
  }
  private static void OnShipChanged(View view) {
   if(view.Refreshing || view.Manager==null || view.Manager.Selector!=view.Selector) return;
   if(view.Manager.Mode!=NgShipSelectorMenuManager.EShipSelectorMode.Ship) return;
   int selected=view.Selector.Selection.ShipLocalIndex;
   if(Selectable(view,selected)) { SyncPreviewLock(view);view.LastSelected=selected;return; }
   int count=view.Selector.CurrentShipList.Count;
   int direction=view.LastSelected>=0 && selected==(view.LastSelected+count-1)%count ? -1:1;
   for(int tries=0;tries<count;tries++) {
    selected=(selected+direction+count)%count;
    if(Selectable(view,selected)) {
     view.Refreshing=true;
     try { view.Selector.SetShip(selected);view.LastSelected=selected; }
     finally { view.Refreshing=false; }
     return;
    }
   }
  }
  public void Apply(HashSet<string> access) {
   CosmeticRules.Update(access);
   foreach(View prior in views)
    if(prior.Manager==null || !prior.Manager.gameObject.activeInHierarchy || prior.Manager.Selector!=prior.Selector) RestorePreview(prior);
   views.RemoveAll(delegate(View view) { return view.Manager==null; });
   foreach(NgShipSelectorMenuManager manager in Resources.FindObjectsOfTypeAll<NgShipSelectorMenuManager>()) {
    if(manager==null || !manager.gameObject.activeInHierarchy || manager.Selector==null) continue;
    View view=views.Find(delegate(View candidate) { return candidate.Manager==manager && candidate.Selector==manager.Selector; });
    if(view==null) {
     View prior=views.Find(delegate(View candidate) { return candidate.Manager==manager; });
     view=new View();view.Manager=manager;view.Selector=manager.Selector;
     view.OverrideUnlock=prior==null ? manager.OverrideUnlock:prior.OverrideUnlock;view.Report=PreviewDiagnostic;
     view.Access=new HashSet<string>(access);
     View captured=view;
     view.CategoryHandler=delegate(List<ShipEntry> entries) { Refresh(captured); };
     view.ShipHandler=delegate(ShipEntry entry,PlayerShipSelection selection) { OnShipChanged(captured); };
     view.Selector.OnCategoryUpdate+=view.CategoryHandler;
     view.Selector.OnShipUpdated+=view.ShipHandler;
     views.Add(view);
    }
    view.Access=new HashSet<string>(access);
    manager.OverrideUnlock=false;
    Refresh(view);
   }
  }
  public void Restore() {
   foreach(View view in views) {
    RestorePreview(view);
    view.Selector.OnCategoryUpdate-=view.CategoryHandler;
    view.Selector.OnShipUpdated-=view.ShipHandler;
    if(view.Manager==null) continue;
    List<ShipEntry> list=view.Selector.CurrentShipList;
    for(int i=0;i<list.Count;i++) {
     ShipEntry source;
     if(Marked(list[i]) && view.Originals.TryGetValue(Key(list[i]),out source)) list[i]=source;
    }
    view.Manager.OverrideUnlock=view.OverrideUnlock;
    if(list.Count>0 && view.Manager.Selector==view.Selector && view.Manager.gameObject.activeInHierarchy) {
     int selected=view.Selector.Selection.ShipLocalIndex;
     if(view.Manager.Mode==NgShipSelectorMenuManager.EShipSelectorMode.Ship && view.Selector.OnCategoryUpdate!=null)
      view.Selector.OnCategoryUpdate(list);
     view.Selector.SetShip(selected);
    }
   }
   views.Clear();CosmeticRules.Reset();
  }
 }
}
