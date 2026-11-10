# 154: A sketch clicked in the browser, or selected before the window opens, picks its free hole centres

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** 145, 117
- **Needs:** windows build
- **Parallel-safe with:** 152, 153 (disjoint files; run one after another anyway, they share the build)

## Goal

Decision (Aidan, 2026-11-10, checklist 145-3): clicking a sketch in the browser while the AWB Hole window is open picks only its **free** hole centres, the hole-centre points no feature uses yet. Used points can still be picked by clicking them directly. If every hole-centre point of that sketch is used, all of them are picked. The same rule applies to a sketch selected before AWB Hole opens (task 117, checklist 117-4). This replaces task 145's "all its hole centres" and task 094/117's "a selected sketch gives all its centres".

## Scope

- `src/InventorAddin.Core/Holes/HoleSessionRules.cs` and tests: `CentresOfPickedSketch` and the selected-sketch branch of `DefaultCentres` take the free points, or all points when none is free. Update the doc comments and the log text (for example "its 3 free hole centres of 5 are picked" and "all 5 are used, so all are picked").
- `src/InventorAddin/Holes/HoleSession.cs`: pass the "used by a feature" check the default path already uses to the browser-click path. The sketch of a selected **point** (not a selected sketch) is unchanged: it still gives all its centres.
- `docs/features/09-awb-hole.md`, "Hole centres": update the sentences for a selected sketch and a browser-clicked sketch.

Out of scope:

- The default sketch when nothing is selected (unchanged: newest visible with free centres).
- Clicking single points (unchanged).

## Acceptance criteria

- [ ] Core tests: a sketch with some used points gives only the free ones; a sketch with every point used gives all; a sketch with no hole centres leaves the picks; the selected-sketch branch of `DefaultCentres` follows the same rule; a selected point's sketch still gives all.
- [ ] `dotnet build InventorAddin.slnx` on Windows, 0 errors; Core tests pass.

## Notes for the implementer

`DefaultCentres` already filters with `usedByFeature` for the newest-sketch branch; reuse it. Inventor may be running: check `tasklist //FI "IMAGENAME eq Inventor.exe"` and build with `-p:DeployToInventor=false` while it runs.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups
