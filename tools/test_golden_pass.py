"""Regression checks against the production pass completion helper; .NET SDK required."""
import subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
HARNESS=r'''
using System;
using System.Collections.Generic;
using System.Linq;
using BallisticNGArchipelago;
namespace NgShips {
 public class ShipController {
  public int CurrentPlace,FinalPlace;public bool FinishedEvent;public float TotalRaceTime,Score;
  public int Callbacks;
  public void SetScore(float value,bool callDelegate=true,bool ignoreNetworked=true) { Score=value;if(callDelegate)Callbacks++; }
 }
}
namespace NgModes {
 using NgShips;
 public class Gamemode {
  public int Finished;
  public virtual void NextLapFinishTask(ShipController p) { p.FinalPlace=p.CurrentPlace;p.FinishedEvent=true;Finished++; }
  public void TestSingleplayerCompleteCondition(int place) { throw new Exception("Production pass used a debug helper"); }
 }
 public class ScoreMode:Gamemode {
  public List<ShipController> Ships;
  private void FinishSession(ShipController p) {
   if(Ships.Any(s=>s.Callbacks!=0))throw new Exception("Scores finished prematurely");
   p.FinalPlace=Ships.OrderByDescending(s=>s.Score).ToList().IndexOf(p)+1;p.FinishedEvent=true;Finished++;
  }
 }
}
class Tests {
 static int checks;
 static void Assert(bool ok,string text) { if(!ok)throw new Exception(text);checks++; }
 static void Main() {
  var ships=Enumerable.Range(1,8).Select(i=>new NgShips.ShipController { CurrentPlace=i,FinalPlace=i,Score=100-i }).ToList();
  var player=ships[7];var race=new NgModes.Gamemode();
  Assert(player.CurrentPlace==8,"regression starts in eighth");
  GoldenPassResults.Complete("Race",race,player,ships,0);
  Assert(player.FinalPlace==1 && player.FinishedEvent && race.Finished==1,"pass supplies winning place before native completion");
  Assert(ships.Select(s=>s.CurrentPlace).Distinct().Count()==8,"unique ranks remain");
  var time=new NgModes.Gamemode();player.FinishedEvent=false;
  GoldenPassResults.Complete("Time Trial",time,player,ships,59.9f);
  Assert(player.TotalRaceTime==59.9f && time.Finished==1,"time trial supplies Gold time before finishing");
  foreach(string mode in new string[] {"Eliminator","Upsurge"}) {
   player.FinishedEvent=false;player.Score=-1;
   var score=new NgModes.ScoreMode { Ships=ships };
   Assert(GoldenPassResults.Supported(mode,score),"native score completion found");
   GoldenPassResults.Complete(mode,score,player,ships,0);
   Assert(player.FinalPlace==1 && score.Finished==1,"score mode finishes first");
   Assert(ships.All(s=>s.Callbacks==0),"no intermediate score-change result");
  }
  Assert(!GoldenPassResults.Supported("Eliminator",race),"missing score API rejected before spending");
  var team=new NgModes.Gamemode();player.FinishedEvent=false;
  GoldenPassResults.Complete("Team Race",team,player,ships,0);
  Assert(team.Finished==1 && player.FinalPlace==1,"team mode uses native completion");
  Console.WriteLine("Golden pass result checks passed: "+checks);
 }
}
'''
def main():
 with tempfile.TemporaryDirectory() as scratch:
  p=Path(scratch)
  (p/'GoldenPassResults.cs').write_bytes((ROOT/'BallisticNGArchipelago/GoldenPassResults.cs').read_bytes())
  (p/'Program.cs').write_text(HARNESS,encoding='utf-8')
  (p/'Tests.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><NuGetAudit>false</NuGetAudit><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>',encoding='utf-8')
  subprocess.run(['dotnet','build',str(p/'Tests.csproj'),'--verbosity','quiet'],check=True)
  subprocess.run(['dotnet',str(p/'bin/Debug/net8.0/Tests.dll')],check=True)
if __name__=='__main__':main()
