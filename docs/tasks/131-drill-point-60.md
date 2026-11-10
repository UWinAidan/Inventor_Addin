# 131: A 60 deg drill point

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Blind holes")
- **Status:** done
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

- [x] The starter workbook lists 60 deg; Aidan's own workbook gets it when he adds the row.
- [x] 60, 90 and 118 each have their own clearly different icon.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

- **Ran (Windows, Aidan's PC):**
  - `dotnet build src/InventorAddin.Core`: 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4120 passed, 0 failed, 0 skipped.
  - `dotnet build InventorAddin.slnx` (Inventor was not running, so the deploy step ran): 0 errors, 8 warnings, all existing CS8600 warnings in files this task did not touch.
- **Icons: render.py and PNGs do not apply.** The brief lists `tools/icons/render.py` and rendered light and dark PNGs, but `render.py` renders the ribbon icons only. The AWB Hole icons are XAML `Geometry` resources in `src/InventorAddin/UI/Theme/Styles.xaml`, drawn in the theme's colours, so light and dark need no separate files (the same as task 119). The new icon is `HoleIcon.PointSharp.Material`: the same 14 wide hole and the same tip as the 90 deg icon, the cone drawn at a true 60 deg so it starts almost at the top. Task 119's tests check that every `HoleIcons` name has its Material key, that no key is unknown and that no two icons have the same drawing; all pass. A rough preview of the four drill point drawings side by side (118, 90, 60, Flat) showed three clearly different cones.
- **Core:** `HoleIcons.PointSharp` added; `ForDrillPoint` maps to the nearest of 118, 90 and 60 deg with thresholds at the midpoints, 104 and 75. An angle exactly half way goes to the wider cone (75 deg shows the 90 deg icon), as 104 already did: a Default for the parent to log. The existing view-model test for an unknown "75 deg special" point still expects the 90 deg icon.
- **HoleDiagram:** already draws the point from the angle. New test: a 60 deg cone's depth over the hole width is 1/(2 tan 30), the 90 deg one 0.5, and the label reads 60 deg; a 60 deg case was added to the fits-the-box theory.
- **Workbook:** `libraries/AWB_Holes.xlsx` edited in its package XML with Python `zipfile` (script `add_60_131.py` in the session scratchpad). Only `xl/sharedStrings.xml`, `xl/worksheets/sheet9.xml` (Drill Point) and `xl/workbook.xml` changed; every other entry is byte-identical to HEAD and in the same order. The Drill Point tab now has 118 deg (Default Y), 90 deg, 60 deg (Default N, Show Y, note "Sharp 60 degree point."), Flat, in the order spec 09 lists them. The dimension, the autofilter and the `_FilterDatabase` name now cover A4:E8. The How to edit line for Drill Point names 60 deg. Two new shared strings (indexes 207, 208); the sst counts are updated. The starter workbook reader test now expects the four rows, their angles and 118 deg as the only default.
- **Stored hole data:** unchanged (version 4). The drill point is stored by its name, so a 60 deg hole needs no new field.
- **Model:** `HoleFeatureWriter` already passes any row's angle to Inventor as an angled drill point; no add-in code changed for it.
- **Not compiled (changed under `src/InventorAddin`):** none. `Styles.xaml` was changed and the add-in was built on Windows; it has not been loaded in Inventor yet.
- **Inventor API members not confirmed:** none (no Inventor API code changed).
- **Manual checklist for Inventor:**
  1. Open `libraries/AWB_Holes.xlsx` in Excel: no repair prompt; the Drill Point tab reads 118 deg, 90 deg, 60 deg, Flat, and its filter buttons cover all five columns down to Flat.
  2. Your own workbook: add a row `60 deg | 60 | N | Y | ...` to its Drill Point tab (or point Settings at the starter).
  3. Open a part, click AWB Hole, set Termination to Distance. Drill point shows four buttons: 118, 90, 60 and Flat, each with a different icon; the 60 icon is the tallest, narrowest cone.
  4. Choose 60 deg: the diagram shows a deeper point labelled 60 deg, and the preview hole in the model has a 60 deg point. OK, then edit the hole: 60 deg is still chosen.

## Follow-ups

- Spec 09's "Blind holes" section still reads "118 deg (default), 90 deg, or Flat"; the Fields table (row "Drill point") already lists 60 deg. The parent may want to bring the "Blind holes" line in step. (Done by the parent at commit.)
