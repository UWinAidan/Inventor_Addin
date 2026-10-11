# 055: Author is written with the same name as Designer

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11; "Written on creation"; "Windows user name as Designer or Detailer"; Mass Update)
- **Status:** todo
- **Depends on:** 054
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Aidan's item 11 (2026-10-11). Inventor fills Author with the Windows user name on every new file, so old files carry it there too. The add-in writes Author with the same name as Designer, and treats Author as it treats Designer in Part Properties, Mass Update and the Other names rule.

## Scope

- `src/InventorAddin.Core/PartProperties/PartPropertyStorage.cs` (an `Author` location: set `PropertySetNames.Summary`, property `Author`, text)
- `src/InventorAddin.Core/Numbering/CreationProperties.cs` (New Part, Save As Copy, Save As and Replace)
- `src/InventorAddin.Core/PartProperties/PartPropertiesSnapshot.cs`, `PartPropertiesWritePlan.cs`, and `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs` if needed
- `src/InventorAddin/Extraction/PartPropertiesReader.cs` (read Author)
- Mass Update: `src/InventorAddin.Core/Numbering/MassUpdate/ScannedFile.cs`, `MassUpdatePlan.cs`, `MassUpdateRunner.cs`, `src/InventorAddin.Core/ViewModels/MassUpdateViewModel.cs`, `src/InventorAddin/Commands/MassUpdateCommand.cs` (where it reads a scanned file's properties)
- The matching tests under `tests/InventorAddin.Core.Tests/`

Out of scope:

- An Author field in the Part Properties window. Author is not shown (Default, spec 02).

## Acceptance criteria

- [ ] `PartPropertyStorage.Author` is the standard `Author` property in `PropertySetNames.Summary` ("Inventor Summary Information"), text. Spike 052 confirmed this set and name write and read back
- [ ] `CreationProperties.Build` writes Author equal to Designer whenever it writes Designer (a non-blank Default designer), and not at all when the Default designer is blank. Tested
- [ ] Part Properties: the snapshot reads Author. On Apply, when the Designer the window applies (edited or unchanged) is not blank and Author differs from it (ignoring surrounding spaces, exact otherwise), the write plan sets Author to it. When Designer is blank, Author is left as it is (Default: the add-in never clears an Author). Tested, including an unchanged Designer with Author equal to the Windows user name, which now produces a write
- [ ] The "nothing to apply" logic still works: an Apply where only Author needs fixing counts as a change
- [ ] Mass Update: a scanned file records its Author; Author equal to the Windows user name or an Other name counts like Designer does (shown and counted in the "your name" text and the replace action). Replace sets Author to the Default designer from Settings, as it sets Designer. Tested in Core (scan state, plan, runner)
- [ ] The add-in reads Author where it reads Designer (`PartPropertiesReader`, Mass Update's file reading) through the existing `PropertyReader` helpers
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow how Detailer follows Designer, but Author has no field.
- Mass Update's row text may need one more word; keep it short and within its column, and keep "your name" wording (Default, task 048).
- `PropertyWriter` already writes a standard property by set and name; Author needs no new write path.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
