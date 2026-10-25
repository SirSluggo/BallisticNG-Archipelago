from runtime_files import route
"""Offline, journaled BallisticNG local-save profiles. Python 3.11+, Windows CLI."""
import argparse
from contextlib import contextmanager
import csv
from datetime import datetime, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import time
import uuid

MARKER = '.bng-ap-profile'
DEFAULT_MOD = r'G:\SteamLibrary\steamapps\common\BallisticNG\User\Mods\Code Mods\BallisticNGArchipelago'

class ProfileError(RuntimeError):
    pass

def profile_key(identity):
    return hashlib.sha256(json.dumps(identity).encode()).hexdigest()

def valid_key(key):
    return isinstance(key, str) and re.fullmatch(r'[0-9a-f]{64}', key) is not None

def atomic_text(path, value):
    temp = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        with temp.open('x', encoding='utf-8', newline='\n') as out:
            out.write(value)
            out.flush()
            os.fsync(out.fileno())
        os.replace(temp, path)
    finally:
        if temp.exists():
            temp.unlink()

def atomic_json(path, value):
    atomic_text(path, json.dumps(value, indent=2) + '\n')

def plain_path(path):
    """Refuse symbolic links/junctions/reparse points rather than following them."""
    if not path.exists() and not path.is_symlink():
        return
    info = path.lstat()
    if stat.S_ISLNK(info.st_mode) or getattr(info, 'st_file_attributes', 0) & 0x400:
        raise ProfileError('Linked/reparse path is unsupported: ' + str(path))

def inventory(path):
    plain_path(path)
    if not path.is_dir():
        raise ProfileError('Expected save directory: ' + str(path))
    result = {}
    for current, dirs, files in os.walk(path, followlinks=False):
        for name in dirs:
            plain_path(Path(current) / name)
        for name in files:
            f = Path(current) / name
            plain_path(f)
            if not f.is_file():
                raise ProfileError('Unexpected save entry: ' + str(f))
            digest = hashlib.sha256()
            with f.open('rb') as source:
                for chunk in iter(lambda: source.read(1024 * 1024), b''):
                    digest.update(chunk)
            result[f.relative_to(path).as_posix()] = digest.hexdigest()
    return result

def ensure_game_closed():
    if os.name != 'nt':
        raise ProfileError('The profile-switching CLI currently supports Windows only.')
    result = subprocess.run(['tasklist', '/FI', 'IMAGENAME eq BallisticNG.exe', '/FO', 'CSV', '/NH'],
                            capture_output=True, text=True, errors='replace', timeout=15)
    if result.returncode:
        raise ProfileError('Could not check running games; no save switch allowed.')
    for row in csv.reader(io.StringIO(result.stdout)):
        if row and row[0].casefold() == 'ballisticng.exe':
            raise ProfileError('Close BallisticNG completely before changing save profiles.')

@contextmanager
def process_guard(root):
    """Also conflicts with the mod's lifetime FileStream opened with FileShare.Read."""
    root.mkdir(parents=True, exist_ok=True)
    gate = root / 'run.lock'
    plain_path(gate)
    try:
        handle = gate.open('a+b')
    except OSError as ex:
        raise ProfileError('Game/profile tool still has the save lock. Close it and retry.') from ex
    try:
        if gate.stat().st_size == 0:
            handle.write(b'0'); handle.flush()
        handle.seek(0)
        if os.name == 'nt':
            import msvcrt
            msvcrt.locking(handle.fileno(), msvcrt.LK_NBLCK, 1)
        else:
            import fcntl
            fcntl.flock(handle, fcntl.LOCK_EX | fcntl.LOCK_NB)
        yield
    finally:
        handle.close()

