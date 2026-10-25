# 123: The seat is kept when type, standard or size changes

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Types and seats", "Editing reads the hole first")
- **Status:** todo
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

- [ ] The cases above keep or restore the seat as described.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Task 124 changes the same cascades for typed values: if both are in progress, run this first and leave a clear hook for 124.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Tapped M6, Counterbore, change size and seat; change Type to Clearance: the counterbore stays. Back to Tapped: still there. OK, Edit: the counterbore is there.
  2. Dowel 6 mm with a counterbore, change Standard to AWB Clearance and back: the counterbore stays.

## Follow-ups
