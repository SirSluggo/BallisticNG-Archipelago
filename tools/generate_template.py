"""Generate the bundled YAML with Archipelago's own template generator."""
import argparse,importlib.util,sys,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def generate(ap_source,check=False):
    sys.path.insert(0,str(Path(ap_source).resolve()))
    import Utils
    Utils.local_path.cached_path=str(Path(ap_source).resolve())
    from Options import generate_yaml_templates
    from worlds import AutoWorldRegister
    path=ROOT/'ballisticng/__init__.py'
    spec=importlib.util.spec_from_file_location('worlds.ballisticng',path,submodule_search_locations=[str(path.parent)])
    module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module;spec.loader.exec_module(module)
    # Operate only inside a private temporary directory; the AP generator clears
    # YAMLs in its output directory before writing templates.
    registry=AutoWorldRegister.world_types.copy()
    try:
        AutoWorldRegister.world_types.clear();AutoWorldRegister.world_types['BallisticNG']=module.BallisticNGWorld
        with tempfile.TemporaryDirectory() as t:
            generate_yaml_templates(t)
            data=(Path(t)/'BallisticNG.yaml').read_text(encoding='utf-8-sig').encode('utf-8-sig')
    finally:
        AutoWorldRegister.world_types.clear();AutoWorldRegister.world_types.update(registry)
    target=ROOT/'BallisticNG.yaml'
    if check:
        if target.read_bytes()!=data:raise SystemExit('Bundled YAML differs from the Archipelago-generated template. Regenerate it.')
        print('Bundled YAML matches the Archipelago-generated template byte for byte.')
    else:
        target.write_bytes(data);print('Generated:',target)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--ap-source',required=True);parser.add_argument('--check',action='store_true');args=parser.parse_args();generate(args.ap_source,args.check)
