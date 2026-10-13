# 073: Ribbon and window icon for Material and Finish

- **Milestone:** M3
- **Feature spec:** `docs/features/08-window-style.md` ("Ribbon icons"); `docs/features/03-material-and-finish.md`
- **Status:** todo
- **Depends on:** none
- **Needs:** **Python 3 with Pillow** (a cloud session, or Python installed on Aidan's PC: this PC had none on 2026-10-12)
- **Parallel-safe with:** 068 to 072

## Goal

An icon named `MaterialFinish`, drawn in the style of the existing ones, so the Material and Finish button (075) has one.

## Scope

- `src/InventorAddin/UI/Icons/MaterialFinish.svg`, `MaterialFinish.16.svg` and the four PNGs
- `src/InventorAddin.Core/Ribbon/` `IconNames` (add `MaterialFinish`)
- `tools/icons/render.py` (its `ICONS` list), `tools/icons/README.md`, `docs/ui-mockups/ribbon-icons.png`

## Acceptance criteria

- [ ] 32 px and 16 px SVGs in the style of task 024 and 042 (simple line icons, one weight, our own drawing), for example a material swatch or a part with a finish stripe; distinct from the other icons
- [ ] Rendered with `python3 tools/icons/render.py` to Light and Dark PNGs at both sizes; a second run gives byte-identical files; the other PNGs are unchanged
- [ ] `IconNames.MaterialFinish` exists; `RibbonIconsTests` finds its PNGs
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read `tools/icons/README.md` first: the renderer reads only the SVG features it lists.
- Task 042 is the model for this task.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Look at `docs/ui-mockups/ribbon-icons.png`: does the new icon read, beside the others?

## Follow-ups

Things noticed but not done.
