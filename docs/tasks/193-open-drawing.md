# 193: Open Drawing

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5: Open Drawing")
- **Status:** done (not run in Inventor)
- **Depends on:** 188, 191
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Open Drawing opens the active model's print from `Prints\`; when there is none it offers Create Print.

## Scope

- `src/InventorAddin.Core/Drawing/PrintPlan.cs`: the question text when no print exists (reuse `FindExistingPrint`).
- `src/InventorAddin/Commands/OpenDrawingCommand.cs` + ribbon button next to Create Print (model environments).
- Tests for the message.

## Acceptance criteria

- [x] A model with a print: the print opens (or is activated when already open).
- [x] A model with none: "This part has no print. Create one now?"; Yes runs Create Print, No does nothing.
- [x] An unsaved model says so, as Create Print does.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes
- [x] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Share Create Print's checks (wrong document, unsaved) instead of copying them.
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors (11 warnings, existing nullable ones). `dotnet test tests/InventorAddin.Core.Tests`: 5308 passed, 0 failed.
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor: `Commands/OpenDrawingCommand.cs` (new), `Commands/CreatePrintCommand.cs` (shared `ModelProblem`, `OpenPrint`, `RunFromOpenDrawing`), `UI/RibbonSetup.cs`.
- **Inventor API members not confirmed:** none new. Used `Documents.Open(path, true)` and `Document.Activate()` exactly as Create Print's existing "open it" path already does.
- **Defaults chosen:** Open Drawing's unsaved text is "Save the part before opening its print." and wrong-document text "Open Drawing works on parts and assemblies."; large button after Create Print on the model ribbons; command type `kFileOperationsCmdType` (opens a file, edits no document); targets the document being edited (`CommandDocument.Edited`), like Create Print.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a saved part that has a print in its `Prints\` folder. On the AWB Addin tab, Drawing panel, click Open Drawing. The drawing opens and is the active window.
  2. With that drawing still open, go back to the part and click Open Drawing again. The already-open drawing becomes active; no second copy opens.
  3. Open a saved part with no print. Click Open Drawing. A question reads "This part has no print. Create one now?". Click No: nothing happens.
  4. Click Open Drawing again and Yes: the Initial Views window appears (Create Print). Confirm; the print is created in `Prints\` and opens.
  5. Click Create Print on a part with a print: it still offers "A print for this part already exists. Open it?" (unchanged).
  6. A new, unsaved part: click Open Drawing. A message says to save the part first.
  7. Open Drawing is shown on part and assembly ribbons, not on the drawing ribbon. Try it on an assembly too.

## Follow-ups

- Open Drawing and Create Print's "already exists" prompt overlap; if Aidan wants the prompt dropped (Create Print could just open), say so.
