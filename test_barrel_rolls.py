from runtime_files import route
import tempfile,unittest
from pathlib import Path
from bridge import Bridge,M
from test_multiworld_toasts import Socket,connection

class BarrelRollSettingTests(unittest.IsolatedAsyncioTestCase):
 async def asyncSetUp(self):
  self.temp=tempfile.TemporaryDirectory();self.root=Path(self.temp.name);self.mod=self.root/'mod';self.mod.mkdir();self.ws=Socket()
 async def asyncTearDown(self):self.temp.cleanup()
 async def connect(self,data):
  b=Bridge(self.mod,self.root/'state','BNGTest');b.seed='barrel-test';p=connection();p['slot_data'].update(data)
  await b.packet(self.ws,p);await b.packet(self.ws,{'cmd':'ReceivedItems','index':0,'items':[]});return b
 async def test_enabled_setting_reaches_session_rules(self):
  b=await self.connect({'barrel_rolls':1});rules=(route(self.mod,'rules-'+b.token+'.txt')).read_text().splitlines()
  self.assertEqual(rules,[b.token,M['digest'],'2','1','1','2']);self.assertTrue(b.barrel_rolls)
 async def test_disabled_setting_reaches_session_rules(self):
  b=await self.connect({'barrel_rolls':0});self.assertEqual((route(self.mod,'rules-'+b.token+'.txt')).read_text().splitlines()[4],'0');self.assertFalse(b.barrel_rolls)
 async def test_supported_legacy_rooms_preserve_rolls_off(self):
  for protocol,key in [(24,'legacy_v24_digest'),(25,'legacy_v25_digest'),(26,'legacy_v26_digest')]:
   b=await self.connect({'bridge_protocol':protocol,'manifest_digest':M[key],'barrel_rolls':1})
   self.assertTrue(b.ready);self.assertFalse(b.barrel_rolls);b.deactivate()
 async def test_invalid_new_settings_are_rejected_before_activation(self):
  for value in [None,'true',2,-1,[],{}]:
   b=Bridge(self.mod,self.root/'state','BNGTest');b.seed='bad';p=connection();p['slot_data']['barrel_rolls']=value
   with self.assertRaisesRegex(ValueError,'Invalid barrel roll setting'):await b.packet(self.ws,p)
   self.assertFalse(b.ready);self.assertFalse((route(self.mod,'active-session.txt')).exists())
 async def test_old_protocol_with_new_digest_is_rejected(self):
  b=Bridge(self.mod,self.root/'state','BNGTest');b.seed='bad';p=connection();p['slot_data']['bridge_protocol']=26
  with self.assertRaisesRegex(ValueError,'Wrong game/catalog'):await b.packet(self.ws,p)
