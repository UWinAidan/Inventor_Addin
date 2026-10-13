# 069: Material list, filters, finish matching and appearance lookup (Core)

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md`
- **Status:** todo
- **Depends on:** 068
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 070

## Goal

The rules the Material and Finish window and Part Properties need, all in Core: the materials of a library, filtering them, which finishes suit a material, whether a material is in the library, and which Inventor appearance a finish's appearance name means.

## Scope

- `src/InventorAddin.Core/Materials/`: `LibraryMaterial.cs`, `MaterialFilter.cs`, `FinishMatching.cs`, `AppearanceLookup.cs`
- `tests/InventorAddin.Core.Tests/Materials/`

Out of scope: reading Inventor (074), the view-model (072).

## Acceptance criteria

- [ ] `LibraryMaterial` record: `DisplayName`, `Category` (empty when the library gives none), `AppearanceName` (null when unreadable). Spike 067: one of Aidan's materials has no category and one an unreadable appearance
- [ ] `MaterialFilter.Categories(materials)`: "All" first, then the distinct non-empty categories, sorted ignoring case. A material with no category shows only under All
- [ ] `MaterialFilter.Apply(materials, category, preferredOnly, preferred, search)`: category "All" or a category (ignoring case); preferred only keeps materials whose display name is in `preferred` (ignoring case); search keeps names containing the text (ignoring case, trimmed; blank means everything). Sorted by display name ignoring case. Tested for each and combined
- [ ] `FinishMatching.Suits(finish, material)`: true when AppliesTo is empty, or an entry equals the material's category or its display name (ignoring case, trimmed). `FinishMatching.For(finishes, material, showPaints, search)`: the suiting finishes, without Kind Paint when `showPaints` is false, filtered by search text on Name or Code, in file order. Tested
- [ ] `MaterialFilter.IsInLibrary(materialName, materials)`: display name equality ignoring case and surrounding spaces (spike 067: a part's copy of a material keeps the library's display name; compare by display name). A blank name counts as not in the library. Tested
- [ ] `AppearanceLookup.Find(name, candidates)`, where each candidate is `(LibraryRank, DisplayName, AssetName)` and a lower rank is searched first. It returns Found (with the asset name and rank), Missing, or Ambiguous (with the count). Matching is by display name ignoring case. The first rank holding any match decides: candidates there with the same asset name are one appearance (spike 067: the same asset appears in two libraries with one `Name`); more than one distinct asset name there is Ambiguous. Tested, including: a name found in rank 0 and also in a later rank as a different asset gives rank 0's (Zinc, spike 067); a name only in a later rank is found there; two distinct assets in the deciding rank are Ambiguous
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 067's Results, sections 1, 2 and 5, first.
- The add-in (task 074) will rank Inventor's Autodesk Appearance Library 0, the other libraries Inventor loads 1, and the material library the add-in opened last, so a finish's appearance comes from Inventor's own appearances before anything in a material library.
- Invent all test data; do not copy names from any library.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.
