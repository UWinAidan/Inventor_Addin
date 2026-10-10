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
| 2026-10-10 | Part number format `AWB-10001`: prefix `AWB`, a dash, five digits, starting at 10001. One shared sequence for parts, sheet metal parts and assemblies. The part type is not in the number | Aidan | spec 02 |
| 2026-10-10 | File names are number, comma, name with underscores for spaces (`AWB-10001,mounting_bracket.ipt`). The part name is everything after the first comma, with underscores shown as spaces | Aidan | spec 02 |
| 2026-10-10 | The register design is accepted: a register file in a folder set in Settings (default `B:\aidan\AWB_Design_Data`, the NAS over Tailscale) with a local copy on each PC. When the NAS is unreachable, assign from the local copy, sync later and report duplicates | Aidan | spec 02 |
| 2026-10-10 | A file already has a number when its Part Number matches the format and is in the register | Aidan | spec 02 |
| 2026-10-10 | New files use Inventor's default templates, metric by default with an imperial option. A templates folder setting comes later, when Aidan has his own templates | Aidan | spec 02 |
| 2026-10-10 | Written on creation: Part Number, Description (the part name), Designer, Detailer, Part Type | Aidan | spec 02 |
| 2026-10-10 | No assembly browser folder management | Aidan | spec 02 |
| 2026-10-10 | Rename searches the active Inventor project's workspace for referencing files | Aidan | spec 02 |
| 2026-10-10 | A Create Print ribbon button makes a drawing from the default drawing template with a base view of the active model, named and numbered the same as the model. It replaces spec 05's Create Drawing | Aidan | specs 02, 05 |
| 2026-10-10 | Part Properties treats a Designer or Detailer equal to the Windows user name as blank and pre-fills it from Settings. A folder scan or Mass Update command replaces it in bulk | Aidan | specs 02, 07 |
| 2026-10-10 | A custom drawing template is a separate M5 item. Its title block reads every property from the model rather than copying them into the drawing | Aidan | spec 05 |
| 2026-10-10 | A file name without a comma (an old file) gives its whole name as the part name, also with underscores shown as spaces | Default | spec 02 |
| 2026-10-10 | A typed part name keeps its case, is trimmed, and has runs of white space collapsed. Characters Windows does not allow, reserved names and a trailing dot are rejected, not replaced | Default | spec 02, question 14 |
| 2026-10-10 | The register is a CSV file `part-register.csv` (columns Number, Name, PartType, Kind, FilePath, AssignedAt, AssignedBy, Computer). The local copy and offline entries live in `%APPDATA%\AwbAddin\register\`. Writes take a lock file; a lock older than 2 minutes counts as stale | Default | spec 02 |
| 2026-10-10 | The register syncs at each assignment and when Mass Update runs, not at Inventor start. The NAS counts as unreachable after 3 seconds | Default | spec 02 |
| 2026-10-10 | Duplicates found at sync are both kept in the register and reported; Aidan resolves them by hand | Default | spec 02, question 15 |
| 2026-10-10 | No folder scan before each assignment; the add-in only checks that the new file does not exist yet | Default | spec 02 |
| 2026-10-10 | A Part Number in the format but not in the register counts as no number: shown with "Not in the register", Generate offered | Default | spec 02, question 13 |
| 2026-10-10 | Generate part number needs a saved file, writes Part Number only, and does not rename the file | Default | spec 02 |
| 2026-10-10 | New Part saves the new file straight away, into the active document's folder or the project workspace (Browse to change). Designer and Detailer come from Settings and are not in the dialog. New Part and Mass Update also appear on the ZeroDoc ribbon | Default | specs 01, 02 |
| 2026-10-10 | Save As Copy writes the creation properties on the copy and keeps everything else. Save As and Replace replaces only the selected occurrence | Default | spec 02, question 16 |
| 2026-10-10 | Create Print works on parts and assemblies, uses the units set in Settings, places one front view at a standard scale that fits, copies only the Part Number, and offers to open an existing drawing instead of making a second | Default | spec 02 |
| 2026-10-10 | The folder scan and Mass Update are one command, Mass Update: it registers numbers, assigns numbers and replaces the Windows user name, on ticked files, after a confirmation. Files open in Inventor are skipped | Default | specs 01, 02 |
| 2026-10-10 | When `DefaultDesigner` is blank, a Windows-user-name Designer or Detailer is shown as it is, so Apply never clears it | Default | spec 02 |
| 2026-10-10 | M2 runs in two cloud runs around a Windows spike (032): Core, settings, Part Properties and icons first (033 to 042), then the commands that create, copy, rename and print files (043 to 048) | Default | `docs/PLAN.md` |
| 2026-10-10 | A blank register folder in Settings means no shared register: numbers come from the PC's own copy, as when the NAS is unreachable | Default | spec 02 |
| 2026-10-10 | Rename updates the file path and name in the number's register entry; it is the only change the add-in makes to an existing row | Default | spec 02 |
| 2026-10-10 | Create Print picks the largest standard scale at which the front view fits in 60% of the sheet | Default | task 044 |
| 2026-10-10 | The part number prefix `AWB` is a numbering-format constant in `PartNumber`, not a brand name, so it is the one exception to keeping brand text only in `Branding` | Default | task 033 |
