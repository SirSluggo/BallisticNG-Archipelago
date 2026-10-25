from runtime_files import route
import tempfile,unittest
from pathlib import Path
from bridge import Bridge,M
class Socket:
 async def send(self,value):pass
class RandomStartBridgeTests(unittest.IsolatedAsyncioTestCase):
 async def test_every_starter_access_counts_reconnect_and_gtek_receipt(self):
  for ship in M['starting_ship_candidates']:
   with self.subTest(ship=ship),tempfile.TemporaryDirectory() as t:
    path=Path(t);mod=path/'mod';mod.mkdir()
    packet={'cmd':'Connected','team':0,'slot':1,'checked_locations':[], 'slot_data':{
     'bridge_protocol':30,'ai_preset':2,'barrel_rolls':1,'manifest_digest':M['digest'],'goal_keys':M['goal_keys'],
     'required_medal':2,'survival_milestones':1,'starting_ship':ship,'starting_track':M['starting_track']}}
    b=Bridge(mod,path/'state','BNGTest');b.seed='test-seed'
    await b.packet(Socket(),packet)
    await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[]})
    lines=(route(mod,'access-'+b.token+'.txt')).read_text().splitlines()
    self.assertIn('Ship:'+ship,lines);self.assertIn(next(v.get('gate',v['name']) for v in M['track_items'].values() if v['name']==M['starting_track']),lines)
    self.assertEqual('Ship:Gtek' in lines,ship=='Gtek')
    activity=(route(mod,'activity-'+b.token+'.txt')).read_text()
    self.assertIn('campaign\tBreeze\t'+str(5 if ship in ('Gtek','Hyperion') else 4)+'\t0',activity)
    await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[{'item':M['ship_items']['Gtek']['id'],'location':1,'player':1,'flags':1}]})
    self.assertIn('Ship:Gtek',(route(mod,'access-'+b.token+'.txt')).read_text().splitlines())
    c=Bridge(mod,path/'state','BNGTest');c.seed='test-seed'
    await c.packet(Socket(),packet)
    await c.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':b.state['items']})
    self.assertIn('Ship:Gtek',(route(mod,'access-'+c.token+'.txt')).read_text().splitlines())
 async def test_invalid_starter_cannot_activate_session(self):
  with tempfile.TemporaryDirectory() as t:
   p=Path(t);b=Bridge(p,p/'state','BNGTest');b.seed='bad'
   with self.assertRaises(ValueError):
    await b.packet(Socket(),{'cmd':'Connected','team':0,'slot':1,'slot_data':{'bridge_protocol':30,'ai_preset':2,'barrel_rolls':1,'manifest_digest':M['digest'],'goal_keys':M['goal_keys'],'starting_ship':'Zoneship','starting_track':M['starting_track']}})
   self.assertFalse((route(p,'active-session.txt')).exists())
if __name__=='__main__':unittest.main()
