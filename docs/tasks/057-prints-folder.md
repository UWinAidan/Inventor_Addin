# 057: Create Print saves in a Prints subfolder

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11; "Commands: Create Print")
- **Status:** done (Core tested; add-in compiled locally, not tested in Inventor)
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

- [x] A constant for the folder name, `PrintPlan.PrintsFolderName = "Prints"`
- [x] `PrintPlan.DrawingFileName(model, ext)` returns `{model folder}\Prints\{model file name without extension}{ext}`, for example `C:\CAD\Prints\BRACKET,AWB-10005.dwg`. Keeps working on any platform (no `System.IO.Path`), with `\` or `/` in the input. Tested
- [x] `PrintPlan.PrintsFolder(model)` returns `{model folder}\Prints`. Tested
- [x] `ExistingPrintCandidates` lists, in order: `Prints` with the preferred extension, `Prints` with the other extension, then beside the model with the preferred and then the other extension. `FindExistingPrint` returns the first that exists. Tested
- [x] Create Print creates the `Prints` folder when missing (`Directory.CreateDirectory`, aliased as the add-in aliases System.IO types) before saving, and a failure to create it shows the same kind of message as a failed save, naming the folder
- [x] The button description (`CreatePrintCommand.Description`) no longer says "saved beside it"
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Keep the folder rule in `PrintPlan`, so task 059 can ask it which drawings belong to a model.
- The template extension rule is unchanged (spike 032: save with the template's extension).

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, only the 4 existing CS8600 warnings (CreatePrintCommand, NewPartCommand, SaveAsCopyCommand, SaveAsReplaceCommand). `dotnet test tests/InventorAddin.Core.Tests`: 2015 passed, 0 failed (baseline 1994, plus 21 net new test cases in `PrintPlanTests`).
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** `Commands/CreatePrintCommand.cs`. It works out `PrintPlan.PrintsFolder(modelPath)`, and in `Create` runs `IODirectory.CreateDirectory(printsFolder)` (alias `IODirectory = System.IO.Directory`) as its own step just before `SaveAs`. A failure there goes through the existing catch: the half-made drawing is closed without saving and the message reads "Creating the print failed while creating the folder '...\Prints': {reason} The drawing was closed without saving." The folder is made only once the drawing is built, so a failure in an earlier step leaves no empty `Prints` folder. The button description now reads "Create a drawing of this part or assembly, with a front view, saved in its folder's Prints folder."
- **Inventor API members not confirmed:** none. No new Inventor API members are used; `Directory.CreateDirectory` is .NET.
- **Core:** `PrintPlan.PrintsFolderName`, `PrintPlan.PrintsFolder(model)`, and `DrawingFileName` now returns `{folder}\Prints\{name}{ext}`. Paths are joined with the last separator the model's path uses (`\` or `/`), and a bare file name gives `Prints\{name}{ext}`. `ExistingPrintCandidates` returns Prints with the preferred extension, then Prints with the other, then beside the model with the preferred and then the other. Its first entry is always the file Create Print saves.
- **Manual checklist for Inventor:**
  1. Close Inventor, build and deploy (`dotnet build InventorAddin.slnx`), start Inventor.
  2. Open a saved, numbered part in a folder with no `Prints` subfolder and no drawing beside it. Click Create Print (Drawing Tools). A drawing opens with a front view. In Explorer, `Prints` now exists in the part's folder and holds `{part file name}.dwg` (or `.idw`, per the template). Check that the drawing's Part Number matches the part.
  3. Close the drawing, go back to the part and click Create Print again. It asks "A print for this part already exists. Open it?". Yes opens the drawing from `Prints`.
  4. Open a second part whose folder already has a `Prints` folder (from step 2). Create Print saves into the existing folder without complaint.
  5. Old print: take a part that has a drawing with the same name beside it (made before this change) and no drawing in `Prints`. Create Print offers to open the old drawing beside the part and does not make a new one.
  6. Hover over Create Print: the tooltip no longer says "saved beside it".
  7. Optional failure check: in a test folder, create a plain file named `Prints` (no extension) beside a saved part, then click Create Print. A message says the print failed while creating the folder, naming it, and that the drawing was closed without saving. No drawing stays open. Delete the file afterwards.

## Follow-ups

Things noticed but not done.

- An old print found beside the model is opened where it is, not moved into `Prints`. Moving old prints, if wanted, would be a separate command or a Rename step; that is Aidan's call.
- The "already exists" question does not say where the print was found (in `Prints` or beside the model). Naming the path could help once both places are in use.
