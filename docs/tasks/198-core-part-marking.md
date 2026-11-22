# 198: Part marking: options, note text and window model (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 2; open question 16)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 196

## Goal

Core holds the marking options, builds the marking note text, and the window's view-model.

## Scope

- `src/InventorAddin.Core/Drawing/PartMarking*.cs`: method (laser engrave, stamp, etch, ink, none), extra lines, target (leader or zone), engrave feature on/off, character height and depth per units, highlight colour from Settings (`PartMarkingColour`, default `#0070C0`).
- `src/InventorAddin.Core/ViewModels/PartMarkingViewModel.cs`.
- Tests.

## Acceptance criteria

- [ ] Note text, for example "LASER ENGRAVE PART NUMBER, 3 mm CHARACTERS" then each extra line; method None gives only the extra lines (for example "DO NOT STAMP").
- [ ] The zone text "MARKING MUST BE PLACED INSIDE THIS ZONE"; zone size 40 by 15 mm (imperial 1.5 by 0.6 in).
- [ ] The engrave option is off for an assembly.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

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
