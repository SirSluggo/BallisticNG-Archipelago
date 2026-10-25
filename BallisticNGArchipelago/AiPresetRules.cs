using NgGame;
using NgModes;
using NgSettings;
using NgSp;
using NgData;
using UnityEngine;

namespace BallisticNGArchipelago {
 internal sealed class AiPresetRules {
  private Gamemode.Config config;
  private RaceManager manager;
  private EAiLevel originalLevel,appliedLevel;
  private float originalSpeed;
  private bool originalLeaderboardsDisabled;
  public bool Applied { get { return config!=null; } }
  public string Report { get; private set; }
  // Generated from manifest ai_presets in Catalog.Campaigns order.
  private static readonly int[] Relaxed={0,0,0,1,1,1,1,1};
  private static readonly int[] Balanced={1,1,1,1,1,1,2,2};
  public static string Name(int preset) { return preset==1 ? "Relaxed":preset==3 ? "Balanced (legacy)":preset==2 ? "Standard":"Original"; }
  public bool Apply(string campaign,int preset) {
   Restore();
   if(preset==0 || NgCampaign.HardEnabled || Race.AiCount<=0) return false;
   int index=System.Array.IndexOf(Catalog.Campaigns,campaign);
   if(index<0 || (preset!=1 && preset!=2 && preset!=3) || RaceManager.Instance==null || RaceManager.CurrentGmConfig==null) return false;
   manager=RaceManager.Instance;config=RaceManager.CurrentGmConfig;
   originalLevel=Gameplay.AiLevel;originalSpeed=manager.AiSpeedMultiplier;originalLeaderboardsDisabled=manager.LeaderboardsDisabled;
   if(preset==2 && index<4) { config=null;manager=null;return false; }
   appliedLevel=preset==2 ? (EAiLevel)System.Math.Min((int)originalLevel,index<6 ? (int)originalLevel:index==6 ? 1:2) : (EAiLevel)(preset==1 ? Relaxed[index]:Balanced[index]);
   manager.LeaderboardsDisabled=true;
   manager.AiSpeedMultiplier=preset==2 ? (index<6 ? originalSpeed*.98f:index==6 ? Mathf.Min(originalSpeed,.97f):Mathf.Min(originalSpeed,Mathf.Clamp(originalSpeed,.95f,1f)*.95f)) : Mathf.Clamp(originalSpeed,preset==1 ? .90f:.95f,preset==1 ? .98f:1f);
   Gameplay.AiLevel=appliedLevel;
   Report=Name(preset)+" campaign="+campaign+" native="+originalLevel+"/"+originalSpeed.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+
    " applied="+appliedLevel+"/"+manager.AiSpeedMultiplier.ToString("0.000",System.Globalization.CultureInfo.InvariantCulture)+" leaderboard_uploads=disabled";
   return true;
  }
  public void Restore() {
   if(config==null) return;
   bool same=manager!=null && manager==RaceManager.Instance && RaceManager.CurrentGmConfig==config;
   if(same) {
    Gameplay.AiLevel=originalLevel;
    manager.AiSpeedMultiplier=originalSpeed;
    manager.LeaderboardsDisabled=originalLeaderboardsDisabled;
   } else if(NgSceneManager.GetLoadedScene()=="Menu" && Gameplay.AiLevel==appliedLevel) Gameplay.AiLevel=originalLevel;
   config=null;manager=null;
  }
 }
}
