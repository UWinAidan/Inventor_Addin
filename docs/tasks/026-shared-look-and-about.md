# 026: Shared look, theme from Inventor, and the About window

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (The look; Window layout; About; Following Inventor's theme; How it is built)
- **Status:** todo
- **Depends on:** 020 (its Results must be filled in), 021, 023
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 025

## Goal

The shared look exists in one place and `WindowHost` applies it, in Inventor's light or dark theme, to every window it shows. The About window is the first window built from it. Settings and Part Properties follow in 028 and 029, once 027 has added the input control styles.

## Before starting

Read the Results of task 020. If the spike showed that a step below does not work in Inventor (dictionary loading, the theme member or names, the dark title bar), follow what the spike found instead and say so in Follow-ups. If the Results are empty, stop: this task is not runnable yet.

## Scope

- `src/InventorAddin/UI/Theme/Colors.Light.xaml` and `Colors.Dark.xaml` (new)
- `src/InventorAddin/UI/Theme/Styles.xaml` (new): everything except the input controls, which 027 adds
- `src/InventorAddin/UI/Theme/ThemeResources.cs` (new): loads the dictionaries for a `UiTheme`
- `src/InventorAddin/UI/Controls/` (new): the shared header, section, field row and footer pieces
- `src/InventorAddin/UI/WindowHost.cs` (applies the theme)
- `src/InventorAddin/UI/AboutWindow.xaml` and `.xaml.cs` (rewritten on the shared look)
- `src/InventorAddin.Core/ViewModels/AboutViewModel.cs` and its tests (remove the `*Line` properties once nothing binds to them)

Out of scope:

- Text box, dropdown and checkbox styles (027). The Settings and Part Properties windows (028, 029): they keep their current XAML in this task and must still open, unstyled or partly styled, without errors.
- Icons in the header tile (030). The tile is drawn, empty.

## Acceptance criteria

Colours

- [ ] `Colors.Light.xaml` and `Colors.Dark.xaml` each define one `SolidColorBrush` per token in the spec's colour table, keyed `<Token>Brush` (for example `WindowBackgroundBrush`, `AccentBrush`), with the spec's values. Both files define exactly the same keys. Brushes are frozen (`PresentationOptions:Freeze="True"`)
- [ ] No colour value appears anywhere else in the add-in's XAML or code (`Styles.xaml` refers to the brushes with `DynamicResource`)

Styles (`Styles.xaml`)

- [ ] A window style keyed `DialogWindow`: `WindowBackgroundBrush` background, `TextPrimaryBrush` foreground, Segoe UI 13, `SizeToContent="WidthAndHeight"`, no resize, not in the taskbar
- [ ] Text styles for the spec's type table: `HeaderTitleText` (16 semibold, `TextStrong`), `HeaderSublineText` (12, `TextMuted`), `SectionHeadingText` (11 semibold, upper case through the control, wide letter spacing, `SectionHeading`), `LabelText` (`TextLabel`), `ValueText` (`TextPrimary`), `IdentifierText` (Consolas 13), `PlaceholderText` (`TextPlaceholder`), `MutedText` (`TextMuted`), `StatusText` (12, `TextMuted`) and `ErrorStatusText` (12, `Error`)
- [ ] `SelectableText` and `SelectableIdentifierText`: a borderless, transparent, read-only `TextBox` that looks like `ValueText` (or `IdentifierText`), can be selected and copied, wraps, and is not a Tab stop
- [ ] Buttons: an implicit `Button` style for secondary buttons and a `PrimaryButton` style, both with a control template giving the spec's fills, borders, text, radius 4, height 30, minimum width 80, and the disabled look. Hover and pressed states are a small change of the same colours (for example a lighter or darker fill), with no new tokens
- [ ] Keyboard focus: a focus visual style drawing a 2 px `Accent` outline, used by the button styles and available for 027
- [ ] Every size in the spec's spacing table comes from `Styles.xaml` (or the controls' templates), not from a window

Shared pieces (`UI/Controls/`)

