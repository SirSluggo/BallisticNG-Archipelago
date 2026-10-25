from runtime_files import route
import tempfile,unittest
from pathlib import Path
from bridge import Bridge,M
from event_activity import event_remaining,activity,optional_only_events
from medal_rules import locations
from test_random_start import Socket
class MenuActivityTests(unittest.TestCase):
 def test_survival_stays_uncleared_until_every_server_check_is_present(self):
  event=next(e for e in M['events'] if e['event']=='Cassandra Survival')
  for medal in (1,2,3):
   rows=[r for r in locations(M,medal,True) if r['event_key']==event['key']]
   checked=set()
   for expected,row in zip((3,2,1),rows):
    self.assertEqual(event_remaining(M,checked,medal)[event['key']],expected)
    checked.add(row['id'])
   self.assertEqual(event_remaining(M,checked,medal)[event['key']],0)
 def test_sage_requires_main_clear_and_only_above_target_checks_remaining(self):
  event=next(e for e in M['events'] if e['event']=='Cassandra Survival')
  key=event['key']
  for medal in (1,2,3):
   rows={r['medal']:r for r in locations(M,medal,True) if r['event_key']==key}
   self.assertNotIn(key,optional_only_events(M,set(),medal))
   # All checks at/below the selected medal have been acknowledged.
   required={r['id'] for tier,r in rows.items() if tier<=medal}
   self.assertEqual(key in optional_only_events(M,required,medal),medal<3)
   self.assertNotIn(key,optional_only_events(M,{r['id'] for r in rows.values()},medal))
   # A missing lower check cannot be mistaken for an optional-only remainder.
   if medal>1:self.assertNotIn(key,optional_only_events(M,{event['id']},medal))
  regular=next(e for e in M['events'] if e['event']=='Cassandra Introduction Race')
  self.assertNotIn(regular['key'],optional_only_events(M,{regular['id']},2))
 def test_ready_count_and_hint_stop_after_acknowledged_completion(self):
  first=next(e for e in M['events'] if e['event']=='Cassandra Introduction Race')
  owned={M['ship_items']['Caliburn']['id'],next(v['id'] for v in M['track_items'].values() if v['name']==M['starting_track'])}
  self.assertEqual(activity(M,owned,set(),{first['id']},2,True)['Breeze'],[4,True])
  self.assertEqual(activity(M,owned,{first['id']},{first['id']},2,True)['Breeze'],[3,False])
class MenuPublicationTests(unittest.IsolatedAsyncioTestCase):
 async def test_bridge_publishes_all_event_states_and_updates_on_server_ack(self):
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'mod';mod.mkdir();b=Bridge(mod,root/'state','BNGTest');b.seed='menu-test'
   p={'cmd':'Connected','team':0,'slot':1,'checked_locations':[], 'slot_data':{'bridge_protocol':30,'ai_preset':2,'barrel_rolls':1,'manifest_digest':M['digest'],'goal_keys':M['goal_keys'],'required_medal':2,'survival_milestones':1,'starting_ship':'Caliburn','starting_track':M['starting_track']}}
   await b.packet(Socket(),p)
   await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[]})
   path=route(mod,'activity-'+b.token+'.txt')
   def states():return {a[1]:int(a[2]) for line in path.read_text().splitlines() if (a:=line.split('\t'))[0]=='event'}
   self.assertEqual(len(states()),101)
   event=next(e for e in M['events'] if e['event']=='Cassandra Introduction Race')
   self.assertEqual(states()[event['key']],1)
   b.state['pending']=[event['id']];b.publish_activity()
   self.assertEqual(states()[event['key']],1)
   await b.packet(Socket(),{'cmd':'RoomUpdate','checked_locations':[event['id']]})
   self.assertEqual(states()[event['key']],0)
   survival=next(e for e in M['events'] if e['event']=='Cassandra Survival')
   checked=[r['id'] for r in locations(M,2,True) if r['event_key']==survival['key'] and r['medal']<=2]
   await b.packet(Socket(),{'cmd':'RoomUpdate','checked_locations':checked})
   self.assertIn('event\t'+survival['key']+'\t1\t1',path.read_text())
if __name__=='__main__':unittest.main()
