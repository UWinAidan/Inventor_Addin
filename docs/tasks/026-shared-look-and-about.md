# 026: Shared look, theme from Inventor, and the About window

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (The look; Window layout; About; Following Inventor's theme; How it is built)
- **Status:** done
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

- [x] `Colors.Light.xaml` and `Colors.Dark.xaml` each define one `SolidColorBrush` per token in the spec's colour table, keyed `<Token>Brush` (for example `WindowBackgroundBrush`, `AccentBrush`), with the spec's values. Both files define exactly the same keys. Brushes are frozen (`PresentationOptions:Freeze="True"`)
- [x] No colour value appears anywhere else in the add-in's XAML or code (`Styles.xaml` refers to the brushes with `DynamicResource`). Holds for every file this task adds or rewrites; Settings and Part Properties keep their own colours until 028 and 029 (see Follow-ups)

Styles (`Styles.xaml`)

- [x] A window style keyed `DialogWindow`: `WindowBackgroundBrush` background, `TextPrimaryBrush` foreground, Segoe UI 13, `SizeToContent="WidthAndHeight"`, no resize, not in the taskbar
- [x] Text styles for the spec's type table: `HeaderTitleText` (16 semibold, `TextStrong`), `HeaderSublineText` (12, `TextMuted`), `SectionHeadingText` (11 semibold, upper case through the control, wide letter spacing, `SectionHeading`), `LabelText` (`TextLabel`), `ValueText` (`TextPrimary`), `IdentifierText` (Consolas 13), `PlaceholderText` (`TextPlaceholder`), `MutedText` (`TextMuted`), `StatusText` (12, `TextMuted`) and `ErrorStatusText` (12, `Error`)
- [x] `SelectableText` and `SelectableIdentifierText`: a borderless, transparent, read-only `TextBox` that looks like `ValueText` (or `IdentifierText`), can be selected and copied, wraps, and is not a Tab stop
- [x] Buttons: an implicit `Button` style for secondary buttons and a `PrimaryButton` style, both with a control template giving the spec's fills, borders, text, radius 4, height 30, minimum width 80, and the disabled look. Hover and pressed states are a small change of the same colours (for example a lighter or darker fill), with no new tokens
- [x] Keyboard focus: a focus visual style drawing a 2 px `Accent` outline, used by the button styles and available for 027
- [x] Every size in the spec's spacing table comes from `Styles.xaml` (or the controls' templates), not from a window

Shared pieces (`UI/Controls/`)

- [x] `DialogHeader`: icon tile (36 square, radius 6, `IconTile` fill) holding an optional `Icon` (`ImageSource`, empty for now), `Title` and `Subline`
- [x] `FormSection`: an optional heading with the 1 px `Divider` line under it, then its rows. With no heading, no heading or line is drawn
- [x] `FieldRow`: a label (100 wide, `LabelText`, supports an access key and a `Target`) and a value, 12 apart; rows 9 apart
- [x] `DialogFooter`: the status line on the left (`StatusText`, or `ErrorStatusText` when its `IsError` is true; collapsed when the text is empty) and the buttons on the right, 8 apart, in one row
- [x] Each piece is a small control whose look lives in `Styles.xaml` or its own XAML, and uses only the shared brushes and styles. Use the simplest WPF form for each (a `UserControl` or a `ContentControl`/`HeaderedContentControl` subclass with a style); say which in Follow-ups

Theme

- [x] `WindowHost.ShowDialog` reads Inventor's active theme name with `ComSafe.Get` (member from 020's Results), maps it with `UiThemes.FromInventorThemeName` (021), merges the colour dictionary and then `Styles.xaml` into the window's resources using the loading method 020 proved, and applies `DialogWindow` to the window (`SetResourceReference`, since an implicit `Window` style does not apply to derived window types)
- [x] If the theme cannot be read, light is used and one `WARN` line is logged; if a dictionary fails to load, the window still opens with the default WPF look and the error is logged, not shown
- [x] When the theme is dark and 020 showed the dark title bar works, `WindowHost` turns it on through `DwmSetWindowAttribute` (failure ignored and logged once). If 020 showed it does not work or is unsafe, leave it out and record that in Follow-ups
- [x] No window applies the theme itself

About window (spec "About")

