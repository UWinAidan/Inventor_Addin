# 029: Part Properties window on the shared look

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (The three windows: Part Properties; Part number display)
- **Status:** todo
- **Depends on:** 022, 027
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 025, 028 (provided neither edits `UI/Theme/` or `UI/Controls/`)

## Goal

The Part Properties window uses the shared look and the spec's three sections, shows read-only values as text, says "Not assigned" and "Not set" instead of showing a file name or a blank, and has no empty gap above the buttons (follow-up from Aidan's test).

## Scope

- `src/InventorAddin/UI/PartPropertiesWindow.xaml` and `.xaml.cs`
- `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs` and its tests: only to remove `FileName` and `PartName` if nothing binds to them any more (see Notes)

Out of scope:

- The shared styles and pieces (`UI/Theme/`, `UI/Controls/`). If something is missing, stop and record it.
- What is read or written. The part number rule is display only.

## Acceptance criteria

- [ ] `DialogHeader` with title `HeaderTitle` and subline `HeaderSubline` (022)
- [ ] Section **Identity**
  - Part number: `PartNumberDisplay` in `SelectableIdentifierText` (026, so it can be copied) when `IsPartNumberAssigned`, else in `PlaceholderText`
  - Part _type: the dropdown in `CodeComboBox` style, with the full name of the selected code (`PartTypeFullName`) in `SideNoteText` (027) beside it. The closed box shows the code; the entries show "code: full name"; the tooltip stays (task 019's item template and tooltip trigger are kept)
- [ ] Section **People**: _Designer and D_etailer text boxes, as now
- [ ] Section **Cost and make-up**
  - _Cost: text box in `NarrowTextBox` style
  - Material: `MaterialDisplay` as `ValueText` when `IsMaterialSet`, else `PlaceholderText`
  - Finish: the same with `FinishDisplay` and `IsFinishSet`
  - Weight: `Weight` as `ValueText`
- [ ] Read-only values are text, not boxes, and are not Tab stops
- [ ] `DialogFooter`: `StatusText`, in the error style when `HasError`; _Apply (`ApplyCommand`), OK (`PrimaryButton`, `IsDefault`, `OkCommand`), Cancel (`IsCancel`). The status line and the buttons share one row; when the status line is empty there is no empty band between the last row and the buttons
- [ ] Editable controls still bind `IsEnabled` to `IsEditable`; text boxes still use `UpdateSourceTrigger=PropertyChanged`
- [ ] The window XAML contains layout and bindings only: no colour, font size, font weight, margin, padding or width/height values, and no `StaticResource` to shared styles. The fixed width of 420 is removed; the window sizes to its content
- [ ] Code-behind still only sets the title, the `DataContext` and the close wiring
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists both window files as not compiled

## Notes for the implementer

- The read-only values were `TextBox`es with `IsReadOnly` so they could be copied. Now the part number uses `SelectableIdentifierText` (026) and Material, Finish and Weight are `TextBlock`s in `ValueText` or `PlaceholderText`.
- A long status message must wrap within the left part of the footer and must not push the buttons off the window.
- The part type row has the narrow dropdown and the full name side by side: a `StackPanel` with `Orientation="Horizontal"` inside the `FieldRow`, the gap coming from `SideNoteText`.
- After this task nothing binds to `FileName` or `PartName`. Remove them and their tests only if no Core code uses them; `PartName` the class (`PartName.FromFileName`) stays.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:** none expected (no Inventor API used)
- **Manual checklist for Inventor:**
  1. Light theme, then dark theme: open Part Properties on a saved part. Header shows the part name, and "Part · <file name>.ipt" under it. Three sections with headings and divider lines. Values that cannot be changed are plain text; boxes only where you can type.
  2. A part with no part number set: Part number shows **Not assigned** in placeholder colour. Set a Part Number in Inventor's own iProperties dialog (for example `TEST-001`), reopen: it shows `TEST-001` in the identifier font.
  3. Type the file name itself as the Part Number in Inventor's dialog (different case, for example all capitals): the window still shows Not assigned.
  4. A part with no material or finish shows **Not set** in placeholder colour for each; a part with a material shows it as normal text.
  5. Part type: the narrow dropdown shows the code, the full name sits beside it in muted text, the open list shows "code: full name", the blank entry can be clicked and clears the full name.
  6. Assembly: the subline reads "Assembly · <file name>.iam". Weldment: "Weldment · ...". Sheet metal part: "Sheet metal part · ...".
  7. A new, never-saved part: title "New part", subline "Part · Not saved yet". **Record what the Part number row shows** (blank gives Not assigned; anything else is shown as is, for example `Part1`). Spec 08 open question 5 needs this.
  8. No empty band between Weight and the buttons when the status line is empty; with a long error (cost `abc`), the message wraps and the buttons stay in place.
  9. Keyboard: Tab goes Part type, Designer, Detailer, Cost, Apply, OK, Cancel, with the accent focus outline. Alt+T, Alt+D, Alt+E, Alt+C, Alt+A work. Enter is OK, Esc is Cancel.
  10. Read-only file (as task 019 step 14): every box and the dropdown are disabled and still read clearly in both themes; the status line says why.
  11. Apply and Undo still work as before (task 019 steps 6 and 7).

## Follow-ups

Things noticed but not done.
