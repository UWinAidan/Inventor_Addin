# 219: Revision and Status filled in automatically

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/07-part-properties.md`; `docs/features/02-part-creation-and-numbering.md`; `docs/features/05-drawing-tools.md`
- **Status:** done
- **Depends on:** 217
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Revision and Status fill themselves in, so Aidan never types them per drawing (Aidan, 2026-11-22). Status is Inventor's own Status field (Design Tracking "User Status", id 17), written in words that match Design Status (Design State) and kept in step with it.

## Scope

- **Core.** A `PartStatus` type (Work In Progress = Design Status 1, Pending = 2, Released = 3, per the Inventor API reference) with its User Status text: "WORK IN PROGRESS", "PENDING", "RELEASED". Then write plans:
  - New part / copy: Revision Number "A", Design Status 1, User Status "WORK IN PROGRESS".
  - Backfill for older parts:
    - a missing Revision Number becomes "A";
    - a missing User Status becomes the words for the current Design Status;
    - a Design Status of 0 (not set) becomes Work In Progress for both.
    - Extend task 217's backfill so one call returns every missing print property.
- **Add-in:**
  - New Part and Save As Copy write the new-part plan.
  - Create Print and Mass Update run the backfill. In Create Print, Finish Spec, Revision and Status are filled in one transaction.
- **Replace the custom Status property.** It came from Aidan's earlier decision, now replaced, so nothing writes it any more. Leave existing custom Status properties alone; say so under Verification.
- Tests for every plan.

## Acceptance criteria

- [x] A new part and a Save As Copy have Rev "A", Design Status Work In Progress and Status "WORK IN PROGRESS".
- [x] Create Print and Mass Update fill a missing Revision or Status and leave set ones alone.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

Read DECISIONS.md rows dated 2026-11-22 (Status and Revision). Task 210 (Part Properties dropdown and Revise) builds on this.

## Verification

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5207 passed, 0 failed. `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors (add-in compiled on Windows; not run in Inventor).
- **Not compiled (changed under `src/InventorAddin`):** none; all compiled in the Windows build. Nothing has been run in Inventor.
- **Inventor API members not confirmed:** the Design Tracking property named "Design Status" (id 40) and writing a .NET `int` to it; the name is from the API reference and spike 183's property id, never written by the add-in before. "User Status" (id 17) and "Revision Number" are written by name the same way as other standard properties; also unconfirmed by a write. Read of Design Status is via `PropertyReader.Get` then `Convert.ToInt32`.
- **Existing custom "Status" properties:** left alone. Nothing writes the custom Status property any more (it was never written by the add-in before this task).
- **Design:** `PrintPropertyBackfill` (Core) returns Finish Spec, Revision Number, Design Status and User Status writes in one list; `FinishSpecBackfiller.WritesFor` feeds it from Material and Finish, Create Print and Mass Update. `CreationProperties.Build` adds Rev "A", Design Status 1 and "WORK IN PROGRESS" for New Part and Save As Copy (also for a new assembly, since Build does not know the kind). New `PropertyValueKind.Integer` / `PropertyWrite.SetInteger` carry Design Status.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Close nothing; make a New Part (AWB Addin tab). Open iProperties: Summary tab Revision Number is "A"; Status tab Status is "WORK IN PROGRESS" and the Design Status (Project tab Design State) reads Work In Progress. One Undo of the creation is not needed; just check them.
  2. Save As Copy of a part: the copy shows Rev "A" and WORK IN PROGRESS, even when the source was Released.
  3. Take an older part with Revision Number blank and no Status. Open it, Create Print. In the print's title block REV shows "A" and STATUS "WORK IN PROGRESS"; the model's iProperties now hold them (and Finish Spec if it had a Finish). One Undo on the model reverts all of it.
  4. Repeat with a part that has Rev "C", Design Status Released and Status "RELEASED": Create Print changes none of them.
  5. Open Material and Finish on an older part missing these: they are filled in on opening; the log (`%APPDATA%\AwbAddin\awbaddin.log`) says "Filled in ..." with property names only.
  6. Mass Update on a folder with an older part, applying any change: the part gets Rev, Design Status and Status as well. A part set to Design Status Pending with blank Status gets "PENDING".

## Follow-ups

- Mass Update only backfills files it opens to change (as task 217); a part Mass Update does not otherwise touch is not filled in.
- Assemblies are skipped by the backfill (as task 217) but get Rev A and Work In Progress when created; whether older assemblies should be backfilled is for Aidan.
- If a write to Design Status fails in Inventor (name or type), `PartPropertyStorage.DesignStatus` is the one place to fix; task 210 builds on the same locations.
