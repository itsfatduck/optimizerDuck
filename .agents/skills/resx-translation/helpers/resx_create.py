# resx_create.py — Create new locale .resx files from the source

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from resx_core import (
    auto_detect,
    create_locale,
    ensure_utf8,
    parse_resx,
)


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Create new locale .resx files from the source."
    )
    parser.add_argument("path", nargs="?", type=Path, default=Path("."),
                        help="Translations directory")
    parser.add_argument("--locale", required=True,
                        help="Language code (e.g. vi, ja, fr-FR)")
    parser.add_argument("--source", default=None,
                        help="Source .resx (default: auto-detected base)")
    parser.add_argument("--dry-run", action="store_true",
                        help="Preview without creating")
    parser.add_argument("--json", action="store_true", help="JSON output")
    args = parser.parse_args()

    ensure_utf8()
    base = args.path.resolve()
    if not base.is_dir():
        print(f"directory not found: {base}")
        sys.exit(1)

    default_name, _, _ = auto_detect(base)
    source_name = args.source or default_name
    source_path = base / source_name

    if not source_path.exists():
        print(f"source not found: {source_path}")
        sys.exit(1)

    source = parse_resx(source_path)
    if source is None:
        print(f"error parsing {source_name}")
        sys.exit(1)

    stem = Path(source_name).stem
    new_name = f"{stem}.{args.locale}.resx"
    new_path = base / new_name

    if new_path.exists():
        msg = f"already exists: {new_name}"
        if args.json:
            import json
            json.dump({"error": msg, "file": new_name}, sys.stdout)
            print()
        else:
            print(msg)
        sys.exit(1)

    if args.dry_run:
        if args.json:
            import json
            json.dump({
                "dry_run": True,
                "would_create": new_name,
                "source": source_name,
                "keys": source.key_count,
            }, sys.stdout, indent=2)
            print()
        else:
            print(f"[dry-run] would create: {new_name} ({source.key_count} keys from {source_name})")
        return

    ok = create_locale(source_path, new_path)
    if ok:
        if args.json:
            import json
            json.dump({
                "created": new_name,
                "source": source_name,
                "keys": source.key_count,
                "encoding": source.encoding,
            }, sys.stdout, indent=2)
            print()
        else:
            print(f"Created: {new_name} ({source.key_count} keys from {source_name}, {source.encoding})")
    else:
        print(f"error creating {new_name}")
        sys.exit(1)


if __name__ == "__main__":
    main()
