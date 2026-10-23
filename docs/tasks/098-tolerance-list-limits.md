# 098: Tolerance list shows the limits for the size

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Dowel and precision holes")
- **Status:** done
- **Depends on:** 090
- **Needs:** windows build (Core view-model plus XAML)
- **Parallel-safe with:** 093, 094, 095, 096

## Goal

Aidan, 090 check: each entry of the Tolerance list shows the actual tolerance for the chosen size, for example `H7  +0.012 / 0`, with the Precision Tol row's name only as secondary text.

## Scope

- Core: `AwbHoleViewModel` (a tolerance option item with its main and secondary text, rebuilt when size, units or fit change), using `Iso286` for ISO rows; tests
- `src/InventorAddin/UI/AwbHoleWindow.xaml` (an item template with the secondary text in the shared look's secondary brush)

## Acceptance criteria

- [x] ISO rows: the code and its limits at the chosen size, from `Iso286` (H7 at 6 mm: `H7  +0.012 / 0`). Limits rows: the row's Upper and Lower (`+0 / -0.020`). Main text is the tolerance; secondary text the row's name, when it differs from the main text.
- [x] Limits are in the window's units: mm with 3 decimals, inches with 4; a zero limit is written `0`; a plus or minus sign on every non-zero limit.
- [x] A size outside ISO 286's range shows the code and "not in ISO 286" and stays unselectable or flagged as today's rules decide (no change to the rules).
- [x] The texts update when the size, units or fit change; the selection is kept.
- [x] The closed dropdown shows the same main text.
- [x] XAML rules test passes; light and dark both readable.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

- **Ran:** on Windows, `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: Build succeeded, 0 errors, 8 warnings (all CS8600 in `CreatePrintCommand`, `SaveAsReplaceCommand`, `SaveAsCopyCommand`, `NewPartCommand`; already there, not from this task). `dotnet test tests/InventorAddin.Core.Tests`: 3291 passed, 0 failed, 0 skipped (the XAML rules tests included).
- **Not compiled (changed under `src/InventorAddin`):** none; `src/InventorAddin/UI/AwbHoleWindow.xaml` was compiled in the Windows build. It has not been run in Inventor.
- **Inventor API members not confirmed:** none; no Inventor API is used. WPF only: the item template's second `DataTrigger` finds the `ComboBoxItem` ancestor (with `FallbackValue={x:Null}`) to collapse the name in the closed box. Not yet seen running.
- **What was built:** `HoleToleranceOption(Value, Text, Detail)` in `AwbHoleViewModel.cs` replaces `HoleOption<string>` for `ToleranceOptions`. `TextFor` builds the main text: an ISO row's code and its limits from `Iso286` at the dowel row's nominal size (only the code when the size is not known, `H7  not in ISO 286` when ISO 286 does not cover it); a Limits row's Upper and Lower; the row's name when a row has neither. `FormatLimit` writes mm to 3 decimals and inches to 4 (ISO micrometres / 25400 for inches), puts a sign on every non-zero limit, and writes a limit that rounds to zero as `0`. Numbers use the invariant culture (a point), as `HoleRules` messages do. Detail is the row's name, or null when it is the same as the main text. The list is rebuilt on every change, so it follows size, units and fit. The selection and the rules for keeping it are unchanged, and the out-of-range tolerance is still flagged by `HoleRules`.
- **Brief note:** the acceptance criteria give a Limits row as `+0 / -0.020` but also say "a zero limit is written `0`". I followed the rule, so that row reads `0 / -0.020`.
- **Manual checklist for Inventor:**
  1. Open a part, click AWB Hole, choose Standard AWB Dowel, size 6 mm, Normal fit. Open the Tolerance list: each ISO entry reads like `H7  +0.012 / 0` with the row's name beside it in grey. The closed dropdown shows only `H7  +0.012 / 0`. Change to 10 mm: `H7  +0.015 / 0`, and the same row stays selected. A Limits row (Slip or Press, if the workbook has one) shows its own limits, for example `0 / -0.020`.
  2. Switch Units to Imperial: the limits are in inches with 4 decimals (H7 at 1/4" reads `H7  +0.0006 / 0`).
  3. Hover and select entries in light and dark Inventor themes: the tolerance text and the grey name are both readable, and the highlighted entry is still readable.
  4. Optional: with a dowel size over 500 mm in the workbook, the ISO entries read `H7  not in ISO 286` and the error under Tolerance stays as before.

## Follow-ups

- When the closed box draws the selected entry, the `ComboBoxItem` ancestor binding in the Tolerance item template fails on purpose and writes a binding trace to the debug output. It does no harm. If the noise is unwanted, a `DataTemplateSelector` or a separate `SelectionBoxItemTemplate` style in `UI/Theme` could replace it.
