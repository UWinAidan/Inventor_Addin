# 166: Add-in: store the wizard's state, Update, and re-open a table on the Edit Table tab

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update, Edit Table tab)
- **Status:** todo
- **Depends on:** 162, 164, 165
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** none

## Goal

After the model changes, Update redraws both tables with fresh XDIM, YDIM and descriptions, keeps every tag, and tags any new hole. The Edit Table tab re-opens a view's tables with their edits and notes. Everything is stored in the drawing, so it survives save and reopen.

## Scope

- **Storing.** Store task 162's `HoleTableState` JSON in an attribute set `AWB_HoleTable` (attribute `State`, a string) on the placed table symbols. Store the view link on the sheet (spike 160, C10). Write it on Create, Update and Renumber.
  - On the document, use `AttributeSets.Add(name, false)`; `true` fails.
  - Find stored views by scanning the sheet's sketched symbols for the set.
- **`ReadState` and `UpdateTables` (task 162's source methods):**
  1. read the view again (task 163);
  2. refresh the plan (task 162);
  3. redraw both definitions in place (task 164);
  4. add tags, precision marks and centre marks for new holes;
  5. delete the ones whose hole is gone (found by task 165's mark attribute set, `HoleTableMarks.AttributeSetName`, with `HoleId`, `ViewKey` and `Role`; it is a different set from the table state's, so tags and marks are never mistaken for tables);
  6. update the tag text of any hole whose tag changed (Renumber).

  A table that no longer has any group is deleted along with its definition.
- One transaction per Update, aborted on failure.
- The status line and the log name what changed (task 162's counts).

## Acceptance criteria

- [ ] Builds on Windows.
- [ ] The manual checklist below passes.

## Notes for the implementer

- Spike 160 round 6: the update test in `spikes/160/spike160-round6b.ps1`. Round 5: C10, attribute sets survive `SaveAs` and reopen. An `AttributeSet` is enumerable, so do not pass one through LINQ that might flatten it.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Create tables, change a parameter that moves holes, then Update: XDIM and YDIM change, tags stay, and the tables stay where they were.
  2. Add a centre to a hole feature, then Update: the hole gets the next number, with a tag and marks.
  3. Remove a centre, then Update: its row, tag and marks go, and the other tags are unchanged.
  4. Renumber closes the gap.
  5. Save, close, reopen. Edit Table lists the view, and opening it shows the same tags and notes.
  6. One Undo reverts an Update.

## Follow-ups

Things noticed but not done.
