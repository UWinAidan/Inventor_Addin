# 087: Ribbon and window icon for AWB Hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/08-window-style.md` ("Ribbon icons")
- **Status:** todo
- **Depends on:** none
- **Needs:** Python 3 with Pillow (installed on Aidan's PC since task 073)
- **Parallel-safe with:** 082 to 086

## Goal

An icon named `AwbHole`, drawn in the style of the existing ones, for the AWB Hole button (large, where Inventor's Hole was) and the window header.

## Scope

- `src/InventorAddin/UI/Icons/AwbHole.svg`, `AwbHole.16.svg` and the four PNGs
- `src/InventorAddin.Core/Ribbon/` `IconNames.AwbHole`
- `tools/icons/render.py` (`ICONS`), `tools/icons/README.md`, `docs/ui-mockups/ribbon-icons.png`

## Acceptance criteria

- [ ] 32 px and 16 px SVGs in the house style (task 024 and 073 rules): our own drawing, for example a block in section with a drilled hole and its drill point, distinct from every other icon and from Inventor's Hole icon.
- [ ] Rendered to Light and Dark at both sizes; a second run gives the same new files; existing PNGs restored if Pillow re-encodes them (task 073 follow-up).
- [ ] `IconNames.AwbHole` exists; `RibbonIconsTests` finds its PNGs.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- If the ribbon colour refresh (branch `claude/ribbon-icon-colour`) is decided first, draw this icon to match the chosen option; otherwise draw it plain and it is recoloured with the rest.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups
