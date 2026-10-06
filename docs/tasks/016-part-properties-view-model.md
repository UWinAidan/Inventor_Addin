# 016: Part properties view-model (Core)

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Rules; The window; Open questions 4 and 5)
- **Status:** todo
- **Depends on:** 012, 014
- **Needs:** cloud
- **Parallel-safe with:** 011, 015, 017, 018

## Goal

Everything the Part Properties window does, apart from drawing itself and talking to Inventor, lives in a tested Core view-model: the fields, the pre-fills, Detailer following Designer, validation, dirty tracking, Apply/OK and the status line.

## Scope

- `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs` (new)
- `src/InventorAddin.Core/PartProperties/IPropertyWriteTarget.cs` (new)
- `tests/InventorAddin.Core.Tests/ViewModels/PartPropertiesViewModelTests.cs` (new)

Out of scope:

- The WPF window and the command (019). Reading and writing Inventor (018).
- A "Generate part number", Rename or Change button. The spec says no placeholders.

## Acceptance criteria

Construction and display

- [ ] Built from a `PartPropertiesSnapshot` (014), an `IPropertyWriteTarget`, the default designer string, a `CultureInfo` for cost, and an optional `ILog` (default `NullLog.Instance`, as `SettingsViewModel` does)
- [ ] `IPropertyWriteTarget` has one method, `void Apply(IReadOnlyList<PropertyWrite> writes)`, which throws on failure. The add-in implements it in 019
- [ ] Read-only display properties: `FileName` (header: the file name with extension, or "Not saved yet" for a never-saved file), `PartNumber`, `PartName` (`PartName.FromFileName`), `Material`, `Finish`, `Weight` (`WeightDisplay`, or `"-"` when null). Blank values show as empty strings
- [ ] Editable properties: `PartType` (a code string), `Designer`, `Detailer`, `CostText`. Each raises `PropertyChanged` and re-evaluates state
- [ ] `PartTypeOptions`: a blank entry followed by `PartTypes.All`. If the file holds a code not in the list, that code is added as an extra option so the window can show it unchanged
- [ ] `PartTypeFullName`: the full name of the current code (`PartTypes.FullNameFor`), for the field's tooltip; updates when the code changes

Pre-fills (spec rules; open question 5 default)

- [ ] Blank Designer in the file is pre-filled with the default designer (trimmed; nothing when it is blank)
- [ ] Blank Part Type in the file is pre-filled with `PartTypes.DefaultFor(kind)`
- [ ] Blank Detailer in the file starts following Designer: it shows the Designer (after the pre-fill) and changes with it. Setting `Detailer` to anything other than the current Designer stops following for the rest of the window's life. A file with a Detailer never follows
- [ ] Pre-fills, the followed Detailer and the Description sync count as pending changes: when any exists at open, `HasChanges` is true, Apply is enabled and the status line reads "Pre-filled values will be written when you apply."

State and commands

- [ ] `HasChanges` is true when `PartPropertiesWritePlan.Build` for the current values returns any write. Editing a field back to its original value makes it false again
- [ ] `ValidationMessage`: the first error from `PropertyValues.ValidateText` (Designer, Detailer) or `TryParseCost`, or null. `IsValid` follows it
- [ ] `ApplyCommand` is enabled only when `HasChanges && IsValid && IsEditable`. It builds the plan, calls the target once with the whole list, then treats the written values as the new originals (so Apply disables again) and sets the status line to "Saved to the file. Save the document to keep the changes." The cost field is reformatted with `FormatCost` after a successful apply
- [ ] When the target throws, nothing is rebased, the view-model logs `ERROR` with the exception, and the status line shows "Could not write the properties: <message>". The window stays open
- [ ] `OkCommand` is enabled when `IsValid` (or the file is not editable). It applies first when there are changes; if that apply fails it does not close. Otherwise it raises `CloseRequested`
- [ ] An empty plan never calls the target (OK on an unchanged file writes nothing)
- [ ] `IsEditable` is false when the snapshot's `EditBlock` is not `None`. Then every editable field reports `IsEditable = false` for binding, Apply is disabled, no pre-fill counts as a change, OK only closes, and the status line shows `EditBlockMessage`
- [ ] `StatusText` priority: edit block message, then validation message, then the last apply result, then the pre-fill notice, else empty. `HasError` is true for a validation or write error, for red text
- [ ] Tests cover each criterion above with a fake `IPropertyWriteTarget` that records calls and can be told to throw. Include: a part and an assembly and a weldment with blank type; Detailer following through several Designer edits and stopping; a file with every field already matching (no pending changes, OK writes nothing); invalid cost disables Apply and OK; an apply failure; a read-only snapshot; an unknown part type code kept as is
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Base it on `ObservableObject` and `RelayCommand` as `SettingsViewModel` does. Call `RaiseCanExecuteChanged` whenever state changes.
- The view-model holds the snapshot as the original and builds `PartEditedValues` from its fields each time it needs a plan. Rebasing after Apply means building a new snapshot from the old one with the written values (records with `with`).
- Cancel needs no command: the window's Cancel button uses `IsCancel` and nothing is written, as in the Settings window.
- Log `INFO` after a successful apply with the number of writes and the property names, never the values (they may hold personal data in future fields).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (covered by 019)

## Follow-ups

Things noticed but not done.
