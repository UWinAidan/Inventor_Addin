# Roadmap

Owned by the parent session (see `docs/WORKFLOW.md`). Feature behaviour lives in `docs/features/`; this file tracks order and status only.

## Where the project stands

- Solution with three projects: `InventorAddin.Core` (plain .NET 8, builds anywhere), `InventorAddin` (the add-in, .NET 8 for Windows, built against Inventor 2026) and `InventorAddin.Core.Tests`.
- The add-in registers an "AWB Addin" tab with Settings and About windows, and a Developer panel (off by default) with two buttons that dump the active document and the asset libraries to JSON.
- Foundations are in place: per-user settings, a rolling log, a ribbon drawn from a Core layout, error reporting, and a base class that runs editing commands inside one transaction.
- The extraction layer reads iProperties, parameters, iLogic rules, material and appearance, mass properties, sheet metal data, holes and threads, finishes, assembly structure, and drawing sheets, views, title blocks and tables.
- Not there yet: anything that writes to a document, any feature UI, icons.

## Milestones

Order set by Aidan on 2026-10-06: the part properties window first, then numbering. The order after M2 is still a draft.

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

### M1: Part properties window (spec 07)

One window to view and edit a part's or assembly's information, stored as iProperties. The first command that edits a document, so it is also the Inventor test for the transaction base from task 006. Not blocked: every open question in the spec has a default.

Planned 2026-10-06. Run in number order; each task depends only on lower numbers.

| Task | Needs | Depends on | Parallel-safe with |
|---|---|---|---|
| 011 | cloud | none | 012 to 018 |
| 012 | cloud | none | 011, 013, 014, 015, 017, 018 |
| 013 | cloud | none | 011, 012, 017 |
| 014 | cloud | 013 | 011, 012, 015, 017 |
| 015 | cloud, add-in not compiled | 013 | 011, 012, 014, 016, 017, 018 |
| 016 | cloud | 012, 014 | 011, 015, 017, 018 |
| 017 | cloud, add-in not compiled | none | 011 to 016, 018 |
| 018 | cloud, add-in not compiled | 014 | 011, 012, 015, 016, 017 |
| 019 | cloud, add-in not compiled | 015, 016, 017, 018 | none |

- [ ] 011 Agent definitions use the product name
- [ ] 012 Part type list (Core)
- [ ] 013 Property value normalisation and validation (Core)
- [ ] 014 Part properties model, storage map and write plan (Core)
- [ ] 015 Default designer setting
- [ ] 016 Part properties view-model (Core)
- [ ] 017 Editing command base: input step and per-edit transactions
- [ ] 018 Read the snapshot from Inventor and write property changes
- [ ] 019 Part Properties command, window and ribbon button

No spike task. The approach (read and write iProperties through `PropertySets`, one transaction per Apply) uses documented API and does not change with the four behaviours the spec lists as unconfirmed; those decide details only (the value type Cost accepts, `Property.Delete`, `Document.IsModifiable`, whether Part Number falls back to the file name). Each is a numbered step in task 019's manual checklist, so Aidan's first build and test settles them. Undo during in-place edit is checked the same way (019, step 13).

Two spec questions were added while planning, each with a default the tasks build: blank cost is stored as 0 (question 4), and pre-fills count as pending changes (question 5).

### M2: Part creation and numbering (spec 02)

Blocked on the spec's open questions. Aidan will decide the numbering configuration, including whether assemblies and parts are numbered differently, when this milestone is planned. This milestone also adds the "Generate part number" button to the M1 window.

### M3: Material and finish (spec 03)

Blocked on questions 1 to 3 of the spec. Needs the Windows spike described in the spec before its add-in side is planned.

### M4: Hole table wizard (spec 04)

The largest feature. Its Core half (classification, grouping, tagging, descriptions, fit limits, table model) can be planned once questions 1, 6 and 7 are answered. The add-in half waits on the Windows spike described in the spec.

### M5: Drawing tools (spec 05)

Blocked on question 1 of the spec, which decides which commands exist at all.

### M6: iLogic injection (spec 06)

Blocked on questions 1 and 2 of the spec.

### Parked: distribution

An installer and a download page so other people can install the add-in. No spec yet. Aidan will decide whether and when to add it at the point the part number generator (M2) is planned. Do not plan or build any of it before then.

## Blocked on Aidan

1. M1 is not blocked. Its spec lists five optional questions with defaults; questions 4 (blank cost) and 5 (pre-fills count as changes) were added while planning and are worth a look before the run.
2. M2 needs the numbering decisions in spec 02 before it can be planned.
3. Spec 01 questions 2 to 4 are still open: update checks in About, settings beyond those built so far, and Settings/About on the no-document ribbon.

## Decisions made

| Date | Decision |
|---|---|
| 2026-10-06 | Target Inventor 2026, C#, .NET 8 |
| 2026-10-06 | Reference screenshots stay local in `ADDIN_PICS/` (gitignored); specs describe behaviour in our own words |
| 2026-10-06 | Parent session plans and commits; `implementer` and `reviewer` subagents do and check the work |
| 2026-10-06 | Rebrand to "AWB Addin": tab and product name "AWB Addin", id prefix `Awb`, data folder `AwbAddin`, log file `awbaddin.log`, all held in `Branding` in Core (task 010). Project, assembly and namespace names and the add-in GUID stay |
| 2026-10-06 | Part information is edited in an add-in window, not an iLogic form. The data is stored as iProperties (spec 07) |
| 2026-10-06 | Part numbers are assigned when the CAD file is created and are never editable in the properties window. A file with no number can have one generated; a file that has one cannot (specs 02 and 07) |
| 2026-10-06 | Milestones reordered: part properties window is M1, numbering M2, material and finish M3, hole table M4, drawing tools M5, iLogic M6 |
| 2026-10-06 | Distribution (installer and download page) is parked until the part number generator is planned |
