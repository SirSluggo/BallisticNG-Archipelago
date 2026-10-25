from runtime_files import route
"""Durable per-receipt trap delivery; effect starts are acknowledged by the game."""
import re


def receipt_key(index,item):
    return ':'.join(str(value) for value in (index,item['item'],item['location'],item['player']))


def pending_traps(state,trap_items,enabled):
    if not enabled:return []
    by_id={row['id']:row['kind'] for row in trap_items.values()}
    consumed=set(state.get('trap_consumed',[]))
    return [(receipt_key(index,item),by_id[item['item']])
            for index,item in enumerate(state['items'])
            if item['item'] in by_id and receipt_key(index,item) not in consumed]


def read_acknowledgments(mod,state,trap_items):
    token=state.get('trap_session');digest=state.get('trap_digest')
    if not isinstance(token,str) or not re.fullmatch('[0-9a-f]{32}',token):return set()
    if not isinstance(digest,str) or not re.fullmatch('[0-9a-f]{64}',digest):return set()
    path=route(mod,'trap-acks-'+token+'.txt')
    if not path.exists() or path.stat().st_size>131072:return set()
    lines=path.read_text(encoding='utf-8-sig').splitlines()
    if len(lines)<2 or lines[:2]!=[token,digest]:return set()
    valid={key for key,kind in pending_traps(dict(state,trap_consumed=[]),trap_items,True)}
    return set(lines[2:]) & valid
