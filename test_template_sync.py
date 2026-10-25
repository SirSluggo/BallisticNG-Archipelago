import importlib.util,tempfile,unittest,shutil
from pathlib import Path
spec=importlib.util.spec_from_file_location('check_template',Path('tools/check_template.py'));module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
class TemplateSyncTests(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.root=Path(self.tmp.name);(self.root/'ballisticng').mkdir();shutil.copy2('ballisticng/options.py',self.root/'ballisticng/options.py');shutil.copy2('BallisticNG.yaml',self.root/'BallisticNG.yaml')
 def tearDown(self):self.tmp.cleanup()
 def change(self,before,after):
  p=self.root/'BallisticNG.yaml';s=p.read_text(encoding='utf-8-sig');self.assertIn(before,s);p.write_text(s.replace(before,after,1))
 def test_canonical_template_passes(self):self.assertEqual(module.validate_template(self.root),13)
 def test_old_trap_description_rejected(self):
  self.change('Replace the 18 blank Pit Crew Encouragement items with random traps.','Old trap wording.')
  with self.assertRaisesRegex(ValueError,'description'):module.validate_template(self.root)
 def test_old_progression_default_rejected(self):
  self.change('normal: 50 # equivalent to 50','normal: 0 # equivalent to 50')
  with self.assertRaisesRegex(ValueError,'common default'):module.validate_template(self.root)
 def test_wrong_custom_default_rejected(self):
  self.change('gold: 50','gold: 0')
  with self.assertRaisesRegex(ValueError,'default'):module.validate_template(self.root)
