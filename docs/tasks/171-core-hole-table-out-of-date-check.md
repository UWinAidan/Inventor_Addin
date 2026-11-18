# 171: Core: on open, check each stored view's tables against the model and offer Update for the out-of-date ones

- **Milestone:** M4 part 2 (request from Aidan's check, 2026-11-18)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update)
- **Status:** todo
- **Depends on:** 162, 168
- **Needs:** cloud (Core only)
- **Parallel-safe with:** 169, 170, 173

## Goal

Aidan (2026-11-18): Update stays manual, but when the wizard opens it checks each stored view's tables against the model and, if any are out of date (holes moved, added, removed, or no longer seen end on), says so and offers Update for those views. After this task the view-model can run that check and the Update, and say what it found; task 172 shows it in the window.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableState.cs`: the stored state also keeps what was drawn for each hole: its description key and its XDIM and YDIM as shown in the table (the formatted text, or the value rounded to the table's precision; implementer's choice, say which). Bump `CurrentVersion` to 2. A version 1 state still reads; it has no positions, so for it only added, removed and description changes are detected (moves are not; say so in the result).
- New `src/InventorAddin.Core/HoleTable/HoleTableStaleness.cs` (name is the implementer's): compares a stored state with a fresh analysis of the view (through the same refresh `HoleTablePlan.Refresh` that Update uses, so the two always agree) and returns whether it is out of date and why: holes moved, added, removed (with "not seen end on" when the analysis left holes out), descriptions changed. When the analysis failed or the origin stops the wizard, the result is "could not be checked" with the reason, never "up to date".
- `src/InventorAddin.Core/HoleTable/HoleTableMessages.cs`: the texts, for example "VIEW1 on Sheet:1: 2 holes moved, 1 hole added." and a heading such as "These views' hole tables are out of date:".
- `src/InventorAddin.Core/ViewModels/HoleTableWizardViewModel.cs`:
  - `CheckStoredTables()`: for every sheet (not only the selected one) and every view with a stored state (`_source.ReadState`), analyse it and check it. Not run by the constructor: the window calls it after it opens (task 172), so the window can show it is busy.
  - `OutOfDateViews` (an observable list of sheet, view and reason text), `CouldNotCheckViews` or one list with a flag (implementer's choice), and a property for the window's message.
  - `UpdateOutOfDateCommand`: runs Update for each out-of-date view, the same Update as the button (refactor `Update()` to take a sheet and view key rather than reading the selection), and reports per view in the status line. A view whose Update fails stays listed with the reason. Views that are up to date drop out of the list.
  - A **Dismiss** (or close the message) command, so the user can go on without updating.
- Tests in `tests/InventorAddin.Core.Tests` with the fake source: moved, added, removed, left out (not end on), description changed, up to date, version 1 state, analysis failed, origin missing, Update of the out-of-date views, an Update that throws.

Out of scope: the window (task 172); any change under `src/InventorAddin`.

## Acceptance criteria

- [ ] The check and Update use the same refresh, so a view the check calls up to date is one Update would not change, and the other way round (test it).
- [ ] A version 1 state is checked without moves and does not crash.
- [ ] Nothing is written to the drawing by the check: only `ReadState` and `Analyze` are called (test with the fake).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Read `HoleTablePlan.Refresh.cs`, `HoleTableState.cs` (JSON in `HoleTableStateJson`), and the view-model's `Update`, `OpenStoredView` and `RefreshStoredViews`.
- "Moved" means the XDIM or YDIM the table would show changed, so a move smaller than the table's precision is not reported.
- The state is limited to about 32 KB (task 166 logs its size). Keep the per-hole addition short (ids are already stored; add only what is needed).
- Log any default you pick in the task file.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** see task 172.

## Follow-ups

Things noticed but not done.
