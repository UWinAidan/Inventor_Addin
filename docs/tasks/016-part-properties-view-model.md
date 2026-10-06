# 016: Part properties view-model (Core)

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Rules; The window; Open questions 4 and 5)
- **Status:** done
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

- [x] Built from a `PartPropertiesSnapshot` (014), an `IPropertyWriteTarget`, the default designer string, a `CultureInfo` for cost, and an optional `ILog` (default `NullLog.Instance`, as `SettingsViewModel` does)
- [x] `IPropertyWriteTarget` has one method, `void Apply(IReadOnlyList<PropertyWrite> writes)`, which throws on failure. The add-in implements it in 019
- [x] Read-only display properties: `FileName` (header: the file name with extension, or "Not saved yet" for a never-saved file), `PartNumber`, `PartName` (`PartName.FromFileName`), `Material`, `Finish`, `Weight` (`WeightDisplay`, or `"-"` when null). Blank values show as empty strings
- [x] Editable properties: `PartType` (a code string), `Designer`, `Detailer`, `CostText`. Each raises `PropertyChanged` and re-evaluates state
- [x] `PartTypeOptions`: a blank entry followed by `PartTypes.All`. If the file holds a code not in the list, that code is added as an extra option so the window can show it unchanged
- [x] `PartTypeFullName`: the full name of the current code (`PartTypes.FullNameFor`), for the field's tooltip; updates when the code changes

Pre-fills (spec rules; open question 5 default)

- [x] Blank Designer in the file is pre-filled with the default designer (trimmed; nothing when it is blank)
- [x] Blank Part Type in the file is pre-filled with `PartTypes.DefaultFor(kind)`
- [x] Blank Detailer in the file starts following Designer: it shows the Designer (after the pre-fill) and changes with it. Setting `Detailer` to anything other than the current Designer stops following for the rest of the window's life. A file with a Detailer never follows
- [x] Pre-fills, the followed Detailer and the Description sync count as pending changes: when any exists at open, `HasChanges` is true, Apply is enabled and the status line reads "Pre-filled values will be written when you apply."

State and commands

- [x] `HasChanges` is true when `PartPropertiesWritePlan.Build` for the current values returns any write. Editing a field back to its original value makes it false again
- [x] `ValidationMessage`: the first error from `PropertyValues.ValidateText` (Designer, Detailer) or `TryParseCost`, or null. `IsValid` follows it
- [x] `ApplyCommand` is enabled only when `HasChanges && IsValid && IsEditable`. It builds the plan, calls the target once with the whole list, then treats the written values as the new originals (so Apply disables again) and sets the status line to "Saved to the file. Save the document to keep the changes." The cost field is reformatted with `FormatCost` after a successful apply
- [x] When the target throws, nothing is rebased, the view-model logs `ERROR` with the exception, and the status line shows "Could not write the properties: <message>". The window stays open
- [x] `OkCommand` is enabled when `IsValid` (or the file is not editable). It applies first when there are changes; if that apply fails it does not close. Otherwise it raises `CloseRequested`
- [x] An empty plan never calls the target (OK on an unchanged file writes nothing)
- [x] `IsEditable` is false when the snapshot's `EditBlock` is not `None`. Then every editable field reports `IsEditable = false` for binding, Apply is disabled, no pre-fill counts as a change, OK only closes, and the status line shows `EditBlockMessage`
- [x] `StatusText` priority: edit block message, then validation message, then the last apply result, then the pre-fill notice, else empty. `HasError` is true for a validation or write error, for red text
- [x] Tests cover each criterion above with a fake `IPropertyWriteTarget` that records calls and can be told to throw. Include: a part and an assembly and a weldment with blank type; Detailer following through several Designer edits and stopping; a file with every field already matching (no pending changes, OK writes nothing); invalid cost disables Apply and OK; an apply failure; a read-only snapshot; an unknown part type code kept as is
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Base it on `ObservableObject` and `RelayCommand` as `SettingsViewModel` does. Call `RaiseCanExecuteChanged` whenever state changes.
- The view-model holds the snapshot as the original and builds `PartEditedValues` from its fields each time it needs a plan. Rebasing after Apply means building a new snapshot from the old one with the written values (records with `with`).
- Cancel needs no command: the window's Cancel button uses `IsCancel` and nothing is written, as in the Settings window.
- Log `INFO` after a successful apply with the number of writes and the property names, never the values (they may hold personal data in future fields).

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 549 passed, 0 failed, 0 skipped (61 of them in `PartPropertiesViewModelTests`).
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (covered by 019)

## Follow-ups

Things noticed but not done.

- Decisions made inside the brief, for the reviewer (and 019) to confirm:
  - **Part type in a different case** (014 follow-up). A file code that `PartTypes.Find` matches but spells differently (`m`, ` pm `) is shown as the listed code (`M`, `PM`), so the dropdown selects the right entry and its tooltip shows the full name. No extra option is added for it. Because the window then shows a value the file does not hold, it counts as a pending change under open question 5: Apply is enabled at open, the pre-fill notice shows, and Apply/OK write the listed code. Whitespace alone is not a change (the plan trims). The `PartType` setter applies the same rule, so known codes are always held in the list's spelling. A code not in the list at all is kept as the file holds it (trimmed) and added as an extra option. Worth adding to spec 07's Part type rule if Aidan agrees.
  - **Files that cannot be edited** show what the file holds: no Designer or Part Type pre-fill and no Detailer following (the only display change is the listed spelling of a part type code above). Showing pre-fills that can never be written seemed misleading.
  - **Blank entry and option type.** `PartTypeOptions` is a list of `PartTypeOption` (a record in the view-model file with `Code`, `FullName`, `DisplayText`), so the blank entry has an empty `DisplayText` instead of `PartType.DisplayText`'s `": "`. 019 should bind the ComboBox with `SelectedValuePath="Code"` and `SelectedValue="{Binding PartType}"`, `DisplayMemberPath` or an item template on `DisplayText`, and show the code in the closed box.
  - **An invalid cost counts as a change** (`HasChanges` is true), since the plan cannot be built for it and the file cannot hold it; Apply stays disabled through `IsValid`.
  - **Status line.** The pre-fill notice shows from open until the first successful apply, and only while there are changes (undoing every pre-fill clears it). The last apply result ("Saved..." or the write error) stays until the next apply, including after further edits. The edit block message is not red (`HasError` is false for it).
  - **After Apply**, a Detailer that was following keeps following for the rest of the window's life (the spec's "file with a Detailer never follows" is applied at open). Text fields are not re-trimmed in the boxes; only the cost is reformatted, as the brief says.
  - The INFO log line after an apply reads `Part properties written (N): Set Designer, Remove Detailer, ...`: actions and property names only, never values. The ERROR line on failure is a fixed message plus the exception.
- From review: `Apply()` returns `true` when `!CanApply`, so `Ok()` called from code while values are invalid would close and drop the edits. WPF checks `CanExecute` first, so the window cannot hit it today; a guard at the top of `Ok()` would close the gap.
- From review: the case-fix rule for part type codes is now spec 07 open question 6 (parent added it). A case fix currently shows the pre-fill status line, which is slightly inaccurate wording for it.
- For 019: bind the part type combo box with `SelectedValuePath="Code"` and check the blank entry is visible and clickable (an empty display text may render as a very short row).
