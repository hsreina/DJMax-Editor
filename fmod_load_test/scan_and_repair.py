# -*- coding: utf-8 -*-
# Scan a song folder for ogg keysounds that FMOD Ex cannot open (the files are
# then silent in the editor), repair them by lossless remux, and re-verify.
#
# Usage:
#   python scan_and_repair.py "G:\path\to\song folder"
#
# Needs: FmodLoadTest.exe next to this script, and ffmpeg in PATH.
import os, re, subprocess, sys, tempfile, shutil
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
TESTER = os.path.join(HERE, 'FmodLoadTest.exe')
FAIL_RE = re.compile(r'^(.*?)\s+FAIL:')


def scan(target):
    """Return {dir: [failed filenames]} using the FMOD test harness."""
    by_dir = defaultdict(list)
    for root, _dirs, files in os.walk(target):
        oggs = [f for f in files if f.lower().endswith('.ogg')]
        if oggs:
            by_dir[root] = oggs

    failures = defaultdict(list)
    for root, oggs in sorted(by_dir.items()):
        listfile = tempfile.NamedTemporaryFile('w', suffix='.txt', delete=False)
        listfile.write('\n'.join(oggs))
        listfile.close()
        try:
            r = subprocess.run([TESTER, root, listfile.name],
                               capture_output=True, text=True, timeout=600)
            for line in r.stdout.splitlines():
                m = FAIL_RE.match(line)
                if m and m.group(1).strip():
                    failures[root].append(m.group(1).strip())
        finally:
            os.unlink(listfile.name)
        total = sum(len(v) for v in by_dir.values())
        done = sum(len(v) for v in failures.values())
        print(f'scanned {total:>5} files...', end='\r')
    print()
    return failures


def remux(path):
    tmp = tempfile.NamedTemporaryFile(suffix='.ogg', delete=False)
    tmp.close()
    try:
        r = subprocess.run(['ffmpeg', '-v', 'error', '-i', path, '-c', 'copy', '-y', tmp.name],
                           capture_output=True, text=True)
        if r.returncode != 0:
            print(f'  ERROR remuxing {path}: {r.stderr.strip()}')
            return False
        shutil.move(tmp.name, path)
        return True
    finally:
        if os.path.exists(tmp.name):
            os.unlink(tmp.name)


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(2)
    target = sys.argv[1]
    if not os.path.isdir(target):
        print(f'not a directory: {target}')
        sys.exit(2)
    if not shutil.which('ffmpeg'):
        print('ffmpeg not found in PATH')
        sys.exit(2)

    print(f'scanning {target} ...')
    failures = scan(target)
    bad = [(d, f) for d, fs in failures.items() for f in fs]
    if not bad:
        print('no broken keysounds found. nothing to do.')
        return

    print(f'\nfound {len(bad)} broken file(s):')
    for d, f in bad:
        print(f'  {os.path.join(d, f)}')

    fixed, still_broken = [], []
    for d, f in bad:
        p = os.path.join(d, f)
        print(f'repairing {p} ...', end=' ')
        (fixed if remux(p) else still_broken).append(p)
        print('ok' if p in fixed else 'FAILED')

    if fixed:
        print('\nre-verifying repaired files with FMOD ...')
        vf = defaultdict(list)
        for p in fixed:
            vf[os.path.dirname(p)].append(os.path.basename(p))
        for root, files in vf.items():
            listfile = tempfile.NamedTemporaryFile('w', suffix='.txt', delete=False)
            listfile.write('\n'.join(files))
            listfile.close()
            try:
                r = subprocess.run([TESTER, root, listfile.name],
                                   capture_output=True, text=True, timeout=600)
                for line in r.stdout.splitlines():
                    if 'FAIL' in line:
                        still_broken.append(os.path.join(root, line.split()[0]))
            finally:
                os.unlink(listfile.name)

    print(f'\n==== summary: {len(fixed)} fixed, {len(still_broken)} still broken ====')
    for p in still_broken:
        print(f'  STILL BROKEN: {p}')
    sys.exit(1 if still_broken else 0)


if __name__ == '__main__':
    main()
