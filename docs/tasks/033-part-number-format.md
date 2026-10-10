# 033: Part number format (Core)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Part number)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 037, 038, 042

## Goal

Core knows what a part number looks like: it can parse `AWB-10001`, format a number, and pick the next one from a set of numbers already handed out. Everything else in M2 builds on this.

## Scope

- `src/InventorAddin.Core/Numbering/PartNumber.cs` (new)
- `tests/InventorAddin.Core.Tests/Numbering/PartNumberTests.cs` (new)

Out of scope:

- The register, file names, and anything that decides whether a file "has a number" (that needs the register, task 035).

## Acceptance criteria

- [x] A `readonly record struct PartNumber` in `InventorAddin.Core.Numbering` holding the numeric value (`int Value`, 10001 to 99999)
- [x] Constants: `Prefix = "AWB"`, `Separator = '-'`, `DigitCount = 5`, `FirstValue = 10001`, `LastValue = 99999`
- [x] `ToString()` gives `AWB-10001`
- [x] `static bool TryParse(string? text, out PartNumber number)`: true for `AWB-` and exactly five digits whose value is between `FirstValue` and `LastValue`. Surrounding white space is ignored and the prefix is matched ignoring case (`awb-10001` parses as `AWB-10001`). False for null, blank, four or six digits, `AWB10001`, `AWB-1000A`, `AWB-09999`, `AWB-00001`, a number with spaces inside, any other prefix, and non-ASCII digits
- [x] `static PartNumber Parse(string text)` throws `FormatException` with the text in the message when `TryParse` fails
- [x] `static bool IsWellFormed(string? text)` is `TryParse` without the out value
- [x] `static PartNumber Next(IEnumerable<PartNumber> issued)`: one more than the highest, or `AWB-10001` when there are none. Gaps are not filled. Throws `InvalidOperationException` with the message `"The part number sequence is full: AWB-99999 has been used."` when the highest is `LastValue`
- [x] Equality and ordering by value (`IComparable<PartNumber>`)
- [x] Tests cover every case above, with invented numbers only
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- One shared sequence for parts, sheet metal parts and assemblies (Aidan). The part type is not in the number, so nothing here takes a part type.
- Use ordinal comparison and `char.IsAsciiDigit`; do not use `int.Parse` on the whole string (it accepts signs and white space).
- Core uses file-scoped namespaces and implicit usings. Follow the doc-comment style of `PartTypes.cs`.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 854 passed, 0 failed (64 of them in `PartNumberTests`). The full count includes tests from tasks running at the same time in this working tree.
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` was changed.
- **Inventor API members not confirmed:** none; no Inventor API used.
- **Manual checklist for Inventor:** none

Implementation notes:

- The constructor throws `ArgumentOutOfRangeException` for a value outside 10001 to 99999, so a `PartNumber` built in code is always in range. `default(PartNumber)` still has `Value` 0 (a struct cannot prevent it); `TryParse` returns it on failure, and `Next` treats it as lower than any real number.
- `ToString()` and the error messages use invariant culture and are built from the constants.

## Follow-ups

Things noticed but not done.

- `CLAUDE.md` says the brand names live only in `Branding`. `PartNumber.Prefix = "AWB"` is a part number format rule set by Aidan rather than a brand name, so it is kept in `PartNumber` as the task asks. The parent may want to note this exception in `CLAUDE.md` or `DECISIONS.md`.
