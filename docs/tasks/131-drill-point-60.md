# 131: A 60 deg drill point

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Blind holes")
- **Status:** todo
- **Depends on:** 111, 119
- **Needs:** **Python with Pillow** for the icon, then cloud or windows (Core and the starter workbook)
- **Parallel-safe with:** none (the starter workbook and `render.py` are shared with 111, 119, 132)

## Goal

Aidan, 2026-10-25: a **60 deg** drill point option, with its own icon, alongside 118, 90 and Flat.

## Scope

- `libraries/AWB_Holes.xlsx` (starter): a 60 deg row on the Drill Point tab, Default = N. Edit the package as tasks 097, 106 and 111 did.
- `src/InventorAddin.Core/Holes/HoleIcons.cs`: `ForDrillPoint` today maps every angle under the shallow threshold to `PointSteep`, so 60 and 90 would share an icon. Give 60 its own name (for example `PointSharp`, Default) and map by nearest of 60, 90, 118, so each of the three angles has its own icon; tests.
- `tools/icons/render.py` and the rendered PNGs, light and dark; the `Styles.xaml` key (task 119's test checks it). The three cones must be clearly different (Aidan, 2026-10-23).
- `HoleDiagram.cs`: draws the 60 deg point (probably already from the angle; confirm with a test).

## Acceptance criteria

- [ ] The starter workbook lists 60 deg; Aidan's own workbook gets it when he adds the row.
- [ ] 60, 90 and 118 each have their own clearly different icon.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Blind hole, Drill point 60: the icon is its own, the model and the diagram show a 60 deg point.

## Follow-ups
