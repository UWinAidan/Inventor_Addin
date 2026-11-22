# 202: Batch Print command

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 3)
- **Status:** todo
- **Depends on:** 184, 191, 201
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

An assembly button that makes one PDF of the assembly's drawing and every drawing under it, saved next to the assembly.

## Scope

- `src/InventorAddin/Drawing/PdfExporter.cs` (spike 184, question 4; a merge library only if the spike chose one, with its licence named in the PR).
- `src/InventorAddin/Commands/BatchPrintCommand.cs` + ribbon button (assembly); progress and the report at the end.

## Acceptance criteria

- [ ] A test assembly with three printed parts and one without: one PDF, the assembly first, then the three; the report names the part with no print.
- [ ] An existing PDF is replaced only after asking.
- [ ] Drawings opened for printing are closed again; nothing is saved.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in the spike results or existing code, or listed under Verification

## Notes for the implementer

- Confirmed calls (spike 184, round 1):
  - The PDF add-in: `ApplicationAddIns` item with `ClassIdString` `{0AC6FD96-2F4D-42CE-8BE0-8AEA580399E4}`, cast to `TranslatorAddIn`.
  - Setup: a `TranslationContext` with `Type = kFileBrowseIOMechanism`, `HasSaveCopyAsOptions(doc, context, options)` to fill the defaults, then `options.Value["Sheet_Range"] = kPrintAllSheets` and `All_Color_AS_Black = 0`.
  - Export: a `DataMedium` with `FileName`, then `SaveCopyAs(doc, context, options, medium)`. One PDF per drawing, every sheet.
  - Hidden drawings: `Documents.Open(path, false)` opens without showing; `Close(true)` after.
  - There is no append option, so merge the files with PDFsharp (MIT; Default, DECISIONS.md 2026-11-22) and name the licence in the PR.

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
