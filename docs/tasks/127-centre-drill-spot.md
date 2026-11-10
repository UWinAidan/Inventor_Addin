# 127: Centre-drill spot: a small countersink on any hole, and a seat depth of 0

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats"; question 20)
- **Status:** done
- **Depends on:** 108, 126
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-25: a centre-drill spot should be easy: a small countersink at 60 or 90 deg on any hole type. Also check whether Inventor takes a seat depth of 0, and allow it if it does.

## Scope

- Core (`ViewModels/AwbHoleViewModel.cs`): the countersink angle box offers 60 and 90 deg as quick choices (an editable dropdown; other angles can still be typed) on every type (Default). The countersink diameter starts as task 126 says.
- **Seat depth 0 is allowed** (spike 108 question 7: Inventor accepts a counterbore or spotface depth of 0, keeps the hole type and models only the hole). The depth check allows 0 for counterbore and spotface depths, and the diagram draws no seat wall. Spec 09 question 20 is answered by this.
- Add-in: the window's angle box as a dropdown; nothing else changes if 126 is done.

## Acceptance criteria

- [ ] A tapped, clearance, dowel or Simple hole can take a 60 or 90 deg countersink in two clicks after choosing Countersink. (Core tests pass and the window builds; the two clicks need Aidan's check in Inventor, checklist items 1, 2, 4 and 6.)
- [x] A counterbore or spotface depth of 0 is allowed, with tests; a negative depth is still refused.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

- **Ran (Aidan's PC, Windows, Inventor 2026 installed, Inventor not running):**
  - `dotnet test tests/InventorAddin.Core.Tests`: passed 4149, failed 0. 18 tests added; 5 existing tests changed because a counterbore depth of 0 is no longer a problem (they now use a negative depth).
  - `dotnet build InventorAddin.slnx -tl:off`: build succeeded, with the deploy step (Inventor was closed). The only warnings are the existing CS8600 ones in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand` and `SaveAsReplaceCommand`, which this task does not touch.
- **Not compiled (changed under `src/InventorAddin`):** none. `UI/AwbHoleWindow.xaml`, `UI/AwbHoleWindow.xaml.cs` and `UI/Theme/Styles.xaml` compiled in the Windows build above. Nothing has been run in Inventor, and the new dropdown template has not been seen on screen.
- **Inventor API members not confirmed:** none. No Inventor API member is new. Spike 108 question 7 showed that Inventor accepts a counterbore or spotface depth of 0 and a small countersink on tapped, dowel and Simple holes, so `HoleFeatureWriter` is unchanged.
- **WPF members used for the first time in this add-in:** `ComboBox.IsEditable` set to True with a `PART_EditableTextBox` template part, `ComboBox.Text` bound two-way, `ComboBox.DropDownClosed`, and `FrameworkElement.TemplatedParent`. All are standard WPF and compiled. Their runtime behaviour is untested: selection following typed text, and focus moving into the inner text box.
- **What changed:**
  - Core: `HoleRules.CountersinkQuickAnglesDeg` (60, 90). `AwbHoleViewModel.CountersinkAngleChoices` lists them for the box. A counterbore depth of 0 is allowed, and below 0 the check says "The counterbore depth is less than zero." A spotface depth of 0 was already allowed by task 112. `HoleDowelSeat.Valid` keeps a counterbore depth of 0, so it is remembered as the last seat. A remembered counterbore depth expression that comes to 0 starts the box. The diagram draws a counterbore or spotface of depth 0 flush with the top face, with no wall and no depth dimension. The seat diameter is still shown.
  - Add-in: the countersink angle box is an editable dropdown (`EditableComboBox`, a new style in `Styles.xaml`, 104 wide like a depth box). Picking an entry commits it at once, on `DropDownClosed`. A typed angle commits when the box loses focus or on Enter, which now also commits a box inside an editable dropdown.
  - Spec 09: no change needed. Question 20 was already answered there (spike 108, task 127), and the Seat sizes row already describes the 60 and 90 deg quick choices as a Default.
- **Manual checklist for Inventor:** open a part of your own with an unconsumed sketch containing a few points. Start AWB Hole on the 3D Model tab, or press H.
  1. Tapped M6, Seat Countersink. The Csink angle box has an arrow at its right end. Click the arrow, then click 60. The box shows 60, the diagram's angle changes at once, and the preview updates with no other click. Type 7 in Csink dia and OK: the spot is in the model. Edit Feature reads back 7 at 60.
  2. Dowel 6 mm, Countersink: pick 90 from the dropdown, OK. The model has the countersink.
  3. Simple 4 mm and a clearance hole, Countersink at 60: likewise.
  4. Type an angle that is not in the list (for example 82) and press Tab or Enter: 82 is kept. Open the list and pick 90: the box goes back to 90. Typing 6 may autocomplete to 60, with the 0 selected, because the dropdown matches typed text against its entries. Typing a further digit replaces the 0.
  5. Look at the angle box in the light and dark themes. Its border, fill, hover, focus outline (press Tab into it) and list should match the other dropdowns, and its text should line up with the depth boxes. Shift+Tab out of the Csink angle box moves focus to the previous field.
  6. After picking 60 or 90 from the dropdown, the preview updates without clicking elsewhere.
  7. Counterbore depth 0 on a clearance hole and spotface depth 0 on a tapped hole: no message under the box, and the diagram shows no seat wall. OK: the hole is made. Edit reads the seat back with depth 0 and offers no re-apply.
  8. Counterbore depth -1: "The counterbore depth is less than zero." appears under the box and OK is disabled.
  9. A dowel with a counterbore depth of 0, then OK. Start a new hole of the same dowel size with Counterbore: the depth box starts at 0, because it is remembered.

## Defaults

Behaviour picked to keep work moving. The parent should log these in `DECISIONS.md` as Default; Aidan may overturn them.

- **Picking 60 or 90 from the dropdown commits at once.** It updates the hole and the preview, so a centre-drill spot is two clicks. A typed angle commits when the box loses focus or on Enter, like the other boxes.
- **The angle dropdown matches typed text against its entries**, which is WPF's standard editable-dropdown behaviour. Typing 6 suggests 60, with the suggested part selected so the next digit replaces it. Switching this off would let the selection drift from the text, so that picking an entry already selected would not change the box.
- **A counterbore depth of 0 is remembered as the last seat**, like a spotface depth of 0 (task 112), for dowel, tapped and Simple holes.
- **A seat of depth 0 is drawn in the diagram flush with the top face**, with no wall and no depth dimension. Its diameter is still dimensioned.

## Follow-ups

- The workbook's Counterbore and Dowel seat columns still require a counterbore depth greater than zero (`HoleWorkbookReader`). This task allows 0 only in the window. Whether a workbook row may hold a depth of 0 is for Aidan to decide.
- `HoleDowelSeat.Valid` still keeps a remembered countersink angle between 0 and 180 deg, while `HoleRules` asks for 1 to 179 (already noted by task 126).
- The `EditableComboBox` style is the add-in's only editable dropdown template. It needs a look on screen in both themes (checklist item 5) before another window uses it.
