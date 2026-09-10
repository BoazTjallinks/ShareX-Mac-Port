#!/usr/bin/env python3
"""
extract-winforms-layout.py

Extracts a structured JSON layout description from a WinForms `*.Designer.cs`
file (optionally combined with its invariant/English `.resx` resource file).

This is a reverse-engineering / archaeology tool used to reproduce ShareX's
WinForms UI 1:1 on macOS. It parses the generated `InitializeComponent()`
method body plus the sibling field declarations and the (optional) non
-designer partial class file, and never raises on an unparseable statement:
anything it cannot classify is recorded in `unparsed_lines` so coverage is
auditable.

Usage:
    python3 extract-winforms-layout.py --designer <path to X.Designer.cs> \
        [--resx <path to X.resx>] --out <out.json>

    python3 extract-winforms-layout.py --all --source reference/ShareX \
        --out-dir planning/ui-layout

Stdlib only: re, json, argparse, pathlib, xml.etree.ElementTree, base64.
"""

from __future__ import annotations

import argparse
import base64
import json
import re
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

# --------------------------------------------------------------------------
# Low level tokenizer: split an InitializeComponent() method body into
# top-level (semicolon terminated) statements, comment/string aware.
# --------------------------------------------------------------------------

def split_statements(body_text: str):
    """Split C# statement text into a list of (statement_text, start_line, end_line).

    Handles: // line comments, /* */ block comments, "regular" and @"verbatim"
    string literals, 'c' char literals. Never raises; unterminated constructs
    at EOF are flushed as a best-effort trailing statement.
    """
    statements = []
    i = 0
    n = len(body_text)
    line = 1
    state = "code"  # code, line_comment, block_comment, string, verbatim_string, char
    buf = []
    start_line = None

    def flush_ws(ch):
        # Collapse runs of whitespace inside a statement into a single space,
        # but never emit leading whitespace before real content starts.
        if buf:
            if buf[-1] != " ":
                buf.append(" ")

    while i < n:
        c = body_text[i]
        nxt = body_text[i + 1] if i + 1 < n else ""

        if state == "code":
            if c == "\n":
                line += 1
                i += 1
                continue
            if c == "/" and nxt == "/":
                state = "line_comment"
                i += 2
                continue
            if c == "/" and nxt == "*":
                state = "block_comment"
                i += 2
                continue
            if c == '"':
                if buf and buf[-1] == "@":
                    state = "verbatim_string"
                else:
                    state = "string"
                if start_line is None:
                    start_line = line
                buf.append(c)
                i += 1
                continue
            if c == "'":
                state = "char"
                if start_line is None:
                    start_line = line
                buf.append(c)
                i += 1
                continue
            if c == ";":
                if start_line is None:
                    start_line = line
                stmt = "".join(buf).strip()
                if stmt:
                    statements.append((stmt, start_line, line))
                buf = []
                start_line = None
                i += 1
                continue
            if c.isspace():
                flush_ws(c)
            else:
                if start_line is None:
                    start_line = line
                buf.append(c)
            i += 1
            continue

        elif state == "line_comment":
            if c == "\n":
                state = "code"
                line += 1
            i += 1
            continue

        elif state == "block_comment":
            if c == "*" and nxt == "/":
                state = "code"
                i += 2
                continue
            if c == "\n":
                line += 1
            i += 1
            continue

        elif state == "string":
            buf.append(c)
            if c == "\\" and nxt:
                buf.append(nxt)
                i += 2
                continue
            if c == "\n":
                line += 1
            if c == '"':
                state = "code"
            i += 1
            continue

        elif state == "verbatim_string":
            buf.append(c)
            if c == '"':
                if nxt == '"':
                    buf.append(nxt)
                    i += 2
                    continue
                state = "code"
            if c == "\n":
                line += 1
            i += 1
            continue

        elif state == "char":
            buf.append(c)
            if c == "\\" and nxt:
                buf.append(nxt)
                i += 2
                continue
            if c == "'":
                state = "code"
            i += 1
            continue

        else:
            i += 1

    leftover = "".join(buf).strip()
    if leftover:
        statements.append((leftover, start_line or line, line))

    return statements


