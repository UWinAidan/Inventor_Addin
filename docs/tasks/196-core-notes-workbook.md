# 196: Automatic notes: workbook and rules (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 1; open questions 12, 15 and 23)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 194

## Goal

Core reads the notes workbook and gives, for a material, a finish and the model's Finish Note, the notes to place and where.

## Scope

- `src/InventorAddin.Core/Drawing/NotesWorkbook*.cs`: tabs How to edit, Material (Pattern, Note, Placement), Finish (Pattern, Note, Placement); reader on `XlsxPackage` with messages as the hole workbook has.
- `src/InventorAddin.Core/Drawing/NotesRules.cs`: wildcard match ignoring case, every matching row in order, each note once; placement List or Bottom.
- `libraries/AWB_Notes.xlsx`: starter with **invented** rows (a 3D print material, a hardened steel, an anodise finish); `LibraryPaths.NotesWorkbook` and a Settings row as for the hole workbook.
- Tests.

Out of scope: Inventor (197).

## Acceptance criteria

- [x] A 3D-print material gives a Bottom note "NOTE: 3D PRINTED PART"; a hardened material its heat-treat note in the list; a finish rule its extra note.
- [x] The Finish Note is a numbered List note when it is not blank: after the material notes, before the finish rows' notes (Aidan 2026-11-21; order is Default, question 23). Blank adds nothing; a rule note with the same text is not repeated.
- [x] Starter finish rows are process instructions (masking, handling, surface requirements), invented.
- [x] Bad rows (blank pattern, unknown placement) give a message naming tab and row; the rest still load.
- [ ] The workbook opens in Excel with no repair prompt (Aidan checks).
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow how tasks 097, 106 and 111 wrote the hole workbook package (Python `zipfile`, script kept in the scratchpad). No material names copied from any library (`CLAUDE.md`).
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Defaults

Picked to keep work moving; the question is spec 05's question 15 (placements and rule columns) and question 23 (order).

- A blank Placement means List (a row with an unknown word is the problem, not a blank one).
- A blank Note is a bad row, like a blank Pattern.
- A blank material or finish name matches no rule (even `*`).
- A pattern matches the whole trimmed name; `*` and `?` only, every other character literal.
- Each note is placed once across both placements; duplicates are found ignoring case and leading or trailing spaces only. The first match wins, except that the Finish Note keeps its own place in the list and a rule note equal to it is dropped.
- The headers are in row 4 (title row 1, note row 2), as in the hole workbook; the "How to edit" tab is not read.
- A missing Material or Finish tab is a problem (not an optional tab).
- Blank rows are skipped, not the end of the data (unlike `HoleWorkbookReader`, which stops at the first blank row), so a gap in the sheet does not hide the rows below it.
- The Settings row is "Notes data" in the Material and finish section, after the print templates; its summary names the file when one is chosen.
- The Settings property is `NotesWorkbookPath`; a blank means the starter `AWB_Notes.xlsx`.

## Verification

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5636 passed, 0 failed (23 new tests in `Drawing/NotesWorkbookTests.cs`, plus the settings equality test extended). `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (built on Windows; the csproj copy line and the Settings window row under `src/InventorAddin`). The starter workbook was read back with openpyxl and by the Core reader in a test.
- **Not compiled (changed under `src/InventorAddin`):** none. Built on Windows with `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (csproj copy line, `SettingsWindow.xaml` and `.xaml.cs`).
- **Inventor API members not confirmed:** none; no Inventor code.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open `libraries/AWB_Notes.xlsx` in Excel: no repair prompt, tabs How to edit, Material, Finish, headers in row 4. (Python script that built it: the scratchpad `build_notes.py`; not kept in the repo.)
  2. Settings, Material and finish section: a "Notes data" row with Browse (.xlsx); the summary names the file once one is chosen; a wrong extension or a missing file is refused on Save with "Notes data" in the message; blank uses the starter.
  3. After task 197: Update Notes on a drawing of a part whose material name contains "3D Print" gives a bold "NOTE: 3D PRINTED PART" at the bottom; "Hardened" gives the heat-treat note in the list; an anodise finish gives its three notes after the Finish Note.

## Follow-ups

- Task 197 should load the workbook with `NotesWorkbookReader.Read(LibraryPaths.NotesWorkbook(...))` and show the problems the way AWB Hole does.
