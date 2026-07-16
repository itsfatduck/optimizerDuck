# resx_audit.py — Comprehensive ResX audit: missing, empty, untranslated, warnings

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from resx_core import (
    KeyDiff,
    ResxFile,
    auto_detect,
    diff_files,
    ensure_utf8,
    find_designer,
    format_diffs_compact,
    load_designer_keys,
    parse_resx,
    resx_key_to_property,
)


def audit_directory(
    base: Path,
    *,
    show_missing: bool = True,
    show_empty: bool = True,
    show_untranslated: bool = True,
    show_warnings: bool = True,
    check_designer: bool = False,
    by_key: bool = False,
    lang_filter: str | None = None,
    json_out: bool = False,
    fail_on_any: bool = False,
) -> None:
    default_name, pattern, _ = auto_detect(base)
    if not default_name:
        print("no .resx files found")
        sys.exit(1)

    default_path = base / default_name
    default = parse_resx(default_path)
    if default is None:
        print(f"error parsing {default_name}")
        sys.exit(1)

    # Parse locale files
    locales: list[ResxFile] = []
    for fp in sorted(base.glob(pattern)):
        if fp.name == default_name:
            continue
        rf = parse_resx(fp)
        if rf is not None:
            locales.append(rf)

    if lang_filter:
        locales = [lf for lf in locales if lang_filter.lower() in lf.lang.lower()]

    if not locales:
        print("no locale files found")
        sys.exit(0)

    # Compute diffs
    all_diffs: dict[str, list[KeyDiff]] = {}
    for lf in locales:
        all_diffs[lf.lang] = diff_files(default, lf)

    # Collect issues
    total_missing = 0
    total_empty = 0
    total_untranslated = 0
    warnings: list[str] = []

    for lang, diffs in all_diffs.items():
        for d in diffs:
            if d.status == "missing":
                total_missing += 1
            elif d.status == "empty":
                total_empty += 1
            elif d.status == "untranslated":
                total_untranslated += 1

    # Warnings
    if show_warnings:
        # Check encoding consistency
        encodings = set()
        encodings.add(default.encoding)
        for lf in locales:
            encodings.add(lf.encoding)
        if len(encodings) > 1:
            warnings.append(f"mixed encodings: {', '.join(sorted(encodings))}")

        # Check key count mismatch
        counts = {lf.lang: lf.key_count for lf in locales}
        if len(set(counts.values())) > 1:
            min_c = min(counts.values())
            max_c = max(counts.values())
            warnings.append(f"key count mismatch: {min_c} to {max_c}")

        # Check for duplicate keys in any file
        for lf in [default] + locales:
            keys = [e.key for e in lf.entries]
            seen = set()
            for k in keys:
                if k in seen:
                    warnings.append(f"duplicate key '{k}' in {lf.filename}")
                seen.add(k)

        # Designer.cs check
        if check_designer:
            designer = find_designer(base, default_name)
            if designer:
                d_props = load_designer_keys(designer)
                r_props = {resx_key_to_property(k) for k in default.keys}
                missing_props = r_props - d_props
                stale_props = d_props - r_props
                if missing_props:
                    warnings.append(f"Designer.cs missing {len(missing_props)} properties")
                if stale_props:
                    warnings.append(f"Designer.cs has {len(stale_props)} stale properties")

    any_issues = total_missing + total_empty + total_untranslated > 0 or warnings

    if json_out:
        data = {
            "default": default.filename,
            "locales": len(locales),
            "total_keys": default.key_count,
            "missing": total_missing,
            "empty": total_empty,
            "untranslated": total_untranslated,
            "warnings": warnings,
            "details": {},
        }
        for lang, diffs in all_diffs.items():
            issues = [d for d in diffs if d.status != "ok"]
            data["details"][lang] = [
                {
                    "key": d.key,
                    "status": d.status,
                    "default_value": d.default_value,
                    "locale_value": d.locale_value,
                    "default_line": d.default_line,
                    "locale_line": d.locale_line,
                    "default_index": d.default_index,
                    "locale_index": d.locale_index,
                }
                for d in issues
            ]
        json.dump(data, sys.stdout, indent=2, ensure_ascii=False)
        print()
        if fail_on_any and any_issues:
            sys.exit(1)
        return

    # Human-readable output
    print(f"\n{'=' * 60}")
    print("resx manager — audit")
    print(f"{'=' * 60}")
    print(f"  default   : {default.filename} ({default.key_count} keys)")
    print(f"  locales   : {len(locales)}")

    if not any_issues:
        print(f"\n  all translations look good across all locales")
        _print_summary(default.key_count, len(locales), 0, 0, 0, warnings)
        return

    # Per-locale issues
    for lf in locales:
        lang = lf.lang
        diffs = all_diffs.get(lang, [])
        issues = [d for d in diffs if d.status != "ok"]
        missing = [d for d in diffs if d.status == "missing"]
        empty = [d for d in diffs if d.status == "empty"]
        untranslated = [d for d in diffs if d.status == "untranslated"]

        print(f"\n  [{lang}] — {lf.filename}")
        if not issues:
            print(f"    ok ({lf.key_count} keys)")
            continue

        parts = []
        if missing:
            parts.append(f"missing={len(missing)}")
        if empty:
            parts.append(f"empty={len(empty)}")
        if untranslated:
            parts.append(f"untranslated={len(untranslated)}")
        print(f"    {' | '.join(parts)}")

        if show_missing and missing:
            print(f"\n    missing ({len(missing)}):")
            for d in missing:
                val_preview = d.default_value[:50] + "..." if len(d.default_value) > 50 else d.default_value
                print(f"      [-] {d.key}  (default:L{d.default_line} #{d.default_index})  = '{val_preview}'")

        if show_empty and empty:
            print(f"\n    empty ({len(empty)}):")
            for d in empty:
                print(f"      [E] {d.key}  ({lf.filename}:L{d.locale_line} #{d.locale_index})")

        if show_untranslated and untranslated:
            print(f"\n    untranslated ({len(untranslated)}):")
            for d in untranslated:
                print(f"      [U] {d.key}  ({lf.filename}:L{d.locale_line} #{d.locale_index})")

    # Warnings
    if warnings:
        print(f"\n  warnings ({len(warnings)}):")
        for w in warnings:
            print(f"    ! {w}")

    _print_summary(default.key_count, len(locales), total_missing, total_empty, total_untranslated, warnings)

    if fail_on_any and any_issues:
        sys.exit(1)


