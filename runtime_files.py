"""Locations of generated runtime files. Unknown names remain at the mod root."""
from pathlib import Path
import re
from functools import lru_cache
DATA={'layout-migration.json','active-session.txt','bridge-heartbeat.txt','profile-request.json','panel-status.txt','panel-settings.txt','consumable-bindings.txt','panel-command.txt','panel-command.tmp','panel-recovery.txt','comms-state.txt','comms-command.txt','comms-command.tmp'}
LOGS={'events.tsv','panel-bridge.log','setup-panel.log','offline-normal.log','trap-test.log','save-isolation.txt','profile-recovery.txt'}
CACHE={'appearance-catalog-ready.txt','appearance-catalog.tsv','appearance-variants.tsv','ship-catalog-ready.txt','ship-catalog.tsv','event-ship-requirements.tsv'}
SESSION=re.compile(r'^(access|activity|checks|rules|profile|toasts|traps|trap-acks|consumables)-[0-9a-f]{32}\.(txt|tmp)$')
def category(name):
    if name in LOGS:return 'Logs'
    if name in CACHE:return 'Cache'
    if name in DATA or SESSION.fullmatch(name) or re.fullmatch(r'setup-status-[0-9a-f]{32}\.tmp',name):return 'Data'
    return None

@lru_cache(maxsize=256)
def folder_path(mod,folder):
    target=Path(mod)/folder
    for ancestor in [target,*target.parents]:
        if ancestor.is_symlink() or (hasattr(ancestor,'is_junction') and ancestor.is_junction()):raise OSError('Linked runtime folders unsupported')
    target.mkdir(parents=True,exist_ok=True)
    return target

def route(mod,name):
    mod=Path(mod);name=str(name);folder=category(name)
    return folder_path(str(mod),folder)/name if folder else mod/name

def rotate(path,limit=2*1024*1024,backups=3):
    path=Path(path)
    if not path.exists() or path.stat().st_size<limit:return
    oldest=path.with_name(path.name+'.'+str(backups));oldest.unlink(missing_ok=True)
    for i in range(backups-1,0,-1):
        source=path.with_name(path.name+'.'+str(i))
        if source.exists():source.replace(path.with_name(path.name+'.'+str(i+1)))
    path.replace(path.with_name(path.name+'.1'))
