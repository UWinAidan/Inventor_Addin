# Roadmap

Owned by the parent session (see `docs/WORKFLOW.md`). This file holds the milestones, their order and their task lists. Feature behaviour lives in `docs/features/`. The state of each item is in `STATUS.md`, open items and what is waiting on Aidan are in `TODO.md`, and decisions are in `DECISIONS.md`.

## Milestones

Order set by Aidan on 2026-10-06: the part properties window first, then window style, then numbering. The order after M2 is still a draft. M5 also gets the custom drawing template (Aidan, 2026-10-10).

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

- [x] 011 Agent definitions use the product name
- [x] 012 Part type list (Core)
- [x] 013 Property value normalisation and validation (Core)
- [x] 014 Part properties model, storage map and write plan (Core)
- [x] 015 Default designer setting
- [x] 016 Part properties view-model (Core)
- [x] 017 Editing command base: input step and per-edit transactions
- [x] 018 Read the snapshot from Inventor and write property changes
- [x] 019 Part Properties command, window and ribbon button

No spike task. The approach (read and write iProperties through `PropertySets`, one transaction per Apply) uses documented API and does not change with the four behaviours the spec lists as unconfirmed; those decide details only (the value type Cost accepts, `Property.Delete`, `Document.IsModifiable`, whether Part Number falls back to the file name). Each is a numbered step in task 019's manual checklist, so Aidan's first build and test settles them. Undo during in-place edit is checked the same way (019, step 13).

Two spec questions were added while planning, each with a default the tasks build: blank cost is stored as 0 (question 4), and pre-fills count as pending changes (question 5).

### M1b: Window style and icons (spec 08)

One shared look for every window, following Inventor's light or dark theme, plus ribbon icons. Aidan chose the look on 2026-10-06 and wants it done before numbering, so later windows inherit it. Not blocked.

Planned 2026-10-07. Run in number order; each task depends only on lower numbers.

| Task | Needs | Depends on | Parallel-safe with |
|---|---|---|---|
| 020 | **windows** (spike, Aidan) | none | every cloud task |
| 021 | cloud | none | 022, 023, 024, 025 |
| 022 | cloud | none | 021, 023, 024, 025 |
| 023 | cloud | none | 021, 022, 024, 025 |
| 024 | cloud | none | 021, 022, 023, 025 |
| 025 | cloud, add-in not compiled | none | 021 to 029 |
| 026 | cloud, add-in not compiled | 020, 021, 023 | 025 |
| 027 | cloud, add-in not compiled | 026 | 025 |
| 028 | cloud, add-in not compiled | 023, 027 | 025, 029 |
| 029 | cloud, add-in not compiled | 022, 027 | 025, 028 |
| 030 | cloud, add-in not compiled | 020, 021, 024, 025, 026, 028, 029 | none |
| 031 | cloud | 026 to 030 | none |

- [x] 020 Spike: theme, shared styles and icons inside Inventor (Windows)
- [x] 021 Theme choice and ribbon icon names (Core)
- [x] 022 Part Properties display text (Core)
- [x] 023 Settings and About view-model text (Core)
- [x] 024 Ribbon icon drawings
- [x] 025 Build says plainly whether the add-in was deployed
- [x] 026 Shared look, theme from Inventor, and the About window
- [x] 027 Input control styles: text box, dropdown, checkbox
- [x] 028 Settings window on the shared look
- [x] 029 Part Properties window on the shared look
- [x] 030 Icons on the ribbon buttons and window headers
- [x] 031 A test that keeps windows on the shared styles

**Two runs.** Spike 020 settles the three unconfirmed API behaviours in the spec (dictionaries loading inside Inventor, reading the theme, icons on a button definition) plus the dark title bar. Nothing is planned on top of an unproven assumption, so the work splits:

1. Cloud run 1: 021 to 025. None depends on the spike. Aidan runs 020 on Windows at any point.
2. Cloud run 2, after 020's Results are committed: 026 to 031. 026 and 030 read the Results first and follow them where they differ from the brief.

Folded in from `TODO.md` follow-ups: the empty gap in Part Properties (029), the Settings error that stays visible (023, 028), the build's deploy message (025), and one ribbon failure stopping the rest (030).

**Done.** Every Windows check passed (reported 2026-10-10). The build's deploy line needs two small fixes, recorded as follow-ups in `TODO.md`.

Left out until Aidan agrees: the reviewer styling checklist and the PowerShell build script that spec 08 proposes. Task 031 checks the window rules with a test instead of a reviewer checklist; it is a default Aidan can drop.

