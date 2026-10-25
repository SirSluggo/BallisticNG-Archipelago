from runtime_files import route
"""First-connect setup, using only Python's standard library until the client starts."""
import os,subprocess,sys,uuid
from pathlib import Path
from bridge_lock import BridgeLock

def ensure_runtime(mod,run=subprocess.run):
    runtime=Path(mod)/'PanelRuntime'
    python=runtime/'.venv'/('Scripts/python.exe' if os.name=='nt' else 'bin/python')
    def command(args):
        result=run([str(x) for x in args],cwd=runtime)
        if result.returncode:raise RuntimeError('Python setup failed. See setup-panel.log.')
    if sys.version_info<(3,11):raise RuntimeError('Install Python 3.11 or newer, then try Connect again.')
    if not python.is_file():command([sys.executable,'-m','venv',runtime/'.venv'])
    # This also detects moved/broken virtual environments and missing dependencies.
    probe=run([str(python),'-c',"import sys,importlib.metadata as m; assert sys.version_info>=(3,11); v=int(m.version('websockets').split('.')[0]); assert 14<=v<17"],cwd=runtime,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    if probe.returncode:
        pip=run([str(python),'-m','pip','--version'],cwd=runtime,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
        if pip.returncode:command([python,'-m','ensurepip','--upgrade'])
        command([python,'-m','pip','install','-r',runtime/'requirements.txt'])
        command([python,'-c',"import websockets;import sys;assert sys.version_info>=(3,11)"])
    return python

def write_status(mod,nonce,state,message):
    temp=route(mod,'setup-status-'+nonce+'.tmp')
    temp.write_text(nonce+'\n'+state+'\n\n'+message+'\n',encoding='utf-8')
    os.replace(temp,route(mod,'panel-status.txt'))

def main():
    mod=Path(__file__).resolve().parents[1];nonce=uuid.uuid4().hex
    # Credentials are passed to the client through stdin only, never setup logs.
    payload=''.join(sys.stdin.readline() for _ in range(4))
    try:lock=BridgeLock(mod,'setup.lock')
    except (FileExistsError,PermissionError):return
    try:
        with (route(mod,'setup-panel.log')).open('w',encoding='utf-8') as log:
            write_status(mod,nonce,'setup','Preparing Python client. First setup may take a few minutes...')
            def run(args,**kwargs):
                kwargs.setdefault('stdout',log);kwargs.setdefault('stderr',subprocess.STDOUT)
                if os.name=='nt':kwargs['creationflags']=subprocess.CREATE_NO_WINDOW
                return subprocess.run(args,**kwargs)
            try:
                python=ensure_runtime(mod,run)
                result=run([str(python),'-u',str(mod/'PanelRuntime/panel_client.py')],cwd=mod.parents[3],input=payload,text=True)
                if result.returncode:raise RuntimeError('Client startup failed. See setup-panel.log and panel-bridge.log.')
            except Exception as error:
                # Setup command exceptions contain only local paths, never the input payload.
                log.write(type(error).__name__+': '+str(error)+'\n');log.flush()
                write_status(mod,nonce,'error','Could not prepare/start Python client. See setup-panel.log. Install Python 3.11+ and check internet access.')
    finally:lock.release()
if __name__=='__main__':main()
