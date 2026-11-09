# 141: The sketch point mini toolbar keeps every button, with AWB Hole in Hole's place

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo
- **Depends on:** 133
- **Needs:** windows build
- **Parallel-safe with:** 140, 143 (not 142: same file)

## Goal

Product bug from Aidan's checks 133-1, 133-2 and 133-5 (2026-11-09). It happens in normal parts, in place and in sheet metal parts. While AWB Hole has taken over, left-clicking a sketch point shows a mini toolbar with only Extrude and Revolve. Inventor's Hole is removed, AWB Hole is not added, and Edit Sketch and Create Sketch are gone too. With "Use Inventor's Hole" on, the toolbar shows Extrude, Revolve, Hole, Edit Sketch and Create Sketch. AWB Hole must take Hole's place, and every other button must stay where it was.

## Scope

- `src/InventorAddin/Holes/HoleEditRedirect.cs`: `ReplaceMiniToolbarHole` and `OnContextualMiniToolbar`.
  - Log the toolbar's entries (names, in order) **before and after** the swap, at Info, for each mini toolbar on a sketch point, not only in sheet metal.
  - Find why the entries after Hole are lost. Suspects: `Insert`'s index or before/after flag, an entry value Inventor will not take, or `Insert` failing part-way with no exception. Fix it so the result is Inventor's list with `PartDMHoleCmd` replaced by `Awb_AwbHole` at the same position.
  - If the inserted entry would show Inventor's icon or run Inventor's Hole, insert our `ButtonDefinition` (task 133 follow-up).
  - If the list after the swap is not Inventor's list with that one change, put Inventor's list back and log a warning. A broken toolbar is worse than Inventor's Hole.
- `src/InventorAddin.Core/Holes/HoleTakeoverRules.cs` and its tests: any changed index rule, and a rule that checks the list after the swap against the list before.

Out of scope:

- The marking menu (task 142).
- The Edit Feature swap on a hole's mini toolbar (task 092), which works.

## Acceptance criteria

- [ ] Core tests for any changed or new rule.
- [ ] The log has a before line and an after line for each sketch point mini toolbar.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Read task 133's brief and Follow-ups, and `HoleEditRules.MiniToolbarIndex` (the Edit Feature swap). `NameValueMap.Insert` and `Remove` are already in use. Check what `Insert`'s last argument means in the API reference, and say what you found.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. In a part, with AWB Hole active, left-click a sketch point. The toolbar shows Extrude, Revolve, AWB Hole (in Hole's place), Edit Sketch, Create Sketch. The log has the before and after lines. AWB Hole opens on the point.
  2. The same in a part edited in place, and in a sheet metal part.
  3. With "Use Inventor's Hole" on: Inventor's toolbar is unchanged, and Hole opens Inventor's Hole.

## Follow-ups
