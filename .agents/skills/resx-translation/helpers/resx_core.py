# resx_core.py — Core ResX manager library
# Provides low-level ResX parsing, writing, encoding, and batch operations.

from __future__ import annotations

import io
import json
import re
import sys
import xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from pathlib import Path
from typing import Optional
from xml.sax.saxutils import escape as _xml_escape

# ---------------------------------------------------------------------------
# Encoding helpers
# ---------------------------------------------------------------------------

def ensure_utf8() -> None:
    """Force stdout to UTF-8 to avoid encoding errors on Windows."""
    try:
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
    except Exception:
        pass


def detect_encoding(path: Path) -> str:
    """Return 'UTF-8 with BOM', 'UTF-8 (no BOM)', or 'unknown'."""
    try:
        raw = path.read_bytes()
    except Exception:
        return "unknown"
    if raw[:3] == b"\xef\xbb\xbf":
        return "UTF-8 with BOM"
    if raw[:5] == b"<?xml" or raw[:6] == b"<?xml ":
        return "UTF-8 (no BOM)"
    return "unknown"


def read_resx_text(path: Path) -> str | None:
    """Read a .resx file preserving BOM awareness."""
    try:
        return path.read_text(encoding="utf-8-sig")
    except Exception:
        return None


# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------

@dataclass
class ResxEntry:
    """A single <data> entry inside a .resx file."""
    key: str
    value: str
    line: int  # 1-based line number of <data name="..."> in the raw file
    index: int  # 0-based ordinal among all <data> elements
    xml_space: str = "preserve"


@dataclass
class ResxFile:
    """Parsed representation of a .resx file."""
    path: Path
    filename: str
    lang: str  # extracted locale code, or 'default' for the base file
    encoding: str
    entries: list[ResxEntry] = field(default_factory=list)
    _key_map: dict[str, ResxEntry] = field(default_factory=dict, repr=False)

    def __post_init__(self) -> None:
        self._key_map = {e.key: e for e in self.entries}

    @property
    def key_count(self) -> int:
        return len(self.entries)

    @property
    def keys(self) -> list[str]:
        return [e.key for e in self.entries]

    def get(self, key: str) -> ResxEntry | None:
        return self._key_map.get(key)

    def has_key(self, key: str) -> bool:
        return key in self._key_map

    def to_dict(self) -> dict[str, str]:
        return {e.key: e.value for e in self.entries}


# ---------------------------------------------------------------------------
# Parsing
# ---------------------------------------------------------------------------

_DATA_RE = re.compile(r'<data\s+name="([^"]*)"(?:\s+[^>]*)?>')
_SPACE_RE = re.compile(r'xml:space="([^"]*)"')


def parse_resx(path: Path) -> ResxFile | None:
    """Parse a .resx file into a ResxFile. Returns None on error."""
    text = read_resx_text(path)
    if text is None:
        return None

    encoding = detect_encoding(path)
    lang = _extract_lang(path)

    # Build line number map: key -> line number
    line_map: dict[str, int] = {}
    for i, line in enumerate(text.splitlines(), 1):
        m = _DATA_RE.search(line)
        if m:
            line_map[m.group(1)] = i

    # Parse XML for values
    try:
        tree = ET.fromstring(text.encode("utf-8"))
    except ET.ParseError:
        return None

    entries: list[ResxEntry] = []
    for idx, data_el in enumerate(tree.findall("data")):
        name = data_el.get("name")
        if not name:
            continue
        val_el = data_el.find("value")
        value = (val_el.text or "").strip() if val_el is not None else ""
        xml_space = data_el.get("xml:space", "preserve")
        line = line_map.get(name, 0)
        entries.append(ResxEntry(key=name, value=value, line=line, index=idx, xml_space=xml_space))

    return ResxFile(
        path=path,
        filename=path.name,
        lang=lang,
        encoding=encoding,
        entries=entries,
    )


def _extract_lang(path: Path) -> str:
    """Extract locale from filename. 'Translations.vi.resx' -> 'vi', 'Translations.resx' -> 'default'."""
    stem = path.stem
    parts = stem.split(".", 1)
    return parts[1] if len(parts) > 1 else "default"


# ---------------------------------------------------------------------------
# Auto-detection
# ---------------------------------------------------------------------------

