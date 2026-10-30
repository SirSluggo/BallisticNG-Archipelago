using UnityEngine;
namespace BallisticNGArchipelago {
 internal static class PanelStyles {
  private static Texture2D background;
  public static GUIStyle OpaqueWindow() {
   if(background==null) {
    background=new Texture2D(1,1,TextureFormat.RGBA32,false);
    background.SetPixel(0,0,new Color(.035f,.045f,.06f,1f));background.Apply();
   }
   GUIStyle style=new GUIStyle(GUI.skin.window);
   style.normal.background=background;style.onNormal.background=background;
   style.hover.background=background;style.onHover.background=background;
   style.active.background=background;style.onActive.background=background;
   style.focused.background=background;style.onFocused.background=background;
   style.normal.textColor=Color.white;style.onNormal.textColor=Color.white;
   style.hover.textColor=style.onHover.textColor=style.active.textColor=style.onActive.textColor=style.focused.textColor=style.onFocused.textColor=Color.white;
   style.padding=new RectOffset(20,20,36,18);style.fontSize=16;
   return style;
  }
 }
}