def split_top_level(s: str, sep: str = ","):
    """Split a string on a separator, respecting (), [], {}, and quotes."""
    parts = []
    depth = 0
    buf = []
    in_str = False
    in_char = False
    i = 0
    n = len(s)
    while i < n:
        c = s[i]
        if in_str:
            buf.append(c)
            if c == "\\" and i + 1 < n:
                buf.append(s[i + 1])
                i += 2
                continue
            if c == '"':
                in_str = False
            i += 1
            continue
        if in_char:
            buf.append(c)
            if c == "\\" and i + 1 < n:
                buf.append(s[i + 1])
                i += 2
                continue
            if c == "'":
                in_char = False
            i += 1
            continue
        if c == '"':
            in_str = True
            buf.append(c)
            i += 1
            continue
        if c == "'":
            in_char = True
            buf.append(c)
            i += 1
            continue
        if c in "([{":
            depth += 1
            buf.append(c)
            i += 1
            continue
        if c in ")]}":
            depth -= 1
            buf.append(c)
            i += 1
            continue
        if c == sep and depth == 0:
            parts.append("".join(buf).strip())
            buf = []
            i += 1
            continue
        buf.append(c)
        i += 1
    tail = "".join(buf).strip()
    if tail or parts:
        parts.append(tail)
    return [p for p in parts if p != ""]


# --------------------------------------------------------------------------
# C# literal / expression parsing
# --------------------------------------------------------------------------

_CS_ESCAPES = {
    "\\\\": "\\",
    '\\"': '"',
    "\\n": "\n",
    "\\r": "\r",
    "\\t": "\t",
    "\\0": "\0",
    "\\'": "'",
}


def unescape_cs_string(s: str) -> str:
    out = []
    i = 0
    n = len(s)
    while i < n:
        if s[i] == "\\" and i + 1 < n:
            pair = s[i : i + 2]
            if pair in _CS_ESCAPES:
                out.append(_CS_ESCAPES[pair])
                i += 2
                continue
        out.append(s[i])
        i += 1
    return "".join(out)


IDENT_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")


def decode_decimal_bits(nums):
    """Decode System.Decimal's `new decimal(new int[] {lo, mid, hi, flags})` form."""
    try:
        lo, mid, hi, flags = nums
        sign = -1 if (flags & 0x80000000) else 1
        scale = (flags >> 16) & 0xFF
        unscaled = (hi << 64) | (mid << 32) | (lo & 0xFFFFFFFF)
        value = sign * unscaled / (10 ** scale)
        if value == int(value):
            return int(value)
        return value
    except Exception:
        return {"kind": "expr", "raw": f"new decimal(new int[] {{{', '.join(str(x) for x in nums)}}})"}


def parse_expr(s: str):
    """Best-effort parse of a C# expression into a JSON-friendly value.
    Never raises; unrecognized expressions fall back to {"kind":"expr","raw":...}.
    """
    s = s.strip()
    if s == "":
        return None
    if s == "null":
        return None
    if s == "true":
        return True
    if s == "false":
        return False

    m = re.fullmatch(r'"((?:[^"\\]|\\.)*)"', s, re.S)
    if m:
        return unescape_cs_string(m.group(1))

    m = re.fullmatch(r'@"(.*)"', s, re.S)
    if m:
        return m.group(1).replace('""', '"')

    m = re.fullmatch(r"-?\d+\.\d+[FfDdMm]?", s)
    if m:
        try:
            return float(s.rstrip("FfDdMm"))
        except ValueError:
            pass

    m = re.fullmatch(r"-?\d+[FfDdMmLlUu]*", s)
    if m:
        try:
            return int(re.sub(r"[FfDdMmLlUu]+$", "", s))
        except ValueError:
            pass

    m = re.fullmatch(r"new\s+System\.Drawing\.Point\((.*)\)", s, re.S)
    if m:
        parts = split_top_level(m.group(1))
        if len(parts) == 2:
            return {"kind": "Point", "x": parse_expr(parts[0]), "y": parse_expr(parts[1])}

    m = re.fullmatch(r"new\s+System\.Drawing\.Size[F]?\((.*)\)", s, re.S)
    if m:
        parts = split_top_level(m.group(1))
        if len(parts) == 2:
            return {"kind": "Size", "width": parse_expr(parts[0]), "height": parse_expr(parts[1])}

    m = re.fullmatch(r"new\s+System\.Windows\.Forms\.Padding\((.*)\)", s, re.S)
    if m:
        parts = split_top_level(m.group(1))
        vals = [parse_expr(p) for p in parts]
        if len(vals) == 1:
            return {"kind": "Padding", "left": vals[0], "top": vals[0], "right": vals[0], "bottom": vals[0]}
        if len(vals) == 4:
            return {"kind": "Padding", "left": vals[0], "top": vals[1], "right": vals[2], "bottom": vals[3]}

    m = re.fullmatch(r"new\s+decimal\(\s*new\s+int\[\]\s*\{(.*)\}\s*\)", s, re.S)
    if m:
        nums_raw = split_top_level(m.group(1))
        try:
            nums = [int(x) for x in nums_raw]
            if len(nums) == 4:
                return decode_decimal_bits(nums)
        except ValueError:
            pass

    m = re.fullmatch(r'resources\.GetString\(\s*"([^"]+)"\s*\)', s)
    if m:
        return {"kind": "resource_string", "resource_key": m.group(1)}

    m = re.fullmatch(r"\(\((?:[^()]|\([^()]*\))*\)\)?\s*\(?\s*resources\.GetObject\(\s*\"([^\"]+)\"\s*\)\s*\)?\)?", s)
    if m:
        return {"kind": "resource_object", "resource_key": m.group(1)}
    m = re.search(r'resources\.GetObject\(\s*"([^"]+)"\s*\)', s)
    if m:
        return {"kind": "resource_object", "resource_key": m.group(1)}

    if IDENT_RE.match(s):
        return {"kind": "ident", "raw": s}

    return {"kind": "expr", "raw": s}


