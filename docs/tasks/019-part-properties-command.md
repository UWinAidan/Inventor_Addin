# 019: Part Properties command, window and ribbon button

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (The window; Where it appears; Notes for planning)
- **Status:** todo
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

- [ ] `CommandNames.PartProperties` is built from the prefix like the others (`Awb_PartProperties`)
- [ ] The CAD Automation panel lists a small Part Properties button first, before Settings, on the Part and Assembly ribbons only. Not on Drawing, not on ZeroDoc. Layout tests updated, including the panel order on each ribbon

Command

- [ ] `PartPropertiesCommand : DocumentEditCommand`, display name "Part Properties", command type `kFilePropertyEditCmdType`, `CanRunOn` accepts parts and assemblies (`IsPart()` or `IsAssembly()`) with the message "Part Properties works on parts and assemblies." otherwise
- [ ] It overrides `Run` (017): reads the snapshot with `PartPropertiesReader` (018), builds the view-model (016) with `AddinServices.Settings.DefaultDesigner`, `CultureInfo.CurrentCulture` and `AddinServices.Log`, and shows the window through `WindowHost.ShowDialog`
- [ ] Its `IPropertyWriteTarget` calls `RunInTransaction(doc, () => PropertyWriter.Apply(doc, writes))`, so each Apply is one transaction named "Part Properties" and a failure aborts it
- [ ] `Execute(Document)` throws `NotSupportedException` as 017 describes
- [ ] It acts on `InventorHost.ActiveEditDocument` (what the base already passes), so a part edited in place inside an assembly opens that part

Window

- [ ] Title "Part Properties"; a header line with `FileName`
- [ ] Fields in the spec's order: Part number, Part name, Part type, Designer, Detailer, Cost, Material, Finish, Weight. Read-only fields are read-only text boxes with a grey background (text can be selected and copied); editable ones are normal
- [ ] Part type is a non-editable `ComboBox` over `PartTypeOptions`: the closed box shows the code, each dropdown entry shows `DisplayText`, and the tooltip is `PartTypeFullName`
- [ ] Editable controls bind `IsEnabled` to `IsEditable`; text boxes use `UpdateSourceTrigger=PropertyChanged`
- [ ] Buttons Apply (`ApplyCommand`), OK (`OkCommand`, `IsDefault`) and Cancel (`IsCancel`). The window closes on `CloseRequested`
- [ ] A status line bound to `StatusText`, red when `HasError`, using the same `DataTrigger` pattern as the Settings window
- [ ] Code-behind only sets the `DataContext`, the title and the close wiring
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- Follow `SettingsWindow` and `SettingsCommand` for the window, `WindowHost` and how the title is set in code-behind.
- `CommandTypesEnum.kFilePropertyEditCmdType` is named in `DocumentEditCommand`'s comment but not used yet. Confirm it.
- The checklist below must cover the spec's unconfirmed API behaviour. Keep its numbered steps and add to them; do not drop any.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
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

## Follow-ups

Things noticed but not done.
