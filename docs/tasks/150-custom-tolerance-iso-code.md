# 150: Custom tolerance takes a typed ISO 286 code

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Tolerance")
- **Status:** todo
- **Depends on:** 113, 115, 132, 149
- **Needs:** Core, then windows build
- **Parallel-safe with:** 140 to 143, 148 (not 144 to 147 or 149: same window files)

## Goal

Aidan's answer (2026-11-09) to a first-look item: a Custom tolerance can be given as a typed ISO 286 code, for example H8 or k6, **as well as** the Upper and Lower limits it takes today. This works on dowels and on toleranced Clearance and Simple holes, wherever Custom is offered.

## Scope

- **Core.** `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs`, `src/InventorAddin.Core/Holes/HoleSpec.cs` (beside `CustomUpper` and `CustomLower`), the stored data (`HoleStoredDataStore` or its Core codec; a new field needs a version step that older data still reads), and `Iso286`, plus tests:
  - Custom gets a way to enter a code. A suggested layout: a Code box before Upper and Lower. A valid code works out the limits at the chosen size through `Iso286`, as ISO rows do, and shows them in Upper and Lower, read-only. With no code, Upper and Lower are typed as today. Pick the clearest layout that fits the row, and log it as a Default.
  - An invalid code, or one `Iso286` has no limits for at this size, gives a message under the box, and OK is disabled. This follows the window's other typed boxes (task 124's typed records, task 130's live typing).
  - The code is remembered and stored with the hole, so an edit opens it as typed.
- **Add-in.** `src/InventorAddin/Holes/HoleFeatureWriter.cs` and `HoleFeatureReader.cs`: a Custom code is applied as ISO rows are, with Inventor's own fit tolerance. Read it back that way. An Inventor hole with a fit tolerance whose code has no row then opens as Custom with that code. This answers task 113's follow-up "a hole with an ISO code no row has", and this task removes that line from the follow-ups.
- **Colour.** A Custom code hole takes the same colour as Custom limits today (spec 09 question 17 Default).
- `docs/features/09-awb-hole.md`: the Tolerance row and the Toleranced section.

Out of scope:

- Workbook rows and their colours.

## Acceptance criteria

- [ ] Core tests: valid codes in either case (H7 and h7 mean different things, so case is kept as typed), invalid codes, a code with no limits at a size, a size change working out the limits again, stored data round-trip, and old stored data still read.
- [ ] Inventor shows the code's fit tolerance on the hole diameter, and an edit opens with the code.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Read tasks 113, 115 and 132 first, and spec 09 questions 17 and 18. Task 088 sets an ISO row with Inventor's fit tolerance; use the same call.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. A 6 mm dowel, Tolerance Custom, code H8: the limits show for 6 mm; OK. Inventor's hole shows H8 on the diameter.
  2. Change the size to 8 mm: the limits follow.
  3. Type `Z9x`: a message under the box, and OK is disabled. Clear the code and type Upper and Lower: it works as before.
  4. A toleranced Simple hole, Custom k6: as step 1.
  5. Edit each hole: it opens on Custom with the code.
  6. In Inventor's own Hole, give a hole a fit tolerance with a code no workbook row has, then edit it in AWB Hole: it opens as Custom with that code.

## Follow-ups