def resx_value_to_json(type_attr, raw_value: str):
    """Convert a resx <data> entry's raw text into the same JSON shapes as parse_expr."""
    raw_value = raw_value.strip()
    if type_attr is None:
        return raw_value
    t = type_attr
    if "System.Drawing.Point" in t:
        parts = [p.strip() for p in raw_value.split(",")]
        if len(parts) == 2:
            try:
                return {"kind": "Point", "x": int(parts[0]), "y": int(parts[1])}
            except ValueError:
                pass
    if "System.Drawing.Size" in t:
        parts = [p.strip() for p in raw_value.split(",")]
        if len(parts) == 2:
            try:
                return {"kind": "Size", "width": int(parts[0]), "height": int(parts[1])}
            except ValueError:
                pass
    if "System.Windows.Forms.Padding" in t:
        parts = [p.strip() for p in raw_value.split(",")]
        try:
            vals = [int(p) for p in parts]
            if len(vals) == 1:
                return {"kind": "Padding", "left": vals[0], "top": vals[0], "right": vals[0], "bottom": vals[0]}
            if len(vals) == 4:
                return {"kind": "Padding", "left": vals[0], "top": vals[1], "right": vals[2], "bottom": vals[3]}
        except ValueError:
            pass
    if "System.Int32" in t or "System.Boolean" in t:
        if "Boolean" in t:
            return raw_value.strip().lower() == "true"
        try:
            return int(raw_value)
        except ValueError:
            pass
    # Enum-like or anything else (DockStyle, AutoScaleMode, ComboBoxStyle, ...): keep raw string
    return raw_value


# --------------------------------------------------------------------------
# resx parsing
# --------------------------------------------------------------------------

BINARY_TYPE_HINTS = ("System.Drawing.Bitmap", "System.Drawing.Icon", "byte[]", "System.IO.MemoryStream")


def parse_resx(resx_path: Path):
    """Return (strings_and_typed: dict[name -> (type_attr, raw_value)], binary: list[dict])."""
    entries = {}
    binary = []
    if resx_path is None or not resx_path.exists():
        return entries, binary
    try:
        tree = ET.parse(str(resx_path))
    except ET.ParseError:
        return entries, binary
    root = tree.getroot()
    for data in root.findall("data"):
        name = data.get("name")
        if name is None:
            continue
        if name.startswith(">>"):
            continue  # designer metadata (Name/Type/Parent/ZOrder), not a UI property
        mimetype = data.get("mimetype")
        type_attr = data.get("type")
        value_el = data.find("value")
        raw_value = value_el.text if value_el is not None else None
        is_binary = bool(mimetype) or (type_attr and any(h in type_attr for h in BINARY_TYPE_HINTS))
        if is_binary:
            binary.append({"resource_key": name, "mimetype": mimetype, "type": type_attr})
            continue
        entries[name] = (type_attr, raw_value if raw_value is not None else "")
    return entries, binary


# --------------------------------------------------------------------------
# Field declarations (control name -> declared type) from the Designer.cs
# class body outside InitializeComponent()
# --------------------------------------------------------------------------

FIELD_DECL_RE = re.compile(
    r"^\s*(?:private|public|internal|protected)\s+(?:readonly\s+)?"
    r"(?P<type>[\w][\w\.\[\]<>,\s]*?)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*;\s*$"
)


