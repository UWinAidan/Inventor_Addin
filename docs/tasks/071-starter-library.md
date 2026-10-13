# 071: Starter material library: data file and generator

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md` ("Libraries")
- **Status:** todo
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

- [ ] `starter-materials.csv` columns: `Name, Category, BaseMaterial, Density_kg_m3, Appearance`. About 20 common materials (Default, spec 03 question 14), written from standard designations and general engineering knowledge: carbon steels (1018, 1045, A36), alloy steel 4140 annealed and 4140 flame hardened 55-58 HRC, tool steels O1 and D2 hardened, stainless 303, 304 and 316, aluminium 6061-T6, 7075-T6 and 5052-H32, brass C360, bronze C932, copper C110, gray and ductile cast iron, and plastics acetal, nylon 6/6, UHMW-PE and PTFE. Names follow `Family, grade condition` (`Aluminum, 6061-T6`). Categories are Steel, Aluminum, Brass, Bronze, Copper, Iron and Plastic, so the finish list's AppliesTo values match. Densities in kg/m³
- [ ] `StarterMaterialsCsv.Parse` reads it through `CsvTable` and reports per-line problems: blank or duplicate Name (ignoring case), blank Category, a density that is not a positive number. Tested with invented rows; a test also loads the repo file and finds no problems, and checks every AppliesTo entry in `libraries/finishes.csv` is a category in it or a material name in it
- [ ] `tools/StarterLibraryBuilder` follows spike 067 section 6: start an invisible Inventor; delete and `AssetLibraries.Add` the output file; for each row, assign the stock `BaseMaterial` (a display name in Inventor's stock material library) to a scratch part, `Duplicate` the part's copy with the row's name, set `structural_Density`, set the appearance (found by display name in the Autodesk Appearance Library first), `CopyTo(library, true)`, then `MaterialAssetCategories.Add(category, asset)` or set `Category` when the category exists. Prints one line per material and a summary; exits non-zero if any row failed. Closes the scratch part without saving and quits Inventor
- [ ] Run once on Aidan's PC; the generated file is committed. A second run gives a library with the same materials (the file need not be byte-identical; say whether it is)
- [ ] The library, opened in a fresh Inventor with `AssetLibraries.Open`, lists every material with its category, appearance and density (the tool can do this with a `--check` argument)
- [ ] `git check-ignore libraries/StarterMaterials.adsklib` reports nothing; `git check-ignore -v libraries/other.adsklib` still reports the ignore rule
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` still succeeds

## Notes for the implementer

- **Never open, read or copy from Aidan's library** (`B:\aidan\…`). Names and data come from standard designations. Stock Inventor or Autodesk materials may be used only as `BaseMaterial` templates; the new material gets our own name, category, density and appearance.
- Read spike 067 Results section 6 first. Its scratchpad console app `Spike067` (parent's scratchpad) shows the working calls, including `(Asset)(object)` casts and that the library's display name is its file name.
- The tool's project file can reference the Inventor interop exactly as `src/InventorAddin/InventorAddin.csproj` does, and reference `src/InventorAddin.Core` for the CSV parser. Add `tools/StarterLibraryBuilder/README.md` with the one command to run it.
- If a `BaseMaterial` or appearance name is not found, report it and fail that row; pick names that exist in Inventor 2026 (list them with the tool if needed).

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. In Inventor, open the Material Browser, open `libraries\StarterMaterials.adsklib` from the repo: the materials show under their categories with sensible appearances.

## Follow-ups

Things noticed but not done.
