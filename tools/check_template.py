"""Reject stale custom-option descriptions/defaults without requiring AP to build a ZIP."""
import ast,inspect,re
from pathlib import Path

def validate_template(root):
    root=Path(root)
    tree=ast.parse((root/'ballisticng/options.py').read_text())
    classes={node.name:node for node in tree.body if isinstance(node,ast.ClassDef)}
    text=(root/'BallisticNG.yaml').read_text(encoding='utf-8-sig')
    keys=list(re.finditer(r'^  ([a-z0-9_]+):\s*$',text,re.M))
    blocks={key[1]:text[key.end():keys[i+1].start() if i+1<len(keys) else len(text)] for i,key in enumerate(keys)}
    if [match[1] for match in keys][:2]!=['progression_balancing','accessibility']:raise ValueError('Common option order changed')
    for name,expected in {'progression_balancing':'normal','accessibility':'full'}.items():
        weights=re.findall(r"^    '?([a-z0-9_-]+)'?:\s*(\d+)\s*(?:#.*)?$",blocks[name],re.M)
        if [key for key,weight in weights if int(weight)>0]!=[expected]:raise ValueError('Stale common default: '+name)
    count=0
    for field in classes['BallisticNGOptions'].body:
        if not isinstance(field,ast.AnnAssign):continue
        name=field.target.id;cls=classes[field.annotation.id];body=blocks[name]
        comments='\n'.join(line[6:] for line in body.splitlines() if line.startswith('    # '))
        normalize=lambda value:' '.join(value.split())
        if normalize(comments)!=normalize(inspect.cleandoc(ast.get_docstring(cls,clean=False))):raise ValueError('Stale template description: '+name)
        assignments={node.targets[0].id:ast.literal_eval(node.value) for node in cls.body if isinstance(node,ast.Assign) and isinstance(node.value,ast.Constant)}
        base=cls.bases[0].id
        default=assignments.get('default',1 if base=='DefaultOnToggle' else 0)
        if base in ('Toggle','DefaultOnToggle'):expected='true' if default else 'false'
        else:expected=next(key[len('option_'):] for key,value in assignments.items() if key.startswith('option_') and value==default)
        weights=re.findall(r"^    '?([a-z0-9_]+)'?:\s*(\d+)\s*$",body,re.M)
        if [key for key,weight in weights if int(weight)>0]!=[expected]:raise ValueError('Stale template default: '+name)
        count+=1
    return count
if __name__=='__main__':print('Template descriptions/defaults verified:',validate_template(Path(__file__).resolve().parents[1]))
