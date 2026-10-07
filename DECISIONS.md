# Decisions

A log of decisions, oldest first. Append only: never edit or delete an entry. To change a decision, add a new entry that says which one it replaces.

Each entry says who decided. **Aidan** means he said so. **Default** means an agent picked it to keep work moving and Aidan has not confirmed it; he can overturn it.

The detail of each decision lives in the spec named in the last column. This file is the index.

| Date | Decision | By | Where |
|---|---|---|---|
| 2026-10-06 | Target Inventor 2026, C#, .NET 8 | Aidan | `CLAUDE.md` |
| 2026-10-06 | Reference screenshots stay local in `ADDIN_PICS/` (gitignored); specs describe behaviour in our own words | Aidan | `CLAUDE.md` |
| 2026-10-06 | A parent session plans and commits; `implementer` and `reviewer` subagents do and check the work | Aidan | `docs/WORKFLOW.md` |
| 2026-10-06 | The material library is a custom `.adsklib` Aidan supplies; it is never committed and the add-in reads it from a path in settings | Aidan | spec 03 |
| 2026-10-06 | A finish is a process applied to a part (plating, coating, heat treatment), not an Inventor appearance | Aidan | spec 03 |
| 2026-10-06 | Rebrand to "AWB Addin": tab and product name "AWB Addin", id prefix `Awb`, data folder `AwbAddin`, log file `awbaddin.log`, all held in `Branding` in Core. Project, assembly and namespace names and the add-in GUID stay | Aidan | task 010 |
| 2026-10-06 | Part information is edited in an add-in window, not an iLogic form. The data is stored as iProperties | Aidan | spec 07 |
| 2026-10-06 | Part numbers are assigned when the CAD file is created and are never editable in the properties window. A file with no number can have one generated; a file that has one cannot | Aidan | specs 02, 07 |
| 2026-10-06 | Milestone order: part properties window M1, numbering M2, material and finish M3, hole table M4, drawing tools M5, iLogic M6 | Aidan | `docs/PLAN.md` |
| 2026-10-06 | Distribution (installer and download page) is parked until the part number generator is planned | Aidan | `docs/PLAN.md` |
| 2026-10-06 | Material and finish are chosen in their own window, using only Aidan's library. The properties window shows both read-only | Aidan | specs 03, 07 |
| 2026-10-06 | Aidan writes the finish list himself, later. Material and finish (M3) is not planned before the list or its format exists | Aidan | spec 03 |
| 2026-10-06 | The part name is linked to the file name: the window shows the file name read-only and keeps Description equal to it. Renaming is a separate command, planned with numbering | Aidan | specs 02, 07 |
| 2026-10-06 | Detailer follows Designer by default until a different Detailer is typed | Aidan | spec 07 |
| 2026-10-06 | Part type codes: P purchased, M manufactured, PM purchased modified, F fastener, A assembly, W weldment, R reference, C customer supplied. A and W are pre-filled from the file type | Aidan | spec 07 |
| 2026-10-06 | The part type's full name is shown as a tooltip on hover | Default | task 019 |
| 2026-10-06 | A blank cost is stored as 0, and a stored 0 shows as blank | Default | spec 07, question 4 |
| 2026-10-06 | Values the window pre-fills count as pending changes: OK writes them, Cancel writes nothing | Default | spec 07, question 5 |
| 2026-10-06 | A part type stored in the wrong case (`m`) is shown and rewritten as the listed code (`M`) | Default | spec 07, question 6 |
| 2026-10-06 | The project keeps three tracking files at the repo root: `TODO.md`, `STATUS.md` and `DECISIONS.md`. The parent session maintains them | Aidan | `CLAUDE.md` |
| 2026-10-06 | Every window uses one shared look, "grouped sections": fields in named sections, read-only values as text, light and dark following Inventor | Aidan | spec 08 |
| 2026-10-06 | Window style is done before numbering, as milestone M1b | Aidan | `docs/PLAN.md` |
| 2026-10-06 | A file with no assigned part number shows "Not assigned", never its name. Until numbering exists, a Part Number equal to the file name counts as none | Aidan | specs 07, 08 |
| 2026-10-06 | No new agent roles for now. The limit on speed is the steps only Aidan can do, not the agents | Aidan and Claude | `TODO.md` |
| 2026-10-07 | Ribbon icons come in a light-theme and a dark-theme set; the ribbon uses the set for the theme Inventor has at start | Default | spec 08, question 3 |
| 2026-10-07 | A never-saved file's header reads "New part" (or the kind) with "Part · Not saved yet" under it | Default | spec 08, question 4 |
| 2026-10-07 | For a never-saved file, any non-blank Part Number counts as assigned | Default | spec 08, question 5 |
| 2026-10-07 | A theme name containing "dark" selects the dark colours; anything else, or no name, selects light | Default | spec 08, question 6 |
| 2026-10-07 | About shows the Inventor release year (major version + 1996) | Default | spec 08, question 7 |
| 2026-10-07 | A Core test checks window XAML against the shared-style rules (task 031) | Default | spec 08, question 8 |
| 2026-10-07 | Windows refer to shared styles with `DynamicResource`, because `WindowHost` merges the theme after a window is built. Confirmed or replaced by spike 020 | Default | tasks 020, 026 |
| 2026-10-07 | M1b runs in two cloud runs around the Windows spike: 021 to 025 first, 026 to 031 after the spike's results | Default | `docs/PLAN.md` |
| 2026-10-07 | The ribbon icons stay as drawn in task 024; they are not redrawn | Aidan | task 024, spec 08 |
| 2026-10-07 | Secondary buttons use the input background colour (white in light, slightly darker than the window in dark) | Default | spec 08, question 9 |
| 2026-10-07 | Section headings get wide letter spacing from hair spaces inserted by a Core helper, because WPF text has no letter spacing | Default | task 026 |
| 2026-10-07 | The XAML rules test also requires each input to sit in a labelled row, and bans brushes, colours and size values defined in a window's own resources | Default | task 031 |
| 2026-10-07 | Windows must not use `UseLayoutRounding`; the shared window style uses `SnapsToDevicePixels` instead. With layout rounding on, text boxes and dropdowns lost their bottom border at 150% display scale | Aidan | `CLAUDE.md`, `UI/Theme/Styles.xaml` |
