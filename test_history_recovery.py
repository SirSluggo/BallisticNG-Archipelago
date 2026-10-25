from runtime_files import route
import asyncio,json,tempfile,unittest
from pathlib import Path
from unittest.mock import patch
import websockets
from bridge import Bridge,M
from history_recovery import HistoryConflict,HistorySyncRequired
from test_multiworld_toasts import connection,Socket

def item(code,location=-1,flags=1):return {'item':code,'location':location,'player':1 if location>=0 else 0,'flags':flags,'class':'NetworkItem'}
TECHNO=item(M['access_items']['Techno']);URBAN=item(M['access_items']['Urban']);FILLER=item(M['filler_id'],21591000,0)
class RecoveryTests(unittest.IsolatedAsyncioTestCase):
 async def asyncSetUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.mod=self.root/'mod';self.mod.mkdir();self.state=self.root/'state'
  self.b=Bridge(self.mod,self.state,'BNGTest');self.b.seed='recovery-test';self.ws=Socket()
 async def asyncTearDown(self):self.temp.cleanup()
 async def connect(self):await self.b.packet(self.ws,connection())
 async def receive(self,items,index=0):await self.b.packet(self.ws,{'cmd':'ReceivedItems','index':index,'items':items})
 async def seed(self,items):await self.connect();await self.receive(items)
 async def conflict(self,items):
  self.b.deactivate();await self.connect()
  with self.assertRaises(HistoryConflict):await self.receive(items)
  return self.b.recovery
 async def apply(self,items,proposal):
  self.assertTrue(self.b.approve_recovery(proposal['id']));await self.connect();await self.receive(items)
 def access(self):return (route(self.mod,'access-'+self.b.token+'.txt')).read_text().splitlines()
 async def test_no_activation_or_checks_before_full_history(self):
  await self.connect();await self.b.poll(self.ws)
  self.assertFalse(self.b.ready);self.assertFalse((route(self.mod,'active-session.txt')).exists())
  self.assertFalse(any(p['cmd']=='LocationChecks' for p in self.ws.sent))
  await self.receive([TECHNO],4);self.assertFalse(self.b.ready);self.assertEqual(self.ws.sent[-1]['cmd'],'Sync')
  await self.receive([]);self.assertTrue(self.b.ready)
 async def test_duplicate_admin_items_are_valid(self):
  await self.seed([TECHNO]);await self.receive([TECHNO],1);self.assertEqual(len(self.b.state['items']),2)
  self.b.deactivate();await self.connect();await self.receive([TECHNO,TECHNO]);self.assertTrue(self.b.ready);self.assertEqual(self.b.toast_rows,[])
 async def test_replaced_suffix_backup_preview_confirm_and_reconnect(self):
  await self.seed([FILLER,TECHNO]);before=self.b.state_path.read_bytes();proposal=await self.conflict([FILLER,URBAN])
  self.assertEqual(Path(proposal['backup']).read_bytes(),before)
  self.assertIn('No longer owned: Techno Invitational',proposal['text']);self.assertIn('Newly owned: Urban Invitational',proposal['text'])
  self.assertFalse(self.b.ready);self.assertFalse((route(self.mod,'active-session.txt')).exists());self.assertEqual(self.b.state_path.read_bytes(),before)
  self.assertFalse(self.b.approve_recovery('wrong'));await self.apply([FILLER,URBAN],proposal)
  self.assertTrue(self.b.ready);self.assertIn('Urban',self.access());self.assertNotIn('Techno',self.access());self.assertEqual(self.b.toast_rows,[])
  self.b.deactivate();await self.connect();await self.receive([FILLER,URBAN]);self.assertEqual(self.b.toast_rows,[])
  self.assertEqual(len(list((self.state/'recovery-backups').glob('*.json'))),2)
 async def test_shorter_and_empty_history_detected(self):
  await self.seed([TECHNO]);proposal=await self.conflict([]);self.assertIn('Server receipts: 0',proposal['text'])
  await self.apply([],proposal);self.assertNotIn('Techno',self.access());self.assertEqual(self.b.state['items'],[])
 async def test_duplicate_grant_removal_keeps_unlock(self):
  await self.seed([TECHNO,TECHNO]);proposal=await self.conflict([TECHNO]);self.assertIn('No longer owned: None.',proposal['text'])
  await self.apply([TECHNO],proposal);self.assertIn('Techno',self.access())
 async def test_serialization_metadata_not_conflict(self):
  await self.seed([TECHNO]);other=dict(TECHNO);other.pop('class');other['unrelated']='metadata'
  self.b.deactivate();await self.connect();await self.receive([other]);self.assertTrue(self.b.ready)
 async def test_pending_capture_resend_confirm_no_replayed_notification(self):
  await self.seed([TECHNO]);key=next(k for k,v in self.b.checks.items() if v==21591000)
  (route(self.mod,'checks-'+self.b.token+'.txt')).write_text(key+'\n');proposal=await self.conflict([])
  self.assertEqual(self.b.state['pending'],[21591000]);await self.apply([],proposal);await self.b.poll(self.ws)
  self.assertIn({'cmd':'LocationChecks','locations':[21591000]},self.ws.sent)
  await self.receive([FILLER]);self.assertEqual(self.b.toast_rows,[])
  await self.b.packet(self.ws,{'cmd':'RoomUpdate','checked_locations':[21591000]});await self.b.poll(self.ws);self.assertEqual(self.b.state['pending'],[])
 async def test_changed_snapshot_requires_new_review(self):
  await self.seed([TECHNO]);proposal=await self.conflict([URBAN]);before=self.b.state_path.read_bytes()
  self.b.approve_recovery(proposal['id']);await self.connect()
  with self.assertRaises(HistoryConflict):await self.receive([URBAN,FILLER])
  self.assertNotEqual(self.b.recovery['id'],proposal['id']);self.assertEqual(self.b.state_path.read_bytes(),before);self.assertFalse(self.b.ready)
 async def test_changed_room_cannot_use_approval(self):
  await self.seed([TECHNO]);proposal=await self.conflict([URBAN]);self.b.approve_recovery(proposal['id']);self.b.seed='another-room'
  with self.assertRaisesRegex(ValueError,'Room changed'):await self.connect()
  self.assertFalse(self.b.ready)
 async def test_concurrent_local_edit_cannot_be_overwritten(self):
  await self.seed([TECHNO]);proposal=await self.conflict([URBAN]);self.b.approve_recovery(proposal['id']);await self.connect()
  data=json.loads(self.b.state_path.read_text());data['sentinel']='external update';self.b.state_path.write_text(json.dumps(data))
  with self.assertRaisesRegex(ValueError,'state changed'):await self.receive([URBAN])
  self.assertEqual(json.loads(self.b.state_path.read_text())['sentinel'],'external update')
 async def test_backup_failure_does_not_commit(self):
  await self.seed([TECHNO]);proposal=await self.conflict([URBAN]);self.b.approve_recovery(proposal['id']);await self.connect();before=self.b.state_path.read_bytes()
  with patch('bridge.backup_state',side_effect=OSError('disk full')):
   with self.assertRaises(OSError):await self.receive([URBAN])
  self.assertEqual(self.b.state_path.read_bytes(),before);self.assertFalse(self.b.ready)
 async def test_incremental_conflict_requires_full_snapshot(self):
  await self.seed([FILLER,TECHNO])
  with self.assertRaises(HistorySyncRequired):await self.receive([URBAN],1)
  self.assertFalse(self.b.ready);self.assertIsNone(self.b.recovery)
 async def test_entire_packet_validated_before_write(self):
  await self.seed([TECHNO]);before=self.b.state_path.read_bytes()
  with self.assertRaises(ValueError):await self.receive([URBAN,{'item':'bad'}],1)
  self.assertEqual(self.b.state_path.read_bytes(),before)
 async def test_legacy_cache_without_recovery_fields(self):
  await self.seed([TECHNO]);self.b.deactivate()
  legacy=json.loads(self.b.state_path.read_text());legacy.pop('quiet_checks',None)
  self.b.state_path.write_text(json.dumps(legacy));before=self.b.state_path.read_bytes()
  await self.connect()
  with self.assertRaises(HistoryConflict):await self.receive([URBAN])
  proposal=self.b.recovery;self.assertEqual(Path(proposal['backup']).read_bytes(),before)
  await self.apply([URBAN],proposal);self.assertTrue(self.b.ready)
 async def test_new_notifications_resume(self):
  await self.seed([TECHNO]);proposal=await self.conflict([]);await self.apply([],proposal);await self.receive([URBAN]);self.assertEqual(len(self.b.toast_rows),1)

