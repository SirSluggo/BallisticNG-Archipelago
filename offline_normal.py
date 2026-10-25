from runtime_files import route,rotate
"""Return to the normal save after game exit, without opening a network connection."""
import asyncio
from pathlib import Path
from bridge import atomic_text
from bridge_lock import BridgeLock
from maintenance import cleanup_sessions
from panel_client import wait_for_exit,switch_and_launch

async def return_normal(mod,wait=wait_for_exit,switch=switch_and_launch,lock_timeout=25):
    mod=Path(mod)
    await wait()
    # A disconnected panel client may still be shutting down. It exits after
    # observing the closed game; wait for its OS lock rather than trusting status.
    deadline=asyncio.get_running_loop().time()+lock_timeout
    while True:
        try:
            lock=BridgeLock(mod)
            break
        except (FileExistsError,PermissionError):
            if asyncio.get_running_loop().time()>=deadline:raise
            await asyncio.sleep(.25)
    try:
        await asyncio.to_thread(cleanup_sessions,mod,mod/'PanelRuntime/bridge-state')
        return await asyncio.to_thread(switch,mod,'normal')
    finally:
        lock.release()

def main():
    mod=Path(__file__).resolve().parents[1]
    try:
        asyncio.run(return_normal(mod))
    except Exception as error:
        # No server fields/passwords are accepted by this helper.
        rotate(route(mod,'offline-normal.log'))
        with (route(mod,'offline-normal.log')).open('a',encoding='utf-8') as log:
            log.write(type(error).__name__+': '+str(error)+'\n')
        atomic_text(route(mod,'panel-status.txt'),'\nerror\n\nOffline return stopped. See offline-normal.log. No fallback save was created.\n')
        raise SystemExit(1)
if __name__=='__main__':main()
