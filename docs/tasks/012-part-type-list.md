# 012: Part type list (Core)

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Rules: Part type, Part type default for assemblies)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 011, 013, 014, 015, 017, 018

## Goal

The part type codes, their full names and the default for a document kind exist as data in Core, so the window and later features read one list and codes can be added without touching the window.

## Scope

- `src/InventorAddin.Core/PartProperties/PartTypes.cs` (new)
- `tests/InventorAddin.Core.Tests/PartProperties/PartTypesTests.cs` (new)

Out of scope:

- Anything that uses the type to decide something else (the spec says the type is only stored in this version).
- The view-model (016).

## Acceptance criteria

- [ ] A `PartType` record with `Code` and `FullName`, and a `DisplayText` of the form `"PM: Purchased, modified"` for dropdown entries
- [ ] `PartTypes.All` lists the eight codes from the spec's table in the spec's order, with the spec's full names word for word
- [ ] `PartTypes.Find(string? code)` returns the entry or null. Matching ignores surrounding whitespace and case (`" pm "` finds PM); null and blank return null
- [ ] `PartTypes.DefaultFor(DocumentKind kind)` returns A for `Assembly`, W for `WeldmentAssembly`, and null for every other kind
- [ ] `PartTypes.FullNameFor(string? code)` returns the full name, an empty string for blank, and `"Unknown code"` for a code not in the list (a file can hold any text if someone edited it in Inventor's own dialog)
- [ ] Tests cover every code, lookup normalisation, the defaults for every `DocumentKind` member, blank and unknown codes, and that codes are unique
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- File-scoped namespace `InventorAddin.Core.PartProperties`, implicit usings, nullable on (see `Branding.cs`, `Ribbon/RibbonLayout.cs`).
- `DocumentKind` lives in `InventorAddin.Core.Models` (`Models/ModelData.cs`).
- Keep the list a static read-only array of records. No settings file, no loading from disk.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (covered by 019)

## Follow-ups

Things noticed but not done.
