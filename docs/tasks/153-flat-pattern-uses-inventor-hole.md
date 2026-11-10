# 153: In a flat pattern, H, double-click and Edit Feature open Inventor's Hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** done
- **Depends on:** 142
- **Needs:** windows build
- **Parallel-safe with:** 152, 154 (disjoint files; run one after another anyway, they share the build)

## Goal

Aidan's checklist 141 (2026-11-10): on a sheet metal part's Flat Pattern, the mini toolbar shows Inventor's Hole as designed, but the H shortcut and a double-click on a hole still open AWB Hole. Decision (Aidan, 2026-11-10): Flat Pattern uses Inventor's Hole everywhere. H, double-click and Edit Feature open Inventor's Hole (or Inventor's own hole edit) there.

## Scope

- `src/InventorAddin.Core/Holes/HoleTakeoverRules.cs` and tests: the rule that, in a flat pattern, the AWB Hole command started by its shortcut hands over to Inventor's Hole, and a double-click or Edit Feature on a hole is left to Inventor. Reuse `InFlatPattern` and `TakesOverHoleEntries` where they fit.
- `src/InventorAddin/Commands/AwbHoleCommand.cs` (or wherever the command starts the session): in a flat pattern, start Inventor's `PartHoleCmd` instead of opening the window, marked as asked for so the backstop in `HoleEditRedirect` lets it through (as the split button's Inventor Hole entry does, task 142 step 5). Log one Info line.
- `src/InventorAddin/Holes/HoleEditRedirect.cs`: `OnDoubleClick` and the Edit Feature route (`EditFeaturePressed`, and the swap of Edit Feature for `HoleEditFeatureCommand` if it happens in a flat pattern) leave Inventor's own behaviour in a flat pattern. If our Edit Feature command is already showing and pressed there, it runs Inventor's edit for that hole.
- `src/InventorAddin/Commands/HoleEditFeatureCommand.cs` if needed.
- `docs/features/09-awb-hole.md`: one line under the Flat Pattern rule.

Out of scope:

- The ribbon and the marking menu in a flat pattern (task 142, passed).
- The folded model: unchanged.

## Acceptance criteria

- [ ] Core tests for the flat pattern rule.
- [ ] In a flat pattern: H opens Inventor's Hole and it stays open; double-click on a hole and Edit Feature open Inventor's hole edit. Outside a flat pattern nothing changes.
- [ ] `dotnet build InventorAddin.slnx` on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Read task 142's brief and Verification (the backstop, the "asked" flag, `InFlatPattern`). Find how the split button starts Inventor's Hole as asked for and reuse it. Inventor's edit command name for a feature is not confirmed in this repo: find what Inventor runs today when our redirect is not in the way, and list any name you could not confirm. Inventor may be running: check `tasklist //FI "IMAGENAME eq Inventor.exe"` and build with `-p:DeployToInventor=false` while it runs.

## Verification

Filled in by the implementer.

### What changed

- Core (`HoleTakeoverRules`): `AwbHoleRunsInventorHole(isPart, inFlatPattern)`, `EditOpensAwbHole(owner, isPart, inFlatPattern)` (task 092's `HoleEditRules.Redirects` and not in a flat pattern), `EditFeaturePressedAction(isPart, inFlatPattern)` returning the new enum `EditFeatureAction` (`NotRun`, `OpenAwbHole`, `RunInventorsEdit`), and the log lines `FlatPatternHoleMessage` and `FlatPatternEditMessage`. Tests in `HoleTakeoverRulesTests`.
- `AwbHoleCommand.Execute`: after the part check, in a flat pattern it logs `FlatPatternHoleMessage` and runs Inventor's `PartHoleCmd` instead of opening the window. Any start of the AWB Hole command does this (H, the split button, the AWB Addin tab button), not only H.
- `InventorHoleCommand` (in `HoleDropdownCommands.cs`): its "ask the backstop, then run `PartHoleCmd` after the event returns" code is now `internal static RunInventorsHole(internalName, displayName)`, used by both commands. The split button's behaviour is unchanged. In a flat pattern the backstop would not stop `PartHoleCmd` anyway (`StopsInventorHole` is false there), but the request is still made as the brief asks.
- `HoleEditRedirect`: `InFlatPattern` is now `internal static InFlatPattern(Application)` so the command can use it. `Redirects` uses `EditOpensAwbHole`, so in a flat pattern the browser menu and mini toolbar keep Inventor's Edit Feature, a double-click is left unhandled (Inventor's edit runs), and the `OnActivateCommand` backstop does not stop Inventor's `PartNGxHoleEditCtxCmd`. `EditFeaturePressed` switches on `EditFeaturePressedAction`: in a flat pattern (a menu built just before the flat pattern was entered) it logs `FlatPatternEditMessage` and runs `PartNGxHoleEditCtxCmd` after the event returns.
- Spec 09: one sub-bullet under the Flat Pattern rule.

