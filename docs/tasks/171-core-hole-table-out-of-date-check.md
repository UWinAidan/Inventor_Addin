# 171: Core: on open, check each stored view's tables against the model and offer Update for the out-of-date ones

- **Milestone:** M4 part 2 (request from Aidan's check, 2026-11-18)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update)
- **Status:** done (Core; not committed)
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
- [ ] No table or state is written by the check: only `GetKeyedViews` (default `GetViews`), `ReadState` and `Analyze` are called (test with the fake). In the add-in `GetViews` writes view keys for unkeyed views (its own transaction), so task 172 overrides `GetKeyedViews` to list without assigning keys.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Read `HoleTablePlan.Refresh.cs`, `HoleTableState.cs` (JSON in `HoleTableStateJson`), and the view-model's `Update`, `OpenStoredView` and `RefreshStoredViews`.
- "Moved" means the XDIM or YDIM the table would show changed, so a move smaller than the table's precision is not reported.
- The state is limited to about 32 KB (task 166 logs its size). Keep the per-hole addition short (ids are already stored; add only what is needed).
- Log any default you pick in the task file.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core` (0 warnings, 0 errors); `dotnet test tests/InventorAddin.Core.Tests` (4861 passed, 0 failed; 27 new tests in `HoleTableStalenessTests` and `HoleTableWizardViewModelTests`); `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows (0 errors), so the add-in still compiles against the changed Core signatures.
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` was edited. It built on Windows as above.
- **Inventor API members not confirmed:** none; Core only.
- **Manual checklist for Inventor:** see task 172.

## Defaults chosen (log in DECISIONS.md)

- **What the state stores per hole:** the XDIM and YDIM as the table showed them (the formatted text), so a move smaller than the table's precision is never reported. The description key was already stored per group (`k`), so it is not repeated per hole. Positions are stored inside each group as `c`, keyed by tag number (`"3": "12.50;4.00"`), not by hole id, to stay inside the 32 KB budget (the 200-hole size test still passes). `HoleTableState.Drawn` is null for a version 1 state.
- **Version:** `CurrentVersion` is 2; versions 1 and 2 read, anything else returns the "reads versions 1 to 2" reason. A version 1 state is checked without moves and the reason text ends "Moves are not checked for tables stored by an older version."
- **Check = Update's refresh:** `HoleTableStaleness.Check` calls `HoleTablePlan.Refresh`. Out of date means any of: moved (text differs), added, removed (stored tag, hole gone), description changed, or a still-present hole that now gets a different tag (counted under "description changed"). A removed hole with `LeftOutCount > 0` reads "removed (not seen end on)".
- **Not counted:** stale edit ids (an edit for a hole that no longer exists and never had a tag). Update would drop them silently; the check does not call that out of date.
- **Could not check:** a failed analysis, an analysis or ReadState that throws, or any origin stop (none, unreadable, several) goes to `CouldNotCheckViews`, a separate list from `OutOfDateViews`.
- **Update of out-of-date views:** a view open in the window (selected, with tags) uses the button's own Update (window edits kept); any other view uses the edits and tags stored on it, through the same Refresh and `UpdateTables`, without touching the window's lists. Failed views stay listed with "Update failed. <reason>". Status line is one sentence per view.
- **`GetKeyedViews`:** added to `IHoleTableSource` as a default interface member that falls back to `GetViews`; the check calls it, so task 172 only has to override it in the add-in. Until then the check goes through `GetViews`, which can assign view keys (a write).
- **"Descriptions changed" count:** whole groups that kept their letter with a new description, plus single holes that now get a different tag (retagged). A retagged hole that also moved counts in both Moved and DescriptionChanged.
- **Left-out text:** "1 hole removed." and the "holes were left out" sentence are stated separately; the left-out count is for the whole view and is not claimed as the cause of any removal.
- **Dismiss** clears both lists and the message.

## Follow-ups

- Task 172: give the add-in a listing for the check that does not assign keys, by overriding `IHoleTableSource.GetKeyedViews` (unkeyed views cannot have stored tables). Then bind `StoredCheckMessage`, `HasStoredCheckMessage`, `OutOfDateViews`, `CouldNotCheckViews`, `UpdateOutOfDateCommand`, `DismissStoredCheckCommand` and calls `CheckStoredTables()` after the window opens. The add-in's `ReadState` must return null (not throw) for a view with no stored tables.
- The add-in reads and writes state through `HoleTableStateJson`; no add-in change was needed, but Aidan should confirm in Inventor that a version 1 state stored earlier still opens.
