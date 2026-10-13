# 071: Starter material library: data file and generator

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md` ("Libraries")
- **Status:** done
- **Depends on:** 068 (its `CsvTable`), 070 (its file name constant)
- **Needs:** windows (local session: the generator drives Inventor)
- **Parallel-safe with:** none

## Goal

The repo ships `libraries/StarterMaterials.adsklib`, a small generic material library made from scratch, generated reproducibly from `libraries/starter-materials.csv` by a console tool, so a plain download of the add-in has materials to pick (Aidan, 2026-10-12).

## Scope

- `libraries/starter-materials.csv` (new): the material data
- `src/InventorAddin.Core/Materials/StarterMaterialsCsv.cs` (new): parse and validate it
- `tools/StarterLibraryBuilder/` (new): a console app, **not** in `InventorAddin.slnx`, that drives an invisible Inventor over COM
- `libraries/StarterMaterials.adsklib` (generated, committed)
- `.gitignore`: `!libraries/StarterMaterials.adsklib` after the `*.adsklib` line, and nothing else let through
- Tests under `tests/InventorAddin.Core.Tests/Materials/`

## Acceptance criteria

- [x] `starter-materials.csv` columns: `Name, Category, BaseMaterial, Density_kg_m3, Appearance`. About 20 common materials (Default, spec 03 question 14), written from standard designations and general engineering knowledge: carbon steels (1018, 1045, A36), alloy steel 4140 annealed and 4140 flame hardened 55-58 HRC, tool steels O1 and D2 hardened, stainless 303, 304 and 316, aluminium 6061-T6, 7075-T6 and 5052-H32, brass C360, bronze C932, copper C110, gray and ductile cast iron, and plastics acetal, nylon 6/6, UHMW-PE and PTFE. Names follow `Family, grade condition` (`Aluminum, 6061-T6`). Categories are Steel, Aluminum, Brass, Bronze, Copper, Iron and Plastic, so the finish list's AppliesTo values match. Densities in kg/m³
- [x] `StarterMaterialsCsv.Parse` reads it through `CsvTable` and reports per-line problems: blank or duplicate Name (ignoring case), blank Category, a density that is not a positive number. Tested with invented rows; a test also loads the repo file and finds no problems, and checks every AppliesTo entry in `libraries/finishes.csv` is a category in it or a material name in it
- [x] `tools/StarterLibraryBuilder` follows spike 067 section 6: start an invisible Inventor; delete and `AssetLibraries.Add` the output file; for each row, assign the stock `BaseMaterial` (a display name in Inventor's stock material library) to a scratch part, `Duplicate` the part's copy with the row's name, set `structural_Density`, set the appearance (found by display name in the Autodesk Appearance Library first), `CopyTo(library, true)`, then `MaterialAssetCategories.Add(category, asset)` or set `Category` when the category exists. Prints one line per material and a summary; exits non-zero if any row failed. Closes the scratch part without saving and quits Inventor
- [x] Run once on Aidan's PC; the generated file is committed. A second run gives a library with the same materials (the file need not be byte-identical; say whether it is)
- [x] The library, opened in a fresh Inventor with `AssetLibraries.Open`, lists every material with its category, appearance and density (the tool can do this with a `--check` argument)
- [x] `git check-ignore libraries/StarterMaterials.adsklib` reports nothing; `git check-ignore -v libraries/other.adsklib` still reports the ignore rule
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` still succeeds

## Notes for the implementer

