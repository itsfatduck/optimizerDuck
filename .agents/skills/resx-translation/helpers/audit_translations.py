# audit_translations.py — Audit .resx locale files for missing, empty, or untranslated keys

from __future__ import annotations

try:
    import argparse
    import sys
    from pathlib import Path
except ImportError as ex:
    print(f"failed to import required module: {ex}")
    raise SystemExit(1)

sys.path.insert(0, str(Path(__file__).resolve().parent))
from resx_helpers import (
    auto_detect_pattern,
    compute_issues,
    ensure_utf8,
    find_designer,
    get_union_keys,
    load_designer_keys,
    load_resx,
    load_resx_with_lines,
    print_header,
    resx_key_to_property,
)


def parse_args() -> argparse.Namespace:
    p = argparse.ArgumentParser(description="Audit .resx translation files across all locales.")

    p.add_argument("path", type=Path, help="Path to the translations directory")

    p.add_argument("--default-file", default=None, help="Base .resx filename (default: auto-detected)")
    p.add_argument("--pattern", default=None, help="Glob pattern for locale files (default: auto-detected)")
    p.add_argument("--auto-detect", action="store_true", help="Auto-detect file pattern from directory")

    p.add_argument("--show-missing", action="store_true", help="Show keys present in other locales but missing")
    p.add_argument("--show-empty", action="store_true", help="Show keys with empty values")
    p.add_argument("--show-untranslated", action="store_true", help="Show keys matching the default file value")

    p.add_argument("--by-key", action="store_true", help="Group results by key name")
    p.add_argument("--fail-on-any", action="store_true", help="Exit code 1 if any issues found")
    p.add_argument("--check-designer", action="store_true", help="Check .Designer.cs for missing properties")

    return p.parse_args()


def designer_check(base: Path, default_name: str, all_keys: set[str]) -> None:
    designer = find_designer(base, default_name)
    if not designer:
        print("\n  [designer] no .Designer.cs found")
        return

    props = load_designer_keys(designer)
    resx_props = {resx_key_to_property(k) for k in all_keys}
    missing = sorted(resx_props - props)
    stale = sorted(props - resx_props)

    if not missing and not stale:
        print(f"\n  [designer] {designer.name}: ok ({len(props)} properties)")
        return

    if stale:
        print(f"\n  [designer] {designer.name}: {len(stale)} stale propert(y/ies) (in .Designer.cs but not in .resx):")
        for s in stale:
            print(f"      - {s}")

    if missing:
        print(f"\n  [designer] {designer.name}: {len(missing)} missing propert(y/ies) (in .resx but not in .Designer.cs):")
        for m in missing:
            print(f"      - {m}")

    print("  (regenerate by rebuilding the project or running ResGen)")


