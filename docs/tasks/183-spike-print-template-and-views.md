# 183: Spike: building the print template and placing views

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5", "Added for M5: Template additions", open questions 21 and 22)
- **Status:** todo
- **Depends on:** none
- **Needs:** **windows** (spike, local session with Aidan in Inventor 2026)
- **Parallel-safe with:** 184 (same session, separate rounds)

## Goal

Settle, before any template or view task is written against them, the Inventor calls that build the AWB print template from code and place the initial views. Every later template task (186, 187, 191) uses only members this spike confirms.

## Questions

**A. Title block and border, drawn from code**

1. Creating a title block definition from code: `DrawingDocument.TitleBlockDefinitions.Add`, editing its sketch
   (`Edit`/`ExitEdit`), lines, and text boxes. Where is the insertion point (bottom-right corner?), and can geometry sit
   outside the block's own rectangle (the small part number in the right margin, task 185's layout)?
2. Property fields in title block text: the `<Property Document='model' ...>` (or equivalent) formatted text for
   Part Number, Description, Material, Revision Number, Designer, Project, Mass, Design Status, and the custom
   properties Finish, Finish Note and Material Size. Does Design Status print as text (Work In Progress) or a number?
   Can Mass be formatted in kg or lb with 2 decimals?
3. Drawing fields: the drawing's own Author and Creation Date; sheet scale (the first view's), sheet number and sheet
   count, sheet size; the model's file name with its extension.
4. Fixed text with `±`, `°` and bold runs; rotated text (90°) for the edge part number; Tahoma.
5. The logo: a sketch image (`SketchImages.Add`?) from `libraries/Branding/aidanwbrown-mark-black-1024.png`, embedded
   (not linked) so the template carries it.
6. Border: Inventor's default border with zones (`Sheet.AddDefaultBorder` and its zone count, label and centre mark
   arguments, or a `BorderDefinitions` default border) versus a custom border drawn per sheet size. Which gives numbers
   left to right, letters from the top, centring arrows, and the zone counts in spec 05?
7. The X/Y triad in the bottom-left corner: part of a custom border, a sketched symbol placed on each sheet, or
   something else that follows every sheet size?
8. Sheet formats for ISO A4 to A0 and ANSI A to E (portrait A4 and ANSI A; landscape otherwise) and saving the
   document as an `.idw` template in a chosen folder. Sheet colour white (the drafting standard's sheet colour or the
   application's?), third angle projection in the drafting standard, Tahoma as the default text style.
9. Parts list and revision table styles in the template: column sets (item, quantity, part number, description,
   Make/Buy as a custom column, manufacturer from Vendor, manufacturer part number from Stock Number).

**B. Using the template**

10. `Documents.Add(kDrawingDocumentObject, <template path>, true)` with the AWB template, then choosing a sheet
    format (A3 landscape or ANSI B landscape) for the first sheet. Does the new drawing read the model's properties
    once a view of the model is placed?

**C. Placing views** (for task 191)

11. Projected views from a base view (`DrawingViews.AddProjectedView`) for top and right in third angle; an iso view
    (`kIsoTopRightViewOrientation` base view or a projected corner view); shaded or hidden-line style.
12. A sheet metal part: the folded front and the flat pattern (`AddBaseView` with the `SheetMetalFoldedModel` option
    false in its additional options?); what happens with no flat pattern.
13. A view's sheet size after placing (`DrawingView.Width`, `Height`, `Left`, `Top`, `Center`) to check the layout.

## How

- A spike branch `spike/183-print-template`, off `main`. Drive the running Inventor from PowerShell over COM (as spike
  160 did) or with a temporary developer button. Scripts stay in the session scratchpad, not in the repo.
- Aidan uses a test part, a sheet metal part and an assembly of his own (no employer data).
- Check every member against the Inventor API reference or the interop by reflection before trying it (`CLAUDE.md`).
- Record each confirmed member, its arguments and what it returned under Results. Then update tasks 186, 187, 191
  (and 185 if the layout must change, for example a different insertion point) with the confirmed calls.

## Results

Filled in during the spike, round by round, with what each call returned. Nothing from the spike branch is merged; the
tasks that depend on this one are updated with the confirmed members before they run.