- [x] Built from `DialogHeader` (title `Title`, subline `HeaderSubline`), one `FormSection` with no heading holding three `FieldRow`s: Built (`BuildDate`), Inventor version (`InventorRelease`), Log folder (`LogFolder`, in `SelectableIdentifierText`), and a `DialogFooter` with Close as the primary button (`IsDefault` and `IsCancel`)
- [x] The window XAML contains layout and bindings only: no colour, font size, font weight, margin, padding or width/height values
- [x] The `VersionLine`, `BuildDateLine`, `InventorVersionLine` and `LogFolderLine` properties and their tests are removed from `AboutViewModel`, since nothing binds to them
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- **Why `DynamicResource`.** `WindowHost` receives a window that is already built, so its dictionaries are merged after `InitializeComponent`. Every reference from a window or a shared piece to a shared style or brush must be a `DynamicResource`; a `StaticResource` would fail at construction. Implicit styles pick up after the merge (spike 020, question 1).
- Dictionaries reference brushes with `DynamicResource` too, so `Styles.xaml` works with either colour dictionary.
- Upper case for section headings: set it in the control (`FormSection` can upper-case its heading text), since `TextBlock` has no text-transform property.
- Keep `WindowHost` the one place that owns window setup. Put dictionary loading in `ThemeResources` so 030 can reuse it for the header icons.
- XAML files under the add-in project are compiled as `Page` by default with `UseWPF`; no project file change should be needed. If one is, say so.
- Check every WPF and Win32 member you use; nothing here compiles in the cloud. List any you could not confirm.

## Verification

Filled in by the implementer.

