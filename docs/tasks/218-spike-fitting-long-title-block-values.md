# 218: Spike: fitting long title block values

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** done (spike)
- **Depends on:** 187
- **Needs:** **windows** (spike, parent with Inventor)
- **Parallel-safe with:** any

## Goal

Title block values (Finish Spec, Description, Material) can be longer than their cells. Aidan asked for a safety net: shrink a too-long value to fit. Find out whether Inventor can fit or wrap title block field text, and report the options before anything is built.

## Scope

- In a scratch copy of the template:
  - a field text box made with `TextBoxes.AddByRectangle` (does it wrap a long property value?);
  - `TextBox.WidthScale` or a `<StyleOverride>` width factor on a field (does it compress?);
  - `FitTextToBox` or similar members, if the interop has any.
- Look at what a drawing-time step could do instead: Create Print or an Update measuring `GetResultText` with the task 215 width estimate and setting a per-drawing override.
- Record each result under Results and list the options for Aidan. Then the parent writes the build task.

## Acceptance criteria

- [x] Results list what Inventor can and cannot do, and the options with their costs.

## Verification

Filled in by the implementer.

- **Ran:** spike scripts in Inventor 2026 over late-bound COM (PowerShell 5.1), on a copy of `AWB Metric.idw` and a copy of the test part in `C:\AwbSpike\218\`. No repo file other than this one changed.
- **Not compiled (changed under `src/InventorAddin`):** none.
- **Inventor API members not confirmed:** none. Every member named in Results was called, and its result read back.
- **Manual checklist for Inventor:** none for this spike.

## Results

**Method.** As in spikes 183 and 225: PowerShell 5.1 over COM, late bound, with `SilentOperation` on while the scripts ran.

- The scripts and screenshots are in the session's job folder (`tmp\218\`, not in the repo).
- Each run made a fresh drawing from `C:\AwbSpike\218\AWB Metric 218.idw` (a copy of the template) and placed a base view of a copy of the test part.
- On the part copy, Description was set to an 84-character value and Finish Spec to a 47-character one. Both run well past their cells: 185 mm against a 113 mm cell, and 71 mm against 43 mm.
- Each experiment edited **that drawing's own copy** of the title block definition (`TitleBlockDefinition.Edit`, then `ExitEdit(true)`). The templates in `libraries/Templates/` were not opened.

### What Inventor can do

| Approach | Result |
|---|---|
| `TextBox.WidthScale` | **Works.** It compresses the text horizontally only. The height and the baseline stay where they were. |
| `TextBoxes.AddByRectangle` | Wraps, but never shrinks. A third line runs out of the cell. |
| `<StyleOverride WidthScale='…'>` or `WidthFactor='…'` | Accepted, then dropped silently. No effect. |
| A copied `TextStyle` with `WidthScale` 0.6, set as `TextBox.Style` | The text did not get narrower, and the style's own justification and size replaced the box's. |
| A fit, wrap or shrink member | None exists. Reflection on the interop found only `Fitted`, `FittedTextWidth` and `FittedTextHeight`, which are read-only. |
| A prompted entry | Works for each title block. `SetPromptResultText` writes plain text only. |

**`TextBox.WidthScale`**

- Set on a field text box inside `Edit`, a scale of 0.6 printed Description and Finish Spec at 0.6 of their width. Description went from 185.3 mm to 111.1 mm.
- The text stays at full height. Its baseline did not move: the pixel check matched Material's within 0.1 mm.
- The text is still readable at 0.6 (`r2-widthscale.png`).
- **Setting it outside `Edit` fails** with E_FAIL.

**`TextBoxes.AddByRectangle`**

- A field box made with `AddByRectangle(corner1, corner2, formattedText, Missing)` at the cell's width wraps the resolved value at spaces. Description took 2 lines, and so did Finish Spec.
- The box comes back `Fitted` = false and top-justified (`kAlignTextUpper`).
- The font size does not change, so the second line runs down past the rectangle. Finish Spec's second line crossed into the DESIGNER row.
- `RangeBox` reports the rectangle, not the text, so it cannot detect the overflow.
- Wrapping only helps in a cell that has room for two lines at a smaller size.

**`StyleOverride` attributes.** `<StyleOverride … WidthScale='0.6'>` and `WidthFactor='0.6'` were accepted on write. They were missing from `FormattedText` on read-back and had no effect.

**Text style.** A copied `TextStyle` with `WidthScale` 0.6, set as `TextBox.Style`, did not narrow the text, and it changed the box's justification and size. It is not a route.

**Prompted entry**

- A definition text `<Prompt>…</Prompt>`, added with `AddFitted` inside `Edit`, gets a `ReadOnlyUniqueID`.
- On the placed title block, `TitleBlock.SetPromptResultText(defTextBox, value)` sets the value, and `GetResultText` reads it back.
- The value is plain text only. A `<StyleOverride FontSize=…>` passed in printed literally, tags and all.
- Using a prompt would replace the live Finish Spec field with text that something must keep filled. A copy that goes stale is worse than a long value.

### Measuring at drawing time is exact

- In a drawing whose sheet has a view, `TextBox.RangeBox` of the **definition's** field text box (from `TitleBlock.Definition.Sketch.TextBoxes`) measures the **resolved value**, in the title block's own coordinates. Task 223 saw the property name only because the template had no model.
- The range is read for the **active sheet's** model. On a second sheet with no view, the range was empty.
- Core's `PrintTextFit.WidthMm` matched the drawn width within 0.1 % on five values. Description estimated 185.40 mm against 185.32 mm drawn, Finish 70.56 mm against 70.53 mm, and three short values matched as closely.
- So the step can measure with Core's estimate on `GetResultText`, which can be tested in Core, or with `RangeBox`.

### Undo, save and sheets

- **Undo.** `Edit`, `WidthScale` and `ExitEdit` inside `TransactionManager.StartTransaction(doc, name)` … `End()` were all reverted by one `UndoTransaction()`.
- **Save.** The scale survived `SaveAs`, close and reopen.
- **Sheets.** Every sheet of the drawing shares the one definition, so the scale applies to all of them. A new sheet's title block showed WidthScale 0.6.

### Baselines (task 223's follow-up)

**The row drift comes from descenders, not from size.**

- With `kAlignTextLower`, which the builder uses, Inventor puts the **bottom of the actual letters** at the origin. The range box's bottom equals the origin for every value, and its height changes with the text.
- A value with descenders (letters that hang below the line, such as p and g, and commas) is lifted by their depth. Measured on the screenshot pixels at 2.8 mm:
  - "Bronze, Ampco 21" (p, comma) and the Finish value (commas) sit 0.47 mm above "25 X 50 mm".
  - "4.33 kg" (g) sits 0.46 mm above "2026-11-23" and "AidanwBrown".
- This is the WEIGHT vs DATE and MATERIAL vs MATERIAL SIZE drift from task 223. It depends on the value, so no fixed offset in the layout can remove it.

**`kAlignTextUpper` gives one baseline.**

- The three Material row values were placed with upper justification at one origin y. All three baselines landed on the same pixel row (within 0.1 mm), with or without descenders.
- The tops of capitals, digits and lowercase ascenders are within one pixel of each other in Tahoma.

**`kAlignTextBaseline` is refused.**

- Setting `TextBox.VerticalJustification` to it fails with E_FAIL.
- Setting `TextStyle.VerticalJustification` to it fails with "Value does not fall within the expected range".

**A smaller font size on one field.** Finish Spec's FontSize was changed from 0.2036 cm to 0.13 cm, with its origin unchanged.

- With lower justification, the baseline moves only by the change in descender depth. Here that was about 0.1 mm, from the commas, and it is 0 for a value without descenders.
- With upper justification, the baseline sits a cap height below the top. A smaller size therefore **raises** the baseline, unless the origin moves down by the change in cap height (`0.727 × Δsize`).
- Either way the field needs a correction, and it looks visibly smaller than its neighbours.

**`WidthScale` does not move the baseline under either justification.** The height is unchanged, so the row stays at one size, as task 223 requires.

### Options for Aidan

1. **Set `WidthScale` at drawing time (recommended).**
   - **How it works:** Create Print and an Update command do this for each field cell (Description, Material, Material Size, Finish Spec and the others):
     - read `GetResultText`;
     - estimate the width with `PrintTextFit.WidthMm` at the cell's size;
     - in one transaction, set `WidthScale = min(1, available / width)` on the drawing's definition text box.
   - A value that fits again resets the scale to 1.
   - The decision and the floor live in Core. The floor is the narrowest scale allowed, for example 0.6; below it the command warns instead.
   - **Costs:**
     - The step must run again after a property changes. A stale scale only squashes or under-fills; it never hides text.
     - One scale serves every sheet in the drawing. With different parts on different sheets, it takes the smallest scale any sheet needs.
     - It edits the drawing's local copy of the definition. Inventor might then report that copy as different from the template. This was not seen, and not tried.
2. **Wrap with `AddByRectangle`.**
   - **How it works:** the builder makes the Description value a rectangle box, with a Description cell laid out for two lines (a smaller size or a taller cell).
   - **Costs:**
     - It never shrinks, so a value longer than two lines still overflows.
     - The cell must be redesigned.
     - `RangeBox` cannot detect the overflow.
     - It does not help the 8 mm cells in the Material row.
3. **Use a smaller font for one field at drawing time.**
   - **How it works:** the same step as option 1, changing FontSize instead of the width.
   - **Costs:** the row stops being one size, which task 223 rules out. The baseline also needs a correction, which depends on the justification.
4. **A prompted entry, or a shortened value written to the part's property.**
   - **Costs:** a prompt loses the live link to the property, and a shortened value changes the model's data to suit the print. `SetPromptResultText` also takes plain text only.
   - Not recommended.

## Follow-ups

Things noticed but not done.

- **Recommended build task (option 1).**
  - **Core:** a `TitleBlockFit` decision. It takes the cell width, size, bold flag and resolved text, and returns a width scale. The floor defaults to 0.6, logged as "Default" for Aidan to confirm; below the floor it returns a warning. Covered by tests.
  - **Add-in:** a step at the end of Create Print and in an Update command, in one transaction:
    1. For each sheet with a view, activate the sheet and read `GetResultText` for each field text box. Keep the smallest scale for each box.
    2. `Edit` the drawing's title block definition once, set `WidthScale` on the field boxes, then call `ExitEdit(true)`.
- **Baseline fix for task 223.**
  - The builder should place field values, and probably all title block text, with `kAlignTextUpper`, at origin y = baseline + `PrintTextFit.CapHeightMm(size)`.
  - This replaces lower justification and the range-box shift, and removes the WEIGHT vs DATE and MATERIAL vs MATERIAL SIZE drift for any value.
  - It changes `PrintTemplateBuilder.AddText`, so it needs its own task, and both templates need a rebuild afterwards.
- **Questions for spec 05:**
  - What is the narrowest width scale Aidan accepts?
  - Below it, should the command only warn, or also wrap Description onto two lines?
- **Not tried:** whether Inventor flags the drawing's edited title block definition as different from the template's, or offers to replace it.
