# 213: Fix: Finish Spec thickness in µm or inches

- **Milestone:** M5 (Aidan, 2026-11-22, overturning task 211's Default)
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Finish on the print", question 23); `docs/features/03-material-finish.md`
- **Status:** done
- **Depends on:** 211
- **Needs:** windows build (Core plus a small add-in change)
- **Parallel-safe with:** none

## Goal

Finish Spec shows the thickness in the part's own units: µm on metric parts ("ANODISE, CLEAR, 10-15 µm") and inches on imperial parts ("ANODISE, CLEAR, .0004-.0006 IN"), with no trailing comma when there is no thickness. Finish Thickness itself stays stored in mm (Aidan, 2026-11-22).

## Scope

- Core:
  - `FinishProperties.SpecText(Finish, units)` takes the part's units (metric or imperial; reuse an existing Core units enum if one fits, for example `PrintUnits` from task 185, otherwise add a small one).
  - Metric: the thicknesses in µm (mm × 1000), shown with up to one decimal and trailing zeros dropped ("10-15 µm", "12.5 µm"). Imperial: in inches (mm ÷ 25.4) to 4 decimals with no leading zero, then " IN" (".0004-.0006 IN"). Use one value when the minimum and maximum are equal. Show no thickness when both are zero or when the values do not read as numbers. Then the text is the name alone, with no comma.
  - `FinishProperties.For` gains the same units parameter for the Finish Spec write. The other four writes are unchanged; Finish Thickness stays the mm text.
- Add-in: where the finish is applied to each part (`MaterialFinishTargets.Apply` and its callers), work out each target part's units and build that part's writes for them.
  - The units come from the part's `UnitsOfMeasure.LengthUnits`: inch or foot means imperial, anything else metric (Default; say so under Verification).
  - One choice applied to an assembly selection gives each part its own text.
  - Keep `MaterialFinishChoice` usable; the simplest is to carry the `Finish` and build the writes per part.
  - The two stale "four finish iProperties" comments (`MaterialFinishCommand.cs:23`, `MaterialFinishTargets.cs` near line 131) become "five".
- Tests in `tests/InventorAddin.Core.Tests` next to the `FinishProperties` tests:
  - metric range, single value and decimal
  - imperial range and single value
  - zero thickness, metric and imperial (no comma)
  - unreadable values (name only)
  - the five writes in order for both units

## Acceptance criteria

- [x] Metric part, finish 0.010 to 0.015 mm: Finish Spec "<name>, 10-15 µm"; Finish Thickness still "0.010-0.015 mm".
- [x] Imperial part, same finish: "<name>, .0004-.0006 IN".
- [x] No thickness: "<name>" on both.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

- Invent test data. Read the DECISIONS.md rows dated 2026-11-22 about Finish Spec first.
- No new Inventor API member: `Document.UnitsOfMeasure.LengthUnits` and `UnitsTypeEnum.kInchLengthUnits` are already used in `HoleTableSource.cs` and `HoleExpressionChecker.cs`.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5122 passed, 0 failed. `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors, 8 warnings (none from changed lines).
- **Not compiled (changed under `src/InventorAddin`):** none. `MaterialFinishTargets.cs` and `MaterialFinishCommand.cs` were built on Windows.
- **Inventor API members not confirmed:** none. `UnitsOfMeasure.LengthUnits`, `UnitsTypeEnum.kInchLengthUnits` and `kFootLengthUnits` all compiled. Not yet run in Inventor.
- **Default:** inch or foot length units mean imperial, anything else (including an unreadable value) metric. Log in DECISIONS.md as "Default".
- **Design:** `FinishProperties.For` and `SpecText` take `PrintUnits` (required). `MaterialFinishChoice.PropertyWrites` stays (metric) for the window and tests; the target calls `choice.WritesFor(units)` per part. Imperial single value rounds to 4 decimals first, so two values that round the same show once.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open a metric part, Material and Finish window, pick a finish with 0.010 to 0.015 mm. Apply. Finish Spec reads "<name>, 10-15 µm"; Finish Thickness reads "0.010-0.015 mm".
  2. Open an inch part, apply the same finish. Finish Spec reads "<name>, .0004-.0006 IN".
  3. Select one metric and one inch part in an assembly, apply once. Each part gets its own Finish Spec.
  4. Pick a finish with no thickness. Finish Spec is the name alone, no comma.
  5. One Undo reverts the whole apply.

## Follow-ups

- Existing parts keep the old mm-based Finish Spec until the finish is applied again.
