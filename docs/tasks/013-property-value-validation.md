# 013: Property value normalisation and validation (Core)

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Rules: Cost; Core logic: Validation; Open question 4)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 011, 012, 017

## Goal

Text and cost values typed into the Part Properties window are trimmed, checked and parsed by Core functions with clear messages, so the view-model (016) and the write plan (014) compare and write clean values.

## Scope

- `src/InventorAddin.Core/PartProperties/PropertyValues.cs` (new)
- `tests/InventorAddin.Core.Tests/PartProperties/PropertyValuesTests.cs` (new)

Out of scope:

- Which fields exist and where they are stored (014).
- Showing messages (016).

## Acceptance criteria

- [ ] `PropertyValues.MaxTextLength` is 255
- [ ] `PropertyValues.NormaliseText(string? text)` trims surrounding whitespace and returns `""` for null or blank. Inner whitespace is kept
- [ ] `PropertyValues.ValidateText(string fieldName, string? text)` returns null when valid, or a message naming the field when the trimmed text is longer than `MaxTextLength`, or when it contains a line break or another control character
- [ ] `PropertyValues.TryParseCost(string? text, CultureInfo culture, out decimal? cost, out string? error)`:
  - blank (after trimming) is valid and gives `cost = null`
  - accepts the culture's decimal and group separators, with or without the culture's currency symbol
  - rejects negative values, text that is not a number, and more than 4 decimal places (a COM currency value holds 4) with a message saying which
- [ ] `PropertyValues.FormatCost(decimal? cost, CultureInfo culture)` returns `""` for null **and for 0** (open question 4 default: a stored 0 shows as blank), otherwise the number in the culture's format with no currency symbol and no trailing zeros beyond 2 decimal places (`12.5` shows as `12.50`, `12.3456` as `12.3456`)
- [ ] `PropertyValues.CostToStore(decimal? cost)` returns 0 for null (clearing the field writes 0) and the value otherwise
- [ ] Tests run under fixed cultures (`en-US`, `de-DE`, `fr-FR` at least) and never depend on the machine's culture. They cover: blank, a plain number, group separators, the currency symbol, negative, garbage, 5 decimal places, round trip through `FormatCost` and `TryParseCost`, and the 0-shows-blank rule
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- File-scoped namespace `InventorAddin.Core.PartProperties`.
- `fr-FR` uses a non-breaking (narrow) space as the group separator on recent .NET; make the round-trip test use whatever `FormatCost` produces rather than a hand-typed string.
- Messages are short and say what to do, for example "Cost must be zero or more." They are shown on the window's status line.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (covered by 019)

## Follow-ups

Things noticed but not done.
