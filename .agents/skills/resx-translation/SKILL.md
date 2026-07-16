---
name: resx-translation
description: Translate, extend, and create `.resx` localization files for .NET apps with project-aware wording and minimal file churn. Use this skill whenever the user wants to localize or update `Translations.resx`, `*.resx`, WPF/WinForms/.NET resource files, add a new language like `vi`, `ja`, or `fr-FR`, fill missing translations, or improve an existing locale while preserving XML structure, placeholders, naming conventions, and current formatting. Trigger even when the user does not mention `.resx` explicitly but is clearly asking to localize app text, UI labels, menus, dialogs, settings, validation messages, or existing app translations. Also trigger for requests like "translate this resx", "add a new language", "fill missing strings", "localize my app", "translate resource files", or "complete the existing locale".
---

# Resx Translation Skill

## Golden Rule: Use helpers, NEVER `read` the full file

**Do NOT use `read`/`glob`/`grep`** to discover files, check encoding, or audit translations.
Use the helper scripts — they return structured data in <10 lines, saving hundreds of tokens.

**Never `read` an entire `.resx` file.** Files can exceed 100k tokens.
Use `(filename:line)` annotations from helpers to `read` only the relevant lines:

```
# resx_audit shows: [-] AppTitle  (default:L42 #3)  = 'OptimizerDuck'
read Translations.vi-VN.resx offset=42 limit=5
```

Only `read` 1-5 lines around each flagged key — never the whole file.

---

## Helper Scripts Reference