- **Ran:** cloud session (`CLAUDE_CODE_REMOTE=true`). `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 672 passed, 0 failed, 0 skipped (662 before: +9 in the new `SectionHeadingsTests`, +1 case for a blank log folder; the `*Line` assertions were rewritten onto the remaining properties, no test was dropped).
- **Extra check, not a build of the add-in:** the new WPF files (`UI/Theme/*`, `UI/Controls/*`, `AboutWindow.xaml`/`.xaml.cs`, `WindowHost.cs`, `ComSafe.cs`) were copied into a throwaway `net8.0-windows` WPF project in the scratchpad with `EnableWindowsTargeting=true`, referencing Core, with small stubs in place of `InventorHost`, `AddinServices` and the Inventor `ThemeManager`/`Theme` types. It built with 0 errors and produced BAML for all four XAML files; a deliberately wrong type name in `Styles.xaml` failed with MC3050, so the markup compiler really checked the XAML. This confirms the WPF members and the XAML compile, not the Inventor members, and nothing was run.
- **Not compiled (changed under `src/InventorAddin`):** `UI/Theme/Colors.Light.xaml`, `UI/Theme/Colors.Dark.xaml`, `UI/Theme/Styles.xaml`, `UI/Theme/ThemeResources.cs`, `UI/Controls/DialogHeader.cs`, `UI/Controls/FormSection.cs`, `UI/Controls/FieldRow.cs`, `UI/Controls/DialogFooter.cs`, `UI/WindowHost.cs`, `UI/AboutWindow.xaml` (in the add-in project itself; see the extra check above). No project file change was needed: the XAML files are picked up as `Page` by `UseWPF`.
- **Inventor API members not confirmed:** none. The only Inventor members used are `Application.ThemeManager`, `ThemeManager.ActiveTheme` and `Theme.Name`, all confirmed by spike 020, plus `MainFrameHWND` as before. Win32: `DwmSetWindowAttribute` with attribute 20, confirmed by spike 020.
- **WPF behaviour not confirmed (compiles, but only Inventor can show it):** a `DynamicResource` inside the `FocusOutline` template resolving from the adorner layer to the window's merged dictionaries; `pack://application:` loading of the three new dictionaries (spike 020 proved the method with one dictionary); `PresentationOptions:Freeze` with `mc:Ignorable` in compiled BAML; Segoe UI having a hair space (U+200A) for the section heading letter spacing (no window shows a heading yet).
- **Manual checklist for Inventor:**
  1. Inventor in its light theme: open About. It has the header (empty icon tile, "AWB Addin", "Version ..."), the three rows, and Close in the accent colour. Colours match the light column of the spec.
  2. Switch Inventor to its dark theme and open About again: the dark column, and the title bar is dark.
  3. Enter and Esc each close About. Tab shows the 2 px accent focus outline on Close, with a thin gap between the outline and the blue button.
  4. Hover and press Close: the fill changes slightly (lighter in dark, darker in light).
  5. The log folder can be selected with the mouse and copied with Ctrl+C. It is in Consolas and does not get a box or border on hover or click.
  6. The window fits its content with no empty band above the footer; the gap from the last row to Close is about the same as from the header to the first row.
  7. Settings and Part Properties still open and work (their restyle comes in 028 and 029). Their buttons now have the new secondary look. In dark theme some of their text (labels, the checkbox) may be dark on dark: expected until 028 and 029, but note anything that stops the window working.
  8. The log shows no errors from loading the dictionaries, no theme warning, and no dark title bar warning.

## Follow-ups

Things noticed but not done.

- **Spike 020 followed, no differences from the brief.** Pack URI loading (020 method a), theme member `ThemeManager.ActiveTheme.Name` (names `LightTheme` and `DarkTheme`, which the existing "contains dark" rule in `UiThemes.FromInventorThemeName` already maps correctly), and the dark title bar through `DwmSetWindowAttribute(20)` after `EnsureHandle()`, called after the owner is set. The remark in `UiTheme.cs` still says the rule waits for 020; it can be updated to name the two confirmed names (one line, Core, left out of this task's scope).
- **Question for Aidan: secondary button fill in dark.** The spec says secondary buttons are "transparent in dark and white in light", but no token is transparent in dark and white in light, and the task allows no new tokens (031 also expects exactly the sixteen). The secondary button uses `InputBackground` (white in light, `#1F232C` in dark, slightly darker than the window). The alternative is a new token, which is a spec change.
- **Letter spacing for section headings.** WPF's `TextBlock` has no letter-spacing property. `FormSection` shows `SectionHeadings.Format(Heading)` (new, in Core, tested): upper case (invariant) with a hair space (U+200A) between characters; the plain heading is the heading's automation name so screen readers do not spell it out. Check how it looks when 029 shows the first headings.
- **Forms of the shared pieces.** `DialogHeader` is a lookless `Control`; `FieldRow` is a `ContentControl` (the value is its content); `FormSection` and `DialogFooter` are `ItemsControl`s, so rows and buttons are given directly as children and stay in the window's logical tree (`ElementName` and `Target` bindings work). All templates are implicit styles in `Styles.xaml`. `DialogFooter` wraps each button in a `ContentPresenter` so its `ItemContainerStyle` gives the 8 px gap even to a button with its own style. If the dictionaries fail to load, `FieldRow` shows only its value and `DialogHeader` shows nothing (the window title still names the window); the window still opens.
- **Two styles beyond the brief's list.** `DialogContent` (window content padding 22/18 and a 360 to 560 width range, for the window's root panel, since a `Window`'s padding is not used by its default template) and `FocusOutline` (the focus visual style). `LabelText` targets `Label`, not `TextBlock`, so it can carry an access key and a `Target`.
- **Rows-and-section spacing.** Each `FieldRow` has 9 below it and `FormSection` adds 9, so sections and the footer are 18 apart without a first- or last-row special case. 027 and 029 should keep this when they add rows that are not `FieldRow`s.
- **`AboutWindow.xaml` keeps `SizeToContent`, `ResizeMode` and `ShowInTaskbar`,** repeating the `DialogWindow` style, so the window still sizes to its content if the styles fail to load. 031's test does not forbid these.
- **`Transparent` appears in `Styles.xaml`** (disabled button fill and the selectable text background). It is the spec's "transparent", not a colour of its own; 031 may want to allow it explicitly.
- **Settings and Part Properties** still contain their own colours (`Firebrick`, system brushes) and margins. 028 and 029 replace them.
- **Cloud compile check for WPF.** A scratch `net8.0-windows` WPF project with `EnableWindowsTargeting=true` compiles the add-in's XAML and WPF code on Linux when the Inventor types are stubbed. A permanent version (for example a check project that compiles `UI/` against stub types) could catch XAML and WPF errors in cloud tasks before Aidan builds. Proposed work, not done.
