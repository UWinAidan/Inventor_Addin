# Roadmap

Owned by the parent session (see `docs/WORKFLOW.md`). Feature behaviour lives in `docs/features/`; this file tracks order and status only.

## Where the project stands

- Solution with two projects: `InventorAddin.Core` (plain .NET 8, builds anywhere) and `InventorAddin` (the add-in, .NET 8 for Windows, built against Inventor 2026).
- The add-in registers a "Workflow Tools" tab with two developer buttons that dump the active document and the asset libraries to JSON.
- The extraction layer reads iProperties, parameters, iLogic rules, material and appearance, mass properties, sheet metal data, holes and threads, finishes, assembly structure, and drawing sheets, views, title blocks and tables.
- Not there yet: tests, anything that writes to a document, any feature UI, settings, icons.

## Milestones

The order below is a draft for Aidan to confirm or change.

### M0: Foundations

Makes the cloud workflow work and gives later features what they share.

- [x] 001 Core test project
- [ ] Settings model and JSON store in Core
- [ ] Editing-command base: transaction, command type, error reporting, log file
- [ ] Ribbon layout per environment, driven by a description in Core (spec 01)
- [ ] Settings and About windows

### M1: Material and finish (spec 03)

Self-contained, and the library extraction it needs already exists. Blocked on questions 1 to 3 of the spec.

### M2: Part creation and numbering (spec 02)

Blocked on questions 1 to 6 of the spec.

### M3: Hole table wizard (spec 04)

The largest feature. Its Core half (classification, grouping, tagging, descriptions, fit limits, table model) can be planned once questions 1, 6 and 7 are answered. The add-in half waits on the Windows spike described in the spec.

### M4: Drawing tools (spec 05)

Blocked on question 1 of the spec, which decides which commands exist at all.

### M5: iLogic injection (spec 06)

Blocked on questions 1 and 2 of the spec.

## Blocked on Aidan

1. Confirm or reorder the milestones.
2. Answer the open questions in the spec for whichever milestone comes after M0.

## Decisions made

| Date | Decision |
|---|---|
| 2026-10-06 | Target Inventor 2026, C#, .NET 8 |
| 2026-10-06 | Reference screenshots stay local in `ADDIN_PICS/` (gitignored); specs describe behaviour in our own words |
| 2026-10-06 | Parent session plans and commits; `implementer` and `reviewer` subagents do and check the work |
