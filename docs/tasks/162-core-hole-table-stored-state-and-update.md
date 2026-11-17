# 162: Core: the hole table's stored state, Update that keeps tags, and re-opening a table

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update, Edit Table tab; open questions 5 and 18)
- **Status:** done
- **Depends on:** 158, 159
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 161

## Goal

What the wizard must remember about a view's tables is one Core record that the add-in saves as a string in an attribute set and reads back (spike 160, C10). Update rebuilds the tables from the model and keeps every tag. The Edit Table tab re-opens a view's tables with their edits.

## Scope

`src/InventorAddin.Core/HoleTable/` and `ViewModels/HoleTableWizardViewModel.cs`. Tests sit next to the existing ones.

- **`HoleTableState` record.** Holds:
  - a format version;
  - the sheet and view names;
  - task 158's `HoleTableEdits` (swaps, deletes, splits, notes);
  - the tags in force: each hole `Id` mapped to its letter and number.
- **`HoleTableStateJson.Write` and `Read`.** `System.Text.Json`, invariant culture. `Read` of an unknown version, or of text that is not JSON, gives null and a reason; it never throws. Keep the payload under 32 KB for 200 holes (test it).
- **`HoleTablePlan.Refresh(holes, edits, previousTags)`.** Update (Default, question 18):
  - Every hole still present keeps its tag.
  - A new hole in an existing group takes the next number after that group's highest.
  - A new group takes the next letter after the highest in use. Letters run on across both tables, as task 158 does.
  - Rows are ordered by tag within each group, and groups by letter.
  - Holes that are gone drop out and leave gaps. Only Renumber (`Build`) closes gaps and reorders.
  - Return what changed: holes added, holes gone, and descriptions changed. The add-in logs it, and the status line names the counts.
- **View-model, Edit Table tab** (Aidan, question 5):
  - `IHoleTableSource` gains `ReadState(sheet, view)`, which returns a `HoleTableState` or null.
  - The tab lists the views that have stored tables. Opening one analyses the view, then calls `Refresh` with the stored edits and tags. Swap, delete, split, renumber and NOTES then work as on the Create Tables tab.
  - **Update** on either tab writes the refreshed plan through the source (`UpdateTables(plan, state)`).
  - Write the stored state on every Create, Update and Renumber.
- An `Id` in the stored edits or tags that is no longer present is dropped and reported, as in task 158.

Out of scope: the attribute set calls and drawing (task 166).

## Acceptance criteria

- [ ] A JSON round trip of a state with every kind of edit and 50 tags gives an equal state. A wrong version and broken text return null with a reason.
- [ ] Refresh:
  - a hole moved: same tag;
  - a new hole in group B: `B7` after `B6`;
  - a new group of a new type: the next unused letter, even if its sort order would put it earlier;
  - a hole removed: a gap left, and every other tag unchanged.
- [ ] Refresh then Renumber equals Build.
- [ ] View-model: re-opening a stored view shows the stored tags and notes. Update calls the source with the refreshed plan. A stale `Id` is reported.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- From task 159's review: the Edit Table tab wants NOTES per group, but the view-model only edits notes through a hole row; add a group-level list (Letter, Description, Notes). Report `HoleTablePlan.StaleIds` in the status line.
- From task 158: `HoleTablePlan.Build` is the Renumber path. Update needs its own path that takes the previous tags and letters (`HoleTableRow.Number` is the number within a group); under Update a new split takes the next unused letter, not the slot after its source. Store the swapped, deleted and split `Id` sets, ordinary notes keyed by (table, description `Key`), and split notes keyed by hole `Id`.

