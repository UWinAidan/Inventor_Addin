# 174: AWB Hole started with a sketch point selected picks that point and the sketch's free hole centres

- **Milestone:** M4 part 1 (fix from Aidan's check of 168 to 173, 2026-11-20)
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** 154
- **Needs:** windows build
- **Parallel-safe with:** 175 (disjoint files; run one after another anyway, they share the build)

## Goal

Bug (Aidan, 2026-11-20): AWB Hole started from a sketch point's mini toolbar (or with sketch points selected some other way) picks every hole-centre point of that sketch, including points other holes already use. Decision (Aidan, 2026-11-20): apply task 154's rule. The picks are the selected point(s) plus the sketch's **free** hole centres; if every hole-centre point of the sketch is used, all of them are picked.

## Scope

- `src/InventorAddin.Core/Holes/HoleSessionRules.cs` and its tests: the `sketchOfSelectedPoint` branch of `DefaultCentres` takes a way to tell which points were selected (for example `Func<TPoint, bool> selected`, or the selected points of that sketch) and returns, in sketch order, the hole centres that are selected or free. When every hole centre of the sketch is used, all are picked (`AllUsed`). `usedByFeature` is now asked for this branch too; update the doc comments. `DefaultCentres.Describe()` gets log text for the branch, for example `"selected point(s), 1 selected and 3 free of 5 hole centres"` and `"selected point(s), all 5 hole centres are used, so all are picked"`.
- `src/InventorAddin/Holes/HoleSession.cs`, `StartDefaultPicks`: collect every selected `SketchPoint` whose parent is the first selected point's sketch, and pass them. Match selected points to the sketch's points as the session already matches points elsewhere (same object, or how `_picks` compares points; check before choosing).
- `docs/features/09-awb-hole.md`, "Hole centres": replace "the sketch of selected points gives all its hole centres, used or not" with the new rule and cite Aidan, 2026-11-20, task 174.

Out of scope:

- A selected sketch, a browser-clicked sketch, nothing selected (unchanged, task 154 and 117).
- Clicking points while the window is open (unchanged).

## Acceptance criteria

- [ ] Core tests: a selected free point gives the sketch's free points; a selected **used** point gives that point plus the free points; two selected points (one used) give both plus the free points; every point used gives all with `AllUsed`; a selected point that is not a hole centre does not add itself (the picks are hole centres, as now); the order is sketch order; `Describe()` texts for each case.
- [ ] Existing tests that asserted "a selected point's sketch gives all" are changed to the new rule, not deleted.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows, 0 errors.

## Notes for the implementer

- `FreeOrAll` is the existing helper for the other branches; extend it or add a sibling, don't duplicate the rule.
- A selected point that is not marked as a hole centre: keep the current behaviour (the picks come from the sketch's hole centres). Say so in the Verification section.
- Inventor may be running: check `tasklist //FI "IMAGENAME eq Inventor.exe"` and build with `-p:DeployToInventor=false` while it runs.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
