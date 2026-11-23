# 226: Decimal period on every dimension style a print uses

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 223)
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 225
- **Needs:** windows build; Aidan rebuilds both templates
- **Parallel-safe with:** none

## Goal

A Create Print drawing still shows "20,00", "120,00", "260,00" on ordinate and linear dimensions: the builder set the period on only 2 dimension styles. Set it on every style the drawing's dimensions actually use (ordinate, linear, radial, angular, hole notes), as spike 225 found, and fix or quieten the 3 feature control frame E_FAIL warnings per template.

## Scope

- `PrintTemplateBuilder` decimal marker step, using spike 225's findings: the drawing's dimensions all use 'Default (ISO)' (LeaderText 'Default - Method 1b (ISO)'), and Inventor replaces them with the style library's comma versions on `Documents.Add` because the names match. Copy each to an "AWB ..." name (`style.Copy`), set the period on the copy, and point every object-defaults member that named the original at the copy. The feature control frame style needs `LinearUnits` set to mm (inches on imperial) before its marker can be set; do not turn on alternate units. The imperial template already prints a period; only its frame style needs the fix.
- Create Print: wrap `Documents.Add` in `Application.SilentOperation` (restored after) so the Style Conflict dialog cannot stop it.
- The log line names the styles set.

## Acceptance criteria

- [ ] On a fresh Create Print drawing every dimension kind prints a period.
- [ ] A template build logs no decimal marker warning, or one explained line per style that cannot be set.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 11 warnings (built on Windows).
  `dotnet test tests/InventorAddin.Core.Tests`: first run 3 failed (the known flaky probe-timeout tests), rerun 5342 passed,
  0 failed. No Core code changed. The behaviour itself was not run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** nothing; both changed files built. Not run in Inventor.
  - `Drawing/PrintTemplateBuilder.cs`: `SetStyleDecimalMarkers` now, on metric only, copies each distinct dimension style to "AWB <name>", sets the
    period (and the alternate marker) on the copy, and reassigns every `ObjectDefaultsStyle` member that named the original. The
    feature control frame style is copied the same way, `LinearUnits` is set to mm (inch on imperial) before its marker, and
    alternate units are not touched. The log line lists the styles set.
  - `Commands/CreatePrintCommand.cs`: `Documents.Add` is wrapped in `Application.SilentOperation = true`, restored in a `finally`.
- **Inventor API members not confirmed:** the build confirms the names. Not confirmed at run time: the setters on each
  `ObjectDefaultsStyle` dimension style member (they compile; spike 225 assigned them), `FeatureControlFrameStyle.LinearUnits`
  taking `UnitsTypeEnum.kMillimeterLengthUnits` / `kInchLengthUnits` (spike 225 did mm only; inch is from the spike's note), and
  `FeatureControlFrameStyle.Copy` (spike 225 used it).
- **Manual checklist for Inventor:**
  1. Close Inventor, build with deploy, open Inventor.
  2. Run Build Print Template for metric and for imperial. Check awbaddin.log: one "period decimal marker set on N style(s)" line
     naming "AWB Default (ISO)", "AWB Default - Method 1b (ISO)" and "AWB Feature Control Frame (ISO)" (imperial: only
     "AWB Feature Control Frame (ANSI)"), and no decimal marker warning.
  3. Open a test part with holes, click Create Print. Afterwards the Style and Standard Editor shows the object defaults naming "AWB Default (ISO)", and a dimension prints a period.
  4. On the drawing add a linear, an ordinate set, a radius, a diameter, an angular and a hole note (and a bend note on a sheet metal flat pattern): all should print a period ("20.00", "R2.25").
  5. Imperial: repeat with a part set to inches; the period was already printed, check it still is.

## Follow-ups

Things noticed but not done.
- A template built before this task still holds the library-named styles; Aidan rebuilds both templates (task header).
- If a frame style's alternate marker (`AlternateDecimalMarkerType`) turns out to matter, it was not set (needs alternate units, spike 225).
