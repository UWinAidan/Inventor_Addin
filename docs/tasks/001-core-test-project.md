# 001: Core test project

- **Milestone:** M0
- **Feature spec:** none (foundations)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** none

## Goal

Give Core a test project, so every later task has somewhere to put tests and the cloud workflow is proven end to end on something small.

## Scope

- `tests/InventorAddin.Core.Tests/InventorAddin.Core.Tests.csproj` (new, xUnit, `net8.0`, references `src/InventorAddin.Core`)
- `tests/InventorAddin.Core.Tests/Serialization/ModelJsonTests.cs` (new)
- `tests/fixtures/README.md` (new: fixtures come from Aidan's personal models only, exported with *Export Model Data*)
- `InventorAddin.slnx` (add the test project under a `/tests/` folder)
- `README.md` (add the test command to "Build & run")

Out of scope:

- Any change to `src/`
- CI

## Acceptance criteria

- [x] `dotnet test tests/InventorAddin.Core.Tests` passes in a cloud session
- [x] A `PartData`, an `AssemblyData` and a `DrawingData` each survive `ModelJson.Serialize` then `ModelJson.DeserializeModel` as the same subclass with their fields intact
- [x] The serialized JSON contains the `$type` discriminator, enums as strings, and omits null properties
- [x] `BoundingBox` length properties are covered
- [x] The solution file lists the test project

## Notes for the implementer

- `ModelJson` and the models are in `src/InventorAddin.Core`. Read `ModelData.cs` for the polymorphism attributes.
- If NuGet restore fails in the cloud, report the exact error and stop. Do not work around it by vendoring packages.

## Verification

- **Ran (cloud, Linux, .NET SDK 10.0.112 with the 8.0.31 runtime):**
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: NuGet restore succeeded. 11 passed, 0 failed, 0 skipped. The test project builds with 0 warnings.
  - `dotnet build InventorAddin.slnx` was not run (it cannot build in the cloud).
- **Not compiled (changed under `src/InventorAddin`):** none. Nothing under `src/` was changed.
- **Inventor API members not confirmed:** none. No Inventor API code was written.
- **Manual checklist for Inventor:** none, this task does not touch the add-in. On Windows, `dotnet build InventorAddin.slnx` should still succeed with the test project added.

## Follow-ups

- There is no `global.json`, so the cloud session builds with .NET SDK 10.0.112 (targeting `net8.0`) while Windows may use another SDK. Consider pinning the SDK if builds start to differ.
- `BoundingBox.LengthX/Y/Z` are computed, read-only properties, yet `ModelJson` writes them into every exported JSON file (they are ignored when read back). A test now records that behaviour. If exports should not carry derived values, mark them `[JsonIgnore]` in a separate task.
