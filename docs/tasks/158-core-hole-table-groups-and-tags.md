# 158: Core: groups, tags, Split, swap, delete and Renumber

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output", "Decided behaviour", open questions 2, 6, 12, 13)
- **Status:** done
- **Depends on:** 155, 156, 157
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 160

## Goal

Core turns a view's holes into the two tables' groups with their tags (`A1`, `A2`, `B1` ...), and applies the wizard's row edits: swap a row to the other table, delete it, split it, and renumber. This is the plan the add-in draws and both wizard tabs edit.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTablePlan.cs` (names are suggestions):
  - `HoleTablePlan.Build(IEnumerable<HoleTableHole> holes, HoleTableEdits edits, ...)` gives two ordered lists, `Precision` and `Clearance`, of `HoleTableGroup`s. A group has its letter, its description (task 156), its table, and its rows; a row has the hole, its tag and its XDIM and YDIM text (task 157).
  - **Grouping:** holes with the same description `Key` in the same table form one group.
  - **Group order:** task 155's `HoleTableOrder`, with the description `Key` as tie-break.
  - **Row order:** Y then X (task 155).
  - **Letters** (question 13): the precision groups first, then the clearance groups continue the sequence. A to Z, then AA, AB.
  - **Tags:** the letter and the row's 1-based number in its group.
- `HoleTableEdits`: the user's changes, kept by hole `Id` so they survive a rebuild (Update, and re-opening a table; task 159 and later tasks persist them):
  - **Swap:** the hole goes to the other table than its classification gives, and groups there by its description.
  - **Delete:** the hole is left out of both tables.
  - **Split** (Aidan, question 2): the hole gets its own group and letter. Its group sits straight after the group it came from (Default, question 12).
  - **Notes:** text per group, keyed by the group's description `Key` (a Split hole's by its `Id`); NOTES shows `-` when there is none (spec).
- **Renumber** is `Build` run again on the current holes and edits: letters and numbers come out without gaps. Because the order never depends on quantity (Aidan), adding or removing an instance changes only that group's numbers, not other groups' letters. Write a test that proves it.
- A swap, delete or split naming an `Id` that is no longer among the holes is ignored, and `Build` reports the stale `Id`s so the add-in can log them.
- A deleted or swapped instance of a split hole keeps working (the edits combine).

Out of scope: the table's cell layout and titles, Align, persisting edits in the drawing (after spike 160), the view-model (159).

## Acceptance criteria

- [x] A test shaped like the spec's reference plate: two toleranced holes of one kind, six plain through holes of one diameter, one of another, four tapped holes of one thread. Tags `A1`, `A2`, `B1` to `B6`, `C1`, `D1` to `D4`, precision first.
- [x] Clearance group order follows type then diameter (plain, counterbore, countersink, spotface, tapped, taper tapped).
- [x] Swap, delete, split, and combinations; a stale `Id` is ignored and reported.
- [x] Adding an instance to group B leaves every other letter unchanged.
- [x] Notes survive a rebuild; a group with no note shows `-`.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Keep `Build` a pure function of its inputs; the view-model (task 159) calls it after every edit.
- Wording for any message goes in `HoleTableMessages` (task 157).

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests` on Windows: 4658 passed, 0 failed (includes the new `HoleTablePlanTests.cs`).
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.

- `HoleTableOrder.SortInstances` (chained Y bands, then X, then `Id`) replaced the non-transitive `HoleTableOrder.Instances`; it is the one instance-ordering rule, so tags never depend on input order.
- Group order still goes through `HoleTableOrder.Groups` (its 1e-5 cm diameter tolerance is non-transitive in theory). The plan pre-sorts holes by `Id` and uses each group's minimum diameter, so it is deterministic for real data.
- Persisting edits (a later task) must store the swapped, deleted and split `Id` sets and both note dictionaries: ordinary notes by table and description `Key`, split notes by hole `Id`.
- `HoleTablePlan.Build` is the Renumber path. Update (task 162) needs a separate path that takes the previous tags and letters, and under Update a new split takes the next unused letter.