def main():
    try:
        args = parse_args()
        ensure_utf8()
        base = args.path.resolve()

        if not base.exists() or not base.is_dir():
            print(f"directory not found:\n{base}")
            sys.exit(1)

        if args.auto_detect or not args.default_file:
            default_name, pattern, detected = auto_detect_pattern(base)
            default_name = args.default_file or default_name
            pattern = args.pattern or pattern
        else:
            default_name = args.default_file
            pattern = args.pattern or default_name.replace(".resx", ".*.resx")

        if not pattern:
            print("no locale pattern could be determined")
            sys.exit(1)

        default_path = base / default_name
        default_data = load_resx(default_path) if default_path.exists() else {}
        default_lang = None

        locale_data: dict[str, dict[str, str]] = {}
        locale_line_data: dict[str, dict[str, int]] = {}
        locale_filenames: dict[str, str] = {}
        for fp in sorted(base.glob(pattern)):
            lang = fp.stem.split(".", 1)[1] if "." in fp.stem else fp.stem
            result = load_resx_with_lines(fp)
            if result is not None:
                values = {}
                lines_map = {}
                for k, (v, ln) in result.items():
                    values[k] = v
                    lines_map[k] = ln
                locale_data[lang] = values
                locale_line_data[lang] = lines_map
                locale_filenames[lang] = fp.name
                if fp.name == default_name:
                    default_lang = lang

        if default_data and default_lang is None:
            default_lang = Path(default_name).stem
            locale_data[default_lang] = default_data
            locale_filenames[default_lang] = default_name
            default_with_lines = load_resx_with_lines(default_path)
            if default_with_lines is not None:
                locale_line_data[default_lang] = {k: ln for k, (_, ln) in default_with_lines.items()}

        if not locale_data:
            print(f"no files matching '{pattern}' in {base}")
            sys.exit(1)

        all_keys = get_union_keys(locale_data)
        if not all_keys:
            print("all loaded files are empty")
            sys.exit(1)

        issues = compute_issues(locale_data, default_data, default_lang)

        totals = {
            kind: sum(len(issues[l][kind]) for l in locale_data)
            for kind in ("missing", "empty", "untranslated")
        }
        any_issues = any(issues[l][k] for l in locale_data for k in ("missing", "empty", "untranslated"))
        show_flags = args.show_missing or args.show_empty or args.show_untranslated
        names = sorted(locale_data)

        print_header("translation audit")

        if not any_issues:
            print("\n  all translations look good across all locales")
            print_header("summary")
            print(f"  locales: {len(names)}")
            print(f"  keys   : {len(all_keys)}")
            print(f"  pattern: {pattern}")
            if args.check_designer:
                designer_check(base, default_name, all_keys)
            return

        if args.by_key:
            key_map: dict[str, list[str]] = {}
            for lang in names:
                iss = issues[lang]
                if args.show_missing:
                    for k in iss["missing"]:
                        key_map.setdefault(k, []).append(f"{lang}:missing")
                if args.show_empty:
                    for k in iss["empty"]:
                        key_map.setdefault(k, []).append(f"{lang}:empty")
                if args.show_untranslated:
                    for k in iss["untranslated"]:
                        key_map.setdefault(k, []).append(f"{lang}:untranslated")

            if not show_flags:
                for lang in names:
                    for kind in ("missing", "empty", "untranslated"):
                        for k in issues[lang][kind]:
                            key_map.setdefault(k, []).append(f"{lang}:{kind}")

            print(f"\n  {len(key_map)} key(s) with issues across {len(names)} locale(s)")
            for key in sorted(key_map):
                default_file = locale_filenames.get(default_lang, default_name) if default_lang else default_name
                line_num = locale_line_data.get(default_lang, {}).get(key, 0)
                line_str = f"  ({default_file}:{line_num})" if line_num else ""
                print(f"\n  {key}{line_str}")
                for entry in sorted(key_map[key]):
                    print(f"    - {entry}")
        else:
            for lang in names:
                iss = issues[lang]
                missing = iss["missing"]
                empty = iss["empty"]
                untranslated = iss["untranslated"]

                print(f"\n  [{lang}]")
                if not missing and not empty and not untranslated:
                    print("    ok")
                    continue

                parts = []
                if missing:
                    parts.append(f"missing={len(missing)}")
                if empty:
                    parts.append(f"empty={len(empty)}")
                if untranslated:
                    parts.append(f"untranslated={len(untranslated)}")
                print("    " + " | ".join(parts))

                if args.show_missing and missing:
                    print(f"\n    missing keys ({len(missing)}):")
                    for key in missing:
                        sample = None
                        for olang in names:
                            if olang != lang and key in locale_data[olang]:
                                sample = locale_data[olang][key]
                                break
                        default_file = locale_filenames.get(default_lang, default_name) if default_lang else default_name
                        line_num = locale_line_data.get(default_lang, {}).get(key, 0)
                        line_str = f"  ({default_file}:{line_num})" if line_num else ""
                        if sample is not None:
                            print(f"      - {key} = {sample!r}{line_str}")
                        else:
                            print(f"      - {key}{line_str}")

                if args.show_empty and empty:
                    print(f"\n    empty values ({len(empty)}):")
                    for k in empty:
                        locale_file = locale_filenames.get(lang, f"{lang}.resx")
                        line_num = locale_line_data.get(lang, {}).get(k, 0)
                        line_str = f"  ({locale_file}:{line_num})" if line_num else ""
                        print(f"      - {k}{line_str}")

                if args.show_untranslated and untranslated:
                    print(f"\n    untranslated values ({len(untranslated)}):")
                    for k in untranslated:
                        locale_file = locale_filenames.get(lang, f"{lang}.resx")
                        line_num = locale_line_data.get(lang, {}).get(k, 0)
                        line_str = f"  ({locale_file}:{line_num})" if line_num else ""
                        print(f"      - {k}{line_str}")

        print_header("summary")
        print(f"  locales: {len(names)}")
        print(f"  keys   : {len(all_keys)}")
        print(f"  pattern: {pattern}")
        for kind in ("missing", "empty", "untranslated"):
            if totals[kind]:
                print(f"  {kind}: {totals[kind]}")

        if args.check_designer:
            designer_check(base, default_name, all_keys)

        if args.fail_on_any and any(totals.values()):
            sys.exit(1)

    except KeyboardInterrupt:
        print("\ncancelled")
        sys.exit(130)
    except Exception as ex:
        print(f"\nfatal: {ex}")
        sys.exit(1)


if __name__ == "__main__":
    main()
