import json,tempfile,unittest,hashlib
from pathlib import Path
from unittest.mock import patch
from runtime_files import route,rotate
from maintenance import migrate_layout,cleanup_sessions
from save_profiles import profile_key
from trap_delivery import receipt_key
M=json.loads(Path('manifest.json').read_text())
class LayoutTests(unittest.TestCase):
 def test_files_go_to_expected_folders_and_unknown_stays_root(self):
  with tempfile.TemporaryDirectory() as t:
   p=Path(t)
   for name,folder in [('panel-settings.txt','Data'),('checks-'+32*'a'+'.txt','Data'),('events.tsv','Logs'),('ship-catalog.tsv','Cache')]:self.assertEqual(route(p,name),p/folder/name)
   self.assertEqual(route(p,'bridge.lock'),p/'bridge.lock')
 def test_migration_preserves_bytes_and_unknown_files(self):
  with tempfile.TemporaryDirectory() as t:
   p=Path(t);(p/'panel-settings.txt').write_bytes(b'host\nport\nslot');(p/'events.tsv').write_bytes(b'events');(p/'unrelated.txt').write_bytes(b'keep')
   self.assertEqual(migrate_layout(p),2);self.assertFalse((p/'panel-settings.txt').exists());self.assertEqual(route(p,'panel-settings.txt').read_bytes(),b'host\nport\nslot');self.assertEqual((p/'unrelated.txt').read_bytes(),b'keep')
 def test_conflicting_settings_stop_before_moving_any_file(self):
  with tempfile.TemporaryDirectory() as t:
   p=Path(t);(p/'panel-settings.txt').write_text('old');route(p,'panel-settings.txt').write_text('new');(p/'events.tsv').write_text('keep')
   with self.assertRaises(OSError):migrate_layout(p)
   self.assertEqual((p/'events.tsv').read_text(),'keep')
 def test_log_conflict_preserves_both_versions(self):
  with tempfile.TemporaryDirectory() as t:
   p=Path(t);(p/'events.tsv').write_text('old');route(p,'events.tsv').write_text('new');migrate_layout(p)
   self.assertEqual(route(p,'events.tsv').read_text(),'new');self.assertEqual(next((p/'Logs/Imported').rglob('events.tsv')).read_text(),'old')
 def test_rotation_keeps_three_backups(self):
  with tempfile.TemporaryDirectory() as t:
   p=Path(t)/'events.tsv'
   for i in range(5):p.write_text(str(i));rotate(p,limit=1)
   self.assertEqual(p.with_name(p.name+'.1').read_text(),'4');self.assertEqual(p.with_name(p.name+'.3').read_text(),'2');self.assertFalse(p.with_name(p.name+'.4').exists())

class CleanupTests(unittest.TestCase):
 def setUp(self):
  self.temp=tempfile.TemporaryDirectory();self.mod=Path(self.temp.name)/'mod';self.mod.mkdir();self.state=self.mod/'PanelRuntime/bridge-state';self.state.mkdir(parents=True)
  self.identity=['cleanup',0,1];self.key=profile_key(self.identity);self.token='a'*32;self.path=self.state/(self.key+'.json')
  item={'item':next(iter(M['trap_items'].values()))['id'],'location':-1,'player':0,'flags':4};self.receipt=receipt_key(0,item)
  self.path.write_text(json.dumps({'identity':self.identity,'items':[item]}))
  route(self.mod,'profile-'+self.token+'.txt').write_text(self.key)
  route(self.mod,'rules-'+self.token+'.txt').write_text(self.token+'\n'+M['digest']+'\n')
  self.check=M['events'][0]['key'];self.checkid=M['events'][0]['id']
  route(self.mod,'checks-'+self.token+'.txt').write_text(self.check+'\n')
  route(self.mod,'trap-acks-'+self.token+'.txt').write_text(self.token+'\n'+M['digest']+'\n'+self.receipt+'\n')
 def tearDown(self):self.temp.cleanup()
 def cleanup(self):return cleanup_sessions(self.mod,self.state,closed=lambda:None)
 def test_pending_check_and_started_trap_saved_before_cleanup(self):
  result=self.cleanup();state=json.loads(self.path.read_text())
  self.assertEqual(result['removed'],4);self.assertIn(self.checkid,state['pending']);self.assertIn(self.receipt,state['trap_consumed']);self.assertTrue(list((self.state/'recovery-backups').glob('*.json')))
 def test_unknown_check_retains_entire_session(self):
  route(self.mod,'checks-'+self.token+'.txt').write_text('unknown-check');self.assertEqual(self.cleanup()['removed'],0);self.assertTrue(route(self.mod,'profile-'+self.token+'.txt').exists())
 def test_running_game_blocks_cleanup(self):
  with self.assertRaises(RuntimeError):cleanup_sessions(self.mod,self.state,closed=lambda:(_ for _ in ()).throw(RuntimeError('running')))
  self.assertTrue(route(self.mod,'profile-'+self.token+'.txt').exists())
 def test_failed_state_write_keeps_all_session_files(self):
  with patch('maintenance.atomic_json',side_effect=OSError('disk full')):self.assertEqual(self.cleanup()['removed'],0)
  self.assertTrue(route(self.mod,'checks-'+self.token+'.txt').exists())
 def test_current_session_is_never_removed(self):
  route(self.mod,'active-session.txt').write_text(self.token);self.assertEqual(self.cleanup()['removed'],0)
 def test_missing_state_keeps_session(self):
  self.path.unlink();self.assertEqual(self.cleanup()['removed'],0)

 def test_universal_survival_key_recovers_new_id_not_legacy_id(self):
  from medal_rules import locations,LOCATION_SCHEMA
  row=next(r for r in locations(M,3,True,True) if r['event_key'] in M['survival_milestones'] and r['medal']==1)
  state=json.loads(self.path.read_text());state['location_schema']=LOCATION_SCHEMA;self.path.write_text(json.dumps(state))
  route(self.mod,'rules-'+self.token+'.txt').write_text('\n'.join([self.token,M['digest'],'3','1','1','2',LOCATION_SCHEMA,'1','0','0'])+'\n')
  route(self.mod,'checks-'+self.token+'.txt').write_text(row['key']+'\n')
  self.assertEqual(self.cleanup()['removed'],4)
  recovered=json.loads(self.path.read_text())['pending'];self.assertIn(row['id'],recovered)
  self.assertNotIn(M['survival_milestones'][row['event_key']]['Bronze']['id'],recovered)
 def test_unknown_location_schema_preserves_session(self):
  state=json.loads(self.path.read_text());state['location_schema']='unknown';self.path.write_text(json.dumps(state))
  self.assertEqual(self.cleanup()['removed'],0)
  self.assertTrue(route(self.mod,'checks-'+self.token+'.txt').exists())
