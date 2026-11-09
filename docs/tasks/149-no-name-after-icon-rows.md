# 149: No name text after the icon rows

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`, `docs/features/08-window-style.md`
- **Status:** done
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

- [x] The four rows show icons only; tooltips and automation names name each choice.
- [ ] The window still fits at 150 percent on 1080p (task 118) with no new scrolling.
- [x] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

The rows are at about lines 118, 132, 474 and 565 of `AwbHoleWindow.xaml`. Task 118 laid out the rows, so read its notes before removing columns.

## Verification

Filled in by the implementer.

- **Ran:**
  - `tasklist` showed Inventor not running, so `dotnet build InventorAddin.slnx` with the deploy step on: 0 errors, 9 warnings (the existing CS8600 warnings in the commands, none in changed files).
  - `dotnet test tests/InventorAddin.Core.Tests`: 4483 passed, 0 failed, 0 skipped (one full run).
- **Changed:**
  - `src/InventorAddin/UI/AwbHoleWindow.xaml`: the four `ChoiceNameText` text blocks are gone, and with them the `DockPanel` around each icon row (it only docked the row left so the name could fill the rest) and the rows' `DockPanel.Dock="Left"`. Each `ChoiceList` now sits straight in its row or its row's `StackPanel`; `IconChoiceList` already sets `HorizontalAlignment="Left"`, so the buttons stay where they were. The Hole section's comment now says icons only.
  - `src/InventorAddin/UI/Theme/Styles.xaml`: the `ChoiceNameText` style removed; nothing else used it. `SideNoteText` stays (Units, depth units, Part type).
  - `docs/features/08-window-style.md`: the Type and Seat line says icons only, the tooltip naming each choice. `docs/features/09-awb-hole.md` had no sentence about the name after a row, so it is unchanged.
  - Tooltips and screen-reader names: unchanged. `IconChoiceItem` sets each button's `ToolTip` and `AutomationProperties.Name` to the option's `Text`, and each row keeps its own `AutomationProperties.Name` (Type, Seat, Termination, Drill point).
- **Not compiled (changed under `src/InventorAddin`):** none; built on Windows.
- **Inventor API members not confirmed:** none; no Inventor API code changed.
- **Manual checklist for Inventor:**
  1. Open a part and press AWB Hole: the Type, Seat, Termination and Drill point rows (Drill point shows for a blind hole) show icons with no name after them. Nothing is cut off.
  2. Hover each icon: the tooltip names it (for example Clearance, Counterbore, Through All, 118 deg).
  3. Light and dark themes, 100 and 150 percent: the rows line up with the other fields as before, and a blind clearance hole still fits at 150 percent on 1080p with the diagram open, no new scroll bar.
  4. Optional, Narrator: Tab into the Type row; it reads the row name and the chosen button's name.

## Follow-ups

None.
