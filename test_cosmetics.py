from runtime_files import route
import tempfile,unittest
from pathlib import Path
from bridge import Bridge,M,ITEM_NAMES
from test_random_start import Socket
class CosmeticTests(unittest.IsolatedAsyncioTestCase):
 async def test_cosmetics_before_ship_replay_and_reconnect(self):
  with tempfile.TemporaryDirectory() as t:
   root=Path(t);mod=root/'mod';mod.mkdir()
   packet={'cmd':'Connected','team':0,'slot':1,'checked_locations':[], 'slot_data':{
    'bridge_protocol':30,'ai_preset':2,'barrel_rolls':1,'manifest_digest':M['digest'],'goal_keys':M['goal_keys'],'required_medal':2,
    'survival_milestones':1,'starting_ship':'Caliburn','starting_track':M['starting_track']}}
   b=Bridge(mod,root/'state','BNGTest');b.seed='cosmetics'
   await b.packet(Socket(),packet)
   await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':[]})
   def access(client):return set((route(mod,'access-'+client.token+'.txt')).read_text().splitlines())
   self.assertNotIn('Ship:Orbitronix',access(b))
   items=[{'item':v['id'],'location':-1,'player':1,'flags':0} for v in M['cosmetic_items'].values()]
   await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':items})
   self.assertEqual({x for x in access(b) if x.startswith('Cosmetic:')},{'Cosmetic:'+key for key in M['cosmetic_items']})
   self.assertNotIn('Ship:Orbitronix',access(b))
   await b.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':items})
   self.assertEqual(len(b.state['items']),67)
   c=Bridge(mod,root/'state','BNGTest');c.seed='cosmetics';await c.packet(Socket(),packet)
   await c.packet(Socket(),{'cmd':'ReceivedItems','index':0,'items':items})
   self.assertIn('Cosmetic:Orbitronix:Stealth',access(c))
   self.assertNotIn('Ship:Orbitronix',access(c))
   await c.packet(Socket(),{'cmd':'ReceivedItems','index':67,'items':[{'item':M['ship_items']['Orbitronix']['id'],'location':-1,'player':1,'flags':0}]})
   self.assertIn('Ship:Orbitronix',access(c));self.assertIn('Cosmetic:Orbitronix:Stealth',access(c))
 def test_new_unlock_labels_distinguish_speed_campaign_and_finale(self):
  self.assertEqual(ITEM_NAMES[M['access_items']['Zen Zeus']],'Zen Zeus Invitational')
  self.assertEqual(ITEM_NAMES[M['access_items']['Finale A']],'Finale A Entry')
  self.assertIn('Zen Speed License',ITEM_NAMES.values())
  self.assertIn('Harpstone Track Permit',ITEM_NAMES.values())
  self.assertNotIn('Zen Speed Access',ITEM_NAMES.values())
  self.assertNotIn('Zen Zeus Access',ITEM_NAMES.values())
 def test_pool_appearance_catalog_has_only_approved_base_liveries(self):
  self.assertEqual(len(M['cosmetic_items']),67)
  self.assertNotIn('Zoneship:Alt',M['cosmetic_items'])
  self.assertEqual({v['livery'] for v in M['cosmetic_items'].values()},{'Alt','Neon','Clean','Stealth'})
  self.assertEqual(len({v['id'] for v in M['cosmetic_items'].values()}),67)
  for value in M['cosmetic_items'].values():self.assertEqual(ITEM_NAMES[value['id']],value['name'])
if __name__=='__main__':unittest.main()
