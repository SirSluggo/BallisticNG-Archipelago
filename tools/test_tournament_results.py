"""Production tournament eligibility and medal-tier regression checks; .NET SDK required."""
import subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
HARNESS=r'''
using System;
using System.Linq;
using BallisticNGArchipelago;
namespace NgSp {
 public enum NgAward { None,Bronze,Silver,Gold,Platinum }
 public static class NgCampaign { public static bool HardEnabled; }
 public class NgCampaignEvent { public class Progress { public float BronzeValue,SilverValue,GoldValue,PlatinumValue; } }
}
class Tests {
 static int checks;
 static void Assert(bool ok,string name) { if(!ok)throw new Exception(name);checks++; }
 static void Main() {
  EventSpec spec=Catalog.Events.First(e=>e.Mode=="Race" && e.Levels.Contains("|"));
  string[] tracks=spec.Levels.Split('|');int last=tracks.Length-1;
  Assert(TournamentResultRules.FinalRace(spec,true,tracks.Length,last,8,8,tracks[last]),"final tournament race is eligible even without a finish flag");
  Assert(!TournamentResultRules.FinalRace(spec,true,tracks.Length,0,8,8,tracks[0]),"early round rejected");
  Assert(!TournamentResultRules.FinalRace(spec,false,tracks.Length,last,8,8,tracks[last]),"non-tournament rejected");
  Assert(!TournamentResultRules.FinalRace(spec,true,tracks.Length+1,last,8,8,tracks[last]),"mismatched track catalog rejected");
  Assert(!TournamentResultRules.FinalRace(spec,true,tracks.Length,last,7,8,tracks[last]),"mismatched ship roster rejected");
  Assert(!TournamentResultRules.FinalRace(spec,true,tracks.Length,last,8,8,"different-track"),"different track rejected");
  Assert(!TournamentResultRules.FinalRace(null,true,tracks.Length,last,8,8,tracks[last]),"missing event rejected");
  EventSpec single=Catalog.Events.First(e=>e.Mode=="Race" && !e.Levels.Contains("|"));
  Assert(!TournamentResultRules.FinalRace(single,true,1,0,8,8,single.Levels),"single race cannot use tournament exception");
  Assert(MedalRules.Position(1)==NgSp.NgAward.Gold,"first overall earns Gold");
  Assert(MedalRules.Position(2)==NgSp.NgAward.Silver,"historical Gold cannot upgrade current second overall");
  Assert(MedalRules.Position(3)==NgSp.NgAward.Bronze && MedalRules.Position(4)==NgSp.NgAward.None,"lower current standings yield correct tiers");
  NgSp.NgCampaign.HardEnabled=true;
  Assert(MedalRules.Position(1)==NgSp.NgAward.Platinum && MedalRules.Position(2)==NgSp.NgAward.None,"native hard-mode conversion retained");
  Console.WriteLine("Tournament result checks passed: "+checks);
 }
}
'''
def main():
 with tempfile.TemporaryDirectory() as scratch:
  p=Path(scratch)
  for name in ('TournamentResultRules.cs','MedalRules.cs','Catalog.cs'):(p/name).write_bytes((ROOT/'BallisticNGArchipelago'/name).read_bytes())
  (p/'Program.cs').write_text(HARNESS,encoding='utf-8')
  (p/'Tests.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><NuGetAudit>false</NuGetAudit><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>',encoding='utf-8')
  subprocess.run(['dotnet','build',str(p/'Tests.csproj'),'--verbosity','quiet'],check=True)
  subprocess.run(['dotnet',str(p/'bin/Debug/net8.0/Tests.dll')],check=True)
if __name__=='__main__':main()
