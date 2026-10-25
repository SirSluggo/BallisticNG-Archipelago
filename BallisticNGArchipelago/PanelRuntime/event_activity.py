"""Availability counts derived only from validated received items and server checks."""
from medal_rules import locations

def activity(manifest,owned,checked,hinted,required_medal=3,survival_milestones=False):
    counts={name:[0,False] for name in manifest['campaigns']}
    names={v.get('gate',v['name']):v['id'] for group in ('track_items','speed_items') for v in manifest[group].values()}
    remaining={}
    for row in locations(manifest,required_medal,survival_milestones):
        if row['id'] not in checked:remaining.setdefault(row['event_key'],set()).add(row['id'])
    for event in manifest['events']:
        if event['key'] not in remaining:continue
        campaign=event['campaign']
        if campaign!='Breeze' and manifest['access_items'][campaign] not in owned:continue
        finale=manifest['finale_requirements'].get(event['key'])
        if finale and manifest['access_items'][finale] not in owned:continue
        ship=manifest['required_ships'].get(event['key'])
        if ship and ship!=manifest['starting_ship'] and manifest['ship_items'][ship]['id'] not in owned:continue
        if any(names[n] not in owned for n in manifest['event_requirements'][event['key']]):continue
        counts[campaign][0]+=1
        if remaining[event['key']] & hinted:counts[campaign][1]=True
    return counts


def own_hint_locations(hints,slot,valid_locations):
    """Only hints whose location belongs to this slot; recipient can be anybody."""
    result=set()
    if not isinstance(hints,list):return None
    for hint in hints:
        if not isinstance(hint,dict):continue
        if (type(hint.get('finding_player')) is int and hint['finding_player']==slot
                and type(hint.get('location')) is int and hint['location'] in valid_locations
                and hint.get('found') is not True and hint.get('status')!=40):
            result.add(hint['location'])
    return result


def event_remaining(manifest,checked,required_medal=3,survival_milestones=True):
    """Unchecked server locations per event, including optional Survival thresholds."""
    result={event['key']:0 for event in manifest['events']}
    for row in locations(manifest,required_medal,survival_milestones):
        if row['id'] not in checked:result[row['event_key']]+=1
    return result


def optional_only_events(manifest,checked,required_medal=3,survival_milestones=True):
    """Main clear confirmed; every remaining location is a non-progression bonus."""
    grouped={event['key']:[] for event in manifest['events']}
    for row in locations(manifest,required_medal,survival_milestones):
        if row['id'] not in checked:grouped[row['event_key']].append(row)
    return {event['key'] for event in manifest['events']
            if event['id'] in checked and grouped[event['key']]
            and all(row['key']!=row['event_key'] and row['medal']>required_medal
                    for row in grouped[event['key']])}
