# 161: Core: hole table layout as a drawing (lines and text), formatted text, and Align

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output", "Design decision, settled by spike 160")
- **Status:** todo
- **Depends on:** 156, 158
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 159

## Goal

Core turns a table plan (task 158) into exactly what the add-in draws inside a table's sketched symbol definition: every grid line and every text box with its formatted text and centre, in the definition's own coordinates. Core also gives the two placement points for Align. The add-in then only draws and places what it is given. These are the rules that spike 160 proved in Inventor (Aidan, option 1).

## Scope

`src/InventorAddin.Core/HoleTable/`; tests in `tests/InventorAddin.Core.Tests/HoleTable/`.

- **`HoleTableFormattedText`.** Builds Inventor formatted text from task 156's description parts:
  - A symbol becomes one AIGDT character in its own override at the data text height (`<StyleOverride Font='AIGDT' FontSize='0.3048'>n</StyleOverride>`, with the height written in invariant culture to 4 decimals). The characters are `n` diameter, `v` counterbore, `w` countersink, `x` depth, and U+00C2 spotface. Counterbore, countersink, spotface and depth are each followed by a space, as Inventor's hole table writes them.
  - A toleranced line is one text: the size and fit, a space, `<Stack FractionalTextScale='1.0'>upper^lower</Stack>`, a space, then the termination.
  - Plain text is XML-escaped (`&`, `<`, `>`, apostrophe).
- **`HoleTableLayout.For(plan, measure, metrics)`.** Gives one `HoleTableDrawing` per non-empty table.
  - `measure` is an interface the add-in implements: the width in cm of a formatted text in a text role (Title, Header, Data). Tests use a fake.
  - `metrics` holds the data text height and the one-line row height, both read by the add-in from Inventor's hole table style.
  - A drawing has its width and height, its lines (start, end, and Inside or Outside weight), and its texts (role, formatted text, centre, and whether the text has a stack).
  - **Coordinates.** The origin is the table's bottom-right corner (the definition's insertion point), so x ≤ 0 and y ≥ 0.
  - **Columns.** HOLE, XDIM, YDIM, DESCRIPTION, NOTES. The two tables share their column widths. Each width is the widest content of either table (header included), plus one text height of margin on each side, rounded to 0.001 cm. If the wider title needs more room than the columns give, DESCRIPTION takes the difference.
  - **Rows.** A title row and a header row, each one row height. Then each group in turn, with n rows and a description of k lines (a stacked one counts as 2). The group's height is max(n x row height, row height + (k - 1) x 1.75 x text height), and its rows share that height evenly.
  - **Lines.** The outside border is drawn with the Outside weight; every other line uses the Inside weight. There is a rule under the title and one under the header. The column rules run from the bottom up to the title rule. Rules between rows inside a group stop at DESCRIPTION, so DESCRIPTION and NOTES are merged per group. A rule between groups runs across the full width.
  - **Texts.** Every text is centred in its cell. The title is centred across the full width. A group's description sits at the centre of its merged cell. A stacked description is one text. Any other description is one text per line, at a pitch of 1.75 x the text height, centred as a block. NOTES shows the group's note, or `-` (task 158).
  - The tags, XDIM and YDIM are the plan's text (task 157's decimals).
- **`HoleTableAlign.Positions(titleBlockMaxX, titleBlockMaxY, clearanceHeight, precisionHeight)`.** Gives the placement points: the clearance table at the title block's top-right corner, and the precision table straight above it. If only one table exists, it goes on the corner (Aidan, question 4).

Out of scope: measuring text in Inventor, drawing, and placing (task 164).

## Acceptance criteria

- [ ] Formatted text for each description shape in task 156's tests: plain, blind with the depth symbol, each seat, tapped, taper tapped, and toleranced with a stack. Each matches the spike's strings exactly.
- [ ] Layout of the spec's reference plate, using a fake measure that returns 0.2 cm per character, gives the expected widths, group heights, line count and text centres. A one-line group of 3 rows, a lone two-line hole, and a two-line group of 2 rows each follow the height rule.
- [ ] Shared widths: a wide clearance description widens the precision table's DESCRIPTION too. A long title widens DESCRIPTION.
- [ ] No row rule crosses DESCRIPTION or NOTES inside a group, and every rule between groups crosses the full width.
- [ ] Align with both tables, and with only one.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- The proved Inventor version is the spike's script `spikes/160/spike160-round6b.ps1` on branch `spike/160-hole-table`: functions `Layout`, `Draw-Table`, `DescFt` and `Build-Tables`. Read it with `git show spike/160-hole-table:spikes/160/spike160-round6b.ps1`. Match its geometry.
- Lengths are in cm, like the rest of Core. Write each rule's source in a doc comment (spike 160, round number).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none expected
- **Inventor API members not confirmed:** none expected
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.