def auto_detect(directory: Path) -> tuple[str, str, list[Path]]:
    """Detect the base .resx and locale pattern in a directory.
    Returns (base_filename, locale_glob, locale_files).
    """
    all_resx = sorted(directory.glob("*.resx"))
    if not all_resx:
        return ("", "", [])

    no_lang = [f for f in all_resx if "." not in f.stem]
    has_lang = [f for f in all_resx if "." in f.stem]

    if no_lang:
        base = no_lang[0]
    elif has_lang:
        base = has_lang[0]
    else:
        base = all_resx[0]

    base_stem = base.stem
    pattern = f"{base_stem}.*.resx"
    return base.name, pattern, has_lang


# ---------------------------------------------------------------------------
# Writing — add keys
# ---------------------------------------------------------------------------

def add_keys(path: Path, entries: dict[str, str]) -> int:
    """Add new <data> entries to a .resx file before </root>. Returns count added."""
    text = read_resx_text(path)
    if text is None:
        return -1

    existing = set(re.findall(r'<data\s+name="([^"]*)"', text))
    to_add = {k: v for k, v in entries.items() if k not in existing}
    if not to_add:
        return 0

    buf: list[str] = []
    for key, val in to_add.items():
        escaped = _xml_escape(val)
        buf.append(
            f'  <data name="{key}" xml:space="preserve">\n'
            f'    <value>{escaped}</value>\n'
            f'  </data>'
        )

    insert = "\n".join(buf) + "\n</root>"
    new_text = text.replace("</root>", insert)

    try:
        path.write_text(new_text, encoding="utf-8-sig")
    except Exception:
        return -1

    return len(to_add)


# ---------------------------------------------------------------------------
# Writing — update existing values
# ---------------------------------------------------------------------------

def update_values(path: Path, updates: dict[str, str]) -> int:
    """Update values of existing <data> entries. Returns count updated."""
    text = read_resx_text(path)
    if text is None:
        return -1

    count = 0
    for key, new_val in updates.items():
        # Match the <data> block for this key and replace <value>...</value>
        pattern = re.compile(
            r'(<data\s+name="' + re.escape(key) + r'"[^>]*>\s*'
            r'<value>)(.*?)(</value>)',
            re.DOTALL,
        )
        escaped = _xml_escape(new_val)
        new_text, n = pattern.subn(r'\g<1>' + escaped + r'\g<3>', text, count=1)
        if n > 0:
            text = new_text
            count += 1

    if count > 0:
        try:
            path.write_text(text, encoding="utf-8-sig")
        except Exception:
            return -1

    return count


# ---------------------------------------------------------------------------
# Writing — create new locale
# ---------------------------------------------------------------------------

def create_locale(source_path: Path, target_path: Path) -> bool:
    """Copy a source .resx to create a new locale file."""
    import shutil
    if target_path.exists():
        return False
    try:
        shutil.copy2(source_path, target_path)
        return True
    except Exception:
        return False


# ---------------------------------------------------------------------------
# Batch write from structured input
# ---------------------------------------------------------------------------

def parse_batch_input(raw: str) -> dict[str, str]:
    """Parse KEY=VALUE pairs from a multiline string.
    Supports:
      KEY=VALUE
      KEY="quoted value with spaces"
      # comments and blank lines are skipped
    """
    result: dict[str, str] = {}
    for line in raw.strip().splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        if "=" not in line:
            continue
        key, _, val = line.partition("=")
        key = key.strip()
        val = val.strip()
        # Strip surrounding quotes
        if len(val) >= 2 and val[0] == val[-1] and val[0] in ('"', "'"):
            val = val[1:-1]
        if key:
            result[key] = val
    return result


def write_translations(
    path: Path,
    translations: dict[str, str],
    mode: str = "auto",
) -> dict[str, int]:
    """Write translations to a .resx file.
    mode:
      'update'  — only update existing keys
      'add'     — only add new keys
      'upsert'  — add new + update existing
      'auto'    — update existing, add new (same as upsert)
    Returns dict with counts: {'added': N, 'updated': N, 'skipped': N}.
    """
    text = read_resx_text(path)
    if text is None:
        return {"added": -1, "updated": 0, "skipped": 0}

    existing = set(re.findall(r'<data\s+name="([^"]*)"', text))
    to_update = {k: v for k, v in translations.items() if k in existing}
    to_add = {k: v for k, v in translations.items() if k not in existing}

    updated = 0
    added = 0

    if mode in ("update", "auto", "upsert") and to_update:
        updated = update_values(path, to_update)
        # Re-read after update
        text = read_resx_text(path) or text

    if mode in ("add", "auto", "upsert") and to_add:
        added = add_keys(path, to_add)

    skipped = len(translations) - updated - added
    return {"added": added, "updated": updated, "skipped": max(0, skipped)}


