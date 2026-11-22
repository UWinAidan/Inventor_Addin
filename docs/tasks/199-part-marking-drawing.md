# 199: Part Marking window and marking on the drawing

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 2)
- **Status:** todo
- **Depends on:** 184, 191, 198
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The Part Marking button opens the window; OK places a leader note to a picked edge on the iso view, or a dashed zone with its text, in the highlight colour.

## Scope

- `src/InventorAddin/UI/PartMarkingWindow.xaml(.cs)`.
- `src/InventorAddin/Drawing/PartMarkingWriter.cs` (spike 184, question 3) and `src/InventorAddin/Commands/PartMarkingCommand.cs` + ribbon button.

Out of scope: the engraved feature (200).

## Acceptance criteria

- [ ] Leader: after OK the user picks an edge; the note points to it, in the highlight colour.
- [ ] Zone: a dashed rectangle 40 by 15 mm centred on the picked view, with its text, in the highlight colour; the user can drag its sketch.
- [ ] One Undo removes the marking.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Confirmed calls (spike 184, round 2):
  - Picking the edge: `CommandManager.Pick(kDrawingCurveSegmentFilter, prompt)` returns a `DrawingCurveSegment`, or null on Esc. `.Parent` is the `DrawingCurve`, and its `.Parent` the `DrawingView`.
  - Leader: `LeaderNotes.Add(ObjectCollection{textPoint, Sheet.CreateGeometryIntent(curve, Missing)}, formattedText, Missing)`. A Part Number property field in the text shows the model's number.
  - Zone sketch: `DrawingView.Sketches.Add()`, `Edit()`, `SheetToSketchSpace(view.Center)`, then `SketchLines.AddAsTwoPointRectangle`. On each line set `LineType = kDashedLineType` and `OverrideColor`. Add the text with `TextBoxes.AddFitted`, then `ExitEdit()`.
  - The sketch is in model units, so the 40 x 15 mm zone is drawn as 4 x 1.5 cm (Default, DECISIONS.md 2026-11-22).

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
