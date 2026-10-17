# 03: Material and finish

One window to pick a material from the material library and a finish that suits it, and apply both to the part, or to the selected parts of an assembly.

Reference screenshot (local only): `matterial and finish custom library.png`.

## Terms

- **Material**: what the part is made of, including any hardening. Materials come from an Inventor material library file (`.adsklib`). A hardened steel is its own material, for example a 4140 flame hardened to 55-58 HRC.
- **Finish**: a process applied to the surface after the part is made: plating (chrome, nickel, zinc), conversion coatings (black oxide, passivate), anodize, surface treatments such as nitriding, and paint or powder coat. A part has at most one finish. Hardening is not a finish, so a hardened material can still take a finish such as black oxide or chrome.

## What exists today

- `LibraryExtractor` lists every material and appearance in the asset libraries Inventor has loaded (`LibraryData` in Core).
- `PartExtractor` reads a part's current material and appearance; `FinishReader` reads Inventor finish features (not used by this feature).
- Part Properties (spec 07) shows Material and the `Finish` custom property, read-only.
- Nothing applies a material or finish yet.

## Decided

Aidan, 2026-10-06:

- **This window is the way to set material and finish.** It lists only the materials in the library set in Settings, in place of Inventor's own material list. Part Properties shows both read-only and opens this window through a Change button.
- **Finishes are processes**, not appearances, and are not stored in the material library, so the add-in has its own finish list.

Aidan, 2026-10-12 (answers questions 1 to 8 of the earlier list):

- **Material library.** Aidan's own working library is private and is never committed (`*.adsklib` stays gitignored). It lives on the NAS at `B:\aidan\AWB_Design_Data`, and Settings points at it. The repo instead ships a small generic **starter library** under `libraries/`, with common standard materials made from scratch: no names or entries come from Aidan's library. The add-in uses the starter library by default, so a plain download works. Only the starter library file is allowed past `.gitignore`.
- **Hardening is part of the material**, not a finish (question 1).
- **One finish per part.**
- **The finish list is `libraries/finishes.csv`**, Aidan's starter, editable in Excel. A setting can point at another copy, for example on the NAS. Columns are below (question 2).
- **A finish changes the part's appearance** (for example chrome to blue chrome), carries a thickness range, and carries a print note: the finishing rules for the drawing, such as breaking sharp edges before chrome or dimensions applying after finish (question 4).
- **Stored on the part as iProperties** `Finish`, `Finish Code`, `Finish Thickness` and `Finish Note`, so title blocks and parts lists can use them (question 3).
- **Material and finish are chosen together in one window**, laid out like the reference: a Materials list, and a Finishes list filtered by the chosen material.
- **Preferred materials** are marked with a star in the window, and a *Preferred only* filter shows only them (question 5).
- **In an assembly** the command applies to the selected components. With none selected it says to select some (question 6).
- **No *Enable Filters* checkbox** (question 7).
- **Part Properties warns** when a part's material is not from the library, for example in an imported file. Only a warning, never an error (question 8).
- **Print notes reach the drawing in M5**, through the title block or a notes block. M3 only stores them on the part.

## Libraries

**Starter library** (in the repo, `libraries/`): a small set of common standard materials, for example a few carbon, alloy and stainless steels (some hardened), aluminium alloys, brass, bronze, copper, cast iron and common plastics. Each has our own name in the family, grade, condition pattern (`Aluminum, 6061-T6`), a category, a density, and an appearance from Inventor's Autodesk Appearance Library. Spike 067 found that the add-in can create the library through the Inventor API, so it is generated from `libraries/starter-materials.csv` by a console tool in `tools/StarterLibraryBuilder`, and both the data file and the generated `libraries/StarterMaterials.adsklib` are committed (task 071).

**Aidan's library** (private, `B:\aidan\AWB_Design_Data\InventorMaterialLibrary.adsklib`): about 135 materials, described here without its contents:

- Categories include Steel, Aluminum, Copper, Brass, Bronze, Iron, Polymer, Plastic, Ceramic and Misc. The window's Category dropdown lists whatever categories the loaded library has.
- Names follow a family, grade, condition pattern; some have heat treatment in the name, which is now the intended way to say a material is hardened.
- Some entries are not raw stock: purchased components, and reference-only parts with zero density.
- One material has no category and one has an appearance Inventor cannot read; the add-in must cope with both.
- It has the same file name as Inventor's stock library. Spike 067 found that both open side by side.

## The finish list

`libraries/finishes.csv`, one row per finish, with a header row. Kept outside the code so Aidan can change it in Excel without a rebuild.

