from runtime_files import route
"""Background bridge and post-exit isolated profile selection for the F2 panel. Windows only."""
import asyncio
import base64
from contextlib import suppress
import os
from pathlib import Path
import subprocess
import sys
import time
import uuid
from urllib.parse import urlsplit
from bridge import Bridge,atomic_text
from bridge_lock import BridgeLock
from history_recovery import HistoryConflict,HistorySyncRequired
from isolated_profiles import Profiles
from save_profiles import ProfileError,ensure_game_closed


def connection_url(host,port):
    if not isinstance(host,str) or not isinstance(port,str):raise ValueError('Invalid connection fields')
    host=host.strip()
    if any(c.isspace() for c in host) or not host:raise ValueError('Invalid server')
    if not port.isascii() or not port.isdecimal() or not 1<=int(port)<=65535:raise ValueError('Invalid port')
    raw=host if '://' in host else 'ws://'+host
    parsed=urlsplit(raw)
    if parsed.scheme not in ('ws','wss') or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.path not in ('','/'):
        raise ValueError('Use a server hostname or ws/wss hostname without a path')
    if parsed.port is not None:raise ValueError('Enter the port in the separate port field')
    hostname='['+parsed.hostname+']' if ':' in parsed.hostname else parsed.hostname
    return parsed.scheme+'://'+hostname+':'+str(int(port))


def read_settings(stream):
    values=[]
    for _ in range(4):
        line=stream.readline()
        if not line or len(line)>8192:raise ValueError('Missing connection fields')
        values.append(base64.b64decode(line.strip(),validate=True).decode('utf-8'))
    host,port,slot,password=values
    url=connection_url(host,port)
    if not slot.strip() or any(c in slot for c in '\r\n'):raise ValueError('Invalid slot')
    return url,slot,password or None


def game_closed():
    try:ensure_game_closed();return True
    except ProfileError as ex:
        if str(ex)=='Close BallisticNG completely before changing save profiles.':return False
        raise


async def wait_for_exit(closed=game_closed,timeout=90,interval=.5):
    deadline=time.monotonic()+timeout
    while not await asyncio.to_thread(closed):
        if time.monotonic()>=deadline:raise ProfileError('Game did not close; no switch performed')
        await asyncio.sleep(interval)


def switch_and_launch(mod,action,expected=None,profiles=None,launch=None):
    mod=Path(mod);game=mod.parents[3]
    profiles=profiles or Profiles(game)
    # run() rechecks every BallisticNG process and holds the existing lifetime guard.
    if action=='ap':
        current=profiles.request(mod)
        if expected is None or current['identity']!=expected['identity'] or current['key']!=expected['key']:
            raise ProfileError('Connected seed changed; no switch performed')
    profiles.run(action,mod)
    if action=='normal' and not profiles.verify_normal():raise ProfileError('Normal save verification differs; no relaunch')
    if launch is None:launch=lambda:subprocess.Popen([str(game/'BallisticNG.exe')],cwd=game)
    return launch()


