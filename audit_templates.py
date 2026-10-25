"""Verify option ordering and docs used by the official AP template generator."""
import importlib.util,sys,inspect
from pathlib import Path
from Options import get_option_groups
root=Path(__file__).parent
spec=importlib.util.spec_from_file_location('worlds.ballisticng',root/'ballisticng/__init__.py',submodule_search_locations=[str(root/'ballisticng')]);module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module;spec.loader.exec_module(module)
groups=get_option_groups(module.BallisticNGWorld)
assert list(groups)[0]=='Game Options'
assert list(groups['Game Options'])[:2]==['progression_balancing','accessibility']
doc=inspect.cleandoc(groups['Game Options']['required_medal'].__doc__)
for text in ['BallisticNG Archipelago','Goal — AGL Champion:','Zen Zeus Invitational','Finale A, B, and C','Required Medal:']:assert text in doc,text
assert doc.index('Goal — AGL Champion:')<doc.index('Required Medal:')
assert '"The Game\'s Playing Itself Jon"' in groups['Game Options']['backseat_gaming_trap'].__doc__
assert 'mirror_trap' in groups['Game Options']
print('Template inputs verified: Progression Balancing and Accessibility first; overview/goal before medal instructions; Backseat Gaming quote and Mirror Trap included.')
