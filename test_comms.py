from runtime_files import route
import asyncio,base64,json,tempfile,unittest,uuid
from pathlib import Path
from unittest.mock import patch
import websockets
from bridge import Bridge,M,GOAL_LOCATIONS,atomic_text
from medal_rules import locations
from test_multiworld_toasts import Socket,connection

class CommsTests(unittest.IsolatedAsyncioTestCase):
 async def asyncSetUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.mod=self.root/'mod';self.mod.mkdir();self.ws=Socket()
  self.b=Bridge(self.mod,self.root/'state','BNGTest');self.b.seed='comms-test'
  self.b.comms.observe({'cmd':'RoomInfo','seed_name':self.b.seed,'hint_cost':10})
  p=connection();p['hint_points']=24;p['missing_locations']=[row['id'] for row in locations(M,2,True)]
  await self.b.packet(self.ws,p);await self.b.packet(self.ws,{'cmd':'ReceivedItems','index':0,'items':[]})
 async def asyncTearDown(self):self.temp.cleanup()
 def command(self,text,client=None,token=None,request=None):
  request=request or uuid.uuid4().hex
  (route(self.mod,'comms-command.txt')).write_text('\n'.join([client or self.b.client_id,token or self.b.token,request,base64.b64encode(text.encode()).decode()])+'\n')
  return request
 def state(self):
  self.b.comms.publish();return (route(self.mod,'comms-state.txt')).read_text().splitlines()
 def say(self):return [p for p in self.ws.sent if p['cmd']=='Say']
 async def test_explicit_chat_and_hint_requests_use_say_once(self):
  r=self.command('!hint Finale A Entry')
  await self.b.poll(self.ws);await self.b.poll(self.ws)
  self.assertEqual(self.say(),[{'cmd':'Say','text':'!hint Finale A Entry'}])
  self.command('!hint Finale A Entry',request=r);await self.b.poll(self.ws)
  self.assertEqual(len(self.say()),1)
 async def test_stale_client_or_session_never_sends(self):
  self.command('hello',client='0'*32);await self.b.poll(self.ws)
  self.command('hello',token='0'*32);await self.b.poll(self.ws)
  self.assertEqual(self.say(),[])
 async def test_disconnected_command_is_discarded_without_replay(self):
  self.command('hello');self.b.deactivate();await self.b.poll(self.ws)
  self.assertFalse((route(self.mod,'comms-command.txt')).exists());self.assertEqual(self.say(),[])
 async def test_new_connection_cannot_replay_old_request(self):
  self.command('old request')
  self.b.deactivate();await self.b.packet(self.ws,connection());await self.b.packet(self.ws,{'cmd':'ReceivedItems','index':0,'items':[]})
  await self.b.poll(self.ws);self.assertEqual(self.say(),[])
 async def test_rate_limit_does_not_queue_late_commands(self):
  self.command('hello');await self.b.poll(self.ws)
  self.command('second');await self.b.poll(self.ws)
  self.assertEqual(len(self.say()),1);self.assertFalse((route(self.mod,'comms-command.txt')).exists())
 async def test_malformed_and_multiline_commands_do_not_disconnect(self):
  for text in ['hello\n!hint','\x00bad','x'*501,'   ']:
   self.command(text);await self.b.poll(self.ws)
  (route(self.mod,'comms-command.txt')).write_bytes(b'\xff\xfe\xff');await self.b.poll(self.ws)
  (route(self.mod,'comms-command.txt')).write_text('x'*8200);await self.b.poll(self.ws)
  self.assertTrue(self.b.ready);self.assertEqual(self.say(),[])
 async def test_server_cost_and_points_are_displayed_without_spending(self):
  s=self.state();self.assertEqual(s[10:12],['24','12']);self.assertEqual(self.say(),[])
  await self.b.packet(self.ws,{'cmd':'RoomUpdate','hint_points':3,'hint_cost':0})
  self.assertEqual(self.state()[10:12],['3','0'])
 async def test_check_and_finale_counts_are_server_confirmed(self):
  first=next(iter(GOAL_LOCATIONS));(route(self.mod,'checks-'+self.b.token+'.txt')).write_text(next(k for k,v in self.b.checks.items() if v==first)+'\n')
  self.assertEqual(self.state()[6], '0')
  await self.b.packet(self.ws,{'cmd':'RoomUpdate','checked_locations':[first,999999]})
  s=self.state();self.assertEqual(s[6:8],['1','125']);self.assertEqual(s[9],'1')
 async def test_structured_cross_game_names_resolve_without_mutating_packet(self):
  p={'cmd':'PrintJSON','data':[{'type':'player_id','text':'2'},{'text':': '},{'type':'item_id','text':'101','player':2},{'text':' at '},{'type':'location_id','text':'202','player':2}]}
  before=json.dumps(p);await self.b.packet(self.ws,p)
  self.assertIn('Item 101',self.b.comms.render(self.b.comms.logs[-1][1]))
  await self.b.packet(self.ws,{'cmd':'DataPackage','data':{'games':{'Test Adventure':{'item_name_to_id':{'Hookshot':101},'location_name_to_id':{'First Chest':202}}}}})
  self.assertEqual(self.b.comms.render(self.b.comms.logs[-1][1]),'Alex: Hookshot at First Chest');self.assertEqual(json.dumps(p),before)
 async def test_known_hints_include_recipient_and_finder_not_unrelated_hints(self):
  hint={'receiving_player':1,'finding_player':2,'item':M['access_items']['Urban'],'location':202,'found':False}
  other=dict(hint,receiving_player=3,finding_player=2)
  await self.b.packet(self.ws,{'cmd':'Retrieved','keys':{self.b.hint_key:[hint,other]}})
  self.assertEqual(len(self.b.comms.hints),1)
  self.assertIn('Urban Invitational',self.b.comms.hint_text(hint));self.assertIn('Alex',self.b.comms.hint_text(hint))
  self.assertEqual(self.say(),[])
 async def test_log_bounded_and_plain_text(self):
  for i in range(210):await self.b.packet(self.ws,{'cmd':'Print','text':str(i)+'\nhello'})
  self.assertEqual(len(self.b.comms.logs),160)
  self.assertNotIn('\n',self.b.comms.render(self.b.comms.logs[-1][1]))
  s=self.state();self.assertLess(len('\n'.join(s)),1048576)
 async def test_send_failure_consumes_request_and_never_replays_it(self):
  class Fail:
   async def send(self,value):raise ConnectionError('test')
  self.command('hello')
  with self.assertRaises(ConnectionError):await self.b.comms.command(Fail())
  await self.b.comms.command(self.ws);self.assertEqual(self.say(),[])
 async def test_empty_hint_listing_is_explicit_not_automatic(self):
  self.assertEqual(self.say(),[])
  self.command('!hint');await self.b.poll(self.ws);self.assertEqual(self.say(),[{'cmd':'Say','text':'!hint'}])

