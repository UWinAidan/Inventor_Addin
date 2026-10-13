# 076: Part Properties: Change material and finish, and the not-from-the-library warning

- **Milestone:** M3
- **Feature spec:** `docs/features/07-part-properties.md`; `docs/features/03-material-and-finish.md` ("Decided")
- **Status:** done
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

- [x] A Change button beside Material (or spanning the Material and Finish rows) opens the Material and Finish window for the document Part Properties shows, owned by the Part Properties window. On close, Part Properties re-reads and shows the material and finish. Disabled for a read-only file and for an assembly (an assembly has no material; Default)
- [x] The view-model gets the library's material names, or null when the library could not be read. `MaterialWarning`: for a part whose material is not in the library (069's `IsInLibrary`), "Not from the material library" shown under the Material value in a warning style, never as an error and never blocking Apply. No warning when the library could not be read, for assemblies, or when the material is blank. Tested
- [x] The library is read when Part Properties opens (074's reader; about 1 s from the NAS); a failure is logged once and the window still opens
- [x] Spec 07 describes the button and the warning; the old "no placeholder button is added now" line is replaced
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds; `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Spec 08 may not have a warning style; if not, use the muted or note style used for "pre-filled values" and say so in Follow-ups.
- Pending Part Properties edits and the material window: the Material and Finish window applies in its own transaction. If Part Properties has unapplied changes, they stay pending in its view-model; check this does not lose them when the window re-reads.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's Windows PC, `dotnet build InventorAddin.slnx -tl:off -nodeReuse:false -p:DeployToInventor=false`: succeeded, 0 errors; the 8 warnings are the existing CS8600 ones in CreatePrint, NewPart, SaveAsCopy and SaveAsReplace, none in files this task changed. `dotnet test tests/InventorAddin.Core.Tests`: 2598 passed, 0 failed (the XAML rules test included); the Part Properties view-model tests alone: 175 passed, 28 of them new test cases for this task.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
  - `Commands/MaterialFinishCommand.cs`: the open-library, finish-list and show steps moved out of `Run` into a new `MaterialFinishWindowLauncher` (same file): `OpenLibrary(out error)` (no message box; logs once) and `Show(target, runInTransaction, openedLibrary, owner)`. The command now calls `Show` and behaves as before.
  - `Commands/PartPropertiesCommand.cs`: reads the library for a part when the window opens (wait cursor), passes its materials (or null) to the view-model, and a `MaterialFinishChanger` that shows the window over Part Properties and re-reads material, finish and weight through `PartPropertiesReader.Read`.
  - `UI/PartPropertiesWindow.xaml`: Change button (access key H) beside Material, the warning under the Material value, Material, Finish and Weight bindings now OneWay. The `.xaml.cs` needed no change.
  - `UI/WindowHost.cs` (not in the task's listed files; needed for the owner): `ShowDialog` gains an optional `owner` window. With an owner, `Window.Owner` is set to it and the window centres on it; without one, nothing changes.
- **Inventor API members not confirmed:** none new. The task uses only members already used and compiled: `InventorHost.ActiveDocument`, `AssetLibraries.Open` through 074's reader, and `PartPropertiesReader.Read`. Not proven in Inventor: a WPF `Window.Owner` set to Part Properties (itself owned by Inventor's HWND) opening over it as a modal child.
- **Manual checklist for Inventor:**
  1. Part with a starter material: Part Properties shows it with no warning. Change opens Material and Finish over Part Properties; pick a finish, OK: Part Properties shows the new material and finish.
  2. Part with an Inventor stock material (for example set from Inventor's own material list): "Not from the material library" shows under Material; Apply still works.
  3. An imported STEP part: the same warning.
  4. Assembly: Change is disabled; no warning.
  5. Settings pointing at a missing library: Part Properties opens with no warning and the log says why.
  6. Read-only part (set the file read-only in Explorer, then open it): Change is disabled; the warning still shows if the material is not from the library.
  7. Part with a stock material: Change, pick a library material, OK: the warning goes away and Weight changes to match the new material.
  8. Pending edits: type a new Designer (Apply enabled), Change, pick a material, OK: Designer still shows what was typed and Apply is still enabled; Apply writes it. Then Undo twice in Inventor: the first Undo reverts the Designer, the second the material and finish.
  9. Change, then Cancel in Material and Finish: Part Properties shows the same material and finish as before.
  10. Change, Apply in Material and Finish, then Cancel: Part Properties shows the applied material and finish.
  11. Edit a part in place inside an assembly, open Part Properties and press Change: the window shows that part; OK; one Undo from the assembly reverts the material.
  12. Material and Finish opens noticeably faster from Change than from the ribbon (the library read when Part Properties opened is reused).
  13. Dark and light theme: the warning reads in muted text; the Change button looks like Rename and Generate.
  14. The Material and Finish command from the ribbon still works as before (task 075's checklist), since its steps moved into the launcher.

## Follow-ups

Things noticed but not done.

- **Undo name.** Applies made through Change run in a transaction named after the command that started it, so Inventor's Undo list shows "Part Properties", not "Material and Finish". `DocumentEditCommand.RunInTransaction` takes no name; giving it an optional transaction name would fix it (outside this task's files).
- **Warning style.** Spec 08 has no warning colour or style; the warning uses `MutedText` (the help-text style) under the value. A `Warning` token and style in spec 08 and `Styles.xaml` would set it apart from help text if Aidan wants that.
- **Spec 08, Part Properties.** Spec 08's "The three windows: Part Properties" still lists Material as "text, or Not set"; it could gain the Change button (access key H) and the warning line.
- **Library read only for parts.** Part Properties reads the library only for a part (sheet metal included); an assembly skips the read, since it shows no warning and Change is disabled. Default; confirm with Aidan if the acceptance line ("the library is read when Part Properties opens") was meant for assemblies too.
- **Re-read after Change** re-reads the whole snapshot (`PartPropertiesReader.Read`) and keeps only material, finish and weight. If the Material and Finish window ever writes properties Part Properties edits, this would need to re-base those too.