class Profiles:
    def __init__(self, game, closed=ensure_game_closed):
        # Test code can inject its process check; the CLI never bypasses it.
        self.game = Path(game).absolute()
        self.user = self.game / 'User'
        self.root = self.user / 'ArchipelagoProfiles'
        self.active = self.user / 'Save'
        self.state = self.root / 'active-profile.txt'
        self.journal = self.root / 'switch-journal.json'
        self.closed = closed

    def log(self, message):
        line = datetime.now(timezone.utc).isoformat() + ' ' + message
        plain_path(route(self.root,'profile-log.txt'))
        with (route(self.root,'profile-log.txt')).open('a', encoding='utf-8') as out:
            out.write(line + '\n')
        print(message, flush=True)

    def parked(self, key):
        if key == 'normal':
            return self.root / 'normal' / 'Save'
        if not valid_key(key):
            raise ProfileError('Invalid profile identity.')
        return self.root / 'slots' / key / 'Save'

    def validate_tree(self):
        for p in (self.user, self.root, self.active, self.state, self.journal):
            plain_path(p)
        # Reject linked profile ancestors before any reads/mutations.
        for name in ('normal', 'slots', 'backups'):
            base = self.root / name
            plain_path(base)
            if base.is_dir():
                for child in base.iterdir():
                    plain_path(child)

    def check_marker(self, path, key):
        plain_path(path)
        if not path.is_dir():
            raise ProfileError('Missing save folder: ' + str(path))
        marker = path / MARKER
        plain_path(marker)
        if key == 'normal':
            if marker.exists():
                raise ProfileError('AP marker found in a normal save; refusing to relabel it.')
        elif not marker.is_file() or marker.read_text(encoding='utf-8').strip() != key:
            raise ProfileError('Save marker does not match its expected profile; nothing overwritten.')

    def current(self):
        if not self.state.exists():
            if (self.active / MARKER).exists() or self.parked('normal').exists():
                raise ProfileError('Profile state is missing but profile data exists. Stop and send the log.')
            return 'normal'
        key = self.state.read_text(encoding='utf-8').strip()
        if key != 'normal' and not valid_key(key):
            raise ProfileError('Profile state is invalid.')
        return key

    def validate_current(self):
        key = self.current()
        self.check_marker(self.active, key)
        if self.parked(key).exists():
            raise ProfileError('Both active and parked copies exist; refusing to overwrite either.')
        return key

    def backup_normal(self):
        before = inventory(self.active)
        stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '-' + uuid.uuid4().hex[:10]
        parent = self.root / 'backups'
        parent.mkdir(exist_ok=True)
        staging = parent / (stamp + '.partial')
        final = parent / stamp
        shutil.copytree(self.active, staging / 'Save')
        if inventory(staging / 'Save') != before or inventory(self.active) != before:
            raise ProfileError('Normal save changed during backup. Original was not moved; close all writers and retry.')
        atomic_json(staging / 'inventory.json', before)
        staging.rename(final)
        atomic_json(self.root / 'normal-baseline.json', {'backup': stamp, 'files': before})
        self.log('Normal save backed up and SHA256 verified: ' + str(final / 'Save'))

    def verify_normal(self):
        baseline_path = self.root / 'normal-baseline.json'
        plain_path(baseline_path)
        if not baseline_path.exists():
            raise ProfileError('No normal-save baseline yet. Use the AP profile once to create one.')
        baseline = json.loads(baseline_path.read_text(encoding='utf-8'))
        path = self.active if self.current() == 'normal' else self.parked('normal')
        self.check_marker(path, 'normal')
        actual = inventory(path)
        expected = baseline['files']
        changed = sorted(k for k in set(actual) | set(expected) if actual.get(k) != expected.get(k))
        if changed:
            self.log('Normal save differs from the last pre-AP baseline: ' + ', '.join(changed))
            return False
        self.log('NORMAL SAVE UNCHANGED: all ' + str(len(actual)) + ' files match the pre-AP SHA256 baseline.')
        return True

    def request(self, mod):
        mod = Path(mod)
        token = (route(mod,'active-session.txt')).read_text(encoding='utf-8').strip()
        request = json.loads((route(mod,'profile-request.json')).read_text(encoding='utf-8'))
        identity = request.get('identity')
        if not re.fullmatch(r'[0-9a-f]{32}', token) or request.get('schema') != 1 or request.get('token') != token:
            raise ProfileError('Start the v0.12 or newer bridge and wait for a validated connection first.')
        if (not isinstance(identity, list) or len(identity) != 3 or not isinstance(identity[0], str)
            or not all(isinstance(x, int) and not isinstance(x, bool) for x in identity[1:])
            or request.get('key') != profile_key(identity)):
            raise ProfileError('Invalid seed/team/slot profile identity.')
        age = time.time() - (route(mod,'bridge-heartbeat.txt')).stat().st_mtime
        if not -5 <= age <= 15:
            raise ProfileError('Bridge heartbeat is stale. Reconnect before selecting the AP profile.')
        return request

    def prepare_ap(self, request):
        key = request['key']
        target = self.parked(key)
        parent = target.parent
        if parent.exists():
            plain_path(parent)
            identity_file = parent / 'identity.json'
            plain_path(identity_file)
            if not identity_file.exists() or json.loads(identity_file.read_text(encoding='utf-8')) != request['identity']:
                raise ProfileError('Existing AP profile identity is missing or mismatched.')
            if not target.exists():
                raise ProfileError('Existing AP profile save is missing; refusing to replace it with an empty save.')
        else:
            parent.mkdir(parents=True)
            atomic_json(parent / 'identity.json', request['identity'])
        if not target.exists():
            target.mkdir()
            atomic_text(target / MARKER, key + '\n')
        self.check_marker(target, key)
        return key

    def move(self, source, dest):
        if dest.exists() or dest.is_symlink():
            raise ProfileError('Destination already exists; nothing overwritten: ' + str(dest))
        dest.parent.mkdir(parents=True, exist_ok=True)
        source.rename(dest)

    def switch(self, target, request=None):
        old = self.validate_current()
        if old == target:
            self.log('Already using ' + ('NORMAL save.' if old == 'normal' else 'this AP seed/slot save.'))
            return
        if old == 'normal':
            self.backup_normal()
        if target != 'normal':
            if request is None or request['key'] != target:
                raise ProfileError('Validated bridge identity required.')
            self.prepare_ap(request)
        destination = self.parked(target)
        self.check_marker(destination, target)
        inventory(destination)  # Validate nested paths before moving the directory.
        self.closed()  # Recheck after potentially lengthy backup.
        atomic_json(self.journal, {'schema': 1, 'from': old, 'to': target})
        self.move(self.active, self.parked(old))
        self.move(destination, self.active)
        atomic_text(self.state, target + '\n')
        self.journal.unlink()
        self.log('Switched to ' + ('NORMAL save.' if target == 'normal' else 'AP save ' + target[:12] + '.'))
        if target == 'normal':
            self.verify_normal()

    def recover(self):
        if not self.journal.exists():
            self.validate_current()
            self.log('No interrupted switch to recover.')
            return
        journal = json.loads(self.journal.read_text(encoding='utf-8'))
        if journal.get('schema') != 1:
            raise ProfileError('Unrecognized switch journal.')
        old, target = journal['from'], journal['to']
        if old == target:
            raise ProfileError('Invalid switch journal.')
        before, after = self.parked(old), self.parked(target)
        if self.state.exists() and self.state.read_text(encoding='utf-8').strip() not in (old, target):
            raise ProfileError('Profile state conflicts with recovery journal; nothing overwritten.')
        # A journal only records a requested move; no save tree is deleted in recovery.
        situation = (self.active.exists(), before.exists(), after.exists())
        if situation == (True, False, True):
            self.check_marker(self.active, old)
            self.check_marker(after, target)
            atomic_text(self.state, old + '\n')
            self.journal.unlink()
            self.log('Recovered: switch had not moved any save; kept previous profile.')
            return
        if situation == (False, True, True):
            self.check_marker(before, old); self.check_marker(after, target)
            self.move(after, self.active)
        elif situation == (True, True, False):
            self.check_marker(before, old); self.check_marker(self.active, target)
        else:
            raise ProfileError('Ambiguous interrupted switch. Nothing overwritten. Do not launch the game; send profile-log.txt and switch-journal.json.')
        atomic_text(self.state, target + '\n')
        self.journal.unlink()
        self.log('Recovered interrupted switch; active profile: ' + target)

    def run(self, action, mod=None, cloud_disabled=False):
        self.closed()
        self.validate_tree()
        if not self.user.is_dir():
            raise ProfileError('Game User folder not found.')
        with process_guard(self.root):
            self.validate_tree()
            if action == 'recover':
                self.recover(); return
            if self.journal.exists():
                raise ProfileError('Interrupted switch detected. Run Recover while the game stays closed.')
            current = self.validate_current()
            if action == 'status':
                self.log('Active profile: ' + current)
            elif action == 'verify':
                if not self.verify_normal():
                    raise ProfileError('Verification differs; no automatic restore performed. Normal play after restoring can legitimately change this baseline.')
            elif action == 'normal':
                self.switch('normal')
            elif action == 'ap':
                ack = self.root / 'cloud-disabled-ack.txt'
                plain_path(ack)
                if not cloud_disabled and not ack.exists():
                    raise ProfileError('Disable Steam Cloud for BallisticNG first; then acknowledge with --cloud-disabled or the menu prompt.')
                if cloud_disabled:
                    atomic_text(ack, 'User confirmed Steam Cloud disabled for profile testing. Keep it disabled while AP profiles are used.\n')
                request = self.request(mod)
                self.switch(request['key'], request)
                self.log('AP profile ready for seed ' + request['identity'][0] + ', team ' + str(request['identity'][1]) + ', slot ' + str(request['identity'][2]) + '.')
            else:
                raise ProfileError('Unknown action.')

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', nargs='?', choices=['ap', 'normal', 'status', 'verify', 'recover'])
    parser.add_argument('--mod-dir', default=DEFAULT_MOD)
    parser.add_argument('--cloud-disabled', action='store_true')
    args = parser.parse_args()
    if not args.action:
        print('Close BallisticNG before using this menu. Keep Steam Cloud OFF for these profiles.')
        args.mod_dir = input('Installed mod folder [Enter for default G: path]: ').strip().strip('"') or DEFAULT_MOD
        print('1. Use connected AP seed/slot save\n2. Return to normal save\n3. Show active profile\n4. Verify normal save unchanged\n5. Recover interrupted switch')
        args.action = {'1':'ap','2':'normal','3':'status','4':'verify','5':'recover'}.get(input('Choose 1-5: ').strip())
        if not args.action:
            raise ProfileError('No action selected.')
    mod = Path(args.mod_dir).absolute()
    game = mod.parents[3]
    if not (game / 'BallisticNG.exe').is_file() or not (mod / 'BallisticNGArchipelago.cs').is_file():
        raise ProfileError('That is not the installed Windows BallisticNG mod folder.')
    profiles = Profiles(game)
    if args.action == 'ap' and not args.cloud_disabled and not (profiles.root / 'cloud-disabled-ack.txt').exists():
        answer = input('Disable Steam Cloud in BallisticNG Properties first. Type OFF once done: ').strip()
        if answer != 'OFF':
            raise ProfileError('No save switch made. Disable cloud synchronization before this test.')
        args.cloud_disabled = True
    profiles.run(args.action, mod, args.cloud_disabled)
    print('Profile log:', route(profiles.root,'profile-log.txt'))

if __name__ == '__main__':
    try:
        main()
    except (ProfileError, OSError, ValueError, KeyError, IndexError, subprocess.SubprocessError) as ex:
        print('STOPPED:', ex, file=sys.stderr)
        print('Do not launch the game if a switch-journal.json exists. Use Recover first.', file=sys.stderr)
        sys.exit(1)
