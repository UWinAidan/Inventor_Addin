# 019: Part Properties command, window and ribbon button

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (The window; Where it appears; Notes for planning)
- **Status:** done (add-in code not compiled)
- **Depends on:** 015, 016, 017, 018
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** none

## Goal

A Part Properties button on part and assembly ribbons opens the window for the document being edited, and Apply/OK write the changed iProperties in one undoable step each. This is the first command that edits a document, so its checklist is also the Inventor test of the transaction base (tasks 006 and 017).

## Scope

- `src/InventorAddin.Core/Ribbon/RibbonModels.cs` (`CommandNames.PartProperties`)
- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs` (the button)
- `tests/InventorAddin.Core.Tests/Ribbon/RibbonLayoutTests.cs` (update)
- `src/InventorAddin/Commands/PartPropertiesCommand.cs` (new)
- `src/InventorAddin/UI/PartPropertiesWindow.xaml` and `.xaml.cs` (new)
- `src/InventorAddin/UI/RibbonSetup.cs` (register the command)

Out of scope:

- "Generate part number", Rename and Change buttons (spec: no placeholders).
- Icons.

## Acceptance criteria

Core

- [x] `CommandNames.PartProperties` is built from the prefix like the others (`Awb_PartProperties`)
- [x] The CAD Automation panel lists a small Part Properties button first, before Settings, on the Part and Assembly ribbons only. Not on Drawing, not on ZeroDoc. Layout tests updated, including the panel order on each ribbon

Command

- [x] `PartPropertiesCommand : DocumentEditCommand`, display name "Part Properties", command type `kFilePropertyEditCmdType`, `CanRunOn` accepts parts and assemblies (`IsPart()` or `IsAssembly()`) with the message "Part Properties works on parts and assemblies." otherwise
- [x] It overrides `Run` (017): reads the snapshot with `PartPropertiesReader` (018), builds the view-model (016) with `AddinServices.Settings.DefaultDesigner`, `CultureInfo.CurrentCulture` and `AddinServices.Log`, and shows the window through `WindowHost.ShowDialog`
- [x] Its `IPropertyWriteTarget` calls `RunInTransaction(doc, () => PropertyWriter.Apply(doc, writes))`, so each Apply is one transaction named "Part Properties" and a failure aborts it
- [x] `Execute(Document)` throws `NotSupportedException` as 017 describes
- [x] It acts on `InventorHost.ActiveEditDocument` (what the base already passes), so a part edited in place inside an assembly opens that part

Window

- [x] Title "Part Properties"; a header line with `FileName`
- [x] Fields in the spec's order: Part number, Part name, Part type, Designer, Detailer, Cost, Material, Finish, Weight. Read-only fields are read-only text boxes with a grey background (text can be selected and copied); editable ones are normal
- [x] Part type is a non-editable `ComboBox` over `PartTypeOptions`: the closed box shows the code, each dropdown entry shows `DisplayText`, and the tooltip is `PartTypeFullName`
- [x] Editable controls bind `IsEnabled` to `IsEditable`; text boxes use `UpdateSourceTrigger=PropertyChanged`
- [x] Buttons Apply (`ApplyCommand`), OK (`OkCommand`, `IsDefault`) and Cancel (`IsCancel`). The window closes on `CloseRequested`
- [x] A status line bound to `StatusText`, red when `HasError`, using the same `DataTrigger` pattern as the Settings window
- [x] Code-behind only sets the `DataContext`, the title and the close wiring
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- Follow `SettingsWindow` and `SettingsCommand` for the window, `WindowHost` and how the title is set in code-behind.
- `CommandTypesEnum.kFilePropertyEditCmdType` is named in `DocumentEditCommand`'s comment but not used yet. Confirm it.
- The checklist below must cover the spec's unconfirmed API behaviour. Keep its numbered steps and add to them; do not drop any.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 557 passed, 0 failed, 0 skipped. Cloud session (`CLAUDE_CODE_REMOTE=true`), so `src/InventorAddin` was not built.
- **Not compiled (changed under `src/InventorAddin`, cloud session):**
  - `src/InventorAddin/Commands/PartPropertiesCommand.cs` (new)
  - `src/InventorAddin/UI/PartPropertiesWindow.xaml` (new; the XAML has not been through the markup compiler either)
  - `src/InventorAddin/UI/PartPropertiesWindow.xaml.cs` (new)
  - `src/InventorAddin/UI/RibbonSetup.cs` (registers `PartPropertiesCommand`)
- **Inventor API members not confirmed:**
  - (Reviewer confirmed `CommandTypesEnum.kFilePropertyEditCmdType` in the Inventor API reference: value 8, "Commands that edit File Properties".)
  - Carried over from 018, first exercised by this command: `Document.IsModifiable`, `Property.Delete()`, `PropertySet.Add(value, name)`, and setting the standard Cost `Value` to a boxed `decimal` (checklist steps 6, 8, 9, 15).
  - Everything else the command uses is existing add-in code (`DocumentEditCommand.RunInTransaction`, `InventorHost.ActiveEditDocument`, `WindowHost.ShowDialog`, `PartPropertiesReader`, `PropertyWriter`, `DocumentKinds`).
- **Manual checklist for Inventor:**
  1. Build with Inventor closed. In Settings, set Default designer to a test name and save.
  2. Ribbons: Part and Assembly show Part Properties first on CAD Automation. Drawing does not. No document open: no button.
  3. New part, saved as `Test Bracket.ipt`, with nothing filled in. Open Part Properties. Header shows the file name. Part number shows what Inventor holds; **note whether it is the file name** (spec 02 needs to know). Part name reads "Test Bracket". Designer shows the default, Detailer follows it, Part type is blank, Weight shows the mass in the document's units. Status line says pre-filled values will be written and Apply is enabled.
  4. Change Designer: Detailer follows. Type a different Detailer, then change Designer again: Detailer stays.
  5. Pick part type M. The box shows "M", the dropdown shows "M: Manufactured", the tooltip shows "Manufactured".
  6. Type `12.50` as cost and click Apply. Inventor's own iProperties: Description "Test Bracket", Designer, Estimated Cost 12.50, and on the Custom tab `Part Type` M and `Detailer`. **Note whether the cost was written correctly** (currency type question).
  7. Edit > Undo once: all of step 6 is reverted together. Redo.
  8. Clear Detailer and Part type, Apply. The `Detailer` and `Part Type` custom properties are **deleted** from the Custom tab, not left empty.
  9. Clear cost, Apply: Estimated Cost shows 0, and reopening the window shows a blank cost.
  10. Save and close the part, reopen it, open the window, click OK without changing anything. The document is not marked modified (no asterisk, no save prompt on close).
  11. Invalid input: cost `-1`, then `abc`. Apply and OK are disabled and the status line says why.
  12. Assembly with blank type: Part type pre-fills A. Weldment: W. Both can be changed to P.
  13. In-place edit: in an assembly, edit a part in place, open Part Properties. The header shows the part's file. Change Designer and Apply. Leave in-place edit and do one Undo from the assembly: **record whether that Undo reverts the change** (task 006 follow-up). If it does not, record it under Follow-ups; do not change the command.
  14. Read-only file: close a saved part, set its file read-only in Explorer, open it, open Part Properties. Every field is disabled and the status line says the file is read-only. OK closes.
  15. A library or otherwise non-modifiable document, if one is available: same as 14 with the "cannot be modified" line. Record whether `IsModifiable` reported it.
  16. Cancel after editing writes nothing. Check the log for one `INFO` line per apply naming the properties written.
  17. Undo list: after an Apply, Inventor's Undo entry reads "Part Properties". Two Applies in one window session are two Undo steps.
  18. Part type dropdown: the blank entry at the top is visible and can be clicked (it has no text). Picking it clears the code and the tooltip disappears (no empty tooltip box). The closed box shows only the code for every entry. Check the Output window or log for nothing alarming; the template's `ComboBoxItem` lookup can log a harmless WPF binding message for the closed box.
  19. Part type in a different case: in Inventor's own iProperties, set the custom `Part Type` to `m`. Open Part Properties: the box shows `M`, the status line shows the pre-fill notice, Apply writes `M`.
  20. Never-saved part (new, not saved): the header shows "Not saved yet", Part name is blank. Apply still writes Designer and Detailer.
  21. Sheet metal part and weldment both open the window (they are parts and assemblies). A presentation file, if one is available, shows "Part Properties works on parts and assemblies." (the button should not be on that ribbon at all; record it if it is).
  22. Keyboard: Alt+T, Alt+D, Alt+E, Alt+C move to Part type, Designer, Detailer and Cost; Alt+A applies; Enter is OK and Esc is Cancel. Tab skips the grey read-only fields.
  23. Read-only fields: in Part number, Part name, Material, Finish and Weight, text can be selected with the mouse and copied with Ctrl+C, and cannot be changed. They look grey; the editable fields look normal.

## Follow-ups

Things noticed but not done.

- Step 16 expects one `INFO` line per apply. The view-model writes that line (`Part properties written (N): ...`), but `PropertyWriter` (018) also logs one `INFO` line per write (`Property write: <Action> <set>/<name>`), so an apply produces 1 + N lines. 018's follow-up already suggests moving the writer's line to the failure path; decide that with this checklist's result.
- Decisions made inside the brief, for the reviewer:
  - The Part type closed box shows the code through one `ItemTemplate` with a `DataTrigger` on `RelativeSource AncestorType=ComboBoxItem` being null (true only in the closed box). This keeps the code-behind to DataContext, title and close wiring, at the cost of a possible harmless binding trace message (checklist step 18). A `DataTemplateSelector` would avoid that but adds code outside the three allowed concerns.
  - The tooltip is cleared for a blank code so WPF does not show an empty tooltip box.
  - Read-only text boxes have `IsTabStop=False` so Tab moves between editable fields only. They can still be clicked, selected and copied.
  - The write target is a private nested class holding the command and the document, calling the command's protected `RunInTransaction`.
- Still open from 016's review: `PartPropertiesViewModel.Ok()` has no guard for invalid values when called from code. Not reachable from the window (WPF checks `CanExecute`).
