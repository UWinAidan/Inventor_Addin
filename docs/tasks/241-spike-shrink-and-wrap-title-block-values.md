# 241: Spike: smaller text and a two-line Description in the title block

- **Milestone:** M5 (Aidan overturned spec 05 question 31, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (questions 31, 32; spike 218)
- **Status:** todo
- **Depends on:** 218, 237
- **Needs:** windows (drives Inventor; the parent runs it)
- **Parallel-safe with:** any

## Goal

Aidan wants a long title block value to narrow to 0.6 width, then get smaller down to a readable floor, and Description to wrap onto two lines in its cell; only then warn. Spike 218 showed `WidthScale` works, but `AddByRectangle` wraps without shrinking and its range box cannot show an overflow. Find a way that works, or show why it cannot be done, before task 238 is built.

## Questions

1. **Smaller text on one field.** On a drawing's own title block definition (inside `Edit`), set a field text box's font size (FontSize, or a `StyleOverride FontSize` in its formatted text) with upper justification and the origin moved to baseline + cap height of the new size (task 237's rule). Does the baseline stay on the row's baseline? Together with `WidthScale` 0.6?
2. **Two lines, live.** Can a Description that is too long show on two lines while still following the part's property?
   - (a) `AddByRectangle` field box at the cell's width with a font size the add-in chooses so two lines fit the cell height: does Inventor wrap where Core's `PrintTextFit` estimate says (same break), and does `WidthScale` apply to a rectangle box?
   - (b) A field box whose formatted text holds the field and a line break: does Inventor accept a `<Br/>` around a property field?
3. **Two lines, written.** If no live way works: two plain text boxes (line 1 and line 2) whose text the add-in writes from the resolved value, split by Core, refreshed by Create Print and the fit step. What does Inventor keep after save and reopen, and does one Undo revert it?
4. Undo, save and reopen, and a second sheet, for whichever way works.

## Results

Filled in by the parent after running it.

## Follow-ups

Things noticed but not done.
