# 168: Fix: read the view's ordinate origin when the origin indicator fails, and tell "no origin" from "could not read it"

- **Milestone:** M4 part 2 (fixes from Aidan's check, 2026-11-18)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Origin)
- **Status:** done
- **Depends on:** 163
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 170

## Goal

Aidan rotated the views (VIEW1 now a side view, VIEW2 the front). Hole Table on VIEW2 then said the view has no ordinate origin, although it has ordinates; deleting and re-adding VIEW1's ordinate changed nothing. The log shows `Hole table: View origin point: Unspecified error (0x80004005 (E_FAIL))`: `view.OriginIndicator.Intent.PointOnSheet` throws, so the indicator exists but its point cannot be read, and the source then reports no origin. After this task the wizard finds the origin through a fallback, and says "no origin" only when the view truly has none; when it has one that cannot be read, it says so.

## Scope

- `src/InventorAddin.Core/HoleTable/IHoleTableSource.cs`: `HoleTableViewAnalysis` gets a way to say an origin exists but could not be read (for example `bool OriginUnreadable = false`, or a reason string). Keep existing callers and tests compiling.
- `src/InventorAddin.Core/HoleTable/HoleTableOrigin.cs`: `HoleTableOriginResult.From` takes that and, with no origin read, stops with a new message instead of `NoOrdinateOrigin`.
- `src/InventorAddin.Core/HoleTable/HoleTableMessages.cs`: the new message, in plain words, for example "This view has an ordinate origin, but it could not be read. Delete the view's ordinate dimensions and add them again, then run the wizard again." (words are the implementer's; keep it short and tell the user what to do).
- `src/InventorAddin.Core/ViewModels/HoleTableWizardViewModel.cs`: pass the new field to `HoleTableOriginResult.From` (line about 360).
- `src/InventorAddin/HoleTable/HoleTableSource.cs` (`AnalyzeCore`, the origin block about line 397): the fallback read below.
- Tests in `tests/InventorAddin.Core.Tests` for the new origin result.

Out of scope: the WPF automation exception that is also logged under "View origin point" (task 169), and the stale-table check (tasks 171, 172).

## Acceptance criteria

- [ ] With `HasOriginIndicator` true and `Intent.PointOnSheet` throwing, the source tries the fallbacks in order and uses the first that gives a point.
- [ ] Each step that fails is logged with what failed (the warnings list), and the step that succeeded is logged once at Info, for example `Hole table: VIEW2 origin from ordinate dimensions (indicator point unreadable)`.
- [ ] Only a view with no origin indicator and no ordinate dimensions gives `NoOrdinateOrigin`. A view with an indicator or ordinates whose point cannot be read gives the new "could not read" message.
- [ ] Core tests cover: origins found, none found, none found but unreadable, several different origins (existing message unchanged).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes and `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

- Spike 160, round 1 (`docs/tasks/160-spike-hole-table-in-inventor.md`, Findings B8, and the API list on line 55): single ordinates live in `Sheet.DrawingDimensions.OrdinateDimensions`; sets in `OrdinateDimensionSets` (`OriginMember`). Each ordinate has `Intent.PointOnSheet`, a parent view, `IsOrdinateSetMember` and a value. In the spike, the two ordinates at the origin both had value 0 and their `Intent.PointOnSheet` was the origin corner, the same point as the indicator.
- Fallback order (Default, log it in the task file):
  1. `OriginIndicator.Intent.PointOnSheet` (as now).
  2. The indicator's intent geometry another way, only if you can confirm the member names in existing code or the API reference (for example the intent's `Geometry` as a `DrawingCurve` end point or a `Point2d`). `RelativeX`/`RelativeY` are on the spike's list but what they are relative to is not known: do not use them unless you can confirm it.
  3. Ordinate dimensions on this view: for an `OrdinateDimensionSet` on the view, its `OriginMember`'s `Intent.PointOnSheet`; else single ordinates on the view whose value is 0 (within a small tolerance), their `Intent.PointOnSheet`. Use only dimensions whose parent view is this view (compare with the view object as task 163 compares views, or by name if that is how existing code does it). Several zero ordinates at the same point count as one origin; ones at different points go through `HoleTableOriginResult.From` as now (more than one origin).
  4. Nothing found, but the view had an indicator or ordinates: unreadable.
- Do not guess member names. Check `Extraction/`, `HoleTable/` and the spike task's notes; list anything unconfirmed in Verification.
- `ComSafe.Get` swallows every exception. Keep using it, with a context naming the step.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: see report (all pass).
- **Fallback order used (Default):** 1 indicator `Intent.PointOnSheet`; 2 skipped (intent geometry / `RelativeX`/`RelativeY` not confirmed); 3a `OrdinateDimensionSets[].OriginMember.Intent.PointOnSheet` on this view; 3b single `OrdinateDimensions` on this view with `ModelValue` 0: a point counts as the origin only when zero-valued ordinates of two different `DimensionType`s sit there (one zero ordinate alone can be a hole lined up with the origin on one axis); 4 unreadable (an indicator, a failed indicator read, or ordinates on the view exist). An ordinate's view is its `Intent.Geometry` `DrawingCurve.Parent` (for a `Centermark`, its `AttachedEntity` as a `DrawingCurve`), compared as COM objects; if undeterminable a warning is logged. One Info line is logged on every path naming the step.
- **Tests:** `dotnet test tests/InventorAddin.Core.Tests`: 4834 passed, 0 failed. Build: 0 errors.
- **Not compiled (changed under `src/InventorAddin`):** none; `HoleTableSource.cs` was built on Windows (0 errors). Not run in Inventor.
- **Inventor API members not confirmed:** `Centermark.AttachedEntity`, `OrdinateDimension.DimensionType` and `DrawingCurve.Parent` (a `DrawingView`) were confirmed by reflection on the interop DLL. The rest compile, but behaviour is unverified in Inventor: `OrdinateDimension.ModelValue` (assumed cm, 0 at origin), `OrdinateDimension.Intent.Geometry` being a `DrawingCurve` whose `.Parent` is the view (if the geometry is another type the ordinate is not matched to a view).
- **Manual checklist for Inventor:**
  1. Aidan's rotated drawing: Hole Table on VIEW2 (the front view, with ordinates) analyses it, and the XDIM and YDIM measure from VIEW2's ordinate origin. The log says which way the origin was read.
  2. A view with no ordinate dimensions: the "no ordinate dimension origin" message, as before.
  3. A view with an ordinate to a hole lined up with the origin on one axis still gives one origin, at the corner (not at the hole).
  4. A view whose origin cannot be read: the wizard says "This view has an ordinate origin, but it could not be read...", not "no ordinate dimension origin".
  5. VIEW1 (now a side view) with its ordinate: Analyze finds the origin (or, if the view shows no holes end on, says holes were left out, not that there is no origin).

## Follow-ups

Things noticed but not done.
