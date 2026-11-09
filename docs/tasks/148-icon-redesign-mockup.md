# 148: AWB icon redesign and light-mode colours: mockup for Aidan

- **Milestone:** M4 part 1 (window icons); applies to M1b's ribbon icons too
- **Feature spec:** `docs/features/08-window-style.md`, `docs/features/09-awb-hole.md`
- **Status:** todo
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

- [ ] One PNG Aidan can judge without running anything, plus its source.
- [ ] Every icon used in the AWB Hole window appears, old and new.
- [ ] Colours named by value (hex), ready for the build task.

## Notes for the implementer

Today's icon geometry is in `src/InventorAddin/UI/Styles.xaml` (task 119) and the ribbon images come from `tools/icons/` (task 087). Spec 08 question 3 (icon style) is still open, so this mockup is how Aidan answers it. Show contrast ratios against the window background for both themes.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none; Aidan reviews the image.

## Follow-ups
