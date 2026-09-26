# -*- coding: utf-8 -*-
# Repair ogg keysounds that FMOD Ex refuses to open (ERR_FILE_COULDNOTSEEK).
# The trigger is the original 45-byte zero-comment packet combined with the
# vorbis setup header split across two ogg pages. Remuxing the stream losslessly
# (copy packets, rewrite page layout + granules) makes the file loadable.
import subprocess, sys, os, tempfile, shutil

def remux_in_place(path):
    tmp = tempfile.NamedTemporaryFile(suffix='.ogg', delete=False)
    tmp.close()
    try:
        r = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-c', 'copy', '-y', tmp.name],
                           capture_output=True, text=True)
        if r.returncode != 0:
            print(f"ERROR remuxing {path}: {r.stderr}")
            return False
        old_size = os.path.getsize(path)
        shutil.move(tmp.name, path)
        print(f"REMUXED {path} ({old_size} -> {os.path.getsize(path)} bytes)")
        return True
    finally:
        if os.path.exists(tmp.name):
            os.unlink(tmp.name)

if __name__ == '__main__':
    ok = all([remux_in_place(p) for p in sys.argv[1:]])
    sys.exit(0 if ok else 1)
