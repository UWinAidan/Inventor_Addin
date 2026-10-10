# 038: Windows user name counts as blank in Part Properties

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Windows user name as Designer or Detailer); `docs/features/07-part-properties.md` (Rules: Designer)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 033, 035, 036, 037, 042

## Goal

Old files carry the Windows login name as Designer and Detailer. After this task, Part Properties treats that name as blank, so it pre-fills Designer from Settings and lets Detailer follow, exactly as for an empty field.

## Scope

- `src/InventorAddin.Core/PartProperties/PersonNames.cs` (new)
- `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs`
- `src/InventorAddin/Commands/PartPropertiesCommand.cs` (passes the Windows user name)
- `tests/InventorAddin.Core.Tests/PartProperties/PersonNamesTests.cs` (new)
- `tests/InventorAddin.Core.Tests/ViewModels/PartPropertiesViewModelTests.cs`

Out of scope:

- Replacing the name in bulk (Mass Update, tasks 041 and 048).
- New Part (writes Settings values directly).

## Acceptance criteria

- [ ] `PersonNames.IsWindowsUserPlaceholder(string? value, string? windowsUserName, string? defaultDesigner)`: true when `defaultDesigner` is not blank, `windowsUserName` is not blank, and `value` equals `windowsUserName` ignoring case and surrounding spaces. False otherwise
- [ ] The view-model constructor gains a `string? windowsUserName` parameter (after `defaultDesigner`). Existing callers in tests pass `null` and behave as before
- [ ] An editable file whose Designer is the Windows user name opens with Designer = `DefaultDesigner`, counted as a pending change (the pre-fill notice shows, as for a blank Designer)
- [ ] An editable file whose Detailer is the Windows user name opens with Detailer following Designer, as for a blank Detailer
- [ ] When `DefaultDesigner` is blank, both fields show the stored value unchanged and nothing is pending (Default: Apply never clears a Designer with nothing to replace it)
- [ ] A file that cannot be edited shows the stored values unchanged
- [ ] A Designer that differs from the Windows user name is kept, whatever `DefaultDesigner` is
- [ ] Apply writes the new Designer and Detailer through the existing write plan; no change to `PartPropertiesWritePlan`
- [ ] `PartPropertiesCommand` passes `System.Environment.UserName` (aliased as the add-in does for the `Inventor.Environment` clash)
- [ ] Tests for each case above, with an invented user name (`testuser`) and designer (`A. Person`)
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Rule decided by Aidan on 2026-10-10; the blank-`DefaultDesigner` exception is a Default in `DECISIONS.md`.
- The view-model already handles a blank Designer and Detailer (see the constructor). Treat the placeholder as blank at that one point rather than adding a second path.
- `AddinServices.cs` shows the `SysEnvironment` alias.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. With Default designer set in Settings, open an old part whose Designer and Detailer are your Windows login name: both show the Settings value and the status line says pre-filled values will be written
  2. Apply, then check Inventor's iProperties dialog: Designer and Detailer hold the Settings value
  3. Clear Default designer in Settings, open another such part: Designer and Detailer show the login name and Apply is disabled

## Follow-ups

Things noticed but not done.
