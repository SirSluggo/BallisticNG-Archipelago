"""Closed-game migration and conservative session cleanup; never deletes save/history files."""
import hashlib,json,re,uuid
from pathlib import Path
from datetime import datetime,timezone
from runtime_files import category,route,rotate,SESSION
from save_profiles import ensure_game_closed,plain_path,profile_key,atomic_json
from bridge_lock import BridgeLock
from medal_rules import locations
from trap_delivery import read_acknowledgments,pending_traps
from history_recovery import backup_state

def migrate_layout(mod):
    mod=Path(mod);stamp=datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')+'-'+uuid.uuid4().hex[:8]
    plan=[]
    for source in mod.iterdir():
        folder=category(source.name)
        if not folder:continue
        plain_path(source)
        if not source.is_file():raise OSError('Expected generated file: '+source.name)
        target=route(mod,source.name);plain_path(target)
        if target.exists() and target.read_bytes()!=source.read_bytes():
            if folder=='Data':raise OSError('Conflicting runtime data; preserve both copies: '+source.name)
            target=mod/folder/'Imported'/stamp/source.name
        plan.append((source,target))
    report=[]
    for source,target in plan:
        target.parent.mkdir(parents=True,exist_ok=True)
        digest=hashlib.sha256(source.read_bytes()).hexdigest()
        if target.exists():source.unlink() # Preflight established byte equality.
        else:source.replace(target)
        report.append({'file':source.name,'target':str(target.relative_to(mod)),'sha256':digest})
    if report:atomic_json(route(mod,'layout-migration.json'),{'moved':report})
    return len(report)

def cleanup_sessions(mod,state_dir,closed=ensure_game_closed):
    mod=Path(mod);state_dir=Path(state_dir)
    if not state_dir.exists():return {'removed':0,'retained':0}
    closed()
    manifest=json.loads((Path(__file__).parent/'manifest.json').read_text())
    checks={row['key']:row['id'] for medal in (1,2,3) for row in locations(manifest,medal,True)}
    data=mod/'Data';removed=retained=0
    if not data.exists():return {'removed':0,'retained':0}
    active=(data/'active-session.txt').read_text().strip() if (data/'active-session.txt').exists() else None
    for profile in sorted(data.glob('profile-*.txt')):
        match=re.fullmatch(r'profile-([0-9a-f]{32})\.txt',profile.name)
        if not match:continue
        token=match[1]
        if token==active:continue
        files=[f for f in data.iterdir() if SESSION.fullmatch(f.name) and token in f.name]
        try:
            for f in files:plain_path(f)
            key=profile.read_text().strip()
            if not re.fullmatch('[0-9a-f]{64}',key):raise ValueError('invalid profile')
            state_path=state_dir/(key+'.json');plain_path(state_path)
            state=json.loads(state_path.read_text())
            if profile_key(state['identity'])!=key:raise ValueError('identity mismatch')
            rules=(data/('rules-'+token+'.txt')).read_text().splitlines()
            if len(rules)<2 or rules[:2]!=[token,manifest['digest']]:raise ValueError('unknown catalog/session')
            # Unknown check/ack records are retained for diagnosis rather than discarded.
            path=data/('checks-'+token+'.txt');lines=path.read_text().splitlines() if path.exists() else []
            if any(line and line not in checks for line in lines):raise ValueError('unknown check')
            pending=set(state.get('pending',[]));pending.update(checks[line] for line in lines if line)
            ack_path=data/('trap-acks-'+token+'.txt')
            ack_state=dict(state,trap_session=token,trap_digest=manifest['digest'])
            ack=read_acknowledgments(mod,ack_state,manifest['trap_items'])
            if ack_path.exists():
                records=ack_path.read_text(encoding='utf-8-sig').splitlines()
                if len(records)<2 or records[:2]!=[token,manifest['digest']] or not set(records[2:])<=ack:raise ValueError('unknown acknowledgment')
            candidate=dict(state,pending=sorted(pending),trap_consumed=sorted(set(state.get('trap_consumed',[]))|ack))
            if candidate!=state:
                backup_state(state_path);atomic_json(state_path,candidate)
            # Verify durable state before removing the now-redundant session snapshots.
            if json.loads(state_path.read_text())!=candidate:raise OSError('state verification failed')
            for f in files:f.unlink();removed+=1
        except (OSError,ValueError,KeyError,TypeError):retained+=1
    return {'removed':removed,'retained':retained}

def main():
    mod=Path(__file__).resolve().parents[1]
    ensure_game_closed()
    setup=BridgeLock(mod,'setup.lock')
    try:
        bridge=BridgeLock(mod)
        try:
            moved=migrate_layout(mod)
            result=cleanup_sessions(mod,mod/'PanelRuntime/bridge-state')
            print('Organized',moved,'files. Session cleanup:',result)
            print('Saves, Python environment and persistent bridge history were preserved.')
        finally:bridge.release()
    finally:setup.release()
if __name__=='__main__':
    try:main()
    except Exception as error:print('STOP:',error);raise SystemExit(1)
