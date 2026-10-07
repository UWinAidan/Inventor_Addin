# 020: Spike: theme, shared styles and icons inside Inventor

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (Notes for planning; Following Inventor's theme; Ribbon icons)
- **Status:** todo
- **Depends on:** none
- **Needs:** windows (run by Aidan in a local Claude session on his PC, with Inventor 2026)
- **Parallel-safe with:** every cloud task (it changes no committed source)

## Goal

Settle, in Inventor, the four API behaviours that the rest of M1b is built on, and record the answers in this file. Tasks 026 and 030 are not run until this file says what works.

## Scope

Throwaway code on a local scratch branch (`spike/020`), never merged. The only committed change is this task file, with the Results section filled in.

Out of scope:

- Any real styling, icons or window changes. Use the smallest code that answers each question.

## What to find out

Work in the add-in project on the scratch branch. Use the About window for everything; it is the simplest window.

**1. Shared resource dictionaries load in Inventor's process, merged after the window is built.**

The plan (task 026) is that `WindowHost` merges the theme dictionaries into `window.Resources` *after* the window's constructor has run `InitializeComponent`, so windows must refer to shared styles with `DynamicResource` (and implicit, keyless styles must still pick up). Prove that exact flow:

- Add `UI/Theme/SpikeColors.xaml`: a `ResourceDictionary` with one `SolidColorBrush` keyed `Spike.Background` (an obvious colour, such as dark red) and an implicit `Button` style setting `Background` to a second obvious colour.
- In `WindowHost.ShowDialog`, before `ShowDialog()`, merge it two ways, one at a time, rebuilding between:
  - a. `new ResourceDictionary { Source = new Uri("pack://application:,,,/InventorAddin;component/UI/Theme/SpikeColors.xaml", UriKind.Absolute) }`
  - b. a dictionary with an `x:Class` and code-behind constructor calling `InitializeComponent()`, created with `new SpikeColors()`
- Set the window's background with `window.SetResourceReference(Control.BackgroundProperty, "Spike.Background")`.
- Record for a and b: does the About window open, with the background colour and the button colour applied? Any exception (check `awbaddin.log` and the error box)?

**2. Inventor's active theme.**

- Log `InventorHost.App.ThemeManager.ActiveTheme.Name` (wrap in `ComSafe.Get`) when About opens. Confirm the member names compile.
- Open About in Inventor's light theme, then switch to dark (Application Options, Colors, UI Theme) and open it again. Record the exact names reported for each theme, and any other themes Inventor 2026 offers.

**3. Icons on a button definition from .NET 8.**

- Embed one 16 px and one 32 px PNG (any simple drawing) in the add-in.
- Convert each to an `IPictureDisp` with `OleCreatePictureIndirect` from `oleaut32.dll` (P/Invoke, `PICTDESC` with `PICTYPE_BITMAP` from `Bitmap.GetHbitmap()`, IID of `IPictureDisp` `7BF80981-BF32-101A-8BBB-00AA00300CAB`). Pass the results as the `StandardIcon` and `LargeIcon` arguments of `ControlDefinitions.AddButtonDefinition` for the About command (the two `Type.Missing` arguments in `AddinCommand.Register`).
- Record: does the icon show on the ribbon at small size? Make the button large for one build: does the 32 px icon show? Is transparency kept (a PNG with a transparent background shows no box)? Does it read on both themes?
- When Inventor reuses an existing definition (`defs[InternalName]` returns one), can the icons be set afterwards through `ButtonDefinition.StandardIcon` and `ButtonDefinition.LargeIcon`? Record whether those setters exist and work.
- If `OleCreatePictureIndirect` does not work, try `System.Windows.Forms.AxHost.GetIPictureDispFromPicture` through a small `AxHost` subclass, and record which one works.

**4. A dark title bar.**

- In `WindowHost`, after the window's handle exists (`SourceInitialized`, or `new WindowInteropHelper(window).EnsureHandle()`), call `DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref int 1, sizeof(int))` from `dwmapi.dll`.
- Record: does the About title bar turn dark? Any side effect (flicker, wrong size, error)? Record the Windows version (`winver`).

## Acceptance criteria

- [ ] The Results section below answers questions 1 to 4, with the exact names and the method that worked
- [ ] Any member that did not compile is listed with the name that did
- [ ] The scratch branch is not merged or pushed to `main`; only this file is committed

## Notes for the implementer

- This runs on Windows with Inventor. Close Inventor before each build (`dotnet build InventorAddin.slnx`), then start it and test.
- Use `ComSafe.Get` around the theme read, as for every COM getter.
- Keep the code you tried, in short, in the Results so 026 and 030 can copy it.

## Results

Filled in after the spike.

1. Resource dictionaries:
   - a. pack URI:
   - b. `x:Class` dictionary:
   - implicit `Button` style picked up after merging:
2. Theme names: light = , dark = , others:
3. Icons:
   - method that worked:
   - small 16 px shows:
   - large 32 px shows:
   - transparency kept:
   - setters on an existing definition:
4. Dark title bar (Windows version):

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none committed
- **Inventor API members not confirmed:** to be filled in
- **Manual checklist for Inventor:** the questions above

## Follow-ups

Things noticed but not done.
