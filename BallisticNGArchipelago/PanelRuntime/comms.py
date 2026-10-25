from runtime_files import route
"""Bounded, plain-text AP chat/hints UI. Commands are explicit and session scoped."""
import base64
import binascii
import re
import time


def clean_text(value,limit=1600):
    return ''.join(c for c in str(value) if c=='\t' or ord(c)>=32).replace('\t',' ')[:limit]


def encode(value):return base64.b64encode(str(value).encode('utf-8')).decode('ascii')


class Comms:
    def __init__(self,bridge,manifest,item_names,goal_locations,atomic_text):
        self.b=bridge;self.m=manifest;self.item_names=item_names;self.goal=goal_locations;self.write=atomic_text
        self.logs=[];self.hints=[];self.points=None;self.percent=None;self.total=None
        self.published=None;self.seen_commands=set();self.last_command_at=-100.0
        self.location_maps={'BallisticNG':{row['id']:row['name'] for row in bridge.locations}}
        self.room=None

    def log(self,category,value):
        self.logs.append((category,value));self.logs=self.logs[-160:]

    def observe(self,packet):
        cmd=packet.get('cmd')
        if cmd=='RoomInfo':
            room=packet.get('seed_name')
            if room!=self.room:self.logs=[];self.hints=[]
            self.room=room;self.points=None;self.percent=None;self.total=None
            self.log('SYSTEM','Connecting to Archipelago. F3: Chat, Hints & Log.')
        if cmd in ('RoomInfo','RoomUpdate','Connected'):
            if type(packet.get('hint_points')) is int:self.points=packet['hint_points']
            if type(packet.get('hint_cost')) is int and packet['hint_cost']>=0:self.percent=packet['hint_cost']
        if cmd=='RoomUpdate' and isinstance(packet.get('checked_locations'),list):
            for code in packet['checked_locations']:
                if type(code) is int and code in self.b.checks.values() and code not in self.b.checked:
                    self.log('CHECK','Check confirmed: '+self.lookup('location_id',code,self.b.slot))
        if cmd=='Connected':
            missing=packet.get('missing_locations');checked=packet.get('checked_locations')
            if isinstance(missing,list) and isinstance(checked,list):
                self.total=len({x for x in missing+checked if type(x) is int})
            self.hints=[]
        elif cmd=='PrintJSON' and isinstance(packet.get('data'),list):
            category='HINT' if packet.get('type')=='Hint' else 'ITEM' if packet.get('type')=='ItemSend' else 'CHAT'
            # Keep parts so a later data package can resolve cross-game names.
            self.log(category,packet['data'][:256])
        elif cmd=='Print' and isinstance(packet.get('text'),str):self.log('CHAT',clean_text(packet['text']))
        elif cmd=='DataPackage':
            games=packet.get('data',{}).get('games',{}) if isinstance(packet.get('data'),dict) else {}
            if isinstance(games,dict):
                for game,package in games.items():
                    if not isinstance(package,dict):continue
                    mapping=package.get('location_name_to_id')
                    if isinstance(mapping,dict):self.location_maps[game]={v:k for k,v in mapping.items() if isinstance(k,str) and type(v) is int}

    def set_hints(self,value):
        if not isinstance(value,list):return
        self.hints=[dict(h) for h in value[:512] if isinstance(h,dict) and
            type(h.get('finding_player')) is int and type(h.get('receiving_player')) is int and
            type(h.get('item')) is int and type(h.get('location')) is int and
            self.b.slot in (h['finding_player'],h['receiving_player'])]

    def lookup(self,kind,code,player):
        try:code=int(code);player=int(player)
        except (TypeError,ValueError):return clean_text(code)
        if kind=='player_id':return self.b.player_names.get(code,'Player '+str(code))
        game=self.b.slot_games.get(player)
        maps=self.b.item_maps if kind=='item_id' else self.location_maps
        return maps.get(game,{}).get(code,('Item ' if kind=='item_id' else 'Location ')+str(code))

    def render(self,value):
        if isinstance(value,str):return clean_text(value)
        pieces=[]
        for node in value:
            if not isinstance(node,dict):continue
            kind=node.get('type','text');text=node.get('text','')
            if kind in ('player_id','item_id','location_id'):text=self.lookup(kind,text,node.get('player',self.b.slot))
            pieces.append(str(text))
        return clean_text(''.join(pieces))

    def hint_text(self,h):
        recipient=self.b.player_names.get(h['receiving_player'],'Player '+str(h['receiving_player']))
        owner=self.b.player_names.get(h['finding_player'],'Player '+str(h['finding_player']))
        found=h.get('found') is True or h.get('status')==40 or (h['finding_player']==self.b.slot and h['location'] in self.b.checked)
        item=self.lookup('item_id',h['item'],h['receiving_player'])
        loc=self.lookup('location_id',h['location'],h['finding_player'])
        return ('FOUND' if found else 'UNCHECKED')+' | '+recipient+': '+item+' — '+loc+' ('+owner+")"+((' via '+str(h['entrance'])) if h.get('entrance') else '')

    def publish(self):
        b=self.b;valid=set(b.checks.values());total=len(valid)
        cost=-1 if self.percent is None else (max(1,int(self.percent*.01*(self.total if self.total is not None else total))) if self.percent else 0)
        lines=['BNG-COMMS-1',self.m['digest'],b.client_id,b.token or '-',str(int(b.ready)),encode(b.name),
               str(len(b.checked&valid)),str(total),str(b.required_medal),str(len(b.checked&self.goal)),
               str(self.points if self.points is not None else -1),str(cost)]
        lines+=['L\t'+kind+'\t'+encode(self.render(value)) for kind,value in self.logs]
        lines+=['H\t0\t'+encode(clean_text(self.hint_text(h))) for h in self.hints]
        lines+=['I\t'+str(int(code in self.b.progression_ids))+'\t'+encode(name) for code,name in sorted(self.item_names.items(),key=lambda pair:pair[1])]
        content='\n'.join(lines)+'\n';path=route(b.mod,'comms-state.txt')
        if content!=self.published or not path.exists():self.write(path,content);self.published=content

    async def command(self,ws):
        path=route(self.b.mod,'comms-command.txt')
        if not path.exists():return
        if path.stat().st_size>8192:
            path.unlink(missing_ok=True);self.log('SYSTEM','Chat request rejected: too large.');return
        try:lines=path.read_text(encoding='utf-8-sig').splitlines()
        except UnicodeError:
            path.unlink(missing_ok=True);self.log('SYSTEM','Invalid chat text; request rejected.');return
        # Consume before sending. Never replay an ambiguous send after a network failure.
        path.unlink(missing_ok=True)
        if len(lines)!=4 or lines[0]!=self.b.client_id or lines[1]!=self.b.token or not self.b.ready:
            self.log('SYSTEM','Chat request expired or disconnected; it was not sent.');return
        request=lines[2]
        if not re.fullmatch('[0-9a-f]{32}',request) or request in self.seen_commands:return
        self.seen_commands.add(request)
        if len(self.seen_commands)>512:self.seen_commands={request}
        try:text=base64.b64decode(lines[3],validate=True).decode('utf-8')
        except (binascii.Error,UnicodeError):self.log('SYSTEM','Invalid chat text; request rejected.');return
        if not text.strip() or len(text)>500 or any(ord(c)<32 for c in text):
            self.log('SYSTEM','Use one line of 1–500 characters.');return
        now=time.monotonic()
        if now-self.last_command_at<1:self.log('SYSTEM','Please wait one second between requests.');return
        self.last_command_at=now
        try:await self.b.send(ws,cmd='Say',text=text.strip())
        except Exception:
            self.log('SYSTEM','Connection interrupted during send. Delivery is uncertain; this request will not be replayed.')
            raise
        self.log('SENT','You: '+text.strip())