### M2: Part creation and numbering (spec 02)

Part numbers `AWB-10001` from a register on the NAS with a local copy on each PC, file names `NAME,AWB-10001.ipt` (name first since 2026-10-11), and the commands that create, copy, rename, print and bulk-update files. Aidan answered the spec's numbering questions on 2026-10-10; the rest have defaults.

Planned 2026-10-10. Run in number order; each task depends only on lower numbers.

| Task | Needs | Depends on | Parallel-safe with |
|---|---|---|---|
| 032 | **windows** (spike, Aidan) | none | every cloud task |
| 033 | cloud | none | 037, 038, 042 |
| 034 | cloud | 033 | 035, 036, 037, 042 |
| 035 | cloud | 033 | 034, 037, 038, 042 |
| 036 | cloud | 035 | 034, 037, 038, 042 |
| 037 | cloud, add-in not compiled | none | 033 to 036, 038 |
| 038 | cloud, add-in not compiled | none | 033, 035, 036, 037, 042 |
| 039 | cloud, add-in not compiled | 033, 035, 036, 037, 038 | 040, 042 |
| 040 | cloud | 033, 034, 037 | 039, 041, 042 |
| 041 | cloud | 033, 034, 035, 038 | 039, 040, 042 |
| 042 | cloud, add-in not compiled | none | 033 to 041 |
| 043 | cloud, add-in not compiled | 032, 036, 039, 040, 042 | none |
| 044 | cloud, add-in not compiled | 032, 034, 037, 042, 043 | none |
| 045 | cloud, add-in not compiled | 032, 040, 043 | none |
| 046 | cloud, add-in not compiled | 032, 045 | none |
| 047 | cloud, add-in not compiled | 032, 034, 036, 039 | none |
| 048 | cloud, add-in not compiled | 032, 036, 039, 041, 042, 049, 051 | none |
| 049 | cloud, add-in not compiled | none | 050 |
| 050 | cloud, add-in not compiled | none | 049 |
| 051 | cloud, add-in not compiled | 049 | none |

- [x] 032 Spike: templates, creating and saving, rename, base view (Windows)
- [x] 033 Part number format (Core)
- [x] 034 File name rule and part name (Core)
- [x] 035 Register model and CSV format (Core)
- [x] 036 Register store: shared folder, local copy, lock, offline (Core)
- [x] 037 Register folder and units settings
- [x] 038 Windows user name counts as blank in Part Properties
- [x] 039 Part number display and Generate part number in Part Properties
- [x] 040 New Part dialog view-model and creation properties (Core)
- [x] 041 Mass Update scan result and view-model (Core)
- [x] 042 Icons for the M2 commands, and a ribbon that survives one failing command
- [x] 043 New Part command and window
- [x] 044 Create Print
- [x] 045 Save As Copy
- [x] 046 Save As and Replace
- [x] 047 Rename, with references updated across the workspace
- [x] 048 Mass Update command and window
- [x] 049 Settings: shorter units label, CAD folders, other names that mean me
- [x] 050 Register AssignedBy is the Default designer
- [x] 051 Other names that mean me, in Part Properties and Mass Update

**Two runs.** Spike 032 settles what the creating, renaming and printing commands depend on: how Inventor finds its default templates in metric and imperial, creating and saving a document with a comma in its name, the workspace path, updating references to a renamed file in closed files, opening files invisibly, placing a base view, replacing an occurrence, and how long a disconnected NAS blocks. Nothing is planned on top of an unproven assumption, so:

1. Cloud run 1: 033 to 042. None depends on the spike. Aidan runs 032 on Windows at any point; it is easiest after building run 1, which he needs to do anyway.
2. Cloud run 2, after 032's Results are committed: 049 to 051 first, then 043 to 048. Each reads the Results first and stops if they are empty. All six touch the ribbon files, so they run one at a time.

Folded in from `TODO.md` follow-ups: delete `PropertyReader.Set` and the unbound view-model properties (039), the ribbon's duplicate-definition retry and one command stopping the ribbon (042), and the Settings access key for Show developer tools (037).

**Added 2026-10-10 before run 2** from Aidan's run 1 checks and his answers to spec 02 questions 23 to 25: 049 (Settings: "New file units", Work folder and Other CAD folders, Other names that mean me), 050 (AssignedBy) and 051 (names that mean me). They run before 043, which is why the order is not the number order. Spike 032's findings are written into 043 to 048's notes. Folded in from `TODO.md`: the Settings Browse freeze (049), the unit label (049), Mass Update's shared name check (051), and its duplicate row and register-only files (048).

