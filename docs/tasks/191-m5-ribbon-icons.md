# 191: Ribbon icons for the M5 commands

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` and `docs/features/08-*.md`
- **Status:** done
- **Depends on:** none
- **Needs:** **Python with Pillow** (as task 073)
- **Parallel-safe with:** 189, 190

## Goal

Icons for every new M5 button, drawn by the existing icon tooling, with a preview PNG for Aidan.

## Scope

- `tools/icons/`: icons for Open Drawing, Place Initial Views, Check Out of Bounds, Update Notes, Part Marking, Batch Print, Overall REF Dimensions, Stock Size, Update Parts List; small and large, light and dark as the existing set.
- `src/InventorAddin/UI/Icons/` (generated files) and `src/InventorAddin.Core/Ribbon/RibbonIcons.cs` / `IconNames`.
- `docs/ui-mockups/m5-icons-preview.png`.

Out of scope: adding the buttons (each command's task does).

## Acceptance criteria

- [x] Every icon follows the existing style and sizes; the preview shows them all at both sizes on light and dark.
- [x] No icon copies another product's artwork.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Aidan plans a polish of every icon later (TODO, 2026-10-25); match today's style, do not redesign.
- If Python is not available, stop and say so; later tasks then use an existing icon until this one runs.
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

- **Ran:** `python tools/icons/render.py` (Python 3.12.10, Pillow 12.3.0): wrote 84 PNGs and both previews; the 48 existing PNGs came out byte-identical (git shows no change). `dotnet build src/InventorAddin.Core`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5280 passed, 0 failed. I looked at `docs/ui-mockups/m5-icons-preview.png`: all nine at 16 and 32 px on light and dark.
- **Not compiled (changed under `src/InventorAddin`):** only new SVG and PNG files in `UI/Icons/` (the PNGs are embedded by the existing `UI\Icons\*.png` glob). No .cs file under `src/InventorAddin` changed. Nothing built on Windows.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. The icons are not on any button yet; each command's task adds its button. When one does, check its ribbon icon at small and large size in Inventor's light theme and in its dark theme: clear, not cut off at the edges, accent colour readable.
  2. Open `docs/ui-mockups/m5-icons-preview.png` and check the nine icons read as their command (Open Drawing, Place Initial Views, Check Out of Bounds, Update Notes, Part Marking, Batch Print, Overall REF Dimensions, Stock Size, Update Parts List).

Icon names (in `IconNames`): OpenDrawing, PlaceInitialViews, CheckOutOfBounds, UpdateNotes, PartMarking, BatchPrint, OverallRefDimensions, StockSize, UpdatePartsList. Stock Size uses the Properties (blue) panel colours because it writes a property; the rest use the Drawing (orange) colours.

## Follow-ups

- `render.py` now writes `ribbon-icons.png` with all 21 icons (it was 12) plus `m5-icons-preview.png` with the M5 nine. `BuildPrintTemplate` and `HoleTable` (placeholder PNG copies, no SVG sources) are not in the renderer; left as the brief says.
- The 16 px Overall REF Dimensions and Stock Size dimension marks are small; look at them in Inventor and polish with the later icon polish (TODO 2026-10-25).
