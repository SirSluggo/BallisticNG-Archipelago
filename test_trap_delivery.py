from runtime_files import route
import asyncio,json,tempfile,unittest
from pathlib import Path
import websockets
from bridge import Bridge,M
from trap_delivery import pending_traps,receipt_key
from test_multiworld_toasts import Socket,connection

def trap(kind=0,location=-1):return {'item':21598000+kind,'location':location,'player':0,'flags':4,'class':'NetworkItem'}
def connect(traps=True):
 p=connection();p['slot_data']['traps']=int(traps);return p

class TrapDeliveryTests(unittest.IsolatedAsyncioTestCase):
 async def asyncSetUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.mod=self.root/'mod';self.mod.mkdir();self.state=self.root/'state'
  self.b=Bridge(self.mod,self.state,'TrapTest');self.b.seed='trap-test';self.ws=Socket()
 async def asyncTearDown(self):self.temp.cleanup()
 async def receive(self,items,index=0,b=None):await (b or self.b).packet(self.ws,{'cmd':'ReceivedItems','index':index,'items':items})
 async def start(self,items=(),enabled=True):await self.b.packet(self.ws,connect(enabled));await self.receive(list(items))
 def rows(self,b=None):
  b=b or self.b;return (route(self.mod,'traps-'+b.token+'.txt')).read_text().splitlines()[2:]
 def acknowledge(self,items,token=None):
  token=token or self.b.token
  (route(self.mod,'trap-acks-'+token+'.txt')).write_text(token+'\n'+M['digest']+'\n'+'\n'.join(receipt_key(i,x) for i,x in items)+'\n')
 async def test_live_trap_and_repeat_sync_queue_once(self):
  await self.start();await self.receive([trap()]);self.assertEqual(self.rows(),[receipt_key(0,trap())+'\t0'])
  await self.receive([trap()]);self.assertEqual(len(self.rows()),1)
  self.assertEqual(self.b.toast_rows[-1][1]&4,4)
 async def test_admin_packet_missing_trap_flags_still_marks_trap_receipt(self):
  await self.start();row=trap();row['flags']=0;await self.receive([row])
  self.assertEqual(self.b.toast_rows[-1][1]&4,4);self.assertEqual(len(self.rows()),1)
 async def test_ack_write_failure_preserves_retryable_receipt(self):
  from unittest.mock import patch
  await self.start([trap()]);self.acknowledge([(0,trap())])
  with patch('bridge.atomic_json',side_effect=PermissionError('temporarily busy')):
   with self.assertRaises(PermissionError):await self.b.poll(self.ws)
  self.assertEqual(len(pending_traps(self.b.state,M['trap_items'],True)),1)
  await self.b.poll(self.ws);self.assertEqual(self.rows(),[])
 async def test_flag_only_resync_does_not_replay_started_trap(self):
  from history_recovery import HistoryConflict
  row=trap();row['flags']=0;await self.start([row]);self.acknowledge([(0,row)]);await self.b.poll(self.ws);self.b.deactivate()
  await self.b.packet(self.ws,connect())
  with self.assertRaises(HistoryConflict):await self.receive([trap()])
  self.assertTrue(self.b.approve_recovery(self.b.recovery['id']));await self.b.packet(self.ws,connect());await self.receive([trap()])
  self.assertEqual(self.rows(),[])
 async def test_all_nine_kinds_publish_correct_ids(self):
  items=[trap(i) for i in range(9)];await self.start(items)
  self.assertEqual([int(x.split('\t')[1]) for x in self.rows()],list(range(9)));self.assertEqual(self.b.toast_rows,[])
 async def test_consumed_survives_reconnect_and_duplicate_is_distinct(self):
  await self.start([trap(),trap()]);self.acknowledge([(0,trap())]);await self.b.poll(self.ws)
  self.assertEqual(self.rows(),[receipt_key(1,trap())+'\t0']);self.b.deactivate()
  await self.b.packet(self.ws,connect());await self.receive([trap(),trap()])
  self.assertEqual(self.rows(),[receipt_key(1,trap())+'\t0']);self.assertEqual(self.b.toast_rows,[])
 async def test_offline_trap_waits_and_is_quiet_on_connect(self):
  await self.start([trap(2)]);self.b.deactivate()
  await self.b.packet(self.ws,connect());await self.receive([trap(2),trap(7)])
  self.assertEqual(len(self.rows()),2);self.assertEqual(self.b.toast_rows,[])
 async def test_ack_at_disconnect_is_collected_before_new_session(self):
  await self.start([trap(1)]);self.acknowledge([(0,trap(1))]);self.b.deactivate()
  await self.b.packet(self.ws,connect());await self.receive([trap(1)]);self.assertEqual(self.rows(),[])
 async def test_crash_recovery_reads_last_session_ack_without_deactivate(self):
  await self.start([trap(3)]);self.acknowledge([(0,trap(3))])
  fresh=Bridge(self.mod,self.state,'TrapTest');fresh.seed='trap-test'
  await fresh.packet(self.ws,connect());await self.receive([trap(3)],b=fresh);self.assertEqual(self.rows(fresh),[])
 async def test_wrong_token_digest_or_unknown_receipt_cannot_consume(self):
  await self.start([trap(4)]);path=route(self.mod,'trap-acks-'+self.b.token+'.txt')
  key=receipt_key(0,trap(4))
  for text in ['wrong\n'+M['digest']+'\n'+key+'\n',self.b.token+'\nwrong\n'+key+'\n',self.b.token+'\n'+M['digest']+'\n999:21598004:-1:0\n']:
   path.write_text(text);await self.b.poll(self.ws);self.assertEqual(len(self.rows()),1)
 async def test_traps_off_does_not_activate_unexpected_trap(self):
  await self.start([trap()],enabled=False);self.assertEqual(self.rows(),[])
 async def test_legacy_v25_seed_accepts_existing_trap_delivery(self):
  p=connect();p['slot_data']['bridge_protocol']=25;p['slot_data']['manifest_digest']=M['legacy_v25_digest']
  await self.b.packet(self.ws,p);await self.receive([trap(7)])
  self.assertTrue(self.b.ready);self.assertEqual(self.rows(),[receipt_key(0,trap(7))+'\t7'])
 async def test_legacy_v24_seed_is_accepted_with_traps_off(self):
  p=connect();p['slot_data']['bridge_protocol']=24;p['slot_data']['manifest_digest']=M['legacy_v24_digest'];p['slot_data']['traps']=1
  await self.b.packet(self.ws,p);await self.receive([trap()]);self.assertTrue(self.b.ready);self.assertFalse(self.b.traps_enabled);self.assertEqual(self.rows(),[])
 async def test_recovery_replaced_receipt_is_not_marked_used_by_old_ack(self):
  from history_recovery import HistoryConflict
  await self.start([trap(0)]);self.acknowledge([(0,trap(0))]);await self.b.poll(self.ws);self.b.deactivate()
  await self.b.packet(self.ws,connect())
  with self.assertRaises(HistoryConflict):await self.receive([trap(1)])
  self.assertTrue(self.b.approve_recovery(self.b.recovery['id']))
  await self.b.packet(self.ws,connect());await self.receive([trap(1)])
  self.assertEqual(self.rows(),[receipt_key(0,trap(1))+'\t1']);self.assertEqual(self.b.toast_rows,[])
 async def test_more_than_64_traps_wait_until_first_batch_is_acknowledged(self):
  items=[trap(i%8) for i in range(70)];await self.start(items);self.assertEqual(len(self.rows()),64)
  self.acknowledge(list(enumerate(items[:64])));await self.b.poll(self.ws);self.assertEqual(len(self.rows()),6)

