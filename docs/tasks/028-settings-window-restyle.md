# 028: Settings window on the shared look

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (The three windows: Settings)
- **Status:** todo
- **Depends on:** 023, 027
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 025, 029 (provided neither edits `UI/Theme/` or `UI/Controls/`)

## Goal

The Settings window uses the shared header, sections and footer, so it looks like About in both themes.

## Scope

- `src/InventorAddin/UI/SettingsWindow.xaml` and `.xaml.cs`

Out of scope:

- The shared styles and pieces (`UI/Theme/`, `UI/Controls/`). If something is missing there, stop and record it in Follow-ups rather than adding it here, so 028 and 029 can run side by side.
- What the settings do.

## Acceptance criteria

- [ ] `DialogHeader` with title `HeaderTitle` ("Settings") and subline `HeaderSubline` ("AWB Addin")
- [ ] Section **General**: a `FieldRow` "_Default designer" targeting the text box (two-way, `UpdateSourceTrigger=PropertyChanged`), with `DefaultDesignerHelp` under the box in `MutedText`
- [ ] Section **Developer**: the "Show developer tools" checkbox, with `RestartNotice` under it in `MutedText`
- [ ] `DialogFooter`: its status line shows `ErrorText` in the error style while `HasError` is true and is collapsed otherwise; Save (`PrimaryButton`, `IsDefault`, `SaveCommand`) and Cancel (`IsCancel`)
- [ ] The window XAML contains layout and bindings only: no colour, font size, font weight, margin, padding or width/height values, and no `StaticResource` to shared styles
- [ ] Code-behind still only sets the title, the `DataContext` and the close-on-save wiring
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists both window files as not compiled

## Notes for the implementer

- The literal help text now comes from `SettingsViewModel.DefaultDesignerHelp` (023); remove it from the XAML.
- The error text clears when the user edits after a failed save (023). The window only binds to it.
- Follow how About (026) uses the shared pieces.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:** none expected (no Inventor API used)
- **Manual checklist for Inventor:**
  1. Light theme, then dark theme: open Settings. Header, two sections with headings and divider lines, the text box and checkbox in the shared colours, Save in the accent colour.
  2. Tab order: Default designer, Show developer tools, Save, Cancel, each with the accent focus outline. Alt+D moves to Default designer. Space toggles the checkbox. Enter saves, Esc cancels.
  3. Save is disabled (disabled look) until something changes.
  4. Type a name of more than the allowed length, or one with a character the validation rejects: the error shows in the footer in the error colour, and Save is disabled.
  5. Make the settings file read-only (`%APPDATA%\AwbAddin`), change a value and Save: the error shows. Change the value again: the error clears (task 008 follow-up).
  6. The window fits its content with no empty band above the footer.

## Follow-ups

Things noticed but not done.