def _print_summary(keys: int, locales: int, missing: int, empty: int,
                   untranslated: int, warnings: list[str]) -> None:
    print(f"\n{'=' * 60}")
    print("summary")
    print(f"{'=' * 60}")
    print(f"  keys        : {keys}")
    print(f"  locales     : {locales}")
    if missing:
        print(f"  missing     : {missing}")
    if empty:
        print(f"  empty       : {empty}")
    if untranslated:
        print(f"  untranslated: {untranslated}")
    if warnings:
        print(f"  warnings    : {len(warnings)}")


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Comprehensive ResX audit: missing, empty, untranslated, warnings."
    )
    parser.add_argument("path", nargs="?", type=Path, default=Path("."),
                        help="Translations directory")
    parser.add_argument("--lang", default=None, help="Filter by locale code")
    parser.add_argument("--no-missing", action="store_true", help="Hide missing keys")
    parser.add_argument("--no-empty", action="store_true", help="Hide empty values")
    parser.add_argument("--no-untranslated", action="store_true", help="Hide untranslated")
    parser.add_argument("--no-warnings", action="store_true", help="Hide warnings")
    parser.add_argument("--check-designer", action="store_true", help="Audit Designer.cs")
    parser.add_argument("--by-key", action="store_true", help="Group by key name")
    parser.add_argument("--json", action="store_true", help="JSON output")
    parser.add_argument("--fail-on-any", action="store_true", help="Exit 1 if issues found")
    args = parser.parse_args()

    ensure_utf8()
    base = args.path.resolve()
    if not base.is_dir():
        print(f"directory not found: {base}")
        sys.exit(1)

    audit_directory(
        base,
        show_missing=not args.no_missing,
        show_empty=not args.no_empty,
        show_untranslated=not args.no_untranslated,
        show_warnings=not args.no_warnings,
        check_designer=args.check_designer,
        by_key=args.by_key,
        lang_filter=args.lang,
        json_out=args.json,
        fail_on_any=args.fail_on_any,
    )


if __name__ == "__main__":
    main()