def parse_field_declarations(designer_source: str, after_offset: int):
    fields = {}
    tail = designer_source[after_offset:]
    for line in tail.splitlines():
        m = FIELD_DECL_RE.match(line)
        if m:
            fields[m.group("name")] = m.group("type").strip()
    return fields


# --------------------------------------------------------------------------
# Regex building blocks for statement classification
# --------------------------------------------------------------------------

IDENT = r"[A-Za-z_][A-Za-z0-9_]*"
PATH = r"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*"

RE_APPLY_RESOURCES = re.compile(r"^resources\.ApplyResources\((.*)\)$", re.S)
RE_CONTROLS_ADD = re.compile(
    rf"^(?:(?P<target>{PATH})\.)?Controls\.Add\(\s*(?:this\.)?(?P<child>{IDENT})\s*(?:,\s*[^)]*)?\)$"
)
RE_INSTANTIATION_CAST = re.compile(
    rf"^(?:(?P<decltype>[\w\.]+(?:\[\])?)\s+)?(?:this\.)?(?P<name>{IDENT})\s*=\s*"
    rf"\(\(\s*[\w\.\[\]]+\s*\)\s*\(\s*new\s+(?P<newtype>[\w\.]+)\s*\((?P<args>.*)\)\s*\)\)$",
    re.S,
)
RE_ADDRANGE = re.compile(r"^(?P<full>.+)\.AddRange\(\s*new\s+[\w\.]+\s*\[\]\s*\{(?P<items>.*)\}\s*\)$", re.S)
RE_SETTOOLTIP = re.compile(
    rf"^(?:this\.)?(?P<owner>{IDENT})\.SetToolTip\(\s*(?:this\.)?(?P<target>{IDENT})\s*,\s*(?P<value>.*)\)$", re.S
)
RE_INSTANTIATION = re.compile(
    rf"^(?:(?P<decltype>[\w\.]+(?:\[\])?)\s+)?(?:this\.)?(?P<name>{IDENT})\s*=\s*new\s+(?P<newtype>[\w\.]+)\s*\((?P<args>.*)\)$",
    re.S,
)
RE_EVENT = re.compile(rf"^(?:this\.)?(?P<target>{PATH})\.(?P<event>{IDENT})\s*\+=\s*(?P<handler>.*)$", re.S)
RE_FORM_EVENT = re.compile(rf"^(?P<event>{IDENT})\s*\+=\s*(?P<handler>.*)$", re.S)
RE_PROP_ASSIGN = re.compile(rf"^(?:this\.)?(?P<target>{PATH})\.(?P<prop>{IDENT})\s*=\s*(?P<value>.*)$", re.S)
RE_FORM_PROP_ASSIGN = re.compile(rf"^(?P<prop>{IDENT})\s*=\s*(?P<value>.*)$", re.S)

RESOURCE_CALL_RE = re.compile(r"resources\.(?:GetString|GetObject)\(")

FORM_KEY = "__form__"


def resolve_target(path: str):
    path = path.strip()
    if path.startswith("this."):
        path = path[5:]
    elif path == "this":
        path = ""
    if path == "":
        return (FORM_KEY, None)
    parts = path.split(".")
    return (parts[0], ".".join(parts[1:]) if len(parts) > 1 else None)


