"""Exercise production event-local barrel-roll configuration lifecycle; .NET SDK required."""
import subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
HARNESS=r'''
using System;
using BallisticNGArchipelago;
namespace NgModes { public class Gamemode { public class Config { public bool ShipsCanBarrelRoll; } } }
namespace NgGame { public class RaceManager {
 public static RaceManager Instance;public static NgModes.Gamemode.Config CurrentGmConfig;
 public bool LeaderboardsDisabled;
} }
class Tests {
 static int checks;
 static void Assert(bool ok,string text) { if(!ok)throw new Exception(text);checks++; }
 static void Main() {
  var rules=new BarrelRollRules();
  Assert(!rules.Apply() && !rules.Applied,"no race remains unchanged");
  var first=new NgModes.Gamemode.Config();var owner=new NgGame.RaceManager();
  NgGame.RaceManager.Instance=owner;NgGame.RaceManager.CurrentGmConfig=first;
  Assert(rules.Apply() && rules.Applied && first.ShipsCanBarrelRoll && owner.LeaderboardsDisabled,"apply only to event configuration");
  first.ShipsCanBarrelRoll=false;owner.LeaderboardsDisabled=false;rules.Maintain();
  Assert(first.ShipsCanBarrelRoll && owner.LeaderboardsDisabled,"late native reset is corrected");
  rules.Restore();Assert(!first.ShipsCanBarrelRoll && !owner.LeaderboardsDisabled && !rules.Applied,"restore original event settings");
  first.ShipsCanBarrelRoll=true;owner.LeaderboardsDisabled=true;rules.Apply();rules.Restore();
  Assert(first.ShipsCanBarrelRoll && owner.LeaderboardsDisabled,"preexisting enabled configuration retained");
  first.ShipsCanBarrelRoll=false;owner.LeaderboardsDisabled=false;rules.Apply();
  var next=new NgModes.Gamemode.Config();NgGame.RaceManager.CurrentGmConfig=next;rules.Maintain();
  Assert(!first.ShipsCanBarrelRoll && next.ShipsCanBarrelRoll,"configuration replacement is independently captured");
  rules.Restore();Assert(!next.ShipsCanBarrelRoll,"replacement configuration restored");
  owner.LeaderboardsDisabled=false;rules.Apply();var other=new NgGame.RaceManager();
  NgGame.RaceManager.Instance=other;rules.Restore();
  Assert(!other.LeaderboardsDisabled,"restoration does not overwrite another manager");
  rules.Maintain();Assert(!rules.Applied && !next.ShipsCanBarrelRoll,"inactive maintenance does not enable rolls");
  Console.WriteLine("Barrel-roll lifecycle checks passed: "+checks);
 }
}
'''
def main():
 with tempfile.TemporaryDirectory() as scratch:
  p=Path(scratch)
  (p/'BarrelRollRules.cs').write_bytes((ROOT/'BallisticNGArchipelago/BarrelRollRules.cs').read_bytes())
  (p/'Program.cs').write_text(HARNESS,encoding='utf-8')
  (p/'Tests.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><NuGetAudit>false</NuGetAudit><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>',encoding='utf-8')
  subprocess.run(['dotnet','build',str(p/'Tests.csproj'),'--verbosity','quiet'],check=True)
  subprocess.run(['dotnet',str(p/'bin/Debug/net8.0/Tests.dll')],check=True)
if __name__=='__main__':main()
