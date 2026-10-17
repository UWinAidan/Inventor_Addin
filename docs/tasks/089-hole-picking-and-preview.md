# 089: Hole centre picking and the live preview session

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); spike results in `docs/tasks/081-spike-awb-hole.md`
- **Status:** todo
- **Depends on:** 088
- **Needs:** windows build (Inventor API)
- **Parallel-safe with:** 086, 087

## Goal

The add-in object that runs while the AWB Hole window is open: it picks the hole centres the way Inventor's Hole does, keeps the real hole feature in step with the view-model as the live preview, and ends with OK (one Undo step) or Cancel (nothing changed).

## Scope

- `src/InventorAddin/Holes/` (new files): the picking session and the preview session, or one session class

## Acceptance criteria

- [ ] **Default centres** (new hole): the selected sketch or the sketch of selected points, else the newest visible sketch with `SketchPoint.HoleCenter` points; all its hole centres picked.
- [ ] **Picking** with `CreateInteractionEvents`, `SelectEvents.AddSelectionFilter(kSketchPointFilter)`, `SingleSelectEnabled = true`: a click toggles a point, without Ctrl, then `ResetSelections`; picked centres in a `HighlightSet` coloured blue (from a theme-independent constant in Core); the centre count goes to the view-model.
- [ ] **Edit**: the hole's own centres are picked; points of their sketch can be added or removed.
- [ ] The sketch is shown (`PlanarSketch.Visible = true`) while the window is open and put back as it was on OK and on Cancel (a new hole's sketch ends hidden, as Inventor leaves it).
- [ ] **Preview**: one outer transaction for the session ("AWB Hole" or "Edit AWB Hole"); each view-model change applied by 088 inside its own inner transaction; if Inventor refuses the change or the hole is unhealthy, abort only the inner transaction, keep the last good hole, and set the view-model's refused status (spike 081 round 6). No centres means no preview hole.
- [ ] **OK** applies the dowel tolerance, colour and stored data (or removes them, for a hole that is no longer a dowel), ends the outer transaction: one Undo step. **Cancel** or closing the window aborts it: the part is as before, also for an edited hole.
- [ ] Interaction events stopped, highlight deleted and the view updated on every way out, including an exception and Inventor closing the document.
- [ ] `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- The spike's `SpikeHoleWindow` on branch `spike/081-in-inventor` shows working code for all of this; read it, do not merge it.
- Log timings at Info only when a change takes over 500 ms.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (with 090)

## Follow-ups
