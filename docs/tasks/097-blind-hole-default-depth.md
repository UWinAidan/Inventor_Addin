# 097: Blind holes start at the last depth used, or 2 x D

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Blind holes", "The workbook"; question 7)
- **Status:** done
- **Depends on:** 090
- **Needs:** windows build (Core plus a settings field)
- **Parallel-safe with:** 093, 094, 095, 096

## Goal

Aidan's answer to spec 09 question 7: a blind clearance or dowel hole starts at the last depth used, or at 2 x D the first time, where 2 is a value on the workbook's Settings tab.

## Scope

- Core: `HoleWorkbookReader` and `HoleSettings` (a new Settings value, "Blind depth multiplier", starter 2), `AwbHoleViewModel` (the starting depth), `AddinSettings` (the last depth used), with tests
- `libraries/AWB_Holes.xlsx`: add the setting to the Settings tab, in the tab's own layout. Edit the package's XML directly (unzip, change the sheet and shared strings, zip again) so every other cell, formula and cached value stays as it is; do not save it through a library that drops cached formula values. Check it still reads with no problems.
- `src/InventorAddin/Holes/HoleSession.cs` or the window: record the depth on OK

## Acceptance criteria

- [x] A new blind clearance or dowel hole (Termination Distance) starts at the depth of the last blind clearance or dowel hole made with OK in those units (Metric and Imperial each remember their own), else at multiplier x the size's nominal diameter, rounded as the thread depth is.
- [x] The last depth is kept in the add-in's settings file, so it survives a restart (Default). A depth that was an expression (task 100) is kept as its value.
- [x] The Settings value is read like the thread multipliers: a missing or unreadable value uses the starter value; a workbook without the row (Aidan's own copy) reads with no problem reported, since the setting is new.
- [x] Switching to Distance, changing the size or units, on a hole with no depth typed in this window: the depth follows these rules; a depth the user typed in this window is kept.
- [x] OK is no longer disabled just because a blind hole has no depth; an emptied depth box still disables it.
- [x] An edited hole keeps its own depth.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows), `dotnet build src/InventorAddin.Core` (0 warnings, 0 errors); `dotnet test tests/InventorAddin.Core.Tests`: 3274 passed, 0 failed; `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: succeeded, 0 errors (8 warnings, all CS8600 in files this task did not touch: CreatePrintCommand, SaveAsReplaceCommand, SaveAsCopyCommand, NewPartCommand). Not deployed and not run in Inventor.
- **Workbook:** `libraries/AWB_Holes.xlsx` edited in its package XML with Python `zipfile`: only `xl/worksheets/sheet2.xml` (Settings) and `xl/sharedStrings.xml` were rewritten; every other entry's content is identical to HEAD (checked entry by entry). Settings gets row 7 "Blind depth multiplier" = 2 with a meaning in C, in the style of rows 4 to 6; the colour rows move from 7-9 to 8-10 (nothing referenced them; the Tapped formulas use `Settings!$B$5` and `$B$6`, which did not move). The "How to edit" tab's Settings line now mentions the blind hole depth. Cached formula values are untouched; `StarterWorkbook_KnownRows` still reads the M6x1 depths 9 and 12 from them, and `StarterWorkbook_HasTheBlindDepthMultiplierRow_AfterTheDrillExtraDepth` reads the new row with the reader's own `XlsxPackage`.
- **Not compiled (changed under `src/InventorAddin`):** none; `Commands/AwbHoleCommand.cs` and `Holes/HoleSession.cs` were built on Windows above, but not run in Inventor.
- **Inventor API members not confirmed:** none; the add-in changes use no new Inventor API members (only `AddinServices`, `SettingsStore` and `HoleLastDepths`).
- **Behaviour choices inside the brief:** OK on an edited blind clearance or dowel hole also records its depth (the brief says "made with OK"; editing is also OK). Emptying the depth box leaves it empty and OK disabled until the next change the rule follows (becoming blind, a size or units change); a seat or fastener change alone does not refill it. An edited hole's own depth counts as typed, so it is kept; an edited through hole made blind takes the rule. A typed depth is converted on a units switch, as before.
- **Manual checklist for Inventor:**
  1. With a fresh settings file (no `lastBlindDepthMm` / `lastBlindDepthIn` in `%APPDATA%\AwbAddin\settings.json`): a new clearance M6, Distance: the depth is 12 mm (2 x 6). OK is enabled with a centre picked. OK.
  2. A new hole, Distance: 12 mm again. Type 20, OK. The next new blind hole starts at 20 mm, also after an Inventor restart (`settings.json` has `"lastBlindDepthMm": 20`). Imperial starts from its own last depth (the first time, 2 x the size's nominal in inches).
  3. In a new blind hole, change the size: an untyped depth stays at the last depth (or follows 2 x D when there is none); a depth you typed is kept. Empty the depth box: OK is disabled and the box says to enter a depth.
  4. Edit a blind hole made earlier with another depth: it opens on its own depth, not the last one.
  5. Change the Settings tab's value to 3 in your workbook, clear the settings file's last depth (or use a fresh one): M6 starts at 18 mm. Your own workbook without the row opens with no workbook problem and uses 2.

## Follow-ups

- The Settings window edits a copy of the settings taken when it opens; if a blind hole is made with OK while it is open and the window is then saved, the last depth goes back to what it was. Small; it could merge the hole depths into the saved settings.
- Task 100 (depth expressions): `HoleLastDepths.Remember` takes `HoleSpec.Depth`; when a depth can be an expression, pass the expression's value so the setting stays a number (acceptance criterion 2).
- The setting is named "Blind depth multiplier" as the spec and brief say; the neighbouring rows are named "Thread depth (x D)" and "Drill extra depth (x D)". Aidan may want "Blind depth (x D)" for consistency (the reader would then need to accept both names).
