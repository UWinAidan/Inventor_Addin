# 241: Spike: smaller text and a two-line Description in the title block

- **Milestone:** M5 (Aidan overturned spec 05 question 31, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (questions 31, 32; spike 218)
- **Status:** done (spike)
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

**Method.** As in spikes 218 and 225: PowerShell 5.1 over late-bound COM, with `SilentOperation` on while the scripts ran. The scripts and pictures are in the session's job folder (`tmp\241\`, not in the repo).

- Each run made a fresh drawing from `C:\AwbSpike\241\AWB Metric 241.idw`, a copy of the current template (already rebuilt with task 237's upper justification). It placed a base view of a copy of the 218 test part, whose Description is an 84-character invented value and whose Finish Spec is a 47-character one.
- Each experiment edited that drawing's own title block definition (`TitleBlockDefinition.Edit`, then `ExitEdit(true)`). The templates in `libraries/Templates/` were not opened.
- Measurements came from `RangeBox` and from `View.SaveAsBitmap` pictures of the block (16.1 px/mm, so one pixel is 0.06 mm). Baselines were read as the lowest ink of letters without descenders, and line widths as the rightmost ink in each line's band.
- Size convention: the `StyleOverride FontSize` in a field's formatted text is the **cap height** in cm (`0.0727 × em mm`). The template's 2.8 mm values are `FontSize='0.2036'`, and Description's 4 mm is `0.2908`. With upper justification, the origin is the top of the capitals.

### Findings

| Question | Result |
|---|---|
| 1. Smaller text on one field | **Works.** Change `FontSize` in the `StyleOverride` and move the origin to baseline + new cap height. The baseline stays on the row's baseline within 0.06 mm (1 px), with or without `WidthScale` 0.6. `TextBox` has no `FontSize` member. |
| 2a. Wrap with a rectangle box | **Works, and it stays live.** The box wraps the resolved value and re-wraps when the part's property changes. `WidthScale` applies to a rectangle box, and the wrap uses the scaled width. Inventor's breaks matched `PrintTextFit.Wrap` in 7 of 8 cases. In the eighth, Inventor fitted one more word on line 1, because the estimate is 0.2 to 0.6 % wide. Inventor therefore never uses more lines than Core predicts. |
| 2a. New member | `TextBox.Width` is **writable** inside `Edit`. Setting it on an existing fitted field box makes it a wrapping box (`Fitted` = False) and keeps its origin, justification and formatted text. No new box is needed. |
| 2b. `<Br/>` around a field | Accepted and kept, but it only breaks lines around the whole field. It cannot split the property's value, so it is not a route. |
| 3. Two written plain text boxes | Not needed, because 2a works live. Not tested. |
| 4. Undo, save, reopen, sheets | One `UndoTransaction` reverted everything (size, width, `Fitted`, origin). The changes survived `SaveAs`, close and reopen. Each sheet wraps its own resolved value; the size and width are shared by every sheet. |

### 1. Smaller text on one field

Inside `Edit`, on the drawing's definition:

- **Finish Spec:** `WidthScale` 0.6, then `FormattedText` with `FontSize='0.13086'` (em 1.8, the floor), then `Origin` y = 17.9 + 1.3086 mm. Reference: Material Size, untouched, has its baseline at 17.89.
  - Its baseline came out at **17.95**, within 1 px of the reference.
  - The range box is 27.19 mm wide. The estimate, 70.53 × 1.8/2.8 × 0.6, is 27.20 mm.
  - Inventor stored the size as `0.1309`: it rounds `FontSize` to 4 decimal cm, which changes it by 0.0004 mm.
- **Description:** em 2.8 (`0.20356`) with `WidthScale` 0.6, origin y = 41.3 + 2.0356. Its baseline came out at **41.32** against the template's 41.26. It printed on one line, 77.8 mm wide, against an estimate of 77.84.
- **Control (Material):** em 1.8 with the origin left where it was. Its baseline rose to **18.63**, 0.73 mm high. That is exactly the change in cap height, 2.036 − 1.309, so the origin rule is needed and sufficient.
- **Order of calls:** setting `FormattedText` after `WidthScale` kept the scale at 0.6. It also left the origin unchanged.
- **`TextBox.FontSize`:** reading it returned nothing. Writing it failed with "The property 'FontSize' cannot be found on this object."
- **Readability:** the Finish value at 1.8 mm and 0.6 width is small but readable (`q1.png`).

### 2a. Description in a rectangle box

**Setup.** `Sketch.TextBoxes.AddByRectangle(corner1, corner2, formattedText, Missing)`, with the Description field's formatted text at a chosen size. The rectangle spans x 66.5 to 178.5 (112 mm wide). In the Q4 run, the existing box was instead given `Width = 11.2` cm, with the same result.

**Placement.**

- The box is top-justified (`kAlignTextUpper`). Line 1's cap top sits at the rectangle's top (the origin): 46.28 against 46.3 set.
- The line pitch is **1.2 × em**: 3.60 mm at em 3.0 and 4.09 mm at em 3.4. That is Inventor's `LineSpacing` 1 (`kMultipleLineSpacing`) with Tahoma.
- So line 2's baseline = origin − 0.727 em − 1.2 em.
- At em 3.4 with the origin at 46.3:
  - line 2's baseline is at 39.71, and its descenders reach 39.03, inside the 38 to 50 cell;
  - line 1 sits 1.3 mm below the DESCRIPTION label's baseline (47.6).
- At origin 46.8, line 1 touched the label (`q2a-4.png`), so the origin should stay at about 46.3.
- The largest em that fits two lines, from origin 46.3 down to ink at 38.5, is about 3.65. **Em 3.4 fits with margin.**

**Breaks against `PrintTextFit.Wrap`.** Four invented descriptions were tried at each of em 3.0 and em 3.4, at 112 mm. Each was set on the part, then `Drawing.Update`.

| Description | em 3.0: Inventor's line 1 ends | em 3.4: Inventor's line 1 ends |
|---|---|---|
| BRACKET, MOUNTING, LEFT HAND, … | "…HOLES AND", as Core (105.0 mm drawn against 105.5 estimated) | "…HOLES", as Core (111.4 against 111.9) |
| PLATE, ADAPTER, MOTOR MOUNT, … | "…HOLES", as Core | "…COUNTERBORED", as Core |
| SHAFT, IDLER, STEPPED, … | "…GROOVE AND", as Core | "…RING", as Core |
| Housing, gearbox, lower half, … (lower case) | "…boss and **bearing**". Core put "bearing" on line 2: its estimate for the line was 112.6 mm against a 112 mm limit, and the drawn line ended at 178.78, 0.28 mm past the rectangle. | "…drain boss", as Core |

- Core's estimate is never narrower than what Inventor draws, so a greedy wrap in Inventor fits at least as much on each line. If Core says two lines, Inventor gives at most two.
- The exact break can differ by one word near the limit. That is cosmetic. The add-in needs only the line count, not the break.

**`WidthScale` on a rectangle box** (em 2.8, `WidthScale` 0.6):

- Setting it was accepted.
- The wrap uses the scaled width: every test value printed on **one** line, 77.5 to 78.2 mm wide, which is 129.7 mm unscaled.
- So Core should wrap at `available / scale`.

**`RangeBox`** of a wrapping box reports the rectangle, as spike 218 found. It cannot be used to read the line count, so Core's estimate decides it.

**Shorter values.** A value that fits on one line prints on line 1, at the box's size (`q4-5-short.png`). When a value fits again, the fit step must set the single-line size and origin back. `Fitted` cannot be set back to True: it is read-only (spike 218). It is not needed either, since a wide box holding one line prints the same as a fitted box.

**Setting `Width` on a fitted box.** As a side test, `Width = 2.0` cm on the Material field box was accepted. `Fitted` became False and "Bronze, Ampco 21" wrapped into the row below. Width must therefore never be set on a single-line field.

### 2b. `<Br/>` around a field

- `FormattedText` was set to `LINE A<Br/><Property …Finish Spec…/><Br/>LINE C` on a fitted box. It was accepted, and on read-back Inventor had split it into three `StyleOverride` runs joined by `<Br/>`.
- It printed on three lines (`q2a-1.png`). `GetResultText` returned `'LINE AZINC PLATED, …MINLINE C'`, with no line breaks.
- A `<Br/>` can only sit outside the property, so it cannot break a long value in the middle.

### 3. Two written lines

- Not tested, because 2a keeps the value live.
- From spike 218, two plain text boxes in the definition would print the same text on every sheet, since the definition is shared, and they would lose the link to the property.
- Prompted entries are per sheet but take plain text only (spike 218).
- Either would be worse than 2a.

### 4. Undo, save and reopen, second sheet (the 2a route)

**Undo and redo.** In `TransactionManager.StartTransaction((_Document)dd, name)`, the script did one `Edit`, then changed `FormattedText` (em 3.4), `Width = 11.2`, `WidthScale` and `Origin`, then `ExitEdit(true)` and `End()`.

- One `UndoTransaction()` restored the box exactly: `Width` 30.911, `Fitted` True, `FontSize` 0.2908, origin (66.5, 44.208), one line (`q4-2-undone.png`).
- `RedoTransaction()` re-applied it.

**Save and reopen.** After `SaveAs`, close and reopen:

- `Width` was 112, `Fitted` False, `FontSize` 0.2472, origin (66.5, 46.3);
- it printed on the same two lines (`q4-3-reopened.png`).

**Live.** In the reopened drawing, the part's Description was changed and `Drawing.Update` called. The box re-wrapped at the new value's break ("…RING" / "GROOVE AND FLAT FOR SET SCREW", `q4-4-live.png`) without the fit step running.

**Second sheet.**

- A new sheet with no view: `GetResultText` returned '' and the box kept `Width` 112 and the size.
- With a view of a second part, Description "PIN, PIVOT": that sheet printed one line at the shared size (em 3.4), at line 1's position.
- Reactivating sheet 1 printed its own value on two lines again.
- So the wrap is per sheet. The size, width scale and origin are per drawing.

### Recommended approach for task 238

**Core: `TitleBlockFit`.** For each field cell it returns a size (em), a width scale, an origin y, and for Description a line count.

1. **One line at the cell's size.** Width scale = min(1, available / `WidthMm`). Done if the scale is at least 0.6.
2. **Otherwise, one line at 0.6.** Reduce the size until `WidthMm(text, size) × 0.6 ≤ available`, down to the floor (1.8 mm). The origin is at `UpperOriginYMm(rowBaseline, size)`. Single-line fields stop here and warn at the floor.
3. **Description only, two lines.** Use two lines before going below the two-line size, em 3.4 (Default; about 3.65 is the most that fits).
   - Scale down from 1 to 0.6: `Wrap(text, size, available / scale).Count ≤ 2`.
   - Then reduce the size to the floor, still on two lines.
   - Line 1's origin is fixed at 46.3 mm in block coordinates (a layout constant). Line 2's baseline is origin − 0.727 em − 1.2 em.
   - Warn when two lines at the floor and 0.6 still do not fit.
   - **Ordering for Aidan's spec:** whether the one-line 4 mm Description first shrinks toward 3.4 on one line before wrapping, or wraps straight away, is a layout choice. Put it in spec 05 as a Default.

**Add-in.** In one transaction:

1. For each sheet with a view, activate it, read `GetResultText` for each field, and keep the most severe result per field. "Most severe" means the smallest size, then the smallest scale.
2. `Edit` the definition once.
3. For each field box, set `FormattedText` (the `FontSize` regex replace), `WidthScale` and `Origin`. For Description, also set `Width` = the cell's value width (112 mm metric) when it is wrapping, and set it every time. Never set `Width` on single-line fields.
4. `ExitEdit(true)`.

Run it in Create Print and an Update command.

**Costs.**

- **One size per field for the whole drawing.**
  - A short Description on another sheet prints at the two-line size.
  - An alternative is to make the template's Description a 112 mm wrapping box from the start. Then single-line values need nothing but the size and origin reset.
- **When the step has not been re-run after a property change:**
  - Description re-wraps live at the old size, and a value grown to three lines runs below the cell until the step runs;
  - single-line fields run past their cell, as today.
- **A row loses its single size** when one value shrinks. Aidan accepted that in his answer to question 31, and the baseline stays on the row's baseline.
- **Rebuilding the template.**
  - The builder (task 237's `AddText`) does not need to change if the step sets `Width` at drawing time.
  - Making the template's Description a wrapping box from the start is optional. It would also need a 46.3 mm line-1 origin only when wrapping, so it is simpler to leave the builder alone and let the step set `Width`.
- **API members confirmed in this spike:**
  - `TextBox.Width` set (writable inside `Edit`);
  - `TextBox.WidthScale` on a non-fitted box;
  - `TextBoxes.AddByRectangle` (already known from spike 218);
  - `TransactionManager.RedoTransaction`.
- **API members not available:** `TextBox.FontSize` does not exist. Use the `StyleOverride FontSize` in the formatted text.

## Follow-ups

Things noticed but not done.

- **Spec 05, for Aidan (Defaults to log):**
  - the two-line Description size is em 3.4, with line 1's cap top at 46.3 mm (block coordinates, metric);
  - whether Description shrinks on one line before wrapping;
  - one size per field for the whole drawing; on a multi-part drawing, a short value on another sheet prints at that size.
- **Imperial template:** the cell geometry is the same in mm (the layout is mm everywhere), so the same constants should hold. Not run on `AWB Imperial.idw`.
- **Line-1 origin constant:** 46.3 mm came from these measurements (label baseline 47.6 minus 1.3). Task 238 should derive it in `PrintTemplateLayout` as the label baseline minus a gap; the origin is the cap top, so no cap height is subtracted. It should not hard-code 46.3.
- **`TextBox.Width` set on a single-line field wraps it.** Task 238 should guard this so only Description is ever given a width.
- **Not tried:** whether Inventor flags the drawing's edited title block definition as different from the template's (carried over from spike 218).
