# 182: Fix: a tapped hole changed in Inventor shows its actual drill

- **Milestone:** M4 part 2 (fix soon, from task 179's review)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output"; open questions 11 and 19)
- **Status:** done (pending Inventor check)
- **Depends on:** 179
- **Needs:** windows (Core and add-in)
- **Parallel-safe with:** none

## Goal

Task 179 made a straight tapped hole's drill diameter come from, in order: AWB's stored hole diameter, Inventor's hole diameter, the workbook Tapped row. An AWB-made M6 tapped hole later changed to M8 in Inventor's own Hole dialog keeps its stored 5.0 drill, so the hole table prints Ø5.0 against an M8 thread: a wrong number on a print. After this task the table shows the drill the hole actually has.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableHole.cs` (`HoleTableHoles.From`), straight tapped holes only. The drill diameter becomes:
  1. Inventor's hole diameter (`facts.HoleDiameterCm`) when it was read. When the stored diameter is also given and the two agree within `HoleStateComparison.LengthToleranceCm`, use the stored value (so an unchanged AWB hole prints exactly as today); when they differ, Inventor's wins.
  2. Otherwise the stored diameter, but only when the stored data still describes this hole's thread (see Notes). When it does not, skip it.
  3. Otherwise the Tapped row's tap drill for the hole's actual thread (as now).
  Update the `storedHoleDiameterCm` and `tappedRow` parameter docs and the inline comment to give the new order and cite task 182. Taper tapped and other types are unchanged.
- `src/InventorAddin/HoleTable/HoleTableSource.cs`: pass whatever extra the rule in step 2 needs (if anything), and log the "tap drill could not be read" warning once per hole per session instead of on every automatic check (a static set of `featureName` plus document path, or similar; keep it simple).
- Tests in `tests/InventorAddin.Core.Tests` (next to task 179's in `HoleTableTests`).

Out of scope: the description layout; taper tapped holes; the AWB Hole window; the other 179 follow-up about looking up the workbook for every tapped hole.

## Acceptance criteria

- [ ] Core: stored 0.5 cm, Inventor's 0.68 cm: the drill line uses 0.68 (M6 changed to M8 in Inventor).
- [ ] Core: stored 0.5 cm, Inventor's 0.5 cm plus float noise below the tolerance: the drill is exactly the stored value.
- [ ] Core: stored given, Inventor's not read, stored thread matches the actual thread: the stored value (task 179's case, unchanged).
- [ ] Core: stored given, Inventor's not read, stored thread does not match the actual thread, Tapped row for the actual thread given: the row's tap drill.
- [ ] Core: no stored, Inventor's read: Inventor's; nothing at all: no drill line (question 19), as now.
- [ ] Core: a test checks the whole drill line, diameter and the `↧` depth (task 179's test gap).
- [ ] Task 179's test "AWB value wins when both differ" is changed to the new rule, not deleted silently; say so under Verification.
- [ ] Add-in: no new Inventor API member. The warning is written once per hole per session.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

- Why the stored value came first in 179: it followed `HoleDepthReference`'s order, and it covered a tapped hole whose diameter parameter throws (task 170). Inventor's own diameter for an unchanged AWB hole is the workbook tap drill (task 095: the bore is modelled at the tap drill), so preferring Inventor's when it reads loses nothing.
- "The stored data still describes this hole's thread" (step 2): `awbSpec` is the stored `HoleSpec`; it names the AWB thread size. Compare it with the actual thread designation in `facts.ThreadDesignation` through the existing workbook match (`HoleReadBack.TappedRowFor` gives the row for the actual thread; the stored spec's row is the one its size names). Reuse existing matching; do not write a new designation parser. If the spec's size cannot be compared cleanly, treat a stored value whose diameter differs from the actual thread's row tap drill (beyond the tolerance) as stale. Pick one, document it on the parameter, and say which under Verification.
- `HoleStateComparison.LengthToleranceCm` is the length tolerance used by `HoleReapply` and `HoleReadBack`.
- Do not change `HoleFeatureReader.ReadFacts`.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (9 existing warnings). `dotnet test tests/InventorAddin.Core.Tests`: 4969 passed, 0 failed (one earlier run failed only on my own wrong expected string in the new whole-line test, fixed).
- **Stale rule chosen (step 2):** the fallback option, no new parameter and no designation parsing. A stored diameter is stale when the actual thread's Tapped row is given and its tap drill differs from the stored value by `LengthToleranceCm` or more. With no row to compare, the stored value is used. Documented on `storedHoleDiameterCm`.
- **Task 179 test changed:** `Tapped_StoredDiameterWinsOverInventors` ("AWB value wins when both differ") is replaced by `Tapped_InventorsDiameterDiffersFromStored_InventorsWins` (0.5 stored, 0.68 Inventor: 0.68), with a comment in the test file. Also added: noise case, stored-fits-thread, stored-stale, and a whole drill line test (`⌀5.0 ↧12.0`; the real format has no space after the arrow).
- **Review fixes:** warning key is now the part path (via `ComSafe.Get`) plus feature name; stale test uses stored 0.5 with an M8 row (6.8 mm), expecting 0.68.
- **Not compiled (changed under `src/InventorAddin`):** none; `HoleTableSource.cs` was built on Windows in the solution build (0 errors), not run in Inventor.
- **Inventor API members not confirmed:** none new (`PartDocument.FullFileName`, the `Document` member already used elsewhere in the add-in). Untested: that `ReadFacts` reads an M8 hole edited in Inventor's own Hole dialog as 0.68 cm (assumed from task 095).
- **Manual checklist for Inventor:**
  1. In a part, make an M6 tapped hole with AWB Hole. Open it in Inventor's own Hole dialog (not AWB Hole) and change it to M8. Save. On its drawing, Update the hole table: the tapped group shows M8's drill (for example ⌀6.8) above `M8x1.25 - 6H`, not ⌀5.0.
  2. An unchanged AWB M6 tapped hole still shows `⌀5.0 ↧12.0` above `M6x1 - 6H ↧9.0` (rendered spacing may differ slightly).
  3. Leave the drawing open through two automatic checks: any `is tapped but its tap drill could not be read` line appears at most once per hole in the log. To provoke it, make a tapped hole in Inventor's own Hole dialog with a thread size that is not in the workbook and whose diameter will not read; otherwise the warning may not appear at all, which is also fine.

## Follow-ups

Things noticed but not done.

- The warning set is static and never cleared; fine per session. A hole whose workbook row is edited after it was made (thread unchanged) now prints the row's tap drill rather than the stored one, when Inventor's diameter cannot be read.
