# 127: Centre-drill spot: a small countersink on any hole, and a seat depth of 0

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats"; question 20)
- **Status:** todo (completed from spike 108, 2026-10-28)
- **Depends on:** 108, 126
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-25: a centre-drill spot should be easy: a small countersink at 60 or 90 deg on any hole type. Also check whether Inventor takes a seat depth of 0, and allow it if it does.

## Scope

- Core (`ViewModels/AwbHoleViewModel.cs`): the countersink angle box offers 60 and 90 deg as quick choices (an editable dropdown; other angles can still be typed) on every type (Default). The countersink diameter starts as task 126 says.
- **Seat depth 0 is allowed** (spike 108 question 7: Inventor accepts a counterbore or spotface depth of 0, keeps the hole type and models only the hole). The depth check allows 0 for counterbore and spotface depths, and the diagram draws no seat wall. Spec 09 question 20 is answered by this.
- Add-in: the window's angle box as a dropdown; nothing else changes if 126 is done.

## Acceptance criteria

- [ ] A tapped, clearance, dowel or Simple hole can take a 60 or 90 deg countersink in two clicks after choosing Countersink.
- [ ] A counterbore or spotface depth of 0 is allowed, with tests; a negative depth is still refused.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Tapped M6, Countersink, angle 60, diameter 7: the spot is in the model.
  2. Dowel 6 mm, Countersink 90: likewise.
  3. Counterbore depth 0 and spotface depth 0: the hole is made and Edit reads the seat back with depth 0.

## Follow-ups
