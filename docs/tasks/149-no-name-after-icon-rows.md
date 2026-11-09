# 149: No name text after the icon rows

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`, `docs/features/08-window-style.md`
- **Status:** todo
- **Depends on:** 118, 144, 147
- **Needs:** windows build
- **Parallel-safe with:** 140 to 143, 148 (not 144 to 147 or 150: same window file)

## Goal

Aidan's first look (2026-11-07) found the text at the right of the Type, Seat and Drill point rows cut off ("Cl...", "Sp...", "11..."). His answer (2026-11-09): remove the muted name after the Type, Seat, Termination and Drill point icon rows. The icon's tooltip and the diagram already show the choice.

## Scope

- `src/InventorAddin/UI/AwbHoleWindow.xaml`: remove the `ChoiceNameText` text blocks bound to `SelectedItem.Text` of `TypeList`, `SeatList`, `TerminationList` and `DrillPointList`. Leave the others, such as the Units choice on the header line. Remove anything that only served them, such as a column or margin. Remove the `ChoiceNameText` style from `Styles.xaml` only if nothing else uses it.
- Check that each icon's tooltip and automation name still name the choice, so a screen reader and a hover still tell it.
- `docs/features/09-awb-hole.md` and `docs/features/08-window-style.md`: drop any sentence that describes the name after an icon row.

Out of scope:

- The icons themselves (task 148).

## Acceptance criteria

- [ ] The four rows show icons only; tooltips and automation names name each choice.
- [ ] The window still fits at 150 percent on 1080p (task 118) with no new scrolling.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

The rows are at about lines 118, 132, 474 and 565 of `AwbHoleWindow.xaml`. Task 118 laid out the rows, so read its notes before removing columns.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Open AWB Hole: the Type, Seat, Termination and Drill point rows show icons with no name after them. Nothing is cut off.
  2. Hover each icon: the tooltip names it.
  3. Light and dark themes, 100 and 150 percent: the rows line up as before.

## Follow-ups