class FormExtractor:
    def __init__(self, form_name):
        self.form_name = form_name
        self.controls = {}
        self.order_counters = {}  # (parent, slot) -> next child_index
        self.unparsed_lines = []
        self.total_statements = 0
        self.parsed_statements = 0
        self.local_variables = []
        self.tab_order_raw = []  # (tab_index, name)
        self.resx_entries = {}
        self.resx_binary = []
        self.field_types = {}

    def get_control(self, name, default_type=None):
        c = self.controls.get(name)
        if c is None:
            c = {
                "name": name,
                "type": default_type,
                "parent": None,
                "parent_slot": None,
                "child_index": None,
                "properties": {},
                "resources": [],
            }
            self.controls[name] = c
        elif default_type and not c["type"]:
            c["type"] = default_type
        return c

    def set_parent(self, child_name, parent_name, slot):
        c = self.get_control(child_name)
        key = (parent_name, slot)
        idx = self.order_counters.get(key, 0)
        self.order_counters[key] = idx + 1
        c["parent"] = parent_name
        c["parent_slot"] = slot
        c["child_index"] = idx

    def record_property(self, base_name, subpath, prop, value):
        c = self.get_control(base_name)
        key = f"{subpath}.{prop}" if subpath else prop
        c["properties"][key] = value
        if prop == "TabIndex" and isinstance(value, int) and not subpath:
            self.tab_order_raw.append((value, base_name))

    def record_resource(self, base_name, subpath, prop, resource_key, source):
        c = self.get_control(base_name)
        key = f"{subpath}.{prop}" if subpath else prop
        c["resources"].append({"property": key, "resource_key": resource_key, "source": source})

    def apply_resources_bulk(self, target_expr, res_key):
        base_name, subpath = resolve_target(target_expr)
        prefix = res_key
        for name, (type_attr, raw_value) in self.resx_entries.items():
            if name == prefix:
                propname = None
            elif name.startswith(prefix + "."):
                propname = name[len(prefix) + 1 :]
            else:
                continue
            value = resx_value_to_json(type_attr, raw_value)
            prop_key = propname if propname else "(self)"
            full_prop = f"{subpath}.{prop_key}" if subpath else prop_key
            c = self.get_control(base_name)
            c["properties"][full_prop] = value
            c["resources"].append({"property": full_prop, "resource_key": name, "source": "ApplyResources"})
            if prop_key == "TabIndex" and subpath is None and isinstance(value, int):
                self.tab_order_raw.append((value, base_name))

    def classify(self, stmt: str, start_line: int, end_line: int) -> bool:
        # 1. resources.ApplyResources(target, "key")
        m = RE_APPLY_RESOURCES.match(stmt)
        if m:
            args = split_top_level(m.group(1))
            if len(args) >= 2:
                target_expr = args[0].strip()
                key_m = re.fullmatch(r'"([^"]*)"', args[1].strip())
                if key_m:
                    self.apply_resources_bulk(target_expr, key_m.group(1))
                    return True
            return False

        # 2. Controls.Add
        m = RE_CONTROLS_ADD.match(stmt)
        if m:
            target = m.group("target")
            base_name, subpath = resolve_target(target) if target else (FORM_KEY, None)
            slot = f"{subpath}.Controls" if subpath else "Controls"
            self.set_parent(m.group("child"), base_name, slot)
            return True

        # 3. <target>.<Slot>.AddRange(new T[] { a, b, c })
        m = RE_ADDRANGE.match(stmt)
        if m:
            full = m.group("full")
            if "." not in full:
                return False
            base_full, slot = full.rsplit(".", 1)
            base_name, subpath = resolve_target(base_full)
            full_slot = f"{subpath}.{slot}" if subpath else slot
            items = split_top_level(m.group("items"))
            literal_items = []
            for item in items:
                item = item.strip()
                if item.startswith("this."):
                    item = item[5:]
                if IDENT_RE.match(item) and item in self.controls_seen_as_instantiated:
                    self.set_parent(item, base_name, full_slot)
                else:
                    literal_items.append(parse_expr(item))
            if literal_items:
                c = self.get_control(base_name)
                c["properties"][f"{full_slot}.literal_items"] = literal_items
            return True

        # 4. SetToolTip(control, value)
        m = RE_SETTOOLTIP.match(stmt)
        if m:
            target = m.group("target")
            value_raw = m.group("value").strip()
            if RESOURCE_CALL_RE.search(value_raw):
                key_m = re.search(r'"([^"]+)"', value_raw)
                if key_m:
                    self.record_resource(target, None, "ToolTip", key_m.group(1), "GetString/GetObject")
            self.record_property(target, None, "ToolTip", parse_expr(value_raw))
            return True

        # 5. instantiation: [decltype] [this.]name = new Type(args)
        #    (also handles the cast-wrapped form: name = ((CastType)(new Type(args))))
        m = RE_INSTANTIATION.match(stmt) or RE_INSTANTIATION_CAST.match(stmt)
        if m:
            name = m.group("name")
            newtype = m.group("newtype")
            is_local = m.group("decltype") is not None
            self.controls_seen_as_instantiated.add(name)
            if is_local:
                self.local_variables.append({"name": name, "type": newtype})
            else:
                self.get_control(name, default_type=newtype)
            return True

        # 6. event handler wiring: target.Event += handler (or bare Event += handler on the form)
        m = RE_EVENT.match(stmt)
        if m:
            return True  # recognized, intentionally not part of the layout schema
        m = RE_FORM_EVENT.match(stmt)
        if m:
            return True  # recognized, intentionally not part of the layout schema

        # 7. property assign with dot target: target.Prop = value
        m = RE_PROP_ASSIGN.match(stmt)
        if m:
            target = m.group("target")
            prop = m.group("prop")
            value_raw = m.group("value").strip()
            base_name, subpath = resolve_target(target)
            if RESOURCE_CALL_RE.search(value_raw):
                key_m = re.search(r'"([^"]+)"', value_raw)
                if key_m:
                    self.record_resource(base_name, subpath, prop, key_m.group(1), "GetString/GetObject")
            self.record_property(base_name, subpath, prop, parse_expr(value_raw))
            return True

        # 8. bare property assign on the form itself: Prop = value
        m = RE_FORM_PROP_ASSIGN.match(stmt)
        if m:
            prop = m.group("prop")
            value_raw = m.group("value").strip()
            if RESOURCE_CALL_RE.search(value_raw):
                key_m = re.search(r'"([^"]+)"', value_raw)
                if key_m:
                    self.record_resource(FORM_KEY, None, prop, key_m.group(1), "GetString/GetObject")
            self.record_property(FORM_KEY, None, prop, parse_expr(value_raw))
            return True

        # 9. generic method call with no '=' at all, ending in ')': recognized, ignored
        if "=" not in stmt and stmt.endswith(")"):
            return True

        return False

    def run(self, statements):
        self.controls_seen_as_instantiated = set()
        self.total_statements = len(statements)
        for stmt, start_line, end_line in statements:
            ok = False
            try:
                ok = self.classify(stmt, start_line, end_line)
            except Exception as exc:  # never crash the extractor
                ok = False
            if ok:
                self.parsed_statements += 1
            else:
                self.unparsed_lines.append({"line": start_line, "text": stmt[:500]})


