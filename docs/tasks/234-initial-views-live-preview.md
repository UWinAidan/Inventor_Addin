# 234: Initial Views: live sheet preview

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 192)
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 232, 233
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The Initial Views window shows a live preview of the sheet: border, title block, the view boxes at the scale and the front view direction, updating as units, views, front view, scale or sheet change.

## Scope

- Core: the preview geometry (sheet, border, title block, view rectangles) from the plan, tests.
- A WPF preview control in the window bound to it; no Inventor calls.

## Acceptance criteria

- [ ] Every change in the window updates the preview at once.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5459 passed, 0 failed (7 new in `InitialViewsPreviewTests`; the XAML style rules test passes).
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor.
- **Inventor API members not confirmed:** none; no Inventor calls. WPF only (`DrawingContext`, `FormattedText`, as in `HoleDiagramView`).
- **Manual checklist for Inventor:**
  1. Open a part, click Create Print. The window shows a Preview section at the top: the sheet, its border, a grey "Title block" band bottom-right, and a box per ticked view (labelled Front: Auto, Top, Right, Iso), with the scale in the corner.
  2. Untick Iso: its box vanishes at once. Tick it again: it returns.
  3. Change Scale to 1:1 on a big part: the boxes grow, and turn red-outlined when they do not fit.
  4. Change Front view to Right: the Front box and the boxes projected from it change shape; its label reads "Front: Right".
  5. Switch Units to Imperial: the sheet changes to an ANSI size and the border and title block move with it. Pick another sheet size: the preview follows.
  6. Check light and dark themes, and that no bottom border is cut off at 150% display scale.
  7. Place Initial Views in a drawing shows the same preview on the fixed sheet.

## Follow-ups

Things noticed but not done.

- The preview shows each view as a labelled box only, not the model's outline; drawing the real shape would need geometry from Inventor.
- Title block band is a plain grey box labelled "Title block"; it does not show the template's real cells.
