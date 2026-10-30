"""Exercise the production C# consumable ledger using an installed .NET SDK."""
import os,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
HARNESS=r'''
using System;
using System.IO;
using BallisticNGArchipelago;
namespace BallisticNGArchipelago { public static class SaveProfile { public static bool IsKey(string key) { return key!=null && key.Length==64; } } }
class Tests {
 static int checks;
 static void Assert(bool ok,string name) { if(!ok)throw new Exception(name);checks++; }
 static void Main(string[] args) {
  string key=new string('a',64),path=Path.Combine(args[0],"ledger.txt");
  string first=Catalog.Events[0].Key,second=Catalog.Events[1].Key;
  var a=new ConsumableLedger(path,key);Assert(a.Assists==0 && a.Passes==0,"new ledger is empty");
  a.SetDelivered(3);a.SetDelivered(3);Assert(a.Assists==3,"history replay does not duplicate inventory");
  Assert(a.SpendAssist(),"consume assist");
  var b=new ConsumableLedger(path,key);Assert(b.Assists==2,"assist consumption survives restart");
  Assert(b.Reward(first) && !b.Reward(first) && b.Passes==1,"Platinum is granted only once per event");
  Assert(b.SpendPass(second) && b.Tainted(second),"pass marks attempt before result");
  var c=new ConsumableLedger(path,key);Assert(c.Passes==0 && c.Tainted(second),"pass consumption and mark survive restart");
  c.NewAttempt(second,false);Assert(!c.Reward(second),"later tournament round cannot reward a pass");
  c.NewAttempt(second,true);Assert(c.Reward(second),"a clean fresh attempt can earn its Platinum reward");
  Assert(c.Reward(Catalog.Events[2].Key),"another genuine Platinum earns a pass");
  Assert(c.SpendPass(first) && c.SpendPass(second),"multiple event attempts can be marked");
  c.NewAttempt(first,true);Assert(!c.Tainted(first) && c.Tainted(second),"new attempt clears only that event mark");
  var marked=new ConsumableLedger(path,key);Assert(marked.Tainted(second),"multiple-event mark survives restart");
  Assert(!c.Reward("unknown-event"),"unknown event rejected");
  bool decreased=false;try { c.SetDelivered(1); } catch(IOException) { decreased=true; }
  Assert(decreased && c.Assists==2,"history shrink does not restore spent assists");
  Directory.CreateDirectory(path+".tmp");bool denied=false;
  try { c.SpendAssist(); } catch(UnauthorizedAccessException) { denied=true; } catch(IOException) { denied=true; }
  Assert(denied && c.Assists==2,"failed persistent write prevents consumption");
  var d=new ConsumableLedger(path,key);Assert(d.Assists==2,"failed write preserves prior ledger");
  bool wrong=false;try { new ConsumableLedger(path,new string('b',64)); } catch(IOException) { wrong=true; }
  Assert(wrong,"wrong seed refused");
  File.AppendAllText(path,"corruption");bool corrupt=false;
  try { new ConsumableLedger(path,key); } catch(IOException) { corrupt=true; }
  Assert(corrupt,"corrupt ledger refused without empty fallback");
  Console.WriteLine("Native ledger checks passed: "+checks);
 }
}
'''

def main():
 with tempfile.TemporaryDirectory() as scratch:
  p=Path(scratch)
  for name in ('ConsumableLedger.cs','Catalog.cs'):(p/name).write_bytes((ROOT/'BallisticNGArchipelago'/name).read_bytes())
  (p/'Program.cs').write_text(HARNESS,encoding='utf-8')
  (p/'Tests.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><NuGetAudit>false</NuGetAudit><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>',encoding='utf-8')
  subprocess.run(['dotnet','build',str(p/'Tests.csproj'),'--verbosity','quiet'],check=True)
  subprocess.run(['dotnet',str(p/'bin/Debug/net8.0/Tests.dll'),str(p)],check=True)
if __name__=='__main__':main()
