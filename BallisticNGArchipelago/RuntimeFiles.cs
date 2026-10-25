using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
namespace BallisticNGArchipelago {
 internal static class RuntimeFiles {
  private static readonly HashSet<string> created=new HashSet<string>();
  internal static string File(string root,string name) {
   string folder=null;
   switch(name) {
    case "active-session.txt": folder="Data";break;
    case "bridge-heartbeat.txt": folder="Data";break;
    case "comms-command.tmp": folder="Data";break;
    case "comms-command.txt": folder="Data";break;
    case "comms-state.txt": folder="Data";break;
    case "panel-command.tmp": folder="Data";break;
    case "panel-command.txt": folder="Data";break;
    case "panel-recovery.txt": folder="Data";break;
    case "panel-settings.txt": folder="Data";break;
    case "panel-status.txt": folder="Data";break;
    case "profile-request.json": folder="Data";break;
    case "events.tsv": folder="Logs";break;
    case "offline-normal.log": folder="Logs";break;
    case "panel-bridge.log": folder="Logs";break;
    case "profile-recovery.txt": folder="Logs";break;
    case "save-isolation.txt": folder="Logs";break;
    case "setup-panel.log": folder="Logs";break;
    case "trap-test.log": folder="Logs";break;
    case "appearance-catalog-ready.txt": folder="Cache";break;
    case "appearance-catalog.tsv": folder="Cache";break;
    case "appearance-variants.tsv": folder="Cache";break;
    case "event-ship-requirements.tsv": folder="Cache";break;
    case "ship-catalog-ready.txt": folder="Cache";break;
    case "ship-catalog.tsv": folder="Cache";break;
   }
   if(folder==null && Regex.IsMatch(name,@"^(access|activity|checks|rules|profile|toasts|traps|trap-acks)-[0-9a-f]{32}\.(txt|tmp)$")) folder="Data";
   if(folder==null) return Path.Combine(root,name);
   string parent=Path.Combine(root,folder);
   lock(created) { if(!created.Contains(parent)) {
    for(string current=Path.GetFullPath(parent);!string.IsNullOrEmpty(current);current=Path.GetDirectoryName(current)) {
     if((System.IO.File.Exists(current)||Directory.Exists(current)) && (System.IO.File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException("Linked runtime folders unsupported");
    }
    Directory.CreateDirectory(parent);
    if((System.IO.File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0) throw new IOException("Linked runtime folders unsupported");
    created.Add(parent);
   } }
   return Path.Combine(parent,name);
  }
  internal static void Rotate(string path) {
   if(!System.IO.File.Exists(path) || new FileInfo(path).Length<2097152) return;
   if(System.IO.File.Exists(path+".3")) System.IO.File.Delete(path+".3");
   for(int i=2;i>=1;i--) if(System.IO.File.Exists(path+"."+i)) System.IO.File.Move(path+"."+i,path+"."+(i+1));
   System.IO.File.Move(path,path+".1");
  }
 }
}
