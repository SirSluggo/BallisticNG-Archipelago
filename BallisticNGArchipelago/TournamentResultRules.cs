namespace BallisticNGArchipelago {
 internal static class TournamentResultRules {
  public static bool FinalRace(EventSpec spec,bool enabled,int trackCount,int trackIndex,int shipCount,int rosterCount,string scene) {
   if(spec==null || spec.Mode!="Race" || !enabled || shipCount<1 || rosterCount!=shipCount)return false;
   string[] levels=spec.Levels.Split('|');
   return levels.Length>1 && trackCount==levels.Length && trackIndex==levels.Length-1 && scene==levels[levels.Length-1];
  }
 }
}
