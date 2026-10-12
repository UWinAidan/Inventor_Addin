# 063: Create Print makes .idw drawings

- **Milestone:** M2 (fix run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Create Print")
- **Status:** done
- **Depends on:** none
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-12: Create Print makes `.idw` drawings instead of `.dwg`, from Inventor's default `.idw` template: `ANSI (mm).idw` for metric (and `ANSI (in).idw` for imperial). Existing `.dwg` prints are still found and opened, and Rename still renames both kinds.

## Scope

- `src/InventorAddin.Core/Drawing/PrintPlan.cs` (the template choice, the extension of a new print, the missing-template message)
- `src/InventorAddin/Inventor/Templates.cs` (`DrawingFor`)
- `src/InventorAddin/Commands/CreatePrintCommand.cs` only where it uses the extension or the message
- The matching tests under `tests/InventorAddin.Core.Tests/`

Out of scope:

- A templates folder setting, custom templates (M5), sheet size.

## Acceptance criteria

- [x] A Core rule picks the `.idw` template from the paths Inventor's lookups return, for example `PrintPlan.IdwTemplate(IEnumerable<string?> lookedUp, Func<string, bool> exists)`: for each path in order (blank ones skipped), the path itself when it already ends `.idw`, otherwise the same path with its extension changed to `.idw`; the first one that exists, or null. Tested: `…\Metric\ANSI (mm).dwg` gives `…\Metric\ANSI (mm).idw` when that exists; a missing sibling falls through to the next lookup (`standard.dwg` gives `standard.idw`); none existing gives null; extension compared ignoring case
- [x] `Templates.DrawingFor` passes the ANSI lookup in the chosen units, then the units-neutral lookup (both as now), through that rule with `System.IO.File.Exists`, and logs which `.idw` template it chose or that none exists
- [x] A new print's extension is `.idw` (`PrintPlan` constant, for example `NewPrintExtension`), and Create Print saves `Prints\NAME,AWB-10001.idw`
- [x] `FindExistingPrint` still finds a `.dwg` or `.idw` print, in `Prints` or beside the model, and Create Print offers to open it; when both exist, the `.idw` is preferred (pass `.idw` as the preferred extension). Tested
- [x] With no `.idw` template, Create Print stops before making anything with a message naming the missing template kind and units, for example "Inventor has no .idw drawing template for metric units (ANSI (mm).idw). No print was created." It does not fall back to a `.dwg` template (Default)
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Spike 032 (section 1): the lookups return `C:\Users\Public\Documents\Autodesk\Inventor 2026\Templates\en-US\Metric\ANSI (mm).dwg` and `English\ANSI (in).dwg`, and the units-neutral `standard.dwg`; `.idw` templates such as `Metric\ANSI (mm).idw` sit in the same folders. A drawing must be saved with its template's extension, so the template must be the `.idw` one.
- Keep using the existing lookups; only the step after them changes. No new Inventor members are needed.
- The sheet in `ANSI (mm).idw` may differ in size from the `.dwg` one; `ChooseScale` already works from `Sheet.Width` and `Sheet.Height`.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (local session): `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, 0 errors. The 8 warnings are the existing CS8600 on `Forms.Cursor.Current` in four commands, CreatePrintCommand among them; none are new. `dotnet test tests/InventorAddin.Core.Tests`: 2184 passed, 0 failed, 0 skipped.
- **Core:** `PrintPlan.NewPrintExtension` (`.idw`), `PrintPlan.IdwTemplate(IEnumerable<string?>, Func<string, bool>)`, `PrintPlan.IdwTemplateName(UnitsSystem)`, and a new `MissingTemplateMessage` text. Tests are in `tests/InventorAddin.Core.Tests/Drawing/PrintPlanTests.cs`.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
  - `Inventor/Templates.cs`: `DrawingFor` runs the two existing lookups lazily (ANSI in the chosen units, then the units-neutral default) through `PrintPlan.IdwTemplate` with `System.IO.File.Exists`, and logs what each lookup returned and which `.idw` template it chose, or that there is none. The second lookup runs only when the first gives no `.idw`.
  - `Commands/CreatePrintCommand.cs`: saves with `PrintPlan.NewPrintExtension` and passes it as the preferred extension to `FindExistingPrint`. It still checks for an existing print before checking for a missing template, as before. Removed the unused `IOPath` alias.
- **Inventor API members not confirmed:** none. No new Inventor members are used; the lookups are the same calls as before.
- **Manual checklist for Inventor:**
  1. Set the default units to metric in Settings. Open a saved part with a number and no print (for example `C:\Work\TEST_PLATE,AWB-10001.ipt`, with no `Prints\TEST_PLATE,AWB-10001.*` and no `.dwg` or `.idw` of that name beside it). Click **Create Print**. A drawing opens, made from `ANSI (mm).idw`, with a front view centred and scaled to fit. It is saved as `C:\Work\Prints\TEST_PLATE,AWB-10001.idw`. `awbaddin.log` names the `...\Metric\ANSI (mm).idw` template.
  2. Click **Create Print** on the same part again. It asks "A print for this part already exists. Open it?" Yes opens the `.idw`.
  3. On a part that has only an old `Prints\NAME.dwg`, or a `NAME.dwg` beside the model, click **Create Print**. It offers to open that `.dwg`, and Yes opens it.
  4. Put both a `.dwg` and an `.idw` print in `Prints` for a part and click **Create Print**. Yes opens the `.idw`.
  5. Switch to imperial units and repeat step 1 on another part. The drawing comes from `ANSI (in).idw` (`English` folder).
  6. Optional: rename `ANSI (mm).idw` and `standard.idw` in the templates folders for a moment, then run step 1. The message reads "Inventor has no .idw drawing template for metric units (ANSI (mm).idw). No print was created." No drawing is made. Restore the files afterwards.
  7. Rename a part that has both a `.dwg` and an `.idw` print. Both are renamed (task 059 behaviour, unchanged).

## Follow-ups

Things noticed but not done.

- The CS8600 warning on `Forms.Cursor previousCursor = Forms.Cursor.Current;` appears in CreatePrint, NewPart, SaveAsCopy and SaveAsReplace. Typing it as `Forms.Cursor?` would clear all four. This was already there before this task.
- If the `.idw` beside the ANSI `.dwg` is missing but `standard.idw` exists, Create Print uses `standard.idw` without saying so. That template may not match the chosen units. Only the log records it.
