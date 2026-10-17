# StarterLibraryBuilder

Builds `libraries/StarterMaterials.adsklib` from `libraries/starter-materials.csv` (task 071). It starts its own
invisible Inventor over COM, so it needs Inventor 2026 installed. It is not part of `InventorAddin.slnx`.

From the repo root:

```
dotnet run --project tools/StarterLibraryBuilder
```

It deletes and recreates the library, prints one line per material and a summary, and exits non-zero if the CSV has
problems or any material failed. Commit the regenerated `.adsklib` with the CSV change. The file is not byte-identical
between runs, so expect a binary diff every time.

Other arguments:

- `--check`: open the generated library in a fresh Inventor and compare every material's category, density and
  appearance with the CSV; also fails on two appearances with the same name.
- `--list <regex>`: list the libraries Inventor has loaded (names and files), then the stock materials (usable as
  `BaseMaterial`) and the searchable appearances whose names match.
- `--csv <file>`, `--out <file>`: other input or output paths.

The tool reads only Inventor's own libraries. Template materials come from the "Inventor Material Library" whose file
is under the public Autodesk documents folder or Program Files, found by name and path, never from
`ActiveMaterialLibrary`, which follows the active project and can be a private library with the same file name. If
that library is not found, the tool stops before writing anything.

## The CSV

`Name, Category, BaseMaterial, Density_kg_m3, Appearance`

- **Name**: `Family, grade condition`, for example `Aluminum, 6061-T6`.
- **Category**: one of the categories the finish list's `AppliesTo` uses (Steel, Aluminum, Brass, Bronze, Copper,
  Iron) or Plastic.
- **BaseMaterial**: a material in Inventor's stock material library (the Inventor Material Library), used only as a template for the properties the
  CSV does not set. The new material gets its own name, category, density and appearance.
- **Density_kg_m3**: kg/m³, as Inventor stores it.
- **Appearance**: a display name, searched in the Autodesk Appearance Library first, then the stock Inventor Material
  Library, and nowhere else; a name in neither fails the row. It must name one appearance; `--list` shows the
  candidates. Blank keeps the base material's.

Never copy names or data from a private or company library into this file.
