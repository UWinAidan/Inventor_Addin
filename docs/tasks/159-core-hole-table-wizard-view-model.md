# 159: Core: the Hole Table Wizard view-model (Create Tables tab and row edits)

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target dialog", "Decided behaviour")
- **Status:** done
- **Depends on:** 158
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 160

## Goal

The wizard's window logic exists in Core as a plain `INotifyPropertyChanged` view-model, tested without WPF or Inventor, so the add-in task after spike 160 only has to bind a window and supply the drawing.

## Scope

- `src/InventorAddin.Core/ViewModels/HoleTableWizardViewModel.cs`, following `ObservableObject` and `RelayCommand` as the other view-models do.
- It talks to the drawing through an interface (`IHoleTableSource` or similar) that the add-in implements later: list the sheets, list a sheet's views, and analyse a view (returning the holes, the ordinate origins, the view scale, the drawing's units and how many holes were left out, or a failure message). A fake implements it in the tests.
- **Header:** the drawing name; Sheet list (the active sheet selected); View list for the sheet.
- **Analyze View:** calls the source, runs task 157's origin rule (a stop shows its message and leaves the lists empty), then `HoleTablePlan.Build` (task 158). The status line names how many holes were found and how many were left out (question 16).
- **Lists:** Precision Holes and Clearance Holes, one row per hole instance as the reference shows, with Tag, Description and the Split checkbox. Rows show the plan's order.
- **Swap**, **Delete** (the buttons between the lists) act on the selected row; **Split** is the row's checkbox; every edit rebuilds the plan (task 158), keeping the selection on the same hole where it still exists.
- **Renumber:** rebuilds the plan.
- **Notes:** a group's NOTES text can be edited here (Aidan, question 5, Edit Table tab); a property per group row is enough. Where it shows in the window is the add-in task's.
- **Enabled states:** Analyze needs a view; Swap and Delete need a selected row; Generate Precision Table and Generate Clearance Table need a non-empty list of their kind; Create Tables needs at least one. The Generate, Create, Align and Update commands are exposed as commands that call the source's (future) table methods through the interface, with the method bodies left to the add-in; for now the interface methods take the plan and return a result message.
- **Cancel** closes without changes.

Out of scope: the Edit Table tab's re-opening of an existing table, and Update (both task 162); the WPF window; anything under `src/InventorAddin`.

## Acceptance criteria

- [x] Sheet and view selection, Analyze with a fake source, the stop messages for no origin and several origins.
- [x] Swap, Delete, Split and Renumber change the lists and tags as task 158 says; the selection follows the hole.
- [x] Each command's enabled state in the cases above, with `CanExecuteChanged` raised when it changes.
- [x] Notes edited on a group survive a Swap or Split elsewhere.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Look at `AwbHoleViewModel` and `MassUpdateViewModel` for how existing view-models expose lists, commands and status text.
- No Inventor or WPF types, no `System.Windows`; the existing XAML rules test does not apply until the window exists.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests` on Windows: 4676 passed, 0 failed (19 new in `HoleTableWizardViewModelTests`).
- **Not compiled (changed under `src/InventorAddin`):** none; nothing there was touched.
- **Inventor API members not confirmed:** none (Core only).
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.

- Update (task 162) is not exposed on the view-model, as agreed; the interface has Generate, Create and Align only. The brief's "Update" command waits for 162.
- The wizard rows carry `Notes` per row (group note), `IsSplit` and `Tag`; the add-in window decides where Notes shows.
- `IHoleTableSource` (in `HoleTable/IHoleTableSource.cs`) is a first shape: spike 160's add-in implementation may want to adjust it (for example views identified by more than a name).
- Review fixes applied: sheets and views are identified by add-in-chosen keys (`HoleTableSheetInfo(Key, Name, IsActive)`, `HoleTableViewInfo(Key, Name)`, `GetViews`); `GenerateTable`, `CreateTables` and `AlignTables` take the sheet and view keys; Align needs a view; the VM catches source exceptions and shows "Could not ...: message"; `CloseRequested` renamed `CancelRequested`.
- `HoleTablePlan.StaleIds` is not reported in the wizard yet (task 162).