async def run(mod,url,slot,password):
    mod=Path(mod).resolve();nonce=uuid.uuid4().hex
    lock=BridgeLock(mod)
    bridge=None;network=None;launch_normal=False
    statefile=route(mod,'panel-status.txt');commandfile=route(mod,'panel-command.txt')
    current_status=['connecting','Connecting to Archipelago...','']
    def status(state,message,key=''):
        current_status[:]=[state,message,key]
        atomic_text(statefile,'\n'.join([nonce,state,key,message])+'\n')
    try:
        (route(mod,'active-session.txt')).unlink(missing_ok=True)
        bridge=Bridge(mod,Path(__file__).parent/'bridge-state',slot,password)
        status('connecting','Connecting to Archipelago...')
        async def connect():
            while True:
                try:
                    await bridge.session(url)
                except HistorySyncRequired:
                    status('connecting','Requesting a complete server item history...')
                    await asyncio.sleep(1)
                except HistoryConflict:
                    proposal=bridge.recovery
                    atomic_text(route(mod,'panel-recovery.txt'),nonce+'\n'+proposal['id']+'\n'+proposal['text']+'\n')
                    status('recovery','Item history differs from the server. Review the changes below; automatic retry is paused.')
                    return
                except ValueError as ex:
                    # Only fixed, known local validation strings are safe to display.
                    known={
                        'Wrong game/catalog: generate a fresh v0.24 BallisticNG seed.':'Seed/catalog mismatch. Check that this server is hosting your v0.24 seed.',
                        'Invalid starting inventory; generate a fresh v0.24 seed.':'Starting inventory does not match v0.24 slot data.',
                        'Stored room identity mismatch.':'Stored room identity mismatch. Preserve bridge-state for diagnosis.',
                        'Received-item history changed; refusing to overwrite it.':'Received-item history changed. Preserve bridge-state and the server save for diagnosis.',
                        'Room changed during recovery; reconnect and review again.':'Server room changed during recovery. Disconnect and verify the server/seed.',
                        'Bridge state changed during recovery; reconnect and review again.':'Local history changed during recovery. Disconnect and review again.',
                        'Invalid received-item packet':'Malformed item history from server; recovery refused.',
                        'Wrong game/catalog: generate a fresh v0.25 BallisticNG seed.':'Seed/catalog mismatch. Use a v0.25 seed or a supported v0.24 seed.',
                        'Invalid starting inventory; generate a fresh v0.25 seed.':'Starting inventory does not match this catalog.',
                        'Invalid trap setting':'Invalid trap setting in server slot data.',
                        'Invalid barrel roll setting':'Invalid barrel roll setting in server slot data.',
                        'Invalid AI preset':'Invalid AI preset in server slot data.',
                        'Invalid required medal':'Invalid medal setting in server slot data.',
                        'Invalid Survival milestone option':'Invalid Survival setting in server slot data.'
                    }
                    known.update({'Wrong game/catalog: generate a fresh v0.31 BallisticNG seed.':'Seed/catalog mismatch. Use a compatible v0.24–v0.31 seed.', 'Invalid starting inventory; generate a fresh v0.31 seed.':'Starting inventory does not match this catalog.'})
                    detail=known.get(str(ex),'Validation failed (ValueError). Preserve bridge-state for diagnosis.')
                    if str(ex).startswith('Connection refused: '):
                        codes=str(ex)[len('Connection refused: '):].split(', ')
                        allowed={'InvalidSlot','InvalidGame','IncompatibleVersion','InvalidPassword','InvalidItemsHandling','SlotAlreadyTaken'}
                        detail='Connection refused: '+', '.join(code if code in allowed else 'unrecognized reason' for code in codes)
                    status('error',detail+' Auto-retry stopped; Disconnect to edit/retry.')
                    return
                except Exception as ex:
                    # Error messages from networking libraries may contain connection input.
                    # Only the exception class is published/logged.
                    status('connecting','Connection unavailable ('+type(ex).__name__+'). Check fields, password and server; Disconnect to edit.')
                    await asyncio.sleep(5)
        network=asyncio.create_task(connect())
        absent_since=None
        while True:
            if bridge.ready:
                request=Profiles(mod.parents[3]).request(mod)
                status('connected','Connected. AP checks require the matching save.',request['key'])
            else:
                # Keep recovery/error controls live while awaiting a deliberate user action.
                status(*current_status)
            # Only commands belonging to this running instance are accepted.
            if commandfile.exists():
                lines=commandfile.read_text(encoding='utf-8-sig').splitlines()
                commandfile.unlink(missing_ok=True)
                if len(lines)==2 and lines[0]==nonce:
                    action=lines[1]
                    if action=='disconnect':break
                    if action.startswith('resync:') and network.done():
                        if bridge.approve_recovery(action.partition(':')[2]):
                            status('connecting','Rechecking server history before resync...')
                            network=asyncio.create_task(connect())
                        else:
                            status('error','Recovery preview is stale. Disconnect and connect again to review.')
                    if action in ('ap','normal'):
                        expected=Profiles(mod.parents[3]).request(mod) if action=='ap' else None
                        status('waiting','Restart requested. Waiting for BallisticNG to close...')
                        await wait_for_exit()
                        # The network task keeps the identity/heartbeat live while waiting.
                        if action=='normal':
                            normal=Profiles(mod.parents[3]);await asyncio.to_thread(normal.run,'normal',mod)
                            if not normal.verify_normal():raise ProfileError('Normal save verification differs; no relaunch')
                            launch_normal=True;break
                        status('restarting','Selecting the connected AP save...')
                        await asyncio.to_thread(switch_and_launch,mod,'ap',expected)
                        # Allow slow startup without treating it as a normal game exit.
                        for _ in range(20):
                            status('restarting','Starting BallisticNG with the selected isolated save...')
                            await asyncio.sleep(.5)
                        absent_since=None
            closed=await asyncio.to_thread(game_closed)
            if closed:
                if absent_since is None:absent_since=time.monotonic()
                if time.monotonic()-absent_since>10:break
            else:absent_since=None
            await asyncio.sleep(.5)
    except Exception as ex:
        status('error','Stopped ('+type(ex).__name__+'). See panel-bridge.log and save-isolation.txt. No automatic recovery/relaunch.')
        raise
    finally:
        if network:
            network.cancel()
            with suppress(asyncio.CancelledError):await network
        if bridge:bridge.deactivate()
        lock.release()
        if statefile.exists():
            lines=statefile.read_text(encoding='utf-8-sig').splitlines()
            if len(lines)>1 and lines[1] not in ('error','recovery'):status('disconnected','Disconnected. The selected save has not changed unless a restart switch completed.')
    if launch_normal:subprocess.Popen([str(mod.parents[3]/'BallisticNG.exe')],cwd=mod.parents[3])


def main():
    mod=Path(__file__).resolve().parents[1]
    try:
        url,slot,password=read_settings(sys.stdin)
        # Bridge output has no passwords; log is truncated on each new connection.
        with (route(mod,'panel-bridge.log')).open('w',encoding='utf-8',buffering=1) as log:
            sys.stdout=log;sys.stderr=log
            try:asyncio.run(run(mod,url,slot,password))
            except ProfileError as ex:print('Profile selection stopped:',str(ex),flush=True)
            except Exception as ex:print('Panel client stopped:',type(ex).__name__,flush=True)
    except FileExistsError:
        return  # Another bridge owns the lock; never overwrite its panel status.
    except Exception as ex:
        atomic_text(route(mod,'panel-status.txt'),'\nerror\n\nCould not start ('+type(ex).__name__+'). Close other bridges and check connection fields.\n')

if __name__=='__main__':main()
