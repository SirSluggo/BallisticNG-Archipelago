using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using NgAudio;
using UnityEngine;
using NgModding;
using NgData;
using NgGame;
using NgEvents;
using NgShips;
using NgIo;
using NgSettings;
using NgMp;
using NgIntegration.Vr;
using NgAssets;
using NgEffects;
using NgUi.MenuUi;

namespace BallisticNGArchipelago {
 // Trap effects and durable AP delivery. Debug controls are explicitly opt-in.
 public sealed class TrapLabMod : CodeMod {
  public override void OnRegistered(string path) {
   if(TrapLab.Instance!=null) return;
   GameObject host=new GameObject("Archipelago Trap Lab");
   UnityEngine.Object.DontDestroyOnLoad(host);
   host.AddComponent<TrapLab>().Initialize(path);
  }
 }

 [DefaultExecutionOrder(-10000)]
 public sealed class TrapLab : MonoBehaviour {
  public static TrapLab Instance;
  private static readonly string[] Names={"Airbrake Mix-Up Trap","\"Gotta Go Fast!\" Trap","Pit Crew Miscalculation Trap","Cockpit Lockdown Trap","He’s Right Behind Me, Isn’t He? Trap","Art Appreciation Trap","Backseat Gaming Trap","Running in the 90s Trap","Mirror Trap"};
  private static readonly float[] Durations={8f,8f,0f,15f,3f,5f,8f,20f,10f};
  private sealed class Request { public int Kind;public string Receipt,Session; }
  private bool labEnabled;
  private string remoteSession,activeReceipt;
  private float nextRemotePoll,nextAutoArtAt;
  private readonly HashSet<string> seenRemote=new HashSet<string>(),startedRemote=new HashSet<string>();
  private readonly Queue<Request> queue=new Queue<Request>();
  private readonly List<Sprite> art=new List<Sprite>();
  private ShipController target;
  private ShipCamera cameraController;
  private Camera renderCamera;
  private int selected,active=-1,originalView;
  private bool originalBehind,originalLook,originalAutopilot,originalTimerMode;
  private float originalFov,originalAutopilotTimer,remaining,cooldown,forcedFov;
  private Sprite activeArt;
  private bool visible,wasRace;
  private string directory,note="Select a trap here, then press F7 after the race countdown.";
  private float noticeUntil;
  private sealed class ArtPage { public string Id;public readonly List<Sprite> Images=new List<Sprite>();public Sprite Last; }
  private readonly List<ArtPage> artPages=new List<ArtPage>(),artOrder=new List<ArtPage>();
  private ArtPage lastArtPage;
  private bool artLoading;
  private AudioSource trapSound;
  private MirrorRaceView mirror;
  private static readonly string[] RetroGlobals={"ps1effects","_VertexSnapping","_RetroClippingDistance","_AffineTextureMapping"};
  private float[] retroOriginal;
  private NgColorDithering retroDither;
  private CRTEffect retroCrt;
  private string retroCrtSnapshot;
  private bool retroCaptured,retroDitherEnabled,retroCrtEnabled;
  private GUIStyle text,small;

