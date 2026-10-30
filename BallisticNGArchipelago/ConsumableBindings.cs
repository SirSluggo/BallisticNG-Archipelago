using System;
using System.IO;
using Rewired;
using UnityEngine;
using NgIo;

namespace BallisticNGArchipelago {
 public sealed class ConsumableBindings {
  private sealed class Binding { public KeyCode Key=KeyCode.None;public string Hardware="",Label="Unbound";public int Element=-1; }
  private readonly Binding[] bindings={new Binding()};
  private string path,message="Click Change, then press the key or controller button you want to use.";
  private int capture=-1;
  private bool keyboard;
  public void Open(string directory) {
   path=RuntimeFiles.File(directory,"consumable-bindings.txt");
   if(!File.Exists(path)) return;
   string[] lines=File.ReadAllLines(path);
   if(lines.Length<1 || lines.Length>2) { message="Binding file invalid; bindings left unbound.";return; }
   for(int i=0;i<1;i++) {
    string[] row=lines[i].Split('\t');int key,element;
    if(row.Length!=4 || !int.TryParse(row[0],out key) || !Enum.IsDefined(typeof(KeyCode),key) || !int.TryParse(row[2],out element)) continue;
    bindings[i].Key=(KeyCode)key;bindings[i].Hardware=row[1];bindings[i].Element=element;bindings[i].Label=row[3];
   }
  }
  private void Save() {
   string[] lines=new string[1];
   for(int i=0;i<1;i++) { Binding b=bindings[i];lines[i]=((int)b.Key)+"\t"+b.Hardware+"\t"+b.Element+"\t"+b.Label.Replace('\t',' ').Replace('\n',' ').Replace('\r',' '); }
   string temp=path+".tmp";File.WriteAllLines(temp,lines);
   if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
  }
  private static bool Conflict(Controller controller,int element,KeyCode key) {
   if(NgIn.PlayerOne==null)return true;
   if(key==KeyCode.Escape || key==KeyCode.Return || (key>=KeyCode.F1 && key<=KeyCode.F15))return true;
   foreach(ControllerMap map in NgIn.PlayerOne.controllers.maps.GetAllMaps()) {
    if(map.controllerType!=controller.type || map.controllerId!=controller.id)continue;
    foreach(ActionElementMap action in map.AllMaps)
     if((key!=KeyCode.None && action.keyCode==key) || (key==KeyCode.None && action.elementType==ControllerElementType.Button && action.elementIdentifierId==element))return true;
   }
   return false;
  }
  private bool SetController(int index,Controller controller,int element,string label) {
   if(Conflict(controller,element,KeyCode.None)) { message="That button is assigned in the game. Choose a free button or remove its native binding first.";return false; }
   Binding b=bindings[index];b.Hardware=controller.hardwareTypeGuid.ToString();b.Element=element;b.Label=label;Save();return true;
  }
  public string Label(int index) {
   Binding b=bindings[index];return (b.Key==KeyCode.None ? "Keyboard unbound":b.Key.ToString())+" / "+(b.Element<0 ? "controller unbound":b.Label);
  }
  public int Poll(bool panelVisible) {
   if(NgIn.PlayerOne==null)return -1;
   if(capture>=0 && !panelVisible) { capture=-1;message="Binding cancelled."; }
   if(capture>=0) {
    if(keyboard)return -1; // keyboard capture uses IMGUI key events in the F4 settings.
    foreach(Controller controller in NgIn.PlayerOne.controllers.Controllers) {
     if(controller.type!=ControllerType.Joystick)continue;
     foreach(Controller.Button button in controller.Buttons) if(controller.GetButtonDownById(button.id)) {
      int selected=capture;capture=-1;
      if(SetController(selected,controller,button.id,button.name))message="Controller binding saved.";
      return -1;
     }
    }
    return -1;
   }
   if(panelVisible)return -1;
   int chosen=-1;
   for(int i=0;i<1;i++) {
    Binding b=bindings[i];
    bool down=false;
    if(b.Key!=KeyCode.None && Input.GetKeyDown(b.Key)) {
     if(Conflict(NgIn.PlayerOne.controllers.Keyboard,-1,b.Key))message="Keyboard binding conflicts with the game; consumable not used.";
     else down=true;
    }
    if(b.Element>=0) foreach(Controller controller in NgIn.PlayerOne.controllers.Controllers) {
     if(controller.type!=ControllerType.Joystick || controller.hardwareTypeGuid.ToString()!=b.Hardware || !controller.GetButtonDownById(b.Element))continue;
     if(Conflict(controller,b.Element,KeyCode.None))message="Controller binding conflicts with the game; consumable not used.";
     else down=true;
    }
    if(down) { if(chosen>=0) { message="Consumable bindings overlap. Assign different controls.";return -1; }chosen=i; }
   }
   return chosen;
  }
  public void DrawSettings() {
   GUIStyle label=new GUIStyle(GUI.skin.label);label.fontSize=16;label.wordWrap=true;
   GUILayout.Label("Autopilot Assist",label);
   GUILayout.Space(10);
   Binding b=bindings[0];
   GUILayout.BeginHorizontal();GUILayout.Label("Keyboard",GUILayout.Width(100));
   GUILayout.Label(b.Key==KeyCode.None ? "Not set":b.Key.ToString(),GUILayout.Width(190));
   if(GUILayout.Button("Change")) { capture=0;keyboard=true;message="Press a keyboard key. Escape cancels."; }
   if(GUILayout.Button("Clear",GUILayout.Width(70))) { b.Key=KeyCode.None;capture=-1;Save(); }
   GUILayout.EndHorizontal();GUILayout.Space(10);
   GUILayout.BeginHorizontal();GUILayout.Label("Controller",GUILayout.Width(100));
   GUILayout.Label(b.Element<0 ? "Not set":b.Label,GUILayout.Width(190));
   if(GUILayout.Button("Change")) { capture=0;keyboard=false;message="Press a controller button."; }
   if(GUILayout.Button("Clear",GUILayout.Width(70))) { b.Hardware="";b.Element=-1;b.Label="Unbound";capture=-1;Save(); }
   GUILayout.EndHorizontal();GUILayout.Space(10);
   GUILayout.Space(10);GUILayout.Label(message,label);
   if(capture>=0 && keyboard && Event.current.type==EventType.KeyDown) {
    KeyCode key=Event.current.keyCode;
    if(key==KeyCode.Escape) { capture=-1;message="Binding cancelled."; }
    else if(key!=KeyCode.None && NgIn.PlayerOne!=null) {
     if(Conflict(NgIn.PlayerOne.controllers.Keyboard,-1,key))message="That key is already used. Choose another key or remove its game binding.";
     else { bindings[capture].Key=key;capture=-1;Save();message="Keyboard control saved."; }
    }
    Event.current.Use();
   }
  }
 }
}
