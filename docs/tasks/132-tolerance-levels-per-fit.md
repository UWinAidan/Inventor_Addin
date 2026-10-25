# 132: Several tolerance levels per fit

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes")
- **Status:** todo
- **Depends on:** 111, 113
- **Needs:** cloud or windows (Core and the starter workbook)
- **Parallel-safe with:** none (the starter workbook is shared with 111, 131)

## Goal

Aidan, 2026-10-25: more than one tolerance level per fit, for example light, medium and heavy press, close and loose slip, and a couple of normal levels. The starter Precision Tol tab gets such rows, metric and imperial, with clear names, and the Tolerance dropdown lists every row for the chosen fit.

## Scope

- `libraries/AWB_Holes.xlsx` (starter): Precision Tol rows, metric and imperial, invented by us from ISO 286 hole codes and explicit limits, with clear names (Default), for example:
  - Press: Light press (P7), Medium press, Heavy press
  - Slip: Close slip (G6), Loose slip (F7)
  - Normal: Normal (H7), Normal fine (H6)
  - one Default = Y row per fit and units.
  Pick codes that Core's ISO 286 table covers (task 083: F, G, H, JS, K, M, N, P at IT5 to IT11) and say in the How to edit tab what each level is for. Imperial rows as Limits rows in inches.
- Core: check the list already shows every row of the fit (spec 09, task 098) and that one Default per fit and units is preselected; if two rows of a fit say Default = Y, the first wins and a problem names the second (Default). Tests with the new starter workbook.
- The secondary text in the list is the row's name, so the names must read well there.

## Acceptance criteria

- [ ] The starter workbook has at least two levels for each fit, metric and imperial, and loads with no problems.
- [ ] The Tolerance dropdown lists every row for the chosen fit and units, with the tolerance at the size and the name.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Dowel 6 mm, Press: the list shows light, medium and heavy press with their limits. Pick each: the tolerance in Inventor's hole dialog matches.
  2. Slip and Normal: their levels are listed. Imperial likewise.

## Follow-ups