class TrapSocketTests(unittest.IsolatedAsyncioTestCase):
 async def test_actual_websocket_delivery_ack_and_new_client_reconnect(self):
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'mod';mod.mkdir();state=root/'state';history=[trap(7)]
   async def server(ws):
    await ws.send(json.dumps([{'cmd':'RoomInfo','seed_name':'socket-traps'}]));await ws.recv()
    await ws.send(json.dumps([connect(),{'cmd':'ReceivedItems','index':0,'items':history}]))
    try:
     async for raw in ws:
      for p in json.loads(raw):
       if p['cmd']=='Sync':await ws.send(json.dumps([{'cmd':'ReceivedItems','index':0,'items':history}]))
    except websockets.exceptions.ConnectionClosed:pass
   async def ready(b,task):
    for _ in range(150):
     if task.done():await task
     if b.ready:return
     await asyncio.sleep(.01)
    self.fail('No active bridge')
   async with websockets.serve(server,'127.0.0.1',0) as host:
    url='ws://127.0.0.1:'+str(host.sockets[0].getsockname()[1]);b=Bridge(mod,state,'TrapTest')
    task=asyncio.create_task(b.session(url));await ready(b,task)
    (route(mod,'trap-acks-'+b.token+'.txt')).write_text(b.token+'\n'+M['digest']+'\n'+receipt_key(0,trap(7))+'\n')
    task.cancel();await asyncio.gather(task,return_exceptions=True)
    c=Bridge(mod,state,'TrapTest');task=asyncio.create_task(c.session(url));await ready(c,task)
    self.assertEqual(pending_traps(c.state,M['trap_items'],True),[]);self.assertEqual(c.toast_rows,[])
    task.cancel();await asyncio.gather(task,return_exceptions=True)
if __name__=='__main__':unittest.main()
