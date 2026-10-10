# 050: Register AssignedBy is the Default designer

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Decided by Aidan: AssignedBy is the person; The register)
- **Status:** done (add-in not compiled)
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

- [x] A Core rule `RegisterEnvironment.AssignedByFor(string? defaultDesigner, string? windowsUserName)`: the trimmed Default designer when not blank, otherwise the trimmed Windows user name. Tested, including both blank (gives `""`)
- [x] `IRegisterEnvironment.UserName` becomes `AssignedBy` (rename the member and its doc comment), and `RegisterEnvironment` takes the value in its constructor (or a function returning it) instead of reading `Environment.UserName` itself. `RegisterEnvironment.Instance` goes if nothing else needs it
- [x] `AddinServices` builds the store's environment from `Settings.DefaultDesigner` and `System.Environment.UserName` (aliased as the add-in does). The store is already rebuilt after a settings save, so a changed Default designer applies to the next assignment; check that this is true and say where in Verification
- [x] The log line that names who assigned a number uses the same value (no such log line exists; the lock file holder text uses it. See Verification)
- [x] Existing register tests pass with the renamed member; the fake environment is updated
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Aidan decided this on 2026-10-10. Undo after Generate leaving the register row is accepted as designed; do not change it.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 1350 passed, 0 failed, 0 skipped (the working tree also holds task 049's changes in progress).
- **Settings save rebuilds the store:** `SettingsCommand.Execute` (`src/InventorAddin/Commands/ShellCommands.cs`) calls `AddinServices.UpdateSettings(saved)` after a successful save, and `UpdateSettings` sets `_registerStore = null`. The `RegisterStore` getter then builds a new `RegisterEnvironment` from the new `Settings.DefaultDesigner` on next use, so the next assignment uses it. Part Properties takes `AddinServices.RegisterStore` when its window opens; the window is modal, so Settings cannot change while it is open.
- **Log line:** no log line in `RegisterStore` names the person (the "assigned" Info lines name the number and file only). The one place besides the register row that names who is assigning is the lock file's holder text (`TakeLock`), and it now uses `AssignedBy`. I did not add the name to the log lines; see Follow-ups.
- **Not compiled (changed under `src/InventorAddin`):** `src/InventorAddin/AddinServices.cs`.
- **Inventor API members not confirmed:** none. The change uses no Inventor API; `SysEnvironment.UserName` is `System.Environment.UserName`, aliased already in the file.
- **Manual checklist for Inventor:**
  1. Open Settings, set Default designer to your name (for example `A. Person`), Save.
  2. Open a new, unsaved part, click Part Properties, and generate a part number.
  3. Open the shared `part-register.csv` (or this PC's `pending.csv` under `%APPDATA%\AwbAddin\register` if the register folder is unreachable). The new row's `AssignedBy` is `A. Person`, not the Windows login.
  4. Open Settings again, clear Default designer, Save. Without restarting Inventor, generate a number for another new part. Its row's `AssignedBy` is the Windows user name.
  5. Set Default designer to `  A. Person  ` (with spaces), Save, generate another number: `AssignedBy` is `A. Person` with no spaces.

## Follow-ups

Things noticed but not done.

- Spec 02's register table still describes `AssignedBy` as "Windows user name" (`docs/features/02-part-creation-and-numbering.md`, "The register"); the decided rule is the Default designer, falling back to the Windows user name. Outside this task's file list.
- `RegisterEntry`'s doc comment for `AssignedBy` (`src/InventorAddin.Core/Numbering/RegisterEntry.cs`) also says "Windows user name". Outside this task's file list.
- The acceptance criterion about "the log line that names who assigned a number" has no matching log line today. If Aidan wants the assigner in `awbaddin.log`, add `AssignedBy` to the two "Part number ... assigned" Info lines in `RegisterStore.Assign`.
