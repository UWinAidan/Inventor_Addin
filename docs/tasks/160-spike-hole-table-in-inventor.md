# 160: Spike: Inventor's hole table or a custom table; reading a view's holes and its ordinate origin

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Design decision to settle with a spike on Windows", "Add-in side", "Decided behaviour", open questions 15 to 17)
- **Status:** todo
- **Depends on:** none (independent of the Core tasks 155 to 159)
- **Needs:** **windows** (spike, local session with Aidan in Inventor 2026)
- **Parallel-safe with:** 155 to 159 (separate spike branch)

## Goal

Settle how the add-in side of the wizard talks to Inventor before any add-in task is written: which table it draws (Inventor's own hole table or a custom table with our own tags), how it finds a view's holes and their sheet positions, how it reads the view's ordinate-dimension origin (Aidan, 2026-11-12: the wizard uses it and never invents one), and where the wizard keeps its edits so Update and the Edit Table tab can re-open a table. Nothing from this spike is merged.

## Questions

**A. Which table.** Aidan's two tables share one tag sequence (precision letters first), have custom titles (`Precision Table: <view>`, `Tap and Clearance Table: <view>`), the columns HOLE, XDIM, YDIM, DESCRIPTION, NOTES, a DESCRIPTION cell merged across each group's rows with stacked deviations, NOTES text per group, and Split groups.

1. **Inventor's hole table** (`Sheet.HoleTables`; `HoleTables.Add` from a view and an origin?): can it take
   - our tags (`HoleTableRow.HoleTag` writable?) and a letter sequence that runs on from one table to the other?
   - a custom title, our five columns, our description text (a row's description cell writable, or overridden?)
   - merged description cells or one description per group?
   - only the holes we choose (filtering rows, or two tables with complementary row sets)?
   - and does it keep our overrides when the model changes and Inventor updates it?
   - Does it use the view's ordinate origin, or its own origin indicator?
2. **A custom table** (`Sheet.CustomTables.Add`?): titles, column widths, row heights; merged cells (is there an API to merge, or only by hand?); stacked text or two lines in one cell (formatted text in a cell?); Inventor's counterbore, countersink and depth symbols in a cell (question 15).
3. **Tags on the view** with a custom table: what keeps a tag next to its hole when the view moves or the model changes (a leader note or general note attached to the hole's circle by a `GeometryIntent`? a balloon? a sketched symbol?). Does it follow, and does it disappear or orphan when the hole is deleted?
4. **Precision centre marks** (the reference's filled-quadrant marks): a centre mark style, a sketched symbol, or nothing for the first version?

**B. Reading the view.**

5. **The holes in a view and their sheet positions:** from the view's drawing curves (`DrawingView.DrawingCurves`, a curve's `ModelGeometry` back to the `HoleFeature`, `CenterPoint` on the sheet?), or from the model's hole centres through `DrawingView.ModelToSheetTransform`? Which one tells which centre index of the feature an instance is (task 155's `Id`), and which one tells a hole seen end on from one seen from the side (question 16)?
6. **Hidden holes:** a hole behind another face, a hole in a section or break-out, a suppressed instance, a pattern of holes (a `RectangularPatternFeature` of a hole: does each occurrence come back as the same `HoleFeature`?). Which should be listed? Ask Aidan as they come up.
7. **Holes in an assembly view**: a view of an assembly reaches holes through occurrence proxies. Is that wanted in the first version, or part views only? Ask Aidan.
8. **The ordinate origin:** where an ordinate dimension set or a single ordinate dimension keeps its origin (`OrdinateDimensionSet` and its origin member?, `OrdinateDimension.Origin`?), how to tell which view a dimension belongs to, and the origin's sheet position. What happens with ordinates made by Inventor's "Ordinate Dimension Set" and by the single "Ordinate" command; several sets on one view (question 17).
9. **The title block corner** for Align: the title block's position and size on the sheet (`TitleBlock.Position`?, `RangeBox`?), and the border's inner edge.

**C. Keeping the wizard's edits.**

10. Where the swaps, deletes, splits and notes (task 158's `HoleTableEdits`) live so Update and the Edit Table tab can rebuild the plan: attributes on the table, on the sheet, or on the drawing (`AttributeSets`, as AWB Hole stores hole data). Do they survive Save, close and reopen, and copying the drawing?
11. A hole instance's `Id` across model changes: does a feature name plus centre index stay steady when a sketch point is added or deleted, or is the sketch point's own identity (reference key?) better?

## How

- A spike branch `spike/160-hole-table`, off `main`, with a temporary developer button. Aidan makes a test part of his own (no employer data) with each hole type in scope (spec 04 question 8), a drawing with a base view, ordinate dimensions from a corner, and a second view with no ordinates.
- Round 1 (B first, since both table routes need it): read every hole in the view with its sheet position and the origin, and log them with what each step returned.
- Round 2: make one of each table type with three groups and compare them against the reference with Aidan.
- Check every member against the Inventor API reference before trying it (`CLAUDE.md`). Results go in this file.

## Results

(Filled in during the spike.)

## Then

From the results, the parent writes the add-in tasks and the remaining Core tasks:
- the table model and Align layout in Core (if the custom table is chosen), or the mapping onto Inventor's hole table
- Update and the Edit Table tab's re-opening (Core: merging stored edits with a fresh read)
- persisting the edits
- the add-in: reading a view, drawing tables and tags, the WPF window bound to task 159's view-model, the ribbon button on the drawing tab
