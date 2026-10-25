using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using NgModding;
using NgEvents;
using NgShips;
using NgData;
using NgGame;
using NgSp;
using NgMp;
using NgUi.MenuUi.Managers;
using NgUi.MenuUi;
using NgUi.RaceUi.Menus;
using UnityEngine;

namespace BallisticNGArchipelago {
 internal static class ProfileRecoveryNotice {
  [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]
  private static extern int MessageBoxW(IntPtr owner,string text,string caption,uint flags);

  public static void Show(string directory,Exception error) {
   string report=RuntimeFiles.File(directory,"profile-recovery.txt");
   string message="BallisticNG Archipelago could not identify or open the active save profile.\n\n"+
    "Reason: "+error.Message+"\n\n"+
    "AP play is blocked. Click OK to close the game before repairing the profile.\n\n"+
    "Keep User/Save, User/ArchipelagoProfiles and User/ArchipelagoSaves. Do not delete these folders during mod updates.\n\n";
   if(error.Message.IndexOf("Interrupted save switch",StringComparison.OrdinalIgnoreCase)>=0)
    message+="With the game closed, run Save-Profiles.cmd and choose 5: Recover interrupted switch.\n\n";
   else
    message+="If folders were moved or restored, restore the matching save/profile records from backup. If unsure, share events.tsv and profile-recovery.txt for help.\n\n";
   message+="Details: "+report;
   try { File.WriteAllText(report,DateTime.UtcNow.ToString("o")+Environment.NewLine+message+Environment.NewLine+Environment.NewLine+error.ToString()); }
   catch(Exception writeError) { Debug.LogError("[BallisticNG AP] Could not write recovery report: "+writeError.Message); }
   Debug.LogError("[BallisticNG AP] "+message);
   // A blocking Windows dialog holds startup until the user has read the explanation.
   // No save directory, marker or profile state is modified here.
   if(Application.platform==RuntimePlatform.WindowsPlayer || Application.platform==RuntimePlatform.WindowsEditor) {
    try { MessageBoxW(IntPtr.Zero,message,"BallisticNG Archipelago — Save profile recovery required",0x00050010u); }
    catch(Exception dialogError) { Debug.LogError("[BallisticNG AP] Recovery dialog unavailable: "+dialogError.Message); }
   }
  }
 }
 public sealed class ArchipelagoMod : CodeMod {
  public override void OnRegistered(string path) {
   string mods=Path.Combine(Environment.CurrentDirectory,"User/Mods/Code Mods");
   if(Directory.Exists(Path.Combine(mods,"ArchipelagoLogger")) && Directory.Exists(Path.Combine(mods,"BallisticNGArchipelago"))) {
    ProfileRecoveryNotice.Show(path,new IOException("Both ArchipelagoLogger and BallisticNGArchipelago are installed. Close the game/client and use Upgrade-Mod.cmd, or move the old folder outside Code Mods. Do not load both copies."));
    Application.Quit();return;
   }
   if (LoggerBehaviour.Instance != null) return;
   GameObject host=new GameObject("BallisticNG Archipelago");
   UnityEngine.Object.DontDestroyOnLoad(host);
   host.AddComponent<LoggerBehaviour>().Initialize(path);
  }
 }
 public sealed class Snapshot {
  public NgCampaignList List; public int Threshold; public int[] Groups;
 }
 public sealed class LoggerBehaviour : MonoBehaviour {
  public static LoggerBehaviour Instance;
  private string directory, session, raceSession;
  private SaveProfile saveProfile;
  private ConnectionPanel hostPanel;
  private bool appearanceCatalogExported;
  private readonly ItemToasts itemToasts=new ItemToasts();
  private readonly MainMenuConnection mainMenuConnection=new MainMenuConnection();
  private readonly CampaignSummary campaignSummary=new CampaignSummary();
  private bool summaryErrorReported;
  private int requiredMedal=3,debugMedal=3;
  private bool barrelRolls;
  private int aiPreset;
  private readonly AiPresetRules aiRules=new AiPresetRules();
  private readonly BarrelRollRules barrelRules=new BarrelRollRules();
  private bool survivalMilestones,survivorAwardCaptured;
  private NgAward survivorAward;
  private readonly HashSet<string> attemptChecks=new HashSet<string>();
  private int Required(EventSpec e) { return Catalog.RequiredFinale(e.Key)!=null ? Math.Max(3,requiredMedal):requiredMedal; }
  public string MedalSummary {
   get {
    if(!active || !valid) return "Connect to see this seed's medal target.";
    return "Medal target: "+((NgAward)requiredMedal)+" • Win all three finale tournaments to goal.";
   }
  }
  private bool Meets(NgAward award,EventSpec e) { return (int)award>=Required(e); }
  private void QueueCheck(string key) {
   if(attemptChecks.Contains(key)) return;
   File.AppendAllText(RuntimeFiles.File(directory,"checks-"+raceSession+".txt"),key+Environment.NewLine);
   attemptChecks.Add(key);
   Log("check_queued",key+" debug_forced="+debugEventForced);
  }
  private bool CurrentSessionValid() {
   if(!active || !valid || !Live() || raceSession==null || raceSession!=session || saveProfile==null) return false;
   try {
    string tokenPath=RuntimeFiles.File(directory,"active-session.txt"),profilePath=RuntimeFiles.File(directory,"profile-"+raceSession+".txt");Guid token;
    return File.Exists(tokenPath) && File.Exists(profilePath) && Guid.TryParse(ReadShared(tokenPath).Trim(),out token) &&
     token.ToString("N")==raceSession && saveProfile.Matches(ReadShared(profilePath).Trim());
   } catch(IOException) { return false; }
  }
  private readonly ShipAccess shipAccess=new ShipAccess();
  private bool shipUiErrorReported;
  private sealed class LockLabel {
   public NgGraphicText Graphic;
   public string Campaign, Original, Replacement;
   public bool Tmp;
  }
  private readonly List<LockLabel> lockLabels=new List<LockLabel>();
  private bool lockLabelErrorReported;
  private static string LabelText(NgGraphicText graphic,bool tmp) {
   if(tmp) return graphic.TextMeshProComponent!=null ? graphic.TextMeshProComponent.text : null;
   return graphic.TextComponent!=null ? graphic.TextComponent.text : null;
  }
  private static void WriteLabel(NgGraphicText graphic,bool tmp,string value) {
   if(tmp) { if(graphic.TextMeshProComponent!=null) graphic.TextMeshProComponent.text=value; }
   else if(graphic.TextComponent!=null) graphic.TextComponent.text=value;
  }
  private void RestoreLockLabels() {
   bool visible=false;
   foreach(LockLabel label in lockLabels) {
    if(label.Graphic==null || LabelText(label.Graphic,label.Tmp)!=label.Replacement) continue;
    visible=visible || label.Graphic.gameObject.activeInHierarchy;
    WriteLabel(label.Graphic,label.Tmp,label.Original);
   }
   lockLabels.Clear();
   // A surviving owned lock row identifies the campaign overview, not an event list.
   // Rebuild only that visible overview after restoring the original thresholds.
   if(visible && NgMenuManager.Instance!=null && NgMenuManager.Instance.CampaignUiManager!=null)
    NgMenuManager.Instance.CampaignUiManager.GenerateInternalCampaignsList();
  }
  private string LockedCampaignSummary() {
   int count=Array.FindAll(Catalog.Campaigns,delegate(string name) { return !access.Contains(name); }).Length;
   return count==1 ? "1 campaign awaits access" : count+" campaigns await access";
  }
  private void UpdateLockLabels() {
   if(!active || !valid || NgSceneManager.GetLoadedScene()!="Menu" || NgMenuManager.Instance==null) return;
   var ui=NgMenuManager.Instance.CampaignUiManager;
   if(ui==null || ui.TabManager==null || ui.InternalCampaignTabIndex<0 || ui.InternalCampaignTabIndex>=ui.TabManager.Tabs.Count) return;
   var container=ui.TabManager.Tabs[ui.InternalCampaignTabIndex].Container;
   if(container==null || !container.gameObject.activeInHierarchy) return;
   lockLabels.RemoveAll(delegate(LockLabel label) { return label.Graphic==null || LabelText(label.Graphic,label.Tmp)!=label.Replacement; });
   foreach(LockLabel label in lockLabels) {
    if(access.Contains(label.Campaign) && label.Graphic.gameObject.activeInHierarchy) {
     // A newly received item makes the formerly nonselectable row a real button.
     lockLabels.Clear();ui.GenerateInternalCampaignsList();return;
    }
    if(label.Graphic.gameObject.activeInHierarchy) {
     label.Replacement=LockedCampaignSummary();WriteLabel(label.Graphic,label.Tmp,label.Replacement);
    }
   }
   NgCampaignManager manager=NgCampaignManager.Instance;
   if(manager==null) return;
   foreach(NgGraphicText graphic in container.GetComponentsInChildren<NgGraphicText>(false)) {
    for(int part=0;part<2;part++) {
     bool tmp=part==1;string text=LabelText(graphic,tmp);
     if(string.IsNullOrEmpty(text)) continue;
     foreach(Snapshot snapshot in snapshots) {
      string campaign=snapshot.List.Name.Trim();
      if(access.Contains(campaign) || snapshot.List.PointsToUnlock!=int.MaxValue) continue;
      // Match the exact native string for a known base campaign and current score.
      // No global text search, private reflection, custom/expansion edits or threshold changes.
      string expected=(int.MaxValue-manager.TotalCampaignScore).ToString()+" points to unlock "+snapshot.List.Name.ToLower();
      if(text!=expected) continue;
      string replacement=LockedCampaignSummary();
      LockLabel label=new LockLabel();label.Graphic=graphic;label.Tmp=tmp;label.Campaign=campaign;
      label.Original=text;label.Replacement=replacement;lockLabels.Add(label);
      WriteLabel(graphic,tmp,replacement);Log("campaign_lock_label",campaign+" requires access item");
      break;
     }
    }
   }
  }
  public string TrapSession { get { return active && valid && Live() ? session:null; } }
  private string status="AP v0.36: disconnected";
  private readonly List<Snapshot> snapshots=new List<Snapshot>();
  private readonly HashSet<string> access=new HashSet<string>();
  private bool active, valid, blocked, finished, queued;
  private int generation;
  private float pollAt, guardAt, labelPollAt;
  private EventSpec raceEvent;
  private bool debugUsedRound, debugPending, debugFailPending, debugEventForced;
  private string debugEventKey;
  private bool teamResultCaptured;
  private int teamFinishPlace, teamFinishCount;
  private float teamFinishScore;
  private bool DebugEnabled() {
   string path=RuntimeFiles.File(directory,"enable-debug-win.txt");
   try { return File.Exists(path) && ReadShared(path).Trim()=="true"; }
   catch(IOException) { return false; }
  }
  private bool ModeMatches(EventSpec spec) {
   if(RaceManager.CurrentGamemode==null) return false;
   if(spec.Mode=="Team Race") return RaceManager.CurrentGamemode.GetType().FullName=="NgModes.GmTeamRace";
   if(spec.Mode=="Upsurge") return RaceManager.CurrentGamemode.GetType().FullName=="NgModes.GmUpsurge";
   if(spec.Mode=="Eliminator") return RaceManager.CurrentGamemode.GetType().FullName=="NgModes.GmEliminator";
   if(spec.Mode=="Rush Hour") return RaceManager.CurrentGamemode.GetType().FullName=="NgModes.GmRushHour";
   string expected=spec.Mode=="Time Trial" ? "NgModes.GmTimeTrial" : (spec.Mode=="Knockout" ? "NgModes.GmKnockout" : (spec.Mode=="Survival" ? "NgModes.GmSurvival" : "NgModes.GmRace"));
   return RaceManager.CurrentGamemode.GetType().FullName==expected;
  }
  private bool CanForceWin() {
   ShipController player=Ships.PlayerOneShip;
   return DebugEnabled() && active && valid && Live() && !blocked && !debugUsedRound && !finished &&
    raceSession!=null && raceSession==session && raceEvent!=null && EventAllowed(raceEvent) && SelectedShipAllowed() &&
    NgNetworkBase.CurrentNetwork==null && !NgSettings.Gameplay.SplitscreenEnabled &&
    Race.HasCountdownFinished && player!=null && !player.Eliminated && !player.FinishedEvent &&
    ModeMatches(raceEvent) &&
    Matches(raceEvent,NgCampaign.CurrentList,NgCampaign.CurrentEvent);
  }
  private void ForceDebugWin(bool fail) {
   if(!CanForceWin() || (fail && !CanForceFailure())) return;
   NgModes.RaceTeam debugTeam=null;
   if(raceEvent.Mode=="Team Race") {
    debugTeam=Ships.GetTeamForShip(Ships.PlayerOneShip);
    if(debugTeam==null || Ships.Teams.Count<2 || Ships.Teams.Count*2!=Ships.Loaded.Count) return;
   }
   NgModes.GmSurvivalBackend survival=null;
   if(raceEvent.Mode=="Survival") {
    survival=NgModes.GmSurvivalBackend.Instance;
    if(survival==null || survival.ship!=Ships.PlayerOneShip || survival.HandledDeath) return;
   }
   debugUsedRound=true;debugEventForced=true;debugEventKey=raceEvent.Key;
   Cheats.DebuggingEnabled=true;
   Log(fail ? "debug_forced_failure" : "debug_forced_win",raceEvent.Key+" round="+NgTournament.CurrentTrackIndex+" source=temporary_button");
   if(debugTeam!=null) {
    RaceManager.Instance.LeaderboardsDisabled=true;
    bool opponentFirst=fail;
    foreach(NgModes.RaceTeam team in Ships.Teams) {
     if(team==debugTeam) team.Score=1000f;
     else { team.Score=opponentFirst ? 2000f:0f;opponentFirst=false; }
    }
    // Deliberately oppose track placement and team ranking to test the criterion.
    RaceManager.CurrentGamemode.TestSingleplayerCompleteCondition(fail ? 1:Ships.Loaded.Count);
    return;
   }
   if(raceEvent.Mode=="Upsurge") {
    NgModes.GmUpsurge mode=RaceManager.CurrentGamemode as NgModes.GmUpsurge;
    float target=mode.TargetScore;
    // Prevent a low target from finishing halfway through built-in debug rescoring.
    try {
     mode.TargetScore=Mathf.Max(target,Ships.Loaded.Count+1f);
     mode.TestSingleplayerCompleteCondition(fail ? 2:1);
    } finally { mode.TargetScore=target; }
    return;
   }
   if(survival!=null) {
    // Disable score uploads before changing test-only zone data.
    RaceManager.Instance.LeaderboardsDisabled=true;
    if(!fail && MedalRules.Threshold(NgCampaign.CurrentEvent.EventProgress,debugMedal)<0f) return;
    survival.zone=fail ? 0 : Math.Max(survival.zone,Mathf.CeilToInt(MedalRules.Threshold(NgCampaign.CurrentEvent.EventProgress,debugMedal)*NgCampaign.DifficultyMultiplier));
    if(fail) Ships.PlayerOneShip.Eliminate(true);
    return; // Successful debug advances keep Survival running; failure still uses native death handling.
   }
   if(IsSurvivorMode()) {
    if(fail) Ships.PlayerOneShip.Eliminate(true);
    else foreach(ShipController ship in new List<ShipController>(Ships.Loaded))
     if(ship!=null && ship!=Ships.PlayerOneShip && !ship.Eliminated && ship.ShieldIntegrity>=0f) ship.Eliminate(true);
    return; // Let the actual mode process eliminations and last survivor.
   }
   if(raceEvent.Mode=="Eliminator") {
    RaceManager.CurrentGamemode.TestSingleplayerCompleteCondition(fail ? 2:1);
    return; // Built-in rescore/FinishSession owns standings, medal and results.
   }
   // Built-in test_finish entry point: Race place 1; Time Trial grade 2=Gold, 5=None.
   RaceManager.CurrentGamemode.TestSingleplayerCompleteCondition(raceEvent.Mode=="Time Trial" ? (fail ? 5 : 5-debugMedal) : 1);
  }
  public void Initialize(string path) {
   Instance=this; directory=path;
   shipAccess.PreviewDiagnostic=delegate(string data) { Log("ship_preview",data); };
   try {
    if(File.Exists(Path.Combine(directory,"panel-settings.txt")) || File.Exists(Path.Combine(directory,"active-session.txt"))) throw new IOException("Close game and client, then run Organize-Data.cmd in the installed mod folder before using v0.36.");
    saveProfile=new SaveProfile();saveProfile.Open(directory);
    Log("save_profile",saveProfile.Label+" key="+saveProfile.BootKey);
   } catch(Exception ex) {
    Log("save_profile_error",ex.ToString());
    status="AP: save profile recovery required; see profile-recovery.txt.";
    enabled=false;
    try { ProfileRecoveryNotice.Show(directory,ex); }
    finally { Application.Quit(); }
    return;
   }
   NgRaceEvents.OnPreShipsSpawned+=ResetRace;NgRaceEvents.OnShipExploded+=CaptureSurvivorAward;
   NgRaceEvents.OnShipFinished+=Finish;
   NgRaceEvents.OnEventComplete+=Complete;
   NgRaceEvents.OnEventExit+=Exit;
   hostPanel=gameObject.AddComponent<ConnectionPanel>();hostPanel.Initialize(directory,saveProfile);
   gameObject.AddComponent<ChatPanel>().Initialize(directory);
   Log("logger_started","version=0.36");
  }
  private static string Clean(string s) { return (s??"").Replace("\t"," ").Replace("\r"," ").Replace("\n"," "); }
  private void Log(string kind,string data) {
   try { string log=RuntimeFiles.File(directory,"events.tsv");RuntimeFiles.Rotate(log);File.AppendAllText(log,DateTime.UtcNow.ToString("o")+"\t"+kind+"\t"+Clean(data)+Environment.NewLine); }
   catch(Exception ex) { Debug.LogError("[BallisticNG AP] "+ex.Message); }
  }
  private static bool Matches(EventSpec spec,NgCampaignList list,NgCampaignEvent e) {
   if(list==null || e==null || list.IsCustomList || list.IsExpansion) return false;
   string[] levels=e.Settings.GamemodeSettings.Levels;
   return list.Name.Trim()==spec.Campaign && e.UiSettings.EventName==spec.Name &&
    e.Settings.GamemodeSettings.Name==spec.Mode && levels!=null && string.Join("|",levels)==spec.Levels &&
    e.Settings.GamemodeSettings.SpeedClass.ToString()==spec.Speed &&
    e.Settings.GamemodeSettings.UseModernPhysics==spec.Modern;
  }
  private void Restore() {
   itemToasts.Reset();
   campaignSummary.Restore();
   try { shipAccess.Restore(); } catch(Exception ex) { Log("ship_restore_error",ex.ToString()); }
   RestoreFinaleButtons();
   foreach(Snapshot s in snapshots) {
    s.List.PointsToUnlock=s.Threshold;
    for(int i=0;i<s.Groups.Length && i<s.List.EventGroups.Length;i++) s.List.EventGroups[i].PointsToUnlock=s.Groups[i];
   }
   if(snapshots.Count>0) Log("gates_restored","campaigns="+snapshots.Count);
   snapshots.Clear();
   try { RestoreLockLabels(); }
   catch(Exception ex) { Log("campaign_label_restore_error",ex.Message); }
  }
  private bool Validate(NgCampaignList[] lists) {
   foreach(EventSpec e in Catalog.Events) {
    NgCampaignList list=null;
    foreach(NgCampaignList candidate in lists)
     if(candidate!=null && candidate.Name.Trim()==e.Campaign && !candidate.IsCustomList && !candidate.IsExpansion) { list=candidate; break; }
    if(list==null || list.EventGroups==null || e.Group>=list.EventGroups.Length ||
     list.EventGroups[e.Group].Events==null || e.Index>=list.EventGroups[e.Group].Events.Length ||
     !Matches(e,list,list.EventGroups[e.Group].Events[e.Index]) ||
     !ShipRules.ValidateEvent(e,list.EventGroups[e.Group].Events[e.Index])) return false;
   }
   return true;
  }
  private static string ReadShared(string path) {
   using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete))
   using(StreamReader reader=new StreamReader(stream)) { return reader.ReadToEnd(); }
  }
  private bool Live() {
   string heartbeat=RuntimeFiles.File(directory,"bridge-heartbeat.txt");
   return File.Exists(heartbeat) && (DateTime.UtcNow-File.GetLastWriteTimeUtc(heartbeat)).TotalSeconds<=15;
  }
  private void ReadAccess() {
   string tokenPath=RuntimeFiles.File(directory,"active-session.txt");
   if(!File.Exists(tokenPath) || !Live()) { active=false; session=null; Restore(); SetStatus("AP v0.36 "+saveProfile.Label+": disconnected"); return; }
   Guid token;
   if(!Guid.TryParse(ReadShared(tokenPath).Trim(),out token)) throw new Exception("Invalid session token");
   string current=token.ToString("N");
   string[] lines=ReadShared(RuntimeFiles.File(directory,"access-"+current+".txt")).Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
   if(lines.Length<2 || lines[0]!=current || lines[1]!=Catalog.Digest) throw new Exception("Access manifest mismatch");
   string profilePath=RuntimeFiles.File(directory,"profile-"+current+".txt");
   if(!File.Exists(profilePath) || !saveProfile.Matches(ReadShared(profilePath).Trim())) {
    active=false;valid=false;session=null;Restore();
    SetStatus("AP v0.36 "+saveProfile.Label+": AP inactive. F2 -> Restart into connected AP save.");
    return;
   }
   if(session!=current) { Restore(); valid=false; }
   string[] rules=ReadShared(RuntimeFiles.File(directory,"rules-"+current+".txt")).Replace("\r","").TrimEnd('\n').Split('\n');
   int medal,preset;
   if(rules.Length!=6 || rules[0]!=current || rules[1]!=Catalog.Digest || !int.TryParse(rules[2],out medal) || medal<1 || medal>4 || (rules[3]!="0" && rules[3]!="1") || (rules[4]!="0" && rules[4]!="1") || !int.TryParse(rules[5],out preset) || preset<0 || preset>3) throw new Exception("Campaign rules mismatch");
   requiredMedal=medal;survivalMilestones=rules[3]=="1";barrelRolls=rules[4]=="1";aiPreset=preset;
   session=current; access.Clear(); access.Add("Breeze");
   for(int i=2;i<lines.Length;i++) foreach(string campaign in Catalog.AccessNames) if(lines[i]==campaign) access.Add(campaign);
   active=true;
   NgCampaignManager manager=NgCampaignManager.Instance;
   if(manager==null || manager.Campaign==null) return; // Existing validated state survives race scenes.
   if(!Validate(manager.Campaign)) { valid=false; Restore(); SetStatus("AP: catalog mismatch; checks disabled"); return; }
   valid=true;
   if(!appearanceCatalogExported) {
    try {
     string report=ShipCatalog.ExportAppearances(directory);
     if(report!=null) { appearanceCatalogExported=true;Log("appearance_catalog",report); }
    } catch(Exception ex) { appearanceCatalogExported=true;Log("appearance_catalog_error",ex.Message); }
   }
   if(snapshots.Count>0 && !Array.Exists(manager.Campaign,delegate(NgCampaignList list) { return list==snapshots[0].List; })) Restore();
   foreach(string name in Catalog.Campaigns) {
    NgCampaignList list=null;
    foreach(NgCampaignList candidate in manager.Campaign) if(candidate!=null && candidate.Name.Trim()==name && !candidate.IsExpansion && !candidate.IsCustomList) { list=candidate; break; }
    if(list==null) continue;
    Snapshot snap=snapshots.Find(delegate(Snapshot s) { return s.List==list; });
    if(snap==null) {
     snap=new Snapshot();snap.List=list;snap.Threshold=list.PointsToUnlock;snap.Groups=new int[list.EventGroups.Length];
     for(int i=0;i<snap.Groups.Length;i++) snap.Groups[i]=list.EventGroups[i].PointsToUnlock;
     snapshots.Add(snap);
    }
    list.PointsToUnlock=access.Contains(name)?0:int.MaxValue;
    for(int i=0;i<list.EventGroups.Length;i++) list.EventGroups[i].PointsToUnlock=access.Contains(name)?0:int.MaxValue;
   }
   SetStatus("AP v0.36 "+saveProfile.Label+" OPEN: "+string.Join(", ",Array.FindAll(Catalog.Campaigns,delegate(string n) { return access.Contains(n); })));
  }
  private void SetStatus(string value) { if(status!=value) { status=value; Log("access_state",value); } }
  private bool EventAllowed(EventSpec spec) {
   if(spec==null || !access.Contains(spec.Campaign)) return false;
   string required=Catalog.RequiredFinale(spec.Key);
   string ship=ShipRules.Required(spec.Key);
   return (required==null || access.Contains(required)) && (ship==null || ShipRules.Owned(ship,access)) && Array.TrueForAll(ProgressionRules.Required(spec.Key),delegate(string n) { return access.Contains(n); });
  }
  private sealed class FinaleButton {
   public NgCampaignSlideoutButton Button;
   public NgSlideoutButton.ShipMenuButtonEvent Original, Guarded;
   public EventSpec Spec;
   public string Title, AppliedTitle;
   public bool Interactable,SelectedRich,UnselectedRich;
  }
  private readonly List<FinaleButton> finaleButtons=new List<FinaleButton>();
  private void RestoreFinaleButtons() {
   foreach(FinaleButton entry in finaleButtons) {
    if(entry.Button==null) continue;
    if(entry.Button.OnClicked==entry.Guarded) {
     entry.Button.OnClicked=entry.Original;
     entry.Button.interactable=entry.Interactable;
     if(entry.Button.SelectedLabel!=null) entry.Button.SelectedLabel.richText=entry.SelectedRich;
     if(entry.Button.UnselectedLabel!=null) entry.Button.UnselectedLabel.richText=entry.UnselectedRich;
    }
    if(entry.Button.GetTitle()==entry.AppliedTitle) entry.Button.SetTitle(entry.Title);
   }
   finaleButtons.Clear();
  }
  private void UpdateFinaleButtons() {
   if(!active || !valid || NgSceneManager.GetLoadedScene()!="Menu" || NgMenuManager.Instance==null) return;
   var ui=NgMenuManager.Instance.CampaignUiManager;
   if(ui==null || ui.TabManager==null || ui.InternalCampaignTabIndex<0 || ui.InternalCampaignTabIndex>=ui.TabManager.Tabs.Count) return;
   var container=ui.TabManager.Tabs[ui.InternalCampaignTabIndex].Container;
   if(container==null || !container.gameObject.activeInHierarchy) return;
   // CurrentList is set only when launching an event, so it can refer to a different
   // campaign while browsing. Match the native visible header to a known base list.
   string visibleCampaign=null;
   if(ui.HeaderGraphic!=null && ui.HeaderGraphic.gameObject.activeInHierarchy) {
    string header=LabelText(ui.HeaderGraphic,ui.HeaderGraphic.TextMeshProComponent!=null);
    foreach(Snapshot snapshot in snapshots) {
     NgCampaignList list=snapshot.List;
     if(list!=null && !list.IsCustomList && !list.IsExpansion &&
      header==list.Name.ToLower()+" [ "+list.Completion+"% ]") { visibleCampaign=list.Name.Trim();break; }
    }
   }
   if(visibleCampaign==null) { RestoreFinaleButtons();return; }
   if(finaleButtons.Exists(delegate(FinaleButton entry) { return entry.Spec.Campaign!=visibleCampaign; })) RestoreFinaleButtons();
   finaleButtons.RemoveAll(delegate(FinaleButton entry) { return entry.Button==null; });
   foreach(NgCampaignSlideoutButton button in container.GetComponentsInChildren<NgCampaignSlideoutButton>(false)) {
    FinaleButton entry=finaleButtons.Find(delegate(FinaleButton candidate) { return candidate.Button==button; });
    if(entry==null) {
     EventSpec spec=Array.Find(Catalog.Events,delegate(EventSpec candidate) {
      return candidate.Campaign==visibleCampaign && button.GetTitle()==candidate.Name.ToLower();
     });
     if(spec==null) continue;
     entry=new FinaleButton();entry.Button=button;entry.Spec=spec;entry.Title=button.GetTitle();
     entry.Original=button.OnClicked;entry.Interactable=button.interactable;
     entry.SelectedRich=button.SelectedLabel!=null && button.SelectedLabel.richText;
     entry.UnselectedRich=button.UnselectedLabel!=null && button.UnselectedLabel.richText;
     entry.Guarded=new NgSlideoutButton.ShipMenuButtonEvent();
     FinaleButton captured=entry;
     entry.Guarded.AddListener(delegate {
      if(active && valid && Live() && EventAllowed(captured.Spec)) captured.Original.Invoke();
      else Log("finale_access_denied",captured.Spec.Name);
     });
     button.OnClicked=entry.Guarded;finaleButtons.Add(entry);
    }
    bool allowed=EventAllowed(entry.Spec);
    List<string> missing=new List<string>();
    if(!access.Contains(entry.Spec.Campaign)) missing.Add(Catalog.AccessItemName(entry.Spec.Campaign));
    string finale=Catalog.RequiredFinale(entry.Spec.Key), ship=ShipRules.Required(entry.Spec.Key);
    if(finale!=null && !access.Contains(finale)) missing.Add(Catalog.AccessItemName(finale));
    if(ship!=null && !ShipRules.Owned(ship,access)) missing.Add(ShipRules.ItemName(ship));
    foreach(string required in ProgressionRules.Required(entry.Spec.Key)) if(!access.Contains(required)) missing.Add(ProgressionRules.DisplayName(required));
    bool isHinted=campaignSummary.Hinted(entry.Spec.Key);
    string color=campaignSummary.Completed(entry.Spec.Key) ? "#A0A0A0" :
     (allowed ? (isHinted ? "#66BFFF" : (campaignSummary.OptionalOnly(entry.Spec.Key) ? "#A3B99B" : (campaignSummary.Ready ? "#78DD8B":null))) : (isHinted ? "#C59AFF":"#FF8585"));
    string baseTitle=color==null ? entry.Title : "<color="+color+">"+entry.Title+"</color>";
    string title=baseTitle+(allowed ? "":" [requires "+string.Join(", ",missing.ToArray())+"]");
    if(entry.Button.SelectedLabel!=null) entry.Button.SelectedLabel.richText=color!=null || entry.SelectedRich;
    if(entry.Button.UnselectedLabel!=null) entry.Button.UnselectedLabel.richText=color!=null || entry.UnselectedRich;
    if(entry.AppliedTitle!=title) Log("finale_menu_access",entry.Spec.Name+" allowed="+allowed);
    entry.AppliedTitle=title;button.SetTitle(title);button.interactable=entry.Interactable && allowed;
   }
  }
  private void LateUpdate() {
   // Native event rows are built at end of frame; guard new buttons on following frame.
   // Scene-entry and result checks independently enforce item ownership.
   try { UpdateFinaleButtons(); }
   catch(Exception ex) { if(!lockLabelErrorReported) { Log("finale_ui_error",ex.ToString());lockLabelErrorReported=true; } }
  }
  private bool SelectedShipAllowed() {
   var selection=PlayerShipSelection.PlayerOne;
   EventSpec spec=Identify();string required=spec==null ? null:ShipRules.Required(spec.Key);
   return ShipRules.Allowed(selection.ShipEntry,selection.VariantIndex,access) &&
    (required==null || (selection.ShipEntry.Ship.ToString()==required && selection.VariantIndex==0));
  }
  private bool Denied() {
   if(!active || !valid || !NgCampaign.Enabled || NgCampaign.CurrentList==null || NgNetworkBase.CurrentNetwork!=null) return false;
   string name=NgCampaign.CurrentList.Name.Trim();
   if(Array.IndexOf(Catalog.Campaigns,name)<0) return false;
   if(!access.Contains(name)) return true;
   EventSpec spec=Identify();
   return spec!=null && (!EventAllowed(spec) || !SelectedShipAllowed());
  }
  private bool IsSurvivorMode() {
   return raceEvent!=null && (raceEvent.Mode=="Knockout" || raceEvent.Mode=="Rush Hour");
  }
  private string SurvivorResultKind() { return raceEvent.Mode=="Rush Hour" ? "rush_hour_result":"knockout_result"; }
  private void ObserveSurvivor() {
   if(!active || !valid || blocked || queued || !IsSurvivorMode() ||
    raceSession==null || raceSession!=session || !ModeMatches(raceEvent)) return;
   ShipController player=Ships.PlayerOneShip;
   if(player==null) return;
   if(player.Eliminated || player.ShieldIntegrity<0f) {
    if(survivorAwardCaptured) { finished=true;Complete(); }
    else { queued=true;Log(SurvivorResultKind(),raceEvent.Key+" elimination without current-run award capture; no check"); }
    return;
   }
   // Offline Knockout and Rush Hour set FinishedEvent only for the last survivor.
   if(player.FinishedEvent) { finished=true;Complete(); }
  }
  private bool CanForceFailure() {
   return raceEvent!=null && (raceEvent.Mode=="Time Trial" || IsSurvivorMode() || raceEvent.Mode=="Survival" || raceEvent.Mode=="Eliminator" || raceEvent.Mode=="Team Race" || raceEvent.Mode=="Upsurge");
  }
  private void CaptureSurvivorAward(ShipController player) {
   try {
   if(!IsSurvivorMode() || player==null || player!=Ships.PlayerOneShip || survivorAwardCaptured ||
    (player.ShieldIntegrity>=0f && !player.Eliminated) || raceSession==null || raceSession!=session || !ModeMatches(raceEvent)) return;
   int rivals=Ships.Loaded.Count(delegate(ShipController ship) { return ship!=null && ship!=player && !ship.Eliminated && ship.ShieldIntegrity>=0f; });
   NgAward award=Ships.Loaded.Count==2 ? NgAward.None : (rivals==1 ? NgAward.Silver:(rivals==2 ? NgAward.Bronze:NgAward.None));
   survivorAward=MedalRules.Hard(award);survivorAwardCaptured=true;
   Log("survivor_award_captured",raceEvent.Key+" award="+survivorAward+" rivals="+rivals);
   } catch(Exception ex) { Log("survivor_capture_error",ex.ToString()); }
  }
  private void ObserveSurvival() {
   if(blocked || raceEvent==null || raceEvent.Mode!="Survival" || !CurrentSessionValid() ||
    NgNetworkBase.CurrentNetwork!=null || NgSettings.Gameplay.SplitscreenEnabled || PlayerTrackSelection.UsingCustomTrack ||
    !Race.HasCountdownFinished || !ModeMatches(raceEvent) || !EventAllowed(raceEvent) || !SelectedShipAllowed() ||
    !Matches(raceEvent,NgCampaign.CurrentList,NgCampaign.CurrentEvent) || NgTournament.Enabled ||
    raceEvent.Levels!=PlayerTrackSelection.GetDesiredTrackScene()) return;
   var backend=NgModes.GmSurvivalBackend.Instance;var player=Ships.PlayerOneShip;
   if(backend==null || player==null || backend.ship!=player ||
    !ShipRules.Allowed(player.TargetShipData,PlayerShipSelection.PlayerOne.VariantIndex,access) ||
    player.TargetShipData.Ship.ToString()!=ShipRules.Required(raceEvent.Key)) return;
   NgAward award=MedalRules.Zones(NgCampaign.CurrentEvent.EventProgress,NgCampaign.DifficultyMultiplier,backend.zone);
   if(Meets(award,raceEvent)) QueueCheck(raceEvent.Key);
   if(survivalMilestones) for(int tier=1;tier<=3;tier++)
    if(tier!=requiredMedal && (int)award>=tier) QueueCheck(MedalRules.Milestone(raceEvent.Key,tier));
   // Do not finish, eliminate or alter the player's run when checks are earned.
   if(backend.HandledDeath && player.Eliminated && !queued) { finished=true;Complete(); }
  }
  private void ObserveEliminator() {
   if(!active || !valid || blocked || queued || raceEvent==null || raceEvent.Mode!="Eliminator" ||
    raceSession==null || raceSession!=session || !ModeMatches(raceEvent)) return;
   NgModes.GmEliminator mode=RaceManager.CurrentGamemode as NgModes.GmEliminator;
   ShipController player=Ships.PlayerOneShip;
   // FinishSession builds final score standings and then marks every ship finished.
   if(mode!=null && mode.SessionFinished && player!=null && player.FinishedEvent &&
    mode.Standings!=null && mode.Standings.Contains(player)) { finished=true;Complete(); }
  }
  private void ObserveUpsurge() {
   if(!active || !valid || blocked || queued || raceEvent==null || raceEvent.Mode!="Upsurge" ||
    raceSession==null || raceSession!=session || !ModeMatches(raceEvent)) return;
   NgModes.GmUpsurge mode=RaceManager.CurrentGamemode as NgModes.GmUpsurge;
   ShipController player=Ships.PlayerOneShip;
   if(mode!=null && mode.SessionFinished && player!=null && player.FinishedEvent &&
    mode.Standings!=null && mode.Standings.Contains(player)) { finished=true;Complete(); }
  }
  private void Update() {
   if(NgSceneManager.GetLoadedScene()=="Menu") { if(aiRules.Applied) aiRules.Restore();if(barrelRules.Applied) barrelRules.Restore(); }
   if((ChatPanel.Instance==null || !ChatPanel.Instance.Visible) && Input.GetKeyDown(KeyCode.F8) && CanForceWin()) { debugMedal=Math.Max(3,requiredMedal);debugPending=true;debugFailPending=false; }
   if((ChatPanel.Instance==null || !ChatPanel.Instance.Visible) && Input.GetKeyDown(KeyCode.F9) && CanForceWin() && CanForceFailure()) { debugPending=true;debugFailPending=true; }
   if(debugPending) {
    debugPending=false;
    try { ForceDebugWin(debugFailPending); } catch(Exception ex) { Log("debug_win_error",ex.ToString()); }
   }
   try { ObserveSurvivor(); } catch(Exception ex) { Log("survivor_observer_error",ex.ToString()); }
   try { ObserveSurvival(); } catch(Exception ex) { Log("survival_observer_error",ex.ToString()); }
   try { ObserveEliminator(); } catch(Exception ex) { Log("eliminator_observer_error",ex.ToString()); }
   try { ObserveUpsurge(); } catch(Exception ex) { Log("upsurge_observer_error",ex.ToString()); }
   if(Time.realtimeSinceStartup>=pollAt) {
    pollAt=Time.realtimeSinceStartup+0.5f;
    try { ReadAccess(); }
    catch(Exception ex) { active=false;valid=false;Restore();SetStatus("AP: bridge data unavailable; checks disabled");Log("bridge_read_error",ex.Message); }
   }
   // Poll with the bridge cadence, after access updates; label errors never disable checks.
   if(Time.realtimeSinceStartup>=labelPollAt) {
    labelPollAt=Time.realtimeSinceStartup+0.25f;
    try { if(active && valid && Live()) campaignSummary.Refresh(directory,session,access);else campaignSummary.Restore(); }
    catch(Exception ex) { campaignSummary.Restore();if(!summaryErrorReported) { Log("campaign_summary_error",ex.ToString());summaryErrorReported=true; } }
    try { if(active && valid && Live()) itemToasts.Refresh(directory,session);else itemToasts.Reset(); }
    catch(Exception ex) { Log("toast_error",ex.Message); }
    try { mainMenuConnection.Refresh(active && valid && Live(),directory); } catch(Exception ex) { Log("menu_connection_error",ex.Message); }
    try { UpdateLockLabels(); }
    catch(Exception ex) { if(!lockLabelErrorReported) { Log("campaign_label_error",ex.ToString());lockLabelErrorReported=true; } }
    try { if(active && valid && Live() && NgSceneManager.GetLoadedScene()=="Menu") shipAccess.Apply(access); }
    catch(Exception ex) { if(!shipUiErrorReported) { Log("ship_ui_error",ex.ToString());shipUiErrorReported=true; } }
   }
   if(Denied() && NgSceneManager.GetLoadedScene()!="Menu" && Time.realtimeSinceStartup>=guardAt) {
    guardAt=Time.realtimeSinceStartup+3f;blocked=true;
    Log("access_denied",NgCampaign.CurrentList.Name);NgSceneManager.LoadMenu(false);
   }
  }
  private GUIStyle bannerStyle;
  private void DrawBanner() {
   bool menu=NgSceneManager.GetLoadedScene()=="Menu";
   if(active && valid && !menu) return;
   if(bannerStyle==null) { bannerStyle=new GUIStyle(GUI.skin.box);bannerStyle.fontSize=16;bannerStyle.alignment=TextAnchor.MiddleLeft;bannerStyle.padding=new RectOffset(14,14,7,7);bannerStyle.richText=false; }
   string text=status;
   ChatPanel chat=ChatPanel.Instance;
   if(active && valid && chat!=null && chat.Ready) text="Multiworld Linked • "+chat.Slot+"\n"+chat.Progress+"  •  AI: "+AiPresetRules.Name(aiPreset)+"    F2: Connection  |  F3: Chat, Hints & Log";
   else if(status.IndexOf("disconnected",StringComparison.OrdinalIgnoreCase)>=0) text=(saveProfile!=null && saveProfile.BootKey=="normal" ? "Normal Save":"Disconnected • AP checks paused")+"\nF2: Connection  |  F3: Chat, Hints & Log";
   else if(status.IndexOf("AP inactive",StringComparison.OrdinalIgnoreCase)>=0) text="AP save switch required\nF2 → Restart into connected AP save";
   else text+="\nF2: Connection  |  F3: Chat, Hints & Log";
   float scale=Mathf.Clamp(Screen.height/1080f,.75f,2f);Matrix4x4 matrix=GUI.matrix;
   try { GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1f));GUI.Box(new Rect(15,15,Mathf.Min(1000f,Screen.width/scale-30f),68f),text,bannerStyle); }
   finally { GUI.matrix=matrix; }
  }
  private void OnGUI() {
   itemToasts.Draw();
   DrawBanner();
   if(ChatPanel.Instance!=null && ChatPanel.Instance.Visible) return;
   if(!DebugEnabled() || raceEvent==null || NgSceneManager.GetLoadedScene()=="Menu") return;
   GUI.Box(new Rect(15,60,1000,30),"TEMP DEBUG: F8 success; F9 lower result. Medal buttons test Time Trial/Survival.");
   bool prior=GUI.enabled;
   GUI.enabled=CanForceWin();
   if(GUI.Button(new Rect(15,95,360,45),"DEBUG: SUCCESS (F8)")) { debugMedal=Math.Max(3,requiredMedal);debugPending=true;debugFailPending=false; }
   if(CanForceFailure() && GUI.Button(new Rect(390,95,300,45),"DEBUG: LOWER RESULT (F9)")) { debugPending=true;debugFailPending=true; }
   GUI.enabled=prior;
   if(raceEvent.Mode=="Time Trial" || raceEvent.Mode=="Survival") {
    // Survival zone buttons leave the player alive and can be used successively.
    bool used=debugUsedRound;debugUsedRound=false;GUI.enabled=CanForceWin();debugUsedRound=used;
    for(int tier=1;tier<=4;tier++) if(GUI.Button(new Rect(15+(tier-1)*210,150,200,40),"DEBUG: "+((NgAward)tier).ToString())) {
     debugMedal=tier;debugUsedRound=false;debugPending=true;debugFailPending=false;
    }
    GUI.enabled=prior;
   }
  }
  private EventSpec Identify() {
   if(!NgCampaign.Enabled || NgCampaign.CurrentEvent==null || NgCampaign.CurrentList==null) return null;
   foreach(EventSpec e in Catalog.Events) if(Matches(e,NgCampaign.CurrentList,NgCampaign.CurrentEvent)) return e;
   return null;
  }
  private void ResetRace() {
   aiRules.Restore();barrelRules.Restore();
   generation++;finished=false;queued=false;attemptChecks.Clear();survivorAwardCaptured=false;survivorAward=NgAward.None;blocked=Denied();raceSession=active && valid ? session:null;raceEvent=Identify();
   if(barrelRolls && raceSession!=null && raceEvent!=null && !blocked && Live() && EventAllowed(raceEvent) && NgNetworkBase.CurrentNetwork==null) {
    if(barrelRules.Apply()) Log("barrel_rolls","enabled event="+raceEvent.Key+" leaderboard_uploads=disabled");
   }
   if(aiPreset!=0 && raceSession!=null && raceEvent!=null && !blocked && Live() && EventAllowed(raceEvent) && NgNetworkBase.CurrentNetwork==null && raceEvent.Mode!="Time Trial" && raceEvent.Mode!="Survival") {
    if(aiRules.Apply(raceEvent.Campaign,aiPreset)) Log("ai_preset",aiRules.Report);
    else if(NgCampaign.HardEnabled) Log("ai_preset","native hard mode retained event="+raceEvent.Key);
   }
   teamResultCaptured=false;teamFinishPlace=0;teamFinishCount=0;teamFinishScore=0f;
   debugUsedRound=false;debugPending=false;debugFailPending=false;
   if(raceEvent==null || raceEvent.Key!=debugEventKey || !NgTournament.Enabled || NgTournament.CurrentTrackIndex==0) {
    debugEventForced=false;debugEventKey=raceEvent==null ? null:raceEvent.Key;
   }
  }
  private void Exit() { aiRules.Restore();barrelRules.Restore();generation++;finished=false;queued=false;teamResultCaptured=false; }
  private void Finish(ShipController ship) {
   if(ship==null || ship!=Ships.PlayerOneShip || blocked) return;
   finished=true;Log("player_finished","track="+PlayerTrackSelection.GetDesiredTrackScene()+" place="+ship.FinalPlace);
   if(raceEvent!=null && raceEvent.Mode=="Team Race" && ModeMatches(raceEvent) && !teamResultCaptured) {
    try {
     // GmTeamRace calls Race.Finish immediately before freezing its team standings.
     // Capture rank here, using the same stable ordering; scores keep changing later.
     NgModes.RaceTeam team=Ships.GetTeamForShip(ship);
     if(!ship.FinishedEvent || team==null || Ships.Teams.Count*2!=Ships.Loaded.Count) return;
     List<NgModes.RaceTeam> ordered=Ships.Teams.OrderByDescending(t=>t.Score).ToList();
     teamFinishPlace=ordered.IndexOf(team)+1;
     if(teamFinishPlace<1) return;
     teamFinishCount=ordered.Count;teamFinishScore=team.Score;teamResultCaptured=true;
     Complete(); // autoEndRace=false in Team Race; the global completion event is absent.
    } catch(Exception ex) { Log("team_race_capture_error",ex.ToString()); }
   }
  }
  private void Complete() { if(queued) return;queued=true;StartCoroutine(Result(generation)); }
  private IEnumerator Result(int g) {
   yield return null;
   if(g!=generation || blocked) yield break;
   try {
    ShipController player=Ships.PlayerOneShip;
    if(!active || !valid || !Live() || raceSession==null || raceSession!=session || raceEvent==null ||
     !finished || player==null || (player.Eliminated && !IsSurvivorMode() && raceEvent.Mode!="Survival" && raceEvent.Mode!="Eliminator" && raceEvent.Mode!="Upsurge" && raceEvent.Mode!="Team Race") || NgNetworkBase.CurrentNetwork!=null ||
     NgSettings.Gameplay.SplitscreenEnabled ||
     PlayerTrackSelection.UsingCustomTrack || RaceManager.CurrentGamemode==null ||
     !ModeMatches(raceEvent) || !EventAllowed(raceEvent) || !SelectedShipAllowed() ||
     !ShipRules.Allowed(player.TargetShipData,PlayerShipSelection.PlayerOne.VariantIndex,access) ||
     (ShipRules.Required(raceEvent.Key)!=null && player.TargetShipData.Ship.ToString()!=ShipRules.Required(raceEvent.Key)) ||
     !Matches(raceEvent,NgCampaign.CurrentList,NgCampaign.CurrentEvent)) yield break;
    Guid token;
    if(!Guid.TryParse(ReadShared(RuntimeFiles.File(directory,"active-session.txt")).Trim(),out token) || token.ToString("N")!=raceSession) yield break;
    if(saveProfile==null || !saveProfile.Matches(ReadShared(RuntimeFiles.File(directory,"profile-"+raceSession+".txt")).Trim())) yield break;
    string[] levels=raceEvent.Levels.Split('|');
    int place=player.FinalPlace;
    bool success=false;
    if(raceEvent.Mode=="Team Race") {
     if(NgTournament.Enabled || levels.Length!=1 || PlayerTrackSelection.GetDesiredTrackScene()!=levels[0]) yield break;
     if(!teamResultCaptured || !player.FinishedEvent || teamFinishCount!=Ships.Teams.Count ||
      teamFinishCount*2!=Ships.Loaded.Count) yield break;
     place=teamFinishPlace;success=Meets(MedalRules.Position(place),raceEvent);
     Log("team_race_result",raceEvent.Key+" current_run_team_place="+place+" individual_place="+player.FinalPlace+
      " team_score_at_finish="+teamFinishScore.ToString(System.Globalization.CultureInfo.InvariantCulture)+
      " saved_award="+NgCampaign.CurrentEvent.EventProgress.CurrentAward+" debug_forced="+debugEventForced);
    } else if(raceEvent.Mode=="Upsurge") {
     if(NgTournament.Enabled || levels.Length!=1 || PlayerTrackSelection.GetDesiredTrackScene()!=levels[0]) yield break;
     NgModes.GmUpsurge mode=RaceManager.CurrentGamemode as NgModes.GmUpsurge;
     if(mode==null || !mode.SessionFinished || !player.FinishedEvent || mode.Standings==null ||
      mode.Standings.Count!=Ships.Loaded.Count || !mode.Standings.Contains(player)) yield break;
     place=mode.Standings.IndexOf(player)+1;success=Meets(MedalRules.Position(place),raceEvent);
     Log("upsurge_result",raceEvent.Key+" current_run_place="+place+" score="+
      player.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)+" saved_award="+
      NgCampaign.CurrentEvent.EventProgress.CurrentAward+" debug_forced="+debugEventForced);
    } else if(raceEvent.Mode=="Eliminator") {
     if(NgTournament.Enabled || levels.Length!=1 || PlayerTrackSelection.GetDesiredTrackScene()!=levels[0]) yield break;
     NgModes.GmEliminator mode=RaceManager.CurrentGamemode as NgModes.GmEliminator;
     if(mode==null || !mode.SessionFinished || !player.FinishedEvent || mode.Standings==null ||
      mode.Standings.Count!=Ships.Loaded.Count || !mode.Standings.Contains(player)) yield break;
     place=mode.Standings.IndexOf(player)+1;
     success=Meets(MedalRules.Position(place),raceEvent);
     Log("eliminator_result",raceEvent.Key+" current_run_place="+place+" score="+
      player.Score.ToString(System.Globalization.CultureInfo.InvariantCulture)+" saved_award="+
      NgCampaign.CurrentEvent.EventProgress.CurrentAward+" debug_forced="+debugEventForced);
    } else if(raceEvent.Mode=="Survival") {
     if(NgTournament.Enabled || levels.Length!=1 || PlayerTrackSelection.GetDesiredTrackScene()!=levels[0]) yield break;
     NgModes.GmSurvivalBackend backend=NgModes.GmSurvivalBackend.Instance;
     if(backend==null || backend.ship!=player || !backend.HandledDeath || !player.Eliminated) yield break;
     NgCampaignEvent.Progress progress=NgCampaign.CurrentEvent.EventProgress;
     float multiplier=NgCampaign.DifficultyMultiplier;
     // Match the backend's current-run zone comparisons; never use saved CurrentAward.
     NgAward award=MedalRules.Zones(progress,multiplier,backend.zone);
     success=Meets(award,raceEvent);
     Log("survival_result",raceEvent.Key+" current_run_award="+award+" zones="+backend.zone+
      " saved_award="+progress.CurrentAward+" debug_forced="+debugEventForced);
    } else if(raceEvent.Mode=="Time Trial") {
     if(NgTournament.Enabled || levels.Length!=1 || PlayerTrackSelection.GetDesiredTrackScene()!=levels[0]) yield break;
     NgCampaignEvent.Progress progress=NgCampaign.CurrentEvent.EventProgress;
     float multiplier=NgCampaign.DifficultyMultiplier;
     NgAward award=NgModes.Gamemode.CalculateAwardTime(player,progress.PlatinumValue*multiplier,
      progress.GoldValue*multiplier,progress.SilverValue*multiplier,progress.BronzeValue*multiplier);
     success=Meets(award,raceEvent);
     Log("time_trial_result",raceEvent.Key+" current_run_award="+award+" seconds="+
      player.TotalRaceTime.ToString(System.Globalization.CultureInfo.InvariantCulture)+" saved_award="+
      progress.CurrentAward+" debug_forced="+debugEventForced);
    } else if(IsSurvivorMode()) {
     if(NgTournament.Enabled || levels.Length!=1 || PlayerTrackSelection.GetDesiredTrackScene()!=levels[0]) yield break;
     NgAward award=player.FinishedEvent && !player.Eliminated && player.ShieldIntegrity>=0f ? MedalRules.Hard(NgAward.Gold):(survivorAwardCaptured ? survivorAward:NgAward.None);
     success=Meets(award,raceEvent);
     Log(SurvivorResultKind(),raceEvent.Key+" current_run_award="+award+" debug_forced="+debugEventForced);
    } else if(levels.Length>1) {
     if(!NgTournament.Enabled || NgTournament.Tracks==null || NgTournament.Tracks.Count!=levels.Length ||
      !NgTournament.FinalTrack || NgTournament.Ships==null || NgTournament.Ships.Count!=Ships.Loaded.Count ||
      PlayerTrackSelection.GetDesiredTrackScene()!=levels[levels.Length-1]) yield break;
     place=NgTournament.CalculatePlace(player);
    } else if(NgTournament.Enabled || PlayerTrackSelection.GetDesiredTrackScene()!=levels[0]) yield break;
    Log("eligible_result",raceEvent.Key+" placement="+place+" debug_forced="+debugEventForced);
    if(raceEvent.Mode=="Race") success=Meets(MedalRules.Position(place),raceEvent);
    if(!success) yield break;
    QueueCheck(raceEvent.Key);
    Log("medal_check",raceEvent.Key+" minimum="+(NgAward)Required(raceEvent)+" debug_forced="+debugEventForced);
    if(Catalog.RequiredFinale(raceEvent.Key)!=null)
     Log("finale_win_queued",raceEvent.Name+"; goal requires server confirmation of A, B and C");
   } catch(Exception ex) { Log("logger_error",ex.ToString()); }
  }
  private void OnDestroy() {
   aiRules.Restore();barrelRules.Restore();
   mainMenuConnection.Dispose();
   Restore(); NgRaceEvents.OnPreShipsSpawned-=ResetRace;NgRaceEvents.OnShipExploded-=CaptureSurvivorAward;NgRaceEvents.OnShipFinished-=Finish;
   NgRaceEvents.OnEventComplete-=Complete;NgRaceEvents.OnEventExit-=Exit;
   if(saveProfile!=null) saveProfile.Dispose();
   if(Instance==this) Instance=null;
  }
 }

 public sealed class MainMenuConnection {
  private sealed class Heading {
   public UnityEngine.UI.Graphic Label;public string Original,Applied;public Color Color;
  }
  private readonly List<Heading> headings=new List<Heading>();
  private static string Read(UnityEngine.UI.Graphic label) {
   var text=label as UnityEngine.UI.Text;
   if(text!=null) return text.text;
   var tmp=label as TMPro.TMP_Text;return tmp==null ? null:tmp.text;
  }
  private static void Write(UnityEngine.UI.Graphic label,string value) {
   var text=label as UnityEngine.UI.Text;if(text!=null) text.text=value;
   var tmp=label as TMPro.TMP_Text;if(tmp!=null) tmp.text=value;
  }
  private void Restore() {
   foreach(Heading h in headings) {
    if(h.Label!=null && Read(h.Label)==h.Applied) { Write(h.Label,h.Original);h.Label.color=h.Color; }
   }
   headings.Clear();
  }
  public void Refresh(bool connected,string directory) {
   if(!connected || NgSceneManager.GetLoadedScene()!="Menu") { Restore();return; }
   headings.RemoveAll(delegate(Heading h) { return h.Label==null; });
   // Only the literal native welcome heading is eligible; other menu titles are untouched.
   foreach(var label in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Graphic>()) {
    if(!label.isActiveAndEnabled || !string.Equals(Read(label),"welcome",StringComparison.OrdinalIgnoreCase)) continue;
    Heading h=new Heading { Label=label,Original=Read(label),Applied="Multiworld Linked",Color=label.color };headings.Add(h);
    Write(label,"Multiworld Linked");label.color=new Color(0.4f,0.9f,0.5f,1f);
   }
   // Scope the league label to the official campaigns tab and preserve its native percentage.
   var manager=NgMenuManager.Instance;
   if(manager!=null && manager.CampaignUiManager!=null) {
    var ui=manager.CampaignUiManager;
    if(ui.TabManager!=null && ui.InternalCampaignTabIndex>=0 && ui.InternalCampaignTabIndex<ui.TabManager.Tabs.Count) {
     var tabButton=ui.TabManager.Tabs[ui.InternalCampaignTabIndex].Button;
     if(tabButton!=null) {
      UnityEngine.UI.Graphic[] labels={tabButton.SelectedLabel,tabButton.UnselectedLabel};
      foreach(var label in labels) {
       if(label==null || !string.Equals(Read(label),"official campaigns",StringComparison.OrdinalIgnoreCase)) continue;
       headings.Add(new Heading { Label=label,Original=Read(label),Applied="Multiworld Circuit",Color=label.color });
       Write(label,"Multiworld Circuit");
      }
     }
     var container=ui.TabManager.Tabs[ui.InternalCampaignTabIndex].Container;
     if(container!=null && container.gameObject.activeInHierarchy) {
      foreach(var label in container.GetComponentsInChildren<UnityEngine.UI.Graphic>(false)) {
       string value=Read(label);
       if(value==null || !System.Text.RegularExpressions.Regex.IsMatch(value,@"^anti-gravity league \[ \d+% \]$",System.Text.RegularExpressions.RegexOptions.IgnoreCase)) continue;
       string replacement="archipelago league"+value.Substring("anti-gravity league".Length);
       headings.Add(new Heading { Label=label,Original=value,Applied=replacement,Color=label.color });
       Write(label,replacement);
      }
     }
    }
   }
  }
  public void Dispose() { Restore(); }
 }
}
