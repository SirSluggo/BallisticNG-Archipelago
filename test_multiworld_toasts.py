from runtime_files import route
import base64,json,tempfile,unittest
from pathlib import Path
from bridge import Bridge,M
class Socket:
 def __init__(self):self.sent=[]
 async def send(self,value):self.sent.extend(json.loads(value))
def connection(checked=()):
 return {'cmd':'Connected','team':0,'slot':1,'checked_locations':list(checked),
  'players':[{'team':0,'slot':1,'alias':'Phil','name':'BNGTest'},{'team':0,'slot':2,'alias':'Alex','name':'AdventureTest'}],
  'slot_info':{'1':{'name':'BNGTest','game':'BallisticNG','type':1},'2':{'name':'AdventureTest','game':'Test Adventure','type':1}},
  'slot_data':{'bridge_protocol':30,'ai_preset':2,'barrel_rolls':1,'manifest_digest':M['digest'],'goal_keys':M['goal_keys'],'required_medal':2,'survival_milestones':1,'starting_ship':'Caliburn','starting_track':M['starting_track']}}
def send_packet(location=21591001,recipient=2,source=1,item=None,flags=1):
 return {'cmd':'PrintJSON','type':'ItemSend','receiving':recipient,'item':{'item':M['access_items']['Urban'] if item is None else item,'location':location,'player':source,'flags':flags}}
class MultiworldToastTests(unittest.IsolatedAsyncioTestCase):
 async def asyncSetUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.mod=self.root/'mod';self.mod.mkdir();self.ws=Socket()
  self.b=Bridge(self.mod,self.root/'state','BNGTest');self.b.seed='multiworld-test'
  await self.b.packet(self.ws,connection());await self.b.packet(self.ws,{'cmd':'ReceivedItems','index':0,'items':[]})
 async def asyncTearDown(self):self.temp.cleanup()
 def notices(self,client=None):
  b=client or self.b
  lines=(route(self.mod,'toasts-'+b.token+'.txt')).read_text().splitlines()[2:]
  return [(int(p[1]),base64.b64decode(p[3]).decode(),p[4],base64.b64decode(p[5]).decode()) for line in lines if (p:=line.split('\t'))]
 async def package(self):
  await self.b.packet(self.ws,{'cmd':'DataPackage','data':{'games':{'Test Adventure':{'item_name_to_id':{'Hookshot':M['access_items']['Urban'],'Rupees':M['access_items']['Reflex']}}}}})
 async def test_requests_each_game_package_separately(self):
  packet=connection();packet['slot_info']['3']={'name':'Third','game':'Third Game','type':1}
  client=Bridge(self.mod,self.root/'state','BNGTest');client.seed='many-games';socket=Socket()
  await client.packet(socket,packet)
  requests=[p for p in socket.sent if p['cmd']=='GetDataPackage']
  self.assertEqual(requests,[{'cmd':'GetDataPackage','games':['Test Adventure']},{'cmd':'GetDataPackage','games':['Third Game']}])
 async def test_incoming_remote_and_local_acquisition_not_double_reported(self):
  remote={'item':M['access_items']['Urban'],'location':123,'player':2,'flags':1}
  await self.b.packet(self.ws,{'cmd':'ReceivedItems','index':0,'items':[remote]})
  await self.b.packet(self.ws,send_packet(123,recipient=1,source=2))
  self.assertEqual(self.notices(),[(1,'Urban Invitational','RECEIVED','Alex')])
  cosmetic=M['cosmetic_items']['Caliburn:Alt']
  own={'item':cosmetic['id'],'location':21591000,'player':1,'flags':0}
  await self.b.packet(self.ws,{'cmd':'ReceivedItems','index':1,'items':[own]})
  await self.b.packet(self.ws,send_packet(21591000,recipient=1,item=cosmetic['id'],flags=0))
  self.assertEqual(self.notices()[-1],(0,cosmetic['name'],'ACQUIRED',''))
  self.assertEqual(len(self.notices()),2)
 async def test_outgoing_waits_for_correct_game_package_with_colliding_item_ids(self):
  self.assertIn({'cmd':'GetDataPackage','games':['Test Adventure']},self.ws.sent)
  packet=send_packet()
  await self.b.packet(self.ws,{'cmd':'RoomUpdate','checked_locations':[21591001]})
  await self.b.packet(self.ws,packet)
  self.assertEqual(self.notices(),[])
  await self.package()
  self.assertEqual(self.notices(),[(1,'Hookshot','SENT','Alex')])
  await self.b.packet(self.ws,packet);self.assertEqual(len(self.notices()),1)
  await self.b.packet(self.ws,send_packet(21591002,item=M['access_items']['Reflex'],flags=0))
  self.assertEqual(self.notices()[-1],(0,'Rupees','SENT','Alex'))
 async def test_alias_updates_and_unrelated_player_messages(self):
  await self.package()
  await self.b.packet(self.ws,{'cmd':'RoomUpdate','players':[{'team':1,'slot':2,'alias':'Wrong Team','name':'Other'},{'team':0,'slot':2,'alias':'Alex New','name':'AdventureTest'}]})
  await self.b.packet(self.ws,send_packet())
  self.assertEqual(self.notices()[-1][-1],'Alex New')
  await self.b.packet(self.ws,send_packet(21591002,source=2,recipient=3))
  wrong_team=send_packet(21591003);wrong_team['team']=1
  await self.b.packet(self.ws,wrong_team)
  self.assertEqual(len(self.notices()),1)
 async def test_reconnect_does_not_replay_outgoing_receipts(self):
  await self.package();packet=send_packet();await self.b.packet(self.ws,packet)
  c=Bridge(self.mod,self.root/'state','BNGTest');c.seed='multiworld-test';await c.packet(self.ws,connection())
  await c.packet(self.ws,{'cmd':'ReceivedItems','index':0,'items':[]})
  await c.packet(self.ws,{'cmd':'DataPackage','data':{'games':{'Test Adventure':{'item_name_to_id':{'Hookshot':M['access_items']['Urban']}}}}})
  await c.packet(self.ws,packet);self.assertEqual(self.notices(c),[])
 async def test_already_checked_at_connect_is_not_an_outgoing_notification(self):
  c=Bridge(self.mod,self.root/'state','BNGTest');c.seed='baseline-test';await c.packet(self.ws,connection([21591001]))
  await c.packet(self.ws,{'cmd':'ReceivedItems','index':0,'items':[]})
  await c.packet(self.ws,send_packet());self.assertEqual(c.pending_sends,[])
  self.assertEqual(self.notices(c),[])

