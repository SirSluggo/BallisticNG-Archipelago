using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using NgAudio;

namespace BallisticNGArchipelago {
 public sealed class ItemToasts {
  private sealed class Toast { public string Name,Kind,Peer;public bool Progression,Trap;public float Started; }
  private sealed class Receipt { public long Sequence,Timestamp;public int Flags;public string Name,Kind,Peer; }
  private readonly List<Toast> visible=new List<Toast>();
  private string session;
  private bool initialized;
  private long seen;
  private float nextSoundAt;
  private AudioSource notificationSound;
  private GUIStyle headingStyle,nameStyle,peerStyle;
  public void Reset() { visible.Clear();session=null;seen=0;initialized=false; }
  public void Refresh(string directory,string token) {
   visible.RemoveAll(delegate(Toast toast) { return Time.realtimeSinceStartup-toast.Started>=6f; });
   if(session!=token) { Reset();session=token; }
   string path=RuntimeFiles.File(directory,"toasts-"+token+".txt");
   if(!File.Exists(path)) return;
   string text;
   using(FileStream file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite | FileShare.Delete)) {
    if(file.Length>65536) return;
    using(StreamReader reader=new StreamReader(file)) text=reader.ReadToEnd();
   }
   string[] lines=text.Replace("\r","").Split('\n');
   if(lines.Length<2 || lines[0]!=token || lines[1]!=Catalog.Digest) return;
   List<Receipt> next=new List<Receipt>();long largest=seen;
   foreach(string line in lines) {
    if(line=="" || line==token || line==Catalog.Digest) continue;
    string[] fields=line.Split('\t');long sequence,timestamp;int flags;
    if(fields.Length!=6 || !long.TryParse(fields[0],out sequence) || sequence<1 ||
       !int.TryParse(fields[1],out flags) || flags<0 || !long.TryParse(fields[2],out timestamp)) return;
    if(fields[4]!="SENT" && fields[4]!="RECEIVED" && fields[4]!="ACQUIRED") return;
    string name,peer;
    try { name=Encoding.UTF8.GetString(Convert.FromBase64String(fields[3]));peer=Encoding.UTF8.GetString(Convert.FromBase64String(fields[5])); } catch(FormatException) { return; }
    if(name.Length==0 || name.Length>512 || peer.Length>512) return;
    next.Add(new Receipt { Sequence=sequence,Timestamp=timestamp,Flags=flags,Name=name,Kind=fields[4],Peer=peer });
    largest=Math.Max(largest,sequence);
   }
   // A newly opened game does not replay notifications accumulated before it started reading.
   if(!initialized) { seen=largest;initialized=true;return; }
   long now=(long)(DateTime.UtcNow-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds;
   next.Sort(delegate(Receipt a,Receipt b) { return a.Sequence.CompareTo(b.Sequence); });
   int soundKind=0;
   foreach(Receipt receipt in next) {
    if(receipt.Sequence<=seen) continue;
    seen=receipt.Sequence;
    if(now-receipt.Timestamp>15000 || now-receipt.Timestamp< -5000) continue;
    visible.Add(new Toast { Name=receipt.Name,Kind=receipt.Kind,Peer=receipt.Peer,Progression=(receipt.Flags & 1)!=0,Trap=(receipt.Flags & 4)!=0,Started=Time.realtimeSinceStartup });
    while(visible.Count>3) visible.RemoveAt(0);
    int candidate=receipt.Kind=="SENT" ? 2 : ((receipt.Flags & 4)!=0 ? 0:((receipt.Flags & 1)!=0 ? 3:1));
    soundKind=Math.Max(soundKind,candidate);
   }
   PlayNotification(soundKind);
  }
  public void LocalReward(string name,string reason) {
   visible.Add(new Toast { Name=name,Kind="EARNED",Peer=reason,Started=Time.realtimeSinceStartup });
   while(visible.Count>3)visible.RemoveAt(0);
   PlayNotification(3);
  }
  private void PlayNotification(int kind) {
   if(kind==0 || Time.realtimeSinceStartup<nextSoundAt) return;
   // One cue per batch; no overlap and no delayed queue of old receipt sounds.
   if(notificationSound!=null && notificationSound.isPlaying) return;
   nextSoundAt=Time.realtimeSinceStartup+0.75f;
   try {
    string[] cue=kind==3 ? NgSound.UI_AchievementUnlocked : (kind==2 ? NgSound.UI_MpCountdown:NgSound.UI_Move);
    notificationSound=ApAudio.Play(cue,EAudioChannel.Interface);
   } catch(Exception ex) { Debug.LogWarning("Archipelago notification audio: "+ex.Message); }
  }
  public void Draw() {
   if(visible.Count==0) return;
   if(headingStyle==null) {
    headingStyle=new GUIStyle(GUI.skin.label);headingStyle.fontSize=11;headingStyle.fontStyle=FontStyle.Bold;headingStyle.richText=false;
    nameStyle=new GUIStyle(GUI.skin.label);nameStyle.fontSize=14;nameStyle.wordWrap=true;nameStyle.richText=false;
    peerStyle=new GUIStyle(GUI.skin.label);peerStyle.fontSize=11;peerStyle.richText=false;peerStyle.normal.textColor=new Color(0.72f,0.77f,0.83f,1f);
   }
   Color originalColor=GUI.color;int originalDepth=GUI.depth;
   try {
    GUI.depth=-50;
    // Baseline is 1080p. At 4K, cards and text are twice as large in physical pixels.
    float scale=Mathf.Clamp(Screen.height/1080f,1f,3f);
    headingStyle.fontSize=Mathf.RoundToInt(11f*scale);
    nameStyle.fontSize=Mathf.RoundToInt(14f*scale);
    peerStyle.fontSize=Mathf.RoundToInt(11f*scale);
    float width=Mathf.Min(360f*scale,Screen.width-32f*scale),height=78f*scale;
    int position=0;
    for(int i=visible.Count-1;i>=0;i--) {
     Toast toast=visible[i];float age=Time.realtimeSinceStartup-toast.Started;
     if(age>=6f) continue;
     float alpha=Mathf.Clamp01(age/0.15f)*Mathf.Clamp01((6f-age)/0.5f);
     float x=Screen.width-width-18f*scale,y=Screen.height-30f*scale-height-position*(height+7f*scale);position++;
     Color accent=toast.Trap ? new Color(0.96f,0.38f,0.45f,alpha) : (toast.Progression || toast.Kind=="EARNED") ? new Color(0.96f,0.77f,0.38f,alpha):new Color(0.62f,0.69f,0.78f,alpha);
     GUI.color=new Color(0.035f,0.045f,0.065f,0.85f*alpha);GUI.DrawTexture(new Rect(x,y,width,height),Texture2D.whiteTexture);
     GUI.color=accent;GUI.DrawTexture(new Rect(x,y,3f*scale,height),Texture2D.whiteTexture);
     GUI.color=new Color(1f,1f,1f,alpha);headingStyle.normal.textColor=accent;
     GUI.Label(new Rect(x+12f*scale,y+5f*scale,width-22f*scale,17f*scale),toast.Kind,headingStyle);
     nameStyle.normal.textColor=Color.white;
     GUI.Label(new Rect(x+12f*scale,y+23f*scale,width-22f*scale,36f*scale),toast.Name,nameStyle);
     string peerText=toast.Kind=="SENT" ? "To "+toast.Peer : (toast.Kind=="RECEIVED" ? "From "+toast.Peer:(toast.Kind=="EARNED" ? toast.Peer:"Your check"));
     GUI.Label(new Rect(x+12f*scale,y+59f*scale,width-22f*scale,16f*scale),peerText,peerStyle);
    }
   } finally { GUI.color=originalColor;GUI.depth=originalDepth; }
  }
 }
}
