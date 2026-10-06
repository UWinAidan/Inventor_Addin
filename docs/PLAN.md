# Roadmap

Owned by the parent session (see `docs/WORKFLOW.md`). Feature behaviour lives in `docs/features/`; this file tracks order and status only.

## Where the project stands

- Solution with two projects: `InventorAddin.Core` (plain .NET 8, builds anywhere) and `InventorAddin` (the add-in, .NET 8 for Windows, built against Inventor 2026).
- The add-in registers an "AWB Addin" tab with two developer buttons that dump the active document and the asset libraries to JSON.
- The extraction layer reads iProperties, parameters, iLogic rules, material and appearance, mass properties, sheet metal data, holes and threads, finishes, assembly structure, and drawing sheets, views, title blocks and tables.
- Not there yet: tests, anything that writes to a document, any feature UI, settings, icons.

## Milestones

The order below is a draft for Aidan to confirm or change.

### M0: Foundations

Makes the cloud workflow work and gives later features what they share.

Planned 2026-10-06. Run in number order; each task depends only on lower numbers.

| Task | Needs | Depends on | Parallel-safe with |
|---|---|---|---|
| 001 | cloud | none | none |
| 002 | cloud | 001 | 003, 004 |
| 003 | cloud | 001 | 002, 004 |
| 004 | cloud | 001 | 002, 003 |
| 005 | cloud, add-in not compiled | 002, 003 | none |
| 006 | cloud, add-in not compiled | 005 | 007 |
| 007 | cloud, add-in not compiled | 004, 005 | 006 |
| 008 | cloud, add-in not compiled | 002, 007 | none |
| 009 | cloud, add-in not compiled | 008 | none |
| 010 | cloud, add-in not compiled | 009 | none |

- [x] 001 Core test project
- [x] 002 Settings model and JSON store (Core)
- [x] 003 Rolling log file (Core)
- [x] 004 Ribbon layout description (Core)
- [x] 005 Add-in startup services and command error reporting
- [x] 006 Editing command base: transaction and command type
- [x] 007 Ribbon built from the Core layout
- [x] 008 Settings window
- [x] 009 About window
- [x] 010 Rebrand to "AWB Addin"

No spike task: M0 uses only documented Inventor API members (`TransactionManager`, `Application.MainFrameHWND`, `CommandControls.AddButton`). The transaction base (006) has no command to exercise it until M1; the first M1 editing command is its Inventor test.

Scoped around spec 01's open questions: settings are limited to `ShowDeveloperTools` (question 3), About shows the version only (question 2), and ZeroDoc keeps only the Developer panel (question 4). Settings changes take effect at the next Inventor start.

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
3. M0 runs without them, but spec 01 questions 2 to 4 decide what M0 leaves out: update checks in About, settings beyond the developer toggle, and Settings/About on the no-document ribbon.

## Decisions made

| Date | Decision |
|---|---|
| 2026-10-06 | Target Inventor 2026, C#, .NET 8 |
| 2026-10-06 | Reference screenshots stay local in `ADDIN_PICS/` (gitignored); specs describe behaviour in our own words |
| 2026-10-06 | Parent session plans and commits; `implementer` and `reviewer` subagents do and check the work |
| 2026-10-06 | Rebrand to "AWB Addin": tab and product name "AWB Addin", id prefix `Awb`, data folder `AwbAddin`, log file `awbaddin.log`, all held in `Branding` in Core (task 010). Project, assembly and namespace names and the add-in GUID stay |
