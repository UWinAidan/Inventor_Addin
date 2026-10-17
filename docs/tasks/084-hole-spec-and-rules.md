# 084: Hole spec, geometry, checks and stored data (Core)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** 082, 083
- **Needs:** cloud
- **Parallel-safe with:** 086, 087

## Goal

One Core model of an AWB hole (what the user chose) and the rules that turn it into the exact geometry Inventor needs, check it, and store and restore it on the feature. The add-in only copies the geometry into Inventor.

## Scope

- `src/InventorAddin.Core/Holes/`: `HoleSpec` (the choices), `HoleGeometry` (what to build, in centimetres and degrees), `HoleRules` (spec plus workbook to geometry, and checks), `HoleStoredData` (attribute name/value pairs and back), the fit colour
- `tests/InventorAddin.Core.Tests/Holes/`

## Acceptance criteria

- [ ] `HoleSpec`: units; kind (Clearance, Tapped, Dowel; in the window a dowel or precision hole is Type Clearance with Standard AWB Dowel, spec 09 question 3, and Inventor models it as a plain drilled hole); seat (None, Counterbore, Countersink); size key; fit (Close, Normal, Loose for clearance; Slip, Normal, Press for dowel); tolerance row (dowel); thread depth and drill depth (tapped, editable); termination (Distance with depth, Through All, To a face); drill point row; flip.
- [ ] `HoleGeometry` in centimetres (internal units) with unit-bearing names: hole diameter, depth, seat diameter and depth or countersink diameter and angle, drill point (angled with angle, or flat), and for tapped holes the thread designation, thread type, class, tap drill (**in the thread table's units: mm for metric, inches for imperial**, as Inventor's `_CreateTapInfo` takes it; spike 081) and thread depth. Dowel: the tolerance as an ISO code or as upper and lower deviations, and the fit colour (RGB).
- [ ] Mapping per spec 09 "Types and seats": counterbore and countersink take the through hole from the Clearance tab at the row's "Hole fit"; tapped uses the Tapped row; dowel uses the Dowel row's nominal.
- [ ] Checks, each with a message the window shows, before anything reaches Inventor: at least one hole centre; positive diameter and depth; hole diameter smaller than the counterbore or countersink diameter; counterbore depth less than the hole depth for a blind hole; thread depth not more than drill depth; a size missing from the workbook for the chosen units; a dowel without a tolerance row. Spike 081 round 6: Inventor refuses a hole larger than its counterbore and the session must not get there.
- [ ] `HoleStoredData`: the spec as name/value pairs for the `AWB_Hole` attribute set (strings, numbers), with a `Version` (1), and back; an unknown version or a missing value gives a clear "cannot read" result rather than a guess. Round-trip tests for every kind.
- [ ] A comparison that says whether a hole's current state (diameter, tolerance code or limits, coloured or not) still matches its stored data, for the re-apply offer (spec 09).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Units: workbook metric rows are mm and imperial rows inches; convert to centimetres at the geometry boundary, keeping the tap drill in table units as noted.
- Keep these as plain records and static rules; the view-model (085) and the add-in (088) both use them.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups
