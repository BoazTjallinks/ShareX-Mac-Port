#!/usr/bin/env python3
"""Read-only source inventory. This is a lexical audit, not a C# compiler."""
import argparse
import hashlib
import json
import re
from pathlib import Path

ENUM_FILES = {
    'ShareX/Enums.cs': ['HotkeyType', 'AfterCaptureTasks', 'AfterUploadTasks', 'CaptureType', 'ScreenRecordStartMethod', 'ToastClickAction', 'NativeMessagingAction', 'SupportedLanguage'],
    'ShareX.UploadersLib/Enums.cs': ['ImageDestination', 'TextDestination', 'FileDestination', 'UrlShortenerType', 'URLSharingServices', 'CustomUploaderBody', 'CustomUploaderDestinationType', 'FTPProtocol', 'LinkFormatEnum'],
    'ShareX.ScreenCaptureLib/Enums.cs': ['ShapeType', 'RegionCaptureAction', 'FFmpegVideoCodec', 'FFmpegAudioCodec', 'ScreenRecordGIFEncoding'],
    'ShareX.ImageEditor/Core/Editor/EditorTool.cs': ['EditorTool'],
    'ShareX/Tools/AI/AIOptions.cs': ['AIProvider'],
}

def enum_members(text, name):
    match = re.search(r'\benum\s+' + re.escape(name) + r'\b[^\{]*\{', text)
    if not match:
        raise ValueError('Missing enum ' + name)
    start = match.end()
    end = text.find('}', start)
    body = text[start:end]
    body = re.sub(r'\[[^\]]*\]', '', body, flags=re.S)
    body = re.sub(r'//[^\n]*', '', body)
    body = re.sub(r'/\*.*?\*/', '', body, flags=re.S)
    rows = []
    for chunk in body.split(','):
        match = re.fullmatch(r'\s*(\w+)\s*(?:=\s*(.*?))?\s*', chunk, flags=re.S)
        if match:
            rows.append({'name': match[1], 'value_expression': (match[2] or '').strip()})
        elif chunk.strip():
            raise ValueError('Unparsed enum entry ' + name + ': ' + chunk)
    return rows

def inventory(root):
    out = {'schema_version': 1, 'method': 'Lexical source inventory; setting candidates require semantic reconciliation before app parity sign-off.', 'enums': {}, 'editor_effects': [], 'legacy_effects': [], 'uploader_services': [], 'settings_candidates': [], 'source_files': []}
    for rel, names in ENUM_FILES.items():
        text = (root / rel).read_text(encoding='utf-8-sig')
        for name in names:
            out['enums'][name] = {'source': rel, 'members': enum_members(text, name)}
    for p in sorted(root.rglob('*')):
        if not p.is_file() or '.git' in p.relative_to(root).parts:
            continue
        rel = p.relative_to(root).as_posix()
        out['source_files'].append({'path': rel, 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()})
        if p.suffix != '.cs' or p.name.endswith('.Designer.cs'):
            continue
        text = p.read_text(encoding='utf-8-sig')
        if rel.startswith('ShareX.ImageEditor/Core/ImageEffects/'):
            found = re.search(r'override string Id\s*=>\s*"([^"]+)"', text)
            if found:
                def prop(name):
                    m = re.search(r'override string ' + name + r'\s*=>\s*"([^"]*)"', text)
                    return m[1] if m else ''
                params = re.findall(r'EffectParameters\.\w+(?:<[^>]+>)?\(\s*"([^"]+)"', text)
                out['editor_effects'].append({'id': found[1], 'name': prop('Name'), 'description': prop('Description'), 'parameters': params, 'source': rel, 'category': p.parent.name})
        if rel.startswith('ShareX.ImageEffectsLib/') and p.parent.name in ('Adjustments', 'Drawings', 'Filters', 'Manipulations'):
            out['legacy_effects'].append({'name': p.stem, 'category': p.parent.name, 'source': rel})
        if rel.startswith('ShareX.UploadersLib/'):
            pattern = r'override\s+(ImageDestination|TextDestination|FileDestination|UrlShortenerType|URLSharingServices)\s+EnumValue\s*(?:\{\s*get;\s*\}\s*=|=>)\s*\w+\.(\w+)'
            for enum, name in re.findall(pattern, text):
                out['uploader_services'].append({'enum': enum, 'name': name, 'source': rel})
        # Configuration field index. Preserve source declarations; do not pretend this
        # is a semantic list of serialized fields or a proof of feature completion.
        is_config = (p.name.endswith(('Settings.cs', 'Options.cs', 'Config.cs')) or p.name in ('CustomUploaderItem.cs', 'HotkeysConfig.cs', 'ExternalProgram.cs', 'ProxyInfo.cs', 'HistoryItem.cs', 'UploaderFilter.cs', 'ClipboardFormat.cs'))
        if is_config:
            last_class = ''
            for number, line in enumerate(text.splitlines(), 1):
                cl = re.search(r'\bclass\s+(\w+)', line)
                if cl:
                    last_class = cl[1]
                field = re.match(r'\s*public\s+(?!class\b|enum\b|interface\b|static\b|const\b|override\b)(?:required\s+)?([\w.<>,?\[\] ]+?)\s+(\w+)\s*(=|;|\{\s*get\s*;)', line)
                if field:
                    out['settings_candidates'].append({'class': last_class, 'name': field[2], 'type': field[1].strip(), 'declaration': line.strip(), 'source': rel, 'line': number})
    return out

def main():
    p = argparse.ArgumentParser()
    p.add_argument('--source', required=True, type=Path)
    p.add_argument('--out', required=True, type=Path)
    args = p.parse_args()
    result = inventory(args.source.resolve())
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n')
    print(json.dumps({k: len(v) for k, v in result.items() if isinstance(v, (list, dict))}))

if __name__ == '__main__':
    main()
