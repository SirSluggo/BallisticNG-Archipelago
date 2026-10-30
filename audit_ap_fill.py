"""Developer audit: run with compatible AP core on PYTHONPATH and a world package directory argument.
The full AP source is not bundled. This script fails on any generation or bonus-progression regression.
"""
import argparse,importlib.util,sys,logging,time
from pathlib import Path
from BaseClasses import MultiWorld,CollectionState,Item,ItemClassification
from Fill import distribute_items_restrictive
import Options,Utils
class ForeignItem(Item):game='Test Adventure'
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('root',type=Path)
parser.add_argument('seeds',nargs='?',type=int,default=25)
parser.add_argument('--players',default='1,2',help='Comma-separated player counts to test')
parser.add_argument('--mixed-settings',action='store_true',help='Vary medals and trap settings between players')
args=parser.parse_args()
player_counts=tuple(int(value) for value in args.players.split(','))
if args.seeds<1 or not player_counts or any(count<1 for count in player_counts):parser.error('Positive seeds and player counts required')
root=args.root.resolve();path=root/'ballisticng/__init__.py' 
spec=importlib.util.spec_from_file_location('worlds.ballisticng',path,submodule_search_locations=[str(path.parent)])
mod=importlib.util.module_from_spec(spec);sys.modules[spec.name]=mod;spec.loader.exec_module(mod)
logging.basicConfig(level=logging.ERROR)
successes=[];failed=[];started=time.monotonic()
for players in player_counts:
 for medal in range(1,4):
  for enabled in (0,1):
   for seed in range(args.seeds):
    mw=MultiWorld(players);mw.set_seed(seed);mw.player_name={p:'BNGTest'+str(p) for p in mw.player_ids}
    for player in mw.player_ids:
     mw.game[player]='BallisticNG';w=mod.BallisticNGWorld(mw,player);mw.worlds[player]=w
     w.options=w.options_dataclass(**{n:o.from_any(o.default) for n,o in w.options_dataclass.type_hints.items()})
     w.options.traps=mod.BallisticNGOptions.type_hints['traps'].from_any((enabled+player-1)%2 if args.mixed_settings else enabled)
     w.options.accessibility=Options.Accessibility.from_any('full')
     w.options.required_medal=mod.BallisticNGOptions.type_hints['required_medal'].from_any(1+(medal+player-2)%3 if args.mixed_settings else medal)
    for w in mw.worlds.values():
     required=set(mod.M['required_ships'].values())
     for ship,item in mod.M['ship_items'].items():
      assert w.create_item(item['name']).advancement == (ship in required),item['name']
    mw.state=CollectionState(mw)
    for w in mw.worlds.values():w.generate_early()
    for w in mw.worlds.values():w.create_regions();w.create_items();w.set_rules()
    for w in mw.worlds.values():
     assert len([i for i in mw.itempool if i.player==w.player])==303
     cosmetic_names={v['name'] for v in mod.M['cosmetic_items'].values()}
     cosmetics=[i for i in mw.itempool if i.player==w.player and i.name in cosmetic_names]
     assert len(cosmetics)==67 and len({i.name for i in cosmetics})==67
     assert all(not i.advancement for i in cosmetics)
     assert sum(i.name=='Pit Crew Encouragement' and i.player==w.player for i in mw.itempool)==(0 if w.options.traps else 160)
     assert sum(i.name=='Autopilot Assist' and i.player==w.player for i in mw.itempool)==36
     assert sum(i.trap and i.player==w.player for i in mw.itempool)==(160 if w.options.traps else 0)
     assert len(set(w.item_name_to_id.values()))==len(w.item_name_to_id)
     assert not any(i.name in w.starting_items and i.player==w.player for i in mw.itempool)
     for name in ('Cassandra Introduction Race','Harpstone Introduction Race','Harpstone Pure Race'):
      assert mw.get_location('Breeze - '+name+' - Bronze',w.player).can_reach(mw.state)
     assert mw.get_location('Breeze - G-Teks Cassandra Trial - Bronze',w.player).can_reach(mw.state)==(w.starting_ship=='Gtek')
    try:distribute_items_restrictive(mw)
    except Exception as ex:failed.append((players,medal,enabled,seed,type(ex).__name__));continue
    bonus=set()
    for w in mw.worlds.values():
     for row in w.configured_locations():
      if row['key']!=row['event_key'] and row['medal']>w.options.required_medal.value:
       loc=mw.get_location(row['name'],w.player);bonus.add(loc)
       for classification in (ItemClassification.progression,ItemClassification.progression_skip_balancing):
        foreign=ForeignItem('Foreign progression',classification,999999,2)
        assert not loc.item_rule(foreign)
       assert not loc.item.advancement,(players,medal,enabled,seed,loc,loc.item)
    state=CollectionState(mw);state.sweep_for_advancements(locations=[loc for loc in mw.get_filled_locations() if loc not in bonus])
    assert mw.has_beaten_game(state),(players,medal,enabled,seed,'bonus-free goal unreachable')
    successes.append((players,medal,enabled,seed))
 print('Completed player count',players,'fills',sum(entry[0]==players for entry in successes),'failures',sum(entry[0]==players for entry in failed),'elapsed seconds',round(time.monotonic()-started,1),flush=True)
print('Mixed settings between players:',args.mixed_settings)
print('AP '+Utils.__version__+' audit:',len(successes),'completed fills;',len(failed),'known fill failures')
print('Every completed fill: no progression at above-target checks; goal reachable without any above-target check.')
print('Configurations with success:',len({s[:3] for s in successes}),'of',len(player_counts)*6,'player-count/medal/traps configurations')
print('Fill failures:',failed)

assert not failed, failed
