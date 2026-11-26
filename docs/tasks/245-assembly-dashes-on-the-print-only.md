# 245: Assemblies: the dash is on the print, not in the model

- **Milestone:** M5 (Aidan, 2026-11-26)
- **Feature spec:** `docs/features/05-drawing-tools.md` (question 35)
- **Status:** todo
- **Depends on:** 231, 244
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Task 231 writes "—" into an assembly's blank Material, Material Size and Finish Spec so the print shows a dash. Aidan overturned that (question 35, 2026-11-26): do not write "—" into the assembly. The print shows "—" when the field is blank, and the model stays untouched.

## Scope

- **Stop the model writes:** Create Print and Mass Update no longer write "—" into an assembly's Material, Material Size or Finish Spec (`PrintPropertyBackfill.ForAssembly` and its callers). Rev A and Status are still written as before (the same rule as parts). Dashes already written by 231 into Aidan's test assemblies are left alone; the checklist tells him to clear them by hand first.
- **Dash on the print:** use 244's drawing-side mechanism. On an assembly print, each of MATERIAL, MATERIAL SIZE and FINISH whose model value is blank reads a drawing property holding "—"; when the model value is set, the field reads the model again. Refreshed by 244's refresher (Create Print, open, before save), so filling a field in the assembly later shows on the next open or save of the print.
- MATERIAL is a Design Tracking field (`FormatID='{32853F0F-3444-11D1-9E93-0060B03C1CA6}' PropertyID='20'`), not a custom one. Default: on the drawing it points at a custom property (for example `Material` in User Defined Properties, tag `PropertySet='User Defined Properties' Property='Material' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}'`, the form the template uses for Material Size) and is restored to the Design Tracking tag when it reads the model again. Prove the swap both ways with a COM check before building it. If Inventor refuses, report it and use the least surprising alternative, listed under Verification.
- Parts are unchanged.

## Acceptance criteria

- [ ] Create Print and Mass Update on an assembly with blank Material, Material Size and Finish Spec leave those iProperties blank; the print shows "—" in all three.
- [ ] Filling in one of them in the assembly, then opening or saving the print, shows the value instead of the dash.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Notes for the implementer

- Read task 244's brief and code first; extend its refresher rather than adding a second one.
- COM checks on copies in `C:\AwbSpike\245\`, as described in task 244's notes. Use a new invented assembly made in that folder, or copy an assembly together with its parts so the references resolve. Never open or save files in `C:\CAD_TEST`.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
