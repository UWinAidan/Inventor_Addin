# 050: Register AssignedBy is the Default designer

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Decided by Aidan: AssignedBy is the person; The register)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 049

## Goal

A register row's `AssignedBy` names the person (the Default designer from Settings), not the Windows login, falling back to the Windows user name when the Default designer is blank. From Aidan's run 1 checks.

## Scope

- `src/InventorAddin.Core/Numbering/IRegisterEnvironment.cs` (and `RegisterEnvironment`)
- `src/InventorAddin.Core/Numbering/RegisterStore.cs` (only where it reads the user)
- `src/InventorAddin/AddinServices.cs` (builds the store with the name)
- `tests/InventorAddin.Core.Tests/Numbering/RegisterStoreTests.cs`, and a test for the new rule

Out of scope:

- Changing existing register rows. Other names that mean me (051).

## Acceptance criteria

- [ ] A Core rule `RegisterEnvironment.AssignedByFor(string? defaultDesigner, string? windowsUserName)`: the trimmed Default designer when not blank, otherwise the trimmed Windows user name. Tested, including both blank (gives `""`)
- [ ] `IRegisterEnvironment.UserName` becomes `AssignedBy` (rename the member and its doc comment), and `RegisterEnvironment` takes the value in its constructor (or a function returning it) instead of reading `Environment.UserName` itself. `RegisterEnvironment.Instance` goes if nothing else needs it
- [ ] `AddinServices` builds the store's environment from `Settings.DefaultDesigner` and `System.Environment.UserName` (aliased as the add-in does). The store is already rebuilt after a settings save, so a changed Default designer applies to the next assignment; check that this is true and say where in Verification
- [ ] The log line that names who assigned a number uses the same value
- [ ] Existing register tests pass with the renamed member; the fake environment is updated
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Aidan decided this on 2026-10-10. Undo after Generate leaving the register row is accepted as designed; do not change it.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
