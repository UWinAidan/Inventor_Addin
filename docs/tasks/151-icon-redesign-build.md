# 151: Icon redesign: window icons, Outline chosen state, ribbon option C, and the 7j AWB Hole icon

- **Milestone:** M1b (window style and icons, spec 08), after M4 part 1 is merged
- **Feature spec:** `docs/features/08-window-style.md`, `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** 148 (mockup, approved by Aidan 2026-11-09); PR #29 merged to `main`
- **Needs:** windows build, **Python with Pillow** (icon renders)
- **Parallel-safe with:** none
- **Branch:** a new branch off `main` after PR #29 is merged (Aidan, 2026-11-09). Not on `claude/m4-hole-generator`.

## Goal

Build the icon redesign Aidan approved from the task 148 mockup on 2026-11-09 (`DECISIONS.md`):

- **Window icons:** the new look from `docs/ui-mockups/awb-icons-redesign.png` (section A). The icons are full colour with a solid hatched material fill and a firmer outline. In dark mode they use the blue shown.
- **Chosen state:** the **Outline** version from section B: the input fill with a 2 px dark blue border.
- **Ribbon:** **option C**, an accent colour by panel plus tinted shapes, in the new style (sections D and E).
- **AWB Hole icon:** Aidan's design **7j**, built from his files in `docs/ui-mockups/awb-hole-icon-7j/` (light and dark SVGs at 32 x 32, plus 128 px previews). It is an isometric half-section of a grey block through a counterbored blind hole with a drill point. The hole's interior is light green, it has no edge line, and it is turned toward the cut face. Use those files' shapes and colours exactly, **do not redraw them**. 7j is used for the ribbon button, the split button, the mini toolbar entry and the window header tile.

## Scope

- `src/InventorAddin/UI/Theme/` (`Styles.xaml` and the theme colour dictionaries): the new hole icon tokens and template (hatch clipped to the material), and the chosen state on `IconChoiceItem` and `SegmentedChoiceItem`. Colours come from section C of the mockup (`docs/ui-mockups/awb-icons-redesign.html` holds the hex values).
- `src/InventorAddin/UI/Icons/` and `tools/icons/`:
  - The ribbon icons are redrawn in option C, at 16 and 32 px, light and dark.
  - `AwbHole` is replaced by 7j. The 32 px renders come from Aidan's SVGs. A 16 px version is needed for the small ribbon and split button sizes. Halve 7j only if it stays readable; otherwise draw a pixel-fitted 16 px simplification from the same shapes and colours, and show it to Aidan before shipping.
  - `render.py` gains what it needs: more than one fill colour per icon, and polygons and paths with fills.
  - Strip the `<metadata>` (C2PA manifest) from rendered and shipped copies; keep Aidan's source files as they are.
- The window header tile for AWB Hole uses 7j (`WindowHost`'s `HeaderIcon`).
- Spec 08: answer question 3 (icon style) with this decision; describe the tokens and the chosen state. Spec 09: name 7j as the AWB Hole icon.

Out of scope:

- Icon shapes for the window rows. Section A keeps today's shapes; only colour, fill and outline change.
- Behaviour changes.

## Acceptance criteria

- [ ] Window icons and chosen states match the mockup's Outline version in light and dark, at 100 and 150 percent.
- [ ] Ribbon icons match option C in the new style, light and dark, 16 and 32 px.
- [ ] 7j is the AWB Hole icon on the ribbon button, the split button, the mini toolbar entry and the header tile. Its 32 px render matches Aidan's SVGs.
- [ ] No shipped icon carries the C2PA metadata.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Read task 148's Follow-ups first; they list what this build needs. The mini toolbar and the split button show the icon of our `ButtonDefinition`, so check that they pick up the new pictures, and that the ring (task 142) shows 7j too. The focus ring next to the Outline border must still be visible; check it in Inventor.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups
