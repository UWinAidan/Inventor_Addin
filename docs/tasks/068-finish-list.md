# 068: Finish list: model, CSV file and validation (Core)

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md` ("The finish list")
- **Status:** done
- **Depends on:** none
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 069, 070

## Goal

Core can load `libraries/finishes.csv` (Aidan's finish list, edited in Excel) into a validated list of finishes, and turn a chosen finish into the four iProperty values stored on the part. Everything later in M3 reads finishes through this.

## Scope

- `src/InventorAddin.Core/Materials/` (new folder): `CsvTable.cs` (a small CSV reader), `Finish.cs`, `FinishKind.cs`, `FinishListCsv.cs`, `FinishProperties.cs`
- `src/InventorAddin.Core/PartProperties/PartPropertyStorage.cs`: add `FinishCode`, `FinishThickness`, `FinishNote`
- `tests/InventorAddin.Core.Tests/Materials/` (new)

Out of scope: the material list (069), settings and file locations (070), the window (072).

## Acceptance criteria

- [x] `CsvTable.Parse(string text)` reads RFC 4180 CSV: quoted fields, `""` inside quotes, commas and line breaks inside quotes, CRLF or LF, a UTF-8 byte order mark, blank lines skipped. It returns a header row and data rows, each with its 1-based line number in the file. Tested, including the quoting in the real `libraries/finishes.csv`
- [x] `Finish` record: `Code`, `Name`, `Kind` (`FinishKind`: Plating, Conversion, Anodize, HeatTreat, Paint; the file writes `Heat treat`), `AppliesTo` (list of trimmed entries), `AppearanceName` (null when the cell is blank or `(no change)`, ignoring case: the part keeps its material's appearance), `ThicknessMinMm` and `ThicknessMaxMm` as written (text, trimmed), `DimensionsAfterFinish` (bool), `PrintNote`
- [x] `FinishListCsv.Parse(string text)` returns the valid finishes and a list of problems, each naming the line and what is wrong. Columns are found by header name, ignoring case and order; extra columns are ignored. A missing required column makes the whole file invalid with one problem naming the column. Row problems (the row is left out, the rest load): blank Code or Name; a Code already used on an earlier row (ignoring case); an unknown Kind (ignoring case and spaces, so `heat treat` and `HeatTreat` both read); a thickness that is not a non-negative number in invariant culture, or min greater than max; DimensionsAfterFinish not Yes or No (ignoring case). Tested for each
- [x] Blank AppliesTo means the finish suits every material (Default, spec 03)
- [x] `FinishProperties.For(Finish? finish)` gives the four `PropertyWrite`s (spec 03, "Decided"): `Finish` = Name, `Finish Code` = Code, `Finish Thickness` = the thickness text, `Finish Note` = PrintNote. No finish (null) deletes all four, the same way Part Properties deletes a cleared custom property. Thickness text (Default, spec 03 question 13): `0.013-0.025 mm`; one value `0.025 mm` when min and max are equal; blank (deleted) when both are zero. Tested
- [x] The four storage locations are custom (user-defined) text properties next to the existing `Finish` in `PartPropertyStorage`
- [x] A test loads the repo's own `libraries/finishes.csv` (found by walking up from the test assembly to the folder holding `InventorAddin.slnx`) and asserts it parses with no problems and 14 finishes
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Match the style of the register CSV in `Numbering/RegisterCsv.cs` where it fits, but this file is written by Excel, not by the add-in, so it is read leniently and never written. Do not reuse `RegisterCsv`'s column rules.
- Excel saves "CSV UTF-8" with a byte order mark and plain "CSV" in the Windows code page. Reading as UTF-8 is enough for now; note in Follow-ups if anything else seems needed.
- Look at how `PartPropertiesWritePlan` builds a delete for a cleared custom property and produce the same kind of `PropertyWrite`.
- Test data other than the repo file must be invented (CLAUDE.md).

## Verification

Filled in by the implementer.

- **Ran (Windows, local):** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings. `dotnet test tests/InventorAddin.Core.Tests`: 2291 passed, 0 failed (101 new test cases under `Materials/`, plus 3 new rows in `PartPropertyStorageTests`). `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors (the add-in's existing CS8600 warnings only).
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` changed.
- **Inventor API members not confirmed:** none; no Inventor API used.
- **Manual checklist for Inventor:** none (Core only)
- **Choices made inside the task's scope:**
  - All nine columns are required. A missing AppliesTo or Appearance column could have read as blank, but a misspelt header would then silently let every finish suit every material. Several missing columns give one problem naming them all.
  - A row with several things wrong gives one problem per thing, all on the row's line. A Code counts as used by an earlier row even when that row was left out for another reason.
  - A blank thickness cell is a problem (the spec says both are 0 when there is no thickness). Thickness accepts digits and a `.` only: no sign, group separator or exponent.
  - Rows whose cells are all blank (Excel's `,,,,`) are skipped silently. Short rows read missing trailing cells as blank. An unclosed quote leaves the row out with a problem.
  - An empty file gives one problem with no line number. A header with no rows gives no finishes and no problems.
  - `FinishKinds.TryParse` (in `FinishKind.cs`) reads a kind. `FinishProperties.ThicknessText` is public so the window can show the same text. Any of the four values that is blank (for example an empty PrintNote) is removed rather than set to `""`, matching Part Properties.

## Follow-ups

Things noticed but not done.

- `CsvTable` repeats the CSV splitter in `Numbering/RegisterCsv.cs` (the task said not to reuse `RegisterCsv`'s column rules; the splitter itself could be shared). A later clean-up could make `RegisterCsv.Read` use `CsvTable.Parse`.
- Encoding: the caller (task 070) reads the file as UTF-8. A plain "CSV" save from Excel uses the Windows code page, so a non-ASCII character in a print note (a degree or micro sign) would read as a replacement character. Consider falling back to the system ANSI code page when the bytes are not valid UTF-8.
