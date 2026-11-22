# 207: Parts list Make/Buy rule (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 6; open question 20)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 205

## Goal

Core decides Make, Buy or blank for a parts list row.

## Scope

- `src/InventorAddin.Core/Drawing/MakeBuy.cs`: input the row's part number, whether it is Content Center, and whether its file is in a library folder; output Make (AWB number, by the numbering parser), Buy, or blank.
- Tests.

## Acceptance criteria

- [ ] AWB-10001: Make. A Content Center bolt: Buy. A file in a library folder: Buy. Anything else: blank.
- [ ] An AWB-numbered file in a library folder: Buy (library wins; say so in a test name).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Use `Numbering.PartNumber` parsing; do not write a second parser.
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
