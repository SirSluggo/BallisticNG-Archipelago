import subprocess,sys,tempfile,unittest
from pathlib import Path
from bridge_lock import BridgeLock
class LockTests(unittest.TestCase):
 def test_live_owner(self):
  with tempfile.TemporaryDirectory() as t:
   a=BridgeLock(t)
   with self.assertRaises((FileExistsError,PermissionError)):BridgeLock(t)
   a.release();b=BridgeLock(t);b.release()
 def test_killed_owner(self):
  with tempfile.TemporaryDirectory() as t:
   child=subprocess.Popen([sys.executable,'-c','from bridge_lock import BridgeLock;import sys,time;x=BridgeLock(sys.argv[1]);print("ready",flush=True);time.sleep(60)',t],stdout=subprocess.PIPE,text=True)
   try:
    self.assertEqual(child.stdout.readline().strip(),'ready');child.kill();child.wait(timeout=5)
    b=BridgeLock(t);b.release()
   finally:
    if child.poll() is None:child.kill();child.wait()
    child.stdout.close()
 def test_legacy_refused(self):
  with tempfile.TemporaryDirectory() as t:
   (Path(t)/'bridge.lock').write_bytes(b'')
   with self.assertRaises(FileExistsError):BridgeLock(t)

 def test_windows_crlf_marker(self):
  with tempfile.TemporaryDirectory() as t:
   (Path(t)/'bridge.lock').write_bytes(b'BNG-OS-LOCK-1\r\n')
   a=BridgeLock(t);a.release();b=BridgeLock(t);b.release()
