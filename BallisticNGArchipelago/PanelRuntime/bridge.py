from runtime_files import route
"""Experimental BallisticNG v0.31 bridge. Python 3.11+. Publishes session-scoped campaign access for the game mod."""
import argparse
import base64
import asyncio
import getpass
import json
import os
from pathlib import Path
import uuid
import time
import websockets
from event_activity import activity,own_hint_locations,event_remaining,optional_only_events
from medal_rules import locations,validate_rules
from comms import Comms
from trap_delivery import pending_traps,read_acknowledgments
from history_recovery import HistoryConflict,HistorySyncRequired,normalized,digest,backup_state,review,can_apply

M = json.loads((Path(__file__).parent / 'manifest.json').read_text(encoding='utf-8'))
GAME = M['game']
CHECKS = {e['key']:e['id'] for e in M['events']}
ITEM_NAMES = {code:M['access_item_names'][name] for name, code in M['access_items'].items()}
ITEM_NAMES.update({item['id']:item['name'] for item in M['ship_items'].values()})
ITEM_NAMES.update({item['id']:item['name'] for group in ('track_items','speed_items') for item in M[group].values()})
ITEM_NAMES.update({item['id']:item['name'] for item in M['cosmetic_items'].values()})
ITEM_NAMES.update({v['id']:v['name'] for v in M['trap_items'].values()})
TRAP_IDS={v['id'] for v in M['trap_items'].values()}
ITEM_NAMES[M['filler_id']] = 'Pit Crew Encouragement'
PROGRESSION_IDS = set(M['access_items'].values()) | {v['id'] for group in ('track_items','speed_items') for v in M[group].values()} | {v['id'] for ship,v in M['ship_items'].items() if ship in set(M['required_ships'].values())}
GOAL_LOCATIONS = frozenset(CHECKS[key] for key in M['goal_keys'])


def atomic_text(path, text):
    temp = path.with_suffix('.tmp')
    temp.write_text(text, encoding='utf-8')
    for attempt in range(8):
        try:
            os.replace(temp, path)
            return
        except PermissionError:
            if attempt == 7:
                raise
            time.sleep(0.025)


def atomic_json(path, data):
    atomic_text(path, json.dumps(data, indent=2))


