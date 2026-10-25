using UnityEngine;
using NgAudio;
namespace BallisticNGArchipelago {
 internal static class ApAudio {
  public static AudioSource Play(string[] cue,EAudioChannel channel) {
   AudioClip clip=NgSound.GetAudioClip(cue);
   if(clip==null) return null;
   // Mixer already applies master/channel settings. Avoid scaling the source a second time.
   return OneShot.CreateOneShotInterface(clip,1f,1f,NgSound.GetMixerGroupFromChannel(channel));
  }
 }
}
