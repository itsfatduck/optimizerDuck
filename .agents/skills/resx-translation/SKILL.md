---
name: resx-translation
description: Translate, extend, and create `.resx` localization files for .NET apps with project-aware wording and minimal file churn. Use this skill whenever the user wants to localize or update `Translations.resx`, `*.resx`, WPF/WinForms/.NET resource files, add a new language like `vi`, `ja`, or `fr-FR`, fill missing translations, or improve an existing locale while preserving XML structure, placeholders, naming conventions, and current formatting. Trigger even when the user does not mention `.resx` explicitly but is clearly asking to localize app text, UI labels, menus, dialogs, settings, validation messages, or existing app translations. Also trigger for requests like "translate this resx", "add a new language", "fill missing strings", "localize my app", "translate resource files", or "complete the existing locale".
---

# Resx Translation

## Golden Rule: Use helpers, NEVER `read` the full file

**Do NOT use `read`/`glob`/`grep`** to discover files, check encoding, or audit translations.  
Use the helper scripts — they return structured data in <10 lines, saving hundreds of tokens.

**Never `read` an entire `.resx` file.** Files can exceed 100k tokens.  
Use `(filename:line)` annotations from `audit_translations.py` to `read` only the relevant lines:

```
# audit shows: - AppTitle = 'OptimizerDuck'  (Translations.resx:42)
read Translations.vi.resx offset=42 limit=5
read Translations.resx offset=42 limit=5
```

Only `read` 1-5 lines around each flagged key — never the whole file.

---

## Helper Scripts Reference

All scripts are in `<skill>\helpers\`. Replace `<skill>` with the skill base path (run `codegraph_node resx-translation` or check `.agents/skills/resx-translation`).

---

### `find_resx.py` — Discover `.resx` files, encoding, Designer.cs status

**What it does**: Lists all `.resx` files in a directory — key count, encoding (UTF-8 with/without BOM), file size, and whether `.Designer.cs` is in sync. Use this **instead of** `glob` + `read`.

**Usage**:
```powershell
python <skill>\helpers\find_resx.py Resources\Languages
```

**Flags**:
| Flag | Description |
|------|-------------|
| `--json` | Output as JSON for programmatic use |
| (none) | Human-readable table |

**Output**:
```
════════════════════════
resx discovery
════════════════════════
  directory: Resources\Languages
  default  : Translations.resx
  pattern  : Translations.*.resx
  files    : 3

  Translations.resx
    keys    : 45
    size    : 3200 bytes
    encoding: UTF-8 with BOM
    designer: Translations.Designer.cs (synced)

  Translations.vi.resx
    keys    : 30
    size    : 2400 bytes
    encoding: UTF-8 with BOM
    designer: (none)
```

**Next step**:  
- If adding a new locale → `create_locale.py`.  
- If auditing/translating → `audit_translations.py`.

---

### `audit_translations.py` — Find missing / empty / untranslated keys

**What it does**: Compares all locale files against the union of all keys. Reports keys that are **missing** (absent from a locale), **empty** (present but blank), or **untranslated** (identical to the default file). Use this **instead of** manually diffing files.

**Usage**:
```powershell
python <skill>\helpers\audit_translations.py Resources\Languages --auto-detect --show-missing --show-untranslated
```

**Flags**:
| Flag | Description |
|------|-------------|
| `--show-missing` | Keys absent from a locale that exist in others |
| `--show-empty` | Keys with empty values |
| `--show-untranslated` | Keys whose value matches the default file |
| `--by-key` | Group results by key name instead of by locale |
| `--fail-on-any` | Exit code 1 if any issues found (CI gate) |
| `--auto-detect` | Auto-detect file pattern from directory |
| `--check-designer` | Also audit `.Designer.cs` for missing/stale properties |
| `--default-file` | Custom base filename (e.g. `Strings.resx`) |
| `--pattern` | Custom locale glob (e.g. `Strings.*.resx`) |

**Output** (with `--show-missing --show-untranslated`):
```
════════════════════════
translation audit
════════════════════════

  [vi]
    missing=15 | untranslated=5

    missing keys (15):
      - AppTitle = 'OptimizerDuck'  (Translations.resx:42)
      - ButtonCancel = 'Cancel'  (Translations.resx:55)
      ...

    untranslated values (5):
      - LabelStatus  (Translations.vi.resx:120)
      - TextWelcome  (Translations.vi.resx:135)
      ...

  [ja]
    ok

════════════════════════
summary
════════════════════════
  locales: 3
  keys   : 45
  pattern: Translations.*.resx
  missing: 15
  untranslated: 5
```

**Clean output** (no issues):
```
  all translations look good across all locales
