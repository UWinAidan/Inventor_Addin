# 210: Part Properties: Status dropdown and Revise

- **Milestone:** M5
- **Feature spec:** `docs/features/07-part-properties.md` (question 7); `docs/features/05-drawing-tools.md` (title block Status)
- **Status:** done
- **Depends on:** 219
- **Needs:** windows build
- **Parallel-safe with:** any Core-only task

## Goal

Part Properties shows the part's status as a dropdown (WORK IN PROGRESS, PENDING, RELEASED) and has a **Revise** button that steps the revision (A to B to C ...) (Aidan, 2026-11-22). Setting the status writes Design Status and Inventor's own Status field (User Status, task 219) together.

## Scope

- Core:
  - the snapshot and write plan carry Design Status and User Status through task 219's `PartStatus`;
  - a `RevisionStep.Next("A")` rule: A to B ... Z to AA. Skip I and O (Default; say so). A blank or unreadable revision becomes "A";
  - the view-model gains a Status dropdown and a Revise command (a pending change until Apply);
  - tests.
- Add-in: read Design Status and User Status in `Extraction/`; add the dropdown and the Revise button to `PartPropertiesWindow.xaml`; Apply writes everything in one transaction.
- **Backfill rule change (from task 219's review):** in `PrintPropertyBackfill`, Design Status 0 with User Status already set to one of the three words sets Design Status to match that word, not Work In Progress. Any other text still gives Work In Progress. Update the tests.
- On open, a User Status that does not match Design Status is a pre-filled pending change (the earlier Default, spec 07 question 7).

## Acceptance criteria

- [x] Choosing RELEASED and Apply sets Design Status Released and Status "RELEASED"; one Undo reverts both.
- [x] Revise on Rev "A" shows "B" as a pending change; Apply writes it.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5244 passed, 0 failed (new: RevisionStepTests, PartPropertiesStatusTests, changed backfill rule tests). `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. After review: Status combo no longer uses the 68 px CodeComboBox style (left-aligned, sizes to its content; a fixed MinWidth fails the XAML style test); Revise is disabled (`CanRevise`) when the Revision Number could not be read (null, as against blank), and Apply then leaves Revision alone, so a real revision is never overwritten with "A"; the `ChangeMaterialFinish` doc comment now says its backfill can write Revision and Status.
- **Not compiled (changed under `src/InventorAddin`):** none. Built on Windows, but not run in Inventor: `Extraction/PartPropertiesReader.cs`, `Extraction/FinishSpecBackfiller.cs` (`ReadDesignStatus` made internal), `UI/PartPropertiesWindow.xaml`.
- **Inventor API members not confirmed:** none new. Reads reuse `PropertyReader`, and writes reuse `PropertyWriter` (Integer support from task 219). Not yet seen working in Inventor: the Status combo bound to a nullable enum (`SelectedValuePath="Status"`).
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a part with Rev "A", Design Status Work In Progress and Status "WORK IN PROGRESS". Click Part Properties. Status shows WORK IN PROGRESS, Revision shows A, Apply is greyed out.
  2. Choose RELEASED, click Apply. Open iProperties: Project tab Design Status is Released; Status tab Status is "RELEASED". Press Ctrl+Z once: both go back.
  3. Click Revise: Revision shows B and Apply turns on; nothing is written yet. Click Cancel and check iProperties still shows A. Open again, Revise, Apply: Revision Number is B. Revise from H gives J.
  4. On a part whose Status tab is blank but Design Status is Released: on opening, the status shows RELEASED and the pre-fill notice shows; Apply writes "RELEASED".
  5. On a part with Design Status "not set" and Status "PENDING": on opening the dropdown shows PENDING; Apply sets Design Status to Pending and leaves the text. Create Print or Mass Update on such a part gives the same.
  6. Open a read-only file: Status and Revise are disabled.
  7. Check the Status dropdown shows "WORK IN PROGRESS" in full, not cut off.

## Follow-ups

Things noticed but not done.

- Material and Finish (Change button) can backfill Revision and Status while Part Properties is open; the window does not re-read them afterwards (the Apply then rewrites equal values, harmless). Add Revision, Design Status and User Status to `PartMaterialFinish` if that matters.
- Revision is changed only through Revise; typing a revision is not offered (spec 07 says Revise only).
