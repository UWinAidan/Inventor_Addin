# 112: Core: Simple and Taper Tapped types, Spotface, any seat on any type

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Types and seats", "Simple holes", "Taper tapped holes")
- **Status:** todo
- **Depends on:** 111
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 110, 119

## Goal

Core can describe, check, store and lay out every type with every seat (Aidan, 2026-10-24): Clearance, Tapped, Simple and Taper Tapped, each with None, Counterbore, Countersink or Spotface. The add-in side is task 114.

## Scope

- `HoleSpec.cs`: `HoleKind` gains Simple and TaperTapped; `HoleSeat` gains Spotface; `HoleStandards` gains the Simple and Taper Tapped types and the "AWB Taper Tapped" standard (Simple has none).
- `HoleRules.cs`, `HoleSeatCheck.cs`, `HoleGeometry.cs`: geometry and checks for the new cases (a spotface wider than the hole, a seat deeper than a blind hole, a taper tapped tap drill); a Simple hole's diameter from the Diameter box.
- Seat sizes, as spec 09 "Types and seats" and "Seat boxes" (Defaults, question 19): a spotface from the Spotface row of the same size, always editable; any seat on Simple and Taper Tapped sized in boxes, starting from the last values used with OK. Extend `HoleLastDowelSeats` or add a sibling for Simple (per units and seat) and Taper Tapped (per units, size and seat) seats, and a last Simple diameter per units, kept in `AddinSettings` across restarts.
- `HoleStoredData.cs`: the new kinds, the spotface and the Simple diameter (with any expression); bump the stored version and keep reading the old one. `HoleReadBack.cs`, `HoleEditRules.cs`, `HoleReapply.cs`: the new cases.
- `ViewModels/AwbHoleViewModel.cs`: Type lists four; Standard hidden for Simple and showing AWB Taper Tapped for Taper Tapped; Size hidden for Simple; a Diameter box (expressions, as Depth, task 100); Fastener type only for a clearance or tapped hole with a counterbore or countersink; the seat boxes for a spotface on every type and for any seat on dowel, Simple and Taper Tapped; thread and drill depths for blind Taper Tapped holes; the cascades when Type changes (seat kept where the new type can take it, as spec 09 "Editing reads the hole first").
- `HoleDiagram.cs`: a spotface, and the new types (a taper thread drawn as the tapped thread, or as the mockup's style).
- `HoleIcons.cs`: names for the new Type and Seat buttons; the drawings are task 119.
- Tests for all of it in `tests/InventorAddin.Core.Tests`.

Out of scope:

- Tolerancing (113); depths below the seat (116); writing to Inventor (114).

## Acceptance criteria

- [ ] Every type with every seat can be described, checked, stored and read back in Core; old stored data still reads.
- [ ] The view-model shows and hides fields as spec 09's table says, for each type.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Spike 108 may not be done when this runs. Where its answer changes Core (for example whether a taper tapped hole can be through), choose the simplest rule, note it in Verification, and task 114 adjusts it.

## Verification

Filled in by the implementer.

## Follow-ups