  public int PendingCount { get { return queue.Count; } }
  public void Initialize(string path) {
   Instance=this;directory=path;labEnabled=File.Exists(Path.Combine(path,"enable-trap-test.txt"));
   NgRaceEvents.OnPreShipsSpawned+=OnReset;
   NgRaceEvents.OnEventExit+=OnReset;
   NgRaceEvents.OnShipExploded+=OnShipDone;
   NgRaceEvents.OnShipFinished+=OnShipDone;
   Log("ready","AP trap runtime v0.31; debug controls enabled="+labEnabled);
  }
  private void Log(string action,string detail) {
   try {
    string file=RuntimeFiles.File(directory,"trap-test.log");RuntimeFiles.Rotate(file);
    if(File.Exists(file) && new FileInfo(file).Length>262144) File.Move(file,file+"."+DateTime.UtcNow.Ticks+".old");
    File.AppendAllText(file,DateTime.UtcNow.ToString("o")+" "+action+" "+detail.Replace('\r',' ').Replace('\n',' ')+"\n");
   } catch { }
  }
  private void Notice(string value) { note=value;noticeUntil=Time.realtimeSinceStartup+5f; }
  private static bool Menu() { return NgSceneManager.GetLoadedScene()=="Menu"; }
  private static bool LocalShip(ShipController ship) {
   return ship!=null && ship==Ships.PlayerOneShip && !ship.IsNetworked && NgNetworkBase.CurrentNetwork==null;
  }
  private static bool Racing(ShipController ship) {
   return !Menu() && LocalShip(ship) && !ship.Eliminated && !ship.FinishedEvent &&
    Race.HasCountdownFinished && !Race.AllShipsRestrained && !ship.ShipRestrained;
  }
  private void OnReset() { CancelAll("Race restarted or exited."); }
  private void OnShipDone(ShipController ship) { if(ship==target || ship==Ships.PlayerOneShip) CancelAll("Player finished or was eliminated."); }

