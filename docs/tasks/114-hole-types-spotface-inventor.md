# 114: Simple and Taper Tapped holes and spotfaces in Inventor and the window

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats", "Simple holes", "Taper tapped holes")
- **Status:** todo (completed from spike 108's results)
- **Depends on:** 108, 112, 119
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The AWB Hole window makes, changes and reads back Simple and Taper Tapped holes and spotfaces, and any seat on any type, as real Inventor hole features (Aidan, 2026-10-24).

## Scope

- `src/InventorAddin/Holes/HoleFeatureWriter.cs`, `HoleFeatureReader.cs`, `HoleStoredDataStore.cs`: as spike 108 recommends for the spotface, the tapered thread (tap drill and thread display, as task 095 did for tapped holes), seats on tapped and taper tapped holes, and a Simple diameter that takes expressions (as task 100's depths). A change between types follows task 088's rule (recreate in its browser place when Inventor cannot change it in place).
- `src/InventorAddin/UI/AwbHoleWindow.xaml(.cs)`: the four Type buttons, the Spotface seat button, the Diameter box, the seat boxes for spotfaces and for seats on Simple and Taper Tapped holes, bound to task 112's view-model; the icons from task 119.
- Edit (task 092): a hole made by Inventor's command with a spotface or a tapered thread opens as the right type.

## Acceptance criteria

- To be completed from spike 108. At least:
- [ ] Each type with each seat can be made, changed in the window, OK'd, undone in one step, cancelled, and edited again.
- [ ] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

## Follow-ups
