using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using NgGame;
using NgIo;

namespace BallisticNGArchipelago {
 [DefaultExecutionOrder(-9500)]
 public sealed class ChatPanel : MonoBehaviour {
  public static ChatPanel Instance;
  private string directory,client="",token="",slot="",input="",search="",notice="",pendingHint="";
  private bool visible,valid,serverReady,heldNavigation,priorNavigation;
  private NgMenuInput inputModule;private bool priorModule;
  private DateTime lastWrite=DateTime.MinValue;
  private float nextPoll,nextSendAt;private int tab,checks,total,medal,finales,points=-1,cost=-1;
  private Rect window;private Vector2 logScroll,hintScroll,itemScroll;
  private bool followLog=true,progressionOnly=true;
  private sealed class Row { public string Kind,Text;public bool Progression; }
  private List<Row> logs=new List<Row>(),hints=new List<Row>(),items=new List<Row>();
  private GUIStyle wrap,rowStyle,titleStyle;private float scale=1f;
  public bool Visible { get { return visible; } }
  public string Slot { get { return slot; } }
  public string Progress { get { return "Checks "+checks+"/"+total+"  •  "+(medal==1 ? "Bronze":medal==2 ? "Silver":"Gold")+" Required  •  Finales "+finales+"/3"; } }
  private bool live;
  public bool Ready { get { return valid && serverReady && live; } }
  private void CheckLive() {
   live=false;
   if(!valid || !serverReady || token.Length!=32) return;
   try {
    string heartbeat=RuntimeFiles.File(directory,"bridge-heartbeat.txt"),active=RuntimeFiles.File(directory,"active-session.txt");
    live=File.Exists(heartbeat) && (DateTime.UtcNow-File.GetLastWriteTimeUtc(heartbeat)).TotalSeconds<15 && File.Exists(active) && Read(active).Trim()==token;
   } catch { live=false; }
  }
  public void Initialize(string path) { Instance=this;directory=path; }
  private static bool Allowed() { return NgSceneManager.GetLoadedScene()=="Menu" || (NgGameState.IsPaused && Time.timeScale==0f); }
  private static string Read(string path) {
   using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete))
   using(StreamReader reader=new StreamReader(stream)) return reader.ReadToEnd();
  }
  private static string Decode(string text) { return Encoding.UTF8.GetString(Convert.FromBase64String(text)); }
  private static bool Hex(string s) { if(s.Length!=32) return false;foreach(char c in s) if(!((c>='0' && c<='9') || (c>='a' && c<='f'))) return false;return true; }
  private static int Number(string text,int low,int high) { int n;if(!int.TryParse(text,out n) || n<low || n>high) throw new IOException("Invalid display data");return n; }
  private void Poll() {
   try {
    string path=RuntimeFiles.File(directory,"comms-state.txt");if(!File.Exists(path)) { valid=false;return; }
    DateTime stamp=File.GetLastWriteTimeUtc(path);if(valid && stamp==lastWrite) return;
    if(new FileInfo(path).Length>1048576) throw new IOException("Display data too large");
    string[] lines=Read(path).Replace("\r","").TrimEnd('\n').Split('\n');
    if(lines.Length<12 || lines[0]!="BNG-COMMS-1" || lines[1]!=Catalog.Digest || !Hex(lines[2]) || (lines[3]!="-" && !Hex(lines[3]))) throw new IOException("Display data mismatch");
    bool ready=Number(lines[4],0,1)==1;string name=Decode(lines[5]);
    int c=Number(lines[6],0,10000),t=Number(lines[7],0,10000),m=Number(lines[8],1,3),f=Number(lines[9],0,3),p=Number(lines[10],-1,int.MaxValue),co=Number(lines[11],-1,int.MaxValue);
    var newLogs=new List<Row>();var newHints=new List<Row>();var newItems=new List<Row>();
    for(int i=12;i<lines.Length;i++) {
     string[] fields=lines[i].Split('\t');if(fields.Length!=3) continue;
     Row r=new Row();r.Kind=fields[1];r.Text=Decode(fields[2]);r.Progression=fields[1]=="1";
     if(fields[0]=="L" && newLogs.Count<160) newLogs.Add(r);
     if(fields[0]=="H" && newHints.Count<512) newHints.Add(r);
     if(fields[0]=="I" && newItems.Count<512) newItems.Add(r);
    }
    bool newLog=newLogs.Count!=logs.Count || (newLogs.Count>0 && (logs.Count==0 || newLogs[newLogs.Count-1].Text!=logs[logs.Count-1].Text));
    if(client!=lines[2] || token!=lines[3]) { pendingHint="";notice=""; }
    client=lines[2];token=lines[3];slot=name;checks=c;total=t;medal=m;finales=f;points=p;cost=co;
    serverReady=ready;logs=newLogs;hints=newHints;items=newItems;valid=true;lastWrite=stamp;
    if(followLog && newLog) logScroll.y=float.MaxValue;
   } catch { valid=false;notice="Client display data is temporarily unavailable. Reconnect with F2 if this persists."; }
  }
  private void Open() {
   if(!Allowed()) return;
   ConnectionPanel panel=GetComponent<ConnectionPanel>();if(panel!=null) panel.Hide();
   visible=true;pendingHint="";priorNavigation=NgMenuInput.NavigationDisabled;NgMenuInput.DisableNavigation();
   NgMenuInput.OverlayCount++;heldNavigation=true;
   inputModule=NgMenuInput.Instance;if(inputModule!=null) { priorModule=inputModule.enabled;inputModule.enabled=false; }
   scale=Mathf.Clamp(Screen.height/1080f,0.75f,2f);
   float width=Mathf.Min(940f,Screen.width/scale-30f),height=Mathf.Min(840f,Screen.height/scale-45f);
   window=new Rect((Screen.width/scale-width)*.5f,20f,width,height);logScroll.y=float.MaxValue;
  }
  public void Close() {
   visible=false;pendingHint="";
   if(heldNavigation) {
    NgMenuInput.OverlayCount--;if(!priorNavigation) NgMenuInput.EnableNavigation();
    if(inputModule!=null) inputModule.enabled=priorModule;
    heldNavigation=false;inputModule=null;
   }
  }
  private void OnDisable() { Close(); }
  private void OnDestroy() { Close();if(Instance==this) Instance=null; }
  private void Update() {
   if(Input.GetKeyDown(KeyCode.F3)) { if(visible) Close();else Open(); }
   if(visible && !Allowed()) Close();
   if(visible) { InputStatus.TempInputBlock=true;NgMenuInput.DisableNavigation(); }
   if(Time.realtimeSinceStartup>=nextPoll) { nextPoll=Time.realtimeSinceStartup+.5f;if(directory!=null) { Poll();CheckLive(); } }
  }
  private bool Send(string text) {
   if(!Ready) { notice="Disconnected. Connect through F2 before sending.";return false; }
   if(Time.realtimeSinceStartup<nextSendAt) { notice="Please wait one second between requests.";return false; }
   text=text.Trim();if(text.Length==0 || text.Length>500) { notice="Enter 1–500 characters.";return false; }
   foreach(char c in text) if(char.IsControl(c)) { notice="Use a single line of text.";return false; }
   try {
    string path=RuntimeFiles.File(directory,"comms-command.txt");if(File.Exists(path)) { notice="A request is already waiting for the client.";return false; }
    string temp=RuntimeFiles.File(directory,"comms-command.tmp");
    File.WriteAllLines(temp,new string[] {client,token,Guid.NewGuid().ToString("N"),Convert.ToBase64String(Encoding.UTF8.GetBytes(text))});
    File.Move(temp,path);nextSendAt=Time.realtimeSinceStartup+1f;notice="Request queued. Watch the log for the server response.";return true;
   } catch(Exception ex) { notice="Could not queue request ("+ex.GetType().Name+").";return false; }
  }
  private void OnGUI() {
   if(!visible || !Allowed()) return;
   if(wrap==null) {
    wrap=new GUIStyle(GUI.skin.label);wrap.wordWrap=true;wrap.richText=false;wrap.fontSize=16;
    rowStyle=new GUIStyle(wrap);rowStyle.padding=new RectOffset(6,6,4,4);
    titleStyle=new GUIStyle(wrap);titleStyle.fontSize=20;titleStyle.fontStyle=FontStyle.Bold;
   }
   Matrix4x4 oldMatrix=GUI.matrix;int depth=GUI.depth;
   try { GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1f));GUI.depth=-1100;window=GUI.Window(215919,window,Draw,"Chat, Hints & Log — F3 to close"); }
   finally { GUI.matrix=oldMatrix;GUI.depth=depth; }
  }
  private void Draw(int id) {
   GUILayout.BeginHorizontal();GUILayout.Label((Ready ? "Multiworld Linked • ":"Disconnected • ")+slot,titleStyle);if(GUILayout.Button("Close",GUILayout.Width(80))) Close();GUILayout.EndHorizontal();
   GUILayout.Label(valid ? Progress:"Connect in F2 to load your slot and progress.",wrap);
   tab=GUILayout.Toolbar(tab,new string[] {"Chat & Log","Hints"},GUILayout.Height(32));
   if(tab==0) DrawLog();else DrawHints();
   if(notice.Length>0) GUILayout.Label(notice,wrap);
   GUILayout.Label("Chat and commands are sent to the server. Hint costs and permissions are enforced by the server. Typing is available only in menus or while fully paused.",wrap);
   GUI.DragWindow(new Rect(0,0,window.width-80f,22));
  }
  private void DrawLog() {
   followLog=GUILayout.Toggle(followLog,"Follow newest messages");
   logScroll=GUILayout.BeginScrollView(logScroll,GUILayout.Height(Mathf.Max(160f,window.height-330f)));
   foreach(Row row in logs) {
    Color prior=GUI.contentColor;
    GUI.contentColor=row.Kind=="HINT" ? new Color(.45f,.75f,1f):row.Kind=="ITEM" ? new Color(1f,.86f,.45f):row.Kind=="CHECK" ? new Color(.55f,1f,.7f):row.Kind=="SYSTEM" ? new Color(.75f,.8f,.85f):Color.white;
    GUILayout.Label("["+row.Kind+"] "+row.Text,rowStyle);GUI.contentColor=prior;
   }
   GUILayout.EndScrollView();
   GUILayout.BeginHorizontal();GUI.SetNextControlName("APChatInput");input=GUILayout.TextField(input,500,GUILayout.Height(30));
   bool before=GUI.enabled;GUI.enabled=before && Ready;
   if(GUILayout.Button("Send",GUILayout.Width(90),GUILayout.Height(30)) && Send(input)) input="";GUI.enabled=before;GUILayout.EndHorizontal();
   if(Event.current.type==EventType.KeyDown && (Event.current.keyCode==KeyCode.Return || Event.current.keyCode==KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl()=="APChatInput") {
    if(Send(input)) input="";Event.current.Use();
   }
   GUILayout.Label("Examples: !hint  •  !hint Finale A Entry  •  !help",wrap);
  }
  private void DrawHints() {
   GUILayout.Label("Hint points: "+(points<0 ? "waiting for server":points.ToString())+"  •  Cost: "+(cost<0 ? "waiting for server":cost+" points"),wrap);
   bool before=GUI.enabled;GUI.enabled=before && Ready;
   if(GUILayout.Button("Show my hints and current cost (!hint)")) Send("!hint");GUI.enabled=before;
   hintScroll=GUILayout.BeginScrollView(hintScroll,GUILayout.Height(135));
   if(hints.Count==0) GUILayout.Label("No hints received yet. Request an item below or use !hint in Chat & Log.",wrap);
   foreach(Row row in hints) GUILayout.Label(row.Text,rowStyle);
   GUILayout.EndScrollView();
   GUILayout.BeginHorizontal();GUILayout.Label("Find an item",GUILayout.Width(100));search=GUILayout.TextField(search,100);progressionOnly=GUILayout.Toggle(progressionOnly,"Progression only",GUILayout.Width(150));GUILayout.EndHorizontal();
   itemScroll=GUILayout.BeginScrollView(itemScroll,GUILayout.Height(Mathf.Max(80f,window.height-560f)));
   foreach(Row row in items) {
    if(progressionOnly && !row.Progression) continue;
    if(search.Length>0 && row.Text.IndexOf(search,StringComparison.OrdinalIgnoreCase)<0) continue;
    GUILayout.BeginHorizontal();GUILayout.Label(row.Text,wrap);GUI.enabled=before && Ready;
    if(GUILayout.Button("Request hint",GUILayout.Width(120))) pendingHint=row.Text;
    GUI.enabled=before;GUILayout.EndHorizontal();
   }
   GUILayout.EndScrollView();
   if(pendingHint.Length>0) {
    GUILayout.Label("Request a hint for "+pendingHint+"? Server may charge "+(cost<0 ? "hint points":cost+" points per new hint")+". The server response is final.",wrap);
    GUILayout.BeginHorizontal();GUI.enabled=before && Ready;
    if(GUILayout.Button("Confirm hint request")) { if(Send("!hint "+pendingHint)) { pendingHint="";tab=0;logScroll.y=float.MaxValue; } }
    GUI.enabled=before;if(GUILayout.Button("Cancel")) pendingHint="";GUILayout.EndHorizontal();
   }
  }
 }
}
