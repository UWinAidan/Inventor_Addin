# 051: Other names that mean me, in Part Properties and Mass Update

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Decided by Aidan: Other names that mean me; Windows user name as Designer or Detailer)
- **Status:** todo
- **Depends on:** 049
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** none

## Goal

A Designer or Detailer equal to the Windows user name **or to any name in Settings' Other names** is treated as the Windows user name is today: Part Properties pre-fills over it, and Mass Update offers to replace it. One shared Core rule does the matching. From Aidan's run 1 checks.

## Scope

- `src/InventorAddin.Core/PartProperties/PersonNames.cs`
- `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs`
- `src/InventorAddin.Core/Numbering/MassUpdate/ScannedFile.cs`, `MassUpdatePlan.cs` (renames only, if needed), `src/InventorAddin.Core/ViewModels/MassUpdateViewModel.cs`
- `src/InventorAddin/Commands/PartPropertiesCommand.cs`
- The matching tests under `tests/InventorAddin.Core.Tests/`

Out of scope:

- The Mass Update command (048), which passes the names when it builds the view-model.

## Acceptance criteria

- [ ] `PersonNames.NamesThatMeanMe(string? windowsUserName, string? otherNames)`: the Windows user name plus each comma-separated entry of `otherNames`, each trimmed, blanks dropped, duplicates (ignoring case) removed. Tested with `"Aidan.Brown, 4aida"`-style invented values, empty input, extra commas and spaces
- [ ] `PersonNames.MeansMe(string? value, IReadOnlyCollection<string> names)`: true when the trimmed value equals any name ignoring case. Tested
- [ ] `IsWindowsUserPlaceholder` takes the names list instead of one user name (rename it, for example `IsMePlaceholder`); it is still false when the Default designer is blank
- [ ] `PartPropertiesViewModel` takes the names list instead of `windowsUserName`; its existing tests keep passing with a list of one name, and new tests cover a name from Other names for Designer and for Detailer
- [ ] Mass Update: `ScannedFileState.For` takes the names list and uses `PersonNames.MeansMe`, deleting its private copy of the comparison (task 041 follow-up). The view-model takes the names list. Rename `DesignerIsWindowsUser`/`DetailerIsWindowsUser` to `...IsMe` only if it stays small; otherwise leave the names and fix their doc comments
- [ ] `PartPropertiesCommand` passes `PersonNames.NamesThatMeanMe(Environment.UserName, Settings.OtherNamesForMe)`
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Aidan decided this on 2026-10-10. The example names in his request are his own; use invented names in tests (for example `testuser`, `T.User`).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
