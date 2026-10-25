from runtime_files import route
"""Experimental fixed-root AP profiles. Selection never moves User/Save."""
import json,shutil,sys,uuid
from pathlib import Path
from datetime import datetime,timezone
from save_profiles import Profiles as LegacyProfiles,ProfileError,ensure_game_closed,process_guard,plain_path,inventory,atomic_json,atomic_text,valid_key,profile_key

def plain_tree(path):
    for part in [path,*path.parents]:plain_path(part)

def tree_inventory(root):
    files=inventory(root)
    for part in root.rglob('*'):
        plain_path(part)
        if part.is_dir():files[part.relative_to(root).as_posix()+'/']='directory'
    return dict(sorted(files.items()))

class Profiles:
    def __init__(self,game,closed=ensure_game_closed):
        self.game=Path(game).absolute();self.user=self.game/'User';self.root=self.user/'ArchipelagoSaves'
        self.legacy=LegacyProfiles(self.game,closed=closed);self.closed=closed
        self.state=self.root/'selected-profile.txt'
    def request(self,mod):return self.legacy.request(mod)
    def normal_ready(self):
        plain_tree(self.root);self.legacy.validate_tree()
        if self.legacy.journal.exists():raise ProfileError('Interrupted legacy save switch. Recover with the old tool first.')
        if self.legacy.validate_current()!='normal':raise ProfileError('Legacy AP save active. Use old Save-Profiles.cmd -> 2 to return to normal before this version.')
    def backup_normal(self):
        before=tree_inventory(self.user/'Save');stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'-'+uuid.uuid4().hex[:10]
        parent=self.root/'backups';plain_tree(parent);parent.mkdir(exist_ok=True)
        backup=parent/stamp;shutil.copytree(self.user/'Save',backup/'Save')
        if tree_inventory(backup/'Save')!=before or tree_inventory(self.user/'Save')!=before:raise ProfileError('Normal save changed during backup; no profile selected.')
        atomic_json(self.root/'normal-baseline.json',{'backup':stamp,'files':before})
    def verify_normal(self):
        self.normal_ready();path=self.root/'normal-baseline.json';plain_tree(path)
        if not path.is_file():raise ProfileError('No isolated-save normal baseline. Connect and select an AP profile first.')
        base=json.loads(path.read_text(encoding='utf-8'));actual=tree_inventory(self.user/'Save')
        changed=sorted(k for k in actual.keys()|base['files'].keys() if actual.get(k)!=base['files'].get(k))
        atomic_json(self.root/'normal-verification.json',{'normal_save_unchanged':not changed,'changed_paths':changed,'baseline_backup':base['backup']})
        return not changed
    def current(self):
        plain_tree(self.state)
        if not self.state.exists():
            slots=self.root/'slots';plain_tree(slots)
            if slots.exists() and any(slots.iterdir()):raise ProfileError('Isolated selection missing; no fallback allowed.')
            return 'normal'
        key=self.state.read_text(encoding='utf-8').strip()
        if key!='normal' and not valid_key(key):raise ProfileError('Invalid isolated profile selection.')
        return key
    def prepare_ap(self,request):
        key=request['key'];identity=request['identity']
        if not valid_key(key) or key!=profile_key(identity):raise ProfileError('Invalid isolated profile identity.')
        slot=self.root/'slots'/key;plain_tree(slot)
        if slot.exists():
            for path in [slot/'identity.json',route(slot,'profile-key.txt'),slot/'Save']:plain_tree(path)
            if not (slot/'Save').is_dir() or not (slot/'identity.json').is_file() or not (route(slot,'profile-key.txt')).is_file():raise ProfileError('Existing isolated profile is incomplete; no empty replacement created.')
            if json.loads((slot/'identity.json').read_text())!=identity or (route(slot,'profile-key.txt')).read_text().strip()!=key:raise ProfileError('Existing isolated identity mismatch.')
            tree_inventory(slot/'Save') # Refuse linked entries before activating.
            return
        # Publish complete metadata and Save together; leave incomplete staging for diagnosis.
        slot.parent.mkdir(exist_ok=True)
        staging=slot.with_name(key+'.partial-'+uuid.uuid4().hex);staging.mkdir()
        for name in ['Campaign','Precision','Times','Ghosts','Stats']:(staging/'Save'/name).mkdir(parents=True)
        atomic_json(staging/'identity.json',identity);atomic_text(route(staging,'profile-key.txt'),key+'\n');staging.rename(slot)
    def run(self,action,mod=None,cloud_disabled=False):
        self.closed();self.normal_ready()
        with process_guard(self.legacy.root):
            self.normal_ready();self.root.mkdir(exist_ok=True);current=self.current()
            if action=='ap':
                request=self.request(mod)
                if not self.state.exists():atomic_text(self.state,'normal\n')
                if current=='normal':self.backup_normal()
                elif not self.verify_normal():raise ProfileError('Normal save changed; no profile selection made.')
                self.prepare_ap(request)
                # Ensure the connection is still the same after potentially slow backup.
                latest=self.request(mod)
                if latest['identity']!=request['identity'] or latest['key']!=request['key']:raise ProfileError('Connected seed changed; no profile selected.')
                atomic_text(self.state,request['key']+'\n')
            elif action=='normal':
                if current!='normal' and not self.verify_normal():raise ProfileError('Normal save changed; no automatic selection/relaunch.')
                atomic_text(self.state,'normal\n')
            elif action=='verify':
                if not self.verify_normal():raise ProfileError('Normal-save comparison failed; preserve files and report.')
            elif action=='status':print('Selected isolated profile:',current)
            else:raise ProfileError('Unsupported isolated profile action.')

if __name__=='__main__':
    try:
        mod=Path(__file__).resolve().parents[1]
        Profiles(mod.parents[3]).run('verify',mod)
        print('NORMAL SAVE UNCHANGED. Report: User/ArchipelagoSaves/normal-verification.json')
    except Exception as error:print('STOP:',error);sys.exit(1)