**Fix run, planned 2026-10-11** from Aidan's run 2 checks: six bugs and six changes he decided. Run in a local session on Aidan's PC, so every task builds the whole solution before it is committed. Spike 052 was run first, in the same session, to find the causes from Inventor itself (with Aidan for the browser steps). Run in number order, one at a time.

| Task | Needs | Depends on | Parallel-safe with |
|---|---|---|---|
| 052 | **windows** (spike, local session with Aidan) | none | every task |
| 053 | windows build | none | none |
| 054 | windows build | 053 | none |
| 055 | windows build | 054 | none |
| 056 | windows build | none | none |
| 057 | windows build | 053 | none |
| 058 | windows build | 052, 053 | none |
| 059 | windows build | 052, 057, 058 | none |
| 060 | windows build | 052, 055 | none |
| 061 | windows build | 058, 059 | none |

- [x] 052 Spike: rename references, copies and the selection in a sub-assembly (Windows)
- [x] 053 File names put the name first and the number last (item 7)
- [x] 054 Names in capitals, and assemblies start with ASSY_ (items 8, 10)
- [x] 055 Author is written with the same name as Designer (item 11)
- [x] 056 Ribbon button sizes by importance (item 12)
- [x] 057 Create Print saves in a Prints subfolder (item 9)
- [x] 058 Rename never breaks a reference, and renames occurrences (bugs 1, 3)
- [x] 059 Rename renames the part's drawings (bugs 2, 5)
- [x] 060 Save As and Replace finds the selection in a sub-assembly; copies check their number (bugs 4, 6)
- [x] 061 Rename allows a change of case only (Aidan, 2026-10-11)

Folded in from `TODO.md`: spec 08's ribbon icon list (056), and Save As and Replace's "which document holds the selection while a sub-assembly is edited in place" (052, 060). Bug 6 did not reproduce: every copy from the checks has its own number on disk (spike 052); 060 adds a read-back check.

**Fix run 2, planned 2026-10-12** on the same branch, from Aidan's checks of the first fix run (everything else passed): four items. Local session on Aidan's PC. Spike 062 was run first, with Aidan starting Inventor for the startup tab. Run in number order, one at a time.

| Task | Needs | Depends on | Parallel-safe with |
|---|---|---|---|
| 062 | **windows** (spike, local session with Aidan) | none | every task |
| 063 | windows build | none | none |
| 064 | windows build | none | none |
| 065 | windows build | 062 | none |
| 066 | windows build | 062 | none |

- [x] 062 Spike: occurrence names in a closed assembly; the startup tab (Windows)
- [x] 063 Create Print makes .idw drawings (item 1)
- [ ] 064 Ribbon panels: Create, Properties, Drawing, AWB (item 2)
- [ ] 065 Rename renames occurrences in closed assemblies (item 3)
- [ ] 066 The AWB Addin tab is active when Inventor starts with no document (item 4)

Not in M2: browser folders (Aidan: no), a templates folder setting (later, with Aidan's own templates), the custom drawing template (M5), and distribution (parked; see below). The two deploy-line follow-ups from task 025 stay in `TODO.md`.

### M3: Material and finish (spec 03)

Blocked on questions 1 to 3 of the spec. Needs the Windows spike described in the spec before its add-in side is planned.

### M4: Hole table wizard (spec 04)

The largest feature. Its Core half (classification, grouping, tagging, descriptions, fit limits, table model) can be planned once questions 1, 6 and 7 are answered. The add-in half waits on the Windows spike described in the spec.

### M5: Drawing tools (spec 05)

Blocked on question 1 of the spec, which decides which commands exist at all. Includes a custom drawing template whose title block reads every property from the model (Aidan, 2026-10-10): custom print templates with several sheet sizes, sheet colours, and a title block designed with Aidan (Aidan, 2026-10-11). Create Drawing moved to M2 as Create Print.

### M6: iLogic injection (spec 06)

Blocked on questions 1 and 2 of the spec.

### Parked: distribution

An installer and a download page so other people can install the add-in. No spec yet. Aidan was to decide whether and when to add it when the part number generator (M2) was planned; he did not raise it when M2 was planned on 2026-10-10, so it stays parked and is listed under decisions waiting on him. Do not plan or build any of it before then.