class Bridge:
    def __init__(self, mod, state_dir, name, password=None):
        self.mod = Path(mod)
        self.state_dir = Path(state_dir)
        self.state_dir.mkdir(parents=True, exist_ok=True)
        self.name, self.password = name, password
        self.client_id = uuid.uuid4().hex
        self.token = None
        self.state = None
        self.checked = set()
        self.ready = False
        self.authenticated=False
        self.awaiting_history=True
        self.recovery=None
        self.recovery_approval=None
        self.traps_enabled=False
        self.barrel_rolls=False
        self.ai_preset=0
        self.published_traps=None
        self.last_send = 0
        self.state_path = None
        self.goal_sent = False
        self.toast_primed=False
        self.toast_sequence=0
        self.toast_rows=[]
        self.published_toasts=None
        self.player_names={0:"Server"}
        self.slot_games={}
        self.item_maps={GAME:ITEM_NAMES}
        self.pending_sends=[]
        self.baseline_checked=set()
        self.team=None
        self.published_access = None
        self.published_activity = None
        self.hint_key = None
        self.slot = None
        self.hinted = set()
        self.hints_ready = False
        self.last_hint_refresh = 0
        self.required_medal=3;self.survival_milestones=False
        self.locations=locations(M);self.checks={r["key"]:r["id"] for r in self.locations}
        self.progression_ids=PROGRESSION_IDS
        self.comms=Comms(self,M,ITEM_NAMES,GOAL_LOCATIONS,atomic_text)

    async def send(self, ws, **packet):
        await ws.send(json.dumps([packet]))

    def collect_trap_acks(self):
        if self.state is None:return
        acknowledged=read_acknowledgments(self.mod,self.state,M['trap_items'])
        consumed=set(self.state.get('trap_consumed',[]))
        if acknowledged-consumed:
            candidate=dict(self.state,trap_consumed=sorted(consumed|acknowledged))
            atomic_json(self.state_path,candidate);self.state=candidate

    def publish_traps(self):
        if not self.ready or not self.token:return
        if self.traps_enabled and self.state.get('trap_session')!=self.token:
            candidate=dict(self.state,trap_session=self.token,trap_digest=M['digest'])
            atomic_json(self.state_path,candidate);self.state=candidate
        rows=pending_traps(self.state,M['trap_items'],self.traps_enabled)[:64]
        content='\n'.join([self.token,M['digest']]+[key+'\t'+str(kind) for key,kind in rows])+'\n'
        path=route(self.mod,'traps-'+self.token+'.txt')
        if content!=self.published_traps or not path.exists():
            atomic_text(path,content);self.published_traps=content

    def capture_pending(self):
        self.collect_trap_acks()
        if not self.token or self.state is None or self.state_path is None:
            return
        path=route(self.mod,'checks-'+self.token+'.txt')
        pending=set(self.state.get('pending', []))
        if path.exists():
            pending.update(self.checks[key] for key in path.read_text(encoding='utf-8').splitlines() if key in self.checks)
        pending.difference_update(self.checked)
        if sorted(pending)!=self.state.get('pending', []):
            candidate=dict(self.state,pending=sorted(pending))
            atomic_json(self.state_path,candidate);self.state=candidate

    def approve_recovery(self, review_id):
        if self.recovery is None or review_id!=self.recovery['id']:
            return False
        self.recovery_approval=self.recovery
        return True

    def history_conflict(self, items):
        self.capture_pending()
        # Ensure the complete state exists before creating an exact-byte backup.
        if not self.state_path.exists():atomic_json(self.state_path,self.state)
        backup=backup_state(self.state_path)
        permanent={M['ship_items'][self.starting_ship]['id'],next(v['id'] for v in M['track_items'].values() if v['name']==M['starting_track'])}
        self.recovery=review(self.state,items,ITEM_NAMES,permanent,backup)
        self.recovery_approval=None
        self.deactivate()
        raise HistoryConflict('Received-item history differs. Review the recovery preview.')

    def activate(self):
        key=self.state_path.stem
        self.token=uuid.uuid4().hex
        atomic_text(route(self.mod,'rules-'+self.token+'.txt'),'\n'.join([self.token,M['digest'],str(self.required_medal),'1' if self.survival_milestones else '0','1' if self.barrel_rolls else '0',str(self.ai_preset)])+'\n')
        atomic_text(route(self.mod,'profile-'+self.token+'.txt'),key+'\n')
        atomic_json(route(self.mod,'profile-request.json'),{'schema':1,'token':self.token,'key':key,'identity':self.state['identity'],'slot_name':self.name})
        self.published_access=None
        self.ready=True
        self.published_traps=None
        self.publish_unlock();self.publish_toasts();self.publish_traps()
        self.comms.location_maps[GAME]={row['id']:row['name'] for row in self.locations}
        self.comms.log('SYSTEM','Item history validated. Connected as '+self.name+'.')
        self.comms.publish()
        # Publish activation last, after validated history and the access files exist.
        atomic_text(route(self.mod,'active-session.txt'),self.token)
        print('Connected to BallisticNG v0.36.1. Item history validated; ready for AP play.',flush=True)
        print('Starting ship: '+M['ship_items'][self.starting_ship]['name']+'; starting track: Harpstone.',flush=True)
        print('Save profile ready. Use F2 -> Restart into connected AP save if needed.',flush=True)

    def deactivate(self):
        try:
            self.capture_pending()
        finally:
            self.ready=False;self.authenticated=False
            self.comms.publish()
            path=route(self.mod,'active-session.txt')
            try:
                if self.token and path.exists() and path.read_text().strip()==self.token:path.unlink()
            finally:
                self.token=None

    async def packet(self, ws, p):
        self.comms.observe(p)
        cmd = p.get('cmd')
        if cmd == 'RoomInfo':
            self.seed = p['seed_name']
            await self.send(ws, cmd='Connect', password=self.password, name=self.name,
                game=GAME, uuid=self.client_id, tags=['AP'], items_handling=7,
                version={'major': 0, 'minor': 6, 'build': 0, 'class': 'Version'}, slot_data=True)
        elif cmd == 'ConnectionRefused':
            raise ValueError('Connection refused: ' + ', '.join(p.get('errors', [])))
        elif cmd == 'Connected':
            if self.token:self.deactivate()
            d = p.get('slot_data', {})
            protocol=d.get('bridge_protocol')
            expected_digest={30:M['digest'],29:M['legacy_v29_digest'],28:M['legacy_v28_digest'],26:M['legacy_v26_digest'],25:M['legacy_v25_digest'],24:M['legacy_v24_digest']}.get(protocol)
            if expected_digest is None or d.get('manifest_digest')!=expected_digest or d.get('goal_keys')!=M['goal_keys']:
                raise ValueError('Wrong game/catalog: generate a fresh v0.31 BallisticNG seed.')
            traps=d.get('traps',0) if protocol in (25,26,28,29,30) else 0
            if type(traps) not in (bool,int) or traps not in (0,1):raise ValueError('Invalid trap setting')
            self.traps_enabled=bool(traps)
            rolls=d.get('barrel_rolls') if protocol in (28,29,30) else 0
            if type(rolls) not in (bool,int) or rolls not in (0,1):raise ValueError('Invalid barrel roll setting')
            self.barrel_rolls=bool(rolls)
            preset=d.get('ai_preset') if protocol in (29,30) else 0
            if type(preset) is not int or preset not in (0,1,2):raise ValueError('Invalid AI preset')
            self.ai_preset=3 if protocol==29 and preset==2 else preset
            self.starting_ship=d.get('starting_ship')
            if self.starting_ship not in M['starting_ship_candidates'] or d.get('starting_track')!=M['starting_track']:
                raise ValueError('Invalid starting inventory; generate a fresh v0.31 seed.')
            self.required_medal,self.survival_milestones=validate_rules(d.get('required_medal'),d.get('survival_milestones'))
            self.locations=locations(M,self.required_medal,self.survival_milestones)
            self.checks={r['key']:r['id'] for r in self.locations}
            import hashlib
            identity = [self.seed, p['team'], p['slot']]
            if self.recovery_approval is not None and identity!=self.recovery_approval['identity']:
                raise ValueError('Room changed during recovery; reconnect and review again.')
            key = hashlib.sha256(json.dumps(identity).encode()).hexdigest()
            self.state_path = self.state_dir / (key + '.json')
            self.state = json.loads(self.state_path.read_text()) if self.state_path.exists() else {'identity': identity, 'items': []}
            if self.state['identity'] != identity:
                raise ValueError('Stored room identity mismatch.')
            self.state.setdefault('pending', [])
            self.state.setdefault('notified_sends', [])
            self.state.setdefault('quiet_checks', [])
            self.collect_trap_acks()
            normalized(self.state['items'])
            self.authenticated=True;self.awaiting_history=True
            self.checked = set(p.get('checked_locations', []))
            self.slot = p['slot']
            self.team=p['team']
            self.player_names={0:'Server',self.slot:self.name}
            self.slot_games={self.slot:GAME}
            self.item_maps={GAME:ITEM_NAMES}
            self.pending_sends=[]
            self.baseline_checked=set(self.checked)
            self.update_players(p)
            self.hint_key = '_read_hints_'+str(p['team'])+'_'+str(self.slot)
            self.hinted = set(); self.hints_ready = False
            self.published_activity = None
            self.last_hint_refresh = asyncio.get_running_loop().time()
            self.goal_sent = False
            self.last_send = 0
            self.toast_primed=False;self.toast_sequence=0;self.toast_rows=[];self.published_toasts=None
            # Do not expose cached unlocks until the server's full item history is checked.
            self.ready=False;self.token=None
            await self.send(ws, cmd='Sync')
            await self.send(ws, cmd='SetNotify', keys=[self.hint_key])
            await self.send(ws, cmd='Get', keys=[self.hint_key])
            games=sorted(set(self.slot_games.values())-{GAME})
            for game in games:await self.send(ws,cmd='GetDataPackage',games=[game])
        elif cmd == 'DataPackage' and self.authenticated:
            self.accept_data_package(p.get('data',{}))
        elif cmd == 'RoomUpdate':
            self.update_players(p)
            self.checked.update(p.get('checked_locations', []))
            self.publish_activity()
        elif cmd == 'Retrieved' and self.authenticated:
            keys = p.get('keys', {})
            if isinstance(keys,dict) and self.hint_key in keys:
                self.accept_hints(keys[self.hint_key])
        elif cmd == 'SetReply' and self.authenticated and p.get('key') == self.hint_key:
            self.accept_hints(p.get('value'))
        elif cmd == 'PrintJSON' and self.ready and p.get('type') == 'ItemSend':
            self.accept_sent_item(p)
        elif cmd == 'PrintJSON' and self.ready and p.get('type') == 'Hint':
            item = p.get('item')
            if isinstance(item,dict) and item.get('player') == self.slot and type(item.get('location')) is int:
                location = item['location']
                if location in self.checks.values():
                    if p.get('found') is True:self.hinted.discard(location)
                    else:self.hinted.add(location)
                    self.publish_activity()
            # Refresh the authoritative list; never scout or create hints automatically.
            await self.send(ws, cmd='Get', keys=[self.hint_key])
        elif cmd == 'ReceivedItems' and self.authenticated:
            index=p.get('index');items=normalized(p.get('items'))
            if type(index) is not int or index<0:raise ValueError('Invalid received-item packet')
            saved=normalized(self.state['items'])
            if (self.awaiting_history and index!=0) or index>len(saved):
                await self.send(ws,cmd='Sync');return
            mismatch=any(saved[index+i]!=item for i,item in enumerate(items) if index+i<len(saved))
            truncated=index==0 and len(items)<len(saved)
            recovered=False
            if mismatch or truncated:
                if index!=0:
                    # An incremental packet is not a full authoritative replacement.
                    self.deactivate()
                    raise HistorySyncRequired('Reconnect for a complete item-history snapshot.')
                if can_apply(self.recovery_approval,self.state,items):
                    # Recheck disk as well as memory; never overwrite a concurrent edit.
                    if digest(json.loads(self.state_path.read_text()))!=self.recovery_approval['disk_digest']:
                        raise ValueError('Bridge state changed during recovery; reconnect and review again.')
                    backup=backup_state(self.state_path)
                    candidate=dict(self.state,items=items,
                        quiet_checks=sorted(set(self.state.get('quiet_checks',[]))|set(self.state['pending'])))
                    atomic_json(self.state_path,candidate);self.state=candidate
                    self.baseline_checked.update(candidate['quiet_checks'])
                    saved=items;recovered=True
                    self.recovery=None;self.recovery_approval=None
                    print('Item history resynced with server. Backup: '+backup.name+'. Pending checks retained; old notifications suppressed.',flush=True)
                else:
                    self.history_conflict(items)
            was_initial=self.awaiting_history
            new=items[max(0,len(saved)-index):]
            if new:
                candidate=dict(self.state,items=saved+new)
                atomic_json(self.state_path,candidate);self.state=candidate
                for item in new:
                    print('Received item:',ITEM_NAMES.get(item['item'],item['item']),flush=True)
                    if not was_initial:self.comms.log('ITEM','Received: '+str(ITEM_NAMES.get(item['item'],item['item']))+' from '+self.player_names.get(item['player'],'Server' if item['player']==0 else 'Player '+str(item['player'])))
                    sender=item['player'];local=sender==self.slot and item['location']>=0
                    quiet=local and item['location'] in self.state.get('quiet_checks',[])
                    if self.toast_primed and not was_initial and not recovered and not quiet:
                        kind='ACQUIRED' if local else 'RECEIVED'
                        peer='' if local else self.player_names.get(sender,'Player '+str(sender))
                        if item['location']<0:peer='Server'
                        flags=item['flags'] | (1 if item['item'] in PROGRESSION_IDS else 0) | (4 if item['item'] in TRAP_IDS else 0)
                        self.add_toast(str(ITEM_NAMES.get(item['item'],item['item'])),flags,kind,peer)
            elif not self.state_path.exists():atomic_json(self.state_path,self.state)
            self.toast_primed=True
            if was_initial:
                self.recovery=None;self.recovery_approval=None
                self.baseline_checked.update(self.state.get('quiet_checks',[]))
                self.activate()
                self.awaiting_history=False
            self.publish_toasts();self.publish_unlock();self.publish_traps()
        elif cmd == 'InvalidPacket':
            raise ValueError('Server rejected a packet: ' + str(p.get('text', p.get('type', 'unknown'))))

    def accept_hints(self, value):
        self.comms.set_hints(value)
        locations = own_hint_locations(value,self.slot,set(self.checks.values()))
        if locations is None:
            return  # Unsupported/null response is not an authoritative empty list.
        changed = not self.hints_ready or locations != self.hinted
        self.hinted = locations
        self.hints_ready = True
        if changed:print('Hint indicators refreshed:',len(locations-self.checked),'unchecked location(s) in your world.',flush=True)
        self.publish_activity()

    def update_players(self,packet):
        for slot,info in packet.get('slot_info',{}).items():
            try:slot=int(slot)
            except (TypeError,ValueError):continue
            if isinstance(info,dict):
                if isinstance(info.get('name'),str):self.player_names[slot]=info['name']
                if isinstance(info.get('game'),str):self.slot_games[slot]=info['game']
        for player in packet.get('players',[]):
            if isinstance(player,dict) and player.get('team')==self.team and type(player.get('slot')) is int:
                self.player_names[player['slot']]=str(player.get('alias') or player.get('name') or 'Player '+str(player['slot']))

    def add_toast(self,name,flags,kind,peer='',timestamp=None):
        self.toast_sequence+=1
        clean=lambda value:str(value).replace('\r',' ').replace('\n',' ')[:512]
        encode=lambda value:base64.b64encode(clean(value).encode('utf-8')).decode('ascii')
        self.toast_rows.append((self.toast_sequence,flags,int(time.time()*1000) if timestamp is None else timestamp,encode(name),kind,encode(peer)))
        self.toast_rows=self.toast_rows[-24:]

    def accept_sent_item(self,packet):
        if not self.toast_primed:return
        if 'team' in packet and packet['team']!=self.team:return
        item=packet.get('item');recipient=packet.get('receiving')
        if not isinstance(item,dict) or type(recipient) is not int:return
        if any(type(item.get(key)) is not int for key in ('item','location','player')):return
        if item['player']!=self.slot or recipient==self.slot or item['location'] not in self.checks.values():return
        if item['location'] in self.baseline_checked:return
        key=str(item['player'])+':'+str(item['location'])+':'+str(recipient)+':'+str(item['item'])
        if key in self.state['notified_sends']:return
        candidate=dict(self.state,notified_sends=(self.state['notified_sends']+[key])[-4096:])
        atomic_json(self.state_path,candidate);self.state=candidate
        game=self.slot_games.get(recipient)
        self.pending_sends.append((item,recipient,game,int(time.time()*1000)))
        self.pending_sends=self.pending_sends[-128:]
        self.flush_sent_toasts()

    def accept_data_package(self,data):
        if not isinstance(data,dict) or not isinstance(data.get('games'),dict):return
        for game,package in data['games'].items():
            if game==GAME or not isinstance(package,dict):continue
            mapping=package.get('item_name_to_id')
            if isinstance(mapping,dict):
                self.item_maps[game]={code:name for name,code in mapping.items() if isinstance(name,str) and type(code) is int}
        self.flush_sent_toasts()

    def flush_sent_toasts(self):
        pending=[];now=int(time.time()*1000)
        for item,recipient,game,created in self.pending_sends:
            name=self.item_maps.get(game,{}).get(item['item'])
            if name is None:
                if now-created<=15000:pending.append((item,recipient,game,created))
                continue
            self.add_toast(name,int(item.get('flags',0)),'SENT',self.player_names.get(recipient,'Player '+str(recipient)),created)
        self.pending_sends=pending
        self.publish_toasts()

    def publish_toasts(self):
        if not self.ready or not self.token:return
        content='\n'.join([self.token,M['digest']]+['\t'.join(map(str,row)) for row in self.toast_rows])+'\n'
        target=route(self.mod,'toasts-'+self.token+'.txt')
        if content!=self.published_toasts or not target.exists():
            atomic_text(target,content);self.published_toasts=content

    def publish_activity(self):
        if not self.ready or not self.token:
            return
        owned = {x['item'] for x in self.state['items']}
        owned.update((M['ship_items'][self.starting_ship]['id'],next(v['id'] for v in M['track_items'].values() if v['name']==M['starting_track'])))
        hinted = self.hinted - self.checked
        counts = activity(M,owned,self.checked,hinted,self.required_medal,self.survival_milestones)
        lines = [self.token,M['digest'],'hints\t'+('ready' if self.hints_ready else 'pending')]
        lines += ['campaign\t'+name+'\t'+str(count)+'\t'+('1' if blue else '0') for name,(count,blue) in counts.items()]
        hint_events={r['event_key'] for r in self.locations if r['id'] in hinted}
        lines += ['hint\t'+e['key'] for e in M['events'] if e['key'] in hint_events]
        optional=optional_only_events(M,self.checked,self.required_medal,self.survival_milestones)
        lines += ['event\t'+key+'\t'+str(count)+'\t'+('1' if key in optional else '0') for key,count in event_remaining(M,self.checked,self.required_medal,self.survival_milestones).items()]
        content = '\n'.join(lines)+'\n'
        path = route(self.mod,'activity-'+self.token+'.txt')
        if content != self.published_activity or not path.exists():
            atomic_text(path,content)
            self.published_activity = content

    def publish_unlock(self):
        if not self.ready or not self.token:
            return
        owned = {x['item'] for x in self.state['items']}
        owned.update((M['ship_items'][self.starting_ship]['id'],next(v['id'] for v in M['track_items'].values() if v['name']==M['starting_track'])))
        campaigns = [name for name, code in M['access_items'].items() if code in owned]
        campaigns += [item.get('gate',item['name']) for group in ('track_items','speed_items') for item in M[group].values() if item['id'] in owned]
        campaigns += ['Cosmetic:'+key for key,item in M['cosmetic_items'].items() if item['id'] in owned]
        campaigns += ['Starter:'+self.starting_ship]
        campaigns += ['Ship:' + name for name, item in M['ship_items'].items() if item['id'] in owned]
        path = route(self.mod,'access-' + self.token + '.txt')
        (route(self.mod,'bridge-heartbeat.txt')).write_text(self.token, encoding='utf-8')
        content = '\n'.join([self.token, M['digest']] + campaigns) + '\n'
        if content != self.published_access or not path.exists():
            atomic_text(path, content)
            self.published_access = content
        self.publish_activity()

    async def poll(self, ws):
        await self.comms.command(ws)
        self.comms.publish()
        self.collect_trap_acks()
        self.publish_traps()
        self.publish_toasts()
        self.publish_unlock()
        if not self.ready:
            return
        now = asyncio.get_running_loop().time()
        if self.hint_key and now-self.last_hint_refresh >= 30:
            await self.send(ws, cmd='Get', keys=[self.hint_key])
            self.last_hint_refresh = now
        file = route(self.mod,'checks-' + self.token + '.txt')
        pending = set(self.state['pending'])
        if file.exists():
            pending.update(self.checks[key] for key in file.read_text(encoding='utf-8').splitlines() if key in self.checks)
        pending.difference_update(self.checked)
        if sorted(pending) != self.state['pending']:
            candidate = dict(self.state, pending=sorted(pending))
            atomic_json(self.state_path, candidate)
            self.state = candidate
        now = asyncio.get_running_loop().time()
        if pending and now - self.last_send >= 5:
            await self.send(ws, cmd='LocationChecks', locations=sorted(pending))
            self.last_send = now
            print('Sent campaign check(s):', sorted(pending), flush=True)
        # All three distinct finale wins must be acknowledged by the server. Local goal files are ignored.
        if GOAL_LOCATIONS.issubset(self.checked) and not self.goal_sent:
            await self.send(ws, cmd='StatusUpdate', status=30)
            self.goal_sent = True
            print('AGL CHAMPION: Finale A, B and C confirmed; goal reported to Archipelago.', flush=True)
            self.comms.log('SYSTEM','AGL Champion: all three finales confirmed; goal reported to Archipelago.')

    async def local_io(self, operation):
        reported = False
        while True:
            try:
                return await operation()
            except PermissionError as ex:
                if not reported:
                    print('Local file temporarily busy; keeping server connection and retrying:',
                          ex.filename or str(ex), flush=True)
                    reported = True
                await asyncio.sleep(0.5)

    async def session(self, url):
        try:
            async with websockets.connect(url, max_size=8*1024*1024) as ws:
                # Keep one recv task alive across polling intervals (do not cancel recv).
                task = asyncio.create_task(ws.recv())
                try:
                    while True:
                        done, _ = await asyncio.wait({task}, timeout=0.5)
                        if done:
                            for p in json.loads(task.result()):
                                await self.local_io(lambda: self.packet(ws, p))
                            task = asyncio.create_task(ws.recv())
                        await self.local_io(lambda: self.poll(ws))
                finally:
                    task.cancel()
                    await asyncio.gather(task, return_exceptions=True)
        finally:
            self.deactivate()


