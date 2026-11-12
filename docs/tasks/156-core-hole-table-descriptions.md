# 156: Core: hole table descriptions for every hole type

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output", open questions 14 and 15)
- **Status:** todo
- **Depends on:** 155
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 157, 160

## Goal

Core writes the DESCRIPTION cell for any `HoleTableHole`: every type and seat Aidan put in scope, through or blind, toleranced or not, in millimetres or inches. Holes with the same description share a group (task 158), so the text must be identical for identical holes and differ for any real difference.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableDescription.cs` (or similar): `HoleTableDescription` holds the cell as lines, plus the tolerance as separate parts so a renderer can stack them.
  - `Lines`: the text lines, top to bottom.
  - `Tolerance`: null, or the fit code (may be null for Custom) with the upper and lower deviation texts, attached to the first line.
  - `Key`: one string for grouping and the order tie-break (task 155), built from the lines and the tolerance.
- `HoleTableDescriptions.For(HoleTableHole hole, HoleTableDecimals decimals)`. The words (Default, question 15):
  - Hole line: `Ø6.6 THRU` or `Ø6.6 x 12.0 DP`. A blind hole's depth is the drilled depth as Inventor measures it (`InventorHoleFacts`; a spotface hole's from the spotface floor, spike 108).
  - Precision hole: `Ø6.00 H7 THRU` with the deviations `+0.012` and `-0.000` in `Tolerance`; Custom: no code, only the deviations. Deviations always show their sign and three decimals in mm, four in inches.
  - Seat line after the hole line: `C'BORE Ø11.0 x 6.4 DP`, `CSK Ø12.4 x 90°`, `SPOTFACE Ø14.0 x 1.0 DP`.
  - Tapped: the drill line, then `M6x1 x 10.0 DP`, or `M6x1 THRU` for a full-depth thread on a through hole. Taper tapped: the drill line when the tap drill is known, then the designation (`1/4 NPT`, `Rc 1/4`) with no depth (Inventor sets the thread length, spec 09 question 22).
  - Termination To (a face): `TO FACE` in place of a depth.
- `HoleTableDecimals` (task 155; question 14): descriptions use their table's decimals; the angle is a whole number of degrees. Diameters and depths convert from cm to the hole's units here.
- Number formatting is invariant culture, with trailing zeros kept to the decimals (`6.60`, not `6.6`, in the precision table).

Out of scope: how the add-in puts stacked text into a cell (spike 160), drawing symbols in place of words (question 15 stays open), anything under `src/InventorAddin`.

## Acceptance criteria

- [ ] One test per type and seat: plain through, plain blind, dowel H7 through, toleranced Simple with Custom limits, counterbore, countersink, spotface, tapped blind, tapped through full depth, tapped through with a thread depth, tapped with a counterbore, taper tapped with and without a known tap drill, termination To.
- [ ] mm and inch versions of at least the plain, precision and tapped cases, with the decimals of question 14.
- [ ] Two holes that differ only in position have equal `Key`s; a difference in depth, seat size, thread, tolerance code or deviations gives different `Key`s.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- `Iso286` and `HoleTolerance` (`Holes/HoleGeometry.cs`) already exist; the deviations come from the hole's `HoleTolerance` as read, not recomputed.
- Keep the word table in one place so question 15 (symbols instead of words) is a small change.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none expected
- **Inventor API members not confirmed:** none expected
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.
