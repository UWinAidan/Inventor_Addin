# 099: Counterbore and countersink on tapped and dowel holes

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats"; questions 6 and 9)
- **Status:** todo
- **Depends on:** 093, 098
- **Needs:** windows build
- **Parallel-safe with:** 096

## Goal

Aidan, 090 check: a counterbore or countersink seat is allowed on tapped holes and on dowel holes. A tapped hole uses the row of the same size in the Counterbore or Countersink tab (Aidan, question 6). A dowel hole's seat sizes come from the same tabs, matched the same way, until Aidan answers question 9 (Default).

## Scope

- Core: `HoleRules` (drop the refusal of a seat on a tapped hole; the size match; checks), `HoleSpec`/`HoleGeometry`, `HoleStoredData` and `HoleReadBack` if a seated tapped or dowel hole stores or reads differently, `AwbHoleViewModel` (Seat and Fastener type for Tapped and AWB Dowel; Fit and Tolerance stay for a seated dowel), with tests
- `src/InventorAddin/Holes/HoleFeatureWriter.cs` (a seat with a tap info; a seated dowel's tolerance and colour); `HoleFitWriter` if needed

## Acceptance criteria

- [ ] **Matching a seat row** (Default for both, spec 09): the Counterbore or Countersink rows, same units, whose Size has a Clearance row with the same nominal diameter as the thread's or the dowel's nominal. For example the thread `M6` (nominal 6) and a 6 mm dowel both use the `M6` rows; `#10-32 UNF` uses `#10`; `1/4-20 UNC` and a 1/4 in dowel use `1/4`. The Fastener type list holds those rows' Screw values. A size with no matching seat row offers no seat other than None, and a seat already chosen says why it cannot be kept.
- [ ] Tapped with a seat: the thread and tap drill as today, the seat from the row; blind and Through All; full-depth and blind thread rules as today. Inventor makes it with the seat methods taking a tap info, as the writer does for untapped holes.
- [ ] Dowel with a seat: nominal diameter, tolerance and fit colour as today; every face coloured, the seat faces too (Aidan: every face); Fit and Tolerance stay shown.
- [ ] Checks: the seat must be wider than the hole (the tap drill's major diameter for a tapped hole: the thread's nominal), and a blind hole deeper than the seat; refusals say which value is wrong.
- [ ] Changing a seated tapped or dowel hole's size, seat or type works in both directions without refusals (task 093's order rule), and editing (stored data, read back) keeps the seat.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Tapped M6 with a counterbore (SHCS): the counterbore is the `M6` SHCS row's, the thread and tap drill as before; Through All and blind. The same with a countersink. An imperial thread (`#10-32 UNF`) uses the `#10` rows.
  2. Dowel 6 mm with a counterbore: the hole at 6 mm with its tolerance, the counterbore from the `M6` row, every face, seat included, in the fit colour.
  3. Change size, seat and type on these holes while previewing: no refusal; OK, edit again (after 092): the seat is kept.

## Follow-ups
