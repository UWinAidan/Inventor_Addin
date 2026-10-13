# 076: Part Properties: Change material and finish, and the not-from-the-library warning

- **Milestone:** M3
- **Feature spec:** `docs/features/07-part-properties.md`; `docs/features/03-material-and-finish.md` ("Decided")
- **Status:** todo
- **Depends on:** 074, 075
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Part Properties opens the Material and Finish window through a Change button beside Material and Finish, and warns when a part's material is not from the library (Aidan, 2026-10-12), for example in an imported file.

## Scope

- `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs` and its tests
- `src/InventorAddin/UI/PartPropertiesWindow.xaml` and `.xaml.cs`
- `src/InventorAddin/Commands/PartPropertiesCommand.cs`
- `src/InventorAddin/Commands/MaterialFinishCommand.cs` (expose a way to open the window for one document with an owner window)
- `docs/features/07-part-properties.md` (the Change button and the warning)

## Acceptance criteria

- [ ] A Change button beside Material (or spanning the Material and Finish rows) opens the Material and Finish window for the document Part Properties shows, owned by the Part Properties window. On close, Part Properties re-reads and shows the material and finish. Disabled for a read-only file and for an assembly (an assembly has no material; Default)
- [ ] The view-model gets the library's material names, or null when the library could not be read. `MaterialWarning`: for a part whose material is not in the library (069's `IsInLibrary`), "Not from the material library" shown under the Material value in a warning style, never as an error and never blocking Apply. No warning when the library could not be read, for assemblies, or when the material is blank. Tested
- [ ] The library is read when Part Properties opens (074's reader; about 1 s from the NAS); a failure is logged once and the window still opens
- [ ] Spec 07 describes the button and the warning; the old "no placeholder button is added now" line is replaced
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds; `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Spec 08 may not have a warning style; if not, use the muted or note style used for "pre-filled values" and say so in Follow-ups.
- Pending Part Properties edits and the material window: the Material and Finish window applies in its own transaction. If Part Properties has unapplied changes, they stay pending in its view-model; check this does not lose them when the window re-reads.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Part with a starter material: Part Properties shows it with no warning. Change opens Material and Finish over Part Properties; pick a finish, OK: Part Properties shows the new material and finish.
  2. Part with an Inventor stock material (for example set from Inventor's own material list): "Not from the material library" shows under Material; Apply still works.
  3. An imported STEP part: the same warning.
  4. Assembly: Change is disabled; no warning.
  5. Settings pointing at a missing library: Part Properties opens with no warning and the log says why.

## Follow-ups

Things noticed but not done.