class CommsSocketTests(unittest.IsolatedAsyncioTestCase):
 async def test_chat_hint_and_server_response_over_websocket(self):
  with tempfile.TemporaryDirectory() as tmp:
   root=Path(tmp);mod=root/'mod';mod.mkdir();messages=[]
   async def server(ws):
    await ws.send(json.dumps([{'cmd':'RoomInfo','seed_name':'comms-socket','hint_cost':10}]))
    async for raw in ws:
     for p in json.loads(raw):
      if p['cmd']=='Connect':await ws.send(json.dumps([connection()]))
      elif p['cmd']=='Sync':await ws.send(json.dumps([{'cmd':'ReceivedItems','index':0,'items':[]}]))
      elif p['cmd']=='Say':
       messages.append(p['text']);await ws.send(json.dumps([{'cmd':'PrintJSON','data':[{'text':'Hint request accepted by test server.'}]}]))
   async with websockets.serve(server,'127.0.0.1',0) as listener:
    b=Bridge(mod,root/'state','BNGTest');task=asyncio.create_task(b.session('ws://127.0.0.1:'+str(listener.sockets[0].getsockname()[1])))
    try:
     async def ready():
      for _ in range(100):
       if task.done():await task
       if b.ready:return
       await asyncio.sleep(.02)
      self.fail('bridge did not connect')
     await ready()
     atomic_text(route(mod,'comms-command.txt'),'\n'.join([b.client_id,b.token,uuid.uuid4().hex,base64.b64encode(b'!hint Finale A Entry').decode()])+'\n')
     for _ in range(100):
      if any('accepted by test' in b.comms.render(v) for k,v in b.comms.logs):break
      if task.done():await task
      await asyncio.sleep(.02)
     self.assertEqual(messages,['!hint Finale A Entry'])
     self.assertTrue(any('accepted by test' in b.comms.render(v) for k,v in b.comms.logs))
    finally:
     task.cancel();await asyncio.gather(task,return_exceptions=True)
