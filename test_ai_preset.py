from runtime_files import route
import tempfile,unittest
from pathlib import Path
from bridge import Bridge,M
from test_multiworld_toasts import Socket,connection

class AiPresetTests(unittest.IsolatedAsyncioTestCase):
 async def asyncSetUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.mod=self.root/'mod';self.mod.mkdir();self.ws=Socket()
 async def asyncTearDown(self):self.temp.cleanup()
 async def start(self,settings):
  b=Bridge(self.mod,self.root/'state','BNGTest');b.seed='ai';p=connection();p['slot_data'].update(settings)
  await b.packet(self.ws,p);await b.packet(self.ws,{'cmd':'ReceivedItems','index':0,'items':[]});return b
 async def test_three_preset_values_reach_validated_rules(self):
  for preset in (0,1,2):
   b=await self.start({'ai_preset':preset});lines=(route(self.mod,'rules-'+b.token+'.txt')).read_text().splitlines()
   self.assertEqual(len(lines),6);self.assertEqual(lines[5],str(preset));self.assertEqual(lines[4],'1');b.deactivate()
 async def test_invalid_or_missing_preset_does_not_activate(self):
  for value in (None,'balanced',True,False,-1,3,[]):
   b=Bridge(self.mod,self.root/'state','BNGTest');b.seed='invalid';p=connection();p['slot_data']['ai_preset']=value
   with self.assertRaisesRegex(ValueError,'Invalid AI preset'):await b.packet(self.ws,p)
   self.assertFalse(b.ready);self.assertFalse((route(self.mod,'active-session.txt')).exists())
 async def test_legacy_rooms_keep_original_ai_and_v28_keeps_barrel_option(self):
  for protocol,key in [(24,'legacy_v24_digest'),(25,'legacy_v25_digest'),(26,'legacy_v26_digest'),(28,'legacy_v28_digest')]:
   b=await self.start({'bridge_protocol':protocol,'manifest_digest':M[key],'ai_preset':2,'barrel_rolls':1})
   self.assertEqual(b.ai_preset,0);self.assertEqual(b.barrel_rolls,protocol==28);b.deactivate()
 def test_manifest_profiles_are_monotonic_and_relaxed_is_no_harder(self):
  order=['Breeze','Urban','Techno','Reflex','Ascension','Descension','Speed Freak','Zen Zeus']
  for preset in ('relaxed','legacy_balanced'):
   rows=M['ai_presets'][preset];levels=[rows[name]['level'] for name in order]
   self.assertEqual(levels,sorted(levels));self.assertTrue(all(0<=x<=2 for x in levels))
   for row in rows.values():self.assertLessEqual(row['min_speed'],row['max_speed']);self.assertLessEqual(row['max_speed'],1)
  for name in order:self.assertLessEqual(M['ai_presets']['relaxed'][name]['level'],M['ai_presets']['legacy_balanced'][name]['level'])

 def test_standard_preserves_early_and_never_boosts_native_settings(self):
  rows=M['ai_presets']['standard']
  for name in ('Breeze','Techno','Urban','Reflex'):
   self.assertIsNone(rows[name]['level_cap']);self.assertEqual(rows[name]['native_speed_factor'],1)
  for name in ('Ascension','Descension'):self.assertEqual(rows[name]['native_speed_factor'],.98)
  self.assertEqual(rows['Zen Zeus']['max_speed'],.95)
 async def test_v29_balanced_maps_to_legacy_runtime_preset(self):
  b=await self.start({'bridge_protocol':29,'manifest_digest':M['legacy_v29_digest'],'ai_preset':2})
  self.assertEqual(b.ai_preset,3)
