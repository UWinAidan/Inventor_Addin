# 207: Parts list Make/Buy rule (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 6; open question 20)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 205

## Goal

Core decides Make, Buy or blank for a parts list row.

## Scope

- `src/InventorAddin.Core/Drawing/MakeBuy.cs`: input the row's part number, whether it is Content Center, and whether its file is in a library folder; output Make (AWB number, by the numbering parser), Buy, or blank.
- Tests.

## Acceptance criteria

- [ ] AWB-10001: Make. A Content Center bolt: Buy. A file in a library folder: Buy. Anything else: blank.
- [ ] An AWB-numbered file in a library folder: Buy (library wins; say so in a test name).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Use `Numbering.PartNumber` parsing; do not write a second parser.
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Defaults

- Library wins: a Content Center part or a file in a library folder is Buy even when AWB-numbered.
- "AWB-numbered" means whatever `PartNumber.TryParse` accepts.
- Cell text is upper case, "MAKE" and "BUY", to match the all-caps MAKE/BUY column heading. Blank is an empty string. Both are constants in `MakeBuy`.

## API for task 208

- Folder rule is shared with Batch Print in `LibraryFolderRule` (`IsUnder(file, folders)`, `ResolveProjectFolder`), moved out of `BatchPrintPlan`. Get the folders from `BatchPrintTreeReader.LibraryFolders(app)` (it resolves relative .ipj paths), not from `ActiveDesignProject.LibraryPaths` directly; read once per command.
- `MakeBuy.CellText(partNumber, isContentCenter, LibraryFolderRule.IsUnder(fullFileName, folders))` gives the cell `Value`; `MakeBuy.Decide(...)` gives `MakeBuyKind`.
- Suggest 208 treats a row with no referenced file (virtual component) as blank: skip it. `IsUnder(null, ...)` is false.

## Verification

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5841 passed, 0 failed (one earlier run hit a known flaky SaveAsReplacer test, rerun clean). `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors.
- **Changed under `src/InventorAddin`:** `Commands/BatchPrintCommand.cs` and `Drawing/BatchPrintTreeReader.cs`, call-site renames only (`BatchPrintPlan.IsUnderAnyFolder` / `ResolveProjectFolder` to `LibraryFolderRule`). Built on Windows, 0 errors; Batch Print not re-run in Inventor.
- **Inventor API members not confirmed:** none new.
- **Manual checklist for Inventor:** only a quick regression: Batch Print an assembly with a library part still skips it (task 209 covers Batch Print).

## Follow-ups

- Task 208: skip rows with no referenced file.
