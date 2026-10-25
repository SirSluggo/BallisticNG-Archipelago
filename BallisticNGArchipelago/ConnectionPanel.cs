using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace BallisticNGArchipelago {
 public sealed class ConnectionPanel : MonoBehaviour {
  private string directory, server="localhost",port="38281",slot="BNGTest",password="";
  private string nonce="",connectionState="disconnected",connectedKey="",message="Not connected.",recent="";
  private string recoveryId="",recoveryText="";
  private bool confirmRecovery;
  private Vector2 recoveryScroll,panelScroll;
  private float guiScale=1f;
  private GUIStyle warningStyle;
  public void Hide() { visible=false; }
  private SaveProfile profile;
  private bool visible;
  private float nextPoll;
  private Rect window;
  public void Initialize(string path,SaveProfile save) {
   directory=path;profile=save;
   try {
    string file=RuntimeFiles.File(directory,"panel-settings.txt");
    if(File.Exists(file)) { string[] lines=File.ReadAllLines(file);if(lines.Length==3) { server=lines[0];port=lines[1];slot=lines[2]; } }
   } catch { message="Could not read remembered settings."; }
  }
  private string PathFor(string name) { return RuntimeFiles.File(directory,name); }
  private bool Fresh() {
   string file=PathFor("panel-status.txt");
   return File.Exists(file) && (DateTime.UtcNow-File.GetLastWriteTimeUtc(file)).TotalSeconds<15;
  }
  private void Update() {
   if(Input.GetKeyDown(KeyCode.F2)) {
    if(NgGame.NgSceneManager.GetLoadedScene()=="Menu") {
     if(ChatPanel.Instance!=null) ChatPanel.Instance.Close();
     visible=!visible;guiScale=Mathf.Clamp(Screen.height/1080f,.75f,2f);
     window=new Rect(Math.Max(10,(Screen.width/guiScale-680)/2),45,680,Mathf.Min(850f,Screen.height/guiScale-65f));
    }
   }
   if(Time.realtimeSinceStartup<nextPoll) return;nextPoll=Time.realtimeSinceStartup+.5f;
   if(Time.realtimeSinceStartup<localMessageUntil) return;
   try {
    string file=PathFor("panel-status.txt");
    if(File.Exists(file)) {
     string[] lines=File.ReadAllLines(file);
     if(lines.Length>=4) { nonce=lines[0];connectionState=lines[1];connectedKey=lines[2];message=lines[3]; }
    }
    if(!Fresh() && connectionState!="error" && !(connectionState=="setup" && setupProcess!=null && !setupProcess.HasExited)) { connectionState="disconnected";message="Background client is not active. Connect to start it."; }
    if(connectionState=="recovery") {
     string report=PathFor("panel-recovery.txt");
     if(File.Exists(report)) {
      string[] lines=File.ReadAllLines(report);
      if(lines.Length>=3 && lines[0]==nonce && lines[1].Length==64) {
       if(recoveryId!=lines[1]) { confirmRecovery=false;recoveryScroll=Vector2.zero; }
       recoveryId=lines[1];recoveryText=string.Join("\n",lines,2,lines.Length-2);
      }
     }
    } else { recoveryId="";recoveryText="";confirmRecovery=false; }
    string log=PathFor("panel-bridge.log");
    if(File.Exists(log)) {
     using(FileStream stream=new FileStream(log,FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete))
     using(StreamReader reader=new StreamReader(stream)) {
      string text=reader.ReadToEnd();string[] lines=text.Split('\n');int first=Math.Max(0,lines.Length-7);
      recent=string.Join("\n",lines,first,lines.Length-first);
     }
    }
   } catch { message="Panel status temporarily unavailable; retrying."; }
  }
  private static string Clean(string value) { return value.Replace("\r","").Replace("\n","").Trim(); }
  private void SaveSettings() { server=Clean(server);port=Clean(port);slot=Clean(slot);File.WriteAllLines(PathFor("panel-settings.txt"),new string[] {server,port,slot}); }
  private System.Diagnostics.Process setupProcess;
  private float localMessageUntil;
  private void Connect() {
   localMessageUntil=Time.realtimeSinceStartup+8f;
   try {
    SaveSettings();
    if(setupProcess!=null) { setupProcess.Dispose();setupProcess=null; }
    if(File.Exists(PathFor("bridge.lock")) && File.ReadAllText(PathFor("bridge.lock")).Trim()!="BNG-OS-LOCK-1") { message="A bridge already owns bridge.lock. Close the old terminal/client first. After a crash, remove the stale lock only when no bridge is running.";return; }
    string runtime=PathFor("PanelRuntime"),python=Path.Combine(runtime,".venv/Scripts/python.exe");
    var info=new System.Diagnostics.ProcessStartInfo();info.FileName=File.Exists(python) ? python:"py";
    info.Arguments=(File.Exists(python) ? "-u ":"-3 -u ")+"\""+Path.Combine(runtime,"bootstrap.py")+"\"";
    info.WorkingDirectory=Environment.CurrentDirectory;info.UseShellExecute=false;info.CreateNoWindow=true;info.RedirectStandardInput=true;
    var process=System.Diagnostics.Process.Start(info);
    foreach(string value in new string[] {server,port,slot,password}) process.StandardInput.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(value)));
    process.StandardInput.Close();setupProcess=process;password="";
    connectionState="setup";message="Preparing Python client. First setup may take a few minutes...";
   } catch(Exception ex) { connectionState="error";password="";message="Could not start client ("+ex.GetType().Name+"). Install Python 3.11 or newer, then try Connect. See setup-panel.log if present."; }
  }
  private bool Command(string action) {
   if(!Fresh() || nonce.Length!=32) { message="No active panel client. Connect first.";return false; }
   string temp=PathFor("panel-command.tmp");File.WriteAllLines(temp,new string[] {nonce,action});
   string target=PathFor("panel-command.txt");if(File.Exists(target)) { message="A command is already pending.";File.Delete(temp);return false; }
   File.Move(temp,target);return true;
  }
  private void Restart(string action) {
   try {
    if(action=="ap" && connectionState!="connected") { message="Connect to a validated room first.";return; }
    if(action=="normal") {
     string python=Path.Combine(PathFor("PanelRuntime"),".venv/Scripts/python.exe");
     if(!File.Exists(python)) { message="Run Setup-Panel.cmd before restarting into your normal save.";return; }
     var info=new System.Diagnostics.ProcessStartInfo();info.FileName=python;
     info.Arguments="-u \""+Path.Combine(PathFor("PanelRuntime"),"offline_normal.py")+"\"";
     info.WorkingDirectory=Environment.CurrentDirectory;info.UseShellExecute=false;info.CreateNoWindow=true;
     var child=System.Diagnostics.Process.Start(info);
     if(child==null) throw new IOException("Offline save helper did not start");
     child.Dispose();visible=false;Application.Quit();return;
    }
    if(!Fresh() || nonce.Length!=32) { message="Connect before requesting a restart.";return; }
    if(Command(action)) { visible=false;Application.Quit(); }
   } catch(Exception ex) { message="Restart request failed ("+ex.GetType().Name+"). No in-process save switch was made."; }
  }
  private void OnGUI() {
   if(!visible || NgGame.NgSceneManager.GetLoadedScene()!="Menu") return;
   if(warningStyle==null) { warningStyle=new GUIStyle(GUI.skin.label);warningStyle.wordWrap=true;warningStyle.richText=false;warningStyle.fontSize=16; }
   int old=GUI.depth;Matrix4x4 matrix=GUI.matrix;
   try { GUI.matrix=Matrix4x4.Scale(new Vector3(guiScale,guiScale,1f));GUI.depth=-1000;window=GUI.Window(215918,window,Draw,"Archipelago connection — F2 to close"); }
   finally { GUI.depth=old;GUI.matrix=matrix; }
  }
  private void Draw(int id) {
   panelScroll=GUILayout.BeginScrollView(panelScroll);
   GUILayout.Label("BallisticNG Archipelago v0.36.1 • F3: Chat, Hints & Log");
   GUILayout.Label(profile==null ? "Save unavailable" : "Loaded: "+profile.Label);
   if(LoggerBehaviour.Instance!=null) GUILayout.Label(LoggerBehaviour.Instance.MedalSummary);
   GUILayout.Label("Server (hostname, ws:// or wss://; port goes below)");server=GUILayout.TextField(server,200);
   GUILayout.BeginHorizontal();GUILayout.Label("Port",GUILayout.Width(50));port=GUILayout.TextField(port,5,GUILayout.Width(90));GUILayout.Label("Slot",GUILayout.Width(40));slot=GUILayout.TextField(slot,100);GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();GUILayout.Label("Password",GUILayout.Width(80));password=GUILayout.PasswordField(password,'*',200);GUILayout.EndHorizontal();
   GUILayout.BeginHorizontal();
   bool canConnect=GUI.enabled;GUI.enabled=canConnect && connectionState!="setup";
   if(GUILayout.Button("Connect")) Connect();
   GUI.enabled=canConnect;
   if(GUILayout.Button("Disconnect")) { try { Command("disconnect"); } catch { message="Disconnect command could not be written."; } }
   if(GUILayout.Button("Save fields")) { try { SaveSettings();message="Server, port and slot saved. Password is never saved."; } catch { message="Settings could not be saved."; } }
   GUILayout.EndHorizontal();
   GUILayout.Label(message,GUILayout.Height(55));
   GUILayout.Label("Your progress is saved separately for each Archipelago seed. Switch saves using the restart buttons below.",warningStyle);
   if(connectionState=="connected" && TrapLab.Instance!=null && TrapLab.Instance.PendingCount>0) GUILayout.Label("Traps queued: "+TrapLab.Instance.PendingCount+". Effects wait for racing.");
   GUILayout.BeginVertical(GUI.skin.box);
   if(connectionState=="connected") GUILayout.Label(profile!=null && profile.BootKey==connectedKey ? "Correct AP save active. Ready for campaign play." : "Different save loaded. Restart into the connected AP save before AP play.",warningStyle);
   else GUILayout.Label(profile!=null && profile.BootKey=="normal" ? "Normal save active. Connect to a seed to start Archipelago play." : "AP save active. Connect to resume Archipelago checks, or restart into your normal save.",warningStyle);
   GUILayout.EndVertical();
   bool before=GUI.enabled;
   GUI.enabled=before && Fresh() && connectionState=="connected" && (profile==null || profile.BootKey!=connectedKey);
   if(GUILayout.Button("Restart into connected AP save")) Restart("ap");
   GUI.enabled=before && profile!=null && profile.BootKey!="normal";
   if(GUILayout.Button("Restart into normal save")) Restart("normal");
   GUI.enabled=before;
   GUILayout.Label("Restart closes the game, waits for exit, selects a separate save folder, then relaunches. Normal save files are not moved. Password stays only in the running client; enter it again after disconnect.");
   if(connectionState=="recovery") {
    GUILayout.Label("Server history recovery (backup created):");
    recoveryScroll=GUILayout.BeginScrollView(recoveryScroll,GUILayout.Height(105));
    GUILayout.Label(recoveryText);GUILayout.EndScrollView();
    confirmRecovery=GUILayout.Toggle(confirmRecovery,"I reviewed the changes; use the server history.");
    bool enabled=GUI.enabled;GUI.enabled=enabled && Fresh() && recoveryId.Length==64 && confirmRecovery;
    if(GUILayout.Button("Resync with server")) {
     try { if(Command("resync:"+recoveryId)) { confirmRecovery=false;message="Rechecking history before resync..."; } }
     catch { message="Recovery command could not be written; no recovery was requested."; }
    }
    GUI.enabled=enabled;
   } else {
    GUILayout.Label("Recent bridge activity:");GUILayout.TextArea(recent,GUILayout.Height(115));
   }
   GUILayout.EndScrollView();
   GUI.DragWindow(new Rect(0,0,680,22));
  }
 }
}
