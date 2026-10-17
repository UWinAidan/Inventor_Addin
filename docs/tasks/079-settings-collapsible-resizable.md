# 079: Settings as one page of collapsible sections, resizable and remembered

- **Milestone:** M3 (fix from Aidan's check of 077, 2026-10-17)
- **Feature spec:** `docs/features/08-window-style.md` ("Window layout": resizable windows, collapsible sections; "Settings")
- **Status:** done
- **Depends on:** 077
- **Parallel-safe with:** none

## Goal

Aidan did not like the Settings tabs from task 077: the labels were cut off and too little showed at once. Replace them with one vertically scrolling page whose section headings collapse, with a summary on a collapsed heading, and make the window resizable and remembered. The collapsible section is a shared control.

## Scope

- Core: `ViewModels/SettingsSummaries.cs` (new), the `*Summary` properties on `SettingsViewModel`, `Settings/WindowStateStore.cs` (new), `Theming/WindowFit.cs` (`ScreenArea`, `Restore`), and their tests
- Add-in: `UI/Controls/CollapsibleSection.cs` (new), `UI/Theme/Styles.xaml` (collapsible section, resizable window and content; the 077 tab styles removed), `UI/Theme/ThemeResources.cs`, `UI/WindowHost.cs`, `UI/SettingsWindow.xaml` and `.xaml.cs`, `Commands/ShellCommands.cs`
- `WindowXamlRulesTests` (accepts the resizable root style); spec 08

## Acceptance criteria

- [x] One scrolling page with General, Part numbers, Folders, Material and finish, Developer, in that order
- [x] Each heading collapses its section; a collapsed section shows a one-line summary of its current values beside the heading; the summaries follow edits
- [x] All sections start expanded; the collapsed ones are remembered between sessions
- [x] The window is resizable, has a maximise button, and remembers its size and position; it never opens taller than the screen's working area; the body scrolls while the header and Save and Cancel stay visible
- [x] `CollapsibleSection` is a shared control any window can use
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Verification

- **How it is built.**
  - `CollapsibleSection` (UI/Controls) is an `ItemsControl` like `FormSection`, with `Heading`, `IsExpanded` (default true, two-way) and `Summary`. Its template's heading line is a `ToggleButton`: a chevron (down when expanded, right when collapsed), the heading, and while collapsed the summary in muted 12 px text with an ellipsis and a tooltip. The rows collapse with it.
  - Summaries are built in Core (`SettingsSummaries`) from the values being edited; the view-model raises them on every edit. Examples: `Test Designer, 2 other names, Metric`; `C:\CAD_TEST, 2 other folders`; `starter library, starter finish list`.
  - `WindowHost.ShowDialog` takes a state key. With one, it reads `windows.json` (`WindowStateStore`, beside `settings.json`), collapses the named sections it remembers, and for a resizable window restores the size and position through `WindowFit.Restore`, which picks the screen the window overlaps most, shrinks it to that screen's working area and the minimum size, and moves it inside; a window on a screen that is gone opens as usual. On close (Save, Cancel or the X) it saves the collapsed section names and `RestoreBounds` with the maximised flag. A file that cannot be read or written is logged once a session.
  - A window whose XAML sets `ResizeMode="CanResize"` gets the `ResizableDialogWindow` style (620 wide to start, height from its content, minimum 460 by 320), no minimise button (`WS_MINIMIZEBOX` cleared), and no `MaxHeight` cap, so it can be maximised. Instead, while it still sizes to its content, a height over the working area is cut to it, so it never opens taller than the screen; once resized by hand it is left alone. Fixed-size windows keep task 077's cap.
  - Settings: the tabs and their code are gone. The page is a `StackPanel` of five `CollapsibleSection`s named `GeneralSection`, `PartNumbersSection`, `FoldersSection`, `MaterialFinishSection`, `DeveloperSection`, in a `DialogBody` scroll viewer between the header and the footer. The root uses `ResizableDialogContent`, which has no 560 maximum width, so the rows widen with the window.
- **Ran (local, Windows, Aidan's PC):**
  - `dotnet test tests/InventorAddin.Core.Tests`: 2650 passed, 0 failed. The tests that pin which properties a Settings edit raises now ignore the `*Summary` names; new tests cover the summaries' values and that they are raised.
  - `dotnet build InventorAddin.slnx -tl:off`: build succeeded, 0 errors; the only C# warnings are the four CS8600 ones already in `TODO.md`. **Not deployed**: Inventor was running.
- **Not confirmed in Inventor:** whether clearing `WS_MINIMIZEBOX` right after the handle is created survives WPF showing the window; whether a maximised owned dialog fills the screen; `RestoreBounds` on a maximised window. All are standard WPF and Win32, not Inventor API.
- **Inventor API members not confirmed:** none; no Inventor API was used.
- **Manual checklist for Inventor** (close Inventor, build, open Inventor):
  1. Open Settings: one page with General, Part numbers, Folders, Material and finish, Developer, all expanded, each heading with a down chevron. Save and Cancel at the bottom. Nothing cut off.
  2. Click the Folders heading: it collapses, the chevron points right, and the heading line reads like `C:\CAD_TEST, 2 other folders`. Collapse Material and finish: `starter library, starter finish list` (or your files' names). Check the other three summaries read sensibly. Space on a focused heading toggles it too.
  3. Expand a section, edit a value (for example add a folder under Other folders), collapse it: the summary shows the new value.
  4. Resize the window by its edges and maximise it with the title bar button: the rows widen, there is no minimise button, and the body scrolls when the window is shorter than the page, with the header and the buttons always visible.
  5. Collapse two sections, move and resize the window, Cancel. Reopen Settings: the same two are collapsed, and the window is where you left it at that size. Restart Inventor and reopen: still so. Close while maximised: it reopens maximised.
  6. Make the window taller than the screen allows (for example on a smaller display, or at a higher display scale): it opens no taller than the screen's working area, with the body scrolling.
  7. Save still works from the new layout, the validation messages show below the page, and Cancel keeps nothing. Access keys work for fields in expanded sections.
  8. Every other window is unchanged (fixed size, no maximise button).
  9. Optional: delete `%APPDATA%\AwbAddin\windows.json`, or put `{ broken` in it: Settings opens at its usual size with everything expanded, and the log has one warning for the broken file.

## Follow-ups

- An access key for a field in a collapsed section does nothing; it could expand the section first. A save error for a setting in a collapsed section does not expand it; the message names the setting.
- Only Settings opts in. Mass Update and Material and Finish, with their long lists, may want to be resizable and remembered too; each needs `ResizeMode="CanResize"`, the `ResizableDialogContent` root and a state key.
- The chevron and heading are one button; a screen reader announces the heading but not whether it is expanded (`ToggleButton` reports its checked state, which should cover it, but it is not checked).
