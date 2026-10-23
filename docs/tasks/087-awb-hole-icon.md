# 087: Ribbon and window icon for AWB Hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/08-window-style.md` ("Ribbon icons")
- **Status:** done
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

- [x] 32 px and 16 px SVGs in the house style (task 024 and 073 rules): our own drawing, for example a block in section with a drilled hole and its drill point, distinct from every other icon and from Inventor's Hole icon.
- [x] Rendered to Light and Dark at both sizes; a second run gives the same new files; existing PNGs restored if Pillow re-encodes them (task 073 follow-up).
- [x] `IconNames.AwbHole` exists; `RibbonIconsTests` finds its PNGs.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- If the ribbon colour refresh (branch `claude/ribbon-icon-colour`) is decided first, draw this icon to match the chosen option; otherwise draw it plain and it is recoloured with the rest.

## Verification

Filled in by the implementer.

- **Ran:**
  - `python tools/icons/render.py` twice (Python 3.12.10, Pillow 12.3.0): "wrote 48 icons" both times; the two runs gave byte-identical PNGs and preview (md5 compared).
  - The re-render re-encoded 12 existing 32 px PNGs (About.Dark, CreatePrint.Light, ExportLibraries.Dark/Light, ExportModelData.Light, MassUpdate.Light, PartProperties.Dark/Light, SaveAsCopy.Dark/Light, Settings.Dark/Light). Their decoded RGBA pixels matched HEAD exactly, so they were restored with `git checkout`; only the four new `AwbHole.*.png` and the preview are new or changed.
  - `dotnet build src/InventorAddin.Core`: 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 2795 passed, 0 failed (65 of them in `RibbonIcons*`, including the test that finds a PNG for every `IconNames` field, so it covers `AwbHole`).
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors, 8 warnings (none from this task's files). The PNGs are picked up by the existing `UI\Icons\*.png` embedded-resource glob; no project file change.
- **Not compiled (changed under `src/InventorAddin`):** none. Only the SVG sources and PNGs were added there, and the add-in solution built on Windows.
- **Inventor API members not confirmed:** none; no Inventor API code in this task.
- **Manual checklist for Inventor:** nothing to check in Inventor yet. No button uses `IconNames.AwbHole` until the AWB Hole command task adds its layout entry. Then: open any part, look at the AWB Hole large button (where Inventor's Hole was) on both the light and dark theme, and open its window to see the icon in the header tile. Until then, review `docs/ui-mockups/ribbon-icons.png` (last row in each panel).

## Follow-ups

- **Icon colour.** Drawn plain, in the current single-colour style (`#3B4350` / `#E3E7EE`), because Aidan has not chosen between options A, B and C on branch `claude/ribbon-icon-colour`. When that is decided, recolour `AwbHole.svg` and `AwbHole.16.svg` with the rest.
- **16 px has no section hatching.** The solid either side of the hole is three pixels wide, so a 45 degree hatch line cannot avoid touching the outline or the drill point; the drafts tried read as noise. The 16 px icon is the block outline with the hole and drill point only. The button is large, so the 16 px icon only shows if the ribbon shrinks it; Aidan may want to look at it in the preview.
- **Pillow re-encodes untouched PNGs** with Pillow 12.3.0 (as task 073 noted). `render.py` could skip writing a PNG whose decoded pixels already match the file on disk, so a re-render leaves untouched icons alone.