```

**Next step**:  
- Issues found → use `question` tool to ask user which locale → `read` that file + source → translate only flagged keys.  
- Clean → nothing to do.

---

### `create_locale.py` — Add a new language file

**What it does**: Duplicates the source `.resx` into a new locale file (e.g. `Translations.vi.resx`). Uses `shutil.copy2` to preserve exact encoding, BOM, metadata. **30x faster** than reading + rewriting XML.

**Usage**:
```powershell
python <skill>\helpers\create_locale.py Resources\Languages --locale vi
```

**Flags**:
| Flag | Description |
|------|-------------|
| `--locale` | **(Required)** Language code: `vi`, `ja`, `fr-FR`, `de`, etc. |
| `--source` | Source filename (default: auto-detected base `.resx`) |
| `--dry-run` | Preview without creating |

**Output**:
```
Created: Translations.vi.resx (45 keys copied from Translations.resx)
```

If the file already exists:
```
already exists: Translations.vi.resx
```

**Next step**: Translate ALL values in the new file → run `sync_resx.py --fill-source` to catch any keys added to source after creation.

---

### `sync_resx.py` — Propagate new keys from source to locales

**What it does**: Copies newly added keys from the source `.resx` into all (or a specific) locale files. New `<data>` elements are inserted before `</root>`, preserving XML structure and indentation.

**Usage**:
```powershell
python <skill>\helpers\sync_resx.py Resources\Languages --fill-source
```

**Flags**:
| Flag | Description |
|------|-------------|
| `--fill-source` | Copy source value as-is (English fallback) |
| `--fill-marker "TODO"` | Fill with a marker string instead |
| (no fill flags) | Fill with empty string |
| `--locale vi-VN` | Target a specific locale only |
| `--dry-run` | Preview without writing |
| `--source` | Custom source filename |
| `--target` | Custom locale glob pattern |

**Output**:
```
════════════════════════
resx sync
════════════════════════
  Translations.vi.resx: added 3 key(s) (vi)
  Translations.ja.resx: ok (already up to date)

════════════════════════
result
════════════════════════
  total keys added: 3
```

**Next step**:  
- `--fill-source` → those keys have English text and need translation → run `audit_translations.py --show-untranslated` to find them.  
- `--fill-marker "TODO"` → search for and translate markers.

---

## Task Dispatch: Determine what the user needs

| User says (Vietnamese) | Meaning | Action |
|---|---|---|
| *"dịch các translation còn thiếu"* / *"translate missing translations"* | Fill only missing/untranslated keys | Run `audit` → ask → translate **only flagged keys** |
| *"dịch ngôn ngữ mới"* / *"add new language"* | Brand new locale | `create_locale.py` → translate **all keys** |
| *"dịch các translation"* / *"translate translations"* | General translate request | Run `audit` → find untranslated → ask → translate |
| *"cập nhật translations"* / *"update translations"* | Sync + translate new keys | `sync_resx.py` → `audit` → translate untranslated |

---

## Workflow

### 0. Always ask before translating

**Before any translation work**, use the `question` tool to:

1. **If multiple locales have issues**: Ask which locale(s) to work on
2. **Confirm the approach**: Describe what `audit` found and what you plan to do (how many keys, which file)
3. **Domain context**: If the app has specialized terminology, ask about preferred translations

This prevents wasted work. Example:

> `audit` shows `vi` has 15 missing and 5 untranslated keys out of 45 total.  
> `ja` is fully translated.  
> Should I proceed with translating `vi` only?

---

### A. "Dịch các translation còn thiếu" — Fill Missing Translations

1. **Run `audit_translations.py --show-missing --show-untranslated`** to find issues
2. **Ask user** (via `question`): which locale, confirm scope
3. **`read` only specific lines** in the locale file + source file using line numbers from the audit output (e.g., `read Translations.vi.resx offset=42 limit=5`)
4. **Translate only flagged keys** — skip everything else
5. Report: which file, language, how many keys translated

---

### B. "Dịch ngôn ngữ mới" — Add New Language

1. **Run `find_resx.py`** to see existing files and encoding
2. **Ask user**: confirm language code + any domain-specific preferences
3. **Run `create_locale.py --locale <code>`** to create the file
4. **Translate ALL values** in the new file
5. **Run `sync_resx.py --fill-source`** to catch any keys added after creation
6. Report: file created, language, key count

---

### C. "Dịch các translation" — Translate Existing Translations

1. **Run `audit_translations.py --show-untranslated`** to find untranslated keys
2. **Ask user** (via `question`): which locale(s), confirm scope
3. **`read` only specific lines** in the locale file + source file using line numbers from the audit output
4. **Translate only untranslated keys**
5. Report: file, language, keys translated

---

## Translation Rules

- Translate **only user-facing text**. Never change keys, node names, attributes, or XML structure.
- Preserve exactly: `{0}`, `{1}`, `%s` placeholders; HTML/XAML fragments; product names; error codes; escaped characters.
- Short strings (`Open`, `Save`, `Apply`, `Reset`, etc.) depend on screen context — inspect nearby code if unsure.
- Keep English terms when they function as product language (`Playlist`, `Profile`, `Driver`, `Theme`, `Preset`) unless the app consistently localizes them.
- Preserve original encoding (UTF-8 with/without BOM).

## Editing Behavior

- Edit the target file directly using `edit` tool.
- No explanations, comments, translation tables, or layout changes added to the file.
- Preserve ordering, indentation, XML declaration, surrounding nodes.
- Report back briefly: which file was updated/created, language, key count.

## Quick Reference

| Need | Command |
|---|---|
| See all `.resx` files, encoding, Designer.cs | `find_resx.py <dir>` |
| Find missing / untranslated keys | `audit_translations.py <dir> --show-missing --show-untranslated` |
| Find empty values | `audit_translations.py <dir> --show-empty` |
| Create new locale | `create_locale.py <dir> --locale <code>` |
| Sync new keys to locales | `sync_resx.py <dir> --fill-source` |
| Preview sync | `sync_resx.py <dir> --fill-marker "TODO" --dry-run` |
| CI check | `audit_translations.py <dir> --fail-on-any` |
