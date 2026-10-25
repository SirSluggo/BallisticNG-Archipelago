import tempfile,unittest
from pathlib import Path
from unittest.mock import patch
from bridge_lock import BridgeLock
from offline_normal import return_normal
from panel_client import switch_and_launch
class OfflineNormalTests(unittest.IsolatedAsyncioTestCase):
 async def test_waits_then_returns_without_network(self):
  with tempfile.TemporaryDirectory() as t:
   events=[]
   async def wait():events.append('closed')
   def switch(mod,action):events.append(action);return 'launched'
   with patch('bridge.Bridge.session',side_effect=AssertionError('network forbidden')):
    self.assertEqual(await return_normal(t,wait,switch),'launched')
   self.assertEqual(events,['closed','normal'])
 async def test_timeout_never_selects_or_launches(self):
  with tempfile.TemporaryDirectory() as t:
   async def wait():raise TimeoutError('still running')
   def switch(*args):self.fail('must not launch')
   with self.assertRaises(TimeoutError):await return_normal(t,wait,switch)
 async def test_live_client_blocks_offline_helper(self):
  with tempfile.TemporaryDirectory() as t:
   lock=BridgeLock(t)
   async def wait():pass
   def switch(*args):self.fail('must not launch')
   try:
    with self.assertRaises((FileExistsError,PermissionError)):await return_normal(t,wait,switch,lock_timeout=0)
   finally:lock.release()
 async def test_profile_validation_failure_prevents_launch(self):
  with tempfile.TemporaryDirectory() as t:
   async def wait():pass
   class Profiles:
    def run(self,*args):raise ValueError('changed normal save')
   def switch(mod,action):
    switch_and_launch(mod,action,profiles=Profiles(),launch=lambda:self.fail('must not launch'))
   mod=Path(t)/'game/User/Mods/Code Mods/BallisticNGArchipelago';mod.mkdir(parents=True)
   with self.assertRaises(ValueError):await return_normal(mod,wait,switch)

class ShutdownWaitTests(unittest.IsolatedAsyncioTestCase):
 async def test_waits_for_old_client_to_release_lock(self):
  import asyncio
  with tempfile.TemporaryDirectory() as t:
   lock=BridgeLock(t)
   async def wait():pass
   def switch(*args):return 'launched'
   async def release():
    await asyncio.sleep(.05);lock.release()
   task=asyncio.create_task(release())
   self.assertEqual(await return_normal(t,wait,switch,lock_timeout=2),'launched')
   await task