- **Ran:**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows (Inventor running, so **not deployed**): build succeeded, 0 errors, 9 warnings, all in files this task did not touch (CS8600 in four commands, xUnit2012 in `HoleDiagramTests.cs`).
  - `dotnet test tests/InventorAddin.Core.Tests --no-build`: 4565 passed, 0 failed, 0 skipped.
- **Not compiled (changed under `src/InventorAddin`):** none. `AwbHoleCommand.cs`, `HoleDropdownCommands.cs` and `HoleEditRedirect.cs` compiled on Windows. Not yet run in Inventor; Aidan must close Inventor and build without `-p:DeployToInventor=false` to deploy.
- **Inventor API members not confirmed:** no new member names; every member used (`ControlDefinitions[...].Execute()`, `ActiveEditObject`, `FlatPattern`, `UserInterfaceManager.ActiveEnvironment.InternalName`) already compiled and ran in tasks 092, 129 and 142. Behaviour not yet seen in Inventor:
  1. Whether `PartNGxHoleEditCtxCmd` (task 092's Edit Feature name) is also the edit command for a hole in a flat pattern, and whether running it with `Execute()` from our Edit Feature edits the selected hole. That only matters for the rare case of our entry still showing in a flat pattern. The double-click and Edit Feature on a fresh menu need no command name: the add-in now simply leaves them to Inventor.
  2. Whether running `PartHoleCmd` from our command in the flat pattern opens Inventor's flat pattern Hole, as the Flat Pattern tab's Hole button does.
- **Manual checklist for Inventor:** (close Inventor, build `dotnet build InventorAddin.slnx` to deploy, start Inventor; AWB Hole has Hole, i.e. "Use Inventor's Hole" off)
  1. Open a sheet metal part with a hole, go to its Flat Pattern. Press **H** with the graphics window active. Inventor's Hole dialog opens and stays open. The log has "AWB Hole: a flat pattern is being edited, so Inventor's Hole runs instead ..." and no "Hole takeover: Inventor's Hole ... started; AWB Hole opens instead".
  2. Still in the Flat Pattern, add a hole with Inventor's Hole (or use one there). **Double-click** it in the browser, then on one of its faces in the graphics window: Inventor's hole edit opens each time, not the AWB Hole window.
  3. Right-click the hole in the browser: the menu's **Edit Feature** is Inventor's (opens Inventor's edit). Click the hole in the graphics window: the mini toolbar's Edit Feature opens Inventor's edit too.
  4. **Go to Folded Part**. Press H: the AWB Hole window opens. Double-click a hole made in the folded model: the AWB Hole window opens on it. Right-click > Edit Feature: the AWB Hole window opens. (Nothing changed outside the flat pattern.)
  5. In a plain part, repeat step 4: same as before this task.
  6. Split button > Inventor Hole in the folded model still opens Inventor's Hole and it stays open (the shared run code).
  7. If any step fails, send the log lines around it.

## Follow-ups

- The AWB Hole button on the AWB Addin tab also runs Inventor's Hole in a flat pattern (the command does not know how it was started). This follows "Flat Pattern uses Inventor's Hole everywhere"; if Aidan wants that button to say something instead, it is a small change in `AwbHoleCommand`.
- `HoleEditRules`' class remarks still say editing works "on the part open in the window only, not on a part edited in place", which task 105 changed. Doc fix only.