All scripts are in `<skill>\helpers\`. Replace `<skill>` with the skill base path.

---

### `resx_core.py` — Core library (do not run directly)

Shared data structures and operations used by all other scripts:
- `ResxEntry` / `ResxFile` — parsed .resx with index, line numbers, encoding
- `parse_resx(path)` — parse a .resx file
- `auto_detect(directory)` — detect base file and locale pattern
- `diff_files(default, locale)` — compare two files, returns `KeyDiff` list
- `write_translations(path, dict, mode)` — batch add/update keys
- `add_keys(path, dict)` — add new keys before `</root>`
- `update_values(path, dict)` — update existing key values
- `parse_batch_input(raw)` — parse KEY=VALUE pairs from text
- `format_entries_table(entries)` — compact aligned table output
- `format_diffs_compact(diffs)` — compact diff output with status markers

---

### `resx_audit.py` — Audit translations (missing, empty, untranslated, warnings)

**What it does**: Compares all locale files against the default. Reports issues with line numbers, indices, and warnings (mixed encodings, key count mismatch, duplicate keys, Designer.cs sync).

**Usage**:
```powershell
python <skill>\helpers\resx_audit.py Resources\Languages
python <skill>\helpers\resx_audit.py Resources\Languages --lang vi-VN
python <skill>\helpers\resx_audit.py Resources\Languages --json
```

**Flags**:
| Flag | Description |
|------|-------------|
| `--lang CODE` | Filter by locale (e.g. `vi`, `ja-JP`) |
| `--no-missing` | Hide missing keys |
| `--no-empty` | Hide empty values |
| `--no-untranslated` | Hide untranslated |
| `--no-warnings` | Hide warnings |
| `--check-designer` | Audit `.Designer.cs` sync |
| `--by-key` | Group by key name |
| `--json` | JSON output (full metadata per issue) |
| `--fail-on-any` | Exit code 1 if any issues (CI gate) |

**Output**:
```
============================================================
resx manager — audit
============================================================
  default   : Translations.resx (45 keys)
  locales   : 13

  [vi-VN] — Translations.vi-VN.resx
    missing=15 | untranslated=5

    missing (15):
      [-] AppTitle  (default:L42 #3)  = 'OptimizerDuck'
      [-] ButtonCancel  (default:L55 #12)  = 'Cancel'

    untranslated (5):
      [U] LabelStatus  (Translations.vi-VN.resx:L120 #45)

  warnings (1):
    ! mixed encodings: UTF-8 with BOM, UTF-8 (no BOM)

============================================================
summary
============================================================
  keys        : 45
  locales     : 13
  missing     : 15
  untranslated: 5
  warnings    : 1
```

---

### `resx_list.py` — List all translations with index and line numbers

**What it does**: Lists every translation entry with index, key, line number, and value. Supports locale filtering and key search.

**Usage**:
```powershell
# List all files and entries
python <skill>\helpers\resx_list.py Resources\Languages

# Filter by locale
python <skill>\helpers\resx_list.py Resources\Languages --lang vi-VN

# Search keys
python <skill>\helpers\resx_list.py Resources\Languages --key Dashboard

# Keys only (no values)
python <skill>\helpers\resx_list.py Resources\Languages --keys-only

# JSON output
python <skill>\helpers\resx_list.py Resources\Languages --json
```

**Flags**:
| Flag | Description |
|------|-------------|
| `--lang CODE` | Filter by locale |
| `--key TEXT` | Filter keys by substring |
| `--keys-only` | Show only keys, no values |
| `--no-values` | Hide values entirely |
| `--json` | JSON output |

**Output**:
```
============================================================
resx manager — file listing
============================================================
  directory : Resources\Languages
  default   : Translations.resx (45 keys, UTF-8 with BOM)
  locales   : 13

--- Translations.resx (default) ---
  [#0  ] Sidebar.Dashboard              = 'Dashboard'        (Translations.resx:L125)
  [#1  ] Sidebar.Settings               = 'Settings'         (Translations.resx:L127)
  [#2  ] Button.Close                   = 'Close'            (Translations.resx:L133)
  [#3  ] Dashboard.SystemInfo.Cpu.Cores = '{0} Cores'        (Translations.resx:L136)
```

---

### `resx_create.py` — Create a new locale file

**What it does**: Copies the source `.resx` to create a new locale file. Preserves encoding, BOM, XML structure.

**Usage**:
```powershell
python <skill>\helpers\resx_create.py Resources\Languages --locale vi-VN
python <skill>\helpers\resx_create.py Resources\Languages --locale ja-JP --dry-run
```

**Flags**:
| Flag | Description |
|------|-------------|
| `--locale` | **(Required)** Language code |
| `--source` | Source filename (default: auto-detected) |
| `--dry-run` | Preview without creating |
| `--json` | JSON output |

---

### `resx_sync.py` — Sync missing keys from source to locales

**What it does**: Copies keys that exist in source but are missing from locale files.

**Usage**:
```powershell
# Sync with English fallback values
python <skill>\helpers\resx_sync.py Resources\Languages --fill-source

# Sync with TODO markers
python <skill>\helpers\resx_sync.py Resources\Languages --fill-marker "TODO"

# Sync specific locale only
python <skill>\helpers\resx_sync.py Resources\Languages --locale vi-VN --fill-source
```

**Flags**:
| Flag | Description |
|------|-------------|
| `--fill-source` | Copy source value as-is |
| `--fill-marker "TEXT"` | Fill with marker string |
| `--locale CODE` | Target specific locale |
| `--dry-run` | Preview without writing |
| `--json` | JSON output |

---

### `resx_write.py` — Batch write translations to multiple locales

**What it does**: Write/update translation keys across multiple locale files. Supports `--key`/`--value`, `--file`, or `--stdin` input. Modes: `auto` (add+update), `update` (existing only), `add` (new only).

**Usage**:
```powershell
# Single key to all locales
python <skill>\helpers\resx_write.py Resources\Languages --key AppTitle --value "OptimizerDuck" --locale all

# Single key to specific locale
python <skill>\helpers\resx_write.py Resources\Languages --key Button.Close --value "Đóng" --locale vi-VN

# Write from file (KEY=VALUE or JSON)
python <skill>\helpers\resx_write.py Resources\Languages --file translations.txt --locale vi-VN

# Write from stdin
echo "Button.Close=Đóng" | python <skill>\helpers\resx_write.py Resources\Languages --stdin --locale vi-VN

# Dry run
python <skill>\helpers\resx_write.py Resources\Languages --key Button.Close --value "Đóng" --locale vi-VN --dry-run
```

**KEY=VALUE file format**:
```
# Comments and blank lines are ignored
Button.Close=Đóng
Button.Save=Lưu
Sidebar.Dashboard=Bảng điều khiển
```

**Flags**:
| Flag | Description |
|------|-------------|
| `--key TEXT` | Single key to write |
| `--value TEXT` | Value for `--key` |
| `--file PATH` | File with KEY=VALUE or JSON |
| `--stdin` | Read from stdin |
| `--locale CODE` | Target locale (`vi`, `ja`, `all`) |
| `--mode` | `auto` / `update` / `add` / `upsert` |
| `--dry-run` | Preview without writing |
| `--json` | JSON output |
| `--source` | Source .resx to create locale from if missing |

---

## Task Dispatch

| User says | Action |
|---|---|
| *"dịch các translation còn thiếu"* | `resx_audit` → ask → `resx_write` for flagged keys |
| *"dịch ngôn ngữ mới"* | `resx_create` → `resx_sync --fill-source` → `resx_write` |
| *"dịch các translation"* | `resx_audit --no-missing` → `resx_write` for untranslated |
| *"cập nhật translations"* | `resx_sync --fill-source` → `resx_audit` → `resx_write` |
| *"liệt kê translations"* | `resx_list` |
| *"tìm key X"* | `resx_list --key X` |
| *"kiểm tra Designer.cs"* | `resx_audit --check-designer` |

---

## Workflow

### 0. Always ask before translating

**Before any translation work**, use the `question` tool to:

1. **If multiple locales have issues**: Ask which locale(s) to work on
2. **Confirm the approach**: Describe what `resx_audit` found and what you plan to do
3. **Domain context**: If the app has specialized terminology, ask about preferred translations

---

### A. Fill Missing Translations

1. **Run `resx_audit.py`** to find issues
2. **Ask user**: which locale, confirm scope
3. **Use `resx_write.py`** to write translations:
   ```powershell
   python <skill>\helpers\resx_write.py Resources\Languages --key Button.Close --value "Đóng" --locale vi-VN
   ```
   Or batch from file:
   ```powershell
   python <skill>\helpers\resx_write.py Resources\Languages --file translations.txt --locale vi-VN
   ```
4. **Verify**: run `resx_audit.py` again

---

### B. Add New Language

1. **Run `resx_list.py`** to see existing files
2. **Ask user**: confirm language code
3. **Run `resx_create.py --locale <code>`**
4. **Run `resx_sync.py --fill-source`** to fill with English fallback
5. **Run `resx_audit.py --lang <code>`** to find untranslated
6. **Use `resx_write.py`** to translate all untranslated keys

---

### C. Batch Translate from File

1. Create a KEY=VALUE file:
   ```
   Button.Close=Đóng
   Button.Save=Lưu
   Sidebar.Dashboard=Bảng điều khiển
   ```
2. **Run `resx_write.py --file translations.txt --locale vi-VN`**
3. **Verify**: `resx_audit.py --lang vi-VN`

---

## Translation Rules

- Translate **only user-facing text**. Never change keys, node names, attributes, or XML structure.
- Preserve exactly: `{0}`, `{1}`, `%s` placeholders; HTML/XAML fragments; product names; error codes.
- Short strings (`Open`, `Save`, `Apply`, `Reset`) depend on context — inspect nearby code if unsure.
- Keep English terms that function as product language (`Playlist`, `Profile`, `Driver`, `Theme`, `Preset`).
- Preserve original encoding (UTF-8 with/without BOM).

## Editing Behavior

- Use `resx_write.py` for all writes — never manually edit `.resx` files.
- The script preserves XML structure, encoding, and indentation.
- Report back briefly: which file was updated, language, key count.

## Quick Reference

| Need | Command |
|---|---|
| Audit all locales | `resx_audit.py <dir>` |
| Audit specific locale | `resx_audit.py <dir> --lang vi-VN` |
| List all translations | `resx_list.py <dir>` |
| Search keys | `resx_list.py <dir> --key Dashboard` |
| Create new locale | `resx_create.py <dir> --locale vi-VN` |
| Sync missing keys | `resx_sync.py <dir> --fill-source` |
| Write single key | `resx_write.py <dir> --key K --value V --locale vi-VN` |
| Write from file | `resx_write.py <dir> --file tr.txt --locale vi-VN` |
| Write to all locales | `resx_write.py <dir> --key K --value V --locale all` |
| CI check | `resx_audit.py <dir> --fail-on-any` |
| Designer.cs check | `resx_audit.py <dir> --check-designer` |