- Spike 160 round 6 proved the refresh in Inventor. Its `Build-Groups` with `$keep` kept letters by hole on refresh (`spikes/160/spike160-round6b.ps1` on branch `spike/160-hole-table`).
- Hole `Id`s are the feature name plus the centre sketch point's reference key (task 155).

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core` (0 warnings, 0 errors); `dotnet test tests/InventorAddin.Core.Tests`: 4768 passed, 0 failed after review fixes (4707 before). Under load a few Numbering tests failed once in a full run; run alone (862) and with the rest excluded (3906) all pass.
- **Not compiled (changed under `src/InventorAddin`):** none. Nothing under `src/InventorAddin` implements `IHoleTableSource` yet, so the interface change breaks no add-in code. Task 166 must implement the new members.
- **Inventor API members not confirmed:** none (Core only).
- **Manual checklist for Inventor:** none (Core only).

### What was built

- `HoleTable/HoleTableState.cs`: `HoleTableTag`, `HoleTableStoredGroup`, `HoleTableStoredTags`, `HoleTableState` (value equality), `HoleTableStateJson.Write` and `Read` (never throws; unknown version or bad text gives null and a reason).
- `HoleTable/HoleTablePlan.Refresh.cs`: `HoleTablePlan.Refresh`, `ToStoredTags`, `LetterIndex`, `HoleTableRefresh` (plan, cleaned edits, added, gone, description changes, `Summary`).
- `HoleTablePlan` is now `partial`. `HoleTableMessages` gained `RefreshSummary` and `StaleIdsDropped`.
- `IHoleTableSource`: `ReadState`, `UpdateTables(sheet, view, plan, state)`; `CreateTables` and `GenerateTable` now also take the `HoleTableState` to store.
- View-model: `UpdateCommand`, `StoredViews`, `SelectedStoredView`, `OpenStoredViewCommand`, `OpenStoredView(key)`, `Groups` (`HoleTableWizardGroup`: Letter, Table, Description, Notes), `RefreshStoredViews()`.

### Deviations from the brief

- `Refresh` also takes `origin`, `viewScale` and `drawingUnits` (it builds rows with coordinates) and takes a `HoleTableStoredTags` (the id-to-tag map plus a list of what each letter stood for) instead of a bare tag map. The letter list is what lets Update tell a moved hole from one whose description changed, and a split group from an ordinary one. It adds the table, description key and split id per group to the stored state; 200 holes stay well under 32 KB (tested).
- Renumber does not call the source: the drawing is rewritten only by Create or Update, which store the state. A Renumber in the window changes the lists and the window's tags; the stored state changes on the next Create or Update. Say if Renumber should write straight away.
- Behaviour choices (defaults, for DECISIONS.md if the parent agrees): "highest in use" for numbers and letters means among the holes and groups present now, so removing the highest hole of a group and adding another reuses its number. A hole keeps its tag only while it stays in its letter's group (same table and description, or still the split hole). A hole that was swapped, split, unsplit, or whose description changed alone is placed as a new hole (next number in a matching group, else next letter). A whole group whose description changed keeps its letter and is reported as a description change. A user-deleted hole drops out silently and is not counted as gone.
- In the window, Swap, Delete, Split and Notes use `Refresh` (tags kept) once the view was opened, created or updated here, and `Build` (renumber) before that, so the Create Tables tab behaves as before.

## Follow-ups

- Task 166 must implement `ReadState`, `UpdateTables`, and the extra `state` argument on `CreateTables` and `GenerateTable`, store `HoleTableStateJson.Write(state)` in the attribute set, and log `HoleTableRefresh.Summary` (the window shows it already).
- The window (task for the Edit Table tab XAML) binds `StoredViews`, `SelectedStoredView`, `OpenStoredViewCommand`, `Groups` (Notes) and `UpdateCommand`.
- Open question for Aidan: should Renumber on an opened table rewrite the drawing at once, or wait for Update?

### Review fixes (round 1)

- `HoleTableStateJson.Read` turns null list entries, bad letters and tag numbers below 1 into a reason; it catches every exception. Four null-entry strings added to the theory.
- High-water marks: `HoleTableStoredTags` carries the next letter and the highest number used per letter (JSON `nl` and `hn`; missing fields fall back to the stored tags). `Refresh` carries them (`HoleTablePlan.HighWater`); `Build` resets them. A retired tag or letter is never given out again, so `A_whole_group_removed_...` now expects D.
- View-model: the base tags are replaced only by Open, Create, Update, Generate (each on success) and Renumber; every window edit refreshes against that base, so swap and swap back, or split and unsplit, restore every tag. Analyze View on a view with stored tables opens it as the Edit Table tab does, and Update takes the stored edits whenever the tags come from storage. Generate refreshes `StoredViews`.
- Spec 04 open question 20 (Renumber on an opened table).
- Behaviour choice changed from round 0: "highest in use" is now "highest ever used" (the reviewer's request). There is no undelete in the window, so the delete/undelete test was not written.
- The 32 KB test assumes hole ids of about 75 characters. Task 166 should check the length of one real feature name plus reference key in Inventor.
