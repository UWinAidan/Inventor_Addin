# 231: Assemblies: Rev, Status, and a dash for blank part fields

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (tasks 217, 219)
- **Status:** todo
- **Depends on:** 192
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

On an assembly print REV and STATUS are blank, and MATERIAL, MATERIAL SIZE and FINISH are blank. Assemblies get the same automatic Rev A and Status as parts, and those three fields print "—".

## Scope

- `PrintPropertyBackfill` / `FinishSpecBackfiller`: assemblies get Rev and Status backfill in Create Print and Mass Update (task 219 Default said older assemblies are not backfilled; Aidan overturned it).
- For the three fields, choose the simplest way that prints "—" on assemblies only (for example the add-in writes "—" into blank Material Size, Finish Spec and Material on an assembly in Create Print) and log it as a Default.

## Acceptance criteria

- [ ] An assembly print shows REV "A", STATUS "WORK IN PROGRESS" and "—" in MATERIAL, MATERIAL SIZE and FINISH.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
