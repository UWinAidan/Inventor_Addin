# 054: Names in capitals, and assemblies start with ASSY_

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11; "File names and the part name"; question 14)
- **Status:** todo
- **Depends on:** 053
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Aidan's items 8 and 10 (2026-10-11). Part names are upper case everywhere a name is typed: the name boxes type in capitals, and file names and Description are upper case. An assembly's name always starts with `ASSY_`: the add-in adds it when it is missing and never adds it twice, in every command that names an assembly.

## Scope

- `src/InventorAddin.Core/Numbering/PartFileName.cs` (normalising and validating a typed name; the prefix rule)
- `src/InventorAddin.Core/ViewModels/NewPartViewModel.cs`, `SaveAsCopyViewModel.cs`, `RenameViewModel.cs`, `NewFileNameCheck.cs`
- `src/InventorAddin.Core/Numbering/RenamePlan.cs` (if `ValidateNewName` and `NewFileName` need the kind)
- `src/InventorAddin.Core/Numbering/CreationProperties.cs` (only if Description does not already come out upper case)
- `src/InventorAddin/Commands/RenameService.cs` (pass the document's kind to the view-model)
- `src/InventorAddin/UI/NewPartWindow.xaml`, `SaveAsCopyWindow.xaml`, `RenameWindow.xaml` (the name boxes)
- The matching tests under `tests/InventorAddin.Core.Tests/`

Out of scope:

- Reading names from existing files: `PartName.FromFileName` keeps the case the file has (Default, spec 02), so Part Properties keeps an old lower-case file's Description as its file name has it.
- Rename's refusal of a change of case only (`SameFileMessage`). Leave it, and record a follow-up if upper-casing an old lower-case file by Rename is now refused.

## Acceptance criteria

- [ ] `PartFileName.NormaliseName` also makes the name upper case (invariant culture). So `PartFileName.Build`, `CreationProperties` (Description) and the requests built by the three view-models are upper case. Tested, including a name with a comma and a non-ASCII letter
- [ ] A Core rule for the prefix, for example `PartFileName.ForKind(string? typed, DocumentKind kind)` (or an equivalent taking `NewFileKind` where the view-model has one): for an assembly or weldment (`DocumentKind.IsAssembly()` / `NewFileKind.Assembly`), a normalised name that does not start with `ASSY ` gets `ASSY ` put in front. `GEARBOX`, `gearbox`, `ASSY_GEARBOX`, `assy gearbox` and `ASSY  GEARBOX` all give `ASSY GEARBOX`; `ASSYGEARBOX` gives `ASSY ASSYGEARBOX`. Parts and sheet metal parts are never prefixed. Tested
- [ ] A name that is only `ASSY` (or `ASSY_`) for an assembly is refused with "Enter a name after ASSY_." Tested
- [ ] New Part: the preview, the validation and the request use the prefixed name for an assembly, and follow the Kind when it changes (switching Part to Assembly adds the prefix to the preview, switching back removes it; the name box text itself is not rewritten). `GEARBOX` as an assembly previews `ASSY_GEARBOX,AWB-?????.iam`. Tested
- [ ] Save As Copy and Save As and Replace (`SaveAsCopyViewModel`, both constructors): an assembly copy gets the prefix in its preview and request. Tested
- [ ] Rename: `RenameViewModel` takes the document's kind; renaming an assembly to `NEW GEARBOX` gives `ASSY_NEW_GEARBOX,{number}.iam`. `RenameService` passes `DocumentKinds.Of(_doc)`. Tested in Core
- [ ] The name boxes in `NewPartWindow`, `SaveAsCopyWindow` (used by both copy commands) and `RenameWindow` set `CharacterCasing="Upper"`, so typing shows capitals. Check the XAML rules test (task 031) still passes
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Run after 053, so file names are already name first: `ASSY_GEARBOX,AWB-10006.iam`.
- Description stays equal to the name read from the file name: `ASSY GEARBOX` (Aidan, 2026-10-11).
- Upper-casing in Core, not only in the boxes, keeps pasted text and code paths right; the boxes are for what Aidan sees while typing.
- Keep the prefix text in one constant in Core.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
