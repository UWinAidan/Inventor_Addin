# 225: Spike: view orientation, dimension styles and the lb unit text

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** todo
- **Depends on:** 192
- **Needs:** **windows** (spike, parent with Aidan in Inventor; same session as spike 218)
- **Parallel-safe with:** any

## Goal

Settle the Inventor calls the chunk 2 fixes need before their tasks are built.

## Scope

- **Front view from a direction:** how to place a base view looking along any direction with a chosen "up" (for example `kArbitraryViewOrientation` with a `Camera`, or a custom view orientation): needed for sheet metal (look straight at the flat pattern's base face, longest edge horizontal), for the Auto front view (largest projected area, longest side horizontal) and for the Front view dropdown. Do projected views from such a base view still come out third angle?
- **Projected areas:** how to get a part's or assembly's extent along each axis or along a face normal (`OrientedMinimumRangeBox`, `RangeBox`, the flat pattern's base face and its edges).
- **Bend notes:** whether a flat pattern view gets bend notes (direction, angle, radius) automatically, and how to add them (`DrawingNotes.BendNotes.Add` or similar) for every bend centreline.
- **Dimension styles:** on a Create Print drawing, which `DimensionStyle` each kind of dimension uses (ordinate, linear, radial, angular, hole notes) and why only 2 styles got the period; why setting the decimal marker on the feature control frame style fails with E_FAIL.
- **Mass "lb":** whether the WEIGHT field can print "lb" instead of "lbmass" (unit string settings on the document or the standard, a precision or units attribute, a custom unit). If not, question 29's fallback (a Mass text property) is used.
- Record results in this file; no repo code changes.

## Acceptance criteria

- [ ] Each question has an answer or a "not possible" with what was tried.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
