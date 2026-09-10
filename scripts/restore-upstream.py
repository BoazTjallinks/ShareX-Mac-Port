#!/usr/bin/env python3
"""Restore the pinned upstream source offline, without overwriting user files."""
import hashlib
import json
import os
import shutil
import tarfile
import tempfile
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]

def main():
    lock = json.loads((ROOT / 'upstream.lock.json').read_text())
    archive = ROOT / lock['archive_path']
    if hashlib.sha256(archive.read_bytes()).hexdigest() != lock['archive_sha256']:
        raise SystemExit('Source archive hash mismatch. Nothing was extracted.')
    target = ROOT / 'reference' / 'ShareX'
    if target.exists():
        raise SystemExit('reference/ShareX already exists; left unchanged. Verify it or choose a fresh project copy.')
    target.parent.mkdir(exist_ok=True)
    stage = Path(tempfile.mkdtemp(prefix='sharex-restore-', dir=target.parent))
    try:
        with tarfile.open(archive, 'r:gz') as tar:
            for member in tar:
                name = PurePosixPath(member.name)
                if name.is_absolute() or '..' in name.parts or not name.parts or name.parts[0] != 'ShareX':
                    raise ValueError('Unsafe source archive path')
                dest = stage.joinpath(*name.parts)
                if member.isdir():
                    dest.mkdir(parents=True, exist_ok=True)
                elif member.isfile():
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    stream = tar.extractfile(member)
                    if stream is None:
                        raise ValueError('Unreadable archive member')
                    with stream, dest.open('xb') as output:
                        shutil.copyfileobj(stream, output)
                    os.chmod(dest, 0o755 if member.mode & 0o111 else 0o644)
                else:
                    raise ValueError('Links and special archive members are not allowed')
        expected = json.loads((ROOT / 'inventory/upstream-audit.json').read_text())['source_files']
        for item in expected:
            data = (stage / 'ShareX' / item['path']).read_bytes()
            if hashlib.sha256(data).hexdigest() != item['sha256']:
                raise ValueError('Restored source hash mismatch: ' + item['path'])
        if target.exists():
            raise ValueError('Target appeared during extraction; nothing overwritten')
        (stage / 'ShareX').rename(target)
        print('Restored ' + str(len(expected)) + ' verified source files to ' + str(target))
        print('Keep this reference tree unchanged. Create implementation forks under src/.')
    finally:
        shutil.rmtree(stage)

if __name__ == '__main__':
    main()