  private void Update() {
   try {
    RefreshRemote();
    if(labEnabled && (ChatPanel.Instance==null || !ChatPanel.Instance.Visible) && Input.GetKeyDown(KeyCode.F6)) visible=!visible;
    if(labEnabled && (ChatPanel.Instance==null || !ChatPanel.Instance.Visible) && Input.GetKeyDown(KeyCode.F10)) CancelAll("Cancelled (F10).");
    if(labEnabled && (ChatPanel.Instance==null || !ChatPanel.Instance.Visible) && Input.GetKeyDown(KeyCode.F7)) Enqueue(selected);
    bool inRace=!Menu() && LocalShip(Ships.PlayerOneShip);
    if(wasRace && !inRace) CancelAll("Left race scene.");
    wasRace=inRace;
    if(active>=0) {
     if(!Racing(target)) { CancelAll("Race no longer active.");return; }
     if(NgGameState.IsPaused) return;
     remaining-=Time.deltaTime;
     if(remaining<=0f) { StopEffect("expired");cooldown=0.75f; }
    } else if(!NgGameState.IsPaused && Racing(Ships.PlayerOneShip)) {
     cooldown=Mathf.Max(0f,cooldown-Time.deltaTime);
     if(cooldown<=0f && queue.Count>0) StartEffect(queue.Dequeue());
    }
   } catch(Exception ex) { Fail(ex); }
  }
  private void RefreshRemote() {
   if(Time.realtimeSinceStartup<nextRemotePoll) return;
   nextRemotePoll=Time.realtimeSinceStartup+0.25f;
   string token=LoggerBehaviour.Instance==null ? null:LoggerBehaviour.Instance.TrapSession;
   if(token!=remoteSession) {
    if(remoteSession!=null) CancelAll("AP session changed; unstarted traps remain queued in the bridge.");
    remoteSession=token;seenRemote.Clear();startedRemote.Clear();nextAutoArtAt=0f;
    if(token!=null) LoadStartedAcknowledgments(token);
   }
   if(token==null) return;
   string path=RuntimeFiles.File(directory,"traps-"+token+".txt");
   if(!File.Exists(path) || new FileInfo(path).Length>65536) return;
   string[] lines;
   using(FileStream file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete))
   using(StreamReader reader=new StreamReader(file)) lines=reader.ReadToEnd().Replace("\r","").Split('\n');
   if(lines.Length<2 || lines[0]!=token || lines[1]!=Catalog.Digest) return;
   foreach(string line in lines) {
    string[] fields=line.Split('\t');int kind;
    if(fields.Length!=2 || !int.TryParse(fields[1],out kind) || kind<0 || kind>=Names.Length || fields[0].Length>128) continue;
    string[] parts=fields[0].Split(':');long index;int item,location,player;
    if(parts.Length!=4 || !long.TryParse(parts[0],out index) || index<0 || !int.TryParse(parts[1],out item) || item!=21598000+kind ||
       !int.TryParse(parts[2],out location) || !int.TryParse(parts[3],out player) || player<0) continue;
    if(seenRemote.Contains(fields[0]) || queue.Count>=256) continue;
    seenRemote.Add(fields[0]);queue.Enqueue(new Request { Kind=kind,Receipt=fields[0],Session=token });
    Log("remote_queued",Names[kind]+" receipt="+fields[0]);
   }
   if(Menu() && !artLoading && artPages.Count==0 && Time.realtimeSinceStartup>=nextAutoArtAt) {
    bool needsArt=false;foreach(Request request in queue) if(request.Receipt!=null && request.Kind==5) needsArt=true;
    if(needsArt) { nextAutoArtAt=Time.realtimeSinceStartup+15f;PrepareArt(); }
   }
  }
  private void LoadStartedAcknowledgments(string token) {
   string path=RuntimeFiles.File(directory,"trap-acks-"+token+".txt");
   if(!File.Exists(path) || new FileInfo(path).Length>131072) return;
   string[] lines=File.ReadAllLines(path);
   if(lines.Length<2 || lines[0]!=token || lines[1]!=Catalog.Digest) return;
   for(int i=2;i<lines.Length;i++) if(lines[i].Length>0 && lines[i].Length<=128) {
    startedRemote.Add(lines[i]);seenRemote.Add(lines[i]);
   }
  }
  private bool Acknowledge(Request request) {
   if(request.Receipt==null) return true;
   if(request.Session!=remoteSession) return false;
   try {
    HashSet<string> receipts=new HashSet<string>(startedRemote);receipts.Add(request.Receipt);
    string path=RuntimeFiles.File(directory,"trap-acks-"+request.Session+".txt"),temp=path+".tmp";
    using(FileStream stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
     using(StreamWriter writer=new StreamWriter(stream, new System.Text.UTF8Encoding(false),1024,true)) {
      writer.WriteLine(request.Session);writer.WriteLine(Catalog.Digest);
      foreach(string receipt in receipts) writer.WriteLine(receipt);
      writer.Flush();
     }
     stream.Flush(true);
    }
    if(File.Exists(path)) File.Replace(temp,path,null);else File.Move(temp,path);
    startedRemote.Add(request.Receipt);seenRemote.Add(request.Receipt);activeReceipt=request.Receipt;
    return true;
   } catch(Exception ex) { Log("trap_ack_error",ex.Message);return false; }
  }
  private void Defer(Request request,string reason) {
   queue.Enqueue(request);cooldown=1f;
   if(note!=reason) Notice(reason);
  }
  private void Skip(Request request,string reason) {
   if(!Acknowledge(request)) { Defer(request,"Trap start could not be recorded; waiting to retry.");return; }
   activeReceipt=null;Notice(reason);Log("skipped",Names[request.Kind]+" "+reason);
  }
  public void Enqueue(int kind) {
   if(kind<0 || kind>=Names.Length) return;
   if(Menu() || !LocalShip(Ships.PlayerOneShip)) { Notice("Enter a local race first; select the trap here and press F7 while racing.");return; }
   if(queue.Count>=16) { Notice("Test queue full. F10 clears it.");return; }
   queue.Enqueue(new Request { Kind=kind });
   Notice("Queued: "+Names[kind]+". Starts after countdown, one effect at a time.");
   Log("queued",Names[kind]);
  }
  private void StartEffect(Request request) {
   if(request.Receipt!=null && request.Session!=remoteSession) return;
   target=Ships.PlayerOneShip;
   if(!Racing(target)) { if(request.Receipt!=null) Defer(request,"Trap waiting for racing.");return; }
   cameraController=target.CamSim;renderCamera=target.ShipCamera;
   int kind=request.Kind;
   if((kind==1 || kind==3 || kind==4) && VrNg.VrEnabled) { Skip(request,"Camera trap skipped in VR.");return; }
   if((kind==1 || kind==3 || kind==4) && (cameraController==null || renderCamera==null || !cameraController.ModulesSetup)) {
    Defer(request,"Camera is not ready; trap is waiting.");return;
   }
   if(kind==3 && (target.Settings.REF_COCKPITMESH==null || target.CockpitParent==null || cameraController.CameraModules.Length<4)) {
    Skip(request,"This ship has no usable cockpit; Cockpit Lockdown skipped.");return;
   }
   if(kind==5) {
    activeArt=PickArt();
    if(activeArt==null) { if(request.Receipt!=null) Defer(request,"Art trap waiting: gallery art will load when you return to the menu.");else Notice("Load concept art from the F6 panel while in the menu first.");return; }
   }
   if(kind==6 && target.AutopilotActivated) { if(request.Receipt!=null) Defer(request,"Backseat Gaming is waiting for native autopilot to finish.");else Notice("Native autopilot is already active; Backseat Gaming skipped.");return; }
   if(kind==7) {
    if(VrNg.VrEnabled || Gameplay.SplitscreenEnabled) { Skip(request,"Retro trap skipped in VR or split-screen.");return; }
    if(renderCamera==null) { Defer(request,"Retro trap waiting for the player camera.");return; }
    retroDither=renderCamera.GetComponent<NgColorDithering>();retroCrt=renderCamera.GetComponent<CRTEffect>();
    if(retroDither==null || retroCrt==null) { Skip(request,"Native CRT/dithering components are unavailable; retro trap skipped.");return; }
   }
   if(kind==8) {
    if(VrNg.VrEnabled || Gameplay.SplitscreenEnabled) { Skip(request,"Mirror Trap skipped in VR or split-screen.");return; }
    if(renderCamera==null) { Defer(request,"Mirror Trap waiting for the race camera.");return; }
   }
   if(!Acknowledge(request)) { Defer(request,"Trap start could not be recorded; waiting to retry.");return; }
   active=kind;remaining=Durations[kind];forcedFov=170f;
   if(kind==7) BeginRetro();
   if(kind==8) {
    mirror=renderCamera.GetComponent<MirrorRaceView>();
    if(mirror==null) mirror=renderCamera.gameObject.AddComponent<MirrorRaceView>();
    mirror.enabled=true;
   }
   PlayTrapSound();
   if(kind==1) originalFov=renderCamera.fieldOfView;
   if(kind==3 || kind==4) {
    originalView=cameraController.CameraMode;originalBehind=cameraController.LookingBehind;originalLook=cameraController.CanLookBehind;
    ForceView(kind==3 ? 3:originalView,kind==4);
   }
   if(kind==2) {
    float before=target.ShieldIntegrity;
    // Never heal a ship already below 1; the trap's own deduction is nonlethal.
    if(before>1f) target.ShieldIntegrity=Mathf.Max(1f,before-30f);
    Log("shield",before.ToString("0.0")+" -> "+target.ShieldIntegrity.ToString("0.0"));
    Notice("Pit Crew Miscalculation: -"+(before-target.ShieldIntegrity).ToString("0.0")+" shield.");
    StopEffect("applied");cooldown=0.75f;return;
   }
   if(kind==6) {
    originalAutopilot=target.AutopilotActivated;originalTimerMode=target.AutopilotOnTimer;originalAutopilotTimer=target.AutopilotTimer;
    target.AutopilotActivated=true;target.AutopilotOnTimer=true;target.AutopilotTimer=8f;
   }
   Notice(Names[kind]+(kind==1 ? " ("+forcedFov+" degrees)":"")+" active.");
   Log("started",Names[kind]+" duration="+remaining+" fov="+forcedFov);
  }
  private void BeginRetro() {
   // Snapshot live rendering only: never assign Gfx settings or call a config save.
   retroOriginal=new float[RetroGlobals.Length];
   for(int i=0;i<RetroGlobals.Length;i++) retroOriginal[i]=Shader.GetGlobalFloat(RetroGlobals[i]);
   retroDitherEnabled=retroDither.enabled;retroCrtEnabled=retroCrt.enabled;
   retroCrtSnapshot=JsonUtility.ToJson(retroCrt);
   if(string.IsNullOrEmpty(retroCrtSnapshot)) throw new Exception("Could not snapshot native CRT parameters.");
   retroCaptured=true;
   BaseCRTEffect.SetupBnGPreset(retroCrt,SettingsB.Low,SettingsE.Large,SettingsB.High,SettingsB.High,SettingsB.High);
   ApplyRetro();
  }
  private void ApplyRetro() {
   Shader.SetGlobalFloat("ps1effects",1f);Shader.SetGlobalFloat("_VertexSnapping",1f);
   Shader.SetGlobalFloat("_RetroClippingDistance",60f);Shader.SetGlobalFloat("_AffineTextureMapping",1f);
   if(retroDither!=null && !retroDither.enabled) retroDither.enabled=true;
   if(retroCrt!=null) {
    if(retroCrt.textureSize!=480) BaseCRTEffect.SetupBnGPreset(retroCrt,SettingsB.Low,SettingsE.Large,SettingsB.High,SettingsB.High,SettingsB.High);
    if(!retroCrt.enabled) retroCrt.enabled=true;
   }
  }
  private void RestoreRetro() {
   if(!retroCaptured) return;
   try {
    if(retroCrt!=null) JsonUtility.FromJsonOverwrite(retroCrtSnapshot,retroCrt);
   } finally {
    if(retroCrt!=null) retroCrt.enabled=retroCrtEnabled;
    if(retroDither!=null) retroDither.enabled=retroDitherEnabled;
    for(int i=0;i<RetroGlobals.Length;i++) Shader.SetGlobalFloat(RetroGlobals[i],retroOriginal[i]);
    retroCaptured=false;retroCrt=null;retroDither=null;retroCrtSnapshot=null;retroOriginal=null;
   }
  }
  private void PlayTrapSound() {
   try {
    if(trapSound!=null && trapSound.isPlaying) trapSound.Stop();
    // Native sound-pack key is thunder.wav; use SFX volume, not the voice-scaled interface channel.
    trapSound=ApAudio.Play(NgSound.Env_Thunder,EAudioChannel.Sfx);
   } catch(Exception ex) { Log("sound_error",ex.Message); }
  }
  private void ForceView(int mode,bool behind) {
   if(cameraController==null || target==null) return;
   bool changed=cameraController.CameraMode!=mode || cameraController.LookingBehind!=behind;
   cameraController.CameraMode=mode;cameraController.LookingBehind=behind;cameraController.CanLookBehind=false;
   if(changed) {
    ShipCameraModule module=cameraController.GetCurrentModule();
    if(module!=null) module.OnChangedTo(target);
   }
  }
  // Run before native ShipController.FixedUpdate so physics sees the altered inputs.
  // Re-read the native axes each tick: never swap an already-swapped cached value.
  private void FixedUpdate() {
   if(active<0 || !Racing(target) || NgGameState.IsPaused || target.Input==null) return;
   try {
    if(active==0 && !target.AutopilotActivated) {
     float left=NgIn.GetAxis(NgIn.AxisLeftAirbrake,target.playerIndex),right=NgIn.GetAxis(NgIn.AxisRightAirbrake,target.playerIndex);
     if(Gameplay.MirrorEnabled) { float saved=left;left=-right;right=Mathf.Abs(saved); }
     target.Input.AxisLeftAirbrake=-Mathf.Abs(right);target.Input.AxisRightAirbrake=Mathf.Abs(left);
     target.Input.AxisBothAirbrakes=target.Input.AxisLeftAirbrake+target.Input.AxisRightAirbrake;
    }
   } catch(Exception ex) { Fail(ex); }
  }
  private void LateUpdate() {
   if(active<0 || !Racing(target) || NgGameState.IsPaused) return;
   try {
    if(active==1 && renderCamera!=null) renderCamera.fieldOfView=forcedFov;
    if(active==3) ForceView(3,false);
    if(active==4) ForceView(originalView,true);
    if(active==7) ApplyRetro();
   } catch(Exception ex) { Fail(ex); }
  }
  private void RestoreAxes() {
   if(target==null || target.Input==null || target.Eliminated || target.FinishedEvent || target.AutopilotActivated) return;
   float left=NgIn.GetAxis(NgIn.AxisLeftAirbrake,target.playerIndex),right=NgIn.GetAxis(NgIn.AxisRightAirbrake,target.playerIndex);
   if(Gameplay.MirrorEnabled) { float saved=left;left=-right;right=Mathf.Abs(saved); }
   target.Input.AxisLeftAirbrake=left;target.Input.AxisRightAirbrake=right;target.Input.AxisBothAirbrakes=left+right;
  }
  private void StopEffect(string reason) {
   int kind=active;active=-1;remaining=0f;
   try {
    if(mirror!=null) { mirror.enabled=false;mirror=null; }
    if(kind==7) RestoreRetro();
    if(kind==0) RestoreAxes();
    if(kind==1 && renderCamera!=null) {
     renderCamera.fieldOfView=originalFov;
     if(target!=null && !target.FinishedEvent && !target.Eliminated && cameraController!=null && cameraController.GetCurrentModule()!=null)
      cameraController.GetCurrentModule().OnUpdate(target);
    }
    if((kind==3 || kind==4) && cameraController!=null) {
     cameraController.CameraMode=originalView;cameraController.LookingBehind=originalBehind;cameraController.CanLookBehind=originalLook;
     if(target!=null && !target.FinishedEvent && !target.Eliminated && cameraController.GetCurrentModule()!=null)
      cameraController.GetCurrentModule().OnChangedTo(target);
    }
    // A real Autopilot pickup switches AutopilotOnTimer off. Do not cancel that pickup.
    if(kind==6 && target!=null && target.AutopilotOnTimer && target.AutopilotTimer<=8f) {
     target.AutopilotActivated=originalAutopilot;target.AutopilotOnTimer=originalTimerMode;target.AutopilotTimer=originalAutopilotTimer;
    }
   } catch(Exception ex) { Log("restore_error",ex.ToString()); }
   finally {
    if(kind>=0) Log("stopped",Names[kind]+" "+reason);
    activeReceipt=null;activeArt=null;target=null;cameraController=null;renderCamera=null;
   }
  }
  private void CancelAll(string reason) { bool hadEffect=active>=0;queue.Clear();seenRemote.Clear();foreach(string receipt in startedRemote) seenRemote.Add(receipt);if(trapSound!=null) trapSound.Stop();StopEffect(reason);cooldown=0f;if(labEnabled || hadEffect) Notice(reason); }
  private void Fail(Exception ex) { visible=false;Log("error",ex.ToString());CancelAll("Effect stopped after an error; see trap-test.log."); }

