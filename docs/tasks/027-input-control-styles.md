# 027: Input control styles: text box, dropdown, checkbox

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (The look: Colours, Spacing and size, Buttons)
- **Status:** todo
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

- [ ] Implicit `TextBox` style: `InputBackground` fill, `InputBorder` 1 px border, radius 4, height 30, `TextStrong` text, caret in `TextStrong`, text vertically centred with a little inner padding, `UpdateSourceTrigger` left to the window. Keyboard focus shows the 2 px `Accent` outline from 026
- [ ] Disabled `TextBox`: `DisabledBorder` border, `DisabledText` text, no fill change that makes it look like read-only text (a disabled box still reads as a box that cannot be used now)
- [ ] `NarrowTextBox` style (based on the implicit one) 120 wide, for Cost
- [ ] Implicit `ComboBox` style with a full control template, so the closed box, the arrow, the drop-down list, the hover and the selected entry all use the shared colours in both themes (the default WPF template keeps system colours and looks wrong in dark). Height 30, radius 4, the focus outline, and a disabled look matching the text box. The drop-down list has `InputBackground` fill and `InputBorder` border; the highlighted entry uses a fill that reads in both themes without a new token (for example `Divider`)
- [ ] `CodeComboBox` style (based on the implicit one), narrow enough for a two-letter code plus the arrow, with a drop-down list wide enough for "code: full name" entries (the list may be wider than the box)
- [ ] `SideNoteText` style: `MutedText` with an 8 px gap before it, for the full name shown beside the part type dropdown
- [ ] Implicit `CheckBox` style: a box drawn with `InputBorder` and `InputBackground`, a check mark in `TextStrong`, the label in `TextPrimary`, the focus outline on the box, and a disabled look
- [ ] All new references to brushes and to 026's focus style are `DynamicResource`
- [ ] No window changes, no colour values outside the colour dictionaries
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists `Styles.xaml` as not compiled

## Notes for the implementer

- Base the ComboBox template on the structure of WPF's default (a `ToggleButton` covering the box, a `ContentPresenter` for the selection, a `Popup` holding an `ItemsPresenter` in a `ScrollViewer`) with `ComboBoxItem` styled too. Keep it as short as that structure allows. Part Properties uses a non-editable combo only; an editable one does not need styling now (say so in a comment).
- The Part Properties window shows the code in the closed box and "code: full name" in the list through an item template (task 019). That template stays in the window; the style must not override `ItemTemplate`.
- Nothing here can be seen until 028 and 029 use it, so the manual checks for these styles are in those tasks.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:** none (WPF only); list any WPF member or template part you could not confirm
- **Manual checklist for Inventor:** none here; covered by 028 and 029

## Follow-ups

Things noticed but not done.
