# sync_resx.py — Synchronise missing keys from a source .resx into locale files

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
    add_keys_to_resx,
    auto_detect_pattern,
    ensure_utf8,
    load_resx,
    print_header,
)


def main():
    try:
        parser = argparse.ArgumentParser(
            description="Sync missing keys from a source .resx into locale files."
        )
        parser.add_argument("path", type=Path, help="Translations directory")
        parser.add_argument("--source", default=None,
                            help="Source .resx file (default: auto-detected base file)")
        parser.add_argument("--target", default=None,
                            help="Target locale glob pattern (default: auto-detected)")
        parser.add_argument("--fill-source", action="store_true",
                            help="Copy source value as-is (default: empty string)")
        parser.add_argument("--fill-marker", default=None,
                            help="Fill with a marker string instead of empty (e.g. 'TODO')")
        parser.add_argument("--dry-run", action="store_true",
                            help="Show what would be added without writing")
        parser.add_argument("--locale", default=None,
                            help="Only sync a specific locale (e.g. 'vi-VN')")
        args = parser.parse_args()

        ensure_utf8()
        base = args.path.resolve()

        if not base.exists() or not base.is_dir():
            print(f"directory not found: {base}")
            sys.exit(1)

        default_name, pattern, _ = auto_detect_pattern(base)
        source_name = args.source or default_name
        locale_pattern = args.target or pattern

        source_path = base / source_name
        if not source_path.exists():
            print(f"source not found: {source_path}")
            sys.exit(1)

        source_data = load_resx(source_path)
        if source_data is None:
            sys.exit(1)

        targets = sorted(base.glob(locale_pattern))
        if args.locale:
            targets = [t for t in targets if args.locale in t.stem]
            if not targets:
                print(f"no locale files matching '{args.locale}' in {base}")
                sys.exit(1)

        print_header("resx sync")

        total_added = 0
        for fp in targets:
            if fp.name == source_name and not args.locale:
                continue

            locale_data = load_resx(fp)
            if locale_data is None:
                continue

            missing = {k: v for k, v in source_data.items() if k not in locale_data}
            if not missing:
                print(f"  {fp.name}: ok (no missing keys)")
                continue

            lang = fp.stem.split(".", 1)[1] if "." in fp.stem else fp.stem

            if args.fill_source:
                fill = {k: source_data[k] for k in missing}
            elif args.fill_marker is not None:
                fill = {k: args.fill_marker for k in missing}
            else:
                fill = {k: "" for k in missing}

            if args.dry_run:
                print(f"  {fp.name}: would add {len(missing)} key(s)")
                for k in sorted(missing):
                    print(f"    + {k} = {fill[k]!r}")
                continue

            added = add_keys_to_resx(fp, fill)
            if added > 0:
                print(f"  {fp.name}: added {added} key(s) ({lang})")
                total_added += added
            elif added == 0:
                print(f"  {fp.name}: ok (already up to date)")
            else:
                print(f"  {fp.name}: error")

        if not args.dry_run:
            print_header("result")
            print(f"  total keys added: {total_added}")

    except KeyboardInterrupt:
        print("\ncancelled")
        sys.exit(130)
    except Exception as ex:
        print(f"\nfatal: {ex}")
        sys.exit(1)


if __name__ == "__main__":
    main()
