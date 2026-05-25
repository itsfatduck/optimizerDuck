# resx_helpers.py — Shared utilities for resx translation tools

from __future__ import annotations

import io
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from xml.sax.saxutils import escape as xml_escape

DESIGNER_PROP_RE = re.compile(r'internal\s+static\s+string\s+(\w+)')


def ensure_utf8() -> None:
    try:
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
    except Exception:
        pass


def load_resx(path: Path) -> dict[str, str] | None:
    try:
        tree = ET.parse(path)
    except (FileNotFoundError, ET.ParseError, PermissionError) as ex:
        print(f"  [error] {path.name}: {ex}", file=sys.stderr)
        return None
    except Exception as ex:
        print(f"  [error] unexpected: {path.name}: {ex}", file=sys.stderr)
        return None

    result = {}
    for data in tree.getroot().findall("data"):
        name = data.get("name")
        if name:
            val = data.findtext("value")
            result[name] = (val or "").strip()
    return result


def load_resx_with_lines(path: Path) -> dict[str, tuple[str, int]] | None:
    try:
        tree = ET.parse(path)
    except (FileNotFoundError, ET.ParseError, PermissionError) as ex:
        print(f"  [error] {path.name}: {ex}", file=sys.stderr)
        return None
    except Exception as ex:
        print(f"  [error] unexpected: {path.name}: {ex}", file=sys.stderr)
        return None

    try:
        lines = path.read_text(encoding="utf-8-sig").splitlines()
    except Exception as ex:
        print(f"  [error] scanning {path.name}: {ex}", file=sys.stderr)
        return None

    key_lines: dict[str, int] = {}
    for i, line in enumerate(lines, 1):
        m = re.search(r'<data\s+name="([^"]*)"', line)
        if m:
            key_lines[m.group(1)] = i

    result: dict[str, tuple[str, int]] = {}
    for data in tree.getroot().findall("data"):
        name = data.get("name")
        if name:
            val = data.findtext("value")
            result[name] = ((val or "").strip(), key_lines.get(name, 0))

    return result


def extract_lang(filepath: Path) -> str:
    stem = filepath.stem
    parts = stem.split(".", 1)
    return parts[1] if len(parts) > 1 else parts[0]


def auto_detect_pattern(directory: Path) -> tuple[str, str, list[Path]]:
    all_resx = sorted(directory.glob("*.resx"))
    if not all_resx:
        return ("Translations.resx", "Translations.*.resx", [])

    no_lang = [f for f in all_resx if "." not in f.stem]
    has_lang = [f for f in all_resx if "." in f.stem]
    default = no_lang[0].name if no_lang else all_resx[0].name

    base_stem = Path(default).stem
    pattern = f"{base_stem}.*.resx" if has_lang else default.replace(".resx", ".*.resx")
    return default, pattern, has_lang


def find_designer(base_dir: Path, resx_name: str) -> Path | None:
    candidate = base_dir / resx_name.replace(".resx", ".Designer.cs")
    return candidate if candidate.exists() else None


def load_designer_keys(designer_path: Path) -> set[str]:
    try:
        text = designer_path.read_text(encoding="utf-8")
    except Exception as ex:
        print(f"  [error] reading {designer_path.name}: {ex}", file=sys.stderr)
        return set()
    return {m.group(1) for m in DESIGNER_PROP_RE.finditer(text)}


def resx_key_to_property(key: str) -> str:
    return key.replace(".", "_")


def get_union_keys(locale_data: dict[str, dict[str, str]]) -> set[str]:
    keys: set[str] = set()
    for data in locale_data.values():
        keys.update(data.keys())
    return keys


def compute_issues(
    locale_data: dict[str, dict[str, str]],
    default_data: dict[str, str],
    default_lang: str | None,
) -> dict[str, dict[str, list[str]]]:
    all_keys = get_union_keys(locale_data)
    issues: dict[str, dict[str, list[str]]] = {}
    for lang, data in locale_data.items():
        issues[lang] = {
            "missing": sorted(all_keys - data.keys()),
            "empty": sorted(k for k, v in data.items() if not v),
            "untranslated": sorted(
                k for k, v in data.items()
                if lang != default_lang and k in default_data and v == default_data[k]
            ),
        }
    return issues


def print_header(title: str) -> None:
    n = min(len(title), 72)
    print(f"\n{'=' * n}")
    print(title)
    print("=" * n)


def add_keys_to_resx(resx_path: Path, new_entries: dict[str, str]) -> int:
    try:
        content = resx_path.read_text(encoding="utf-8-sig")
    except Exception as ex:
        print(f"  [error] reading {resx_path.name}: {ex}", file=sys.stderr)
        return -1

    existing = set(re.findall(r'<data\s+name="([^"]*)"', content))
    to_add = {k: v for k, v in new_entries.items() if k not in existing}
    if not to_add:
        return 0

    buf = []
    for key, val in to_add.items():
        escaped = xml_escape(val)
        buf.append(f'  <data name="{key}" xml:space="preserve">\n    <value>{escaped}</value>\n  </data>')

    repl = "\n".join(buf) + "\n</root>"
    new_content = content.replace("</root>", repl)

    try:
        resx_path.write_text(new_content, encoding="utf-8-sig")
    except Exception as ex:
        print(f"  [error] writing {resx_path.name}: {ex}", file=sys.stderr)
        return -1

    return len(to_add)
