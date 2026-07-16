# resx_list.py — List ResX files and their translations with index, line numbers, values

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from resx_core import (
    ResxFile,
    auto_detect,
    detect_encoding,
    ensure_utf8,
    format_entries_table,
    format_entry_compact,
    parse_resx,
)


def list_directory(base: Path, *, lang_filter: str | None = None,
                   show_values: bool = True, json_out: bool = False,
                   keys_only: bool = False, key_filter: str | None = None) -> None:
    default_name, pattern, locale_files = auto_detect(base)
    if not default_name:
        print(f"no .resx files found in {base}")
        sys.exit(1)

    default_path = base / default_name
    default = parse_resx(default_path)
    if default is None:
        print(f"error parsing {default_name}")
        sys.exit(1)

    # Parse all locale files
    locales: list[ResxFile] = []
    for fp in sorted(base.glob(pattern)):
        if fp.name == default_name:
            continue
        rf = parse_resx(fp)
        if rf is not None:
            locales.append(rf)

    if lang_filter:
        locales = [lf for lf in locales if lang_filter.lower() in lf.lang.lower()]

    if json_out:
        data = {
            "default": {
                "file": default.filename,
                "lang": default.lang,
                "encoding": default.encoding,
                "keys": default.key_count,
                "entries": [
                    {"index": e.index, "key": e.key, "value": e.value, "line": e.line}
                    for e in default.entries
                ],
            },
            "locales": [
                {
                    "file": lf.filename,
                    "lang": lf.lang,
                    "encoding": lf.encoding,
                    "keys": lf.key_count,
                    "entries": [
                        {"index": e.index, "key": e.key, "value": e.value, "line": e.line}
                        for e in lf.entries
                    ],
                }
                for lf in locales
            ],
        }
        json.dump(data, sys.stdout, indent=2, ensure_ascii=False)
        print()
        return

    # Human-readable output
    print(f"\n{'=' * 60}")
    print("resx manager — file listing")
    print(f"{'=' * 60}")
    print(f"  directory : {base}")
    print(f"  default   : {default.filename} ({default.key_count} keys, {default.encoding})")
    print(f"  locales   : {len(locales)}")

    # Default file entries
    print(f"\n--- {default.filename} (default) ---")
    entries = default.entries
    if key_filter:
        entries = [e for e in entries if key_filter.lower() in e.key.lower()]
    if keys_only:
        for e in entries:
            print(format_entry_compact(e, show_value=False))
    else:
        print(format_entries_table(entries, show_values=show_values))

    # Locale files
    for lf in locales:
        print(f"\n--- {lf.filename} ({lf.lang}) — {lf.key_count} keys, {lf.encoding} ---")
        entries = lf.entries
        if key_filter:
            entries = [e for e in entries if key_filter.lower() in e.key.lower()]
        if keys_only:
            for e in entries:
                print(format_entry_compact(e, show_value=False))
        else:
            print(format_entries_table(entries, show_values=show_values))


def main() -> None:
    parser = argparse.ArgumentParser(
        description="List ResX files with translations, indices, and line numbers."
    )
    parser.add_argument("path", nargs="?", type=Path, default=Path("."),
                        help="Translations directory (default: current)")
    parser.add_argument("--lang", default=None, help="Filter by locale code (e.g. 'vi', 'ja')")
    parser.add_argument("--key", default=None, help="Filter keys by substring")
    parser.add_argument("--keys-only", action="store_true", help="Show only keys, no values")
    parser.add_argument("--no-values", action="store_true", help="Hide values entirely")
    parser.add_argument("--json", action="store_true", help="Output as JSON")
    args = parser.parse_args()

    ensure_utf8()
    base = args.path.resolve()
    if not base.is_dir():
        print(f"directory not found: {base}")
        sys.exit(1)

    list_directory(
        base,
        lang_filter=args.lang,
        show_values=not args.no_values,
        json_out=args.json,
        keys_only=args.keys_only,
        key_filter=args.key,
    )


if __name__ == "__main__":
    main()