- **Never open, read or copy from Aidan's library** (`B:\aidan\…`). Names and data come from standard designations. Stock Inventor or Autodesk materials may be used only as `BaseMaterial` templates; the new material gets our own name, category, density and appearance.
- Read spike 067 Results section 6 first. Its scratchpad console app `Spike067` (parent's scratchpad) shows the working calls, including `(Asset)(object)` casts and that the library's display name is its file name.
- The tool's project file can reference the Inventor interop exactly as `src/InventorAddin/InventorAddin.csproj` does, and reference `src/InventorAddin.Core` for the CSV parser. Add `tools/StarterLibraryBuilder/README.md` with the one command to run it.
- If a `BaseMaterial` or appearance name is not found, report it and fail that row; pick names that exist in Inventor 2026 (list them with the tool if needed).

## Verification

Filled in by the implementer.

- **Ran** (local session on Aidan's PC, Inventor 2026.2, 2026-10-12):
  - `dotnet test tests/InventorAddin.Core.Tests`: 2442 passed, 0 failed (25 new in `StarterMaterialsCsvTests`, counting theory cases). No flaky temp-folder failures this time.
  - `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: succeeded, 0 errors; the only warnings are the existing CS8600 ones in four commands, not touched here. The build output now holds `libraries\StarterMaterials.adsklib` (the 070 copy rule picks it up).
  - `dotnet build tools/StarterLibraryBuilder`: 0 warnings, 0 errors.
  - `dotnet run --project tools/StarterLibraryBuilder`: "22 of 22 materials written ... (22 materials, 7 categories, 19 appearances in the library); 0 failed", exit 0. Run four times before the review fixes and once after; each gives the same 22 materials, but **the file is not byte-identical between runs** (different hashes, sizes 979 to 992 KB), so every regeneration is a binary diff.
  - `dotnet run --project tools/StarterLibraryBuilder -- --check` in a fresh Inventor: every material listed with its category, density and appearance; "22 of 22 materials as in the CSV; 0 not in the CSV", no duplicate appearance names, exit 0.
  - `git check-ignore libraries/StarterMaterials.adsklib` prints nothing (exit 1); `git check-ignore -v libraries/other.adsklib` (and `libraries/sub/StarterMaterials.adsklib`) still report `.gitignore:22:*.adsklib`.
  - No Inventor process was left running afterwards.
  - **After review (same day):** the tool now takes templates only from the "Inventor Material Library" whose file is under the public Autodesk documents folder or Program Files (found by name and path, never `ActiveMaterialLibrary`; it stops before touching the output if that library is missing or not unique), searches appearances only in the Autodesk Appearance Library (by internal name `314DE259-...`, falling back to display name plus an installed path) and then that stock library, and starts its `try` straight after `CreateInstance` with a guarded document list in `finally`. Regenerated once with the fixed tool: "Templates from 'Inventor Material Library' (C:\Users\Public\Documents\Autodesk\Inventor 2026\Design Data\Materials\InventorMaterialLibrary.adsklib)", 22 of 22 written, 0 failed (979 KB). `--check`: 22 of 22 as in the CSV, 7 categories, 19 appearances, no duplicates, exit 0. Solution rebuilt (0 errors, the same 4 existing warnings) and Core tests rerun: 2442 passed, 0 failed.
  - **Is the library left registered in Inventor?** No. After the generator runs (`AssetLibraries.Add`) and `--check` runs (`AssetLibraries.Open`), a fresh invisible Inventor started by `--list "^zzz-no-match$"` lists only the four libraries it loads by itself (Autodesk Material Library, Autodesk Appearance Library, Favorites, Inventor Material Library); "StarterMaterials" is not among them. Checked twice, before and after the final regeneration. `Add` and `Open` last only for that Inventor session.
- **What the run found** (worth knowing for task 074 too):
  - `Asset.Duplicate` on a part's material also copies the material's appearance into the part. When that appearance already has the name the row wants (the stock material's own appearance, for example `Aluminum - Flat` on the stock `Aluminum 6061`), the copy is named `Aluminum - Flat(1)`, and setting `MaterialAsset.AppearanceAsset` to the Autodesk Appearance Library's `Aluminum - Flat` still leaves the `(1)` name. The tool renames the library copy's appearance back (`Asset.DisplayName` set on an appearance in the new library works) only when the read name is the wanted name plus `(n)`, then checks the name again. For the 7 rows affected, the stock library's appearance has the same asset name as the Autodesk Appearance Library's (checked with `--list`), so it is the same appearance.
  - `MaterialAsset.AppearanceAsset` can be set straight to an appearance from the Autodesk Appearance Library; it does not need to go through `PartDocument.ActiveAppearance` first as the spike did.
  - A new scratch part per material breaks densities: duplicates of the same base material in different parts share their physical-properties asset name, so `CopyTo(library, true)` overwrote earlier densities (4140 flame hardened, O1 and 7075 read the last row's value). The tool uses one scratch part for all rows, which gives distinct physical assets.
  - The appearance of a material's own copy is named `<material id>:<appearance id>` (for example `MaterialInv_004_physmat_aspects:InvGen-062-1-2`).
- **Choices made here:**
  - 22 materials. Names follow `Family, grade condition`; families used: Steel, Tool Steel, Stainless Steel (category Steel), Aluminum, Brass, Bronze, Copper, Cast Iron (category Iron), and Acetal, Nylon, UHMW-PE, PTFE (category Plastic). Conditions: carbon steels cold drawn or hot rolled (A36), stainless annealed, brass and copper half hard, bronze as cast, gray iron ASTM A48 class 30, ductile 65-45-12. Densities are typical handbook values.
  - Base materials and appearances were picked from `--list` output (stock Inventor Material Library and the Autodesk Appearance Library only; the tool cannot search any other library). Every appearance used is a single appearance in the Autodesk Appearance Library; generic ambiguous names (`Satin`, `Semi-Polished`) were avoided. PTFE uses the stock `Thermoplastic Resin` as its template because the stock library has no PTFE.
  - `StarterMaterialsCsv` also reports a blank `BaseMaterial` (the generator cannot use such a row); a blank `Appearance` is allowed and keeps the base material's appearance. Density is parsed like the finish list's thicknesses (digits and a decimal point, invariant culture) and must be greater than 0.
  - The generator reuses Core's `AppearanceLookup` with only two libraries: the Autodesk Appearance Library (rank 0) and the stock Inventor Material Library (rank 1). Other loaded libraries, which could be private, are never searched; a name in neither, or an ambiguous one, fails the row.
  - Added `--list <regex>` beside `--check` to find stock material and appearance names.
- **Changed under `src/InventorAddin`:** nothing. `tools/StarterLibraryBuilder` (Inventor interop) compiled and ran locally.
- **Inventor API members not confirmed:** none; every member the tool uses compiled and ran. Members beyond spike 067's list: `Application.ActiveMaterialLibrary` (investigation only; the tool no longer uses it), `Application.FileManager.GetTemplateFile`, `Documents.Add` with `CreateVisible` false, `PartDocument.AppearanceAssets` (used only during the investigation), `Asset.DisplayName` set on an appearance in a writable library, `MaterialAsset.AppearanceAsset` set with an appearance-library asset.
- **Manual checklist for Inventor:**
  1. In Inventor, open the Material Browser, open `libraries\StarterMaterials.adsklib` from the repo: the library shows as "StarterMaterials" with 22 materials under Aluminum, Brass, Bronze, Copper, Iron, Plastic and Steel, each with a sensible appearance (no names ending in `(1)`).
  2. Assign `Aluminum, 6061-T6` to a part, open iProperties > Physical: density 2.700 g/cm³ (2700 kg/m³). Assign `Tool Steel, D2 hardened`: 7.700 g/cm³.
  3. Close Inventor, run `dotnet run --project tools/StarterLibraryBuilder -- --check` from the repo root: it ends "22 of 22 materials as in the CSV".

## Follow-ups

Things noticed but not done.

- The library file is about 1 MB and changes in full on every regeneration (not byte-identical). Regenerate only when `starter-materials.csv` changes, so the repo history does not grow with no-op diffs.
- New materials copy every property except density (strength, thermal, and so on) from their stock template, so for example `Tool Steel, D2 hardened` carries the stock `Steel, Alloy` yield strength. Fine for mass and appearance; if Aidan wants stress analysis numbers per grade, the CSV would need more columns.
- Task 074 (finding an appearance by name): the starter library now holds its own copies of 19 appearances with the same names and asset ids as the Autodesk Appearance Library's, which the rank order already handles; worth a test case there with the starter library open.
