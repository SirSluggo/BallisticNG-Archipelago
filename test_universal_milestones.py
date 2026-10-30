import copy,json,tempfile,unittest
from pathlib import Path
from bridge import Bridge,M,GOAL_LOCATIONS
from medal_rules import locations,LOCATION_SCHEMA,ASSIST_ID
from event_activity import event_remaining,optional_only_events
from runtime_files import route
from test_multiworld_toasts import connection,Socket

def universal(medal=2):
 p=connection();p['slot_data'].update(bridge_protocol=31,location_schema=LOCATION_SCHEMA,required_medal=medal,golden_race_passes=1,passes_in_finales=0,passes_in_survival=0)
 return p

class MilestoneCatalogTests(unittest.TestCase):
 def test_three_per_event_and_ids_disjoint_from_legacy(self):
  old={r['id'] for r in locations(M,3,True)}
  for medal in (1,2,3):
   rows=locations(M,medal,True,True)
   self.assertEqual(len(rows),303);self.assertEqual(len({r['id'] for r in rows}),303)
   self.assertFalse(old & {r['id'] for r in rows})
   for event in M['events']:self.assertEqual([r['medal'] for r in rows if r['event_key']==event['key']],[1,2,3])
  changed=copy.deepcopy(M);changed['events'].reverse()
  self.assertEqual({r['key']:r['id'] for r in rows},{r['key']:r['id'] for r in locations(changed,3,True,True)})
 def test_optional_ui_requires_all_target_tiers_checked(self):
  rows=locations(M,1,True,True);e=M['events'][0]['key'];bronze=next(r['id'] for r in rows if r['event_key']==e and r['medal']==1)
  self.assertNotIn(e,optional_only_events(M,set(),1,True,True))
  self.assertIn(e,optional_only_events(M,{bronze},1,True,True))
  self.assertEqual(event_remaining(M,{bronze},1,True,True)[e],2)

class MilestoneProtocolTests(unittest.IsolatedAsyncioTestCase):
 async def test_new_schema_inventory_and_gold_goal_confirmation(self):
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'mod';mod.mkdir();ws=Socket();b=Bridge(mod,root/'history','Test');b.seed='fresh'
   await b.packet(ws,universal());await b.packet(ws,{'cmd':'ReceivedItems','index':0,'items':[]})
   self.assertEqual(len(b.locations),303);self.assertNotEqual(b.goal_locations,GOAL_LOCATIONS)
   self.assertEqual(len(b.goal_locations),3)
   self.assertEqual((route(mod,'rules-'+b.token+'.txt')).read_text().splitlines()[-4:],[LOCATION_SCHEMA,'1','0','0'])
   item={'item':ASSIST_ID,'location':101,'player':2,'flags':0}
   await b.packet(ws,{'cmd':'ReceivedItems','index':0,'items':[item,item]})
   await b.packet(ws,{'cmd':'ReceivedItems','index':0,'items':[item,item]})
   self.assertEqual(route(mod,'consumables-'+b.token+'.txt').read_text().splitlines()[-1],'2')
   b.checked.update(GOAL_LOCATIONS);await b.poll(ws)
   self.assertFalse(any(p['cmd']=='StatusUpdate' for p in ws.sent))
   b.checked.update(b.goal_locations);await b.poll(ws)
   self.assertEqual(len([p for p in ws.sent if p['cmd']=='StatusUpdate']),1)
   b.deactivate()
   reconnect=Bridge(mod,root/'history','Test');reconnect.seed='fresh'
   await reconnect.packet(ws,universal());await reconnect.packet(ws,{'cmd':'ReceivedItems','index':0,'items':[item,item]})
   self.assertEqual(route(mod,'consumables-'+reconnect.token+'.txt').read_text().splitlines()[-1],'2');reconnect.deactivate()
 async def test_old_seed_stays_125_and_schema_cannot_change_in_place(self):
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'mod';mod.mkdir();ws=Socket();b=Bridge(mod,root/'history','Test');b.seed='old'
   await b.packet(ws,connection());await b.packet(ws,{'cmd':'ReceivedItems','index':0,'items':[]})
   self.assertEqual(len(b.locations),125);self.assertEqual(len(route(mod,'rules-'+b.token+'.txt').read_text().splitlines()),6)
   b.deactivate();b=Bridge(mod,root/'history','Test');b.seed='old'
   with self.assertRaisesRegex(ValueError,'schema mismatch'):await b.packet(ws,universal())
 async def test_schema_must_be_explicit(self):
  with tempfile.TemporaryDirectory() as t:
   mod=Path(t)/'mod';mod.mkdir();b=Bridge(mod,Path(t)/'history','Test');b.seed='invalid';p=universal();p['slot_data'].pop('location_schema')
   with self.assertRaisesRegex(ValueError,'schema'):await b.packet(Socket(),p)
