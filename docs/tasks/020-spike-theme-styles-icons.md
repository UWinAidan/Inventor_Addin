# 020: Spike: theme, shared styles and icons inside Inventor

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (Notes for planning; Following Inventor's theme; Ribbon icons)
- **Status:** done
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

Run on 2026-10-06 in Inventor 2026, Windows 11 Pro 25H2 (build 26200.9457), on the local branch `spike/020` (not pushed). Each finding was checked by eye in Inventor, and confirmed in `awbaddin.log`.

1. Resource dictionaries: both ways work. Each was merged into `window.Resources.MergedDictionaries` in `WindowHost.ShowDialog`, after the window's constructor ran `InitializeComponent` and just before `ShowDialog()`.
   - a. pack URI: **works.** About opened with the dark red background and no error.
     ```csharp
     var dict = new ResourceDictionary { Source = new Uri("pack://application:,,,/InventorAddin;component/UI/Theme/SpikeColors.xaml", UriKind.Absolute) };
     window.Resources.MergedDictionaries.Add(dict);
     window.SetResourceReference(Control.BackgroundProperty, "Spike.Background");
     ```
   - b. `x:Class` dictionary: **works.** `new SpikeColorsClass()` (a `ResourceDictionary` subclass whose constructor calls `InitializeComponent()`) gave the navy background and no error.
   - implicit `Button` style picked up after merging: **yes, both ways.** The Close button took the dictionary's colour (lime in a, yellow in b), although the style was merged after the button was built.
   - Both worked in the light and the dark theme. `awbaddin.log` showed no exception and no error box appeared.
2. Theme names: light = `LightTheme`, dark = `DarkTheme`, others: none. `ThemeManager.Themes` lists exactly `[DarkTheme, LightTheme]`, matching the UI Theme options in Application Options.
   - Compiles and works: `ComSafe.Get(() => InventorHost.App.ThemeManager.ActiveTheme.Name)`, and `foreach (Inventor.Theme t in InventorHost.App.ThemeManager.Themes)`.
   - The name changes as soon as the theme is switched. The next About opened after switching reported `DarkTheme`.
3. Icons:
   - method that worked: `OleCreatePictureIndirect` from `oleaut32.dll`, with **`PICTYPE_ICON` (3) and `Bitmap.GetHicon()`**. `PICTYPE_BITMAP` with `GetHbitmap()` also loads and shows, but it loses transparency (see below). `AxHost.GetIPictureDispFromPicture` was not needed, so it was not tried.
     ```csharp
     [StructLayout(LayoutKind.Sequential)]
     struct PICTDESC { public int cbSizeofstruct; public int picType; public IntPtr handle; public IntPtr hpal; }

     [DllImport("oleaut32.dll", PreserveSig = false)]
     static extern void OleCreatePictureIndirect(ref PICTDESC desc, ref Guid riid,
         [MarshalAs(UnmanagedType.Bool)] bool own, [MarshalAs(UnmanagedType.IUnknown)] out object picture);

     // bitmap: a 32bpp ARGB PNG loaded from an embedded resource with new Bitmap(stream)
     var desc = new PICTDESC { cbSizeofstruct = Marshal.SizeOf<PICTDESC>(), picType = 3 /* PICTYPE_ICON */, handle = bitmap.GetHicon() };
     Guid iid = new Guid("7BF80981-BF32-101A-8BBB-00AA00300CAB"); // IPictureDisp
     OleCreatePictureIndirect(ref desc, ref iid, true, out object picture);
     ```
   - `AddButtonDefinition` accepts the result as a plain `object` for `StandardIcon` and `LargeIcon`.
   - small 16 px shows: **yes**, on the ribbon in both themes.
   - large 32 px shows: **yes**, sharp (not a scaled-up 16 px), when the button is added with `useLargeIcon: true`.
   - transparency kept: **only with `GetHicon`/`PICTYPE_ICON`.** With `GetHbitmap`/`PICTYPE_BITMAP` the anti-aliased edge showed a white outline on the dark ribbon. With `GetHicon` the edge was clean. Both icons were easy to see in both themes.
   - setters on an existing definition: **exist and work.** `ButtonDefinition.StandardIcon` and `ButtonDefinition.LargeIcon` are typed `stdole.IPictureDisp`, so the add-in needs a reference to `stdole` (`$(InventorInstallDir)\Bin\stdole.dll`, `Private=false`). Without it, the build fails with CS0012. The Settings button was defined with `Type.Missing` icons and given its icons only through the setters. They showed at both sizes, with no error.
     ```csharp
     definition.StandardIcon = (stdole.IPictureDisp)picture16;
     definition.LargeIcon = (stdole.IPictureDisp)picture32;
     ```
     The test did not exercise the "reuse" branch (`defs[InternalName]` returning a definition). Every Inventor start made new definitions (`existing definition reused = False` in the log). The setters work on a live definition, so the same calls should cover that branch.
4. Dark title bar (Windows 11 Pro 25H2, build 26200.9457): **works.** `DwmSetWindowAttribute(hwnd, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref 1, sizeof(int))` returned `S_OK`, and the About title bar was dark. There was no flicker, no wrong size and no error. The handle came from `new WindowInteropHelper(window).EnsureHandle()`, called just before `ShowDialog()`, after the owner was set.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx` on Windows with Inventor closed (succeeded and deployed), then manual tests in Inventor 2026 for each question. Values were read from `awbaddin.log`.
- **Not compiled (changed under `src/InventorAddin`):** none committed. The spike code compiled on Windows, but it stays on the local branch `spike/020`.
- **Inventor API members not confirmed:** none. These all compiled and were confirmed: `Application.ThemeManager`, `ThemeManager.ActiveTheme`, `ThemeManager.Themes`, `Theme.Name`, `ButtonDefinition.StandardIcon`, `ButtonDefinition.LargeIcon` (typed `stdole.IPictureDisp`, needs a `stdole` reference).
- **Manual checklist for Inventor:** questions 1 to 4 above, all checked by Aidan.

## Follow-ups

Things noticed but not done.

- Task 030 (ribbon icons) must use `GetHicon` with `PICTYPE_ICON`, not `GetHbitmap`, or the icons get a white edge on the dark ribbon. It also needs the `stdole` reference if it sets icons through the setters, for example on a theme change.
- Aidan asked whether the ribbon icons could be based on his website favicon, in blue shades instead of green. Consider this when the icon set is designed or reviewed. He is also happy with other designs.
- The deployed add-in in `%APPDATA%\Autodesk\Inventor 2026\Addins` is still the spike build. Rebuild `main` with Inventor closed to replace it.
