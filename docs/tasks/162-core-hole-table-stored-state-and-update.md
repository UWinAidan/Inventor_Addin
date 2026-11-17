# 162: Core: the hole table's stored state, Update that keeps tags, and re-opening a table

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update, Edit Table tab; open questions 5 and 18)
- **Status:** todo
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

- From task 158: `HoleTablePlan.Build` is the Renumber path. Update needs its own path that takes the previous tags and letters (`HoleTableRow.Number` is the number within a group); under Update a new split takes the next unused letter, not the slot after its source. Store the swapped, deleted and split `Id` sets, ordinary notes keyed by (table, description `Key`), and split notes keyed by hole `Id`.

- Spike 160 round 6 proved the refresh in Inventor. Its `Build-Groups` with `$keep` kept letters by hole on refresh (`spikes/160/spike160-round6b.ps1` on branch `spike/160-hole-table`).
- Hole `Id`s are the feature name plus the centre sketch point's reference key (task 155).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none expected
- **Inventor API members not confirmed:** none expected
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.
