# 130: Text boxes update the preview as you type

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window")
- **Status:** todo
- **Depends on:** 124, 126
- **Needs:** windows build (Core first)
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-25: every text box in the AWB Hole window updates the preview live, about **300 ms after the last keystroke**, without clicking elsewhere. While the text is incomplete or invalid, the last good preview stays and the message shows under the box. This replaces the task 090 Default that depth boxes update on losing focus or Enter.

## Scope

- Core: a small debounce helper with an injectable clock or scheduler (so it is testable), and the view-model rule: text that parses (number or Inventor expression, checked as today) is applied after the pause; text that does not leaves the model and the preview at the last good value and sets the box's message; Enter and losing focus apply at once. Tests with a fake clock.
- `src/InventorAddin/UI/AwbHoleWindow.xaml(.cs)`: bindings update on each keystroke (`UpdateSourceTrigger=PropertyChanged`) through the debounce, for every text box (depths, seat boxes, and the Diameter and Custom boxes when 112 and 113 add them; their tasks follow this rule). Expression checks that call Inventor (`HoleExpressionChecker`) run only after the pause, on the UI thread.
- OK uses the latest text: pressing OK within the 300 ms applies it first.

## Acceptance criteria

- [ ] Typing `12` into Depth changes the preview about 300 ms after the last key, with no focus change.
- [ ] Typing `d0+` shows the message under the box and keeps the last preview; finishing it as `d0+2` updates it.
- [ ] OK straight after typing uses the typed value.
- [ ] Core tests for the debounce; `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Blind clearance hole: type a depth; the preview follows without clicking away.
  2. Type an incomplete expression: message under the box, preview unchanged. Complete it: preview updates.
  3. The same in a seat box and in the thread depth box.
  4. Type and press OK at once: the hole has the typed value.

## Follow-ups
