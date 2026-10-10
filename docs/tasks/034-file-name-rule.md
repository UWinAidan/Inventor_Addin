# 034: File name rule and part name (Core)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (File names and the part name)
- **Status:** done
- **Depends on:** 033
- **Needs:** cloud
- **Parallel-safe with:** 035, 036, 037, 042 (not 038: both may touch the view-model tests)

## Goal

Core can build a file name from a part number and a part name (`AWB-10001,mounting_bracket.ipt`), check a typed name, and read the part name back out of a file name. The Part Properties window then shows and writes the part name by the new rule without any change to the window.

## Scope

- `src/InventorAddin.Core/Numbering/PartFileName.cs` (new)
- `src/InventorAddin.Core/PartProperties/PartName.cs` (new rule)
- `tests/InventorAddin.Core.Tests/Numbering/PartFileNameTests.cs` (new)
- `tests/InventorAddin.Core.Tests/PartProperties/PartNameTests.cs` (cases for the new rule)
- Any existing test in `tests/InventorAddin.Core.Tests` whose expected part name changes because of the underscore rule (update the expectation, do not weaken the test)

Out of scope:

- `PartNumberDisplay` (replaced in task 039).
- Renaming files (task 047).

## Acceptance criteria

Typed names

- [x] `PartFileName.NormaliseName(string? typed)`: trims, turns each underscore into a space, and collapses each run of white space to one space. Case is kept
- [x] `PartFileName.ValidateName(string? typed)` returns null when the name is usable, or a message for the first problem found:
  - blank after normalising: `"Enter a part name."`
  - a character Windows does not allow in a file name (`\ / : * ? " < > |` or a control character): `"A part name cannot contain the character 'X'."` naming the character (control characters as `U+000A` form)
  - a name ending in a dot: `"A part name cannot end with a dot."`
  - a Windows reserved name, ignoring case, alone or before a dot (`CON`, `PRN`, `AUX`, `NUL`, `COM1` to `COM9`, `LPT1` to `LPT9`): `"'CON' is a name Windows keeps for itself."`
- [x] A comma in the name is allowed

Building

- [x] `PartFileName.Build(PartNumber? number, string name, string extension)`: the name normalised, spaces replaced by underscores, then `{number},{name}{extension}` or `{name}{extension}` when `number` is null. `extension` includes its dot (`.ipt`); throws `ArgumentException` for an extension without a leading dot, and for a name that fails `ValidateName`
- [x] `PartFileName.MaxPathLength = 259`, and `PartFileName.ValidatePath(string folder, string fileName)` returns `"The full path is too long for Windows. Choose a shorter name or folder."` when `folder` + separator + `fileName` exceeds it, otherwise null

Reading

- [x] `PartName.FromFileName` follows the spec: file name without folder and last extension; if it contains a comma, the text after the first comma; underscores shown as spaces; trimmed. Blank or null gives `""` as now
- [x] `PartFileName.TryReadNumber(string? fullFileName, out PartNumber number)`: true when the file name has a comma and the text before the first comma parses as a part number
- [x] Tests: `C:\Work\AWB-10001,mounting_bracket.ipt` gives `mounting bracket` and number `AWB-10001`; `AWB-10001,plate,_10mm.ipt` gives `plate, 10mm`; `Old_bracket.v2.ipt` gives `Old bracket.v2` and no number; `AWB-10001,.ipt` gives `""`; `notanumber,name.ipt` gives `name` and no number; `/` separators on Linux. Build then read gives back the normalised name for a range of names
- [x] `PartPropertiesWritePlan` and the view-model are not changed, and their tests pass (update an expectation only where a test name contains an underscore)
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Aidan decided the file name format and the comma rule. Applying the underscore rule to old file names without a comma is a Default (`DECISIONS.md`).
- Keep `PartName.FromFileName`'s signature and its handling of `\` and `/` on every platform. The Part Properties header and the Description write both call it, so they follow the new rule automatically.
- The reserved-name list and invalid characters are fixed Windows rules; write them as data, not as a series of `if`s.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 errors, 0 warnings. `dotnet test tests/InventorAddin.Core.Tests`: 1053 passed, 0 failed (run with 035, 037 and 038 changes in the same working tree). The `PartFileName` and `PartName` tests alone: 120 passed, 0 failed.
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` changed.
- **Inventor API members not confirmed:** none; no Inventor API used.
- **Manual checklist for Inventor:** none here; task 039's checklist opens a file named in the new format
- **Notes:**
  - No existing test expectation changed: no existing test file name contained an underscore or comma. `PartPropertiesWritePlan`, the view-model and `PartNumberDisplay` are unchanged.
  - `ValidateName` checks characters in the text as typed, before white space is collapsed, so a line break or tab is reported as `'U+000A'` / `'U+0009'` rather than silently turned into a space. Blank is still checked first, on the normalised name.
  - The code-point message keeps the quotes from the template: `A part name cannot contain the character 'U+000A'.`
  - `ValidatePath` counts no separator when the folder is blank or already ends with `\` or `/`.
  - `Build` also rejects `.` alone as an extension.

## Follow-ups

Things noticed but not done.

- **Comma in a name without a number.** `Build(null, "a,b", ".ipt")` gives `a,b.ipt`, which reads back as part name `b`, because reading takes everything after the first comma whatever comes before it. The spec allows a comma in the name and allows files without a number, so the two rules clash for that case. Question for spec 02: should a name be read from after the first comma only when the text before it is a part number, or should a comma be refused in a name for a file without a number? (Affects Rename, task 047.)
- **`PartNumberDisplay` now compares against the new part name.** It treats a Part Number equal to `PartName.FromFileName` as "not assigned". For `AWB-10001,mounting_bracket.ipt`, Inventor's file-name fallback is `AWB-10001,mounting_bracket`, which no longer equals the part name `mounting bracket`, so that fallback would show as an assigned number until task 039 replaces the rule. Old files without commas or underscores behave as before.