  private static bool ArtLabel(string value) {
   if(value==null) return false;value=value.ToLowerInvariant();
   return value.Contains("concept") || value.Contains("artwork") || value.Contains("sketch") || value.Contains("design") || value.Contains("illustration") || value.Contains("art ") || value=="art";
  }
  private void PrepareArt() {
   if(!Menu()) { Notice("Load gallery art from the main menu, before racing.");return; }
   if(artLoading) return;
   try {
    var galleries=Resources.FindObjectsOfTypeAll<NgGallery>();
    foreach(var gallery in galleries) if(gallery!=null && gallery.gameObject.activeInHierarchy) {
     Notice("Close the native Gallery before loading trap art.");return;
    }
    List<string> candidates=new List<string>();
    foreach(var gallery in galleries) {
     if(gallery==null || gallery.Categories==null) continue;
     foreach(var category in gallery.Categories) {
      if(category.Items==null) continue;
      foreach(var entry in category.Items) {
       Log("gallery_catalog",category.Name+" | "+entry.Header+" | "+entry.DisplayName+" | "+entry.AssetName);
       if((ArtLabel(category.Name) || ArtLabel(entry.Header) || ArtLabel(entry.DisplayName)) && !string.IsNullOrEmpty(entry.AssetName) && !candidates.Contains(entry.AssetName))
        candidates.Add(entry.AssetName);
      }
     }
    }
    art.Clear();artPages.Clear();artOrder.Clear();lastArtPage=null;
    if(candidates.Count==0) { Notice("No concept-art page identified. Open and close Records & Extras > Gallery, then retry; send trap-test.log if needed.");return; }
    GalleryAssets.Load();
    artLoading=true;
    StartCoroutine(LoadArtPages(candidates));
   } catch(Exception ex) { artLoading=false;Log("art_error",ex.ToString());Notice("Gallery art could not load; other traps are available. Send trap-test.log."); }
  }


