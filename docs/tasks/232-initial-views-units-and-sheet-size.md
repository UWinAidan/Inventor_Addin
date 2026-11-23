# 232: Initial Views: Metric or Imperial, and the sheet size

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 192)
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 227
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

There is no way to make an imperial print today. The Initial Views window gets a Metric | Imperial choice, defaulting to the part's units, which picks the AWB Metric or AWB Imperial template, and a sheet size dropdown (A4 to A0 for metric, ANSI A to E for imperial) defaulting to the smallest sheet that fits at the chosen scale. When a chosen scale does not fit, the window offers the next sheet size instead of only a warning.

## Scope

- `InitialViewsViewModel` (Core): units, sheet list per units, default sheet rule, next-size offer; tests.
- `InitialViewsWindow`, `CreatePrintCommand` (template and sheet format from the choice), `PlaceInitialViewsCommand` (sheet choice applies to the active sheet only if it can be resized; otherwise Default: the sheet list is hidden there).

- FINISH and WEIGHT follow the print's units, not the part's (Aidan, 2026-11-23): an inch part on a metric print prints the µm Finish Spec and kg, a metric part on an imperial print the inch spec and lb. Default (spec 05 question 33): when the units differ, Create Print writes the print-units Finish Spec and Mass as custom properties of the drawing (made by the same Core formatting as the part's own) and points that drawing's title block definition's FINISH and WEIGHT fields at them; when they match, the fields stay on the part's live properties.

## Acceptance criteria

- [x] Create Print can make an imperial print of a metric part and an A2 print of a large part.
- [x] An inch part on a metric print shows the µm Finish Spec and kg; a metric part on an imperial print shows the inch spec and lb.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5419 passed, 0 failed (the one run with 2 probe-timeout failures in SaveAsCopyCreator and NewPartCreator passed on the rerun). New tests: `PrintUnitValuesTests` (9), `InitialViewsSheetRuleTests` (6), `InitialViewsUnitsAndSheetTests` (9).
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor: `CreatePrintCommand`, `PlaceInitialViewsCommand`, `InitialViewsPlacer` (the `Measure` signature takes the units), `InitialViewsWindow.xaml` (new Sheet section), `Extraction/MassPropertyReader` (`MassKg`), `Extraction/FinishSpecBackfiller` (`PrintUnitWritesFor`), new `Drawing/TitleBlockPropertySource.cs`.
- **Inventor API members not confirmed:**
  - Writing `TextBox.FormattedText` (reading it is confirmed in `DrawingExtractor`).
  - `Sheet.TitleBlock.Definition` then `TitleBlockDefinition.Edit(out sketch)` and `ExitEdit(true)` on a drawing's own copy of the definition, after the sheet was made from a sheet format (the template builder uses `Edit`/`ExitEdit` on definitions it made itself).
  - That `Document='drawing'` with the custom (User Defined) property set resolves in a title block text. The template builder only uses `Document='drawing'` for the built-in Author and Creation Time.
  - A PropertyID kept in a repointed custom property field tag (the builder writes none for custom properties; the repoint changes only the Document attribute).
  - A custom Finish Spec and Mass property written to a drawing document by `PropertyWriter` (same call as for a part; not tried on a drawing).
- **Manual checklist for Inventor:**
  1. Redeploy. Open a metric part with a Finish and a mass; click Create Print. The Initial Views window shows a Sheet section with Units = Metric, and a sheet size. A small part starts on A4, a large one on a larger sheet; the header line follows the sheet.
  2. Choose Imperial: the sheet list becomes ANSI A to E, the sheet changes to the smallest that fits, and a short wait cursor shows once (measuring with AWB Imperial). Switch back: no second wait.
  3. Pick A3 yourself, then choose scale 1:1 on a part too big for A3: the warning and a "Fits" row offering "Use A2" (or next) show; the button switches the sheet and the warning goes. Picking a sheet yourself stops it following the views.
  3b. With the sheet left automatic, choosing 1:1 moves the sheet to the smallest that fits.
  4. OK with Imperial on the metric part: the print is made from AWB Imperial on the chosen ANSI sheet; its WEIGHT reads lb and FINISH reads the inch spec (for example "..., .0005-.001 IN"). The drawing's iProperties, Custom tab, hold Finish Spec and Mass. The part's own properties are unchanged.
  5. Open an inch part (Document Settings, Units, inch) and make a print on Metric: WEIGHT kg, FINISH the µm spec.
  6. A print in the part's own units: no drawing Finish Spec or Mass, and the fields still follow the part (change the part's Finish, Update the drawing, it changes).
  7. Place Initial Views on an existing drawing: no Sheet section, the views go on the active sheet as before.
  8. Create Print on an assembly: units choice works; imperial print shows lb; FINISH stays the dash.
  9. Check the log for "title block field(s) now read the drawing's" with 2 fields (1 for an assembly) and no Problems.

## Defaults chosen (for DECISIONS.md)

- The print starts in the part's own units (`UnitsOfMeasure.LengthUnits`, as Material and Finish reads them), not the Settings default units.
- "Smallest sheet that fits": the first sheet, smallest first, whose title block fits and where the plan fits; with the Suggested scale it must also give a scale of at least 1:2 (`InitialViewsSheetRule.MinimumSuggestedScale`), else the next sheet. A scale the user picked must fit as it is; when nothing fits the largest sheet is used.
- A sheet picked by the user is kept when views or scale change; switching units goes back to the automatic pick.
- Place Initial Views keeps its fixed sheet, with no units or sheet choice (brief: the sheet list hidden there).
- A stored Finish Spec that is not the add-in's own spec in the part's units is treated as hand-written and copied unchanged to the drawing; an assembly gets only Mass on an off-units print.
- Measuring uses the chosen units' template (once per units), at that units' default-sheet area; view sizes at scale 1 do not depend on the sheet.

## Follow-ups

- Spec 05 question 33 and the task 232 brief: Aidan to confirm the Default (drawing properties refresh on Create Print only, not on every part save). Question 35's alternative (dashes on the drawing only) can now use the same mechanism (`TitleBlockPropertySource`).
- The drawing's Finish Spec and Mass writes and the field repoint are best effort: a failure is logged, the views are still placed, and a notice tells the user FINISH and WEIGHT may show the part's units.
- Material Size on an off-units print stays in the part's units (spec 05 question 34, not changed).
- Initial Views live sheet preview and Front view dropdown are tasks 233 and 234, not done here.
- A drawing made on an off-units print keeps its Finish Spec and Mass as made; a later "Update" of the part does not refresh them. If Aidan wants that, Mass Update could refresh drawings' values.
