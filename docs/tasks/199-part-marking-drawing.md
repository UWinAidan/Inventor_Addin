# 199: Part Marking window and marking on the drawing

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 2)
- **Status:** done
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

## Defaults

- The zone also asks for an edge pick, and puts the zone on the picked edge's view (the task says "the picked view"; spike 184's pick is the only confirmed one, and it returns a curve segment whose parent is the view).
- The leader note's text is the method line, then the model's Part Number as a property field on its own line, then the extra lines. With method None there is no method line and no part number line, only the extra lines.
- In zone mode the zone holds only the zone text; the method line and extra lines go in a second text box under the zone, in the same sketch, so they move with it.
- Zone text is 2.5 mm high in the sketch (model units), width the zone minus 1 mm each side, so it wraps to two lines; the leader note text is 3 mm on the sheet. The leader note sits 15 mm above the view's top-right corner.
- The window has no engrave tick box yet (task 200 adds it).
- The window picks Metric or Imperial from the model's length units (`MaterialFinishTargets.UnitsOf`), or the drawing's when it references no model.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5691 passed, 0 failed on one run; other runs showed one to three of the known flaky probe-timeout or file tests (RenamePlan, RegisterStore, SaveAsReplacer, SaveAsCopyCreator), different each time, none touching this task. Core changes: `CommandNames.PartMarking`, the ribbon button (large, drawing ribbon, after Update Notes), a `CommandDocuments` row (Edited); the ribbon and command-document tests were updated for them (icon count 18 to 19).
- **Review changes:** the leader note's line rule moved to Core (`PartMarking.LeaderNoteLines`, with the `LeaderNoteLine` record marking the part number line; `LeaderPreviewText`); the writer only maps the marker to the field. The view-model has `PreviewText` (leader shows a "<part number>" line), and the window binds to it. The zone line style is wrapped in `ComSafe.Run`. The command shows a warning box when the warnings list is not empty. Re-ran after these: `dotnet build InventorAddin.slnx -p:DeployToInventor=false` 0 errors; `dotnet test tests/InventorAddin.Core.Tests` 5699 passed, 0 failed. Still not run in Inventor.
- **Probe (C:\AwbSpike99\, copy of r1-views.idw):** a leader note to a model edge was added, and `LeaderNote.Color = CreateColor(0,112,192,1.0)` read back as 0,112,192. The probe then failed at `SketchLine.LineType` because it used a wrong enum value (the real `kDashedLineType` is 37634); it was not re-run, because the cleanup of the Inventor instance it started was refused (see Follow-ups). So the zone calls are those of spike 184 round 2, unprobed here.
- **Not compiled (changed under `src/InventorAddin`):** none; the add-in built on Windows. Built does not mean it works in Inventor: nothing has been run there.
- **Inventor API members not confirmed by existing code or the spike (all exist in the interop DLL; behaviour untested):** `TextBox.Color` (set on the zone texts), `TextBox.Width` (set, to wrap the zone text), `TextBox.HorizontalJustification` and `VerticalJustification` on a sketch text box (used the same way in `PrintTemplateBuilder`), `DrawingView.Width` and `Height` (to place the leader note above the view), `DrawingSketch.SketchLines`. `LeaderNote.Color` was confirmed by the probe. Whether `LeaderNote.Color` also colours the leader line, not only the text, is not known. Whether the `<StyleOverride FontSize>` wrapper around a `<Property>` field works in a leader note is not known (spike 184 used the field without it).
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a drawing with an iso view (for example one made by Create Print). On the drawing ribbon click Part Marking. The window shows Method (Laser engrave), Character height 3 mm, Extra lines, Show as (Leader to an edge) and the note "LASER ENGRAVE PART NUMBER, 3 mm CHARACTERS".
  2. Set Method to None with no extra lines: OK is disabled and the footer says to choose a method or type a line. Type "DO NOT STAMP" in Extra lines: OK is enabled and the note reads that line.
  3. Method Laser engrave, extra line "DO NOT STAMP", Leader to an edge, OK. The prompt asks for an edge. Click an edge on the iso view. A note appears above the view's top-right corner with a leader to that edge, in blue (#0070C0): the method line, the model's part number, then "DO NOT STAMP". Check the leader line itself is blue too (not only the text).
  4. Press Ctrl+Z once: the note is gone. Ctrl+Y brings it back.
  5. Run it again, choose Marking zone on a view, OK, click an edge in the front view. A dashed blue rectangle appears centred on that view with "MARKING MUST BE PLACED INSIDE THIS ZONE" in blue inside it, and the method line and extra lines under it. Measure the rectangle: 40 x 15 mm on the part (so it scales with the view). Expand the view's sketches in the browser: the new sketch can be edited and its lines dragged.
  6. Ctrl+Z once: the whole zone, text included, is gone.
  7. Esc at the edge prompt: nothing is added, no error message.
  8. Settings: with Inventor closed, edit PartMarkingColour in %APPDATA%\AwbAddin\settings.json to #FF0000, start Inventor and mark again: the marking is red.
  9. Try a drawing of an imperial part: the window shows 0.12 in and the zone size 1.5 x 0.6 in; the zone is 1.5 x 0.6 in on the part.
  10. Run Part Marking on a part or assembly (not a drawing): a message says to open a drawing.

## Follow-ups

Things noticed but not done.

- The probe in this task started an Inventor instance (process id 91812, started 2026-11-26 4:45 pm, probably with r1-views.idw from C:\AwbSpike99 open) because the script errored before its `Quit`; closing it was refused by the permission check, so it may still be running. Aidan: close it without saving. The test copies in C:\AwbSpike99 are disposable.
- Spec 05 question 16: marking in zone mode puts the method and extra lines under the zone as a second text; ask whether they should be inside it, and whether the zone should ask for a view by clicking the view itself instead of an edge.
- Task 200 should add the engrave tick box to `PartMarkingWindow.xaml` (the view-model already has `EngraveFeature` and `CanEngraveFeature`).

