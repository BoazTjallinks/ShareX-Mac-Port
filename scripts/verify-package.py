#!/usr/bin/env python3
"""Validate source provenance and catalogue integrity, not app functionality."""
import argparse
import hashlib
import json
import re
import tarfile
from collections import Counter
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parents[1]

def load(path):
    return json.loads((ROOT / path).read_text())

def require(condition, message):
    if not condition:
        raise ValueError(message)

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--write-report', action='store_true')
    args = parser.parse_args()
    lock = load('upstream.lock.json')
    audit = load('inventory/upstream-audit.json')
    ledger = load('planning/feature-ledger.json')
    settings = load('planning/settings-ledger.json')
    counts = load('inventory/coverage-counts.json')
    require(lock['commit'] == 'd2502561f63fc3ff502cacd91514e3f7f2948c74', 'Unexpected baseline commit')
    archive = ROOT / lock['archive_path']
    require(hashlib.sha256(archive.read_bytes()).hexdigest() == lock['archive_sha256'], 'Archive hash mismatch')
    expected = {x['path']: x['sha256'] for x in audit['source_files']}
    actual = {}
    with tarfile.open(archive, 'r:gz') as tar:
        for member in tar:
            name = PurePosixPath(member.name)
            require(not name.is_absolute() and '..' not in name.parts and name.parts[0] == 'ShareX', 'Unsafe archive path')
            require(member.isdir() or member.isfile(), 'Unsupported archive member')
            if member.isfile():
                key = PurePosixPath(*name.parts[1:]).as_posix()
                require(key not in actual, 'Duplicate archive path')
                stream = tar.extractfile(member)
                require(stream is not None, 'Unreadable archive member')
                with stream:
                    actual[key] = hashlib.sha256(stream.read()).hexdigest()
    require(expected == actual, 'Source file inventory/hash mismatch')
    ids = [r['id'] for r in ledger]
    require(len(ids) == len(set(ids)), 'Duplicate feature ID')
    for r in ledger:
        require(r['upstream_source'] in expected, 'Invalid feature source: ' + r['id'])
        for key in ('description', 'implementation_plan', 'acceptance', 'implementation_status'):
            require(bool(r.get(key)), 'Missing feature field: ' + r['id'] + ': ' + key)
    def names(enum):
        return {m['name'] for m in audit['enums'][enum]['members']}
    def expect_ids(prefix, values):
        actual_ids = {r['id'] for r in ledger if r['id'].startswith(prefix)}
        require(actual_ids == {prefix + n for n in values}, 'Coverage mismatch for ' + prefix)
    expect_ids('command.', names('HotkeyType') - {'None'})
    expect_ids('AfterCaptureTasks.', names('AfterCaptureTasks') - {'None'})
    expect_ids('AfterUploadTasks.', names('AfterUploadTasks') - {'None'})
    expect_ids('region.', names('ShapeType'))
    expect_ids('editor.', names('EditorTool') | {'Commands', 'HistoryAndCanvas', 'EffectCatalogue'})
    expect_ids('effect.new.', {e['id'] for e in audit['editor_effects']})
    expect_ids('effect.legacy.', {e['name'] for e in audit['legacy_effects']})
    destination_enums = ('ImageDestination', 'TextDestination', 'FileDestination', 'UrlShortenerType', 'URLSharingServices')
    for en in destination_enums:
        expect_ids('destination.' + en + '.', names(en))
    handled = set(destination_enums) | {'HotkeyType', 'AfterCaptureTasks', 'AfterUploadTasks', 'ShapeType', 'EditorTool'}
    for en in set(audit['enums']) - handled:
        expect_ids('option.' + en + '.', names(en))
    service_pairs = {(r['enum'], r['name']) for r in audit['uploader_services']}
    require(len(service_pairs) == 74, 'Unexpected service count')
    for en in destination_enums:
        for name in names(en):
            require((en, name) in service_pairs or name == 'FileUploader', 'Missing service registration')
    function_names = {Path(p).stem for p in expected if p.startswith('ShareX.UploadersLib/CustomUploader/Functions/CustomUploaderFunction') and p.endswith('.cs') and not p.endswith('/CustomUploaderFunction.cs')}
    expect_ids('custom_function.', function_names)
    require(len(settings) == len(audit['settings_candidates']) == counts['settings_candidates'], 'Settings count mismatch')
    for s, original in zip(settings, audit['settings_candidates']):
        require(all(s[k] == v for k, v in original.items()), 'Settings evidence modified')
        require(s['source'] in expected and bool(s['migration_rule']), 'Incomplete settings mapping')
    actual_counts = dict(Counter(r['category'] for r in ledger))
    require(actual_counts == counts['categories'], 'Category totals differ')
    source_links = 0
    pattern = r'https://github\.com/ShareX/ShareX/blob/' + lock['commit'] + r'/([^\s)]+)'
    for p in ROOT.rglob('*.md'):
        if 'reference' in p.relative_to(ROOT).parts:
            continue
        for path in re.findall(pattern, p.read_text()):
            require(path in expected, 'Invalid Markdown source link: ' + path)
            source_links += 1
    report = {
        'result': 'passed', 'scope': 'Design package/source/catalogue integrity only; no macOS app was built or runtime-tested.',
        'commit': lock['commit'], 'source_files_verified': len(actual), 'source_links_verified': source_links,
        'feature_ledger_entries': len(ledger), 'settings_candidates': len(settings), 'category_counts': actual_counts,
        'upstream_archive_sha256': lock['archive_sha256'], 'mac_application_implemented': False,
        'windows_gui_reference_run': False, 'macos_gui_reference_run': False
    }
    if args.write_report:
        (ROOT / 'planning/package-verification.json').write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))

if __name__ == '__main__':
    main()
