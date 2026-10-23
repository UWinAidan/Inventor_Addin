# 083: ISO 286 hole limits (Core)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes"); also used by the hole table wizard (spec 04)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 082, 086, 087

## Goal

Core works out the upper and lower deviations of a hole for an ISO 286 code (for example H7 at 6 mm) so the window can show them and the hole table can print them. Inventor applies the fit itself in the model; this is for display and checking.

## Scope

- `src/InventorAddin.Core/Holes/Iso286.cs` (or a small `Tolerances` folder)
- `tests/InventorAddin.Core.Tests/Holes/`

## Acceptance criteria

- [x] `Iso286.HoleLimits(nominalMm, code)` gives upper and lower deviations in mm for hole codes: fundamental deviations **F, G, H, JS, K, M, N, P** and grades **IT5 to IT11**, nominal sizes over 0 up to 500 mm, from the ISO 286-1 tables (IT values and fundamental deviations, including the delta rule for K, M, N up to IT8 and P up to IT7). Codes are case-sensitive as ISO writes them (`H7`, `JS7`, `n7` is a shaft and is refused).
- [x] A size outside the range, an unknown letter or grade gives a clear failure result, not an exception.
- [x] Tests pin known values, at least: 6 mm H7 +0.012/0, N7 -0.004/-0.016, P7 -0.008/-0.020, G7 +0.016/+0.004, F7 +0.022/+0.010 (these match Inventor 2026, spike 081); 10 mm H7 +0.015/0; 25 mm H7 +0.021/0; 50 mm H8; size boundaries (3 mm belongs to "over 0 to 3"); a delta case such as 20 mm N7.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Type the tables from the standard's values; do not copy them from a third-party library's source.
- Imperial Limits rows do not use this; they carry their own upper and lower values (task 084).

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows). `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 2740 passed, 0 failed, 0 skipped (90 of them in `Holes/Iso286Tests.cs`).
- **Not compiled (changed under `src/InventorAddin`):** none; this task changes Core and tests only.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** nothing to check in Inventor yet; nothing calls `Iso286` until the AWB Hole window shows a Tolerance (later M4 task). When it does: open a part, run AWB Hole, choose AWB Dowel, 6 mm, an ISO tolerance row (H7), and check the window's limits read +0.012 / 0, the same as Inventor's own fit on the hole diameter (Parameters, or the hole's dimension tolerance).

What was built:

- `src/InventorAddin.Core/Holes/Iso286.cs`: `Iso286.HoleLimits(nominalMm, code)` returns an `Iso286Limits` record (`Succeeded`, `Code`, `NominalMm`, `UpperUm`/`LowerUm` as `decimal` micrometres, `UpperMm`/`LowerMm` as `double`, `Message`). It never throws; a bad size, letter, grade or malformed code gives `Succeeded = false` with a message. Also `StandardToleranceUm(nominalMm, grade)`, `DeltaUmFor(nominalMm, grade)`, `It4Um`, `Letters`, `SizeStepUpperMm` (used by the tests to check the tables against each other).
- Tables (IT4 to IT11, delta IT5 to IT8, fundamental deviations F, G, K, M, N, P) typed from the ISO 286-1 values. A test checks delta = IT n - IT n-1 for every size, which cross-checks the two typed tables.
- Readings of ISO 286-1 built in (please confirm at review against a copy of the standard):
  - JS7 to JS11: an odd IT value is rounded to the even value below before halving (25 mm JS7 = +/-0.010). JS5 and JS6 are halved exactly (25 mm JS6 = +/-0.0065).
  - M6 over 250 to 315 mm uses the standard's special case ES = -9 um.
  - K above IT8 (K9 to K11) is refused for sizes over 3 mm (the standard gives no value there); up to 3 mm ES = 0.
  - N above IT8 is refused for sizes up to 1 mm (the standard says not to use it); over 1 to 3 mm ES = -4 um, over 3 mm ES = 0.
  - Codes are trimmed; letters are case-sensitive; an all-lowercase supported letter (`n7`) is refused as a shaft; mixed case (`Js7`) is refused as not a hole code.

## Follow-ups

- The table values were typed from the standard's numbers without a copy of ISO 286-2 in the repo to check every cell; the tests pin about 40 known values plus the internal cross-checks. Worth a spot-check against ISO 286-2 by Aidan or a reviewer, especially the large sizes (over 250 mm).
- The K9 to K11 (over 3 mm) and N9 to N11 (up to 1 mm) refusals are readings of the standard's footnotes, not decisions by Aidan; if the Precision Tol tab ever holds such a code, the window will show the failure message.
