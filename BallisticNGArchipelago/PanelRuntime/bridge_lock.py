"""Process-lifetime lock; the operating system releases ownership after a crash."""
import os
from pathlib import Path
MAGIC=b'BNG-OS-LOCK-1\n'
class BridgeLock:
    def __init__(self,mod,filename="bridge.lock"):
        self.path=Path(mod)/filename;self.file=None
        # Legacy clients use existence rather than OS locking. Never steal theirs.
        try:
            fd=os.open(self.path,os.O_CREAT|os.O_EXCL|os.O_RDWR|getattr(os,"O_BINARY",0))
            os.write(fd,MAGIC)
        except FileExistsError:
            if self.path.read_bytes().replace(b"\r\n",b"\n")!=MAGIC:
                raise FileExistsError('Legacy bridge.lock exists. Close old clients before removing it.')
            fd=os.open(self.path,os.O_RDWR|getattr(os,"O_BINARY",0))
        self.file=os.fdopen(fd,'r+b',buffering=0)
        try:
            if os.name=='nt':
                import msvcrt
                self.file.seek(0);msvcrt.locking(self.file.fileno(),msvcrt.LK_NBLCK,1)
            else:
                import fcntl
                fcntl.flock(self.file.fileno(),fcntl.LOCK_EX|fcntl.LOCK_NB)
        except OSError as ex:
            self.file.close();self.file=None
            raise FileExistsError('A background client is already running.') from ex
    def release(self):
        if self.file is not None:self.file.close();self.file=None
        # Keep the inode: unlinking permits two simultaneous lock owners.
