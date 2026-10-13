# 069: Material list, filters, finish matching and appearance lookup (Core)

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md`
- **Status:** done
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

- [x] `LibraryMaterial` record: `DisplayName`, `Category` (empty when the library gives none), `AppearanceName` (null when unreadable). Spike 067: one of Aidan's materials has no category and one an unreadable appearance
- [x] `MaterialFilter.Categories(materials)`: "All" first, then the distinct non-empty categories, sorted ignoring case. A material with no category shows only under All
- [x] `MaterialFilter.Apply(materials, category, preferredOnly, preferred, search)`: category "All" or a category (ignoring case); preferred only keeps materials whose display name is in `preferred` (ignoring case); search keeps names containing the text (ignoring case, trimmed; blank means everything). Sorted by display name ignoring case. Tested for each and combined
- [x] `FinishMatching.Suits(finish, material)`: true when AppliesTo is empty, or an entry equals the material's category or its display name (ignoring case, trimmed). `FinishMatching.For(finishes, material, showPaints, search)`: the suiting finishes, without Kind Paint when `showPaints` is false, filtered by search text on Name or Code, in file order. Tested
- [x] `MaterialFilter.IsInLibrary(materialName, materials)`: display name equality ignoring case and surrounding spaces (spike 067: a part's copy of a material keeps the library's display name; compare by display name). A blank name counts as not in the library. Tested
- [x] `AppearanceLookup.Find(name, candidates)`, where each candidate is `(LibraryRank, DisplayName, AssetName)` and a lower rank is searched first. It returns Found (with the asset name and rank), Missing, or Ambiguous (with the count). Matching is by display name ignoring case. The first rank holding any match decides: candidates there with the same asset name are one appearance (spike 067: the same asset appears in two libraries with one `Name`); more than one distinct asset name there is Ambiguous. Tested, including: a name found in rank 0 and also in a later rank as a different asset gives rank 0's (Zinc, spike 067); a name only in a later rank is found there; two distinct assets in the deciding rank are Ambiguous
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 067's Results, sections 1, 2 and 5, first.
- The add-in (task 074) will rank Inventor's Autodesk Appearance Library 0, the other libraries Inventor loads 1, and the material library the add-in opened last, so a finish's appearance comes from Inventor's own appearances before anything in a material library.
- Invent all test data; do not copy names from any library.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows). `dotnet build src/InventorAddin.Core`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 2349 passed, 0 failed, 0 skipped. `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: 0 errors, 8 warnings, none in the new files.
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` changed.
- **Inventor API members not confirmed:** none; no Inventor API used.
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.

- Choices made inside the criteria, for the reviewer: `MaterialFilter.All` is the constant "All"; a library category spelled "All" is not listed twice; a blank category in `Apply` means All; `FinishMatching.For` with no material (null) returns an empty list (the spec's "empty until a material is selected") and does not add "None" (the view-model, task 072, adds it); blank AppliesTo entries match nothing; `AppearanceLookup` compares asset names ignoring case; a blank appearance name is Missing. Types: `AppearanceCandidate(LibraryRank, DisplayName, AssetName)` in, `AppearanceLookupResult(Status, AssetName, LibraryRank, Count)` out.
- Tasks 072 and 074: a finish whose `AppearanceName` is null (`(no change)` in the file, already turned into null by `FinishListCsv`) keeps the material's appearance. Check for null before calling `AppearanceLookup.Find`, which reports a null name as Missing, so it is not reported as a missing appearance.
