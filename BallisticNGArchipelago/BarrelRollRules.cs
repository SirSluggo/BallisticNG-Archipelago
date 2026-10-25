using NgGame;
using NgModes;

namespace BallisticNGArchipelago {
 // Runtime event settings only. No global cheat, saved preferences or logic requirements.
 internal sealed class BarrelRollRules {
  private Gamemode.Config config;
  private RaceManager manager;
  private bool originalRolls,originalLeaderboardsDisabled;
  public bool Applied { get { return config!=null; } }
  public bool Apply() {
   Restore();
   Gamemode.Config current=RaceManager.CurrentGmConfig;
   RaceManager owner=RaceManager.Instance;
   if(current==null || owner==null) return false;
   config=current;manager=owner;originalRolls=current.ShipsCanBarrelRoll;
   originalLeaderboardsDisabled=owner.LeaderboardsDisabled;
   // Suppress uploads before enabling the additional movement mechanic.
   owner.LeaderboardsDisabled=true;
   current.ShipsCanBarrelRoll=true;
   return true;
  }
  public void Restore() {
   if(config==null) return;
   config.ShipsCanBarrelRoll=originalRolls;
   // Do not overwrite settings belonging to a different, newly loaded event.
   if(manager!=null && manager==RaceManager.Instance && RaceManager.CurrentGmConfig==config)
    manager.LeaderboardsDisabled=originalLeaderboardsDisabled;
   config=null;manager=null;
  }
 }
}
