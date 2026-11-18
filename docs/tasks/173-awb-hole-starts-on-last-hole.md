# 173: AWB Hole: a new hole starts on the last hole made with OK

- **Milestone:** M4 part 1 (request from Aidan's check, 2026-11-18)
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window")
- **Status:** todo
- **Depends on:** none
- **Needs:** windows (Core and a little add-in wiring; Aidan checks in Inventor)
- **Parallel-safe with:** 171

## Goal

Aidan (2026-11-18): a new AWB Hole starts on the last hole made with OK: type, standard, size, seat, fastener type, fit, tolerance, termination and drill point, kept per units. Editing a hole still opens on that hole. Today a new hole starts on the workbook's defaults, so Aidan picks the same options again for every hole.

## Scope

- `src/InventorAddin.Core/Settings/AddinSettings.cs`: a remembered last hole per units (metric and imperial), kept across restarts in `settings.json` as the other last values are (`LastDowelSeatsMm` and friends). Store only the choices listed above: type (kind), standard, size, seat, fastener type, fit, tolerance (the Toleranced tick, the Precision Tol row's name, or Custom with its code or its Upper and Lower), termination and drill point. Depths, seat sizes and a Simple hole's diameter already have their own last values; leave them as they are.
- A Core helper beside `HoleLastDepths` / `HoleLastDowelSeats` (for example `HoleLastHole.cs`) with `From(settings)` and `Remember(settings, spec)`.
- `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs`: a new hole (not edit mode) starts on the last hole for the starting units (Settings "Default units", as now). Each remembered value is applied only if the workbook still offers it for that units and type; a value it no longer offers falls back to the workbook default for that box, and the rest still apply. The values it sets count as starting values, not typed ones (task 124's typed-value rule is unchanged).
- `src/InventorAddin/Holes/HoleSession.cs` (or wherever OK already remembers the dowel seats and depths, about line 252): remember the last hole on OK, in the same settings save. And `src/InventorAddin/Commands/AwbHoleCommand.cs` (about line 228): pass the remembered last hole into the view-model as the other last values are passed.
- `docs/features/09-awb-hole.md`: the decision in the Units/Type rows or a short "A new hole starts on the last hole" paragraph, and the open question below.
- Tests: a new hole with no last hole (workbook defaults, as now); with a metric last hole; imperial units start with the imperial one; a size no longer in the workbook falls back; Custom tolerance restored; edit mode ignores the last hole; Remember writes only on OK (the view-model does not write on its own).

Out of scope: changing which units a new hole starts in.

## Acceptance criteria

- [ ] Every item in Aidan's list is remembered and restored per units.
- [ ] Edit mode is unchanged.
- [ ] Cancel does not change the remembered hole.
- [ ] Old `settings.json` files without the new field load as before.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes and `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

- Defaults to log in this file (the parent copies them to `DECISIONS.md` and the spec's open questions):
  - The remembered hole is applied when the window opens. Switching units in the window afterwards behaves as it does now; it does not load the other units' last hole. (Open question for Aidan: should switching units load that units' last hole?)
  - With several holes made in one OK (several sketches or points), one hole is remembered: the spec the window had at OK.
- Follow the existing last-values pattern (`HoleLastDowelSeats.Remember`, `RememberKept` in `HoleSession`) and settings equality (`AddinSettings.Equals`).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. With no last hole (fresh settings, or before the first OK): a new AWB Hole opens on the workbook defaults, as now.
  2. Make a counterbored M6 tapped hole, blind, 90 deg drill point, OK. New AWB Hole: it opens on Tapped, AWB Tapped, M6, Counterbore, the same fastener type, Distance, 90 deg.
  3. Make a dowel with a Press fit and a chosen tolerance row, OK. New AWB Hole: Dowel, the same size, Press, the same tolerance.
  4. A toleranced clearance hole with a Custom code (for example H8): the next new hole opens with Toleranced ticked, Custom, H8.
  5. Cancel a changed hole: the next new hole still opens on the last OK'd one.
  6. Restart Inventor: the next new hole still opens on the last OK'd one.
  7. Imperial: make an imperial hole with OK, set Default units to Imperial: a new hole opens on it; set back to Metric: it opens on the last metric hole.
  8. Edit an existing hole: it opens on that hole, not the last one.

## Follow-ups

Things noticed but not done.