# --------------------------------------------------------------------------
# Higher level: extract one form
# --------------------------------------------------------------------------

INITCOMP_SIG_RE = re.compile(r"(?:private|protected|public|internal)\s+void\s+InitializeComponent\s*\(\s*\)\s*\{")
PARTIAL_CLASS_RE_TMPL = r"partial\s+class\s+{name}\s*(?:<[^>]*>)?\s*:\s*([\w\.<>,\s]+?)\s*(?:\{{|,)"


def find_initialize_component_body(source: str):
    m = INITCOMP_SIG_RE.search(source)
    if not m:
        return None, None
    brace_start = m.end() - 1  # position of the opening '{'
    depth = 0
    i = brace_start
    n = len(source)
    in_str = False
    in_verbatim = False
    in_char = False
    in_line_comment = False
    in_block_comment = False
    while i < n:
        c = source[i]
        nxt = source[i + 1] if i + 1 < n else ""
        if in_line_comment:
            if c == "\n":
                in_line_comment = False
            i += 1
            continue
        if in_block_comment:
            if c == "*" and nxt == "/":
                in_block_comment = False
                i += 2
                continue
            i += 1
            continue
        if in_str:
            if c == "\\" and nxt:
                i += 2
                continue
            if c == '"':
                in_str = False
            i += 1
            continue
        if in_verbatim:
            if c == '"':
                if nxt == '"':
                    i += 2
                    continue
                in_verbatim = False
            i += 1
            continue
        if in_char:
            if c == "\\" and nxt:
                i += 2
                continue
            if c == "'":
                in_char = False
            i += 1
            continue
        if c == "/" and nxt == "/":
            in_line_comment = True
            i += 2
            continue
        if c == "/" and nxt == "*":
            in_block_comment = True
            i += 2
            continue
        if c == '"':
            if source[i - 1 : i] == "@":
                in_verbatim = True
            else:
                in_str = True
            i += 1
            continue
        if c == "'":
            in_char = True
            i += 1
            continue
        if c == "{":
            depth += 1
            i += 1
            continue
        if c == "}":
            depth -= 1
            i += 1
            if depth == 0:
                return source[brace_start + 1 : i - 1], i
            continue
        i += 1
    return None, None


def find_base_class(source_dir: Path, form_name: str):
    cs_path = source_dir / f"{form_name}.cs"
    if not cs_path.exists():
        return None
    try:
        text = cs_path.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return None
    pattern = re.compile(PARTIAL_CLASS_RE_TMPL.format(name=re.escape(form_name)))
    m = pattern.search(text)
    if m:
        return m.group(1).strip()
    return None


