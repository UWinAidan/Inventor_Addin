# 156: Core: hole table descriptions for every hole type

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output", open question 14; question 15 answered 2026-11-17: drawing symbols)
- **Status:** todo
- **Depends on:** 155
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 157, 160

## Goal

Core writes the DESCRIPTION cell for any `HoleTableHole`: every type and seat Aidan put in scope, through or blind, toleranced or not, in millimetres or inches. Holes with the same description share a group (task 158), so the text must be identical for identical holes and differ for any real difference.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableDescription.cs` (or similar). `HoleTableDescription` holds the cell as lines of parts, so that task 161 can turn them into Inventor formatted text with Inventor's own symbols.
  - A part is plain text or a symbol: `Diameter`, `Counterbore`, `Countersink`, `Depth` or `Spotface`.
  - `Lines`: the lines, top to bottom.
  - `Tolerance`: null, or the upper and lower deviation texts for the first line. The stack sits between that line's size and fit and its termination, so keep the first line as two part lists, one before the stack and one after.
  - `Key`: one string for grouping and the order tie-break (task 155). It is built from the lines and the tolerance, with the symbols written as Unicode: Ø U+2300, counterbore U+2334, countersink U+2335, depth U+21A7, and `SF` for spotface.
  - `PlainText`: the same Unicode form, for the wizard's lists and logs.
- `HoleTableDescriptions.For(HoleTableHole hole, HoleTableDecimals decimals)`. The format uses Inventor's drawing symbols, as spike 160 drew them in rounds 3 to 7 (Aidan, 2026-11-17, question 15). The examples below show the Unicode forms:
  - **Hole line:** `Ø6.6 THRU`, or `Ø6.6 ↧12.0` for a blind hole. A blind hole's depth is the drilled depth as Inventor measures it (`InventorHoleFacts`). For a spotface hole that depth is measured from the spotface floor (spike 108).
  - **Precision hole:** `Ø6.00 H7`, then the deviations stacked (`+0.012` over `+0.000`), then `THRU` or the depth. A Custom tolerance has no code, only the deviations. Deviations always show their sign, with three decimals in mm and four in inches.
  - **Seat line,** after the hole line: `⌴Ø11.0 ↧6.4`, `⌵Ø12.4 X 90°`, or `SF Ø14.0 ↧1.0` (the spotface symbol, then the diameter).
  - **Tapped:** the drill line, then the thread line. The thread line is the designation with its class (`M6x1 - 6H`, from Inventor's `ThreadDesignation` and `Class`), followed by `↧10.0`, or by `THRU` for a full-depth thread on a through hole.
  - **Taper tapped:** the drill line, when the tap drill is known. Then the designation with the engagement length when it is known (`Rc 1/8 ↧6.5`; spike 160 round 3 read it from `TaperedThreadInfo.EngagementLength`, in mm), else the designation alone.
  - **Termination To (a face):** `TO FACE` in place of a depth.
- `HoleTableDecimals` (task 155; question 14): descriptions use their table's decimals; the angle is a whole number of degrees. Diameters and depths convert from cm to the hole's units here.
- Number formatting is invariant culture, with trailing zeros kept to the decimals (`6.60`, not `6.6`, in the precision table).

Out of scope: Inventor formatted text and the AIGDT characters (task 161), and anything under `src/InventorAddin`.

## Acceptance criteria

- [ ] One test per type and seat: plain through, plain blind, dowel H7 through, toleranced Simple with Custom limits, counterbore, countersink, spotface, tapped blind, tapped through full depth, tapped through with a thread depth, tapped with a counterbore, taper tapped with and without a known tap drill, termination To.
- [ ] mm and inch versions of at least the plain, precision and tapped cases, with the decimals of question 14.
- [ ] Two holes that differ only in position have equal `Key`s; a difference in depth, seat size, thread, tolerance code or deviations gives different `Key`s.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- `Iso286` and `HoleTolerance` (`Holes/HoleGeometry.cs`) already exist; the deviations come from the hole's `HoleTolerance` as read, not recomputed.
- Spike 160's description code is `Read-Holes` in `spikes/160/spike160-round7.ps1`, on branch `spike/160-hole-table` (`git show spike/160-hole-table:spikes/160/spike160-round7.ps1`).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none expected
- **Inventor API members not confirmed:** none expected
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.