| Column | Meaning |
|---|---|
| Code | short code for the drawing and parts list, for example `HC`. Unique |
| Name | shown in the list, for example "Hard chrome" |
| Kind | Plating, Conversion, Anodize, Heat treat or Paint. The *Show paints* filter hides Kind Paint |
| AppliesTo | material categories (or material names) the finish suits, separated by `;`, for example `Steel;Iron` |
| Appearance | the Inventor appearance the finish gives the part, by its display name; `(no change)` keeps the material's appearance |
| ThicknessMin_mm, ThicknessMax_mm | the finish thickness range in mm; both 0 when it has none |
| DimensionsAfterFinish | Yes or No: whether drawing dimensions apply after finishing |
| PrintNote | the note for the drawing |

Aidan's first list has 14 finishes. Spike 067 checked its appearance names: only `Zinc` existed as written. On 2026-10-12 the list was changed to the spike's suggested real names, and Aidan will edit the file later if he wants others (question 9).

**Finding an appearance by name.** Inventor's Autodesk Appearance Library is searched first, then the other libraries Inventor loads, then the material library the add-in opened. The first library with a match decides; the same appearance listed in two libraries counts once. A name that belongs to two different appearances there is ambiguous (Default, question 10). This order matters: Aidan's library has its own `Zinc`, different from Inventor's.

## Target window: "Select Material and Finish"

- **Header:** the active part, or the number of selected parts in an assembly; the current material, finish and finish code.
- **Filter row:** a Category dropdown (default "All"), *Preferred only*, and *Show paints*. No *Enable Filters*.
- **Two lists side by side**, each with its own search box above it: Materials on the left, with a star on preferred materials; Finishes on the right. The Finishes list holds only the finishes that suit the selected material, plus "None". It is empty until a material is selected.
- **Buttons:** Apply, OK, Cancel.
- **Status line** along the bottom.

## Core logic (testable without Inventor)

- The material list as read from a library: display name, category, appearance name.
- The finish list model, loading it from its CSV file, and validating it (unique codes, known Kind, numbers, Yes or No).
- Filtering materials by category, preferred only, and search text.
- Which finishes suit the selected material, and the *Show paints* filter.
- The dialog's view-model: current selection, filtered lists, whether Apply is enabled, the status text.
- The result of a selection: the material to set, the appearance to set (or the material's own), and the four iProperty values to write.
- Whether a material name is in the library, for Part Properties' warning.

## Add-in side

- Settings: the material library path and the finish list path, each blank by default, meaning the starter files shipped with the add-in. A clear message when either is missing or unreadable.
- Open the library by path (`AssetLibraries.Open`) and read its materials.
- Find appearances by display name in Inventor's loaded libraries.
- Read the current material and finish of the part, or of each selected part, to fill the header.
- Apply: in one transaction, set the material, then the finish's appearance, then write the four iProperties.
- The WPF window, bound to the Core view-model, and its ribbon button.
- Part Properties: the Change button beside Material and Finish, and the not-from-the-library warning.

## Spike

Spike 067 (2026-10-12) settled the Inventor behaviour: opening a library by path, assigning a material and appearance, Undo, assemblies, finding appearances by name, and creating the starter library. See `docs/tasks/067-spike-material-library.md`.

## Open questions

Questions 1 to 8 of the earlier list were answered by Aidan on 2026-10-12 (see "Decided").

9. **Appearance names in the finish list.** Answered 2026-10-12: the suggested names from spike 067 for now; Aidan edits `finishes.csv` later if he wants others. A finish whose appearance is not found is still listed and applied, and the part keeps its material's appearance, with a line in the status and the log (Default).
10. **Ambiguous appearance names.** Some display names belong to several different appearances (`Yellow`, `Gray`, `Black`, `Satin`). Default: the finish list check reports such a row and the finish keeps the material's appearance, until the row names a unique appearance.
11. **Where preferred materials are recorded.** Default: clicking the star adds or removes the material in a list in Settings (per PC), saved straight away.
12. **The *Show paints* checkbox** was in the reference; Aidan did not mention it. Default: kept, ticked by default (paints shown).
13. **Finish Thickness text.** Default: `0.013-0.025 mm`, one value when min equals max, blank when both are 0. `Finish Note` holds the print note as written; `DimensionsAfterFinish` is not stored separately because the note already says it.
14. **Which materials go in the starter library**, and their names. Default: about 20 common materials as listed under "Libraries", with categories matching the finish list's AppliesTo values; Aidan can edit the data file.
15. **Selected components that are not parts** (sub-assemblies) in an assembly. Default: they are skipped and named in the status line; the command applies to the selected parts only.
16. **Passivate** applies to stainless steel only, but its AppliesTo is `Steel`, which also lets carbon steels take it. Default: as written, until Aidan adds a stainless category or names the materials.
17. **The ribbon button.** Default: "Material and Finish", small, in the Properties panel after Mass Update, on the Part and Assembly ribbons (task 075).
18. **A part edited in place inside an assembly.** Default (task 075): the part being edited is the target, as Part Properties does, whatever is selected; the transaction is still started on the assembly open in the window, so one Undo reverts it. While a sub-assembly is edited in place, the selection of the top-level assembly is used.
