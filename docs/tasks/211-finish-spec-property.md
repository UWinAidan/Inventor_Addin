# 211: Finish Spec property, written with the finish

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Finish on the print", open question 23); `docs/features/03-material-finish.md` (finish properties)
- **Status:** todo
- **Depends on:** none
- **Needs:** Core only
- **Parallel-safe with:** 186, 189, 190 (not 185: 185's field list names this property)

## Goal

Applying a finish also writes a ready-made custom text property **Finish Spec** (Aidan, 2026-11-22: the Finish Spec property option of question 23), so the title block's Finish field prints one property and a finish with no thickness has no trailing comma.

## Scope

- `src/InventorAddin.Core/PartProperties/PartPropertyStorage.cs`: `FinishSpec`, user defined, `"Finish Spec"`, text.
- `src/InventorAddin.Core/Materials/FinishProperties.cs`: `For` returns five writes. The new one comes last: Finish Spec is set to `SpecText(finish)`, or removed when there is no finish. Update the summaries ("four" becomes "five"), and `MaterialFinishChoice`'s parameter doc.
- `FinishProperties.SpecText(Finish)`: the finish name, then ", " and `ThicknessText(finish)` when that is not empty. Example: "Anodise Clear, 0.010-0.015 mm". It uses the name and thickness exactly as stored (Default; question 23 stays open on mm versus µm).
- Tests in `tests/InventorAddin.Core.Tests` next to the existing `FinishProperties` tests: with a thickness, with none (no trailing comma), a single value, no finish (Remove), the order of the five writes.
- Fix any existing test that counts four writes, and say so under Verification.

Out of scope: the title block itself (tasks 185 and 187 read the property); filling Finish Spec on parts finished before this task (re-applying the finish fills it); the add-in. It applies `MaterialFinishChoice.PropertyWrites` as a list, so check that nothing there assumes four writes, and list any add-in file you changed as not compiled.

## Acceptance criteria

- [ ] A finish with thickness 0.010 to 0.015 writes Finish Spec "<name>, 0.010-0.015 mm".
- [ ] A finish with both thicknesses 0 writes Finish Spec "<name>" (no comma).
- [ ] No finish removes Finish Spec along with the other four.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Invent test data (finish names such as "Test Coat"); do not copy names from a library.
- Read `DECISIONS.md` rows dated 2026-11-21 and 2026-11-22 about the finish on the print first.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
