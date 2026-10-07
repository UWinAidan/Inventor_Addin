# 024: Ribbon icon drawings

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (Ribbon icons; open question 3)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 021, 022, 023, 025

## Goal

Our own icon for each of the five ribbon commands, as editable sources and as the 16 px and 32 px PNG files the add-in will load (030), in a light-theme and a dark-theme version, plus one preview image Aidan can look at without building.

## Scope

- `src/InventorAddin/UI/Icons/*.svg` (new): one source drawing per icon
- `src/InventorAddin/UI/Icons/*.png` (new): the rendered files
- `tools/icons/render.py` (new): renders the PNGs and the preview from the sources
- `tools/icons/README.md` (new): how to re-render, and the drawing rules below
- `docs/ui-mockups/ribbon-icons.png` (new): the preview

Out of scope:

- Any change to the add-in project file or code. 030 embeds and loads the files.

## Acceptance criteria

- [ ] Five icons, file stems matching `IconNames` from task 021: `PartProperties`, `Settings`, `About`, `ExportModelData`, `ExportLibraries`
- [ ] Rendered files named `{Name}.{Light|Dark}.{16|32}.png`: 20 files. `Light` is the version for Inventor's light theme (dark strokes), `Dark` the version for its dark theme (light strokes)
- [ ] PNGs are exactly 16×16 and 32×32, 32-bit with a transparent background, and nothing is drawn outside the frame
- [ ] Simple line icons with one visual weight across the set: about 1.5 px strokes at 32 px and 1 px at 16 px, aligned to the pixel grid so 16 px lines are crisp, not blurred across two pixels. Where a 32 px drawing does not read when shrunk, the source has a separate 16 px drawing
- [ ] Stroke colours: `#3B4350` for the light-theme set and `#E3E7EE` for the dark-theme set. No other colours
- [ ] Our own drawings, not traced or copied from Inventor or anyone else. Suggested subjects: Part Properties a tag or a sheet with lines; Settings a gear or sliders; About a circled `i`; Export Model Data a box with an arrow out; Export Libraries stacked books or layers with an arrow out
- [ ] `python3 tools/icons/render.py` regenerates every PNG and the preview from the sources with no manual step, and states at the top which tools it needs. Running it twice gives identical files
- [ ] The preview shows every icon at 16 and 32 px on Inventor-like backgrounds: light `#F3F4F6` with the light set, dark `#2B303B` with the dark set, each with the command's name beside it. Large enough to judge on a phone (render the preview at 2× or more, nearest-neighbour, so pixels stay sharp)
- [ ] `dotnet test tests/InventorAddin.Core.Tests` still passes (no code changes)

## Notes for the implementer

- Available in the cloud session: Python 3 with Pillow, and ImageMagick (`convert`). Check whether ImageMagick renders SVG well enough at 16 px; if not, keep the SVGs as the readable source and have `render.py` draw the same shapes with Pillow (supersample, then downscale), with the shape coordinates in one place. Either way the committed PNGs are what the add-in uses; say which route you took in `tools/icons/README.md`.
- Keep files small: no metadata, no colour profile.
- Two colour sets is the default for open question 3 ("one drawing if it reads on both" is the spec's alternative). If you find one colour that reads well on both backgrounds, say so in Follow-ups; do not change the file naming.
- The window header tile (spec: 36 px tile) will show the 32 px icon of the window's command, so the drawings should also read on the tile colours `#DDE1E7` (light) and `#3A4150` (dark). Include those in the preview.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none (image files only, not yet referenced by the project)
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (covered by 030). Aidan: look at `docs/ui-mockups/ribbon-icons.png`.

## Follow-ups

Things noticed but not done.
