# 084: Hole spec, geometry, checks and stored data (Core)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** done
- **Depends on:** 082, 083
- **Needs:** cloud
- **Parallel-safe with:** 086, 087

## Goal

One Core model of an AWB hole (what the user chose) and the rules that turn it into the exact geometry Inventor needs, check it, and store and restore it on the feature. The add-in only copies the geometry into Inventor.

## Scope

- `src/InventorAddin.Core/Holes/`: `HoleSpec` (the choices), `HoleGeometry` (what to build, in centimetres and degrees), `HoleRules` (spec plus workbook to geometry, and checks), `HoleStoredData` (attribute name/value pairs and back), the fit colour
- `tests/InventorAddin.Core.Tests/Holes/`

## Acceptance criteria

- [x] `HoleSpec`: units; kind (Clearance, Tapped, Dowel; in the window a dowel or precision hole is Type Clearance with Standard AWB Dowel, spec 09 question 3, and Inventor models it as a plain drilled hole); seat (None, Counterbore, Countersink); size key; fit (Close, Normal, Loose for clearance; Slip, Normal, Press for dowel); tolerance row (dowel); thread depth and drill depth (tapped, editable); termination (Distance with depth, Through All, To a face); drill point row; flip.
- [x] `HoleGeometry` in centimetres (internal units) with unit-bearing names: hole diameter, depth, seat diameter and depth or countersink diameter and angle, drill point (angled with angle, or flat), and for tapped holes the thread designation, thread type, class, tap drill (**in the thread table's units: mm for metric, inches for imperial**, as Inventor's `_CreateTapInfo` takes it; spike 081) and thread depth. Dowel: the tolerance as an ISO code or as upper and lower deviations, and the fit colour (RGB).
- [x] Mapping per spec 09 "Types and seats": counterbore and countersink take the through hole from the Clearance tab at the row's "Hole fit"; tapped uses the Tapped row; dowel uses the Dowel row's nominal.
- [x] Checks, each with a message the window shows, before anything reaches Inventor: at least one hole centre; positive diameter and depth; hole diameter smaller than the counterbore or countersink diameter; counterbore depth less than the hole depth for a blind hole; thread depth not more than drill depth; a size missing from the workbook for the chosen units; a dowel without a tolerance row. Spike 081 round 6: Inventor refuses a hole larger than its counterbore and the session must not get there.
- [x] `HoleStoredData`: the spec as name/value pairs for the `AWB_Hole` attribute set (strings, numbers), with a `Version` (1), and back; an unknown version or a missing value gives a clear "cannot read" result rather than a guess. Round-trip tests for every kind.
- [x] A comparison that says whether a hole's current state (diameter, tolerance code or limits, coloured or not) still matches its stored data, for the re-apply offer (spec 09).
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- The workbook reader (082) does not check that a Counterbore or Countersink size has a Clearance row at its "Hole fit"; that check belongs here (task 082 follow-up).

- `Iso286.HoleLimits` compares the size to the step limits exactly; round the nominal size (for example to 6 decimals of a mm) before calling it, so a size converted from centimetres such as 18.000000000000004 stays in the 10 to 18 mm step (task 083 review).

- Units: workbook metric rows are mm and imperial rows inches; convert to centimetres at the geometry boundary, keeping the tap drill in table units as noted.
- Keep these as plain records and static rules; the view-model (085) and the add-in (088) both use them.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 2963 passed, 0 failed, 117 of them new, in `HoleRulesTests`, `HoleStoredDataTests` and `HoleStateComparisonTests`. Re-run after the review changes, with task 086 committed.
- **Not compiled (changed under `src/InventorAddin`):** none. This task changes Core and tests only.
- **Inventor API members not confirmed:** none used. The geometry follows the spike 081 shapes: tap drill as a number in table units for `_CreateTapInfo`, drill point name and angle for the `...Extent2` methods, ISO code for `SetToFits`, upper and lower deviations for `SetToDeviation`. The add-in (088) confirms the calls.
- **Manual checklist for Inventor:** nothing to check in Inventor yet; nothing here reaches Inventor until task 088.

What was built, in `src/InventorAddin.Core/Holes/`:

- `HoleSpec.cs`: `HoleKind`, `HoleSeat`, `HoleTermination`, `HoleStandards` (Type and Standard names, built from `Branding.ShortName`), and the `HoleSpec` record with `Normalised()`. Lengths are in the spec's units (mm or in). Null tolerance, drill point, fastener and tapped depths take the workbook defaults.
- `HoleGeometry.cs`: `HoleGeometry`, `HoleDrillPoint`, `HoleThread`, `HoleTolerance`. Values are in cm and degrees; the tap drill is in table units.
- `HoleRules.cs`: `Build(spec, workbook, centreCount)` returns the geometry, or the problems (`HoleCheck` with a `HoleField`), plus the resolved spec to store. Also `FindTolerance`, `FindDrillPoint`, `ToCm` and `IsoNominalMm` (rounded to 6 decimals of a mm before `Iso286.HoleLimits`).
- Review changes: `HoleRules` also checks that a blind countersunk hole is deeper than its countersink, which is (csink dia - hole dia) / 2 / tan(angle / 2) deep. A depth that is not deeper is a problem on `HoleField.Depth`. `HoleStoredData.ToAttributes` throws `ArgumentException` on a spec that is not resolved, so it never writes data `Read` would refuse. That covers a seated hole without a fastener, a dowel without a tolerance, a blind clearance or dowel hole without a depth, and a blank size. `Read` is as strict as before.
- `HoleStoredData.cs`: `HoleAttribute`, `HoleStoredData` (`AWB_Hole`, `Version` 1, `ToAttributes`, `Read`), `HoleAppliedState` (diameter, tolerance, coloured, stored with the spec), and `HoleStateComparison.Compare` for the re-apply offer.

Behaviours picked to keep work moving (not decided by Aidan; the parent may log them as Default):

- A seat on a tapped hole is refused with a message, because the workbook gives it no sizes (spec 09 open question 6, added by this task).
- A tapped hole that is Through All or To a face is threaded full depth, and its thread depth is not checked.
- Rows hidden with `Show = N` are still found by size, so a hole made before its size was hidden still opens and builds.
- A dowel whose ISO code or size `Iso286` does not cover (for example over 500 mm) is a problem, so it never reaches Inventor without limits to show.
- The re-apply comparison compares against the state stored when the hole was applied, not against the current workbook, so a workbook edit alone does not trigger the offer. An ISO tolerance matches by code when Inventor reports one and by limits otherwise. Lengths within 1e-5 cm count as equal.
- Stored lengths are written as doubles in the spec's units and read back as decimals (15 significant digits).

## Follow-ups

- Spec 09 open question 6 (seat sizes for a tapped hole) is for Aidan. Once he answers, `HoleRules.BuildTapped` needs the seat lookup in place of the refusal.
- Task 085 (view-model) should list sizes, fasteners, tolerances and drill points without hidden rows, preselect with `HoleRules.FindTolerance`/`FindDrillPoint`, and show `HoleCheck` messages by `HoleField`.
- Task 088 (add-in) should store `HoleBuildResult.Resolved` with `HoleAppliedState.From(geometry)`. To compare, it should read the hole's current diameter, tolerance (fit code or deviations, in cm) and colour into a `HoleAppliedState`.