- [ ] `DialogHeader`: icon tile (36 square, radius 6, `IconTile` fill) holding an optional `Icon` (`ImageSource`, empty for now), `Title` and `Subline`
- [ ] `FormSection`: an optional heading with the 1 px `Divider` line under it, then its rows. With no heading, no heading or line is drawn
- [ ] `FieldRow`: a label (100 wide, `LabelText`, supports an access key and a `Target`) and a value, 12 apart; rows 9 apart
- [ ] `DialogFooter`: the status line on the left (`StatusText`, or `ErrorStatusText` when its `IsError` is true; collapsed when the text is empty) and the buttons on the right, 8 apart, in one row
- [ ] Each piece is a small control whose look lives in `Styles.xaml` or its own XAML, and uses only the shared brushes and styles. Use the simplest WPF form for each (a `UserControl` or a `ContentControl`/`HeaderedContentControl` subclass with a style); say which in Follow-ups

Theme

- [ ] `WindowHost.ShowDialog` reads Inventor's active theme name with `ComSafe.Get` (member from 020's Results), maps it with `UiThemes.FromInventorThemeName` (021), merges the colour dictionary and then `Styles.xaml` into the window's resources using the loading method 020 proved, and applies `DialogWindow` to the window (`SetResourceReference`, since an implicit `Window` style does not apply to derived window types)
- [ ] If the theme cannot be read, light is used and one `WARN` line is logged; if a dictionary fails to load, the window still opens with the default WPF look and the error is logged, not shown
- [ ] When the theme is dark and 020 showed the dark title bar works, `WindowHost` turns it on through `DwmSetWindowAttribute` (failure ignored and logged once). If 020 showed it does not work or is unsafe, leave it out and record that in Follow-ups
- [ ] No window applies the theme itself

About window (spec "About")

- [ ] Built from `DialogHeader` (title `Title`, subline `HeaderSubline`), one `FormSection` with no heading holding three `FieldRow`s: Built (`BuildDate`), Inventor version (`InventorRelease`), Log folder (`LogFolder`, in `SelectableIdentifierText`), and a `DialogFooter` with Close as the primary button (`IsDefault` and `IsCancel`)
- [ ] The window XAML contains layout and bindings only: no colour, font size, font weight, margin, padding or width/height values
- [ ] The `VersionLine`, `BuildDateLine`, `InventorVersionLine` and `LogFolderLine` properties and their tests are removed from `AboutViewModel`, since nothing binds to them
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- **Why `DynamicResource`.** `WindowHost` receives a window that is already built, so its dictionaries are merged after `InitializeComponent`. Every reference from a window or a shared piece to a shared style or brush must be a `DynamicResource`; a `StaticResource` would fail at construction. Implicit styles pick up after the merge (spike 020, question 1).
- Dictionaries reference brushes with `DynamicResource` too, so `Styles.xaml` works with either colour dictionary.
- Upper case for section headings: set it in the control (`FormSection` can upper-case its heading text), since `TextBlock` has no text-transform property.
- Keep `WindowHost` the one place that owns window setup. Put dictionary loading in `ThemeResources` so 030 can reuse it for the header icons.
- XAML files under the add-in project are compiled as `Page` by default with `UseWPF`; no project file change should be needed. If one is, say so.
- Check every WPF and Win32 member you use; nothing here compiles in the cloud. List any you could not confirm.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Inventor in its light theme: open About. It has the header (empty icon tile, "AWB Addin", "Version ..."), the three rows, and Close in the accent colour. Colours match the light column of the spec.
  2. Switch Inventor to its dark theme and open About again: the dark column, and the title bar is dark if the task turned that on.
  3. Enter and Esc each close About. Tab shows the 2 px accent focus outline on Close.
  4. The log folder can be selected with the mouse and copied with Ctrl+C.
  5. The window fits its content with no empty band above the footer.
  6. Settings and Part Properties still open and work (their restyle comes in 028 and 029). Note anything that looks broken.
  7. The log shows no errors from loading the dictionaries.

## Follow-ups

Things noticed but not done.
