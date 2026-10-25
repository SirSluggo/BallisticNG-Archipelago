from runtime_files import route
import base64,tempfile,unittest
from pathlib import Path
from bridge import Bridge,M
from test_random_start import Socket
class ToastTests(unittest.IsolatedAsyncioTestCase):
 async def test_baseline_new_receipts_flags_replay_and_reconnect(self):
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'mod';mod.mkdir()
   connected={'cmd':'Connected','team':0,'slot':1,'checked_locations':[], 'slot_data':{'bridge_protocol':30,'ai_preset':2,'barrel_rolls':1,'manifest_digest':M['digest'],'goal_keys':M['goal_keys'],'required_medal':2,'survival_milestones':1,'starting_ship':'Caliburn','starting_track':M['starting_track']}}
   b=Bridge(mod,root/'state','BNGTest');b.seed='toast-test';await b.packet(Socket(),connected)
   def rows(client):return [line.split('\t') for line in (route(mod,'toasts-'+client.token+'.txt')).read_text().splitlines()[2:]]
   old={'item':M['ship_items']['Caliburn']['id'],'location':-2,'player':1,'flags':0}
   await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[old]})
   self.assertEqual(rows(b),[])
   progression={'item':M['access_items']['Urban'],'location':21591000,'player':1,'flags':1}
   cosmetic=M['cosmetic_items']['Caliburn:Stealth']
   filler={'item':cosmetic['id'],'location':21591001,'player':1,'flags':0}
   await b.packet(Socket(),{'cmd':'ReceivedItems','index':1,'items':[progression,filler]})
   records=rows(b)
   self.assertEqual([r[:2] for r in records],[['1','1'],['2','0']])
   self.assertEqual([base64.b64decode(r[3]).decode() for r in records],['Urban Invitational',cosmetic['name']])
   await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[old,progression,filler]})
   self.assertEqual(rows(b),records)
   c=Bridge(mod,root/'state','BNGTest');c.seed='toast-test';await c.packet(Socket(),connected)
   await c.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[old,progression,filler]})
   self.assertEqual(rows(c),[])
   await c.packet(Socket(),{'cmd':'ReceivedItems','index':3,'items':[{'item':M['filler_id'],'location':21591002,'player':1,'flags':0}]})
   self.assertEqual(len(rows(c)),1)
   await c.packet(Socket(),{'cmd':'ReceivedItems','index':4,'items':[{'item':M['access_items']['Techno'],'location':-1,'player':1,'flags':0}]})
   self.assertEqual(rows(c)[-1][1],'1')
 async def test_bursts_keep_only_latest_24_notifications(self):
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'mod';mod.mkdir();b=Bridge(mod,root/'state','BNGTest');b.seed='burst'
   await b.packet(Socket(),{'cmd':'Connected','team':0,'slot':1,'checked_locations':[], 'slot_data':{'bridge_protocol':30,'ai_preset':2,'barrel_rolls':1,'manifest_digest':M['digest'],'goal_keys':M['goal_keys'],'required_medal':3,'survival_milestones':1,'starting_ship':'Caliburn','starting_track':M['starting_track']}})
   await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[]})
   await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[{'item':M['filler_id'],'location':n,'player':1,'flags':0} for n in range(30)]})
   self.assertEqual(len(b.toast_rows),24);self.assertEqual(b.toast_rows[0][0],7);self.assertEqual(b.toast_rows[-1][0],30)
if __name__=='__main__':unittest.main()
