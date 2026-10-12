# 063: Create Print makes .idw drawings

- **Milestone:** M2 (fix run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Commands: Create Print")
- **Status:** todo
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

- [ ] A Core rule picks the `.idw` template from the paths Inventor's lookups return, for example `PrintPlan.IdwTemplate(IEnumerable<string?> lookedUp, Func<string, bool> exists)`: for each path in order (blank ones skipped), the path itself when it already ends `.idw`, otherwise the same path with its extension changed to `.idw`; the first one that exists, or null. Tested: `…\Metric\ANSI (mm).dwg` gives `…\Metric\ANSI (mm).idw` when that exists; a missing sibling falls through to the next lookup (`standard.dwg` gives `standard.idw`); none existing gives null; extension compared ignoring case
- [ ] `Templates.DrawingFor` passes the ANSI lookup in the chosen units, then the units-neutral lookup (both as now), through that rule with `System.IO.File.Exists`, and logs which `.idw` template it chose or that none exists
- [ ] A new print's extension is `.idw` (`PrintPlan` constant, for example `NewPrintExtension`), and Create Print saves `Prints\NAME,AWB-10001.idw`
- [ ] `FindExistingPrint` still finds a `.dwg` or `.idw` print, in `Prints` or beside the model, and Create Print offers to open it; when both exist, the `.idw` is preferred (pass `.idw` as the preferred extension). Tested
- [ ] With no `.idw` template, Create Print stops before making anything with a message naming the missing template kind and units, for example "Inventor has no .idw drawing template for metric units (ANSI (mm).idw). No print was created." It does not fall back to a `.dwg` template (Default)
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Spike 032 (section 1): the lookups return `C:\Users\Public\Documents\Autodesk\Inventor 2026\Templates\en-US\Metric\ANSI (mm).dwg` and `English\ANSI (in).dwg`, and the units-neutral `standard.dwg`; `.idw` templates such as `Metric\ANSI (mm).idw` sit in the same folders. A drawing must be saved with its template's extension, so the template must be the `.idw` one.
- Keep using the existing lookups; only the step after them changes. No new Inventor members are needed.
- The sheet in `ANSI (mm).idw` may differ in size from the `.dwg` one; `ChooseScale` already works from `Sheet.Width` and `Sheet.Height`.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