class SocketTransportTests(unittest.IsolatedAsyncioTestCase):
 async def test_real_websocket_transport_outgoing_metadata_and_incoming_receipt(self):
  import asyncio,websockets
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'mod';mod.mkdir();bridge=Bridge(mod,root/'state','BNGTest')
   result=asyncio.get_running_loop().create_future()
   async def server(ws):
    try:
     await ws.send(json.dumps([{'cmd':'RoomInfo','seed_name':'socket-test'}]))
     connect=json.loads(await ws.recv())[0];self.assertEqual(connect['cmd'],'Connect')
     await ws.send(json.dumps([connection()]))
     requests=[]
     for _ in range(4):requests.extend(json.loads(await ws.recv()))
     self.assertIn({'cmd':'GetDataPackage','games':['Test Adventure']},requests)
     await ws.send(json.dumps([
      {'cmd':'ReceivedItems','index':0,'items':[]},
      send_packet(),
      {'cmd':'DataPackage','data':{'games':{'Test Adventure':{'item_name_to_id':{'Hookshot':M['access_items']['Urban']}}}}},
      {'cmd':'ReceivedItems','index':0,'items':[{'item':M['access_items']['Urban'],'location':99,'player':2,'flags':1}]},
      send_packet()]))
     records=[]
     for _ in range(100):
      path=route(mod,'toasts-'+str(bridge.token)+'.txt')
      if path.exists():records=[line.split('\t') for line in path.read_text().splitlines()[2:]]
      if len(records)==2:break
      await asyncio.sleep(.01)
     self.assertEqual([(base64.b64decode(r[3]).decode(),r[4],base64.b64decode(r[5]).decode()) for r in records],
                      [('Hookshot','SENT','Alex'),('Urban Invitational','RECEIVED','Alex')])
     result.set_result(True)
    except Exception as ex:
     if not result.done():result.set_exception(ex)
   async with websockets.serve(server,'127.0.0.1',0) as host:
    port=host.sockets[0].getsockname()[1]
    task=asyncio.create_task(bridge.session('ws://127.0.0.1:'+str(port)))
    try:await asyncio.wait_for(result,5)
    finally:
     task.cancel();await asyncio.gather(task,return_exceptions=True)

if __name__=='__main__':unittest.main()
