from runtime_files import route
import json,tempfile,unittest,shutil
from pathlib import Path
from isolated_profiles import Profiles,ProfileError,tree_inventory
from save_profiles import profile_key

class IsolatedProfilesTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.game=Path(self.tmp.name)/'game';self.mod=self.game/'User/Mods/Code Mods/BallisticNGArchipelago';self.mod.mkdir(parents=True)
  self.save=self.game/'User/Save';(self.save/'Campaign').mkdir(parents=True);(self.save/'Campaign/Breeze.sav').write_bytes(b'normal progress');(self.save/'Ghosts').mkdir()
  self.legacy=self.game/'User/ArchipelagoProfiles';self.legacy.mkdir();(self.legacy/'active-profile.txt').write_text('normal')
  self.profiles=Profiles(self.game,closed=lambda:None)
  self.identity=['seed-one',0,1];self.request(self.identity)
 def tearDown(self):self.tmp.cleanup()
 def request(self,identity):
  self.identity=identity;key=profile_key(identity);token='a'*32
  (route(self.mod,'active-session.txt')).write_text(token);(route(self.mod,'bridge-heartbeat.txt')).write_text('live')
  (route(self.mod,'profile-request.json')).write_text(json.dumps({'schema':1,'token':token,'key':key,'identity':identity,'slot_name':'Tester'}));return key
 def test_ap_and_normal_selection_never_move_original_files(self):
  before=tree_inventory(self.save);key=profile_key(self.identity);self.profiles.run('ap',self.mod,True)
  self.assertEqual(self.profiles.current(),key);self.assertEqual(tree_inventory(self.save),before)
  slot=self.profiles.root/'slots'/key/'Save';self.assertEqual((slot/'Campaign').is_dir(),True);self.assertFalse((slot/'Campaign/Breeze.sav').exists())
  (slot/'Campaign/Breeze.sav').write_bytes(b'AP progress');self.profiles.run('normal',self.mod)
  self.assertEqual(self.profiles.current(),'normal');self.assertEqual(tree_inventory(self.save),before);self.assertEqual((slot/'Campaign/Breeze.sav').read_bytes(),b'AP progress');self.assertTrue(self.profiles.verify_normal())
 def test_two_seed_selections_retain_inactive_profile(self):
  self.profiles.run('ap',self.mod,True);a=self.profiles.root/'slots'/profile_key(self.identity)/'Save';(a/'Campaign/a.sav').write_bytes(b'A')
  b=self.request(['seed-two',0,1]);self.profiles.run('ap',self.mod);self.assertEqual((a/'Campaign/a.sav').read_bytes(),b'A');self.assertTrue((self.profiles.root/'slots'/b/'Save').exists())
 def test_incomplete_profile_not_recreated(self):
  self.profiles.run('ap',self.mod,True);key=self.profiles.current();shutil.rmtree(self.profiles.root/'slots'/key/'Save')
  with self.assertRaisesRegex(ProfileError,'incomplete'):self.profiles.run('ap',self.mod)
  self.assertFalse((self.profiles.root/'slots'/key/'Save').exists())
 def test_missing_and_invalid_selection_refused_after_ap_creation(self):
  self.profiles.run('ap',self.mod,True);self.profiles.state.unlink()
  with self.assertRaisesRegex(ProfileError,'missing'):self.profiles.current()
  self.profiles.state.write_text('../Save')
  with self.assertRaisesRegex(ProfileError,'Invalid'):self.profiles.current()
 def test_legacy_ap_profile_blocks_new_backend(self):
  key='b'*64;(self.legacy/'active-profile.txt').write_text(key);(self.save/'.bng-ap-profile').write_text(key)
  with self.assertRaisesRegex(ProfileError,'Legacy AP'):self.profiles.run('ap',self.mod,True)
  self.assertFalse(self.profiles.state.exists())
 def test_normal_mutation_blocks_return_and_preserves_selection(self):
  self.profiles.run('ap',self.mod,True);key=self.profiles.current();(self.save/'Campaign/Breeze.sav').write_bytes(b'changed')
  with self.assertRaisesRegex(ProfileError,'Normal save changed'):self.profiles.run('normal',self.mod)
  self.assertEqual(self.profiles.current(),key)
 def test_identity_mismatch_blocks_activation(self):
  self.profiles.run('ap',self.mod,True);key=self.profiles.current();(self.profiles.root/'slots'/key/'identity.json').write_text('[]')
  with self.assertRaisesRegex(ProfileError,'identity mismatch'):self.profiles.run('ap',self.mod)
 def test_running_game_blocks_all_selection_writes(self):
  def running():raise ProfileError('game running')
  self.profiles.closed=running
  with self.assertRaisesRegex(ProfileError,'game running'):self.profiles.run('ap',self.mod,True)
  self.assertFalse(self.profiles.state.exists())
 def test_room_change_during_preparation_keeps_normal_selection(self):
  original=self.profiles.request;calls=[]
  def changing(mod):
   calls.append(1)
   if len(calls)==2:self.request(['changed-seed',0,1])
   return original(mod)
  self.profiles.request=changing
  before=tree_inventory(self.save)
  with self.assertRaisesRegex(ProfileError,'Connected seed changed'):self.profiles.run('ap',self.mod,True)
  self.assertEqual(self.profiles.current(),'normal');self.assertEqual(tree_inventory(self.save),before)
 def test_no_cloud_disabled_ack_required(self):
  self.assertFalse((self.legacy/'cloud-disabled-ack.txt').exists())
  self.profiles.run('ap',self.mod)
  self.assertEqual(self.profiles.current(),profile_key(self.identity))
  self.assertFalse((self.legacy/'cloud-disabled-ack.txt').exists())

if __name__=='__main__':unittest.main()
