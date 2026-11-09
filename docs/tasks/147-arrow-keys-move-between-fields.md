# 147: Up and Down arrows move between fields in the AWB Hole window

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`, `docs/features/08-window-style.md`
- **Status:** done
- **Depends on:** 118
- **Needs:** windows build
- **Parallel-safe with:** 140 to 143, 145 (not 144 or 146: same window files)

## Goal

Aidan's request (2026-11-09): in the AWB Hole window, Up and Down move focus to the previous and next field, as Shift+Tab and Tab do. The exceptions:

- Inside an open dropdown, the arrows still change the value.
- In icon rows (Type, Seat, Units, and the like), Left and Right still change the choice. Up and Down leave the row.

## Scope

- `src/InventorAddin/UI/AwbHoleWindow.xaml.cs` (`OnPreviewKeyDown`): Up and Down with no modifier call `MoveFocus(FocusNavigationDirection.Previous/Next)` from the focused element, unless an open dropdown or an editable dropdown's open list has the key. A closed dropdown moves focus too, rather than changing its value (Aidan's rule). Say so in Follow-ups if that feels wrong.
- Icon rows: check that Left and Right still change the choice and that Up and Down are not swallowed by the row's own key handling (`UnitsList` and the other choice lists).
- `docs/features/09-awb-hole.md`: one line under the window's keyboard behaviour.

Out of scope:

- Other windows. Say in Follow-ups if the same rule belongs in spec 08 for every window.

## Acceptance criteria

- [ ] Up and Down move focus between fields; an open dropdown keeps its arrows; icon rows keep Left and Right.
- [ ] Enter (OK) and Alt+U behave as before.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Keep the handler in code-behind: it is keyboard routing, not a rule about holes. The window has a Units list on the header line and collapsible rows. `MoveFocus` skips hidden and disabled controls, so check that a collapsed row is skipped.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx` on Windows (Inventor closed, deployed to the Inventor 2026 Addins folder): 0 errors, 9 warnings, none in `AwbHoleWindow.xaml.cs`. `dotnet test tests/InventorAddin.Core.Tests`: 4483 passed, 0 failed. No Core change, so no new tests: the handler is keyboard routing in code-behind.
- **Not compiled (changed under `src/InventorAddin`):** none; `UI/AwbHoleWindow.xaml.cs` was built on Windows. Not yet tried in Inventor.
- **Inventor API members not confirmed:** none used. WPF only: `UIElement.MoveFocus`, `TraversalRequest`, `FocusNavigationDirection.Previous/Next`, `Keyboard.FocusedElement`, `ComboBox.IsDropDownOpen`.
- **How it works:** the window's `PreviewKeyDown` takes Up and Down with no modifier before the focused control sees them and calls `MoveFocus(Previous/Next)` on the focused element. It lets them through when the key comes from an open dropdown (a `ComboBoxItem`, a `ComboBox` with its list open, or the countersink angle's text box while its list is open). A closed dropdown, the editable countersink angle included, moves focus instead of changing value. Icon rows (`ChoiceList`) never see Up and Down, so they leave the row; Left and Right are not touched and still change the choice. Text boxes are all single line, so Up and Down cost the caret nothing. Enter's open-dropdown check now uses the same helper; Enter and Alt+U behave as before.
- **Manual checklist for Inventor:**
  1. Open AWB Hole and click in a text box. Down moves to the next field and Up to the previous, in the same order as Tab and Shift+Tab.
  2. Open a dropdown with Alt+Down: Up and Down change the entry; Enter or Escape closes it.
  3. On the Type row: Left and Right change the type; Down leaves the row.
  4. Enter still OKs from a text box.
  5. Tab to a closed dropdown (Size, Fit): Up and Down move focus, the value does not change. Same in the countersink angle box (Countersink seat): the angle stays; picking "Custom..." from its open list still puts the cursor in the box.
  6. Collapse the diagram and choose a type that hides rows (Simple): Down skips the hidden rows. Scroll the window small: Down brings the next field into view.
  7. Alt+U still jumps to Units; Left and Right there switch mm and in; Down leaves it.

## Follow-ups

- The same Up and Down rule may belong in spec 08 for every window (the material and finish window has its own arrow handling in its search boxes and lists, so it would need care). Not done: out of scope.
- A closed dropdown no longer steps through its entries with Up and Down (Aidan's rule); Alt+Down opens it, or typing a letter still jumps to an entry. If that feels slow in use, an option is to let Up and Down change a closed dropdown's value and leave only text boxes and icon rows to move focus.
- Up and Down no longer scroll the window body when a field has focus; focus moving brings the next field into view instead.
