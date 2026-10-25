import json
from importlib.resources import files
from BaseClasses import Item,ItemClassification,Location,Region
from worlds.AutoWorld import World,WebWorld
from Options import OptionGroup,item_and_loc_options
from .options import BallisticNGOptions
from .medals import locations

M=json.loads(files(__package__).joinpath('manifest.json').read_text(encoding='utf-8'))
GATES={item.get('gate',item['name']):item['name'] for group in ('track_items','speed_items') for item in M[group].values()}
class BNGItem(Item):game='BallisticNG'
class BNGLocation(Location):game='BallisticNG'

class BallisticNGWeb(WebWorld):
    # Put the overview/goal description before common game settings in generated templates.
    option_groups=[OptionGroup('Game Options',list(dict.fromkeys(
        [BallisticNGOptions.type_hints[name] for name in ('progression_balancing','accessibility','required_medal','ai_preset','barrel_rolls','traps',*M['trap_items'])]+
        [option for option in BallisticNGOptions.type_hints.values() if option not in item_and_loc_options]
    ))) ]

class BallisticNGWorld(World):
    """Race the AGL 2159 campaign in Archipelago. Goal: AGL Champion.
    Claim the AGL crown at the Zen Zeus Invitational: unlock AGL 2159 Finale A,
    B and C in the Zen Zeus campaign, then take 1st place overall in all three."""
    game='BallisticNG'
    web=BallisticNGWeb()
    options_dataclass=BallisticNGOptions
    options:BallisticNGOptions
    item_name_to_id={M['access_item_names'][n]:code for n,code in M['access_items'].items()}
    item_name_to_id.update({item['name']:item['id'] for group in ('ship_items','track_items','speed_items','cosmetic_items') for item in M[group].values()})
    item_name_to_id.update({v['name']:v['id'] for v in M['trap_items'].values()})
    item_name_to_id['Pit Crew Encouragement']=M['filler_id']
    location_name_to_id={row['name']:row['id'] for row in locations(M,4,True)}

    def generate_early(self):
        if self.options.traps and not self.enabled_trap_names():
            raise ValueError('BallisticNG: Traps is on, but every trap type is off. Enable at least one trap type or turn Traps off.')
        self.starting_ship=self.random.choice(M['starting_ship_candidates'])
        self.starting_items=[M['ship_items'][self.starting_ship]['name'],M['starting_track']]
        for name in self.starting_items:
            self.multiworld.push_precollected(self.create_item(name))
        # Harpstone supplies three free race checks; Zephyr expands the opening further.
        self.multiworld.local_early_items[self.player][GATES['Zephyr Ridge Track Access']]=1

    def configured_locations(self):return locations(M,self.options.required_medal.value,True)
    def create_regions(self):
        menu=Region('Menu',self.player,self.multiworld);self.multiworld.regions.append(menu)
        rows=self.configured_locations()
        for name in M['campaigns']:
            region=Region(name,self.player,self.multiworld);self.multiworld.regions.append(region)
            if name=='Breeze':menu.connect(region)
            else:menu.connect(region,rule=lambda state,n=name:state.has(M['access_item_names'][n],self.player))
            for row in rows:
                if row['campaign']==name:region.locations.append(BNGLocation(self.player,row['name'],row['id'],region))
            if name=='Zen Zeus':
                victory=BNGLocation(self.player,'AGL Champion',None,region)
                victory.place_locked_item(BNGItem('Victory',ItemClassification.progression,None,self.player));region.locations.append(victory)
    def create_item(self,name):
        required_ships=set(M['required_ships'].values())
        optional_ships={item['name'] for ship,item in M['ship_items'].items() if ship not in required_ships}
        classification=ItemClassification.trap if name in {v['name'] for v in M['trap_items'].values()} else ItemClassification.filler if name=='Pit Crew Encouragement' or name in optional_ships or name in {v['name'] for v in M['cosmetic_items'].values()} else ItemClassification.progression
        return BNGItem(name,classification,self.item_name_to_id[name],self.player)
    def create_items(self):
        names=[M['access_item_names'][n] for n in M['access_items']]+[item['name'] for group in ('ship_items','track_items','speed_items','cosmetic_items') for item in M[group].values()]
        names=[n for n in names if n not in self.starting_items]
        self.multiworld.itempool += [self.create_item(n) for n in names]
        filler_count=len(self.configured_locations())-len(names)
        if self.options.traps:
            enabled=self.enabled_trap_names()
            if filler_count< len(enabled):raise ValueError('BallisticNG: not enough filler slots for enabled trap types.')
            trap_names=enabled+self.random.choices(enabled,k=filler_count-len(enabled))
            self.random.shuffle(trap_names)
            self.multiworld.itempool += [self.create_item(name) for name in trap_names]
        else:
            self.multiworld.itempool += [self.create_item('Pit Crew Encouragement') for _ in range(filler_count)]
    def set_rules(self):
        events={e['key']:e for e in M['events']}
        for row in self.configured_locations():
            e=events[row['event_key']];required=[GATES[n] for n in M['event_requirements'][e['key']]]
            finale=M['finale_requirements'].get(e['key'])
            if finale:required.append(M['access_item_names'][finale])
            ship=M['required_ships'].get(e['key'])
            if ship and ship!=M['starting_ship']:required.append(M['ship_items'][ship]['name'])
            location=self.multiworld.get_location(row['name'],self.player)
            location.access_rule=lambda state,req=tuple(required):all(state.has(n,self.player) for n in req)
            if row['key']!=row['event_key'] and row['medal']>self.options.required_medal.value:
                # Applies to every recipient, including foreign-world progression and skip-balancing progression.
                previous=location.item_rule
                location.item_rule=lambda item,old=previous: not item.advancement and old(item)
        goal={GATES[n] for key in M['goal_keys'] for n in M['event_requirements'][key]}|{M['access_item_names'][n] for n in M['finale_requirements'].values()}
        self.multiworld.get_location('AGL Champion',self.player).access_rule=lambda state:all(state.has(n,self.player) for n in goal)
        self.multiworld.completion_condition[self.player]=lambda state:state.has('Victory',self.player)
    def enabled_trap_names(self):return [v['name'] for key,v in M['trap_items'].items() if getattr(self.options,key)]
    def get_filler_item_name(self):return self.random.choice(self.enabled_trap_names()) if self.options.traps else 'Pit Crew Encouragement'
    def fill_slot_data(self):return {'bridge_protocol':30,'ai_preset':self.options.ai_preset.value,'barrel_rolls':int(bool(self.options.barrel_rolls)),'manifest_digest':M['digest'],'goal_keys':M['goal_keys'],'required_medal':self.options.required_medal.value,'survival_milestones':1,'starting_ship':self.starting_ship,'starting_track':M['starting_track'],'traps':int(bool(self.options.traps)),'enabled_traps':self.enabled_trap_names() if self.options.traps else []}
