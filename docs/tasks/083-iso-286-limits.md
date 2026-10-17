# 083: ISO 286 hole limits (Core)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Dowel and precision holes"); also used by the hole table wizard (spec 04)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 082, 086, 087

## Goal

Core works out the upper and lower deviations of a hole for an ISO 286 code (for example H7 at 6 mm) so the window can show them and the hole table can print them. Inventor applies the fit itself in the model; this is for display and checking.

## Scope

- `src/InventorAddin.Core/Holes/Iso286.cs` (or a small `Tolerances` folder)
- `tests/InventorAddin.Core.Tests/Holes/`

## Acceptance criteria

- [ ] `Iso286.HoleLimits(nominalMm, code)` gives upper and lower deviations in mm for hole codes: fundamental deviations **F, G, H, JS, K, M, N, P** and grades **IT5 to IT11**, nominal sizes over 0 up to 500 mm, from the ISO 286-1 tables (IT values and fundamental deviations, including the delta rule for K, M, N up to IT8 and P up to IT7). Codes are case-sensitive as ISO writes them (`H7`, `JS7`, `n7` is a shaft and is refused).
- [ ] A size outside the range, an unknown letter or grade gives a clear failure result, not an exception.
- [ ] Tests pin known values, at least: 6 mm H7 +0.012/0, N7 -0.004/-0.016, P7 -0.008/-0.020, G7 +0.016/+0.004, F7 +0.022/+0.010 (these match Inventor 2026, spike 081); 10 mm H7 +0.015/0; 25 mm H7 +0.021/0; 50 mm H8; size boundaries (3 mm belongs to "over 0 to 3"); a delta case such as 20 mm N7.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Type the tables from the standard's values; do not copy them from a third-party library's source.
- Imperial Limits rows do not use this; they carry their own upper and lower values (task 084).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups
