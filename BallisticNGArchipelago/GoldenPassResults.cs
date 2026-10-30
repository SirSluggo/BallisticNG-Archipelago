using System;
using System.Collections.Generic;
using System.Reflection;
using NgShips;
using NgModes;

namespace BallisticNGArchipelago {
 // Production pass path: prepare a winning result, then let native completion award it.
 // The game's debug helpers silently skip ranking/rescoring outside debug mode.
 public static class GoldenPassResults {
  private static readonly Dictionary<Type,MethodInfo> scoreFinish=new Dictionary<Type,MethodInfo>();
  private static MethodInfo ScoreFinish(Gamemode mode) {
   Type type=mode.GetType();MethodInfo method;
   if(!scoreFinish.TryGetValue(type,out method)) {
    method=type.GetMethod("FinishSession",BindingFlags.Instance|BindingFlags.NonPublic,null,new Type[] {typeof(ShipController)},null);
    scoreFinish[type]=method;
   }
   return method;
  }
  public static bool Supported(string modeName,Gamemode mode) {
   if(mode==null)return false;
   return modeName=="Race" || modeName=="Team Race" || modeName=="Time Trial" ||
    ((modeName=="Eliminator" || modeName=="Upsurge") && ScoreFinish(mode)!=null);
  }
  public static void Complete(string modeName,Gamemode mode,ShipController player,IEnumerable<ShipController> ships,float goldTime) {
   if(!Supported(modeName,mode))throw new InvalidOperationException("Native completion API unavailable for this mode");
   List<ShipController> ordered=new List<ShipController>();ordered.Add(player);
   foreach(ShipController ship in ships)if(ship!=null && ship!=player)ordered.Add(ship);
   bool score=modeName=="Eliminator" || modeName=="Upsurge";
   for(int i=0;i<ordered.Count;i++) {
    ordered[i].CurrentPlace=i+1;
    if(ordered[i].FinishedEvent)ordered[i].FinalPlace=i+1;
    // Freeze all scores before completion without triggering partial score-change results.
    if(score)ordered[i].SetScore(ordered.Count-i,false);
   }
   if(score)ScoreFinish(mode).Invoke(mode,new object[] {player});
   else {
    if(modeName=="Time Trial")player.TotalRaceTime=goldTime;
    mode.NextLapFinishTask(player);
   }
  }
 }
}
