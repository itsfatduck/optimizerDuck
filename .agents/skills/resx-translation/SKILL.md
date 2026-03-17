---
name: resx-translation
description: Translate, extend, and create `.resx` localization files for .NET apps with project-aware wording and minimal file churn. Use this skill whenever the user wants to localize or update `Translations.resx`, `*.resx`, WPF/WinForms/.NET resource files, add a new language like `vi`, `ja`, or `fr-FR`, fill missing translations, or improve an existing locale while preserving XML structure, placeholders, naming conventions, and current formatting. Trigger even when the user does not mention `.resx` explicitly but is clearly asking to localize app text, UI labels, menus, dialogs, settings, validation messages, or existing app translations. Also trigger for requests like "translate this resx", "add a new language", "fill missing strings", "localize my app", "translate resource files", or "complete the existing locale".
---

# Resx Translation

The goal of this skill is to translate `.resx` files with product-aware context, preserve the resource structure, and change only what needs to be changed.

## When to use it

Use this skill when the user wants to:

- Translate a `.resx` file into another language
- Fill in missing strings in an existing localized file
- Add a new resource file for a language code like `vi`, `ja`, or `fr-FR`
- Update translations for a WPF, WinForms, .NET, or any app that uses `.resx`

## Workflow

### 1. Understand the project before translating

Before editing any file, inspect the project to understand what kind of app it is, who it is for, and what domain it belongs to. Prefer reading files like `README`, project files, UI screens, settings, menus, view models, or any code that reveals domain context so the translation sounds natural.

The point is to avoid overly literal translations when the domain needs more specialized language. For example:

- music apps: keep terms like playlist, mix, mastering, or equalizer when appropriate
- configuration/system apps: prefer concise, professional, technical wording
- business apps: prefer clarity, stability, and consistent business terminology

### 2. Identify the source resx file and its real format

Check the default/source resx file first. Priority order:

1. `Translations.resx` if it exists
2. Any `*.resx` file that does not include a language code in the filename
3. If the project uses English as a base language, also inspect `*.en.resx` if useful

The goal is to understand:

- the filename and naming convention
- whether the project uses patterns like `Name.resx`, `Name.vi.resx`, `Name.ja.resx`, or something else
- XML formatting, line breaks, node order, spacing, and any existing conventions
- the file encoding and whether it uses BOM, UTF-8, UTF-16, or another existing pattern
- which strings are actually user-facing text and which are metadata or technical values

Follow the existing format. Do not regroup, reorder, reformat, add comments, or beautify the file unless the original file already does that.

Preserve encoding carefully. When saving `.resx` files, avoid introducing encoding changes unless there is a clear reason. Match the existing file encoding whenever possible so the XML declaration, localized characters, and tooling behavior stay stable.

### 3. Check whether language support also needs code or settings updates

Do not stop at the `.resx` files. When the user asks to add a new language, also inspect the project for any language-selection or localization plumbing that must be updated so the new locale can actually appear and be used in the app.

Look for related code such as:

- settings pages or language dropdowns
- supported language lists or culture registries
- localization services, resource managers, or startup configuration
- menu items, options screens, or app preferences tied to language selection

If the project requires these updates, make them together with the new `.resx` file. If the project already discovers languages automatically, avoid unnecessary code changes.

### 4. Determine the request type

Clearly distinguish between these two cases:

- **Extend/fill an existing language**: update the existing localized file and translate only what is needed
- **Create a new language**: duplicate the source file into a new file with the requested language code, then translate each entry

If the user's request is explicit, follow it. If the request is ambiguous but the files make the intent obvious, prefer the least disruptive path: extend an existing localized file if it already exists; otherwise create a new one when the requested language code does not exist.

### 5. If the task is to extend an existing language

Read the existing localized file together with the source file, then translate entries that are missing, still left in the source language, or clearly wrong for the project context.

Prefer targeted edits. If the localized file already exists, update only missing, source-language, or clearly incorrect entries unless the user explicitly asks for a full translation pass or a rewrite of the whole locale.

When extending translations:

- keep the original meaning but prefer wording that fits the product context
- do not translate word-for-word mechanically
- respect the tone of the existing localized file if it is already good
- keep terminology consistent within the file and across the project

### 6. If the task is to create a new language

Duplicate the source `.resx` file into a new file using the requested language code. Example: `Translations.resx` -> `Translations.vi.resx`.

Then translate each translatable entry in the new file while preserving the XML structure and all attributes from the source.

### 7. File update strategy

Apply changes in the least disruptive way that still keeps the file correct.

- For small files or narrowly scoped edits, direct rewrite is fine if you can preserve the exact structure and encoding.
- For larger files, prefer targeted updates that touch only the specific entries that need translation instead of regenerating or rewriting the whole file.
- Avoid full-file rewrites when the task is only to fill missing entries or correct a subset of strings.
- Preserve ordering, indentation, XML declaration, and surrounding nodes so diffs stay small and reviewable.

The goal is to keep the change set precise, safe, and easy for the user to review.

### 8. Translation rules

Translate only the user-facing text. Do not change keys, node names, attributes, or XML structure.

Preserve exactly:

- placeholders such as `{0}`, `{1}`, `%s`, `%d`
- markup, HTML, XAML fragments, newline markers, and escaped characters
- product names, feature names, shortcuts, error codes, commands, and any terminology that should remain unchanged in context
- empty strings or technical strings that are not user-facing, unless the source file shows they are meant to be localized

Be careful with short strings like `Open`, `Save`, `Apply`, `Reset`, `Close`, `Track`, `Mix`, `Profile`, `Library`, `Device`, and `Output`, because the right translation depends heavily on the screen and domain. If needed, inspect nearby code or UI context before deciding.

Keep English terms unchanged when they function more like product language, technical labels, or domain-specific jargon than ordinary UI text. This often applies to terms such as `Playlist`, `Profile`, `Output`, `Driver`, `Theme`, and `Preset` when the surrounding product language, target audience, or existing translations suggest they should stay in English. Translate them only when the app clearly treats them as normal user-facing words in the target locale and the rest of the product uses localized equivalents consistently.

### 9. File editing behavior

Edit the target file directly. Do not add explanations into the file. Do not add comments. Do not create translation tables. Do not regroup entries. Do not change casing style, indentation, or layout unless the original file already follows a specific pattern.

If you report back to the user, keep it brief and focus on:

- which file was updated
- which file was created, if any
- which language was added or extended

## Suggested execution order

1. Understand the project and domain
2. Find the source resx file and any related localized files
3. Check whether language settings or localization code also need updates
4. Determine whether this is an extension task or a new-language task
5. Update or create the correct file using the project's naming convention
6. Translate strings with domain-aware wording while preserving format and encoding
7. Recheck placeholders, XML integrity, terminology consistency, and any required language-selection wiring before finishing

## Expected output

The main output is an updated `.resx` file that preserves the original format.

If you respond in chat, keep the response short and focused on the file result.
