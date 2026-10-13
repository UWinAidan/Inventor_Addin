# 067: Spike: material libraries, materials and appearances (Windows)

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md`
- **Status:** done
- **Depends on:** none
- **Needs:** windows (local session with Aidan)
- **Parallel-safe with:** none

## Goal

Find, in Inventor 2026, how the add-in can open a material library by path, assign a material and an appearance, find appearances by name, and build the starter library the repo ships. M3's tasks build on the answers.

## Questions

1. Can an `.adsklib` be opened by path when it is not part of the active project, and how long does it take from the NAS? Can Aidan's library, which has the same file name as Inventor's stock library, be open beside it?
2. Does a library material have to be copied into the part before it is assigned?
3. How is the part's appearance overridden, and what happens to the override when the material changes?
4. Does one transaction and one Undo cover the material and the appearance, also for a part changed through an assembly?
5. Does every Appearance name in `libraries/finishes.csv` exist in Inventor, and are appearance names unique?
6. Can the add-in create a new `.adsklib` and fill it with materials of its own (name, category, density, appearance), so the starter library can be generated rather than built by hand?

## Results

Run on Aidan's PC, 2026-10-12, Inventor 2026.2, with a throwaway COM console app (scratchpad `Spike067`) driving its own invisible Inventor. Nothing from Aidan's library is recorded here except counts.

### 1. Opening a library by path

- Inventor loads four libraries by itself: Autodesk Material Library and Autodesk Appearance Library (read-only, under `Common Files\Autodesk Shared\Materials`), Favorites, and the stock Inventor Material Library (`C:\Users\Public\Documents\Autodesk\Inventor 2026\Design Data\Materials\InventorMaterialLibrary.adsklib`, 74 materials), which is `Application.ActiveMaterialLibrary`.
- **`Application.AssetLibraries.Open(path)` opens Aidan's library from `B:\aidan\AWB_Design_Data\InventorMaterialLibrary.adsklib` in 0.9 to 1.1 s**, beside the stock library of the same file name. The two get different `InternalName`s; the opened one's `DisplayName` is its file name without extension (`InventorMaterialLibrary`), the stock one's is "Inventor Material Library". Not read-only.
- Listing its 135 materials with category and appearance takes about 0.3 s. One material has an empty category, and reading one material's `AppearanceAsset` throws (E_FAIL), so both must go through `ComSafe`.
- `Open` on a path already open returns the same library. `MaterialAssets[displayName]` and `MaterialAssets[name]` both find a material.

### 2 and 3. Assigning a material and an appearance

- **`PartDocument.ActiveMaterial = libraryAsset` works directly**: Inventor copies the material (and its appearance) into the part. `Asset.CopyTo(document)` is not needed, and on a material already in the part it returned an object with an empty name that `ActiveMaterial` then refused (E_INVALIDARG). Do not use `CopyTo` for parts.
- The copy in the part has `Parent` the part and `Name` like `1:<display name>`, not the library's `Name` (a GUID). **Compare materials by `DisplayName`.**
- **`PartDocument.ActiveAppearance = appearanceAsset`** (straight from the appearance library) overrides the part's appearance; `AppearanceSourceType` becomes `kOverrideAppearance`.
- **Setting `ActiveMaterial`, even to the material the part already has, clears the override**: `AppearanceSourceType` goes back to `kMaterialAppearance` and the appearance becomes the material's. So the add-in sets the material first, then the finish's appearance. Setting `AppearanceSourceType = kMaterialAppearance` also returns to the material's appearance.
- Mass follows the material straight away (`MassProperties.Mass`). Material and appearance survive save and reopen.

### 4. Transactions and assemblies

- Material and appearance set inside one `TransactionManager.StartTransaction` on the part: one `UndoTransaction` reverts both.
- In an assembly, the part reached through `ComponentOccurrence.Definition.Document` can be given a material and appearance inside a transaction started on the **assembly**; one Undo reverts both. Afterwards the part is dirty and the assembly is not. The occurrence's own appearance stays `kPartAppearance` (the add-in does not touch occurrence appearances).

### 5. Appearance names in `libraries/finishes.csv`

The loaded libraries hold 1578 appearances. **Only one of the 14 Appearance entries exists as written: `Zinc`.** `(no change)` (PASS) is not an appearance; it reads as "keep the material's appearance". Closest real names found:

| Code | In the list | Missing? | Closest real appearances |
|---|---|---|---|
| BO | Black oxide (check name) | yes | none named oxide; `Iron Black`, `Gunmetal` (Metal) |
| HC, FC | Chrome - Polished (blue chrome) | yes | **`Chrome - Polished Blue`**, `Chrome - Polished` |
| ENP | Nickel - Satin (check name) | yes | **`Nickel`** (Metal) |
| ZN | Zinc (check name) | found | `Zinc` |
| AN2C | Aluminum - Anodized (check name) | yes | `Anodized - Light Gray`, `Anodized - Gray`, `Anodized - White` |
| AN2B | Aluminum - Anodized Black (check name) | yes | **`Anodized - Black`** |
| AN3 | Aluminum - Anodized Dark Grey (check name) | yes | **`Anodized - Dark Gray`** |
| NIT | Black oxide (check name) | yes | as BO |
| PASS | (no change) | not an appearance | keep the material's |
| PC-BLK | Powder Coat - Black (check name) | yes | none named powder; `Satin - Black`, `Flaked Satin - Black` (Metallic Paint), `Fine Textured - Black` (Plastic) |
| PT-BLK | Paint - Enamel Glossy (Black) (check name) | yes | `Glossy - Black` (Metallic Paint) |
| PT-YEL | Paint - Enamel Glossy (Yellow) (check name) | yes | `Smooth - Yellow` (Plastic); `Yellow` is ambiguous (see below) |
| PT-GRY | Paint - Enamel Glossy (Grey) (check name) | yes | `Smooth - Medium Gray` (Plastic); `Gray` is ambiguous |

**Appearance display names are not unique.** Most repeats are the same asset in two libraries (same `Name`, for example `Chrome - Polished Blue` is `InvGen-060` in both the Autodesk Appearance Library and the Inventor Material Library). But some names belong to different appearances: `Yellow` (Wall Paint: Matte, Wall Paint: Glossy, and one in Aidan's library), `Gray`, `Black` (nine), `Satin` (aluminium and steel), `Dark Gray`. Every suggested name in bold above is a single appearance.

### 6. Creating a library

- **`AssetLibraries.Add(path)` creates a new, writable `.adsklib`** and Inventor writes the file straight away (about 50 KB with one material). Its `DisplayName` is the file name and cannot be set (E_FAIL), so the file name is the name Aidan sees.
- A material of our own: assign a stock base material (for example the stock `Steel`) to a scratch part, `Duplicate("new name")` the part's copy, set `DisplayName`, set the density through `PhysicalPropertiesAsset` value `structural_Density` (`FloatAssetValue`, **kg/m³**: the stock Steel reads 7850), set `MaterialAsset.AppearanceAsset` to an appearance already in the part, then `CopyTo(library, true)`. The library copy has no category until `library.MaterialAssetCategories.Add("Steel", asset)`; `Asset.Category = ` an existing category also works.
- Reopened in a fresh Inventor: name, category, appearance and density all as set, and it assigns to a part.

Members confirmed (compile and run): `Application.AssetLibraries`, `AssetLibraries.Open`, `AssetLibraries.Add`, `AssetLibrary.DisplayName`/`InternalName`/`FullFileName`/`IsReadOnly`/`MaterialAssets`/`AppearanceAssets`/`MaterialAssetCategories`, `AssetsEnumerator.Count` and `[name]`, `Asset.DisplayName` (get, set on a document copy), `Asset.Name`, `Asset.CategoryName`, `Asset.Category` (set), `Asset.Parent`, `Asset.Duplicate`, `Asset.CopyTo(library, true)`, `MaterialAsset.AppearanceAsset` (get, set), `MaterialAsset.PhysicalPropertiesAsset`, `AssetValue.Name`, `FloatAssetValue.Value` (get, set), `AssetCategories.Add(name, asset)`, `PartDocument.ActiveMaterial` (get, set with a library asset), `PartDocument.ActiveAppearance` (get, set with a library asset), `PartDocument.AppearanceSourceType` (get, set), `AppearanceSourceTypeEnum.kMaterialAppearance`/`kOverrideAppearance`, `ComponentOccurrence.AppearanceSourceType`, `TransactionManager.UndoTransaction`.

Note: `MaterialAsset` does not convert implicitly to `Asset` in C#; cast through `object`: `doc.ActiveMaterial = (Asset)(object)material`.

## Recommendations

- **Opening the library:** `AssetLibraries.Open(path)` once per command (it returns the open library if it is already open). Read materials into Core as display name, category and appearance name, through `ComSafe`. About 1 s from the NAS, so open it when the window opens, not at Inventor start; Part Properties' "not from the library" warning can use the same read.
- **Applying:** in one transaction, set `ActiveMaterial` to the library asset, then either `ActiveAppearance` to the finish's appearance, or nothing when the finish keeps the material's appearance (setting the material already reset it). Then write the four finish iProperties. Never `CopyTo` a part. In an assembly, do this for each selected part inside one transaction on the assembly; leave saving to Aidan, as Inventor does.
- **Finding an appearance by name:** search every loaded library by `DisplayName`, ignoring case. Treat matches with the same `Name` as one appearance. When the name belongs to more than one appearance, the finish list is invalid at that row until Aidan picks a unique name (Default, see spec 03).
- **Starter library:** generate it with a developer command from a materials data file in `libraries/`, using stock base materials only as templates, our own names, densities and categories, and appearances from the Autodesk Appearance Library. The generated `.adsklib` is committed; the data file keeps it reproducible and reviewable.
