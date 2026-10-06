# 001: Core test project

- **Milestone:** M0
- **Feature spec:** none (foundations)
- **Status:** todo
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

- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes in a cloud session
- [ ] A `PartData`, an `AssemblyData` and a `DrawingData` each survive `ModelJson.Serialize` then `ModelJson.DeserializeModel` as the same subclass with their fields intact
- [ ] The serialized JSON contains the `$type` discriminator, enums as strings, and omits null properties
- [ ] `BoundingBox` length properties are covered
- [ ] The solution file lists the test project

## Notes for the implementer

- `ModelJson` and the models are in `src/InventorAddin.Core`. Read `ModelData.cs` for the polymorphism attributes.
- If NuGet restore fails in the cloud, report the exact error and stop. Do not work around it by vendoring packages.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** none, this task does not touch the add-in. On Windows, `dotnet build InventorAddin.slnx` should still succeed with the test project added.

## Follow-ups
