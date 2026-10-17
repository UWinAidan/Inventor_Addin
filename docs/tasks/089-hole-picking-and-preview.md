# 089: Hole centre picking and the live preview session

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); spike results in `docs/tasks/081-spike-awb-hole.md`
- **Status:** done
- **Depends on:** 088
- **Needs:** windows build (Inventor API)
- **Parallel-safe with:** 086, 087

## Goal

The add-in object that runs while the AWB Hole window is open: it picks the hole centres the way Inventor's Hole does, keeps the real hole feature in step with the view-model as the live preview, and ends with OK (one Undo step) or Cancel (nothing changed).

## Scope

- `src/InventorAddin/Holes/` (new files): the picking session and the preview session, or one session class

## Acceptance criteria

- [x] **Default centres** (new hole): the selected sketch or the sketch of selected points, else the newest visible sketch with `SketchPoint.HoleCenter` points; all its hole centres picked.
- [x] **Picking** with `CreateInteractionEvents`, `SelectEvents.AddSelectionFilter(kSketchPointFilter)`, `SingleSelectEnabled = true`: a click toggles a point, without Ctrl, then `ResetSelections`; picked centres in a `HighlightSet` coloured blue (from a theme-independent constant in Core); the centre count goes to the view-model.
- [x] **Edit**: the hole's own centres are picked; points of their sketch can be added or removed.
- [x] The sketch is shown (`PlanarSketch.Visible = true`) while the window is open and put back as it was on OK and on Cancel (a new hole's sketch ends hidden, as Inventor leaves it).
- [x] **Preview**: one outer transaction for the session ("AWB Hole" or "Edit AWB Hole"); each view-model change applied by 088 inside its own inner transaction; if Inventor refuses the change or the hole is unhealthy, abort only the inner transaction, keep the last good hole, and set the view-model's refused status (spike 081 round 6). No centres means no preview hole.
- [x] **OK** applies the dowel tolerance, colour and stored data (or removes them, for a hole that is no longer a dowel), ends the outer transaction: one Undo step. **Cancel** or closing the window aborts it: the part is as before, also for an edited hole.
- [x] Interaction events stopped, highlight deleted and the view updated on every way out, including an exception and Inventor closing the document.
- [x] `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- The spike's `SpikeHoleWindow` on branch `spike/081-in-inventor` shows working code for all of this; read it, do not merge it.
- Log timings at Info only when a change takes over 500 ms.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows, Inventor 2026 installed, not deployed): `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: Build succeeded, 0 errors; the only warnings are the existing CS8600 ones in `Commands/` (none in the new files). `dotnet test tests/InventorAddin.Core.Tests`: 3156 passed, 0 failed (`HoleSessionRulesTests` alone: 53 passed, 0 failed). Run again after the review fixes (picks moved to another sketch remove and remake the preview hole; a failed removal finds the hole again; an edited hole with no sketch ignores clicks): same build result, the counts above. Nothing was run inside Inventor.
- **Added:** `src/InventorAddin/Holes/HoleSession.cs` (the session: picking, preview, OK, Cancel, every way out; `HoleSessionEndReason`, `HoleSessionEndedEventArgs`); Core `src/InventorAddin.Core/Holes/HoleSessionRules.cs` (`HoleSessionRules`: the blue `PickedColour`, transaction names, the default sketch, the preview plan per change (a new hole whose picks moved to another sketch is removed and made again, never moved across sketches), what to do after each write, the sketch's visibility at the end, the 500 ms slow-change rule; `HoleCentrePicks<TPoint, TSketch>`: the toggle); tests in `tests/InventorAddin.Core.Tests/Holes/HoleSessionRulesTests.cs`.
- **How 090 uses it:** `HoleSession.Start(app, doc, viewModel, existingOrNull, log)` once the view-model holds the hole's settings, opened after Inventor's event has returned. The session handles the view-model's `PreviewNeeded`, `OkRequested` and `CancelRequested` itself; the window closes on `Ended` (raised once, by every way out) and calls `Cancel()` when it is closed any other way; the add-in's shutdown calls `Dispose()`.
- **Not compiled (changed under `src/InventorAddin`):** none: `Holes/HoleSession.cs` compiled against the Inventor 2026 interop. It has not run in Inventor.
- **Inventor API members not confirmed** (each compiles; signatures checked against the interop by reflection; none is in spike 081's confirmed list or in `Extraction/`):
  - `ApplicationEvents.OnCloseDocument` (`_Document, string, EventTimingEnum, NameValueMap, out HandlingCodeEnum`), and whether aborting a transaction during its `kBefore` call is allowed. `PartDocument.FullDocumentName` read, as a second check that the closing document is ours.
  - `InteractionEvents.OnTerminate` firing when Inventor stops the picking (another command, Esc): the session takes that as Cancel. Not known which actions raise it.
  - Used by the spike's window code, which ran, but not in its confirmed list: `SketchPoint.Parent` (as `PlanarSketch`), `PlanarSketches.Count` and `Item`, `PlanarSketch.SketchPoints`, `PlanarSketch.Name`, `Point.IsEqualTo` with its default tolerance, `Application.ActiveView.Update`, enumerating `SelectSet` and `ObjectsEnumerator`.
  - `HoleFeature.Delete(true, true, true)` for removing the preview hole when the last centre is unpicked (keeps the sketch; the spike's `Delete()` would have deleted it), and enumerating `HoleFeatures` to find a hole again by name.
  - Whether `PlanarSketch.Visible` set inside the outer transaction is reverted by its `Abort`: the session reads the value after Cancel and sets it only when it differs, so either way the sketch ends as it was.
- **Manual checklist for Inventor:** (with 090) on a scratch block with a sketch of three hole-centre points:
  1. AWB Hole with nothing selected: the newest visible sketch with hole centres is used, all three points are blue and a hole is at each. Select another sketch (or one of its points) first: that sketch is used instead.
  2. Click a point: it goes from the hole and loses its blue; click it again: it comes back. No Ctrl needed. Click every point off: no hole is shown and OK is disabled; the sketch is still there. Click one back on: the hole comes back.
  3. Click a point of a different sketch on a new hole: the picks start again in that sketch with just that point, and the preview hole is removed and made again there (one hole in the browser, in the new sketch).
  4. The sketch is visible while the window is open. OK: the sketch ends hidden. Cancel: the sketch is as it was before the command.
  5. Change values: the hole follows each change. A refused value (Flip, or a hole wider than its counterbore): the window says why, the last good hole stays, and the next valid change works. A change between tapped and clearance refused part way: the hole is still there and later changes work.
  6. OK, then one Ctrl+Z: the whole hole goes (feature, colour, tolerance, data, sketch visibility). Cancel: the part is exactly as before, also after editing a hole.
  7. Edit an existing hole: its own centres are blue; points of its sketch can be added and removed; points of another sketch do nothing. Cancel leaves the hole as it was. A hole placed on something other than sketch points (if one can be made): clicks do nothing.
  8. While the window is open, close the part (or close Inventor): no error; the window closes. At Inventor's save prompt answer Yes, then reopen the part: there is no preview hole in it (the session was cancelled before the save).
  9. While the window is open, start another command from the ribbon, or press Esc in the model: the session is cancelled and the window closes (note which actions do this; see Follow-ups). Orbit, pan, zoom (mouse wheel and the navigation bar) and the ViewCube must not cancel it: the window stays open, the blue centres stay, and clicking a point still toggles it.
  10. After each of OK, Cancel, closing and an error: no blue highlight is left, and clicking in the model selects normally again.
  11. On a large part, any change over 500 ms appears in `awbaddin.log` as "preview ... took N ms"; faster ones do not.

## Follow-ups

- **Defaults for the parent to log** (task 089 had to pick; Aidan may overturn): (a) on a new hole, clicking a point of another sketch starts the picks again in that sketch with only that point (one feature holds centres of one sketch); (b) Inventor stopping the picking (another command, Esc) cancels the session, so no other command runs inside its transaction; (c) an edited hole with every centre unpicked stays at its last centres (it cannot be removed and made again without losing its browser place; OK is disabled); (d) OK keeps the window open, with the refused status, when the hole in the model is not what the window shows (the last change was refused).
- **090:** pressing the AWB Hole button while the window is open may itself stop the picking (an Inventor command starting) and so cancel the session, before the command brings the window to the front. Check in Inventor; if so, the command must check for an open session first, or the session needs to ignore its own command.
- **090:** termination To still has no face pick (task 088 follow-up): the session passes no face, so the writer refuses To with "Pick the face the hole ends at." Hide To or add the face pick.
- **090 or Core:** the writer's notes for a hole that was made (the tap drill fallback, "made again at the end of the browser") are only logged; the view-model has no place to show a note after a successful change.
- Switching to another document while the window is open is not handled: the outer transaction stays open on the part. Consider `ApplicationEvents.OnActivateDocument` (cancel, or keep the window and stop the picking) once Aidan has seen what Inventor does.
- **090:** close the window on `Ended` through `Dispatcher.BeginInvoke`, not inside the handler: `Ended` can be raised inside Inventor's events (the part closing, the picking stopped), where opening a WPF window directly left the spike's window stuck (spike 081 round 3); closing one there was never tried, so defer it the same way.
- **090:** an edited hole whose centres are not sketch points has no sketch to pick in, so the session ignores every click. The window should say so (for example beside the centres), or the user will think picking is broken.