async def main(args):
    mod = Path(args.mod_dir)
    if not (mod / 'BallisticNGArchipelago.cs').exists():
        raise ValueError('--mod-dir must point to the installed BallisticNGArchipelago folder.')
    from bridge_lock import BridgeLock
    lock = BridgeLock(mod)
    try:
        # Stale/crashed sessions are never reused.
        (route(mod,'active-session.txt')).unlink(missing_ok=True)
        bridge = Bridge(mod, args.state_dir, args.name, getpass.getpass('Room password: ') if args.password else None)
        while True:
            try:
                await bridge.session(args.server)
            except (OSError, websockets.exceptions.ConnectionClosed) as ex:
                print('Connection lost; retrying in 5 seconds. Restart the race after reconnection.', type(ex).__name__, flush=True)
                await asyncio.sleep(5)
    finally:
        (route(mod,'active-session.txt')).unlink(missing_ok=True)
        lock.release()


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--server', required=True, help='ws://localhost:38281 or room websocket URL')
    parser.add_argument('--name', default='BNGTest')
    parser.add_argument('--mod-dir', required=True)
    parser.add_argument('--state-dir', default=str(Path(__file__).parent / 'bridge-state'))
    parser.add_argument('--password', action='store_true', help='Prompt without echoing the room password')
    a = parser.parse_args()
    try:
        asyncio.run(main(a))
    except KeyboardInterrupt:
        pass
    except Exception as ex:
        print('Stopped:', ex)
        raise SystemExit(1)
