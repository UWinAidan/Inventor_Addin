# 245: Assemblies: the dash is on the print, not in the model

- **Milestone:** M5 (Aidan, 2026-11-26)
- **Feature spec:** `docs/features/05-drawing-tools.md` (question 35)
- **Status:** done (built on Windows; not run in Inventor)
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

- **Ran:**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (13 existing warnings).
  - `dotnet test tests/InventorAddin.Core.Tests`: 5564 passed, 0 failed (new `AssemblyDashFieldsTests`, 13 tests; `PrintPropertyBackfillTests` assembly tests rewritten: Rev and Status only, no Material, Material Size or Finish Spec write).
  - COM probes (PowerShell late-bound, copies in `C:\AwbSpike\245\`: a new part `p.ipt`, assembly `a.iam` holding it, a copy of the AWB template as `tpl.idw`, and the print `d.idw`; Inventor started with New-Object and Quit in each script; scripts `p2` to `p4` in the job tmp folder):
    - The assembly's Material, Material Size and Finish Spec are blank, and the print shows blank for all three.
    - The MATERIAL tag `<Property Document='model' PropertySet='Design Tracking Properties' Property='Material' FormatID='{32853F0F-...}' PropertyID='20'>` was rewritten inside `TitleBlockDefinition.Edit` / `ExitEdit(true)` to `<Property Document='drawing' PropertySet='User Defined Properties' Property='Material' FormatID='{D5CDD505-...}'>` with a drawing custom property `Material` = "—": the field printed "—" (`GetResultText`). Inventor stores it back with `PropertyID='3'` added, so the way back finds the tag by FormatID and name, not by exact text.
    - Rewriting that custom tag (matched by regex) back to the Design Tracking tag worked: the field read the model again (blank). Both directions work; Inventor did not refuse. The drawing is dirty after the edit, as in 244, so the add-in edits only when a tag has to change.
  - Core's regex swap was not run against Inventor; the probe used the same patterns and the Core tests use the real tag text.
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows (not run in Inventor): `Extraction/PrintUnitRefresher.cs`, `Drawing/TitleBlockPropertySource.cs` (`Repoint` takes `bool? materialOnDrawing`), `Extraction/FinishSpecBackfiller.cs`.
- **Inventor API members not confirmed:** none new; every member was already used by 244 or confirmed by the probes above (`TitleBlockDefinition.Edit`/`ExitEdit`, `TextBox.FormattedText`, `TitleBlock.GetResultText`, `PropertySets` and `Item`/`Add` on the drawing's `Inventor User Defined Properties`).
- **Manual checklist for Inventor:**
  1. Use an assembly of your own with blank Material, Material Size and Finish Spec. If an earlier build (task 231) wrote dashes into them, clear those three iProperties by hand and save. Close Inventor, `dotnet build InventorAddin.slnx`, start Inventor.
  2. Open the assembly, click Create Print. Expect "—" in MATERIAL, MATERIAL SIZE and FINISH on the print. In the assembly's iProperties, Material, Material Size and Finish Spec are still blank (Project tab Material and the Physical tab show no "—"); only Revision Number and Status are filled in.
  3. Run Mass Update on the assembly: the three stay blank.
  4. Close the print. In the assembly set Material Size (a custom iProperty, for example `1 x 2 x 3 mm`), save, open the print: MATERIAL SIZE shows the value, MATERIAL and FINISH still show "—". Clear it, save, open the print: "—" is back.
  5. Open a print of a part: nothing changes; MATERIAL still reads the part's material.
  6. Open the print again with nothing changed: no `*` on the tab.

## Defaults

- Parts leave MATERIAL's tag alone (the swap is only for an assembly). A print whose model is a part is never swapped back, even if it was an assembly print earlier.

## Follow-ups

- Setting an assembly's Material from code fails (`E_FAIL` on `PropertySets("Design Tracking Properties").Item("Material").Value`, probe p3/p4), so in the probe the "model value set" case for MATERIAL could only be argued from the tag, not run. Check by hand that filling Material in an assembly's iProperties shows on the print (checklist step 4 uses Material Size).
- `PropertyWriter.Apply` still has a best-effort branch for `PartPropertyStorage.Material` (task 231); nothing writes it now. Remove it, and `PartPropertyStorage.Material`'s "written only for an assembly" note, when convenient.
- A refresh on open or save that changes a dash to a value (or back) does not re-run the title block fit (task 238), as noted in 244.
- Dashes already written by 231 into test assemblies stay in the model; clear them by hand (checklist step 1).
