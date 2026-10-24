# 113: Core: toleranced Clearance and Simple holes, Custom tolerance

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Toleranced clearance and Simple holes", "Dowel and precision holes"); `docs/features/04-hole-table-wizard.md` (question 1)
- **Status:** todo
- **Depends on:** 112
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 110, 119

## Goal

Core can describe a toleranced Clearance or Simple hole and a Custom tolerance (Aidan, 2026-10-24), and says which holes are precision, for the colour now and the hole table later. The add-in side is task 115.

## Scope

- `HoleSpec.cs`, `HoleGeometry.cs` (`HoleTolerance`): a Toleranced flag on Clearance (AWB Clearance) and Simple holes; a Custom tolerance with Upper and Lower in the window's units (numbers only, Upper above Lower; Default).
- The list (Default, question 17): for a toleranced Clearance or Simple hole every Precision Tol row for the units, the Default = Y row preselected, then Custom; for a dowel the rows of its fit, then Custom (Default, question 18). Each entry's text at the hole's modelled diameter (the Clearance size at its fit, or the Simple diameter), as task 098 does for dowels.
- One Core rule for "is precision": a dowel, or a toleranced Clearance or Simple hole (Aidan); a hole with an Inventor tolerance on its diameter and no AWB data counts too (Default, spec 04 question 1). Used for the colour here and by the hole table in M4 part 2.
- The colour (Default, question 17): the chosen Precision Tol row's fit colour; Custom takes the normal colour.
- Unticking Toleranced, or changing the hole to a type that cannot be toleranced, removes the tolerance and the colour, as for a dowel (spec 09, spike round 5); task 096's every-face reset applies.
- `HoleStoredData.cs`, `HoleReadBack.cs`, `HoleReapply.cs`: the toleranced state, the row or the Custom limits.
- `ViewModels/AwbHoleViewModel.cs`: the Toleranced checkbox (one row, off by default, Clearance and Simple only), the Tolerance row it opens, the Custom Upper and Lower boxes.
- `HoleDiagram.cs`: the tolerance on the diameter label where the mockup shows one for dowels.
- Tests for all of it.

## Acceptance criteria

- [ ] Toleranced is offered on Clearance and Simple only, off by default; ticked, the list and Custom appear as above.
- [ ] Custom refuses Upper not above Lower and non-numbers, with a message, and OK waits.
- [ ] The precision rule and the colour choice are Core functions with tests.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

## Follow-ups
