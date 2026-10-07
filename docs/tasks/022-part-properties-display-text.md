# 022: Part Properties display text (Core)

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (The three windows: Part Properties; Part number display; Core logic)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 021, 023, 024, 025

## Goal

The Part Properties view-model offers every piece of text the restyled window shows, ready to bind: the header title and subline, the part number with the "Not assigned" rule, and "Not set" for a blank material or finish, each with a flag the window uses to show placeholder colour.

## Scope

- `src/InventorAddin.Core/PartProperties/PartNumberDisplay.cs` (new)
- `src/InventorAddin.Core/PartProperties/DocumentKindNames.cs` (new)
- `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs` (new properties only)
- `tests/InventorAddin.Core.Tests/PartProperties/PartNumberDisplayTests.cs` (new)
- `tests/InventorAddin.Core.Tests/PartProperties/DocumentKindNamesTests.cs` (new)
- `tests/InventorAddin.Core.Tests/ViewModels/PartPropertiesViewModelTests.cs` (additions)

Out of scope:

- The window (029). It keeps binding to the existing properties until then, so do not remove or rename `FileName`, `PartNumber`, `PartName`, `Material`, `Finish` or `Weight`.
- Any change to what is written. The part number rule is display only.

## Acceptance criteria

Part number rule (spec "Part number display", decided by Aidan)

- [ ] `PartNumberDisplay.NotAssignedText` is `"Not assigned"`
- [ ] `PartNumberDisplay.IsAssigned(string? partNumber, string? fullFileName)` is false when the part number is null or blank, or when it equals `PartName.FromFileName(fullFileName)` ignoring case and surrounding spaces on both sides; true otherwise
- [ ] For a never-saved file (`fullFileName` blank) only a blank part number counts as not assigned (see Notes)
- [ ] Tests: `Bracket` on `C:\Work\Bracket.ipt` (not assigned), ` bracket ` on the same file (not assigned), `BRACKET` (not assigned), `PN-1001` on the same file (assigned), `Bracket.ipt` on the same file (assigned: it differs from the name without extension), blank and null (not assigned), a never-saved file with `Part1` (assigned) and with blank (not assigned), a file name with two dots `Bracket.v2.ipt` and part number `Bracket.v2` (not assigned). Invent all names; none from real work

Kind names

- [ ] `DocumentKindNames.For(DocumentKind kind)` returns `Part`, `Sheet metal part`, `Assembly`, `Weldment`, `Drawing`, `Presentation`, and `Document` for `Unknown` or an undefined value

View-model (new read-only properties, fixed at construction)

- [ ] `HeaderTitle`: `PartName` when the file has been saved; for a never-saved file, `"New "` + the kind name in lower case (`New part`, `New sheet metal part`, `New assembly`, `New weldment`)
- [ ] `HeaderSubline`: `"{kind} · {file name with extension}"`, for example `Assembly · Gearbox.iam`; for a never-saved file `"{kind} · Not saved yet"`. The separator is a space, U+00B7 middle dot, space
- [ ] `PartNumberDisplay` (string) and `IsPartNumberAssigned` (bool): the number trimmed when assigned, `Not assigned` when not
- [ ] `MaterialDisplay` and `IsMaterialSet`, `FinishDisplay` and `IsFinishSet`: the trimmed value, or `"Not set"` (a public constant `NotSetText`) when blank
- [ ] Tests for each property, including a part, a sheet metal part, an assembly, a weldment, a never-saved part and a never-saved assembly
- [ ] Existing view-model tests pass unchanged
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- `PartNumberDisplay` is a static class in `InventorAddin.Core.PartProperties`, next to `PartName`, and reuses `PartName.FromFileName` so both rules agree on what the file name is.
- The never-saved title and subline, and treating a never-saved file's non-blank part number as assigned, are defaults logged in `DECISIONS.md` and asked as open questions in spec 08. What Inventor reports as the Part Number of a never-saved part is not known yet; task 029's checklist records it.
- Spec 02 will replace the part number rule when numbering exists. Say so in the class's doc comment.
- Follow the doc-comment and test style of `PartNameTests` and `PartPropertiesViewModelTests`.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (covered by 029)

## Follow-ups

Things noticed but not done.
