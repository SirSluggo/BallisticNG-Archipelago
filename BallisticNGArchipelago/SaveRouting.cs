using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using NgModding;
using NgStats;
using NgSp;
using NgGame;
using NgEvents;
using NeoFs;
using UnityEngine;

namespace BallisticNGArchipelago {
 internal static class SaveRouting {
  private const string Owner="ballisticng.archipelago.isolatedsaves.31";
  private static string logical,physical,reportPath;
  private static volatile bool routing;
  private static bool started,ready;
  private static int mapped;
  private static readonly object sync=new object();
  private static readonly StringBuilder report=new StringBuilder();
  private static readonly HashSet<string> seen=new HashSet<string>();
  private static StringComparison Comparison { get { return Path.DirectorySeparatorChar=='\\' ? StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal; } }
  internal static void Note(string line) { lock(sync) { if(report.Length<131072) report.AppendLine(line); } }
  private static void Plain(string path) {
   for(string current=Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current)) {
    if((File.Exists(current)||Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException("Linked paths unsupported in isolation test: "+current);
   }
  }
  private static string Map(string path) {
   if(!routing || string.IsNullOrEmpty(path)) return path;
   string full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
   bool root=string.Equals(full,logical,Comparison);
   if(!root && !full.StartsWith(logical+Path.DirectorySeparatorChar,Comparison)) return path;
   string relative=root ? "":full.Substring(logical.Length+1);
   string target=root ? physical:Path.Combine(physical,relative);
   Plain(target);
   Interlocked.Increment(ref mapped);
   lock(sync) { if(seen.Count<128 && seen.Add(relative)) report.AppendLine("ROUTE "+relative); }
   return target;
  }
  public static void First(ref string __0) { __0=Map(__0); }
  public static void Second(ref string __1) { __1=Map(__1); }
  public static void Both(ref string __0,ref string __1) {
   string a=Map(__0),b=Map(__1);
   if(!string.Equals(a,__0,StringComparison.Ordinal) != !string.Equals(b,__1,StringComparison.Ordinal)) throw new IOException("Move across isolated save boundary blocked");
   __0=a;__1=b;
  }
  public static bool SkipPlatformAchievement() { return false; }
  public static void ResetCampaignMemory(NgCampaignList __instance) {
   if(!routing) return;
   foreach(NgCampaignList.EventGroup group in __instance.EventGroups) foreach(NgCampaignEvent ev in group.Events) {
    ev.EventProgress.CurrentAward=NgAward.None;
    ev.EventProgress.BeatInHardMode=false;
   }
   Note("CAMPAIGN_LOAD "+__instance.Name);
  }
  public static void DisableScores() { if(RaceManager.Instance!=null) RaceManager.Instance.LeaderboardsDisabled=true; }
  internal static void Flush() {
   if(reportPath==null) return;
   lock(sync) { File.WriteAllText(reportPath,report.ToString()+"\nMapped operations: "+mapped+"\nReady: "+ready+"\n"); }
  }
  public static void Open(string key,string modPath) {
   if(key=="normal") return;
   if(started) throw new Exception("Save routing already initialized");started=true;
   reportPath=RuntimeFiles.File(modPath,"save-isolation.txt");
   Note("BallisticNG AP Save Isolation v0.36 "+DateTime.UtcNow.ToString("o"));
   try {
    string user=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,"User"));
    logical=Path.Combine(user,"Save");
    string slot=Path.Combine(user,"ArchipelagoSaves","slots",key);
    physical=Path.Combine(slot,"Save");Plain(logical);Plain(physical);
    string marker=Path.Combine(slot,"profile-key.txt");Plain(marker);
    string identity=Path.Combine(slot,"identity.json");Plain(identity);
    if(!File.Exists(identity) || !Directory.Exists(physical) || !File.Exists(marker) || File.ReadAllText(marker).Trim()!=key) throw new IOException("Isolated AP profile is missing or mismatched; no empty replacement created");
    foreach(string name in new string[]{"Campaign","Precision","Times","Ghosts","Stats"}) Directory.CreateDirectory(Path.Combine(physical,name));
    Assembly lib=Assembly.LoadFrom(Path.Combine(modPath,"lib","0Harmony.dll"));
    Type ht=lib.GetType("HarmonyLib.Harmony",true),mt=lib.GetType("HarmonyLib.HarmonyMethod",true);
    object h=Activator.CreateInstance(ht,new object[]{Owner});
    MethodInfo patch=ht.GetMethod("Patch",new Type[]{typeof(MethodBase),mt,mt,mt,mt});
    if(patch==null) throw new MissingMethodException("Harmony Patch");
    var targets=new List<MethodBase>();
    foreach(Type t in new Type[]{typeof(NeoFileStreamWriter),typeof(NeoFileStreamReader),typeof(NeoFileWriter),typeof(NeoFileReader)}) targets.Add(t.GetConstructor(new Type[]{typeof(string),typeof(ENeoFileMode)}));
    foreach(string n in new string[]{"Exists","Delete","Move","ReadAllBytes","WriteAllBytes","ReadAllLines","WriteAllLines","ReadAllText","WriteAllText"}) targets.Add(typeof(NeoFile).GetMethod(n));
    foreach(string n in new string[]{"Create","Delete","DeleteAndRecreate","Exists","GetFiles","GetFilesExactExtensions","GetDirectories","GetFilesWithPredicate","GetFilesExactExtensionsWithPredicate"}) targets.Add(typeof(NeoDir).GetMethod(n));
    targets.Add(typeof(NgCampaignList).GetMethod("LoadProgress"));
    foreach(string n in new string[]{"SteamUnlockCheck","SteamClearCheck","GalaxyUnlockCheck","GalaxyClearCheck"}) targets.Add(typeof(NgAchievement).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic));
    foreach(MethodBase target in targets) if(target==null) throw new MissingMethodException("Required isolation hook missing");
    foreach(MethodBase target in targets) {
     string hook=target.DeclaringType==typeof(NgAchievement) ? "SkipPlatformAchievement":target.DeclaringType==typeof(NgCampaignList) ? "ResetCampaignMemory":target.Name=="Move" ? "Both":target.Name.EndsWith("WithPredicate",StringComparison.Ordinal) ? "Second":"First";
     object prefix=Activator.CreateInstance(mt,new object[]{typeof(SaveRouting).GetMethod(hook,BindingFlags.Public|BindingFlags.Static)});
     patch.Invoke(h,new object[]{target,prefix,null,null,null});
     Note("PATCHED "+target.DeclaringType.FullName+"."+target.Name);
    }
    routing=true;
    // Stats and achievements are registered before code mods: reload their cached
    // values from the isolated files, without calling achievement setters/Clear().
    FieldInfo stats=typeof(NgStat).GetField("LoadedStats",BindingFlags.Static|BindingFlags.NonPublic);
    FieldInfo achievements=typeof(NgAchievement).GetField("LoadedAchievements",BindingFlags.Static|BindingFlags.NonPublic);
    FieldInfo unlocked=typeof(NgAchievement).GetField("_isUnlocked",BindingFlags.Instance|BindingFlags.NonPublic);
    if(stats==null || achievements==null || unlocked==null) throw new MissingFieldException("Cached progress fields missing");
    foreach(NgStat stat in (IEnumerable)stats.GetValue(null)) stat.StatValue=NgStat.LoadStat(stat,0);
    foreach(NgAchievement achievement in (IEnumerable)achievements.GetValue(null)) unlocked.SetValue(achievement,NgAchievement.LoadAchievement(achievement));
    if(NgCampaignManager.Instance!=null) NgCampaignManager.Instance.LoadProgress();
    NgRaceEvents.OnPreShipsSpawned+=DisableScores;
    GameObject host=new GameObject("BallisticNG AP save routing");
    UnityEngine.Object.DontDestroyOnLoad(host);host.AddComponent<SaveRoutingStatus>();
    ready=true;Note("ACTIVE isolated AP profile: "+physical);Flush();
   } catch(Exception error) {
    Note("FAILED: "+error);try { Flush(); } catch(Exception write) { Debug.LogError(write); }
    throw;
    // Never remove routing during shutdown: late save writers must not return to
    // User/Save after isolated state has been loaded. Hooks die with the process.
   }
  }
 }
 internal sealed class SaveRoutingStatus : MonoBehaviour {
  private float next;
  private void Update() { if(Time.realtimeSinceStartup>=next) { next=Time.realtimeSinceStartup+5f;try { SaveRouting.Flush(); } catch(Exception error) { Debug.LogError(error); } } }
  private void OnApplicationQuit() { SaveRouting.Note("QUIT "+DateTime.UtcNow.ToString("o"));try { SaveRouting.Flush(); } catch(Exception error) { Debug.LogError(error); } }
 }
}