def build_tree(extractor: FormExtractor):
    controls = extractor.controls
    children_by_parent = {}
    for name, c in controls.items():
        if name == FORM_KEY:
            continue
        if c["parent"] is None:
            continue
        children_by_parent.setdefault((c["parent"], c["parent_slot"]), []).append((c["child_index"] or 0, name))

    def node_for(name):
        c = controls.get(name)
        ctype = c["type"] if c else None
        n = {"name": name, "type": ctype, "slots": {}}
        slots = {}
        for (parent, slot), items in children_by_parent.items():
            if parent != name:
                continue
            items.sort(key=lambda t: t[0])
            slots.setdefault(slot, []).extend([node_for(child_name) for _, child_name in items])
        n["slots"] = slots
        return n

    root = {"name": extractor.form_name, "type": "form", "slots": {}}
    slots = {}
    for (parent, slot), items in children_by_parent.items():
        if parent != FORM_KEY:
            continue
        items.sort(key=lambda t: t[0])
        slots.setdefault(slot, []).extend([node_for(child_name) for _, child_name in items])
    root["slots"] = slots
    return root


def form_meta_get(extractor: FormExtractor, *prop_names):
    c = extractor.controls.get(FORM_KEY)
    if not c:
        return None
    for p in prop_names:
        if p in c["properties"]:
            return c["properties"][p]
    return None


def extract_form(designer_path: Path, resx_path: Path | None):
    source = designer_path.read_text(encoding="utf-8-sig", errors="replace")
    form_name = designer_path.name
    if form_name.endswith(".Designer.cs"):
        form_name = form_name[: -len(".Designer.cs")]

    body, body_end_offset = find_initialize_component_body(source)

    extractor = FormExtractor(form_name)

    if resx_path and resx_path.exists():
        extractor.resx_entries, extractor.resx_binary = parse_resx(resx_path)

    if body_end_offset is not None:
        extractor.field_types = parse_field_declarations(source, body_end_offset)
    else:
        extractor.field_types = parse_field_declarations(source, 0)

    statements = []
    if body is not None:
        statements = split_statements(body)
        extractor.run(statements)
    else:
        extractor.unparsed_lines.append({"line": 0, "text": "InitializeComponent() body not found"})

    # Prefer declared field types over locally-inferred instantiation types.
    for name, ftype in extractor.field_types.items():
        if name in extractor.controls:
            extractor.controls[name]["type"] = ftype

    base_class = find_base_class(designer_path.parent, form_name)

    tab_order = [name for _, name in sorted(extractor.tab_order_raw, key=lambda t: t[0])]

    client_size = form_meta_get(extractor, "ClientSize")
    text_val = form_meta_get(extractor, "Text")
    auto_scale_dim = form_meta_get(extractor, "AutoScaleDimensions")
    auto_scale_mode = form_meta_get(extractor, "AutoScaleMode")
    menu_strip_val = form_meta_get(extractor, "MainMenuStrip")
    if isinstance(menu_strip_val, dict) and menu_strip_val.get("kind") == "ident":
        menu_strip_val = menu_strip_val["raw"]

    local_var_names = {lv["name"] for lv in extractor.local_variables}
    NON_CONTROL_TYPES = {
        "System.Drawing.Size",
        "System.Drawing.SizeF",
        "System.Drawing.Point",
        "System.Drawing.PointF",
        "System.Drawing.Padding",
        "System.Windows.Forms.Padding",
        "System.Drawing.Font",
        "System.Drawing.Color",
        "System.ComponentModel.Container",
    }

    controls_out = []
    for name, c in extractor.controls.items():
        if name == FORM_KEY:
            continue
        if name in local_var_names and c["parent"] is None:
            continue
        if c["parent"] is None and (c["type"] or "") in NON_CONTROL_TYPES:
            continue
        controls_out.append(
            {
                "name": c["name"],
                "type": c["type"],
                "parent": c["parent"] if c["parent"] != FORM_KEY else form_name,
                "parent_slot": c["parent_slot"],
                "child_index": c["child_index"],
                "properties": c["properties"],
                "resources": c["resources"],
            }
        )
    controls_out.sort(key=lambda c: (c["parent"] or "", c["parent_slot"] or "", c["child_index"] if c["child_index"] is not None else 0))

    strings_map = {}
    for name, (type_attr, raw_value) in extractor.resx_entries.items():
        if type_attr is None:
            strings_map[name] = raw_value

    tree = build_tree(extractor)
    tree["type"] = base_class or "Form"

    coverage = (extractor.parsed_statements / extractor.total_statements) if extractor.total_statements else 1.0

    result = {
        "form_name": form_name,
        "base_class": base_class,
        "designer_file": str(designer_path),
        "resx_file": str(resx_path) if resx_path and resx_path.exists() else None,
        "client_size": client_size,
        "text": text_val,
        "menu_strip": menu_strip_val,
        "auto_scale_dimensions": auto_scale_dim,
        "auto_scale_mode": auto_scale_mode,
        "tab_order": tab_order,
        "controls": controls_out,
        "tree": tree,
        "strings": strings_map,
        "binary_resources": extractor.resx_binary,
        "local_variables": extractor.local_variables,
        "parse_coverage": coverage,
        "total_statements": extractor.total_statements,
        "parsed_statements": extractor.parsed_statements,
        "unparsed_lines": extractor.unparsed_lines,
    }
    return result


