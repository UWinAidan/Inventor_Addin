# 057: Create Print saves in a Prints subfolder

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11; "Commands: Create Print")
- **Status:** todo
- **Depends on:** 053
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Aidan's item 9 (2026-10-11): Create Print saves a model's drawing in a `Prints` subfolder of the model's folder, creating it when missing, and looks there for an existing print. Prints made before this change sit beside the model, so they are still found (Default). Task 059 makes Rename look in the same places, through the same Core rule.

## Scope

- `src/InventorAddin.Core/Drawing/PrintPlan.cs`
- `src/InventorAddin/Commands/CreatePrintCommand.cs`
- `tests/InventorAddin.Core.Tests/Drawing/` (the print plan tests)

Out of scope:

- Rename (task 059).
- The other Create Print follow-ups in `TODO.md` (the active document while editing in place, "part" wording for assemblies).

## Acceptance criteria

- [ ] A constant for the folder name, `PrintPlan.PrintsFolderName = "Prints"`
- [ ] `PrintPlan.DrawingFileName(model, ext)` returns `{model folder}\Prints\{model file name without extension}{ext}`, for example `C:\CAD\Prints\BRACKET,AWB-10005.dwg`. Keeps working on any platform (no `System.IO.Path`), with `\` or `/` in the input. Tested
- [ ] `PrintPlan.PrintsFolder(model)` returns `{model folder}\Prints`. Tested
- [ ] `ExistingPrintCandidates` lists, in order: `Prints` with the preferred extension, `Prints` with the other extension, then beside the model with the preferred and then the other extension. `FindExistingPrint` returns the first that exists. Tested
- [ ] Create Print creates the `Prints` folder when missing (`Directory.CreateDirectory`, aliased as the add-in aliases System.IO types) before saving, and a failure to create it shows the same kind of message as a failed save, naming the folder
- [ ] The button description (`CreatePrintCommand.Description`) no longer says "saved beside it"
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Keep the folder rule in `PrintPlan`, so task 059 can ask it which drawings belong to a model.
- The template extension rule is unchanged (spike 032: save with the template's extension).

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
