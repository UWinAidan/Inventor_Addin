# 146: A "Custom..." entry on the countersink angle

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** 127
- **Needs:** Core, then windows build
- **Parallel-safe with:** 140 to 143, 145 (not 144 or 147: same window files)

## Goal

Aidan's request (2026-11-09): the countersink angle dropdown gets a last entry, "Custom...". Choosing it puts the cursor in the box to type any angle. Typing an angle already works; this makes it discoverable.

## Scope

- `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs` (`CountersinkAngleChoices`) and tests: "Custom..." listed last. Choosing it is never taken as a value: the angle is not changed, and the text box is cleared or keeps the current angle, whichever is clearer.
- `src/InventorAddin/UI/AwbHoleWindow.xaml` and `.xaml.cs` (`CountersinkAngleBox`, `OnCountersinkAngleDropDownClosed`): when "Custom..." is chosen, the editable box gets focus with the text selected, so typing replaces it.
- `docs/features/09-awb-hole.md`: one line.

Out of scope:

- The angle rules (1 to 179 deg) and the live typing (task 130).

## Acceptance criteria

- [ ] Core tests: "Custom..." is last; choosing it changes no value and leaves `CanOk` as it was.
- [ ] Choosing it in the window puts the cursor in the box; a typed angle applies as before.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

The dropdown is editable and bound by `Text` (task 127). A choice of "Custom..." must not reach the binding as the text "Custom...". Check the order in which the drop-down closes and the text updates.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. A countersunk hole: open the angle dropdown. "Custom..." is last.
  2. Choose it: the cursor is in the box, ready to type. Type 100 and press Tab: the preview shows 100 deg.
  3. Choose a listed angle: it applies at once, as before.

## Follow-ups
