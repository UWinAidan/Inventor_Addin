# 03: Material and finish

One dialog to pick a material and a compatible finish from a curated library and apply both to the model.

Reference screenshot (local only): `matterial and finish custom library.png`.

## What exists today

- `LibraryExtractor` lists every material and appearance in the loaded Inventor asset libraries (`LibraryData` in Core).
- `PartExtractor` reads a part's current material and appearance; `FinishReader` reads Inventor finish features.
- Nothing applies a material or finish yet.

## Target dialog: "Select Material and Finish"

Seen in the reference:

- **Header:** active model name, current material, current finish, and a "Code" value for the current finish.
- **Filter row:** a Category dropdown (default "All") and three checkboxes: *Preferred Only*, *Enable Filters*, *Show Paints*. All three are ticked by default.
- **Two lists side by side**, each with its own search box above it: Materials on the left, Finishes on the right. The Finishes list is empty until a material is selected, which suggests finishes are filtered by the chosen material.
- **Buttons:** Apply, OK, Cancel.
- **Status line** along the bottom.

Material names in the reference are plain engineering names, for example an acetal grade, an aluminium alloy with temper, and 3D-print materials.

## Core logic (testable without Inventor)

- The library model: materials (name, category, preferred flag) and finishes (name, code, paint flag, which materials or categories each applies to).
- Loading the library from its source file and validating it.
- Filtering: by category, preferred only, show paints, and search text, with *Enable Filters* switching the filters off as a group.
- Which finishes are valid for the selected material.
- The dialog's view-model: current selection, filtered lists, whether Apply is enabled, the status text.
- The result of a selection: the material to set, the appearance to set, and the iProperty values to write.

## Add-in side

- Read the active document's current material and finish to fill the header.
- Apply the material asset to the part and set the appearance.
- Write the finish and its code where the title block and parts list can read them.
- The WPF window, bound to the Core view-model.

## Open questions

These block planning for this feature.

1. **Where does the library live?** A JSON or spreadsheet file Aidan maintains, or an Inventor material library (`.adsklib`), or both: our file for the curated list and Inventor libraries for the actual assets.
2. **What is the finish "Code"?** A short identifier printed on the drawing, or something else.
3. **How is the finish stored on the part?** Custom iProperties, an Inventor finish feature, or both.
4. **Does a finish change the part's appearance**, for example anodize colours or paint?
5. **In an assembly**, does the command act on the selected components, on the assembly itself, or is it disabled? The reference shows the dialog open with an assembly active.
6. What do *Preferred Only* and *Enable Filters* mean exactly, if Aidan wants them at all?
