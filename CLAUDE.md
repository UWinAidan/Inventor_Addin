# AWB Addin: agent guide

An Autodesk Inventor add-in (C#, .NET 8, built against Inventor 2026) that automates part creation and numbering, iLogic injection, a material and finish library, and drawing tools such as hole tables.

- Where each big item stands: `STATUS.md`
- What needs doing next, and what is waiting on Aidan: `TODO.md`
- Decisions made, and who made them: `DECISIONS.md`
- Roadmap, milestones and task lists: `docs/PLAN.md`
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
- **Confirmed in Inventor 2026.** These were tested by Aidan and need no further checking: property writes made inside a transaction are reverted by one Undo; the standard Cost property accepts a .NET `decimal`; Inventor reports the file name as the Part Number when none has been set; WPF windows shown through `WindowHost` load without extra assembly resolution.
- **No `UseLayoutRounding` in windows.** Not in window XAML, not in `UI/Theme/`. At 150% display scale it cut off the bottom border of every text box and dropdown in Inventor 2026. The shared window style sets `SnapsToDevicePixels` instead.
- **`Document` is not `_Document`.** Some API parameters are typed `_Document`, for example `TransactionManager.StartTransaction`. A `Document` does not convert implicitly, so cast it: `(_Document)doc`.

## What must stay out of the repo

This is a personal, public repository.

- `ADDIN_PICS/` holds reference screenshots of another company's add-in. It is gitignored and is not available in cloud sessions. The specs in `docs/features/` describe the behaviour we want in our own words.
- Material library files (`*.adsklib`) are gitignored. The add-in reads the library from a path in settings; it never ships one. Do not copy material names from a library into code, docs, tests or fixtures. Invent test data.
- Do not put company names, real part numbers, title blocks, customer names or any employer data in code, docs, tests or fixtures.
- Test fixtures exported with *Export Model Data* must come from Aidan's personal models only.
- `docs/ui-mockups/` is for our own mockups, not for screenshots of other software.
- Our names: ribbon tab and product name "AWB Addin", id prefix `Awb` (commands `Awb_`), data folder `%APPDATA%\AwbAddin`, log file `awbaddin.log`. They live in `Branding` in Core; nothing else in `src/` repeats them.

## Tracking files

Three files at the repo root keep the project legible between sessions. Each has one job, so nothing is recorded twice.

| File | Holds | Changes when |
|---|---|---|
| `STATUS.md` | the state of each big item, and what works in Inventor today | an item changes state |
| `TODO.md` | what is next, what is waiting on Aidan, proposed work, follow-ups from finished tasks | something comes up, gets done, or becomes a task |
| `DECISIONS.md` | every decision, with who made it | a decision is made. Append only |

- **Only the parent session edits these three files.** Implementers and reviewers do not, so tasks running at the same time never collide on them. An implementer records what it notices under "Follow-ups" in its task file; the parent copies anything still open into `TODO.md` when it commits the task.
- **Read all three before planning.** Do not plan against a decision without checking `DECISIONS.md`, and do not re-ask a question it already answers.
- **A default is not a decision by Aidan.** When an agent has to pick a behaviour to keep work moving, log it in `DECISIONS.md` as "Default" and put the question in the spec. Aidan can overturn it.
- **When Aidan decides something**, in a session or through a spec edit, add it to `DECISIONS.md` as "Aidan" and remove the matching line from `TODO.md`.
- Planned tasks live in `docs/PLAN.md`, not in `TODO.md`. When a to-do item becomes a task, delete it from `TODO.md`.

## Working rules

- One task, one commit. Do the task you were given and nothing else. Put anything else you notice under "Follow-ups" in the task file.
- Questions only Aidan can answer go in the feature spec's "Open questions" section. Do not invent the answer. Build what is decided and stop at the boundary.
- Do not commit `bin/` or `obj/`.
- Core uses file-scoped namespaces and implicit usings. The add-in uses block namespaces and explicit usings. Nullable is on everywhere. Match the file you are in.
