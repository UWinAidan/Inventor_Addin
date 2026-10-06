# 013: Property value normalisation and validation (Core)

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Rules: Cost; Core logic: Validation; Open question 4)
- **Status:** done
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

- [x] `PropertyValues.MaxTextLength` is 255
- [x] `PropertyValues.NormaliseText(string? text)` trims surrounding whitespace and returns `""` for null or blank. Inner whitespace is kept
- [x] `PropertyValues.ValidateText(string fieldName, string? text)` returns null when valid, or a message naming the field when the trimmed text is longer than `MaxTextLength`, or when it contains a line break or another control character
- [x] `PropertyValues.TryParseCost(string? text, CultureInfo culture, out decimal? cost, out string? error)`:
  - blank (after trimming) is valid and gives `cost = null`
  - accepts the culture's decimal and group separators, with or without the culture's currency symbol
  - rejects negative values, text that is not a number, and more than 4 decimal places (a COM currency value holds 4) with a message saying which
- [x] `PropertyValues.FormatCost(decimal? cost, CultureInfo culture)` returns `""` for null **and for 0** (open question 4 default: a stored 0 shows as blank), otherwise the number in the culture's format with no currency symbol and no trailing zeros beyond 2 decimal places (`12.5` shows as `12.50`, `12.3456` as `12.3456`)
- [x] `PropertyValues.CostToStore(decimal? cost)` returns 0 for null (clearing the field writes 0) and the value otherwise
- [x] Tests run under fixed cultures (`en-US`, `de-DE`, `fr-FR` at least) and never depend on the machine's culture. They cover: blank, a plain number, group separators, the currency symbol, negative, garbage, 5 decimal places, round trip through `FormatCost` and `TryParseCost`, and the 0-shows-blank rule
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- File-scoped namespace `InventorAddin.Core.PartProperties`.
- `fr-FR` uses a non-breaking (narrow) space as the group separator on recent .NET; make the round-trip test use whatever `FormatCost` produces rather than a hand-typed string.
- Messages are short and say what to do, for example "Cost must be zero or more." They are shown on the window's status line.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 390 passed, 0 failed (124 of them in `PropertyValuesTests`). Cloud session; `src/InventorAddin` was not touched or built. Every test passes `en-US`, `de-DE` or `fr-FR` explicitly; none reads the machine's culture.
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (covered by 019)

## Follow-ups

Things noticed but not done.

- Choices made inside the brief, for the reviewer to confirm: trailing zeros do not count as decimal places (`12.50000` is accepted as 12.5); a parsed cost has its trailing zeros dropped so `12.50` and `12.5` compare equal; values above the COM currency maximum (922,337,203,685,477.5807) are rejected with "Cost is too large."; in a culture whose group separator is a space (`fr-FR`), a plain, non-breaking or narrow space between digits is accepted as the group separator.
- .NET number parsing does not check where group separators sit, so `1,2,3` parses as 123 in `en-US` (and `12 50` as 1250 in `fr-FR`). Left as is; tighten only if it confuses users.
