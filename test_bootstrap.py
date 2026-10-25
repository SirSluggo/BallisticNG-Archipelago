import os,tempfile,unittest
from pathlib import Path
from types import SimpleNamespace
from bootstrap import ensure_runtime
class BootstrapTests(unittest.TestCase):
 def fixture(self,t,exists=True):
  mod=Path(t);runtime=mod/'PanelRuntime';runtime.mkdir()
  python=runtime/'.venv'/('Scripts/python.exe' if os.name=='nt' else 'bin/python')
  if exists:python.parent.mkdir(parents=True);python.touch()
  return mod,python
 def test_existing_ready_environment_skips_install(self):
  with tempfile.TemporaryDirectory() as t:
   mod,python=self.fixture(t);calls=[]
   def run(args,**kw):calls.append(args);return SimpleNamespace(returncode=0)
   self.assertEqual(ensure_runtime(mod,run),python);self.assertEqual(len(calls),1)
 def test_missing_environment_creates_private_venv(self):
  with tempfile.TemporaryDirectory() as t:
   mod,python=self.fixture(t,False);calls=[]
   def run(args,**kw):calls.append(args);return SimpleNamespace(returncode=0)
   ensure_runtime(mod,run);self.assertEqual(calls[0][1:3],['-m','venv']);self.assertEqual(calls[0][3],str(mod/'PanelRuntime/.venv'))
 def test_missing_pip_repaired_before_dependency_install(self):
  with tempfile.TemporaryDirectory() as t:
   mod,python=self.fixture(t);calls=[];results=iter([1,1,0,0,0])
   def run(args,**kw):calls.append(args);return SimpleNamespace(returncode=next(results))
   ensure_runtime(mod,run)
   self.assertEqual(calls[2][1:4],['-m','ensurepip','--upgrade']);self.assertIn('install',calls[3]);self.assertTrue(all(a[0]==str(python) for a in calls))
 def test_failed_install_stops_startup(self):
  with tempfile.TemporaryDirectory() as t:
   mod,python=self.fixture(t);results=iter([1,0,1])
   def run(args,**kw):return SimpleNamespace(returncode=next(results))
   with self.assertRaises(RuntimeError):ensure_runtime(mod,run)
