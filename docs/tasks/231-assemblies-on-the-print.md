# 231: Assemblies: Rev, Status, and a dash for blank part fields

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (tasks 217, 219)
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 192
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

On an assembly print REV and STATUS are blank, and MATERIAL, MATERIAL SIZE and FINISH are blank. Assemblies get the same automatic Rev A and Status as parts, and those three fields print "—".

## Scope

- `PrintPropertyBackfill` / `FinishSpecBackfiller`: assemblies get Rev and Status backfill in Create Print and Mass Update (task 219 Default said older assemblies are not backfilled; Aidan overturned it).
- For the three fields, choose the simplest way that prints "—" on assemblies only (for example the add-in writes "—" into blank Material Size, Finish Spec and Material on an assembly in Create Print) and log it as a Default.

## Acceptance criteria

- [x] An assembly print shows REV "A", STATUS "WORK IN PROGRESS" and "—" in MATERIAL, MATERIAL SIZE and FINISH.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 13 warnings (none new). `dotnet test tests/InventorAddin.Core.Tests`: 5395 passed, 0 failed. New tests in `PrintPropertyBackfillTests` cover `ForAssembly` (blank gives Rev A, Status and three dashes; all set gives nothing; whitespace counts as blank; a set Material Size is kept).
- **Not compiled (changed under `src/InventorAddin`):** none; `FinishSpecBackfiller.cs` built on Windows. Nothing run in Inventor yet.
- **Inventor API members not confirmed:** writing the standard Design Tracking "Material" property (set "Design Tracking Properties", name "Material") with text on an assembly. The add-in has only read it so far. Material Size and Finish Spec are custom properties written the same way as on parts.
- **Default taken:** the add-in writes "—" (em dash U+2014) into any blank Material, Material Size and Finish Spec on an assembly wherever the print properties are backfilled (Create Print, Mass Update's file scan); values already set are left alone; no finish list lookup or sheet metal size read for assemblies. Weldments count as assemblies (as in Mass). Material and Finish prop (parts only) is unchanged. Logged for the parent to add to DECISIONS.md. The Material dash is best effort: `PropertyWriter.Apply` writes it last in its own try/catch, logs a refusal and does not rethrow (unconfirmed API member).
- **Manual checklist for Inventor:**
  1. Open an assembly (.iam) with no Rev, Status, Material, Material Size or Finish Spec set (an older one is ideal). Check iProperties first: those are blank.
  2. Click Create Print. The print opens.
  3. Check the title block: REV "A", STATUS "WORK IN PROGRESS", MATERIAL, MATERIAL SIZE and FINISH each "—".
  4. Check the assembly's iProperties: Revision Number A, Status Work In Progress, Material "—" and custom Material Size and Finish Spec "—". Ctrl+Z once on the assembly should revert those writes together.
  5. Run Create Print on a part: unchanged (its own material, size and finish; no dashes). Run Mass Update on a folder with an assembly and confirm the same writes appear in its log.
  6. If Inventor refuses the Material write, the log shows "Could not set the property 'Material'. It was left unset." and everything else (Rev, Status, Material Size, Finish Spec, Mass) is still written and the print is still made; report it.
  7. Run Mass Update on one assembly: its renames and part numbers still apply, with the same one-Undo behaviour.

## Follow-ups

- If Inventor refuses a text write to an assembly's standard Material property, switch the MATERIAL field to read a custom property for assemblies (template change).
- When task 232's drawing-property mechanism lands, the assembly dashes could be written into the drawing instead of the model, leaving the assembly's own properties untouched.
