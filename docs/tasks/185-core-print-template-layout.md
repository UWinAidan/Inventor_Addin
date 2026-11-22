# 185: Print template layout (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5", "Added for M5: Template additions", `docs/ui-mockups/title-block.png`)
- **Status:** todo
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
- [ ] Title block: every line and cell of the mockup (180 by 62 mm: project and file name row 5 mm, FINISH row 7 mm, then the approved 50 mm block with Material 45, Material Size 30, Finish 40). Labels, sizes (labels 1.8, values 2.8, description 4, part number and revision 7 bold, status, project, file name and material size 2.5) and the font name Tahoma are in the data.
- [ ] Fixed text: metric and imperial tolerance boxes word for word as spec 05 (both with "UNLESS OTHERWISE SPECIFIED:"), "THIRD ANGLE PROJECTION", "aidanwbrown.com", the logo's file name and its 14 by 14 mm place. Branding strings come from `Branding` where they exist; add the URL there if it is not.
- [ ] Fields: an enum of what each cell reads (PartNumber, Description, Revision, DesignStatus, Material, MaterialSize, Finish, FinishNote, Designer, DrawingAuthor, DrawingCreationDate, Mass, Scale, SheetNumberOfCount, Units, SheetSize, Project, FileName), not Inventor property ids (task 187 maps them after spike 183).
- [ ] The small part number: 2.5 mm, rotated 90°, in the right margin, level with the bottom of the border.
- [ ] Parts list columns: item, quantity, part number, description, Make/Buy, manufacturer (Vendor), manufacturer part number (Stock Number). Revision table columns: rev, description, date, by (Default; say so in Follow-ups).
- [ ] Tests: the title block fits inside the border on every sheet size, including A4 portrait (exactly 180 mm) and ANSI A portrait; no two cells overlap; every cell has a field or fixed text; the zone counts and labels per size; the tolerance texts word for word; metric and imperial differ only in the tolerance box, units and size values.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- The mockup's source `docs/ui-mockups/title-block.html` has every coordinate (its y runs down from the top of the
  62 mm block; convert to the chosen origin once, in one place).
- Core rules (`CLAUDE.md`): file-scoped namespace, no Inventor or WPF types. Units in names (`WidthMm`).
- Keep it data, not drawing code: task 187 walks it and draws.
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
