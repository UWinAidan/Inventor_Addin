# 090: AWB Hole window and command

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command", "The window"); `docs/features/08-window-style.md`
- **Status:** todo
- **Depends on:** 085, 087, 089
- **Needs:** windows build
- **Parallel-safe with:** 086

## Goal

The AWB Hole command and its modeless window in the add-in's shared look, bound to the view-model and driving the session, on the AWB Addin tab for part files.

## Scope

- `src/InventorAddin/UI/AwbHoleWindow.xaml` and `.xaml.cs`, `src/InventorAddin/Commands/AwbHoleCommand.cs`
- `UI/WindowHost.cs`: a modeless show (owner Inventor, the shared theme, header icon, fit to screen, modeless keyboard interop through `ElementHost.EnableModelessKeyboardInterop`), opened through `Dispatcher.BeginInvoke` after Inventor's event or command has returned (spike 081 round 3)
- `RibbonLayout` and its tests: AWB Hole (large, `IconNames.AwbHole`) on the Part ribbon's AWB Addin tab, Create panel (Default)
- `RibbonSetup`/`CommandNames` as needed; spec 01 and spec 08 rows

## Acceptance criteria

- [ ] The window follows spec 09's field order in the shared look (sections, field rows, footer with status, OK and Cancel), passes the XAML rules test, and is never taller than the screen.
- [ ] Opening the command in a part opens the window with the default centres picked and the preview showing; in other documents the button is not shown (part ribbon only).
- [ ] Only one AWB Hole window at a time; pressing the command again brings it to the front.
- [ ] OK and Cancel as the session (089); Esc is Cancel; Enter is OK only when OK is enabled.
- [ ] An API to open the window on an existing hole in edit mode, for 092.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- `MaterialFinishWindow` is the closest existing window for layout and bindings.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. In a part with a sketch of three points, AWB Hole opens with all three picked blue and a hole at each; clicking toggles a point; the preview follows.
  2. Every type and seat, metric and imperial: clearance close/normal/loose, counterbore, countersink, tapped (check the modelled tap drill), dowel with each fit and tolerance; blind with 118, 90 and flat; through all; to a face; flip.
  3. A dowel hole is coloured on every face, bottom included, with the tolerance on the diameter.
  4. An invalid value (hole larger than its counterbore) is refused with a message and the last good hole stays.
  5. OK is one Ctrl+Z; Cancel leaves the part as it was. Light and dark themes.

## Follow-ups
