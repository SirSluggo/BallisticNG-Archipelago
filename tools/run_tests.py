"""Run root tests without importing the AP world package during discovery."""
import os,sys,unittest
from pathlib import Path
root=Path(__file__).resolve().parents[1]
os.chdir(root);sys.path.insert(0,str(root))
suite=unittest.defaultTestLoader.loadTestsFromNames(sorted(f.stem for f in root.glob('test_*.py')))
result=unittest.TextTestRunner(verbosity=1).run(suite)
raise SystemExit(0 if result.wasSuccessful() else 1)
