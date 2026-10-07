# 028: Settings window on the shared look

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (The three windows: Settings)
- **Status:** done
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

- [x] `DialogHeader` with title `HeaderTitle` ("Settings") and subline `HeaderSubline` ("AWB Addin")
- [x] Section **General**: a `FieldRow` "_Default designer" targeting the text box (two-way, `UpdateSourceTrigger=PropertyChanged`), with `DefaultDesignerHelp` under the box in `MutedText`
- [x] Section **Developer**: the "Show developer tools" checkbox, with `RestartNotice` under it in `MutedText`
- [x] `DialogFooter`: its status line shows `ErrorText` in the error style while `HasError` is true and is collapsed otherwise; Save (`PrimaryButton`, `IsDefault`, `SaveCommand`) and Cancel (`IsCancel`)
- [x] The window XAML contains layout and bindings only: no colour, font size, font weight, margin, padding or width/height values, and no `StaticResource` to shared styles
- [x] Code-behind still only sets the title, the `DataContext` and the close-on-save wiring
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists both window files as not compiled

## Notes for the implementer

- The literal help text now comes from `SettingsViewModel.DefaultDesignerHelp` (023); remove it from the XAML.
- The error text clears when the user edits after a failed save (023). The window only binds to it.
- Follow how About (026) uses the shared pieces.

## Verification

Filled in by the implementer.

- **Ran:** cloud session (`CLAUDE_CODE_REMOTE=true`). `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 673 passed, 0 failed, 0 skipped. No Core change in this task; the working tree also held task 029's work in progress (Part Properties view-model and its tests), which was included in that run and passed.
- **Extra check, not a build of the add-in:** `SettingsWindow.xaml` and `.xaml.cs` were copied, with the current `UI/Theme/*` and `UI/Controls/*`, into 026's throwaway `net8.0-windows` WPF project in the scratchpad (`EnableWindowsTargeting=true`, Inventor types stubbed). A `--no-incremental` build had 0 warnings and 0 errors and produced `SettingsWindow.baml`; a deliberately wrong attribute (`IsDefaultX` on Save) failed with MC3072, so the markup compiler checked the window. Nothing was run or shown.
- **Not compiled (changed under `src/InventorAddin`):** `UI/SettingsWindow.xaml` (in the add-in project itself; see the extra check above). `UI/SettingsWindow.xaml.cs` is unchanged: it already set only the title, the `DataContext` and the close-on-save wiring.
- **Inventor API members not confirmed:** none (no Inventor API used).
- **How the window is laid out:** the help line under the text box and the restart note under the checkbox are `FieldRow`s with no label, so they sit in the value column and take their spacing (9) from `FieldRow`. The checkbox is also in a label-less `FieldRow`, so it lines up with the text box and keeps the 18 gap to the footer that 026's spacing rule expects. A note inside the same row as its control was not used: `FieldRow` centres its label vertically on the whole value, so "Default designer" would sit between the box and the help line.
- **Manual checklist for Inventor:**
  1. Light theme, then dark theme: open Settings. Header (empty icon tile, "Settings", "AWB Addin" under it), sections GENERAL and DEVELOPER with divider lines, the text box and checkbox in the shared colours, Save in the accent colour, Cancel in the secondary look. In dark theme no text is dark on dark.
  2. The text box, the help line under it and the checkbox all start at the same left edge (the value column); the "Default designer" label is level with the text box, not with the help line.
  3. Tab order: Default designer, Show developer tools, Save, Cancel, each with the accent focus outline (round the box only for the checkbox). Alt+D moves to Default designer. Space toggles the checkbox. Enter saves, Esc cancels.
  4. Save is disabled (disabled look) until something changes.
  5. Type a name of more than the allowed length, or one with a character the validation rejects: the error shows in the footer, left of the buttons, in the error colour, and Save is disabled. Correct it: the error disappears and the footer is a single row of buttons again.
  6. Make the settings file read-only (`%APPDATA%\AwbAddin`), change a value and Save: the error shows. Change the value again: the error clears (task 008 follow-up). A long error (the file path) wraps rather than widening the window past its maximum width.
  7. The window fits its content with no empty band above the footer.

## Follow-ups

Things noticed but not done.

- **Gap between a control and its note.** The spec gives no value for a help line under a control. This window uses a label-less `FieldRow`, so the gap is the row gap (9). If that reads as too loose, a shared `HelpText` style (muted, a smaller top gap, used in its control's row with the label aligned to the control) would be a change to `UI/Theme/` and `FieldRow`, left out because 029 runs alongside.
- **Restart note alignment.** The note sits under the checkbox's box, not under its label text (24 further in). Aligning it with the label text would need a margin, which the window may not set. Worth a look in Inventor; a shared style would fix it if wanted.
- **No access key on "Show developer tools".** "D" is taken by Default designer, and the brief's checklist names only Alt+D. Aidan may want one, for example "_Show developer tools" for Alt+S.
