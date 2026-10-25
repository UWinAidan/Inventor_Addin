# 127: Centre-drill spot: a small countersink on any hole, and a seat depth of 0

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats"; question 20)
- **Status:** todo (seat depth 0 completed from spike 108 question 7)
- **Depends on:** 108, 126
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-25: a centre-drill spot should be easy: a small countersink at 60 or 90 deg on any hole type. Also check whether Inventor takes a seat depth of 0, and allow it if it does.

## Scope

- Core (`ViewModels/AwbHoleViewModel.cs`): the countersink angle box offers 60 and 90 deg as quick choices (an editable dropdown; other angles can still be typed) on every type (Default). The countersink diameter starts as task 126 says.
- Seat depth 0: as spike 108 question 7 finds. If Inventor accepts a counterbore or spotface depth of 0, the depth check allows 0 (and the diagram draws no seat wall); if it refuses, the check keeps "above zero" with a message that names why. Record the answer in spec 09 question 20.
- Add-in: the window's angle box as a dropdown; nothing else changes if 126 is done.

## Acceptance criteria

- [ ] A tapped, clearance, dowel or Simple hole can take a 60 or 90 deg countersink in two clicks after choosing Countersink.
- [ ] A seat depth of 0 is allowed or refused as spike 108 found, with tests.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Tapped M6, Countersink, angle 60, diameter 7: the spot is in the model.
  2. Dowel 6 mm, Countersink 90: likewise.
  3. Counterbore depth 0: allowed or refused as the spike found.

## Follow-ups