  private IEnumerator LoadArtPages(List<string> candidates) {
   for(int i=0;i<candidates.Count;i++) {
    if(!Menu()) break;
    try {
     var page=GalleryAssets.GetUi(candidates[i]);
     var group=new ArtPage { Id=candidates[i] };
     if(page!=null && page.CarouselEntries!=null) foreach(var entry in page.CarouselEntries) {
      if(entry.Image==null || entry.Image.texture==null || art.Contains(entry.Image)) continue;
      art.Add(entry.Image);group.Images.Add(entry.Image);
     }
     if(group.Images.Count>0) artPages.Add(group);
     Log("art_page",candidates[i]+" unique_images="+group.Images.Count);
    } catch(Exception ex) { Log("art_page_error",candidates[i]+" "+ex.Message); }
    Notice("Loading art page "+(i+1)+"/"+candidates.Count+". Wait for Concept art ready before racing.");
    yield return null;
   }
   artLoading=false;
   Notice(art.Count>0 ? "Concept art ready: "+art.Count+" images across "+artPages.Count+" pages." : "No carousel images found. Send trap-test.log.");
   Log("art_ready","images="+art.Count+" pages="+artPages.Count);
  }
  private Sprite PickArt() {
   foreach(ArtPage page in artPages) page.Images.RemoveAll(delegate(Sprite sprite) { return sprite==null || sprite.texture==null; });
   artPages.RemoveAll(delegate(ArtPage page) { return page.Images.Count==0; });
   artOrder.RemoveAll(delegate(ArtPage page) { return !artPages.Contains(page); });
   if(artPages.Count==0) return null;
   if(artOrder.Count==0) artOrder.AddRange(artPages);
   // Visit every loaded page once per shuffled cycle, instead of weighting pages by image count.
   List<ArtPage> choices=artOrder.FindAll(delegate(ArtPage page) { return page!=lastArtPage; });
   if(choices.Count==0) choices.AddRange(artOrder);
   ArtPage selectedPage=choices[UnityEngine.Random.Range(0,choices.Count)];
   artOrder.Remove(selectedPage);lastArtPage=selectedPage;
   List<Sprite> images=selectedPage.Images.FindAll(delegate(Sprite sprite) { return sprite!=selectedPage.Last; });
   if(images.Count==0) images.AddRange(selectedPage.Images);
   Sprite image=images[UnityEngine.Random.Range(0,images.Count)];selectedPage.Last=image;
   Log("art_pick",selectedPage.Id+" | "+image.name);
   return image;
  }

