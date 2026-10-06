# 015: Default designer setting

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Rules: Designer; Add-in side: Settings window)
- **Status:** done (add-in code not compiled)
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

- [x] `AddinSettings.DefaultDesigner` is a `string` defaulting to `""`. A settings file written before this task loads with `""`. `CurrentSchemaVersion` stays 1 (the change is additive)
- [x] `SettingsViewModel.DefaultDesigner` edits the copy, raises `PropertyChanged`, and affects `IsDirty`. On save the value is normalised with `PropertyValues.NormaliseText` (task 013), and dirty tracking compares normalised values. Typing only spaces into an empty setting is not dirty
- [x] Longer than 255 characters sets `ErrorText` and disables Save (`PropertyValues.ValidateText`)
- [x] The restart notice applies only to developer tools: "Showing or hiding developer tools takes effect the next time Inventor starts." The default designer takes effect at once, because the command reads `AddinServices.Settings` each time it runs (task 008 made a save update it)
- [x] The window has a labelled text box "Default designer" bound with `UpdateSourceTrigger=PropertyChanged`, and a grey hint under it: "Filled in as Designer in Part Properties when a file has none."
- [x] Tests cover: the store round trip with the new field, loading an old file without it, dirty tracking for the text, trimming, the length limit, and `PropertyChanged`
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists the XAML as not compiled

## Notes for the implementer

- Follow the existing `ShowDeveloperTools` pattern in `SettingsViewModel` exactly. `AddinSettings` is a record compared by value, which is how `IsDirty` works; a `string` property keeps that.
- Keep the code-behind unchanged.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core` (0 warnings, 0 errors) and `dotnet test tests/InventorAddin.Core.Tests` (488 passed, 0 failed, 0 skipped; 85 of them in the settings store and settings view-model tests). Cloud session: `src/InventorAddin` was not built.
- **Not compiled (changed under `src/InventorAddin`):** `src/InventorAddin/UI/SettingsWindow.xaml` (adds a `Label` with access key `_Default designer` targeting a `TextBox` named `DefaultDesignerBox`, and the grey hint; the restart notice moved up under the check box). Code-behind unchanged.
- **Inventor API members not confirmed:** none; no Inventor API is used.
- **Design notes for review:**
  - The view-model keeps the default designer exactly as typed for display, and the trimmed value in its edited `AddinSettings` copy, so `IsDirty` (record equality) compares trimmed values. The saved copy is trimmed on construction too, so a file holding `" A. Person "` opens clean. After a save the text box shows the trimmed value.
  - `ErrorText` is now the validation message if there is one, otherwise the last save failure. A validation error disables Save even when other settings changed. A value from the file that is too long shows the error as soon as the window opens.
  - `AddinSettings.DefaultDesigner` turns a null (for example `"defaultDesigner": null` in the file) into `""`.
- **Manual checklist for Inventor:**
  1. Build on Windows with Inventor closed, start Inventor, open any part.
  2. Click the AWB Addin Settings button. The window shows "Show developer tools", under it the grey line "Showing or hiding developer tools takes effect the next time Inventor starts.", then a "Default designer" label with an empty text box and the grey hint "Filled in as Designer in Part Properties when a file has none." Save is disabled.
  3. Press Alt+D: focus moves to the text box. Type a few spaces: Save stays disabled. Type a name: Save enables.
  4. Paste text longer than 255 characters: a red line says "Default designer must be 255 characters or fewer." and Save is disabled. Shorten it: the red line goes and Save enables.
  5. Enter "  A. Person  " and Save. The window closes. Open `%APPDATA%\AwbAddin\settings.json`: it has `"defaultDesigner": "A. Person"` (trimmed).
  6. Reopen Settings: the box shows "A. Person" and Save is disabled. Cancel.
  7. Restart Inventor and reopen Settings: the value is still there.
  8. With an old `settings.json` that has no `defaultDesigner` line, Inventor starts without a warning and the box is empty.

## Follow-ups

Things noticed but not done.

- None.
