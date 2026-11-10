# 123: The seat is kept when type, standard or size changes

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats", "Editing reads the hole first")
- **Status:** done
- **Depends on:** 099, 106
- **Needs:** cloud or windows (Core)
- **Parallel-safe with:** 122

## Goal

PR #29 check 099-3 failed (Aidan, 2026-10-25): a tapped M6 with a counterbore, changed to Clearance, lost the counterbore, and switching back to Tapped did not bring it back; the same with a 6 mm dowel and counterbore. **The seat must be kept when type, standard or size changes, as long as the new choice has a row for it** (Aidan).

## Scope

- `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs`: the cascades. Changing Type, Standard or Size keeps the chosen seat (None, Counterbore, Countersink) when the new choice offers it; otherwise the seat falls back to None with no message, and the seat chosen before is remembered so that a change back to a choice that offers it brings it back (within the window's life, not across restarts).
- With task 126 (editable seat boxes on every type) the seat is offered on every type wherever it can be sized; until 126 runs, "has a row" means the seat list offers it today (task 099).
- This replaces the task 085 Default "changing Type puts the seat back to None" (Aidan, 2026-10-25; logged in `DECISIONS.md`).
- Tests in `tests/InventorAddin.Core.Tests` for: tapped M6 counterbore to Clearance and back; dowel 6 counterbore to AWB Clearance and back; a size with no row and back.

## Acceptance criteria

- [x] The cases above keep or restore the seat as described.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Task 124 changes the same cascades for typed values: if both are in progress, run this first and leave a clear hook for 124.

## Defaults taken

- **What is remembered is the seat the user chose last**, None included: choosing None by hand at a size and then changing back to a size that offers the old seat keeps None. Only a cascade's fallback to None remembers the earlier seat.
- **An edited hole's own seat stays listed until the units, type, standard or size change** (as task 085's rule for an edited hole's missing choices): an edited tapped hole whose size has no row for its seat still opens on that seat with its problem, a change below it (Flip, termination) keeps it, and a size change drops it to None like any other seat. Before this task the seat stayed listed with a message after any size change (task 099 Default "a chosen seat with no row at a new size is kept with a message and OK disabled"); the task's rule (fall back to None with no message) replaces that for a new choice.
- **Only the seat is remembered, not the Fastener type**: when the seat comes back, the Fastener type is the one the new list holds (the earlier one if still listed, else the first). See Follow-ups.

## Verification

- **Ran:**
  - `dotnet test tests/InventorAddin.Core.Tests`: passed 3799, failed 0, skipped 0.
  - `tasklist` showed Inventor not running; `dotnet build InventorAddin.slnx -tl:off`: build succeeded (add-in deployed to `%APPDATA%\Autodesk\Inventor 2026\Addins`), 0 errors; only the existing CS8600 warnings in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand`, `SaveAsReplaceCommand`.
  - New and changed tests in `tests/InventorAddin.Core.Tests/ViewModels/AwbHoleViewModelTests.cs`: tapped counterbore to Clearance and back; a seat the new type cannot take comes back when the type changes back; dowel counterbore to AWB Clearance and back; a dowel seat kept across a type change; a tapped size with no row and back (replaces the task 099 "is kept and says why" test); a seat set to None by hand stays None; an edited hole's seat stays until the size changes; `ChangingTheType_PutsTheSeatBackToNone` replaced by `ChangingTheType_KeepsASeatTheNewTypeOffers`.
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` changed. The whole solution was built on Windows.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:**
  1. Open a part with a sketch point. AWB Hole: Type Tapped, Size M6, Seat Counterbore. Change Type to Clearance: the seat is still Counterbore and the preview shows the counterbore. Change Type back to Tapped: still Counterbore. OK, then Edit the hole: it opens on the counterbore.
  2. New AWB Hole: Clearance, Standard AWB Dowel, 6 mm, Seat Counterbore. Change Standard to AWB Clearance: still Counterbore. Back to AWB Dowel: still Counterbore, with its seat boxes.
  3. Tapped, a size with a countersink row, Seat Countersink. Change Size to one with no countersink row in your workbook (if there is one): the seat shows None with no message under Seat and the preview has no countersink. Change Size back: Countersink comes back.
  4. Repeat 3 but set Seat to None by hand at the size with no row, then change Size back: the seat stays None.
  5. Clearance with a countersink, change Type to Tapped and back to Clearance: the countersink is there again (it shows None on Tapped only if that size has no countersink row).

## Follow-ups

- The Fastener type is not remembered when a seat comes back (for example TLOW chosen, seat dropped and brought back gives the first fastener of the list). Aidan may want the fastener remembered the same way; it fits naturally with task 124's "typed values kept" work.
- Spotface and Taper Tapped are not in the view-model yet (their tasks have not run); when they land, the seat list for Taper Tapped (no Counterbore) goes through the same `RefreshSeats` fallback and the remembered seat, so nothing extra should be needed beyond listing the seats.
- Task 126 (seat boxes on every type) widens "offers it" for tapped holes; `RefreshSeats` is the one place to change.
