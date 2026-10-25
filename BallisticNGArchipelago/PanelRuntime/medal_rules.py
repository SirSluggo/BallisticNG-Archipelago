MEDALS={1:'Bronze',2:'Silver',3:'Gold',4:'Platinum'}

def validate_rules(required_medal,survival_milestones):
    if type(required_medal) is not int or required_medal not in MEDALS:raise ValueError('Invalid required medal')
    if type(survival_milestones) not in (bool,int) or survival_milestones not in (0,1):raise ValueError('Invalid Survival milestone option')
    return required_medal,bool(survival_milestones)

def locations(manifest,required_medal=3,survival_milestones=False):
    required_medal,survival_milestones=validate_rules(required_medal,survival_milestones)
    rows=[]
    for event in manifest['events']:
        common={'event_key':event['key'],'campaign':event['campaign']}
        rows.append(dict(common,key=event['key'],id=event['id'],name=event['campaign']+' - '+event['event']+' - Clear',medal=max(3,required_medal) if event['key'] in manifest['goal_keys'] else required_medal))
        if survival_milestones and event['mode']=='Survival':
            for tier,name in MEDALS.items():
                if tier==4 or tier==required_medal:continue
                extra=manifest['survival_milestones'][event['key']][name]
                rows.append(dict(common,key=extra['key'],id=extra['id'],name=event['campaign']+' - '+event['event']+' - '+name,medal=tier))
    return rows
