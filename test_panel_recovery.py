from runtime_files import route
import asyncio,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import websockets
import panel_client
from bridge import Bridge
from test_multiworld_toasts import connection,Socket
from test_history_recovery import TECHNO,URBAN

class PanelRecoveryTests(unittest.IsolatedAsyncioTestCase):
 async def test_panel_review_nonce_freshness_resync_and_disconnect(self):
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'game/User/Mods/Code Mods/BallisticNGArchipelago';mod.mkdir(parents=True);state=root/'state'
   initial=Bridge(mod,state,'RecoveryTest');initial.seed='panel-test';await initial.packet(Socket(),connection())
   await initial.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[TECHNO]});initial.deactivate()
   clients=[];history=[URBAN]
   def make_bridge(mod,ignored,name,password):
    b=Bridge(mod,state,name,password);clients.append(b);return b
   class Profiles:
    def __init__(self,game):pass
    def request(self,mod):return {'key':clients[-1].state_path.stem,'identity':clients[-1].state['identity']}
   async def server(ws):
    await ws.send(json.dumps([{'cmd':'RoomInfo','seed_name':'panel-test'}]));await ws.recv()
    await ws.send(json.dumps([connection(),{'cmd':'ReceivedItems','index':0,'items':history}]))
    try:
     async for raw in ws:
      for p in json.loads(raw):
       if p['cmd']=='Sync':await ws.send(json.dumps([{'cmd':'ReceivedItems','index':0,'items':history}]))
    except websockets.exceptions.ConnectionClosed:pass
   async def status(expected):
    for _ in range(100):
     path=route(mod,'panel-status.txt')
     if path.exists():
      lines=path.read_text().splitlines()
      if len(lines)>3 and lines[1]==expected:return lines
     await asyncio.sleep(.05)
    self.fail('missing status '+expected)
   async with websockets.serve(server,'127.0.0.1',0) as host:
    url='ws://127.0.0.1:'+str(host.sockets[0].getsockname()[1])
    with patch.object(panel_client,'Bridge',side_effect=make_bridge),patch.object(panel_client,'Profiles',Profiles),patch.object(panel_client,'game_closed',return_value=False):
     task=asyncio.create_task(panel_client.run(mod,url,'RecoveryTest',None))
     try:
      lines=await status('recovery');nonce=lines[0];report=(route(mod,'panel-recovery.txt')).read_text().splitlines()
      self.assertEqual(report[0],nonce);self.assertIn('Techno Invitational','\n'.join(report));self.assertFalse((route(mod,'active-session.txt')).exists())
      stamp=(route(mod,'panel-status.txt')).stat().st_mtime_ns
      await asyncio.sleep(.7);self.assertGreater((route(mod,'panel-status.txt')).stat().st_mtime_ns,stamp)
      # Commands from another panel instance cannot authorize recovery.
      (route(mod,'panel-command.txt')).write_text('wrong-nonce\nresync:'+report[1]+'\n')
      await asyncio.sleep(.6);self.assertEqual((await status('recovery'))[0],nonce)
      (route(mod,'panel-command.txt')).write_text(nonce+'\nresync:'+report[1]+'\n')
      await status('connected');self.assertEqual(clients[-1].state['items'],[URBAN]);self.assertEqual(clients[-1].toast_rows,[])
      (route(mod,'panel-command.txt')).write_text(nonce+'\ndisconnect\n');await asyncio.wait_for(task,3)
      from bridge_lock import BridgeLock
      probe=BridgeLock(mod);probe.release();self.assertFalse((route(mod,'active-session.txt')).exists())
     finally:
      if not task.done():task.cancel()
      await asyncio.gather(task,return_exceptions=True)
if __name__=='__main__':unittest.main()
