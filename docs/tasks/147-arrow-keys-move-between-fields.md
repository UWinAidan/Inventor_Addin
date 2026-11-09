# 147: Up and Down arrows move between fields in the AWB Hole window

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`, `docs/features/08-window-style.md`
- **Status:** todo
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

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Open AWB Hole and click in a text box. Down moves to the next field and Up to the previous, in the same order as Tab and Shift+Tab.
  2. Open a dropdown with Alt+Down: Up and Down change the entry; Enter or Escape closes it.
  3. On the Type row: Left and Right change the type; Down leaves the row.
  4. Enter still OKs from a text box.

## Follow-ups
