"""Reviewable, backed-up recovery of a server-authoritative AP item stream."""
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import uuid


class HistoryConflict(ValueError):
    pass


class HistorySyncRequired(ValueError):
    pass


def normalized(items):
    if not isinstance(items, list) or len(items) > 100000:
        raise ValueError('Invalid received-item packet')
    result = []
    for value in items:
        if not isinstance(value, dict):
            raise ValueError('Invalid received-item packet')
        row = {key: value.get(key, 0) if key == 'flags' else value.get(key)
               for key in ('item', 'location', 'player', 'flags')}
        if any(type(v) is not int for v in row.values()) or row['player'] < 0 or row['flags'] < 0:
            raise ValueError('Invalid received-item packet')
        result.append(dict(row, **{'class': 'NetworkItem'}))
    return result


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def backup_state(path):
    """Preserve exact bytes, never overwrite a backup, and fail closed on write failure."""
    path = Path(path)
    directory = path.parent / 'recovery-backups'
    directory.mkdir(parents=True, exist_ok=True)
    name = path.stem + '-' + datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ') + '-' + uuid.uuid4().hex[:12] + '.json'
    target = directory / name
    raw = path.read_bytes()
    with target.open('xb') as stream:
        stream.write(raw)
        stream.flush()
        os.fsync(stream.fileno())
    if target.read_bytes() != raw:
        raise OSError('Recovery backup verification failed')
    return target


def review(state, items, item_names, permanent_ids, backup):
    old = normalized(state['items'])
    new = normalized(items)
    old_counts = Counter(row['item'] for row in old)
    new_counts = Counter(row['item'] for row in new)
    old_owned = set(old_counts) | set(permanent_ids)
    new_owned = set(new_counts) | set(permanent_ids)
    label = lambda code: item_names.get(code, 'Unknown item ' + str(code))
    lost = sorted(label(code) for code in old_owned - new_owned)
    gained = sorted(label(code) for code in new_owned - old_owned)
    lines = [f'Cached receipts: {len(old)}. Server receipts: {len(new)}.',
             'No longer owned: ' + (', '.join(lost) or 'None.'),
             'Newly owned: ' + (', '.join(gained) or 'None.'),
             'Receipt count decreases: ' + (', '.join(f'{label(code)} (-{count})' for code, count in sorted((old_counts-new_counts).items())) or 'None.'),
             f'Pending checks retained: {len(state.get("pending", []))}.',
             'The server history will replace the cached history. Game medals and save profiles are not changed.',
             'Backup: recovery-backups/' + Path(backup).name]
    body = {'identity': state['identity'], 'base_digest': digest(state), 'items': new,
            'disk_digest': digest(json.loads(Path(backup).read_text()))}
    return dict(body, id=digest(body), text='\n'.join(lines), backup=str(backup))


def can_apply(proposal, state, items):
    return (proposal is not None and proposal['identity'] == state['identity']
            and proposal['base_digest'] == digest(state)
            and proposal['items'] == normalized(items))