# --------------------------------------------------------------------------
# CLI
# --------------------------------------------------------------------------


def find_invariant_resx(designer_path: Path):
    form_name = designer_path.name[: -len(".Designer.cs")]
    candidate = designer_path.parent / f"{form_name}.resx"
    if candidate.exists():
        return candidate
    return None


def project_name_for(designer_path: Path, source_root: Path):
    try:
        rel = designer_path.relative_to(source_root)
    except ValueError:
        rel = designer_path
    parts = rel.parts
    return parts[0] if parts else "unknown"


def run_single(designer: Path, resx: Path | None, out: Path):
    result = extract_form(designer, resx)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Wrote {out} (parse_coverage={result['parse_coverage']:.3f}, controls={len(result['controls'])})")


def run_all(source: Path, out_dir: Path):
    out_dir.mkdir(parents=True, exist_ok=True)
    designer_files = sorted(source.rglob("*.Designer.cs"))
    index = []
    total_statements = 0
    total_parsed = 0
    for designer_path in designer_files:
        resx_path = find_invariant_resx(designer_path)
        try:
            result = extract_form(designer_path, resx_path)
        except Exception as exc:
            index.append(
                {
                    "form_name": designer_path.name[: -len(".Designer.cs")],
                    "project": project_name_for(designer_path, source),
                    "designer_file": str(designer_path),
                    "error": f"{type(exc).__name__}: {exc}",
                }
            )
            continue
        form_name = result["form_name"]
        project = project_name_for(designer_path, source)
        out_name = f"{project}__{form_name}.json".replace("/", "_")
        out_path = out_dir / out_name
        out_path.write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding="utf-8")
        total_statements += result["total_statements"]
        total_parsed += result["parsed_statements"]
        index.append(
            {
                "form_name": form_name,
                "project": project,
                "designer_file": str(designer_path),
                "resx_file": str(resx_path) if resx_path else None,
                "control_count": len(result["controls"]),
                "parse_coverage": result["parse_coverage"],
                "total_statements": result["total_statements"],
                "unparsed_count": len(result["unparsed_lines"]),
                "output_file": str(out_path),
            }
        )

    aggregate_coverage = (total_parsed / total_statements) if total_statements else 1.0
    index_doc = {
        "source": str(source),
        "form_count": len(index),
        "aggregate_parse_coverage": aggregate_coverage,
        "total_statements": total_statements,
        "total_parsed_statements": total_parsed,
        "forms": index,
    }
    index_path = out_dir / "index.json"
    index_path.write_text(json.dumps(index_doc, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Wrote {len(index)} form JSON files to {out_dir}")
    print(f"Wrote index: {index_path}")
    print(f"Aggregate parse coverage: {aggregate_coverage:.4f} ({total_parsed}/{total_statements} statements)")


def main():
    parser = argparse.ArgumentParser(description="Extract structured layout JSON from WinForms Designer.cs files.")
    parser.add_argument("--designer", type=Path, help="Path to a single X.Designer.cs file")
    parser.add_argument("--resx", type=Path, help="Path to the invariant/English X.resx file (optional)")
    parser.add_argument("--out", type=Path, help="Output JSON path (single-file mode)")
    parser.add_argument("--all", action="store_true", help="Walk --source for every *.Designer.cs")
    parser.add_argument("--source", type=Path, help="Source tree root for --all mode")
    parser.add_argument("--out-dir", type=Path, help="Output directory for --all mode")
    args = parser.parse_args()

    if args.all:
        if not args.source or not args.out_dir:
            parser.error("--all requires --source and --out-dir")
        run_all(args.source, args.out_dir)
        return

    if not args.designer or not args.out:
        parser.error("single-file mode requires --designer and --out")

    resx = args.resx
    if resx is None:
        resx = find_invariant_resx(args.designer)
    run_single(args.designer, resx, args.out)


if __name__ == "__main__":
    main()
