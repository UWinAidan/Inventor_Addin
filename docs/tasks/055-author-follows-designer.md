# 055: Author is written with the same name as Designer

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11; "Written on creation"; "Windows user name as Designer or Detailer"; Mass Update)
- **Status:** done (compiled locally, not tested in Inventor)
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

- [x] `PartPropertyStorage.Author` is the standard `Author` property in `PropertySetNames.Summary` ("Inventor Summary Information"), text. Spike 052 confirmed this set and name write and read back
- [x] `CreationProperties.Build` writes Author equal to Designer whenever it writes Designer (a non-blank Default designer), and not at all when the Default designer is blank. Tested
- [x] Part Properties: the snapshot reads Author. On Apply, when the Designer the window applies (edited or unchanged) is not blank and Author differs from it (ignoring surrounding spaces, exact otherwise), the write plan sets Author to it. When Designer is blank, Author is left as it is (Default: the add-in never clears an Author). Tested, including an unchanged Designer with Author equal to the Windows user name, which now produces a write
- [x] The "nothing to apply" logic still works: an Apply where only Author needs fixing counts as a change
- [x] Mass Update: a scanned file records its Author; Author equal to the Windows user name or an Other name counts like Designer does (shown and counted in the "your name" text and the replace action). Replace sets Author to the Default designer from Settings, as it sets Designer. Tested in Core (scan state, plan, runner)
- [x] The add-in reads Author where it reads Designer (`PartPropertiesReader`, Mass Update's file reading) through the existing `PropertyReader` helpers
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow how Detailer follows Designer, but Author has no field.
- Mass Update's row text may need one more word; keep it short and within its column, and keep "your name" wording (Default, task 048).
- `PropertyWriter` already writes a standard property by set and name; Author needs no new write path.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows, Inventor 2026 installed):
  - `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: succeeded, 0 errors, the same 4 existing CS8600 warnings (CreatePrintCommand, NewPartCommand, SaveAsCopyCommand, SaveAsReplaceCommand; each listed twice, once for the WPF temp project).
  - `dotnet test tests/InventorAddin.Core.Tests`: 1983 passed, 0 failed (1950 before; 33 new or newly parameterised cases, including the review fix: a Designer that means the user is never copied into Author). One earlier run had a few folder-probe timeouts in temp-folder tests (RegisterStore, RenamePlan, NewPartCreator, SaveAsCopyCreator, SaveAsReplacer); they did not recur on the next run and do not touch Author.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
  - `Extraction/PartPropertiesReader.cs`: reads Author through `PropertyReader.GetString`, as Designer.
  - `Extraction/FileScanner.cs`: Mass Update's file reading passes Author to `ScannedFile` (this is where Mass Update reads a scanned file's properties, not `MassUpdateCommand.cs`).
  - `Commands/MassUpdateCommand.cs`: the button description now says "Designer, Author and Detailer".
  - `UI/MassUpdateWindow.xaml`: header comment only.
- **Inventor API members not confirmed:** none new. Author is read and written through the existing `PropertyReader` and `PropertyWriter` paths, by set name `Inventor Summary Information` and property `Author`, which spike 052 confirmed.
- **Manual checklist for Inventor:**
  1. Settings: set Default designer to a real name (for example `Test Designer`). Leave your Windows user name as it is.
  2. New Part: create a part. Open iProperties > Summary: Author is `Test Designer`, the same as Designer on the Project tab.
  3. Save As Copy and Save As and Replace on that part: in each copy, Author equals Designer.
  4. Open an old part whose Designer is a real name and whose Author is your Windows user name (Inventor sets it on every new file). Open Part Properties: Apply is enabled with no field changed. Press Apply, then check iProperties > Summary: Author now equals Designer. Reopen Part Properties: Apply is disabled.
  5. In Part Properties, change Designer and Apply: Author changes to the same name.
  6. With Default designer blank, open a file with a blank Designer: Apply stays disabled and Author is left as it was.
  7. With Default designer blank, open a file whose Designer is your Windows user name (or one of the Other names) and whose Author is a real name. Part Properties shows Designer unchanged, Apply stays disabled, and after OK the Author in iProperties > Summary is still the real name.
  8. Mass Update: scan a folder holding a file whose only "your name" is in Author. Its row reads "...; your name as Author", it is counted in "with your name", and Replace names is offered. Tick it and Apply, then open the file: Author is the Default designer, and Designer and Detailer are unchanged. A file with all three as your name reads "your name as Designer, Author and Detailer". Check the row text fits in the window.
  9. Undo after Part Properties Apply reverts Author along with the other writes (one transaction).

## Follow-ups

Things noticed but not done.

- **Pre-fill notice for a hidden Author fix.** When only Author needs fixing, Part Properties opens with Apply enabled and shows "Pre-filled values will be written when you apply." though no visible field changed. Correct, but it may puzzle. A wording such as "Author will be set to the Designer when you apply." is a possible tweak (question for Aidan).
- **Spec 02 "Lists every file ... whether Designer or Detailer equals the Windows user name"** (Mass Update section) could add Author, matching the next line.
