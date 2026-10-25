"""Developer audit: run with AP 0.6.6 core on PYTHONPATH and a world package directory argument.
The full AP source is not bundled. This script fails on any generation or bonus-progression regression.
"""
import importlib.util,sys,logging
from pathlib import Path
from BaseClasses import MultiWorld,CollectionState
from Fill import distribute_items_restrictive
import Options,Utils
root=Path(sys.argv[1]).resolve();path=root/'ballisticng/__init__.py'
spec=importlib.util.spec_from_file_location('worlds.ballisticng',path,submodule_search_locations=[str(path.parent)])
mod=importlib.util.module_from_spec(spec);sys.modules[spec.name]=mod;spec.loader.exec_module(mod)
logging.basicConfig(level=logging.ERROR)
successes=[];failed=[]
for players in (1,2):
 for medal in range(1,4):
  for enabled in (1,):
   for seed in range(int(sys.argv[2]) if len(sys.argv)>2 else 25):
    mw=MultiWorld(players);mw.set_seed(seed);mw.player_name={p:'BNGTest'+str(p) for p in mw.player_ids}
    for player in mw.player_ids:
     mw.game[player]='BallisticNG';w=mod.BallisticNGWorld(mw,player);mw.worlds[player]=w
     w.options=w.options_dataclass(**{n:o.from_any(o.default) for n,o in w.options_dataclass.type_hints.items()})
     w.options.accessibility=Options.Accessibility.from_any('full')
     w.options.required_medal=mod.BallisticNGOptions.type_hints['required_medal'].from_any(medal)
    for w in mw.worlds.values():
     required=set(mod.M['required_ships'].values())
     for ship,item in mod.M['ship_items'].items():
      assert w.create_item(item['name']).advancement == (ship in required),item['name']
    mw.state=CollectionState(mw)
    for w in mw.worlds.values():w.generate_early()
    for w in mw.worlds.values():w.create_regions();w.create_items();w.set_rules()
    for w in mw.worlds.values():
     assert len([i for i in mw.itempool if i.player==w.player])==125
     cosmetic_names={v['name'] for v in mod.M['cosmetic_items'].values()}
     cosmetics=[i for i in mw.itempool if i.player==w.player and i.name in cosmetic_names]
     assert len(cosmetics)==67 and len({i.name for i in cosmetics})==67
     assert all(not i.advancement for i in cosmetics)
     assert sum(i.name=='Pit Crew Encouragement' and i.player==w.player for i in mw.itempool)==18
     assert len(set(w.item_name_to_id.values()))==len(w.item_name_to_id)
     assert not any(i.name in w.starting_items and i.player==w.player for i in mw.itempool)
     for name in ('Cassandra Introduction Race','Harpstone Introduction Race','Harpstone Pure Race'):
      assert mw.get_location('Breeze - '+name+' - Clear',w.player).can_reach(mw.state)
     assert mw.get_location('Breeze - G-Teks Cassandra Trial - Clear',w.player).can_reach(mw.state)==(w.starting_ship=='Gtek')
    try:distribute_items_restrictive(mw)
    except Exception as ex:failed.append((players,medal,enabled,seed,type(ex).__name__));continue
    bonus=set()
    for w in mw.worlds.values():
     for row in w.configured_locations():
      if row['key']!=row['event_key'] and row['medal']>medal:
       loc=mw.get_location(row['name'],w.player);bonus.add(loc)
       assert not loc.item.advancement,(players,medal,enabled,seed,loc,loc.item)
    state=CollectionState(mw);state.sweep_for_advancements(locations=[loc for loc in mw.get_filled_locations() if loc not in bonus])
    assert mw.has_beaten_game(state),(players,medal,enabled,seed,'bonus-free goal unreachable')
    successes.append((players,medal,enabled,seed))
print('AP '+Utils.__version__+' audit:',len(successes),'completed fills;',len(failed),'known fill failures')
print('Every completed fill: no progression at above-target checks; goal reachable without any above-target check.')
print('Configurations with success:',len({s[:3] for s in successes}),'of 6 solo/two-player configurations')
print('Fill failures:',failed)

assert not failed, failed
