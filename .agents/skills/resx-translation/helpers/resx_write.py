# resx_write.py — Batch write/update translations to ResX files
# Supports: KEY=VALUE pairs, JSON, stdin, single or multiple locales.

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from resx_core import (
    auto_detect,
    ensure_utf8,
    parse_batch_input,
    parse_resx,
    write_translations,
)


def resolve_targets(
    base: Path,
    default_name: str,
    locale: str | None,
    all_locales: bool,
) -> list[Path]:
    """Resolve which locale files to write to."""
    _, pattern, _ = auto_detect(base)
    targets: list[Path] = []

    for fp in sorted(base.glob(pattern)):
        if fp.name == default_name:
            continue
        if locale and locale.lower() not in fp.stem.lower():
            continue
        targets.append(fp)

    if not targets and locale:
        # If locale specified but no file exists, try creating it
        source_path = base / default_name
        stem = Path(default_name).stem
        new_path = base / f"{stem}.{locale}.resx"
        if new_path.exists():
            targets.append(new_path)

    return targets


def read_input_source(source: str | None, stdin_flag: bool) -> dict[str, str] | None:
    """Read translations from a source. Returns parsed KEY=VALUE dict."""
    if stdin_flag or source == "-":
        raw = sys.stdin.read()
        return parse_batch_input(raw)

    if source and Path(source).is_file():
        text = Path(source).read_text(encoding="utf-8")
        # Try JSON first, then KEY=VALUE
        try:
            data = json.loads(text)
            if isinstance(data, dict):
                return {str(k): str(v) for k, v in data.items()}
        except (json.JSONDecodeError, ValueError):
            pass
        return parse_batch_input(text)

    # Treat as inline KEY=VALUE string
    if source:
        return parse_batch_input(source)

    return None


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Batch write/update translations to ResX files.",
        epilog="""
Examples:
  # Write a single key to all locales
  python resx_write.py lang --key AppTitle --value "OptimizerDuck" --locale vi

  # Write multiple keys from stdin (KEY=VALUE format)
  echo "AppTitle=OptimizerDuck\nButtonSave=Lưu" | python resx_write.py lang --stdin --locale vi

  # Write from a file
  python resx_write.py lang --file translations.txt --locale vi

  # Update existing keys only (skip new keys)
  python resx_write.py lang --file translations.txt --locale vi --mode update

  # Add new keys only
  python resx_write.py lang --file translations.txt --locale vi --mode add

  # Dry run — show what would change
  python resx_write.py lang --file translations.txt --locale vi --dry-run
        """,
    )
    parser.add_argument("path", nargs="?", type=Path, default=Path("."),
                        help="Translations directory")

    # Input methods (mutually exclusive group)
    input_group = parser.add_mutually_exclusive_group()
    input_group.add_argument("--key", help="Single key to write")
    input_group.add_argument("--file", type=Path, help="File with KEY=VALUE pairs or JSON")
    input_group.add_argument("--stdin", action="store_true", help="Read from stdin")

    parser.add_argument("--value", help="Value for --key mode")
    parser.add_argument("--locale", default=None,
                        help="Target locale(s): 'vi', 'ja', 'all'")
    parser.add_argument("--mode", choices=["auto", "update", "add", "upsert"],
                        default="auto",
                        help="Write mode: auto (default), update, add, upsert")
    parser.add_argument("--dry-run", action="store_true",
                        help="Show what would change without writing")
    parser.add_argument("--json", action="store_true", help="JSON output")
    parser.add_argument("--source", default=None,
                        help="Source .resx to create locale from (if missing)")

    args = parser.parse_args()
    ensure_utf8()

    base = args.path.resolve()
    if not base.is_dir():
        print(f"directory not found: {base}")
        sys.exit(1)

    default_name, pattern, _ = auto_detect(base)
    if not default_name:
        print("no .resx files found")
        sys.exit(1)

    # Build translations dict
    translations: dict[str, str] = {}
    if args.key:
        if not args.value:
            print("--value is required with --key")
            sys.exit(1)
        translations[args.key] = args.value
    else:
        translations = read_input_source(
            str(args.file) if args.file else args.source,
            args.stdin,
        ) or {}

    if not translations:
        print("no translations to write")
        sys.exit(1)

    # Resolve target files
    targets = resolve_targets(base, default_name, args.locale, args.locale == "all")
    if not targets:
        print(f"no locale files found for '{args.locale or 'all'}'")
        sys.exit(1)

    # Create locale files if needed
    if args.source:
        from resx_core import create_locale
        source_path = base / args.source
        for fp in list(targets):
            if not fp.exists():
                ok = create_locale(source_path, fp)
                if ok:
                    print(f"  created: {fp.name}")

    # Write to each target
    results: list[dict] = []
    total_added = 0
    total_updated = 0

    for fp in targets:
        if not fp.exists():
            print(f"  skip: {fp.name} (not found)")
            continue

        if args.dry_run:
            rf = parse_resx(fp)
            if rf is None:
                continue
            existing = set(rf.keys)
            to_update = {k: v for k, v in translations.items() if k in existing}
            to_add = {k: v for k, v in translations.items() if k not in existing}
            would_update = 0
            would_add = 0
            if args.mode in ("update", "auto", "upsert"):
                would_update = len(to_update)
            if args.mode in ("add", "auto", "upsert"):
                would_add = len(to_add)
            results.append({
                "file": fp.name,
                "lang": rf.lang,
                "would_update": would_update,
                "would_add": would_add,
            })
            continue

        result = write_translations(fp, translations, mode=args.mode)
        total_added += result["added"]
        total_updated += result["updated"]
        results.append({
            "file": fp.name,
            "added": result["added"],
            "updated": result["updated"],
            "skipped": result["skipped"],
        })

    if args.json:
        json.dump({
            "translations": translations,
            "mode": args.mode,
            "dry_run": args.dry_run,
            "results": results,
            "total_added": total_added,
            "total_updated": total_updated,
        }, sys.stdout, indent=2, ensure_ascii=False)
        print()
        return

    # Human-readable output
    print(f"\n{'=' * 60}")
    print("resx manager — write translations")
    print(f"{'=' * 60}")
    print(f"  keys    : {len(translations)}")
    print(f"  mode    : {args.mode}")
    print(f"  dry_run : {args.dry_run}")

    for r in results:
        if args.dry_run:
            print(f"\n  {r['file']} ({r['lang']})")
            if r["would_update"]:
                print(f"    would update: {r['would_update']}")
            if r["would_add"]:
                print(f"    would add   : {r['would_add']}")
        else:
            print(f"\n  {r['file']}")
            if r["added"]:
                print(f"    + added   : {r['added']}")
            if r["updated"]:
                print(f"    ~ updated : {r['updated']}")
            if r["skipped"]:
                print(f"    - skipped : {r['skipped']}")

    if not args.dry_run:
        print(f"\n{'=' * 60}")
        print("result")
        print(f"{'=' * 60}")
        print(f"  total added  : {total_added}")
        print(f"  total updated: {total_updated}")


if __name__ == "__main__":
    main()
