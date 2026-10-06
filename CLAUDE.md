# Inventor Workflow Tools: agent guide

An Autodesk Inventor add-in (C#, .NET 8, built against Inventor 2026) that automates part creation and numbering, iLogic injection, a material and finish library, and drawing tools such as hole tables.

- Roadmap: `docs/PLAN.md`
- Feature specs (the source of truth for behaviour): `docs/features/`
- Units of work: `docs/tasks/NNN-slug.md`
- How planning and delegation run: `docs/WORKFLOW.md`

If you are the main session and were asked to plan or run a milestone, follow `docs/WORKFLOW.md`. If you are a subagent, follow your own instructions and the rules below.

## Know which environment you are in

| | Cloud session (Linux) | Aidan's PC (Windows, Inventor 2026) |
|---|---|---|
| `src/InventorAddin.Core` and `tests/` | builds and runs tests | builds and runs tests |
| `src/InventorAddin` (the add-in) | **cannot build**: needs `Autodesk.Inventor.Interop.dll` from an Inventor install | builds and deploys to Inventor |
| Running inside Inventor | impossible | manual, by Aidan |

`CLAUDE_CODE_REMOTE=true` means you are in the cloud.

Commands:

- Cloud: `dotnet build src/InventorAddin.Core` and `dotnet test tests/InventorAddin.Core.Tests`. Do not run `dotnet build InventorAddin.slnx` in the cloud. It fails on the interop reference and the failure tells you nothing.
- Windows: `dotnet build InventorAddin.slnx`. Close Inventor first, or pass `-p:DeployToInventor=false` to skip the deploy step.

Never say add-in code compiles or works unless it was built on Windows. In the task's verification section and in the PR, write "not compiled" for anything under `src/InventorAddin` that was changed in the cloud.

## Architecture

The split exists so that most of the work can be built and tested without Inventor.

- **`src/InventorAddin.Core`** has no Inventor reference and no WPF or WinForms. It holds the data models, every decision the add-in makes (numbering, hole classification and tagging, table layout, library filtering, validation), settings, and view-models written as plain `INotifyPropertyChanged` classes.
- **`src/InventorAddin`** is a thin adapter. A command reads Inventor into Core models (`Extraction/`), calls Core logic, then applies the result back to the document. WPF windows live in `UI/` and bind to Core view-models.
- If you are writing an `if` about an engineering or company rule inside `src/InventorAddin`, it belongs in Core.
- Every Core behaviour gets xUnit tests in `tests/InventorAddin.Core.Tests`.

## Inventor API rules

- **Do not guess API member names.** Nothing under `src/InventorAddin` compiles in the cloud, so a wrong name survives until Aidan builds. Check each member against existing usage in `Extraction/` or the Inventor API reference. List any member you could not confirm in the task's verification notes.
- **Units.** Inventor's internal units are centimetres, radians and kilograms. Convert at the edge. Core fields carry the unit in the name (`DiameterCm`).
- **COM getters throw** when a value does not apply. Use `ComSafe.Get` and `ComSafe.Run`, and pass the warnings list when the caller has one.
- **Keep `ButtonDefinition` objects referenced** (`RibbonSetup._commands`), or their events stop firing.
- **Wrap document edits in one transaction** so a single undo reverts the command, and abort it on failure.
- **Pick the command type that matches what the command changes.** The developer export buttons are query-only; commands that edit documents are not.
- **Namespace clashes.** The `Inventor` namespace defines `File`, `Path`, `Environment` and `Application`. The add-in project has implicit usings off for that reason. Alias the System types (`using IOPath = System.IO.Path;`).

## What must stay out of the repo

This is a personal, public repository.

- `ADDIN_PICS/` holds reference screenshots of another company's add-in. It is gitignored and is not available in cloud sessions. The specs in `docs/features/` describe the behaviour we want in our own words.
- Do not put company names, real part numbers, title blocks, customer names or any employer data in code, docs, tests or fixtures.
- Test fixtures exported with *Export Model Data* must come from Aidan's personal models only.
- `docs/ui-mockups/` is for our own mockups, not for screenshots of other software.
- Our names: ribbon tab "Workflow Tools", command prefix `WorkflowTools_`.

## Working rules

- One task, one commit. Do the task you were given and nothing else. Put anything else you notice under "Follow-ups" in the task file.
- Questions only Aidan can answer go in the feature spec's "Open questions" section. Do not invent the answer. Build what is decided and stop at the boundary.
- Do not commit `bin/` or `obj/`.
- Core uses file-scoped namespaces and implicit usings. The add-in uses block namespaces and explicit usings. Nullable is on everywhere. Match the file you are in.