class TransportTests(unittest.IsolatedAsyncioTestCase):
 async def test_websocket_rollback_review_commit(self):
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'mod';mod.mkdir();b=Bridge(mod,root/'state','BNGTest');history=[TECHNO];rounds=0
   async def server(ws):
    nonlocal rounds
    rounds+=1;await ws.send(json.dumps([{'cmd':'RoomInfo','seed_name':'transport-rollback'}]));await ws.recv()
    await ws.send(json.dumps([connection(),{'cmd':'ReceivedItems','index':0,'items':history}]))
    async for raw in ws:
     for packet in json.loads(raw):
      if packet['cmd']=='Sync':
       try:await ws.send(json.dumps([{'cmd':'ReceivedItems','index':0,'items':history}]))
       except websockets.exceptions.ConnectionClosed:return
   async def ready(task):
    for _ in range(100):
     if task.done():await task
     if b.ready:return
     await asyncio.sleep(.01)
    self.fail('not ready')
   async with websockets.serve(server,'127.0.0.1',0) as host:
    url='ws://127.0.0.1:'+str(host.sockets[0].getsockname()[1]);task=asyncio.create_task(b.session(url));await ready(task)
    task.cancel();await asyncio.gather(task,return_exceptions=True);history=[URBAN]
    with self.assertRaises(HistoryConflict):await asyncio.wait_for(b.session(url),3)
    self.assertFalse(b.ready);self.assertTrue(b.approve_recovery(b.recovery['id']))
    task=asyncio.create_task(b.session(url));await ready(task);self.assertEqual(b.state['items'],[URBAN]);self.assertEqual(b.toast_rows,[])
    task.cancel();await asyncio.gather(task,return_exceptions=True);self.assertEqual(rounds,3)
if __name__=='__main__':unittest.main()
