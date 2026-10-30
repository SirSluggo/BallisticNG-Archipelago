using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;

namespace BallisticNGArchipelago {
 // Native-only local rewards. The profile key is immutable for the process lifetime.
 public sealed class ConsumableLedger {
  private readonly string path,key;
  private int delivered,assistSpent,passSpent;
  private HashSet<string> rewards=new HashSet<string>();
  private string tainted="";
  public int Assists { get { return delivered-assistSpent; } }
  public int Passes { get { return rewards.Count-passSpent; } }
  public bool Tainted(string eventKey) { return ("|"+tainted+"|").Contains("|"+eventKey+"|"); }
  private static string Hash(string value) {
   using(SHA256 h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();
  }
  public ConsumableLedger(string file,string profileKey) {
   if(!SaveProfile.IsKey(profileKey)) throw new IOException("Invalid consumable profile key");
   path=file;key=profileKey;
   if(!File.Exists(path)) { Commit(0,0,0,rewards,"");return; }
   string text=File.ReadAllText(path).Replace("\r","");
   string[] lines=text.TrimEnd('\n').Split('\n');
   if(lines.Length<8) throw new IOException("Consumable ledger is incomplete");
   string body=string.Join("\n",lines,0,lines.Length-1)+"\n";
   if(lines[0]!="BNG-CONSUMABLES-1" || lines[1]!=key || Hash(body)!=lines[lines.Length-1] ||
    !int.TryParse(lines[2],out delivered) || !int.TryParse(lines[3],out assistSpent) || !int.TryParse(lines[4],out passSpent) ||
    delivered<0 || delivered>1000000 || assistSpent<0 || assistSpent>delivered || passSpent<0) throw new IOException("Consumable ledger validation failed");
   tainted=lines[5];
   int count;
   if(!int.TryParse(lines[6],out count) || count<0 || count>101 || lines.Length!=8+count) throw new IOException("Consumable reward list is invalid");
   for(int i=0;i<count;i++) if(!EventKey(lines[7+i]) || !rewards.Add(lines[7+i])) throw new IOException("Consumable reward key is invalid");
   HashSet<string> marked=new HashSet<string>();
   if(tainted!="")foreach(string item in tainted.Split('|'))if(!EventKey(item) || !marked.Add(item))throw new IOException("Consumable attempt marks are invalid");
   if(passSpent>rewards.Count) throw new IOException("Consumable counts are invalid");
  }
  private static bool EventKey(string value) {
   foreach(EventSpec e in Catalog.Events) if(e.Key==value) return true;
   return false;
  }
  private void Commit(int total,int spent,int used,HashSet<string> earned,string marked) {
   List<string> ordered=new List<string>(earned);ordered.Sort(StringComparer.Ordinal);
   string body=string.Join("\n",new string[] {"BNG-CONSUMABLES-1",key,total.ToString(),spent.ToString(),used.ToString(),marked,ordered.Count.ToString()})+"\n";
   if(ordered.Count>0) body+=string.Join("\n",ordered.ToArray())+"\n";
   string pending=path+".tmp";
   using(FileStream stream=new FileStream(pending,FileMode.Create,FileAccess.Write,FileShare.None)) {
    byte[] bytes=Encoding.UTF8.GetBytes(body+Hash(body)+"\n");stream.Write(bytes,0,bytes.Length);stream.Flush(true);
   }
   if(File.Exists(path)) File.Replace(pending,path,path+".bak");else File.Move(pending,path);
   if(File.ReadAllText(path)!=body+Hash(body)+"\n") throw new IOException("Consumable ledger readback failed");
   delivered=total;assistSpent=spent;passSpent=used;rewards=new HashSet<string>(earned);tainted=marked;
  }
  public void SetDelivered(int total) {
   if(total<delivered || total>1000000) throw new IOException("Assist history decreased; preserve the consumable ledger for diagnosis");
   if(total!=delivered) Commit(total,assistSpent,passSpent,rewards,tainted);
  }
  public bool Reward(string eventKey) {
   if(!EventKey(eventKey) || rewards.Contains(eventKey) || Tainted(eventKey)) return false;
   HashSet<string> candidate=new HashSet<string>(rewards);candidate.Add(eventKey);
   Commit(delivered,assistSpent,passSpent,candidate,tainted);return true;
  }
  public bool SpendAssist() {
   if(Assists<=0)return false;
   Commit(delivered,assistSpent+1,passSpent,rewards,tainted);return true;
  }
  public bool SpendPass(string eventKey) {
   if(Passes<=0 || !EventKey(eventKey))return false;
   List<string> marked=new List<string>(tainted=="" ? new string[0]:tainted.Split('|'));
   if(!marked.Contains(eventKey))marked.Add(eventKey);marked.Sort(StringComparer.Ordinal);
   Commit(delivered,assistSpent,passSpent+1,rewards,string.Join("|",marked.ToArray()));return true;
  }
  public void NewAttempt(string eventKey,bool firstRound) {
   if(firstRound && Tainted(eventKey)) {
    List<string> marked=new List<string>(tainted.Split('|'));marked.Remove(eventKey);
    Commit(delivered,assistSpent,passSpent,rewards,string.Join("|",marked.ToArray()));
   }
  }
 }
}
