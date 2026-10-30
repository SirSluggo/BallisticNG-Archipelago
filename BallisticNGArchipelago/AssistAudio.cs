using System;
using NgAudio;
using NgGame;
using UnityEngine;
namespace BallisticNGArchipelago {
 public sealed class AssistAudio {
  private GameObject host;
  private AudioSource loop;
  public void Start() {
   Stop(false);
   try {
    NgSound.PlayVoice(NgSound.Voice_Engaged);
    AudioClip clip=NgSound.GetAudioClip(NgSound.Weapons_Autopilot);
    if(clip==null)return;
    host=new GameObject("Archipelago Assist Audio");
    loop=host.AddComponent<AudioSource>();loop.clip=clip;loop.volume=.5f;loop.loop=true;
    loop.outputAudioMixerGroup=NgSound.GetMixerGroupFromChannel(EAudioChannel.Sfx);loop.Play();
   } catch(Exception ex) { Debug.LogWarning("Archipelago assist audio: "+ex.Message); }
  }
  public void Update() {
   if(loop==null)return;
   if(NgGameState.IsPaused && loop.isPlaying)loop.Pause();
   else if(!NgGameState.IsPaused && !loop.isPlaying)loop.UnPause();
  }
  public void Stop(bool announce) {
   if(loop!=null)loop.Stop();
   if(host!=null)UnityEngine.Object.Destroy(host);
   loop=null;host=null;
   if(announce)try { NgSound.PlayVoice(NgSound.Voice_Disengaged); }catch(Exception ex) { Debug.LogWarning("Archipelago assist audio: "+ex.Message); }
  }
 }
}
