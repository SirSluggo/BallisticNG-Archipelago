"""Check new-seed defaults and slot-data plumbing against actual AP option classes."""
import importlib.util,sys
from pathlib import Path
from BaseClasses import MultiWorld,CollectionState
from Options import get_option_groups
p=Path(__file__).parent;spec=importlib.util.spec_from_file_location('worlds.ballisticng',p/'ballisticng/__init__.py',submodule_search_locations=[str(p/'ballisticng')]);m=importlib.util.module_from_spec(spec);sys.modules[spec.name]=m;spec.loader.exec_module(m)
mw=MultiWorld(1);mw.set_seed(28);w=m.BallisticNGWorld(mw,1);mw.worlds[1]=w;mw.state=CollectionState(mw)
w.options=w.options_dataclass(**{n:c.from_any(c.default) for n,c in w.options_dataclass.type_hints.items()});w.generate_early()
assert w.options.barrel_rolls.value==1
assert w.options.ai_preset.value==2
assert list(get_option_groups(m.BallisticNGWorld)['Game Options'])[:3]==['required_medal','ai_preset','barrel_rolls']
for on in (0,1):
 w.options.barrel_rolls=type(w.options.barrel_rolls).from_any(on);data=w.fill_slot_data()
 assert data['barrel_rolls']==on and data['bridge_protocol']==31 and data['manifest_digest']==m.M['digest']
assert w.fill_slot_data()['ai_preset']==2
print('Actual AP option classes: Barrel Rolls default ON; off/on slot data and template order verified.')
