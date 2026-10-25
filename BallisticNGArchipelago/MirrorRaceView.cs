using UnityEngine;
namespace BallisticNGArchipelago {
 // Added only to the player's world camera. Screen-space HUD is rendered separately.
 public sealed class MirrorRaceView : MonoBehaviour {
  private void OnRenderImage(RenderTexture source,RenderTexture destination) {
   Graphics.Blit(source,destination,new Vector2(-1f,1f),new Vector2(1f,0f));
  }
 }
}
