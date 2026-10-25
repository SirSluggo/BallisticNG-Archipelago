"""Run with actual AP core on PYTHONPATH. Audit the bundled two-slot test YAML constraints."""
import importlib.util,sys,logging
from pathlib import Path
from BaseClasses import MultiWorld,CollectionState
from Fill import distribute_items_restrictive
from worlds.generic.Rules import locality_rules
import Options
root=Path(__file__).parent
mods=[]
for name,path in [('ballisticng',root/'ballisticng/__init__.py'),('bng_test_partner',root/'Multiworld-Test/bng_test_partner/__init__.py')]:
 spec=importlib.util.spec_from_file_location('worlds.'+name,path,submodule_search_locations=[str(path.parent)])
 module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module;spec.loader.exec_module(module);mods.append(module)
logging.basicConfig(level=logging.ERROR)
for medal in (1,2,3):
 for enabled in (0,1):
  for seed in range(10):
   mw=MultiWorld(2);mw.set_seed(seed);mw.player_name={1:'BNGTest',2:'Tester'}
   for player,cls in [(1,mods[0].BallisticNGWorld),(2,mods[1].PartnerWorld)]:
    mw.game[player]=cls.game;w=cls(mw,player);mw.worlds[player]=w
    w.options=w.options_dataclass(**{n:o.from_any(o.default) for n,o in w.options_dataclass.type_hints.items()})
    w.options.accessibility=Options.Accessibility.from_any('full')
   bng=mw.worlds[1];bng.options.required_medal=bng.options.required_medal.from_any(medal)
   bng.options.traps=type(bng.options.traps).from_any(enabled)
   bng.options.non_local_items.value=set(mods[0].M['access_item_names'].values()) | ({v['name'] for v in mods[0].M['trap_items'].values()} if enabled else set())
   mw.worlds[2].options.non_local_items.value={'Tester Token'}
   mw.state=CollectionState(mw)
   for w in mw.worlds.values():w.generate_early()
   for w in mw.worlds.values():w.create_regions();w.create_items();w.set_rules()
   locality_rules(mw);distribute_items_restrictive(mw)
   for loc in mw.get_filled_locations():
    if loc.item.name=='Tester Token':assert loc.player==1 and loc.item.player==2
    if loc.item.name in bng.options.non_local_items.value:assert loc.player==2 and loc.item.player==1
   bonus={mw.get_location(r['name'],1) for r in bng.configured_locations() if r['key']!=r['event_key'] and r['medal']>medal}
   assert all(not loc.item.advancement for loc in bonus)
   state=CollectionState(mw);state.sweep_for_advancements(locations=[loc for loc in mw.get_filled_locations() if loc not in bonus])
   assert mw.has_beaten_game(state)
print('60 BNG + click-partner fills passed with traps off/on; all requested invitations/entries/traps placed in Tester; goals reachable without bonus progression.')
