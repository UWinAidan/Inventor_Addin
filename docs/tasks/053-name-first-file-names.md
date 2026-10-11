# 053: File names put the name first and the number last

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11; "File names and the part name"; open question 18)
- **Status:** todo
- **Depends on:** none
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none (run one at a time)

## Goal

File names become `NAME,AWB-10005.ipt` instead of `AWB-10005,NAME.ipt` (Aidan, 2026-10-11, item 7). The number is the text after the **last** comma; the name is everything before it. Every rule, parser, preview and test that assumes number first changes with it.

## Scope

- `src/InventorAddin.Core/Numbering/PartFileName.cs` (`Build`, `TryReadNumber`, doc comments)
- `src/InventorAddin.Core/PartProperties/PartName.cs` (`FromFileName`)
- `src/InventorAddin.Core/Numbering/RenamePlan.cs` (`NumberToKeep`, `NewFileName`, `ValidateNewName`, `CommaWithoutNumberMessage`, doc comments)
- `src/InventorAddin.Core/ViewModels/NewFileNameCheck.cs` (preview), and any other preview text building `AWB-?????`
- Doc comments and XAML comments that show the old order: `NewPartViewModel.cs`, `SaveAsCopyViewModel.cs`, `RenameViewModel.cs`, `NewPartCreator.cs`, `src/InventorAddin/UI/NewPartWindow.xaml`, `SaveAsCopyWindow.xaml`, and any other file a search for the old order (`AWB-?????,`, `AWB-10001,`, "after the first comma", "number, comma, name") finds under `src/`
- The matching tests under `tests/InventorAddin.Core.Tests/` (`PartFileNameTests`, `PartNameTests`, `NewFileNameCheckTests`, `NewPartViewModelTests`, `SaveAsCopyViewModelTests`, rename and Mass Update tests, and any test with a number-first file name in its data)

Out of scope:

- Upper case and the `ASSY_` prefix (task 054).
- Renaming files already on disk. Old number-first files are read by the new rule, as the spec says.

## Acceptance criteria

- [ ] `PartFileName.Build(number, name, ext)` returns `{NAME_WITH_UNDERSCORES},{number}{ext}`, for example `MOUNTING_BRACKET,AWB-10005.ipt`; with no number it returns `{name}{ext}` as now. Tested
- [ ] `PartFileName.TryReadNumber(path, out n)`: true only when the stem contains a comma **and** the text after the last comma parses as a part number (`PartNumber.TryParse`, so surrounding spaces and prefix case are tolerated as they are now). Tested with: `A,AWB-10001.ipt` (true), `A,B,AWB-10001.ipt` (true, name `A,B`), `AWB-10001,A.ipt` (old order: false), `A.ipt` (false), `A,B.ipt` (false), `,AWB-10001.ipt`, and a path with folders containing commas
- [ ] `PartName.FromFileName`: when `TryReadNumber` finds a number, the name is the text before the last comma; otherwise the whole stem. Then underscores as spaces, trimmed, case unchanged. Tested with the same inputs, including the old order `AWB-10005,BRACKET.ipt` reading as `AWB-10005,BRACKET` (Default, spec 02)
- [ ] The rule lives in one place: `PartName.FromFileName` and `TryReadNumber` share one helper that splits a stem into name and number, so they cannot disagree
- [ ] `RenamePlan.NumberToKeep` keeps the registered number first, then the number read from the old file name by the new rule. `NewFileName` builds `{new_name},{number}`. Tested, including renaming an old number-first file whose registered number is passed in
- [ ] Question 18's new default: a comma in a name is allowed; for a file that will have **no** number, a name whose text after its last comma is itself a part number is refused with a message that says why (replace `CommaWithoutNumberMessage` and its wording, which talks about "after the first comma"). Names like `A,B` with no number are now accepted. Tested both ways
- [ ] The preview reads `{NAME},AWB-?????{ext}` (for example `MOUNTING_BRACKET,AWB-?????.ipt`) in New Part and Save As Copy. Tested
- [ ] Save As Copy pre-fills the copy's name from the original's file name without its number (new rule). Tested with `BRACKET,AWB-10005.ipt` giving `BRACKET`
- [ ] No code, comment, XAML comment or test under `src/` and `tests/` still describes or builds the number-first order, except tests that check an old-order name is read as having no number
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- This is a local session on Aidan's Windows PC: the whole solution builds. Build it before reporting.
- The decision is Aidan's (2026-10-11). Spec 02 already describes the new rule; follow it.
- `PartFileName.NumberSeparator` stays `,`.
- Mass Update reads names through `PartName.FromFileName`, so it follows automatically; check its tests' data.
- Register rows keep `Name` with spaces; nothing in the register format changes.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
