# resx_sync.py — Synchronise missing keys from source to locale files

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from resx_core import (
    add_keys,
    auto_detect,
    ensure_utf8,
    parse_resx,
)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Sync missing keys from source .resx to locale files."
    )
    parser.add_argument("path", nargs="?", type=Path, default=Path("."),
                        help="Translations directory")
    parser.add_argument("--source", default=None,
                        help="Source .resx (default: auto-detected)")
    parser.add_argument("--locale", default=None,
                        help="Only sync a specific locale")
    parser.add_argument("--fill-source", action="store_true",
                        help="Copy source value as-is (default: empty)")
    parser.add_argument("--fill-marker", default=None,
                        help="Fill with marker string (e.g. 'TODO')")
    parser.add_argument("--dry-run", action="store_true",
                        help="Preview without writing")
    parser.add_argument("--json", action="store_true", help="JSON output")
    args = parser.parse_args()

    ensure_utf8()
    base = args.path.resolve()
    if not base.is_dir():
        print(f"directory not found: {base}")
        sys.exit(1)

    default_name, pattern, _ = auto_detect(base)
    source_name = args.source or default_name
    source_path = base / source_name

    if not source_path.exists():
        print(f"source not found: {source_path}")
        sys.exit(1)

    source = parse_resx(source_path)
    if source is None:
        print(f"error parsing {source_name}")
        sys.exit(1)

    source_keys = source.to_dict()

    # Find target locale files
    targets: list[Path] = []
    for fp in sorted(base.glob(pattern)):
        if fp.name == source_name:
            continue
        if args.locale and args.locale.lower() not in fp.stem.lower():
            continue
        targets.append(fp)

    if not targets:
        msg = f"no locale files matching pattern"
        if args.json:
            json.dump({"error": msg}, sys.stdout)
            print()
        else:
            print(msg)
        sys.exit(1)

    results: list[dict] = []
    total_added = 0

    for fp in targets:
        rf = parse_resx(fp)
        if rf is None:
            results.append({"file": fp.name, "error": "parse failed"})
            continue

        missing = {k: v for k, v in source_keys.items() if not rf.has_key(k)}
        if not missing:
            results.append({"file": fp.name, "lang": rf.lang, "added": 0})
            continue

        if args.fill_source:
            fill = {k: source_keys[k] for k in missing}
        elif args.fill_marker:
            fill = {k: args.fill_marker for k in missing}
        else:
            fill = {k: "" for k in missing}

        if args.dry_run:
            results.append({
                "file": fp.name,
                "lang": rf.lang,
                "would_add": len(missing),
                "keys": list(missing.keys()),
            })
            continue

        added = add_keys(fp, fill)
        total_added += added
        results.append({"file": fp.name, "lang": rf.lang, "added": added})

    if args.json:
        json.dump({
            "source": source_name,
            "dry_run": args.dry_run,
            "results": results,
            "total_added": total_added,
        }, sys.stdout, indent=2, ensure_ascii=False)
        print()
        return

    # Human-readable
    print(f"\n{'=' * 60}")
    print("resx manager — sync keys")
    print(f"{'=' * 60}")
    print(f"  source  : {source_name} ({source.key_count} keys)")

    for r in results:
        if "error" in r:
            print(f"  {r['file']}: error — {r['error']}")
            continue
        added = r.get("added", 0)
        would = r.get("would_add", 0)
        if args.dry_run:
            print(f"  {r['file']} ({r['lang']}): would add {would}")
            if "keys" in r:
                for k in r["keys"]:
                    print(f"    + {k}")
        else:
            print(f"  {r['file']} ({r['lang']}): added {added}")

    if not args.dry_run:
        print(f"\n{'=' * 60}")
        print("result")
        print(f"{'=' * 60}")
        print(f"  total added: {total_added}")


if __name__ == "__main__":
    main()
