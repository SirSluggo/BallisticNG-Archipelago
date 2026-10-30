using UnityEngine;
using NgGame;
using NgIo;
namespace BallisticNGArchipelago {
 [DefaultExecutionOrder(-9600)]
 public sealed class AssistSettingsPanel : MonoBehaviour {
  public bool Visible { get;private set; }
  private bool heldNavigation,priorNavigation,priorModule;
  private NgMenuInput inputModule;
  private Rect window;
  private float scale=1f;
  private GUIStyle style;
  private static bool Allowed() { return NgSceneManager.GetLoadedScene()=="Menu" || (NgGameState.IsPaused && Time.timeScale==0f); }
  public void Close() {
   Visible=false;
   if(!heldNavigation)return;
   NgMenuInput.OverlayCount--;if(!priorNavigation)NgMenuInput.EnableNavigation();
   if(inputModule!=null)inputModule.enabled=priorModule;
   inputModule=null;heldNavigation=false;
  }
  private void Open() {
   if(!Allowed())return;
   if(ChatPanel.Instance!=null)ChatPanel.Instance.Close();
   ConnectionPanel connection=GetComponent<ConnectionPanel>();if(connection!=null)connection.Hide();
   Visible=true;priorNavigation=NgMenuInput.NavigationDisabled;NgMenuInput.DisableNavigation();NgMenuInput.OverlayCount++;heldNavigation=true;
   inputModule=NgMenuInput.Instance;if(inputModule!=null) { priorModule=inputModule.enabled;inputModule.enabled=false; }
   scale=Mathf.Clamp(Screen.height/1080f,.75f,2f);
   float width=Mathf.Min(620f,Screen.width/scale-30),height=Mathf.Min(310f,Screen.height/scale-40);
   window=new Rect((Screen.width/scale-width)/2,(Screen.height/scale-height)/2,width,height);
  }
  private void Update() {
   if(Input.GetKeyDown(KeyCode.F4)) { if(Visible)Close();else Open(); }
   if(Visible && !Allowed())Close();
   if(Visible) { InputStatus.TempInputBlock=true;NgMenuInput.DisableNavigation(); }
  }
  private void OnDisable() { Close(); }
  private void OnDestroy() { Close(); }
  private void OnGUI() {
   if(!Visible || !Allowed())return;
   if(style==null)style=PanelStyles.OpaqueWindow();
   Matrix4x4 matrix=GUI.matrix;int depth=GUI.depth;Color color=GUI.color,background=GUI.backgroundColor,content=GUI.contentColor;
   try {
    GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.depth=-1100;GUI.color=GUI.backgroundColor=GUI.contentColor=Color.white;
    window=GUI.Window(215920,window,Draw,"Autopilot Assist controls — F4 to close",style);
   } finally { GUI.matrix=matrix;GUI.depth=depth;GUI.color=color;GUI.backgroundColor=background;GUI.contentColor=content; }
  }
  private void Draw(int id) {
   if(LoggerBehaviour.Instance!=null)LoggerBehaviour.Instance.DrawConsumableSettings();
   GUILayout.FlexibleSpace();if(GUILayout.Button("Done",GUILayout.Height(32)))Close();
   GUI.DragWindow(new Rect(0,0,window.width,25));
  }
 }
}
