# 148: AWB icon redesign and light-mode colours: mockup for Aidan

- **Milestone:** M4 part 1 (window icons); applies to M1b's ribbon icons too
- **Feature spec:** `docs/features/08-window-style.md`, `docs/features/09-awb-hole.md`
- **Status:** done
- **Depends on:** 119
- **Needs:** image only (Python with Pillow, or an HTML mockup rendered to PNG)
- **Parallel-safe with:** every other task (no source code)

## Goal

Aidan's check 118-10 (2026-11-09, with screenshots): the AWB Hole window's icons are hard to make out in both themes. In light mode, a dark blue for the icons and for the chosen-button state could fix it. This folds into the icon redesign already planned for later: every AWB icon gets a more polished look, closer to Inventor's (Aidan, 2026-10-25, `TODO.md` "Next up" 2). Aidan wants a **mockup first**. This task makes the mockup only; building it is a later task, after Aidan approves.

## Scope

- `docs/ui-mockups/awb-icons-redesign.png` (and its source, for example `awb-icons-redesign.html` or a script under `tools/icons/`). It shows:
  - The AWB Hole window's icon rows (Type, Seat, Drill point, Units, and the like), today's icons next to the redesign, in light and dark themes at 100 and 150 percent.
  - The chosen-button state today and redesigned, with a dark blue for light mode.
  - The ribbon icons (AWB Hole and the other AWB buttons) in the same style, beside Inventor's ribbon at the same size, if that can be done without screenshots of Inventor. Describe Inventor's style in words instead.
  - The three ribbon colour options from `docs/ui-mockups/ribbon-icons-colour-options.png` on branch `claude/ribbon-icon-colour`, redone in the new style, so Aidan picks the colour and the look together.
- `docs/ui-mockups/README.md`: one line.

Out of scope:

- Changing `Styles.xaml`, icon resources or ribbon images.
- Screenshots of other software (CLAUDE.md).

## Acceptance criteria

- [x] One PNG Aidan can judge without running anything, plus its source.
- [x] Every icon used in the AWB Hole window appears, old and new.
- [x] Colours named by value (hex), ready for the build task.

## Notes for the implementer

Today's icon geometry is in `src/InventorAddin/UI/Styles.xaml` (task 119) and the ribbon images come from `tools/icons/` (task 087). Spec 08 question 3 (icon style) is still open, so this mockup is how Aidan answers it. Show contrast ratios against the window background for both themes.

## Verification

Filled in by the implementer.

- **Ran:** headless Edge (Windows) on `docs/ui-mockups/awb-icons-redesign.html` at 1600 wide, then the empty bottom trimmed with Pillow 12.3.0 (command in the HTML's top comment); looked over every section of the PNG. No .NET build or tests: no code changed.
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none; Aidan reviews `docs/ui-mockups/awb-icons-redesign.png` and answers the four picks at its foot (window icon look; Outline or Solid chosen state; ribbon option A, B or C in the new style; blue or neutral icons in dark mode).
- **Notes:** the hole icon geometry is unchanged (copied from `Styles.xaml`); only the template and colours differ. The ribbon "today" icons are the PNGs in `src/InventorAddin/UI/Icons`, loaded by relative path. Inventor's ribbon style is described in words, from memory, with no screenshot; the ribbon background colours are the colour-options image's stand-ins, not measured. The new 16 px ribbon icons are the 32 px drawings at half size, not pixel-fitted.

## Follow-ups

- Build task (after Aidan picks): new tokens `HoleIconInk`, `HoleIconMaterial` and the chosen-state colours in `Colors.Light.xaml` / `Colors.Dark.xaml`; the `HoleIcon` template gets a solid fill, a hatch Path clipped to the material geometry, and outline 1.3 / lines 1.1 / face 2.4; `IconChoiceItem` and `SegmentedChoiceItem` take the chosen state; the not-chosen icon is no longer muted.
- Dark-mode Solid cannot meet both 3:1 (fill against the window) and 4.5:1 (white Units text on it): #3A7AC6 gives 3.01:1 and 4.39:1. Outline has no trade-off.
- With Outline chosen, the keyboard focus ring (Accent #0F6CB0) sits 3 px outside a dark blue border in light mode; check in Inventor that the two read as separate.
- New ribbon icons need pixel-fitted 16 px drawings and `tools/icons/render.py` support for fills in more than one colour (today each PNG has exactly one colour), plus `paint-order`-style halos or an explicit halo shape for the New Part plus.
- Spec 08 question 3 (icon style) and the ribbon colour option in `TODO.md` are answered by Aidan's picks on this image.