  private void OnGUI() {
   try {
    if(text==null) {
     text=new GUIStyle(GUI.skin.label);text.richText=false;text.wordWrap=true;
     small=new GUIStyle(GUI.skin.label);small.richText=false;small.alignment=TextAnchor.MiddleCenter;
    }
    float scale=Mathf.Clamp(Screen.height/1080f,1f,3f);
    small.fontSize=Mathf.RoundToInt(15f*scale);
    int depth=GUI.depth;GUI.depth=-150;
    try {
     if(active==5 && activeArt!=null && activeArt.texture!=null) DrawArt(scale);
     if(active>=0) GUI.Label(new Rect(Screen.width*.25f,Screen.height*.13f,Screen.width*.5f,30f*scale),Names[active]+" — "+Mathf.CeilToInt(remaining)+"s"+(labEnabled ? "  |  F10 cancels":""),small);
     else if(!visible && Time.realtimeSinceStartup<noticeUntil)
      GUI.Label(new Rect(Screen.width*.2f,Screen.height*.13f,Screen.width*.6f,55f*scale),note,small);
     if(labEnabled && visible) DrawPanel(scale);
    } finally { GUI.depth=depth; }
   } catch(Exception ex) { Fail(ex); }
  }
  private void DrawArt(float scale) {
   Rect pixels=activeArt.textureRect;Texture2D texture=activeArt.texture;
   float aspect=pixels.width/Mathf.Max(1f,pixels.height);
   float width=Mathf.Min(Screen.width*.25f,Screen.height*.3f*aspect),height=width/aspect;
   Rect rect=new Rect((Screen.width-width)*.5f,(Screen.height-height)*.5f,width,height);
   GUI.Box(new Rect(rect.x-8f*scale,rect.y-30f*scale,width+16f*scale,height+38f*scale),"");
   GUI.Label(new Rect(rect.x,rect.y-29f*scale,width,27f*scale),"Art Appreciation",small);
   GUI.DrawTextureWithTexCoords(rect,texture,new Rect(pixels.x/texture.width,pixels.y/texture.height,pixels.width/texture.width,pixels.height/texture.height));
  }
  private void DrawPanel(float scale) {
   Matrix4x4 matrix=GUI.matrix;GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1f));
   try {
    float left=Mathf.Max(12f,Screen.width/scale-560f),top=130f;
    GUI.Box(new Rect(left,top,540f,590f),"TRAP LAB v0.31 — F6 close | F7 run | F10 cancel");
    text.fontSize=15;
    GUI.Label(new Rect(left+15f,top+28f,510f,46f),"Local effect tests only. No Archipelago items or checks. Select a trap; press F7 after the countdown.",text);
    for(int i=0;i<Names.Length;i++) {
     if(GUI.Button(new Rect(left+15f,top+80f+i*34f,510f,30f),(selected==i ? "> ":"")+Names[i]+(Durations[i]>0 ? "  ("+Durations[i]+"s)":"  (-30 shield)"))) selected=i;
    }
    GUI.Label(new Rect(left+15f,top+395f,510f,30f),"FOV: 170 degrees | Retro: 20s | Thunder on activation",text);
    bool enabled=GUI.enabled;GUI.enabled=enabled && !Menu() && LocalShip(Ships.PlayerOneShip);
    if(GUI.Button(new Rect(left+15f,top+391f,250f,34f),"Run selected (F7)")) Enqueue(selected);
    GUI.enabled=enabled;
    if(GUI.Button(new Rect(left+275f,top+391f,250f,34f),"Cancel + clear queue (F10)")) CancelAll("Cancelled.");
    GUI.enabled=enabled && Menu() && !artLoading;
    if(GUI.Button(new Rect(left+15f,top+433f,510f,32f),"Load concept art (menu only)")) PrepareArt();
    GUI.enabled=enabled;
    GUI.Label(new Rect(left+15f,top+475f,510f,70f),note,text);
    GUI.Label(new Rect(left+15f,top+548f,510f,30f),"Queued: "+queue.Count+" | Art images: "+art.Count+" | Effects pause with the race.",text);
   } finally { GUI.matrix=matrix; }
  }
  private void OnApplicationQuit() { CancelAll("Game quitting."); }
  private void OnDisable() { StopAllCoroutines();artLoading=false;if(directory!=null) CancelAll("Trap Lab disabled."); }
  private void OnDestroy() {
   CancelAll("Trap Lab unloaded.");
   NgRaceEvents.OnPreShipsSpawned-=OnReset;NgRaceEvents.OnEventExit-=OnReset;
   NgRaceEvents.OnShipExploded-=OnShipDone;NgRaceEvents.OnShipFinished-=OnShipDone;
   if(Instance==this) Instance=null;
  }
 }
}
