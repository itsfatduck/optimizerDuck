# find_resx.py — Discover .resx files, naming patterns, and Designer.cs bindings

from __future__ import annotations

try:
    import argparse
    import json
    import sys
    from pathlib import Path
except ImportError as ex:
    print(f"failed to import required module: {ex}")
    raise SystemExit(1)

sys.path.insert(0, str(Path(__file__).resolve().parent))
from resx_helpers import (
    auto_detect_pattern,
    ensure_utf8,
    find_designer,
    load_designer_keys,
    load_resx,
    print_header,
    resx_key_to_property,
)


def detect_encoding(path: Path) -> str:
    raw = path.read_bytes()
    if raw[:3] == b"\xef\xbb\xbf":
        return "UTF-8 with BOM"
    return "UTF-8 (no BOM)" if raw[:5] == b"<?xml" else "unknown"


def scan(base: Path) -> list[dict]:
    if not base.is_dir():
        print(f"not a directory: {base}")
        sys.exit(1)

    all_resx = sorted(base.glob("*.resx"))
    if not all_resx:
        return []

    default_name, pattern, _ = auto_detect_pattern(base)

    result = []
    for rp in all_resx:
        data = load_resx(rp)
        if data is None:
            continue

        info = {
            "file": rp.name,
            "size": rp.stat().st_size,
            "encoding": detect_encoding(rp),
            "keys": len(data),
        }

        designer = find_designer(base, rp.name)
        if designer:
            props = load_designer_keys(designer)
            resx_props = {resx_key_to_property(k) for k in data}
            info["designer"] = designer.name
            info["designer_props"] = len(props)
            info["synced"] = props == resx_props
            out_of_sync = (resx_props - props) | (props - resx_props)
            info["out_of_sync_count"] = len(out_of_sync)

        result.append(info)

    return result


def main():
    try:
        parser = argparse.ArgumentParser(
            description="Discover .resx files, naming patterns, and Designer.cs bindings."
        )
        parser.add_argument("path", nargs="?", type=Path, default=Path("."),
                            help="Translations directory (default: current)")
        parser.add_argument("--json", action="store_true", help="Output as JSON")
        args = parser.parse_args()

        ensure_utf8()
        base = args.path.resolve()

        if not base.exists():
            print(f"directory not found: {base}")
            sys.exit(1)

        default_name, pattern, _ = auto_detect_pattern(base)
        files = scan(base)

        if not files:
            print(f"no .resx files found in {base}")
            sys.exit(0)

        if args.json:
            json.dump({"default": default_name, "pattern": pattern, "files": files},
                      sys.stdout, indent=2)
            print()
            return

        print_header("resx discovery")
        print(f"  directory: {base}")
        print(f"  default  : {default_name}")
        print(f"  pattern  : {pattern}")
        print(f"  files    : {len(files)}")

        for info in files:
            designer = info.get("designer")
            synced = info.get("synced")
            if designer:
                status = "synced" if synced else f"{info['out_of_sync_count']} out of sync"
            else:
                status = "no Designer.cs"

            print(f"\n  {info['file']}")
            print(f"    keys    : {info['keys']}")
            print(f"    size    : {info['size']} bytes")
            print(f"    encoding: {info['encoding']}")
            print(f"    designer: {designer or '(none)'} ({status})")

    except KeyboardInterrupt:
        print("\ncancelled")
        sys.exit(130)
    except Exception as ex:
        print(f"\nfatal: {ex}")
        sys.exit(1)


if __name__ == "__main__":
    main()
