# 095: Tapped holes show their thread in the model

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Tapped holes")
- **Status:** todo
- **Depends on:** 090
- **Needs:** windows build (and its own invisible Inventor for the investigation)
- **Parallel-safe with:** none (changes `HoleFeatureWriter` like 093)

## Goal

Fix the bug Aidan found in the 090 check: tapped holes made by AWB Hole show no thread texture in the model, while Inventor's own tapped holes do. Find out whether the tap drill workaround (`_CreateTapInfo` with `kThreadTapDrillDiameter`, spike 081) causes it, and make AWB tapped holes show their thread while keeping the workbook's tap drill if at all possible.

## Scope

- Investigation with a throwaway console app in the scratchpad, driving its own invisible Inventor (as spike 081 did; see task 093's notes). Never attach to an Inventor Aidan has open.
- `src/InventorAddin/Holes/HoleFeatureWriter.cs`; Core if a rule changes, with tests

## Acceptance criteria

- [ ] On a scratch block, make tapped holes four ways and compare them: Inventor's `CreateTapInfo` (minor diameter), `_CreateTapInfo` as the writer calls it now, `_CreateTapInfo` with the thread's major and minor diameter limits filled in (arguments 10 to 15, which the writer passes as missing), and `_CreateTapInfo` with `kThreadMinorDiameter`. For each, record the `HoleTapInfo` values, whether the thread shows (render the view to a bitmap, for example `Camera.SaveAsBitmap` or `View.SaveAsBitmap`, and look at it; say which you used), and the modelled diameter.
- [ ] If one way shows the thread and models the tap drill, the writer uses it. If none does, write what was found and the choices in Verification and in spec 09 as an open question (thread texture or our tap drill), keep the tap drill as it is, and stop there.
- [ ] Also check the document's thread display setting, if there is one, and Inventor's tapped hole in the same part, so the comparison is fair.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Tapped M6, Through All and blind: the thread texture shows in the model, like an Inventor tapped M6 beside it; the modelled diameter is the workbook's tap drill.
  2. An imperial row (1/4-20 UNC, #10-32 UNF): the same.

## Follow-ups