# ---------------------------------------------------------------------------
# Diff / comparison
# ---------------------------------------------------------------------------

@dataclass
class KeyDiff:
    """Difference for a single key between two ResxFiles."""
    key: str
    status: str  # 'missing', 'empty', 'untranslated', 'ok'
    default_value: str = ""
    locale_value: str = ""
    default_line: int = 0
    locale_line: int = 0
    default_index: int = 0
    locale_index: int = 0


def diff_files(default: ResxFile, locale: ResxFile) -> list[KeyDiff]:
    """Compare a locale file against the default. Returns list of KeyDiff for all keys."""
    default_map = default.to_dict()
    locale_map = locale.to_dict()
    all_keys = list(dict.fromkeys(list(default_map.keys()) + list(locale_map.keys())))

    diffs: list[KeyDiff] = []
    for key in all_keys:
        dv = default_map.get(key, "")
        lv = locale_map.get(key, "")
        d_entry = default.get(key)
        l_entry = locale.get(key)

        if key not in locale_map:
            status = "missing"
        elif not lv:
            status = "empty"
        elif dv and lv == dv and locale.lang != "default":
            status = "untranslated"
        else:
            status = "ok"

        diffs.append(KeyDiff(
            key=key,
            status=status,
            default_value=dv,
            locale_value=lv,
            default_line=d_entry.line if d_entry else 0,
            locale_line=l_entry.line if l_entry else 0,
            default_index=d_entry.index if d_entry else 0,
            locale_index=l_entry.index if l_entry else 0,
        ))

    return diffs


# ---------------------------------------------------------------------------
# Designer.cs helpers
# ---------------------------------------------------------------------------

_DESIGNER_PROP_RE = re.compile(r'internal\s+static\s+string\s+(\w+)')


def find_designer(base_dir: Path, resx_name: str) -> Path | None:
    candidate = base_dir / resx_name.replace(".resx", ".Designer.cs")
    return candidate if candidate.exists() else None


def load_designer_keys(designer_path: Path) -> set[str]:
    try:
        text = designer_path.read_text(encoding="utf-8")
    except Exception:
        return set()
    return {m.group(1) for m in _DESIGNER_PROP_RE.finditer(text)}


def resx_key_to_property(key: str) -> str:
    return key.replace(".", "_")


# ---------------------------------------------------------------------------
# Token-efficient output formatters
# ---------------------------------------------------------------------------

def format_entry_compact(entry: ResxEntry, show_value: bool = True, max_val: int = 60) -> str:
    """Format a single entry compactly: [#idx] Key = 'value' (line:N)"""
    val = entry.value
    if show_value and len(val) > max_val:
        val = val[:max_val] + "..."
    escaped = val.replace("\\", "\\\\").replace("'", "\\'")
    parts = [f"[#{entry.index}]", f"{entry.key} = '{escaped}'"]
    if entry.line:
        parts.append(f"(line:{entry.line})")
    return " ".join(parts)


def format_entries_table(entries: list[ResxEntry], show_values: bool = True) -> str:
    """Format entries as a compact aligned table."""
    if not entries:
        return "  (no entries)"

    lines: list[str] = []
    max_key_len = max(len(e.key) for e in entries)
    for e in entries:
        idx = f"#{e.index:<3d}"
        key = e.key.ljust(max_key_len)
        line_info = f"L{e.line}" if e.line else "?"
        if show_values:
            val = e.value
            if len(val) > 50:
                val = val[:47] + "..."
            lines.append(f"  [{idx}] {key} = '{val}'  ({e.filename}:{line_info})")
        else:
            lines.append(f"  [{idx}] {key}  ({e.filename}:{line_info})")
    return "\n".join(lines)


def format_diffs_compact(diffs: list[KeyDiff], default_filename: str, locale_filename: str) -> str:
    """Format diffs in a compact, token-efficient way."""
    issues = [d for d in diffs if d.status != "ok"]
    if not issues:
        return "  all translations look good"

    lines: list[str] = []
    for d in issues:
        if d.status == "missing":
            lines.append(f"  [-] {d.key}  (in {default_filename}:L{d.default_line} #{d.default_index})")
        elif d.status == "empty":
            lines.append(f"  [E] {d.key}  (in {locale_filename}:L{d.locale_line} #{d.locale_index})")
        elif d.status == "untranslated":
            lines.append(f"  [U] {d.key} = '{d.default_value[:40]}'  ({locale_filename}:L{d.locale_line})")
    return "\n".join(lines)
