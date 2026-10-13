# 074: Read the library and apply material and finish in Inventor

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md` ("Add-in side")
- **Status:** done
- **Depends on:** 067, 068, 069, 070, 072
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** 071, 073

## Goal

The Inventor side of M3, without the window: open the material library by path, read its materials, find appearances, read the current material and finish of the target parts, and apply a choice to them. Task 075 wires it to a command and window; 076 uses the reader in Part Properties.

## Scope

- `src/InventorAddin/Extraction/MaterialLibraryReader.cs` (new)
- `src/InventorAddin/Extraction/AppearanceFinder.cs` (new)
- `src/InventorAddin/Commands/MaterialFinishTargets.cs` (new): finding the target parts, and the `IMaterialFinishTarget` implementation
- `src/InventorAddin.Core/Materials/`: any Core rule this needs (for example the header text for the targets, the skip notes), with tests

## Acceptance criteria

- [x] `MaterialLibraryReader.Open(Application, path, warnings)`: `AssetLibraries.Open(path)` (spike 067: about 1 s from the NAS; reopening returns the same library), then each material as a Core `LibraryMaterial` (display name, `CategoryName`, `AppearanceAsset.DisplayName`) through `ComSafe`, since one material's appearance throws. A missing file, or `Open` failing, gives a clear message naming the path and whether it is the starter library or the Settings path; never an unhandled exception. Returns the library object too, for applying
- [x] `AppearanceFinder`: builds 069's candidates from Inventor's loaded libraries (`Application.AssetLibraries`, each `AppearanceAssets`: display name and `Name`), rank 0 the Autodesk Appearance Library (found by `InternalName` `314DE259-5443-4621-BFBD-1730C6CC9AE9`, spike 067; falling back to the display name "Autodesk Appearance Library"), rank 1 the other libraries Inventor loaded, rank 2 the library opened by the add-in. Returns the `Asset` for a Found result. Reads the candidates once per command, not per part
- [x] Targets (Aidan, 2026-10-12): in a part document, the part itself. In an assembly, the selected components (`SelectSet`, 1-based; proxies count): each occurrence's `Definition.Document` when it is a `PartDocument`, once per file even when several occurrences of it are selected. Sub-assemblies, virtual components and parts that cannot be modified are skipped and named in the notes (Default, spec 03 question 15). With no part left, the command says "Select one or more parts in the assembly, then run Material and Finish." (the wording lives in Core)
- [x] Reading current values: each target's material display name (`ActiveMaterial.DisplayName`) and its `Finish` and `Finish Code` iProperties; Core decides the header ("Varies" when they differ)
- [x] `IMaterialFinishTarget.Apply(choice)`, one transaction per Apply, started on the active document (the assembly, in an assembly), named after the command: for each target part, `ActiveMaterial = (Asset)(object)libraryMaterial` (never `CopyTo`, spike 067), then, when the finish has an appearance name and it is Found, `ActiveAppearance = asset`; when it is Missing or Ambiguous, a note says so and the part keeps its material's appearance (setting the material already reset it, spike 067; Default, spec 03 questions 9 and 10). Then the four finish iProperties through `PropertyWriter.Apply`. Any failure aborts the transaction, so nothing is half applied, and the result says which part failed. Nothing is saved (Default: Inventor leaves saving to Aidan)
- [x] The log gets one line per Apply (the targets, material, finish) and one per note
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds; `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 067's Results and Recommendations first. Every Inventor member needed is in its confirmed list; anything else must be listed under "not confirmed".
- `MaterialAsset` does not convert implicitly to `Asset`: cast through `object`.
- A finish whose `AppearanceName` is null (`(no change)` in the file) keeps the material's appearance: check for null before `AppearanceLookup.Find`, which reports a null name as Missing (task 069).
- The add-in folder (where `InventorAddin.dll` runs from) is needed for `LibraryPaths` (task 070); nothing in `AddinServices` exposes it yet, so add it there (for example from `typeof(StandardAddInServer).Assembly.Location`).
- Selection reading: follow `SaveAsReplaceCommand.ReadSelectedOccurrence` (spike 052 and task 060: in-place editing and proxies).
- Transactions: follow `DocumentEditCommand.RunInTransaction` and task 017's per-edit transactions, as Part Properties does. Command type `kNonShapeEditCmdType` unless the existing code suggests otherwise.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (local session, Windows, Inventor 2026 installed). `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, 0 errors, 4 warnings (all CS8600 in files this task did not touch: `CreatePrintCommand`, `SaveAsReplaceCommand`, `SaveAsCopyCommand`, `NewPartCommand`). `dotnet test tests/InventorAddin.Core.Tests`: 2550 passed, 0 failed, 0 skipped (first run, no flaky failures).
- **Added in Core (with tests):**
  - `src/InventorAddin.Core/Materials/MaterialFinishTargeting.cs`: `MaterialFinishSkip`, `MaterialFinishCandidate<T>`, `MaterialFinishTargetPlan<T>` and `MaterialFinishTargeting`. It covers the part and assembly target plans (once per file), the skip notes, and the stop messages, including "Select one or more parts in the assembly, then run Material and Finish.". It also has `AppearanceToFind` (a null or blank appearance name keeps the material's appearance and is not looked up), the notes for a Missing or Ambiguous appearance, `IndexOfMaterial`, the applied and failed messages, and the log lines.
  - `src/InventorAddin.Core/Materials/AppearanceLibraries.cs`: the library ranks. Rank 0 is the Autodesk Appearance Library, found by `InternalName` with a fallback to its display name. Rank 1 is every other loaded library, and rank 2 is the opened library.
  - `src/InventorAddin.Core/Materials/MaterialLibraryMessages.cs`: the messages for a missing library, one that could not be opened, and one with no materials. Each names the path and says whether it is the starter library or the Settings path.
  - Tests: `tests/InventorAddin.Core.Tests/Materials/MaterialFinishTargetingTests.cs`, `AppearanceLibrariesTests.cs`, `MaterialLibraryMessagesTests.cs`.
  - The header uses 072's `MaterialFinishCurrent.ForPart`/`ForParts` unchanged.
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
  - `src/InventorAddin/Extraction/MaterialLibraryReader.cs` (new): `MaterialLibraryReader.Open(Application, LibraryPath, warnings)` returns a `MaterialLibrary` with the materials, the `AssetLibrary`, `FindMaterial(displayName)` and `Error`.
  - `src/InventorAddin/Extraction/AppearanceFinder.cs` (new): `AppearanceFinder.Read(app, openedLibrary, warnings)`, called once per command, and `Find(name, out Asset?)`.
  - `src/InventorAddin/Commands/MaterialFinishTargets.cs` (new): `Find(activeDocument)` returns the plan and `ReadCurrent(activeDocument, plan)` returns the header. `IMaterialFinishTarget.Apply` runs through an `Action<Action>` transaction runner that task 075's command passes as `edit => RunInTransaction(doc, edit)`. `RunInTransaction` is protected on `DocumentEditCommand`, so the targets cannot start the transaction themselves.
  - `src/InventorAddin/AddinServices.cs`: new `AddinFolder` (from `typeof(StandardAddInServer).Assembly.Location`), for `LibraryPaths`.
- **Inventor API members not confirmed:** every member used is in spike 067's confirmed list or already used elsewhere in the repo:
  - `AssetLibraries`, and enumerating `MaterialAssets`/`AppearanceAssets` as `Asset`: `LibraryExtractor`.
  - `SelectSet`, `ComponentOccurrence.Name`/`Suppressed`/`Definition.Document`, `VirtualComponentDefinition` and `Document.IsModifiable`: `AssemblyExtractor`, `SaveAsReplaceCommand` and `PartPropertiesReader`.

  Behaviour not yet confirmed in Inventor:
  - Whether the same single Undo of the assembly transaction also reverts the four iProperty writes on a part reached through an assembly. Spike 067 confirmed this for material and appearance only.
  - What `AssetLibraries.Open` does with a corrupt file or a file that is not a library. The code catches any exception and reports it.
  - Reading `Suppressed` and `Definition.Document` on a `ComponentOccurrenceProxy` taken from the `SelectSet`. This compiles, and `AssemblyExtractor` reads them on sub-occurrences.
- **Manual checklist for Inventor:** covered by task 075's checklist (nothing calls this code until then). Worth adding to it: in an assembly, select two parts and a sub-assembly and apply a material and a finish. Then Undo once and check that the material, the appearance and the four Finish iProperties all revert on both parts.

## Follow-ups

Things noticed but not done.

- Defaults for the parent to log. None of these were decided by Aidan.
  - Parts whose file is read-only are skipped too, as Part Properties does. "Cannot be modified" in spec 03 question 15 was read as covering both read-only files and documents that cannot be modified.
  - Suppressed occurrences are skipped, with a note.
  - Selected things that are not components (faces, sketches) give one note: "Skipped: N selected items that are not components."
  - The Apply message is "Applied to N parts. Save to keep the changes." instead of the view-model's generic text.
- The log line for each Apply names the parts' file names (not full paths), as the acceptance criteria ask, and the library warnings name the library path. `PartPropertiesReader` keeps file names out of the log, so Aidan may want one rule for both.
- Task 075: while a part is edited in place inside an assembly, `MaterialFinishTargets.Find` is given the top-level assembly and uses its selection. 075 should decide whether the part being edited (`ActiveEditDocument`) is the target in that case.
- Task 075: build the `AppearanceFinder` after `MaterialLibraryReader.Open`, so the opened library gets rank 2. When `MaterialLibrary.Error` is set, show it instead of opening the window.
