# 185: Print template layout (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5", "Added for M5: Template additions", `docs/ui-mockups/title-block.png`)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 186

## Goal

The whole print template as data in Core: sheet sizes, border and zones, the title block's lines, fixed text and fields, and the parts list and revision table columns. Task 187 draws it in Inventor; tests here prove it fits and matches the approved mockup.

## Scope

- `src/InventorAddin.Core/Drawing/PrintTemplateLayout.cs` (and a few small records beside it, for example
  `SheetSizeSpec`, `TitleBlockLine`, `TitleBlockText`, `TitleBlockField`): positions in millimetres, origin at the
  block's bottom-right corner (or as spike 183 says; keep the origin in one constant so it can change).
- `tests/InventorAddin.Core.Tests/Drawing/PrintTemplateLayoutTests.cs`

Out of scope: anything under `src/InventorAddin`; settings; Create Print.

## Acceptance criteria

- [ ] Sheet sizes: ISO A4 to A0 and ANSI A to E with their sizes (A4 and ANSI A portrait, the rest landscape), border margins 20 mm on the filing (left) edge and 10 mm elsewhere (ANSI: 0.5 in, with the filing edge as the ISO sheets), and the zone counts in spec 05.
- [ ] Zone labels: numbers 1.. left to right, letters A.. from the top; a centring arrow at the middle of each edge.
- [ ] Triad: X to the right and Y up, inside the border in the bottom-left corner, as in the mockup.
- [ ] Title block: every line and cell of the mockup (180 by 55 mm: project and file name row 5 mm, then the approved 50 mm block with Material 40, Material Size 30, Finish 45; no FINISH line). Labels, sizes (labels 1.8, values 2.8, description 4, part number and revision 7 bold, status, project, file name and material size 2.5) and the font name Tahoma are in the data.
- [ ] Fixed text: metric and imperial tolerance boxes word for word as spec 05 (both with "UNLESS OTHERWISE SPECIFIED:"), "THIRD ANGLE PROJECTION", "aidanwbrown.com", the logo's file name and its 19 by 19 mm place (1.5 mm inside its 22 mm square cell). Left section as spec 05: logo 22 by 22, tolerance box 43 by 22, projection 65 by 20, URL cell 65 by 8 across the bottom. Branding strings come from `Branding` where they exist; add the URL there if it is not.
- [ ] Fields: an enum of what each cell reads (PartNumber, Description, Revision, Status (Inventor's Status field, Design Tracking User Status; Aidan, 2026-11-22, task 216), Material, MaterialSize, FinishSpec (the custom Finish Spec property, task 211; Aidan, 2026-11-22), Designer, DrawingAuthor, DrawingCreationDate, Mass, Scale, SheetNumberOfCount, Units, SheetSize, Project, FileName), not Inventor property ids (task 187 maps them after spike 183).
- [ ] The small part number: 2.5 mm, rotated 90°, in the right margin, level with the bottom of the border.
- [ ] Parts list columns: item, quantity, part number, description, Make/Buy, manufacturer (Vendor), manufacturer part number (Stock Number). Revision table columns: rev, description, date, by (Default; say so in Follow-ups).
- [ ] Tests: the title block fits inside the border on every sheet size, including A4 portrait (exactly 180 mm) and ANSI A portrait; no two cells overlap; every cell has a field or fixed text; the zone counts and labels per size; the tolerance texts word for word; metric and imperial differ only in the tolerance box, units and size values.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- The mockup's source `docs/ui-mockups/title-block.html` has every coordinate (its y runs down from the top of the
  55 mm block; convert to the chosen origin once, in one place).
- Core rules (`CLAUDE.md`): file-scoped namespace, no Inventor or WPF types. Units in names (`WidthMm`).
- Keep it data, not drawing code: task 187 walks it and draws.
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core` (0 warnings, 0 errors); `dotnet test tests/InventorAddin.Core.Tests`
  (5018 passed, 0 failed on the rerun after the review fixes; one earlier run had two unrelated timing-sensitive RenamePlanTests failures that passed on rerun; the new `PrintTemplateLayoutTests` are part of that count).
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` was touched.
- **Inventor API members not confirmed:** none; Core only.
- **Manual checklist for Inventor:** nothing to check in Inventor for this task (data only). Task 187 draws the layout;
  its checklist compares the result with `docs/ui-mockups/title-block.png`.
  1. After 187: build the template, open a drawing from it and compare the block with the mockup, cell by cell.

Choices made (all in `PrintTemplateLayout.cs`): block frame is origin bottom-left, x right, y up, millimetres, converted
from the mockup's y-down once through `FromTop` (`OriginNote` says so). The sheet frame is bottom-left, y up too;
`SheetSizeSpec.TitleBlockOriginMm` puts the block's bottom-right on the border's inner corner (spike 183). Cells tile the
180 by 55 block exactly, 21 cells (17 field cells, logo, tolerances, projection, URL). Cell edges are drawn by 187 at
`CellLineWeightMm`; `Lines` holds only the outline, the row under the project row, the left section's right edge and the
angles divider. `Branding` gained `WebsiteUrl` and `PrintLogoFileName`.

## Follow-ups

Things noticed but not done.

- **Triad overlaps the block on A4 and ANSI A portrait.** The block fills the border's width there, so the X/Y triad
  (5 mm in from the bottom-left corner, 13 mm long) lies over the block's bottom-left (the URL and projection cells). The
  mockup has the same overlap on its A4 sheet. Needs Aidan: move the triad above the block on those sizes, or accept it?
  Task 187 should not draw it over the block until decided. (Cloud-free; Core can add a rule once decided.)
- **ANSI left margin read as 20 mm.** The brief says "0.5 in, with the filing edge as the ISO sheets", so ANSI is 20 mm
  left and 12.7 mm elsewhere. The mockup caption says "190 mm inside its 0.5 in border" for ANSI A, which is 12.7 mm both
  sides. Both fit the 180 mm block; the data says 20 mm on the left. Confirm.
- **Zone counts for ANSI A to E come from spec 05 as written (4x2, 4x4, 8x4, 8x4, 16x8).** At about 50 mm per zone ANSI A
  portrait would be 4 by 5 and B 8 by 5; the listed counts look low in the down direction. Spec 05 question 13 is the
  place to confirm.
- **Default: table column widths.** Spec 05 names the parts list and revision table columns but not their widths. The
  widths in `PartsListColumns` (12, 12, 35, 60, 18, 30, 35 mm) and `RevisionTableColumns` (12, 80, 25, 20 mm) are mine, and
  the revision table's columns (rev, description, date, by) are the task's Default. Neither is in `DECISIONS.md`; the parent
  should log them as Default.
- **Small part number position (review fix).** Its baseline is 3 mm in from the sheet's right edge, as in the mockup's sheet
  drawing: 7 mm outside the ISO border and 9.7 mm outside the ANSI one, clear of the zone frame (5 mm outside the border).
  It is placed by `SheetSizeSpec.SmallPartNumberMm`, not in block coordinates. The test checks the glyph band against the
  frame using the glyph height (1.8 mm, `SmallPartNumberGlyphHeightMm`), not the full 2.5 mm text size: with 2.5 mm the
  band would start 4.5 mm outside the ISO border and cross the 5 mm frame line, so the reviewer's 2.5 mm band cannot hold on
  ISO sheets at the mockup's position.
- **Imperial values.** Sample values (weight in lb, material size "PL .500 x 2.50 x 5.00") are run-time data, not layout; the
  layout carries only `UnitsText` and `MassUnit`. Task 187 formats the numbers.
- The sheet-level extras (zone frame offset 5 mm, label size 3 mm, arrow geometry, triad geometry) are constants taken from
  the mockup; Task 187 draws them from `SheetSizeSpec` and `PrintTemplateLayout.Triad`.

Review fixes: `TitleBlockCell.FixedText` now documents that it is descriptive only (drawable content is `Texts`, `Logo`,
`Symbol`); inline labels are documented as drawn with a colon; comments added on the dashed centre lines, the outline weight
(mockup 0.7; reviewer thought 0.35, so 187 should confirm) and the triad label offset and line weight.
