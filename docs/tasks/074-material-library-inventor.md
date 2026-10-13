# 074: Read the library and apply material and finish in Inventor

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md` ("Add-in side")
- **Status:** todo
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

- [ ] `MaterialLibraryReader.Open(Application, path, warnings)`: `AssetLibraries.Open(path)` (spike 067: about 1 s from the NAS; reopening returns the same library), then each material as a Core `LibraryMaterial` (display name, `CategoryName`, `AppearanceAsset.DisplayName`) through `ComSafe`, since one material's appearance throws. A missing file, or `Open` failing, gives a clear message naming the path and whether it is the starter library or the Settings path; never an unhandled exception. Returns the library object too, for applying
- [ ] `AppearanceFinder`: builds 069's candidates from Inventor's loaded libraries (`Application.AssetLibraries`, each `AppearanceAssets`: display name and `Name`), rank 0 the Autodesk Appearance Library (found by `InternalName` `314DE259-5443-4621-BFBD-1730C6CC9AE9`, spike 067; falling back to the display name "Autodesk Appearance Library"), rank 1 the other libraries Inventor loaded, rank 2 the library opened by the add-in. Returns the `Asset` for a Found result. Reads the candidates once per command, not per part
- [ ] Targets (Aidan, 2026-10-12): in a part document, the part itself. In an assembly, the selected components (`SelectSet`, 1-based; proxies count): each occurrence's `Definition.Document` when it is a `PartDocument`, once per file even when several occurrences of it are selected. Sub-assemblies, virtual components and parts that cannot be modified are skipped and named in the notes (Default, spec 03 question 15). With no part left, the command says "Select one or more parts in the assembly, then run Material and Finish." (the wording lives in Core)
- [ ] Reading current values: each target's material display name (`ActiveMaterial.DisplayName`) and its `Finish` and `Finish Code` iProperties; Core decides the header ("Varies" when they differ)
- [ ] `IMaterialFinishTarget.Apply(choice)`, one transaction per Apply, started on the active document (the assembly, in an assembly), named after the command: for each target part, `ActiveMaterial = (Asset)(object)libraryMaterial` (never `CopyTo`, spike 067), then, when the finish has an appearance name and it is Found, `ActiveAppearance = asset`; when it is Missing or Ambiguous, a note says so and the part keeps its material's appearance (setting the material already reset it, spike 067; Default, spec 03 questions 9 and 10). Then the four finish iProperties through `PropertyWriter.Apply`. Any failure aborts the transaction, so nothing is half applied, and the result says which part failed. Nothing is saved (Default: Inventor leaves saving to Aidan)
- [ ] The log gets one line per Apply (the targets, material, finish) and one per note
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds; `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 067's Results and Recommendations first. Every Inventor member needed is in its confirmed list; anything else must be listed under "not confirmed".
- `MaterialAsset` does not convert implicitly to `Asset`: cast through `object`.
- A finish whose `AppearanceName` is null (`(no change)` in the file) keeps the material's appearance: check for null before `AppearanceLookup.Find`, which reports a null name as Missing (task 069).
- The add-in folder (where `InventorAddin.dll` runs from) is needed for `LibraryPaths` (task 070); nothing in `AddinServices` exposes it yet, so add it there (for example from `typeof(StandardAddInServer).Assembly.Location`).
- Selection reading: follow `SaveAsReplaceCommand.ReadSelectedOccurrence` (spike 052 and task 060: in-place editing and proxies).
- Transactions: follow `DocumentEditCommand.RunInTransaction` and task 017's per-edit transactions, as Part Properties does. Command type `kNonShapeEditCmdType` unless the existing code suggests otherwise.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** covered by task 075's checklist (nothing calls this code until then)

## Follow-ups

Things noticed but not done.
