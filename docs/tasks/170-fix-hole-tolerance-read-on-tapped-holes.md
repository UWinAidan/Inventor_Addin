# 170: Fix: "Hole tolerance: Object reference not set" for two holes on every analysis

- **Milestone:** M4 part 2 (fixes from Aidan's check, 2026-11-18)
- **Feature spec:** `docs/features/09-awb-hole.md`, `docs/features/04-hole-table-wizard.md`
- **Status:** todo
- **Depends on:** none
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 168, 169

## Goal

Every Hole Table analysis of Aidan's test drawing logs `WARN Hole table: Hole tolerance: Object reference not set to an instance of an object.` twice. It comes from `HoleFeatureReader.ReadTolerance` (`src/InventorAddin/Holes/HoleFeatureReader.cs`, about line 176): `hole.HoleDiameter.Tolerance` throws a `NullReferenceException`, so `hole.HoleDiameter` is null for those holes. The test part has one tapped and one taper tapped hole (spike 160), and Inventor's `HoleDiameter` is not set on a tapped hole: the thread gives the size. After this task the tolerance is read only where it applies, and the log names a hole when a read does fail.

## Scope

- `src/InventorAddin/Holes/HoleFeatureReader.cs`: confirm which holes give the warning (log the hole name with it, at least while checking), then skip the tolerance read on a hole whose diameter parameter is null (tapped and taper tapped holes), with no warning. A tapped hole has no tolerance to read (spec 09: the thread class is separate). Keep the warning, with the hole's name in its context, for a read that fails on a hole that should have one.
- Check the other reads of `hole.HoleDiameter` in the same file (`Length(() => hole.HoleDiameter)`, `Expression(...)`) and the AWB Hole edit path (`ReadForEdit`, `ReadAppliedState`) for the same null; they go through `ComSafe` silently today, so only change them if they give a wrong value.

Out of scope: anything else in the hole table.

## Acceptance criteria

- [ ] The two holes are named in Verification (from code reasoning and, if Inventor is open, from the log).
- [ ] An analysis of a view with tapped and taper tapped holes logs no "Hole tolerance" warning.
- [ ] A dowel or toleranced clearance hole still reads its tolerance (code unchanged on that path).
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

- Confirm against the Inventor API reference (or existing code) that `HoleFeature.HoleDiameter` is null on a tapped hole before relying on it; if you cannot, guard on null anyway and say so.
- Pass the hole's name into `ReadTolerance` for the warning context (for example `"Hole7 tolerance"`).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Hole Table, Analyze VIEW1 (or whichever view shows the holes end on): the log has no "Hole tolerance" warning.
  2. The precision table still shows the dowel and toleranced holes' tolerances; a tapped hole still shows its thread and class.
  3. AWB Hole, edit a dowel hole: it opens on its tolerance as before.

## Follow-ups

Things noticed but not done.
