# create_locale.py — Create a new locale .resx file from the source

from __future__ import annotations

import argparse
import sys
import shutil
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from resx_helpers import auto_detect_pattern, ensure_utf8, load_resx


def main():
    try:
        parser = argparse.ArgumentParser(
            description="Create a new locale .resx file from the source."
        )
        parser.add_argument("path", type=Path, help="Translations directory")
        parser.add_argument("--locale", required=True,
                            help="Language code (e.g. vi, ja, fr-FR)")
        parser.add_argument("--source", default=None,
                            help="Source .resx (default: auto-detected base file)")
        parser.add_argument("--dry-run", action="store_true",
                            help="Preview without creating")
        args = parser.parse_args()

        ensure_utf8()
        base = args.path.resolve()

        if not base.exists() or not base.is_dir():
            print(f"directory not found: {base}")
            sys.exit(1)

        default_name, _, _ = auto_detect_pattern(base)
        source_name = args.source or default_name
        source_path = base / source_name

        if not source_path.exists():
            print(f"source not found: {source_path}")
            sys.exit(1)

        stem = Path(source_name).stem
        ext = Path(source_name).suffix
        new_name = f"{stem}.{args.locale}{ext}"
        new_path = base / new_name

        if new_path.exists():
            print(f"already exists: {new_name}")
            sys.exit(1)

        source_data = load_resx(source_path)
        if source_data is None:
            sys.exit(1)

        if args.dry_run:
            print(f"[dry-run] would create: {new_name} ({len(source_data)} keys from {source_name})")
            return

        shutil.copy2(source_path, new_path)
        print(f"Created: {new_name} ({len(source_data)} keys copied from {source_name})")

    except KeyboardInterrupt:
        print("\ncancelled")
        sys.exit(130)
    except Exception as ex:
        print(f"\nfatal: {ex}")
        sys.exit(1)


if __name__ == "__main__":
    main()
