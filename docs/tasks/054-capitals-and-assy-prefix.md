# 054: Names in capitals, and assemblies start with ASSY_

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11; "File names and the part name"; question 14)
- **Status:** done (compiled locally, not tested in Inventor)
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

- [x] `PartFileName.NormaliseName` also makes the name upper case (invariant culture). So `PartFileName.Build`, `CreationProperties` (Description) and the requests built by the three view-models are upper case. Tested, including a name with a comma and a non-ASCII letter
- [x] A Core rule for the prefix, for example `PartFileName.ForKind(string? typed, DocumentKind kind)` (or an equivalent taking `NewFileKind` where the view-model has one): for an assembly or weldment (`DocumentKind.IsAssembly()` / `NewFileKind.Assembly`), a normalised name that does not start with `ASSY ` gets `ASSY ` put in front. `GEARBOX`, `gearbox`, `ASSY_GEARBOX`, `assy gearbox` and `ASSY  GEARBOX` all give `ASSY GEARBOX`; `ASSYGEARBOX` gives `ASSY ASSYGEARBOX`. Parts and sheet metal parts are never prefixed. Tested
- [x] A name that is only `ASSY` (or `ASSY_`) for an assembly is refused with "Enter a name after ASSY_." Tested
- [x] New Part: the preview, the validation and the request use the prefixed name for an assembly, and follow the Kind when it changes (switching Part to Assembly adds the prefix to the preview, switching back removes it; the name box text itself is not rewritten). `GEARBOX` as an assembly previews `ASSY_GEARBOX,AWB-?????.iam`. Tested
- [x] Save As Copy and Save As and Replace (`SaveAsCopyViewModel`, both constructors): an assembly copy gets the prefix in its preview and request. Tested
- [x] Rename: `RenameViewModel` takes the document's kind; renaming an assembly to `NEW GEARBOX` gives `ASSY_NEW_GEARBOX,{number}.iam`. `RenameService` passes `DocumentKinds.Of(_doc)`. Tested in Core
- [x] The name boxes in `NewPartWindow`, `SaveAsCopyWindow` (used by both copy commands) and `RenameWindow` set `CharacterCasing="Upper"`, so typing shows capitals. Check the XAML rules test (task 031) still passes
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Run after 053, so file names are already name first: `ASSY_GEARBOX,AWB-10006.iam`.
- Description stays equal to the name read from the file name: `ASSY GEARBOX` (Aidan, 2026-10-11).
- Upper-casing in Core, not only in the boxes, keeps pasted text and code paths right; the boxes are for what Aidan sees while typing.
- Keep the prefix text in one constant in Core.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (local session). `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, 0 errors, the same 4 existing CS8600 warnings (CreatePrintCommand, NewPartCommand, SaveAsCopyCommand, SaveAsReplaceCommand; each listed twice, once for the WPF temp project). `dotnet test tests/InventorAddin.Core.Tests`: 1950 passed, 0 failed (baseline 1902; the 72 tests that expected kept case were updated to upper case, and 48 new cases added). `WindowXamlRulesTests` (task 031) passes.
- **What changed in Core:** `PartFileName.NormaliseName` upper-cases (invariant culture). New `PartFileName.AssemblyPrefix` (`"ASSY"`, the one constant), `AssemblyNameMissingMessage` ("Enter a name after ASSY_."), `TakesAssemblyPrefix(DocumentKind)`, `ForKind(string?, DocumentKind)` and `ValidateNameForKind(string?, DocumentKind)`. `NewFileNameCheck.For`, `RenamePlan.NewFileName` and `RenamePlan.ValidateNewName` take an optional `DocumentKind` (default Part). `NewPartViewModel`, `SaveAsCopyViewModel` (both constructors) and `RenameViewModel` (new optional `kind` parameter) use the prefixed name for the preview, the validation and the request. `CreationProperties` needed no change: Description comes from `NormaliseName`, so it is upper case.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** `Commands/RenameService.cs` (passes `ComSafe.Get(() => DocumentKinds.Of(_doc))` to `RenameViewModel`); `UI/NewPartWindow.xaml`, `UI/SaveAsCopyWindow.xaml`, `UI/RenameWindow.xaml` (`CharacterCasing="Upper"` on the name box).
- **Inventor API members not confirmed:** none new. `DocumentKinds.Of` wrapped in `ComSafe.Get` matches `PartPropertiesReader.cs`; `CharacterCasing` is a standard WPF `TextBox` property.
- **Manual checklist for Inventor:**
  1. New Part: type `mounting bracket` in the name box. It shows `MOUNTING BRACKET` as you type, and the preview reads `MOUNTING_BRACKET,AWB-?????.ipt`. Create it; the file is `MOUNTING_BRACKET,AWB-1xxxx.ipt` and Part Properties shows Description `MOUNTING BRACKET`.
  2. New Part: type `gearbox`, then switch File type to Assembly. The preview changes to `ASSY_GEARBOX,AWB-?????.iam` and the box still reads `GEARBOX`. Switch back to Part: the preview drops the prefix. Switch to Assembly again and Create; the file is `ASSY_GEARBOX,AWB-1xxxx.iam` and Description is `ASSY GEARBOX`.
  3. New Part, Assembly: type `ASSY_GEARBOX`. The preview is `ASSY_GEARBOX,...` (no double prefix). Type only `ASSY_`: the footer says "Enter a name after ASSY_." and Create is disabled.
  4. Open the assembly from step 2, Save As Copy: the box is pre-filled `ASSY GEARBOX`; type `new gearbox`, the preview reads `ASSY_NEW_GEARBOX,AWB-?????.iam`. Save; check the copy's file name and Description `ASSY NEW GEARBOX`.
  5. In an assembly, select a sub-assembly occurrence and run Save As and Replace: the preview for a name without the prefix shows `ASSY_` in front.
  6. Open an assembly whose name lacks the prefix, open Part Properties, Rename: type `new gearbox`; the box shows capitals and the preview reads `ASSY_NEW_GEARBOX,{its number}.iam`. Rename and check the file name and Description.
  7. Rename a part: no prefix is added.
  8. Paste lower-case text into any of the three name boxes: the box may show it as pasted (CharacterCasing affects typing), but the preview and the saved file name are upper case.

## Follow-ups

Things noticed but not done.

- **Rename cannot upper-case an old lower-case file.** Renaming `mounting_bracket,AWB-10001.ipt` to `mounting bracket` now builds `MOUNTING_BRACKET,AWB-10001.ipt`, which differs only in case, so `RenamePlan.ValidateNewName` refuses it with `SameFileMessage` (tested by `RenamePlanTests.ValidateNewName_SameFileIsRefused`). Upper-casing such a file needs two renames or a two-step rename in the add-in. A question for Aidan.
- **The creators do not add the prefix themselves.** `NewPartCreator` and `SaveAsCopyCreator` build the file name from the request's name as given; the prefix is applied by the three view-models. Any future caller that builds a request without a view-model must call `PartFileName.ForKind`.
- **Pre-filled text keeps its case in the box.** `CharacterCasing="Upper"` affects typing only, so Rename's box pre-filled from an old lower-case file shows lower case until edited, although the preview is upper case.
- **`ValidateNameForKind` checks the typed name.** For an assembly, a typed Windows-reserved name such as `CON` is refused even though the file would be `ASSY_CON`. Harmless, but strict.
