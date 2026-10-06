# 015: Default designer setting

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Rules: Designer; Add-in side: Settings window)
- **Status:** todo
- **Depends on:** 013
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** 011, 012, 014, 016, 017, 018

## Goal

A `DefaultDesigner` setting exists and can be edited in the Settings window, so the Part Properties window (019) can pre-fill Designer on files that have none.

## Scope

- `src/InventorAddin.Core/Settings/AddinSettings.cs` (add `DefaultDesigner`)
- `src/InventorAddin.Core/ViewModels/SettingsViewModel.cs` (add the field; reword the restart notice)
- `tests/InventorAddin.Core.Tests/Settings/SettingsStoreTests.cs` (update)
- `tests/InventorAddin.Core.Tests/ViewModels/SettingsViewModelTests.cs` (update)
- `src/InventorAddin/UI/SettingsWindow.xaml` (add the text box)

Out of scope:

- The pre-fill itself (016).
- Any other setting (spec 01 open question 3).

## Acceptance criteria

- [ ] `AddinSettings.DefaultDesigner` is a `string` defaulting to `""`. A settings file written before this task loads with `""`. `CurrentSchemaVersion` stays 1 (the change is additive)
- [ ] `SettingsViewModel.DefaultDesigner` edits the copy, raises `PropertyChanged`, and affects `IsDirty`. On save the value is normalised with `PropertyValues.NormaliseText` (task 013), and dirty tracking compares normalised values. Typing only spaces into an empty setting is not dirty
- [ ] Longer than 255 characters sets `ErrorText` and disables Save (`PropertyValues.ValidateText`)
- [ ] The restart notice applies only to developer tools: "Showing or hiding developer tools takes effect the next time Inventor starts." The default designer takes effect at once, because the command reads `AddinServices.Settings` each time it runs (task 008 made a save update it)
- [ ] The window has a labelled text box "Default designer" bound with `UpdateSourceTrigger=PropertyChanged`, and a grey hint under it: "Filled in as Designer in Part Properties when a file has none."
- [ ] Tests cover: the store round trip with the new field, loading an old file without it, dirty tracking for the text, trimming, the length limit, and `PropertyChanged`
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists the XAML as not compiled

## Notes for the implementer

- Follow the existing `ShowDeveloperTools` pattern in `SettingsViewModel` exactly. `AddinSettings` is a record compared by value, which is how `IsDirty` works; a `string` property keeps that.
- Keep the code-behind unchanged.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
