# 053: File names put the name first and the number last

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11; "File names and the part name"; open question 18)
- **Status:** done (compiled locally, not tested in Inventor)
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

- [x] `PartFileName.Build(number, name, ext)` returns `{NAME_WITH_UNDERSCORES},{number}{ext}`, for example `MOUNTING_BRACKET,AWB-10005.ipt`; with no number it returns `{name}{ext}` as now. Tested
- [x] `PartFileName.TryReadNumber(path, out n)`: true only when the stem contains a comma **and** the text after the last comma parses as a part number (`PartNumber.TryParse`, so surrounding spaces and prefix case are tolerated as they are now). Tested with: `A,AWB-10001.ipt` (true), `A,B,AWB-10001.ipt` (true, name `A,B`), `AWB-10001,A.ipt` (old order: false), `A.ipt` (false), `A,B.ipt` (false), `,AWB-10001.ipt`, and a path with folders containing commas
- [x] `PartName.FromFileName`: when `TryReadNumber` finds a number, the name is the text before the last comma; otherwise the whole stem. Then underscores as spaces, trimmed, case unchanged. Tested with the same inputs, including the old order `AWB-10005,BRACKET.ipt` reading as `AWB-10005,BRACKET` (Default, spec 02)
- [x] The rule lives in one place: `PartName.FromFileName` and `TryReadNumber` share one helper that splits a stem into name and number, so they cannot disagree
- [x] `RenamePlan.NumberToKeep` keeps the registered number first, then the number read from the old file name by the new rule. `NewFileName` builds `{new_name},{number}`. Tested, including renaming an old number-first file whose registered number is passed in
- [x] Question 18's new default: a comma in a name is allowed; for a file that will have **no** number, a name whose text after its last comma is itself a part number is refused with a message that says why (replace `CommaWithoutNumberMessage` and its wording, which talks about "after the first comma"). Names like `A,B` with no number are now accepted. Tested both ways
- [x] The preview reads `{NAME},AWB-?????{ext}` (for example `MOUNTING_BRACKET,AWB-?????.ipt`) in New Part and Save As Copy. Tested
- [x] Save As Copy pre-fills the copy's name from the original's file name without its number (new rule). Tested with `BRACKET,AWB-10005.ipt` giving `BRACKET`
- [x] No code, comment, XAML comment or test under `src/` and `tests/` still describes or builds the number-first order, except tests that check an old-order name is read as having no number
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- This is a local session on Aidan's Windows PC: the whole solution builds. Build it before reporting.
- The decision is Aidan's (2026-10-11). Spec 02 already describes the new rule; follow it.
- `PartFileName.NumberSeparator` stays `,`.
- Mass Update reads names through `PartName.FromFileName`, so it follows automatically; check its tests' data.
- Register rows keep `Name` with spaces; nothing in the register format changes.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` on Aidan's PC: succeeded, 0 errors, the same 4 existing CS8600 warnings (CreatePrintCommand, NewPartCommand, SaveAsCopyCommand, SaveAsReplaceCommand). `dotnet test tests/InventorAddin.Core.Tests`: 1902 passed, 0 failed (baseline 1861).
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** `UI/NewPartWindow.xaml` and `UI/SaveAsCopyWindow.xaml`, XAML comments only. No add-in code changed: the add-in reads names and numbers only through Core.
- **Inventor API members not confirmed:** none; no Inventor API code was touched.
- **What changed in Core:** `PartFileName.Build` writes `{NAME},{number}{ext}`. `PartFileName.SplitStem` (internal) is the one split rule: the number is the text after the last comma when it parses as a part number, the name is everything before it, otherwise no number and the whole stem. `TryReadNumber` and `PartName.FromFileName` both call it. `RenamePlan.CommaWithoutNumberMessage` is replaced by `NameEndsWithNumberMessage`: for a file that will have no number, a name is refused only when the built file name would read back as numbered; other commas (`A,B`) are accepted. The preview (`NewFileNameCheck`) reads `{NAME},AWB-?????{ext}`.
- **Manual checklist for Inventor:**
  1. New Part: open the dialog, type `MOUNTING BRACKET`. The preview reads `MOUNTING_BRACKET,AWB-?????.ipt`. Create it: the file on disk is `MOUNTING_BRACKET,AWB-1xxxx.ipt`, and Part Properties shows the part name `MOUNTING BRACKET` and the Part Number `AWB-1xxxx`.
  2. Save As Copy on that part: the name box is pre-filled with `MOUNTING BRACKET` (no number), and the preview reads `MOUNTING_BRACKET,AWB-?????.ipt`. Save: the copy is `{name},AWB-1yyyy.ipt`.
  3. Rename that part to `BASE PLATE` from Part Properties: the preview and the new file are `BASE_PLATE,AWB-1xxxx.ipt`, with the same number.
  4. Open an old number-first file from the run 2 checks (`AWB-1xxxx,NAME.ipt`): Part Properties shows the part name as `AWB-1xxxx,NAME`. If its number is registered, Rename to `NAME` gives `NAME,AWB-1xxxx.ipt`.
  5. Rename an old file with no number (for example `Old_bracket.ipt`, not registered) to `A,B`: accepted, giving `A,B.ipt`. Rename it to `A,AWB-10009`: refused with the message that the name must not end with a comma and a part number.
  6. Save As and Replace on a component: the preview shows the name first and `AWB-?????` last.

## Follow-ups

Things noticed but not done.

- Spec 02 open question 11 still reads "Everything after the first comma, underscores as spaces". It is superseded by the 2026-10-11 rule in "File names and the part name"; the parent may want to strike it through as answered.
- `NewFileNameCheck.NumberPlaceholder` hard-codes `AWB-?????` rather than building it from `PartNumber.Prefix`/`Separator` (pre-existing, not changed here).
