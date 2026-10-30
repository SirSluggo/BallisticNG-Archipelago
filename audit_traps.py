"""Run with official AP core on PYTHONPATH. Exhaustive toggle pools + real fills."""
import importlib.util,sys,itertools,collections,logging
from pathlib import Path
from BaseClasses import MultiWorld,CollectionState,ItemClassification
from Fill import distribute_items_restrictive
import Options,Utils
root=Path(__file__).resolve().parent
spec=importlib.util.spec_from_file_location('worlds.ballisticng',root/'ballisticng/__init__.py',submodule_search_locations=[str(root/'ballisticng')]);mod=importlib.util.module_from_spec(spec);sys.modules[spec.name]=mod;spec.loader.exec_module(mod)
logging.basicConfig(level=logging.ERROR)
keys=list(mod.M['trap_items']);trap_names={v['name'] for v in mod.M['trap_items'].values()}
def setup(players,seed,medal,enabled,chosen):
 mw=MultiWorld(players);mw.set_seed(seed);mw.player_name={p:'TrapTest'+str(p) for p in mw.player_ids}
 for player in mw.player_ids:
  mw.game[player]='BallisticNG';w=mod.BallisticNGWorld(mw,player);mw.worlds[player]=w
  w.options=w.options_dataclass(**{n:o.from_any(o.default) for n,o in w.options_dataclass.type_hints.items()})
  w.options.accessibility=Options.Accessibility.from_any('full');w.options.required_medal=type(w.options.required_medal).from_any(medal)
  w.options.traps=type(w.options.traps).from_any(enabled)
  for i,key in enumerate(keys):setattr(w.options,key,type(getattr(w.options,key)).from_any(int(i in chosen)))
 mw.state=CollectionState(mw)
 for w in mw.worlds.values():w.generate_early()
 for w in mw.worlds.values():w.create_regions();w.create_items();w.set_rules()
 for w in mw.worlds.values():
  pool=[x for x in mw.itempool if x.player==w.player];assert len(pool)==303
  traps=[x for x in pool if x.name in trap_names];filler=[x for x in pool if x.name=='Pit Crew Encouragement']
  assert len(traps)==(160 if enabled else 0) and len(filler)==(0 if enabled else 160)
  if enabled:
   expected={mod.M['trap_items'][keys[i]]['name'] for i in chosen};assert {x.name for x in traps}==expected
   assert all(x.classification==ItemClassification.trap and not x.advancement for x in traps)
  assert len(set(w.item_name_to_id.values()))==len(w.item_name_to_id)
 return mw
pools=0
for mask in range(1<<len(keys)):
 chosen={i for i in range(len(keys)) if mask&(1<<i)}
 setup(1,mask,3,False,chosen);pools+=1
 if mask:
  setup(1,mask,3,True,chosen);pools+=1
 else:
  try:setup(1,0,3,True,set())
  except ValueError as ex:assert 'every trap type is off' in str(ex)
  else:raise AssertionError('all-off enabled configuration accepted')
print('AP',Utils.__version__,':',pools,'toggle-subset pool configurations passed; all-off error verified.',flush=True)
fills=0
for players,medal,mode,seed in itertools.product((1,2),(1,2,3),('off','all','one','subset'),range(8)):
 enabled=mode!='off';chosen=set(range(len(keys))) if mode in ('off','all') else ({0} if mode=='one' else {1,4,7})
 mw=setup(players,seed,medal,enabled,chosen);distribute_items_restrictive(mw)
 bonus=set()
 for w in mw.worlds.values():
  for row in w.configured_locations():
   if row['key']!=row['event_key'] and row['medal']>medal:
    loc=mw.get_location(row['name'],w.player);bonus.add(loc);assert not loc.item.advancement
 state=CollectionState(mw);state.sweep_for_advancements(locations=[loc for loc in mw.get_filled_locations() if loc not in bonus]);assert mw.has_beaten_game(state)
 fills+=1
print('AP',Utils.__version__,':',fills,'solo/two-player real fills passed; exact trap pool sizes; no above-target progression; bonus-free goal reachable.',flush=True)
