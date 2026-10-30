"""Build matching source/player archives from this checkout; no game installation needed."""
import hashlib,io,json,re,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
VERSION='0.37.4'

def source_files():
    paths=[]
    for f in ROOT.iterdir():
        if f.is_file() and (f.suffix in ('.py','.md') or f.name in {'LICENSE','.gitignore','manifest.json','requirements.txt','BallisticNG.yaml','START-HERE.txt'}):paths.append(f)
    for folder in ('BallisticNGArchipelago','ballisticng','docs','tools','licenses','.github'):
        for f in (ROOT/folder).rglob('*'):
            if not f.is_file() or '__pycache__' in f.parts:continue
            rel=f.relative_to(ROOT).as_posix()
            if folder=='BallisticNGArchipelago':
                allowed=(f.suffix in {'.cs','.cmd'} or
                    (rel.startswith('BallisticNGArchipelago/PanelRuntime/') and f.suffix=='.py') or
                    rel in {'BallisticNGArchipelago/PanelRuntime/manifest.json','BallisticNGArchipelago/PanelRuntime/requirements.txt','BallisticNGArchipelago/lib/0Harmony.dll'})
            elif folder=='ballisticng':allowed=f.suffix=='.py' or f.name=='manifest.json'
            elif folder=='tools':allowed=f.suffix=='.py'
            elif folder=='.github':allowed=f.suffix in {'.yml','.yaml'}
            else:allowed=f.suffix in {'.md','.txt'}
            if allowed:paths.append(f)
    return sorted(set(paths))

def validate():
    from check_template import validate_template
    validate_template(ROOT)
    mod=ROOT/'BallisticNGArchipelago';runtime=mod/'PanelRuntime'
    for f in runtime.glob('*.py'):
        original=ROOT/f.name
        if not original.is_file() or f.read_bytes()!=original.read_bytes():raise ValueError('Runtime/source mismatch: '+f.name)
    for f in (runtime/'requirements.txt',runtime/'manifest.json',ROOT/'ballisticng/manifest.json'):
        original=ROOT/f.name
        if f.read_bytes()!=original.read_bytes():raise ValueError('Manifest/dependency mismatch: '+str(f))
    manifest=json.loads((ROOT/'manifest.json').read_text())
    if manifest['digest'] not in (mod/'Catalog.cs').read_text():raise ValueError('C# catalog digest mismatch')
    dll=hashlib.sha256((mod/'lib/0Harmony.dll').read_bytes()).hexdigest()
    if dll!='9611251f080e4855cd9d5fa54b5e56034df25b70b374c49ba09550b08a1bf875':raise ValueError('Unexpected Harmony binary')

def build():
    validate();out=ROOT/'dist';out.mkdir(exist_ok=True);files=source_files()
    hashes={f.relative_to(ROOT).as_posix():hashlib.sha256(f.read_bytes()).hexdigest() for f in files}
    source=out/f'BallisticNG-AP-v{VERSION}-Source.zip'
    with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as z:
        for f in files:z.write(f,f.relative_to(ROOT))
        z.writestr('SOURCE-MANIFEST.json',json.dumps(hashes,indent=2)+'\n')
    world=io.BytesIO()
    with zipfile.ZipFile(world,'w',zipfile.ZIP_DEFLATED) as z:
        for f in files:
            if f.relative_to(ROOT).parts[0]=='ballisticng':z.write(f,f.relative_to(ROOT))
    player=out/f'BallisticNG-AP-v{VERSION}.zip';prefix=f'BallisticNG-AP-v{VERSION}/'
    with zipfile.ZipFile(player,'w',zipfile.ZIP_DEFLATED) as z:
        for f in files:
            rel=f.relative_to(ROOT).as_posix()
            if rel.startswith('BallisticNGArchipelago/') or rel in {'START-HERE.txt','BallisticNG.yaml','LICENSE','AI-DISCLOSURE.md','THIRD-PARTY.md','RELEASE-NOTES.md'}:z.write(f,prefix+rel)
        z.write(ROOT/'licenses/HARMONY-LICENSE.txt',prefix+'HARMONY-LICENSE.txt')
        z.writestr(prefix+'ballisticng.apworld',world.getvalue())
        z.write(source,prefix+'Developer-Source.zip')
    # Verify all distributed C#/Python/assets match the corresponding source bytes.
    with zipfile.ZipFile(player) as z,zipfile.ZipFile(source) as s:
        for name in z.namelist():
            rel=name[len(prefix):]
            if rel in hashes:
                if hashlib.sha256(z.read(name)).hexdigest()!=hashes[rel] or z.read(name)!=s.read(rel):raise ValueError('Source/release mismatch: '+rel)
    return player,source,len(hashes)
if __name__=='__main__':
    player,source,count=build()
    print('Verified',count,'source files; matching player and source archives:')
    print(player);print(source)
