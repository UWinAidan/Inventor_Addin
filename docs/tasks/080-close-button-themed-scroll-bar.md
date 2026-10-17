# 080: Dialogs with only a close button; a themed scroll bar for every scrolling area

- **Milestone:** M3 (fix from Aidan's check of 079, 2026-10-17)
- **Feature spec:** `docs/features/08-window-style.md` ("Resizable windows", "Scroll bars")
- **Status:** done
- **Depends on:** 079
- **Parallel-safe with:** none

## Goal

Aidan liked the collapsible scrolling Settings and asked for two changes: (1) no maximise or minimise button, only close, while Settings stays resizable by its edges and keeps remembering its size; (2) a scroll bar that matches the theme in light and dark (thin, rounded thumb, no arrows, theme colours) with a clear gap of about 12 from the content, the content lining up with the header and footer whether or not the bar shows, in the shared styles so every scrolling list uses it, the Mass Update list included.

## Scope

- `src/InventorAddin/UI/WindowHost.cs`, `UI/Theme/Styles.xaml`, every window's body panel (`DialogBodyContent`), `UI/SettingsWindow.xaml` (comment)
- `WindowXamlRulesTests` (the body panel's style); spec 08

## Acceptance criteria

- [x] A resizable window shows only the close button; it cannot be maximised (snap or Win+Up are undone); it still resizes by its edges and remembers its size and position
- [x] One implicit `ScrollBar` style: 8 thick, no arrow buttons, transparent track, rounded thumb, theme colours only (no new tokens)
- [x] In every window body the bar sits outside the content column, 12 from it, and the content lines up with the header and footer with or without the bar; the Mass Update list keeps a 12 gap; the pick lists and dropdowns get the same bar
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Verification

- **How it is built.**
  - `WindowHost.KeepToCloseButtonOnly` clears `WS_MINIMIZEBOX` and `WS_MAXIMIZEBOX` on a resizable window and puts it back to normal if Windows maximises it anyway. The saved placement is always normal (`Maximized: false`) and a remembered maximised flag is no longer applied.
  - Scroll bar: `ScrollBarPageButton` (transparent, not focusable), `ScrollBarThumb` (rounded, `InputBorderBrush`, `TextMutedBrush` on hover or drag), and the implicit `ScrollBar` style with a vertical and a horizontal template. Being implicit, it reaches every scroll bar in a window: body, Mass Update list, Material and Finish pick lists, dropdown lists.
  - Body: `DialogBody` has its own template, a content column and a 16 column with the bar against its right edge. Its margin is `-4,-4,-20,-4` and the panel inside uses `DialogBodyContent` (margin 4), so the content sits exactly where it did, the bar is 12 from the content and ends 2 short of the window's edge, and focus outlines still fit inside the scrolling area. A XAML rules test checks every body panel uses `DialogBodyContent`.
  - `ListScroller` (Mass Update) has the same kind of template with a 20 column always kept, so the entries do not move when the bar appears.
- **Ran (local, Windows, Aidan's PC):** `dotnet test tests/InventorAddin.Core.Tests`: 2650 passed. `dotnet build InventorAddin.slnx -tl:off`: build succeeded, 0 errors, the 8 known CS8600 warnings; **deployed** (Inventor was closed).
- **Not confirmed in Inventor:** that the title bar shows only close after the style bits are cleared; that the negative margin draws the bar in the right margin without clipping. Standard WPF and Win32, no Inventor API.
- **Inventor API members not confirmed:** none.
- **Manual checklist for Inventor:**
  1. Open Settings: only the close button on the title bar. Double-click the title bar, drag it to the top of the screen, and press Win+Up: it does not maximise. Resize by an edge or corner: it works. Close and reopen: same size and place.
  2. Make Settings shorter than its page: the scroll bar appears to the right of the content, thin with a rounded thumb and no arrows, with a clear gap; the text boxes and Browse buttons do not touch it. Their right edges line up with the header and the Save and Cancel buttons, with and without the bar showing (compare by resizing).
  3. Hover the thumb (it darkens in light, lightens in dark), drag it, click the track (pages), and use the mouse wheel. Check light and dark themes: no system grey anywhere.
  4. Mass Update with enough files to scroll: the same bar, with a gap from the entries; the entries do not shift when it appears.
  5. Material and Finish: the material and finish lists scroll with the same bar. A dropdown with enough entries (Category) too.
  6. Other windows look unchanged (Part Properties, New Part, Save As Copy, Rename, About); keyboard focus outlines are not cut off at the edges of the body.

## Follow-ups

- `WindowPlacement.Maximized` in Core is now always false for dialogs; it stays in case a non-dialog window ever wants it.
- The pick lists' bar sits against the list's inner edge with no extra gap, which suits a bordered list; give it the 12 gap too if Aidan wants it.
