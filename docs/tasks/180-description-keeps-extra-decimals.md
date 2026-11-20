# 180: Fix: description lengths keep their extra decimals

- **Milestone:** M4 part 2 (fix from Aidan's check of 174 to 176, 2026-11-20)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` (open question 14)
- **Status:** todo
- **Depends on:** 156
- **Needs:** cloud (Core only; runs on Windows too)
- **Parallel-safe with:** 177, 178

## Goal

D1 shows `Ø2.3` for a 2.25 mm hole. Descriptions are written with exactly their table's decimals (1 in the clearance table), so 2.25 rounds to 2.3. Aidan (2026-11-20, as agreed in spike 160 round 3): diameters keep their extra decimals; only XDIM and YDIM use the table's decimals.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableDescription.cs`: every length in a description (hole diameter, depths, seat diameter and depth, thread depth, taper engagement) is written with **at least** the table's decimals and **at most** 3 in millimetres or 4 in inches. Round to the maximum, then drop trailing zeros down to the table's decimals. So in the clearance table (1 decimal, mm): 2.25 gives `2.25`, 5 gives `5.0`, 6.6 gives `6.6`, 2.2501 gives `2.25`; in the precision table (2 decimals): 3.2 gives `3.20`. Deviations (3 or 4 places with a sign) and the countersink angle are unchanged. Keep the existing minus-zero handling.
- `HoleTableDescriptions.For(hole, decimals)`: `decimals` is now the minimum; say so in its comment.
- Tests in `tests/InventorAddin.Core.Tests` (the description tests). Existing expectations that relied on rounding a longer value to the table's decimals change; list each changed expectation in Verification.
- `docs/features/04-hole-table-wizard.md`: question 14 reads: descriptions show at least their table's decimals and keep extra ones when the value needs them, up to 3 in mm and 4 in inches (Aidan, 2026-11-20); XDIM and YDIM use exactly the table's decimals. Also check "Target output" for wording that says otherwise.

Out of scope: XDIM and YDIM; the tolerance deviations.

## Acceptance criteria

- [ ] Clearance mm: 2.25 shows `Ø2.25`; 5.0 shows `Ø5.0`; a depth of 6.5 shows `6.5`.
- [ ] Precision mm: 3.2 shows `Ø3.20`; 3.205 shows `Ø3.205`.
- [ ] Inch clearance (2 decimals): 0.25 in shows `0.25`; 0.1875 in shows `0.1875`; 0.18755 in shows `0.1876` (4 at most).
- [ ] A value that differs from a short one only by floating-point noise (2.2500000001 mm) shows `2.25`, so two holes of the same size keep the same description key.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- `Number(value, decimals)` and the `Len` local are the only places lengths are formatted. Change `Len`, not `Signed`.
- A stored table drawn before this change has description keys with the old text, so the next check reports those groups as "descriptions changed" until Update. That is expected; note it in Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Drawing with a 2.25 mm clearance hole. Update its tables (the check may first report "descriptions changed"; that is expected once): it reads `Ø2.25 THRU`. XDIM and YDIM still show 1 decimal.

## Follow-ups

Things noticed but not done.
