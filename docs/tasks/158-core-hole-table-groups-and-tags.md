# 158: Core: groups, tags, Split, swap, delete and Renumber

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output", "Decided behaviour", open questions 2, 6, 12, 13)
- **Status:** todo
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

- [ ] A test shaped like the spec's reference plate: two toleranced holes of one kind, six plain through holes of one diameter, one of another, four tapped holes of one thread. Tags `A1`, `A2`, `B1` to `B6`, `C1`, `D1` to `D4`, precision first.
- [ ] Clearance group order follows type then diameter (plain, counterbore, countersink, spotface, tapped, taper tapped).
- [ ] Swap, delete, split, and combinations; a stale `Id` is ignored and reported.
- [ ] Adding an instance to group B leaves every other letter unchanged.
- [ ] Notes survive a rebuild; a group with no note shows `-`.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Keep `Build` a pure function of its inputs; the view-model (task 159) calls it after every edit.
- Wording for any message goes in `HoleTableMessages` (task 157).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none expected
- **Inventor API members not confirmed:** none expected
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.
