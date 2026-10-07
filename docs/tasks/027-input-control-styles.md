# 027: Input control styles: text box, dropdown, checkbox

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (The look: Colours, Spacing and size, Buttons)
- **Status:** done
- **Depends on:** 026
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 025

## Goal

The shared styles cover every input control the three windows use, in both themes, so 028 and 029 only lay out and bind.

## Scope

- `src/InventorAddin/UI/Theme/Styles.xaml` (additions)

Out of scope:

- Any window. Colours: the tokens from 026 are enough; do not add tokens. If one is truly missing, stop and record it (a new token is a spec change for Aidan).

## Acceptance criteria

- [x] Implicit `TextBox` style: `InputBackground` fill, `InputBorder` 1 px border, radius 4, height 30, `TextStrong` text, caret in `TextStrong`, text vertically centred with a little inner padding, `UpdateSourceTrigger` left to the window. Keyboard focus shows the 2 px `Accent` outline from 026
- [x] Disabled `TextBox`: `DisabledBorder` border, `DisabledText` text, no fill change that makes it look like read-only text (a disabled box still reads as a box that cannot be used now)
- [x] `NarrowTextBox` style (based on the implicit one) 120 wide, for Cost
- [x] Implicit `ComboBox` style with a full control template, so the closed box, the arrow, the drop-down list, the hover and the selected entry all use the shared colours in both themes (the default WPF template keeps system colours and looks wrong in dark). Height 30, radius 4, the focus outline, and a disabled look matching the text box. The drop-down list has `InputBackground` fill and `InputBorder` border; the highlighted entry uses a fill that reads in both themes without a new token (for example `Divider`)
- [x] `CodeComboBox` style (based on the implicit one), narrow enough for a two-letter code plus the arrow, with a drop-down list wide enough for "code: full name" entries (the list may be wider than the box)
- [x] `SideNoteText` style: `MutedText` with an 8 px gap before it, for the full name shown beside the part type dropdown
- [x] Implicit `CheckBox` style: a box drawn with `InputBorder` and `InputBackground`, a check mark in `TextStrong`, the label in `TextPrimary`, the focus outline on the box, and a disabled look
- [x] All new references to brushes and to 026's focus style are `DynamicResource`
- [x] No window changes, no colour values outside the colour dictionaries
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists `Styles.xaml` as not compiled

## Notes for the implementer

- Base the ComboBox template on the structure of WPF's default (a `ToggleButton` covering the box, a `ContentPresenter` for the selection, a `Popup` holding an `ItemsPresenter` in a `ScrollViewer`) with `ComboBoxItem` styled too. Keep it as short as that structure allows. Part Properties uses a non-editable combo only; an editable one does not need styling now (say so in a comment).
- The Part Properties window shows the code in the closed box and "code: full name" in the list through an item template (task 019). That template stays in the window; the style must not override `ItemTemplate`.
- Nothing here can be seen until 028 and 029 use it, so the manual checks for these styles are in those tasks.

## Verification

Filled in by the implementer.

- **Ran:** cloud session (`CLAUDE_CODE_REMOTE=true`). `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 673 passed, 0 failed, 0 skipped (no Core change in this task).
- **Extra check, not a build of the add-in:** the new `Styles.xaml` was copied into 026's throwaway `net8.0-windows` WPF project in the scratchpad (`EnableWindowsTargeting=true`, Inventor types stubbed), together with a throwaway sample window that uses `CodeComboBox`, `SideNoteText`, `NarrowTextBox`, a disabled `TextBox` and a `CheckBox` inside `FormSection`/`FieldRow`. A `--no-incremental` build had 0 warnings and 0 errors and produced BAML for every XAML file; a deliberately wrong trigger property (`IsHighlightedX` on `ComboBoxItem`) failed with MC4109, so the markup compiler checked the template triggers. This confirms the XAML compiles; nothing was run or shown.
- **Not compiled (changed under `src/InventorAddin`):** `UI/Theme/Styles.xaml` (in the add-in project itself; see the extra check above).
- **Inventor API members not confirmed:** none (WPF only). WPF behaviour that compiles but only a running window can show: `DynamicResource` brushes inside the `ComboBox` `Popup` resolving through the templated parent to the window's merged dictionaries (the standard WPF themes do the same, so this is expected to work); `FocusVisualStyle` set through `DynamicResource` to a style that arrives after the window is built; `CheckBoxFocusOutline` lining up with the box (it assumes the box sits at the left edge, centred vertically, as the template draws it).
- **Manual checklist for Inventor:** none here; covered by 028 and 029.

## Follow-ups

Things noticed but not done.

- **For 029: the Part type `ComboBox` sets its own `ComboBox.Style`** (for the tooltip and its blank-code trigger). An explicit style replaces the implicit one, so as written it would drop the new template and look. It cannot use `BasedOn="{StaticResource {x:Type ComboBox}}"` either, since the dictionaries arrive after `InitializeComponent`. 029 should set `Style="{DynamicResource CodeComboBox}"` and move the tooltip out of the style (for example a `ToolTip` binding with a converter or a view-model property that is null for a blank code). The same applies to the window's local `ReadOnlyField`/`EditableField` `TextBox` styles, which 029 replaces anyway.
- **One extra style key, `CheckBoxFocusOutline`.** The brief asks for the focus outline on the box; WPF's focus visual covers the whole control, so the checkbox has its own focus style drawn round the 16 px box only (same 2 px `Accent` outline as `FocusOutline`). 031's checks may want to know the key.
- **Choices made without a spec value:** the checkbox box is 16 px with corner radius 3 (a little under the controls' 4, so it reads as square) and an 8 px gap to its label; `CodeComboBox` is 68 wide; the dropdown entry under the mouse or keyboard gets the `Divider` fill, and the selected entry is semibold (no fill), so the two can be told apart; the closed dropdown and the checkbox box use the buttons' `TextPrimary` veil on hover. Disabled text box and dropdown keep the `InputBackground` fill and only dim the border and text. Worth a look in 028 and 029.
- **The dropdown's scroll bar** is WPF's default (system colours). It only appears when the list is longer than `MaxDropDownHeight`, which the part type list is not expected to be. A themed `ScrollBar` would be a separate small task if a long list ever needs one.
- **Editable dropdowns are not styled.** The template has no `PART_EditableTextBox`, as the brief allows; a comment in `Styles.xaml` says so.
- **Mouse focus.** As with the buttons, the outline shows for keyboard focus only (WPF focus visuals); a text box clicked with the mouse shows its caret but no outline. That matches the spec's "keyboard focus", but Aidan may expect an outline on click too.
- **(Review)** The implicit `ComboBoxItem` style reaches the Part type dropdown, which keeps its own `ComboBox.Style` until 029. In dark theme its entries are white on a white popup until 029 lands. Do not judge dark mode on a build between 027 and 029.
