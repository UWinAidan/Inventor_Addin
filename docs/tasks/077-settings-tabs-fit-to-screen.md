# 077: Settings in tabs, and no window taller than the screen

- **Milestone:** M3 (fix from Aidan's M3 checks, 2026-10-17)
- **Feature spec:** `docs/features/08-window-style.md` ("Window layout", "Settings")
- **Status:** done
- **Depends on:** 070
- **Parallel-safe with:** 078

## Goal

Aidan found the Settings window taller than the screen, with no way to scroll. Split Settings into tabs, and make it a rule for every add-in window: never taller than the screen's working area; when the content is taller, the body scrolls and the header and buttons stay visible.

## Scope

- `src/InventorAddin.Core/Theming/WindowFit.cs` (new) and its tests
- `src/InventorAddin/UI/WindowHost.cs`, `UI/Theme/Styles.xaml`, `UI/SettingsWindow.xaml` and `.xaml.cs`, and the layout of every other window's XAML
- `tests/InventorAddin.Core.Tests/Styling/WindowXamlRulesTests.cs`
- Spec 08

## Acceptance criteria

- [x] Settings has five tabs, in this order: General (Default designer, Other names, New file units), Part numbers (Register folder), Folders (Work folder, Other folders), Material and finish (Material library, Finish list), Developer (Show developer tools)
- [x] Save and Cancel stay below the tabs and apply to every tab
- [x] Every window: the height is capped at the working area of the owner's screen, the window is moved up if its bottom would be off the screen, and the body scrolls while the header and footer stay visible
- [x] A XAML rules test checks every window's layout (root Grid, one `DialogBody` scroll viewer on a star row, header and footer outside it)
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Verification

- **How it is built.**
  - `WindowFit` (Core) holds the two calculations: the maximum height (the working area's height) and the top that keeps a window inside the working area (moved up when its bottom is off the screen, never above the top so the title bar stays reachable).
  - `WindowHost.FitToScreen` reads the working area of the screen the owner window is on (`System.Windows.Forms.Screen.FromHandle`), converts it to WPF units with the window's `TransformFromDevice`, sets `MaxHeight`, and moves the window on `Loaded` and on every `SizeChanged` (windows size to their content, so Part Properties can grow while open). A failure is logged once a session and leaves the window as it was.
  - Every window's root is now a `Grid` (header, body, footer; the body row is star-sized) instead of a `StackPanel`; the body is a `ScrollViewer` in the new `DialogBody` style (vertical scroll bar only when needed, no sideways scroll). Content and bindings are unchanged; the windows were re-indented by a script and each diff was read.
  - Settings: a `TabControl` in the new `TabStrip` style shows the headers only. All five pages sit in one cell of the body; the pages not shown are Hidden, not Collapsed, so the window keeps the tallest page's height and does not jump when a tab is picked. Code-behind shows the selected page and handles Ctrl+Tab and Ctrl+Shift+Tab from anywhere in the window, moving focus to the new tab so it is never left on a hidden box. The pages have no section headings; the tab names them.
- **Ran (local, Windows, Aidan's PC):**
  - `dotnet test tests/InventorAddin.Core.Tests`: 2616 passed, 0 failed.
  - `dotnet build InventorAddin.slnx -tl:off`: build succeeded, 0 errors; the only warnings are the four CS8600 ones already in `TODO.md`. **Not deployed**: Inventor was running and held the add-in files.
- **Inventor API members not confirmed:** none; only WPF and WinForms members were used.
- **Manual checklist for Inventor** (close Inventor, build, open Inventor):
  1. Settings opens with the General tab selected and is shorter than the screen. The five tabs are in the order above; each holds its rows; Save and Cancel are below.
  2. Click through the tabs: the window does not change height or move. The selected tab has the blue line and darker text; hover lightens the others. Check light and dark themes.
  3. Ctrl+Tab and Ctrl+Shift+Tab move between tabs while typing in a box. Tab from the header area reaches the tab strip once, then the page's fields, then Save and Cancel; the arrow keys change tab when the strip has focus.
  4. Change a value on one tab and another on a second tab, Save, reopen: both kept. Change a value, Cancel: nothing kept. An invalid register folder on Part numbers shows its error under the tabs and disables Save on every tab.
  5. Access keys on the visible tab work (Alt+D on General, Alt+R on Part numbers, and so on).
  6. Every other window looks as before: Part Properties, About, New Part, Save As Copy, Rename, Mass Update, Material and Finish.
  7. The scroll rule: set Windows display scale high (for example 175 %) or the taskbar tall, and open Part Properties and Material and Finish. The window fits on the screen; if the content is taller, a scroll bar appears in the body and the header and buttons stay visible. Optional: Mass Update with a long scan.

## Follow-ups

- The body's scroll bar stays light in the dark theme, as the list scroll bars already do (spec 08 has no scroll bar style).
- A save error about a file on another tab (for example the material library file missing) shows below the tabs but does not switch to that tab; the message names the setting.
- The working area is read when a window opens; moving a window to a smaller screen afterwards does not re-cap it.
