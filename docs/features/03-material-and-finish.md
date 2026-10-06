# 03: Material and finish

One dialog to pick a material from Aidan's material library and a compatible finish, and apply both to the model.

Reference screenshot (local only): `matterial and finish custom library.png`.

## Terms

- **Material**: what the part is made of. Materials come from an Inventor material library file (`.adsklib`).
- **Finish**: a process applied to the part after it is made. Plating (chrome, nickel, zinc), coatings (anodize, black oxide, paint, powder coat) and heat treatment (through hardening, case hardening, nitriding) are all finishes. A finish is not an Inventor appearance, although applying one may change the appearance.

## What exists today

- `LibraryExtractor` lists every material and appearance in the asset libraries Inventor has loaded (`LibraryData` in Core).
- `PartExtractor` reads a part's current material and appearance; `FinishReader` reads Inventor finish features.
- Nothing applies a material or finish yet.

## Decided

- **Materials come from a custom `.adsklib` library that Aidan supplies.** The add-in does not ship a library and the file is never committed to this repository (`*.adsklib` is gitignored). Its location is a setting.
- **Finishes are processes**, as defined above. They are not stored in the material library, so the add-in needs its own finish list.
- **This window is the way to set material and finish.** It lists only the materials in Aidan's library, in place of choosing from Inventor's own material list. The part properties window (spec 07) shows both values read-only and opens this window through a Change button.
- **Aidan will create the finish list himself, later.** Until it exists the Finishes side of the window has nothing to offer, so this milestone should not be planned before the list is ready or its format is agreed.

## What the library looks like

Read from the file Aidan supplied, described here without its contents:

- About 140 materials. Each has a name, a category and a linked appearance.
- Categories include Steel, Aluminum, Copper, Brass, Bronze, Iron, Polymer, Plastic, Ceramic and Misc. The dialog's Category dropdown should list whatever categories the loaded library has.
- Names follow a family, grade, condition pattern, such as `Aluminum, 6061-T6`.
- Some entries are not raw stock: there are entries for purchased components and for reference-only parts with zero density.
- **Some materials have heat treatment built into the name**, a tool steel at a given hardness for example. That overlaps with hardening as a finish. See open question 1.
- There is no "preferred" flag in the library. One material has "(preferred)" typed into its name. See open question 5.
- The file has the same name as Inventor's stock material library, so it may be meant to replace the stock file rather than load beside it. The Windows spike below should check.

## Target dialog: "Select Material and Finish"

Seen in the reference:

- **Header:** active model name, current material, current finish, and a "Code" value for the current finish.
- **Filter row:** a Category dropdown (default "All") and three checkboxes: *Preferred Only*, *Enable Filters*, *Show Paints*. All three are ticked by default.
- **Two lists side by side**, each with its own search box above it: Materials on the left, Finishes on the right. The Finishes list is empty until a material is selected, so finishes are filtered by the chosen material.
- **Buttons:** Apply, OK, Cancel.
- **Status line** along the bottom.

## The finish list

A data file Aidan maintains, kept outside the code so it can change without a rebuild. Proposed fields for each finish:

| Field | Purpose |
|---|---|
| Name | shown in the list, for example "Hard chrome" |
| Code | short text for the drawing and parts list |
| Kind | plating, coating, heat treatment or paint. *Show Paints* filters on this |
| Applies to | the material categories, or specific materials, the finish is valid for |
| Appearance | optional: an appearance to apply to the model with the finish |
| Note | optional: text for the drawing, such as hardness range or thickness |

Test data for this feature must be invented. Do not copy names from Aidan's library into tests or fixtures.

## Core logic (testable without Inventor)

- The material list as read from a library: name, category, appearance name.
- The finish list model, loading it from its file, and validating it.
- Filtering materials: by category, preferred only, and search text, with *Enable Filters* switching the filters off as a group.
- Which finishes are valid for the selected material, and the *Show Paints* filter.
- The dialog's view-model: current selection, filtered lists, whether Apply is enabled, the status text.
- The result of a selection: the material to set, the appearance to set, and the iProperty values to write.

## Add-in side

- A setting for the library file location and one for the finish list location, with a clear message when either is missing.
- Read the materials from that library.
- Read the active document's current material and finish to fill the header.
- Apply the material to the part and set the appearance.
- Write the finish and its code where the title block and parts list can read them.
- The WPF window, bound to the Core view-model.

## Spike to run on Windows before planning the add-in side

No code in the repo opens a library by file path or assigns a material, so these need a short experiment in Inventor:

1. Open a `.adsklib` by path and list its materials, when the library is not part of the active project.
2. Whether a library with the stock library's file name can be loaded while the stock one is loaded.
3. Assign a material from the library to a part, including copying it into the document first if Inventor requires that.
4. Override the part's appearance.

## Open questions

These block planning for this feature.

1. **Hardening: finish or material?** The library already has hardened variants as separate materials. Either keep picking those as materials, or pick the base material and add hardening as a finish. Doing both gives two ways to say the same thing.
2. **What finishes go in the first list?** Names, codes, and which materials each applies to. Aidan will write it; nothing in the library provides it.
3. **How is the finish stored on the part?** Custom iProperties, an Inventor finish feature, or both.
4. **Does a finish change the part's appearance**, for example anodize colours or paint?
5. **What does "preferred" mean**, and where is it recorded: a list in settings, or a marker in the material name?
6. **In an assembly**, does the command act on the selected components, on the assembly itself, or is it disabled? The reference shows the dialog open with an assembly active.
7. Is *Enable Filters* wanted at all?
8. **Inventor's own material list still exists.** The add-in cannot remove it. Is it enough that our window is the route Aidan uses, or should the add-in also warn when a part's material is not from his library?
