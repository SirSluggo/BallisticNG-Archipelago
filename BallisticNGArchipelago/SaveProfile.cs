using System;
using System.IO;

namespace BallisticNGArchipelago {
 // Select a fixed isolated root before campaign load. Never swap normal save files.
 public sealed class SaveProfile : IDisposable {
  private FileStream guard;
  private string root, save, isolatedRoot;
  public string BootKey { get; private set; }
  public string Label { get { return BootKey=="normal" ? "NORMAL SAVE" : "AP SAVE "+BootKey.Substring(0,8); } }

  public static bool IsKey(string key) {
   if(key==null || key.Length!=64) return false;
   foreach(char c in key) if(!((c>='0' && c<='9') || (c>='a' && c<='f'))) return false;
   return true;
  }
  private static void Plain(string path) {
   if((File.Exists(path) || Directory.Exists(path)) &&
    (File.GetAttributes(path) & FileAttributes.ReparsePoint)!=0)
    throw new IOException("Linked save/profile paths are unsupported: "+path);
  }
  private static string Read(string path) {
   Plain(path);
   using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete))
   using(StreamReader reader=new StreamReader(stream)) return reader.ReadToEnd().Trim();
  }
  public void Open(string modDirectory) {
   string user=Path.GetFullPath(Path.Combine(Environment.CurrentDirectory,"User"));
   root=Path.Combine(user,"ArchipelagoProfiles");save=Path.Combine(user,"Save");
   Plain(user);Plain(root);Plain(save);
   Directory.CreateDirectory(root);
   string gate=Path.Combine(root,"run.lock");Plain(gate);
   // Denies another writer, including the Python profile switcher, while the mod lives.
   guard=new FileStream(gate,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);
   try {
    if(ReadCurrent()!="normal") throw new IOException("Legacy AP save is still active. Close game and use the old Save-Profiles.cmd -> 2 to return to normal first. No automatic migration performed.");
    isolatedRoot=Path.Combine(user,"ArchipelagoSaves");Plain(isolatedRoot);
    BootKey=ReadSelection();
    SaveRouting.Open(BootKey,modDirectory);
   }
   catch { Dispose();throw; }
  }
  private string ReadCurrent() {
   string journal=Path.Combine(root,"switch-journal.json");Plain(journal);
   if(File.Exists(journal)) throw new IOException("Interrupted save switch. Close game and run Save-Profiles.cmd -> Recover.");
   Plain(save);
   if(!Directory.Exists(save)) throw new IOException("Active save directory is missing. Close game and inspect the profile log.");
   string state=Path.Combine(root,"active-profile.txt");Plain(state);
   string marker=Path.Combine(save,".bng-ap-profile");Plain(marker);
   if(!File.Exists(state)) {
    if(File.Exists(marker) || Directory.Exists(Path.Combine(root,"normal/Save")))
     throw new IOException("Save profile state is missing; cannot safely identify this save.");
    return "normal";
   }
   string key=Read(state);
   if(key=="normal") {
    if(File.Exists(marker)) throw new IOException("Normal save contains an AP profile marker.");
    return key;
   }
   if(!IsKey(key) || !File.Exists(marker) || Read(marker)!=key)
    throw new IOException("AP save marker does not match the selected profile.");
   return key;
  }
  private string ReadSelection() {
   string path=Path.Combine(isolatedRoot,"selected-profile.txt");Plain(path);
   if(!File.Exists(path)) {
    string slots=Path.Combine(isolatedRoot,"slots");Plain(slots);
    if(Directory.Exists(slots) && Directory.GetDirectories(slots).Length>0) throw new IOException("Isolated profile selection is missing; restore it before launch");
    return "normal";
   }
   string key=Read(path);
   if(key!="normal" && !IsKey(key)) throw new IOException("Invalid isolated profile selection; no profile fallback allowed");
   return key;
  }
  public bool Matches(string key) {
   return guard!=null && IsKey(key) && BootKey==key && ReadCurrent()=="normal" && ReadSelection()==key;
  }
  public string ConsumableFile() {
   if(!IsKey(BootKey)) throw new IOException("Consumables require an isolated AP save");
   string slots=Path.Combine(isolatedRoot,"slots"),slot=Path.Combine(slots,BootKey);
   Plain(isolatedRoot);Plain(slots);Plain(slot);
   if(!Directory.Exists(slot)) throw new IOException("AP slot directory is missing");
   string file=Path.Combine(slot,"consumables-v1.txt");Plain(file);Plain(file+".tmp");Plain(file+".bak");return file;
  }
  public void Dispose() { if(guard!=null) { guard.Dispose();guard=null; } }
 }
}
