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
| 2026-10-10 | A part name containing a comma is refused for a file with no part number, so the name reads back the same | Default | spec 02, question 18 |
| 2026-10-10 | The add-in refuses to assign a number or sync while the shared register has a row it cannot read or a column missing, and names the lines to fix, so a write never drops rows | Default | spec 02, question 19 |
| 2026-10-10 | Mass Update replaces Designer and Detailer separately, each only where it equals the Windows user name, with the Default designer from Settings | Default | spec 02, question 20 |
| 2026-10-10 | When Mass Update finds one unregistered number on two files, the first in scan order keeps it and the other is reported as a duplicate | Default | task 041 |
| 2026-10-10 | A registered number found on a second file counts as assigned on both; Mass Update does not flag it | Default | spec 02, question 21 |
| 2026-10-10 | A PC's pending-entries file with a row it cannot read is refused the same way as the shared register, so a sync never deletes rows it could not read | Default | task 036, spec 02 question 19 |
| 2026-10-10 | Part Properties shows a Part Number only when it is in the `AWB-10001` format, for saved and never-saved files alike. Replaces the 2026-10-07 default that any non-blank Part Number of a never-saved file counts as assigned | Default | spec 02, task 039 |
| 2026-10-10 | A Part Number equal to the file name `AWB-10001.ipt` is shown as a number; the add-in does not compare it with the file name | Default | spec 02, question 22 |
| 2026-10-10 | Drawing templates are looked up with the ANSI drafting standard in the chosen units, falling back to Inventor's units-neutral default drawing template, because the default drafting standard returns no drawing template. The drawing is saved with the template's extension | Default | spike 032, spec 02 question 24 |
| 2026-10-10 | A blank project workspace is never replaced by the project file's folder: New Part and Mass Update start in the active document's folder (or need Browse), and Rename searches the renamed file's folder and subfolders | Default | spike 032, spec 02 question 23 |
| 2026-10-10 | Save As and Replace refuses a component selected inside a sub-assembly, because replacing it would change the sub-assembly file | Default | spike 032, spec 02 question 25 |
| 2026-10-10 | Aidan's main CAD folder is `C:\Users\4aida\OneDrive\Documents\CADING`; two other folders of his also hold CAD. Settings gets a Work folder (default that folder) and Other CAD folders. New Part (with no saved document open) and Mass Update start in the work folder; Rename searches all the CAD folders. Replaces the decision that Rename searches the project workspace and the blank-workspace default | Aidan | spec 02, question 23 |
| 2026-10-10 | Rename also searches the renamed file's own folder and subfolders when the file lies in none of the CAD folders | Default | spec 02 |
| 2026-10-10 | The two other CAD folders are entered in Settings, not written into the repo, since one of them may name an organisation | Default | spec 02, question 23 |
| 2026-10-10 | Metric drawings use the ANSI (mm) template. Confirms the default from spike 032 | Aidan | spec 02, question 24 |
| 2026-10-10 | Save As and Replace may replace a component inside a sub-assembly, and saves the sub-assembly. Replaces the default that refused it | Aidan | spec 02, question 25 |
| 2026-10-10 | Undo after Generate part number leaves the register row in place; accepted as designed | Aidan | spec 02 |
| 2026-10-10 | The Settings label "Units for new files" becomes "New file units", so it fits the label column | Aidan | task 049 |
| 2026-10-10 | The register's AssignedBy is the Default designer from Settings, or the Windows user name when that is blank | Aidan | spec 02, task 050 |
| 2026-10-10 | A setting "Other names that mean me" (comma-separated). Part Properties and Mass Update treat a Designer or Detailer equal to any of them, or to the Windows user name, as the Windows user name | Aidan | spec 02, task 051 |
| 2026-10-10 | The Other CAD folders setting is labelled "Other folders" in Settings, so it fits the label column | Default | task 049 |
| 2026-10-10 | The register folder and work folder Browse buttons sit beside their boxes | Default | task 049 |
| 2026-10-10 | An `otherCadFolders` value in `settings.json` that is not a list makes the file invalid (`.bad`), as every wrong type except an unknown units name does | Default | task 049 |
| 2026-10-10 | Mass Update says "your name" rather than "Windows user name", since a match may be one of the Other names | Default | task 048 |
| 2026-10-10 | New Part is a file-operations command (`kFileOperationsCmdType`), lays its dialog out as File, Identity and Location, and checks that the target file does not exist inside the register lock with the real number, so a name already on disk costs no number | Default | task 043 |
| 2026-10-10 | Create Print stops with a message when the model's range box is empty or flat, before making a drawing; a print with either extension (`.dwg` or `.idw`) beside the model counts as existing | Default | task 044 |
| 2026-10-10 | Save As Copy deletes the copy's file (permanently) if a step fails after it was saved, but only a file that did not exist before; a blank Part Type on the original is pre-filled by kind; the top-level active document is the one copied | Default | task 045 |
| 2026-10-10 | Save As and Replace stops for a read-only top-level assembly and for a suppressed, virtual or never-saved component or sub-assembly; if the replace or the sub-assembly save fails, the copy and its register row stay and the message says what to do; its Save button reads "Save and replace" | Default | task 046 |
| 2026-10-10 | Rename keeps the file's registered Part Number in the new name, otherwise the number in the old file name, otherwise none, as spec 02 says; the brief had read it from the file name only | Default | task 047, spec 02 |
| 2026-10-10 | Rename's register updates that cannot be written (NAS unreachable, lock busy, register unreadable, I/O error) wait in `pending-updates.json` and are applied at the next sync; an update matches the row already holding the new path, then the old path, then the first row with the number | Default | task 047 |
| 2026-10-10 | Rename keeps the old file whenever any file failed or any search folder was skipped; it refuses a change of case only, and stops before changing anything when an open document that uses the file has unsaved changes. Folders must answer in 3 s and list in 60 s | Default | task 047 |
| 2026-10-10 | Mass Update registers existing numbers before assigning new ones; opens a file before assigning its number; stops the rest of the run on a register failure part way; does not re-add a number registered by someone else since the scan; scans on this PC's copy if the sync fails but then refuses Apply; fails read-only files before any number is taken; disables Apply after a run until the next scan; lists at most 320 px of rows before scrolling | Default | task 048 |
| 2026-10-10 | Mass Update does not skip numbers carried by unticked, unregistered scanned files when assigning | Default | spec 02, question 26 |
| 2026-10-11 | File names put the name first and the number last: `NAME,AWB-10005.ipt`. The number is the text after the last comma; the name is everything before it. Replaces the number-first file name | Aidan | spec 02 |
| 2026-10-11 | Part names are upper case: the name boxes type in capitals, and file names and Description are upper case. Answers question 14 | Aidan | spec 02, question 14 |
| 2026-10-11 | Assemblies always start with `ASSY_`; the add-in adds it when missing and never twice, in every command that names an assembly (New Part, Save As Copy, Save As and Replace, Rename). Description stays equal to the name (`ASSY GEARBOX`) | Aidan | spec 02 |
| 2026-10-11 | Create Print saves in a `Prints` subfolder of the model's folder, creating it when missing; Create Print and Rename look there | Aidan | spec 02 |
| 2026-10-11 | A rename must never break a reference; Rename also renames the part's drawings and the assembly occurrences named after the old file | Aidan | spec 02 |
| 2026-10-11 | Each copy made by Save As Copy or Save As and Replace gets its own new number | Aidan | spec 02 |
| 2026-10-11 | Author is written with the same name as Designer and is treated like Designer in Part Properties, Mass Update and the Other names rule | Aidan | spec 02 |
| 2026-10-11 | Ribbon sizes by importance: New Part, Part Properties and Create Print large, the rest small. No Generate ribbon button | Aidan | spec 02, spec 08 |
| 2026-10-11 | M5 gets custom print templates with several sheet sizes, sheet colours and a title block designed with Aidan, reading every property from the model | Aidan | `docs/PLAN.md` |
| 2026-10-11 | A file name holds a number only when the text after its last comma is a part number; otherwise the whole name is the part name. Old number-first files are read as having no number in the file name | Default | spec 02 |
| 2026-10-11 | A comma is allowed in any part name, except that a name for a file with no number is refused when the text after its last comma is a part number. Replaces the 2026-10-10 default for question 18 | Default | spec 02, question 18 |
| 2026-10-11 | Reading a name from a file name keeps its case, so an old lower-case file's Description stays as its file name has it | Default | spec 02 |
| 2026-10-11 | An assembly name that is only `ASSY` is refused ("Enter a name after ASSY_.") | Default | spec 02 |
| 2026-10-11 | Prints made before the Prints folder are still found beside the model by Create Print and Rename, and Rename renames them where they are | Default | spec 02 |
| 2026-10-11 | Rename marks every file it updates as changed before saving it (spike 052: a repointed open document is not dirty and `Save` writes nothing), and keeps the old file while any open document still points at it | Default | spec 02, task 058 |
| 2026-10-11 | A drawing whose new name is taken keeps its name, is updated, and is reported | Default | spec 02, task 059 |
| 2026-10-11 | Author is not shown in Part Properties; Apply writes it equal to a non-blank Designer and never clears it | Default | spec 02, task 055 |
| 2026-10-11 | After saving a copy the add-in reads its Part Number back and fails the copy if it is not the new number | Default | spec 02, task 060 |
| 2026-10-11 | Save As and Replace works on the top-level assembly open in the window, also while a sub-assembly is edited in place | Default | spec 02, task 060 |
| 2026-10-11 | M2 fix run in a local session on Aidan's PC: spike 052 first, then 053 to 060, each built on Windows before its commit | Default | `docs/PLAN.md` |
| 2026-10-11 | Rename allows a change of case only, so old lower-case files can be converted: it goes through a temporary name in two steps, and updates references, drawings, occurrences and the register as any rename does. Replaces the default that refused it | Aidan | spec 02, task 061 |
| 2026-10-11 | Part Properties does not copy a Designer that is one of your own names into Author, so a real Author is never overwritten with the Windows user name | Default | spec 02, task 055 |
| 2026-10-11 | A case-only Rename does its second step only once the old file is gone; otherwise, or if the second save fails, it stops under the temporary name and says so | Default | spec 02, task 061 |
| 2026-10-12 | Create Print makes `.idw` drawings from Inventor's default `.idw` template (ANSI (mm) for metric). Existing `.dwg` prints are still found and opened | Aidan | spec 02, task 063 |
| 2026-10-12 | With no `.idw` drawing template, Create Print stops with a message rather than using a `.dwg` template; when a model has both an `.idw` and a `.dwg` print, the `.idw` is opened | Default | spec 02, task 063 |
| 2026-10-12 | Ribbon panels, in order: Create (New Part; Save As Copy, Save As and Replace), Properties (Part Properties; Mass Update), Drawing (Create Print), AWB (Settings, About), Developer only when developer tools are on. Same layout on every ribbon where the buttons appear | Aidan | spec 01, task 064 |
| 2026-10-12 | The Developer panel sits left of the AWB panel, so AWB is always far right | Aidan | spec 01, task 064 |
| 2026-10-12 | Rename renames occurrences in closed assemblies the same as in open ones | Aidan | spec 02, task 065 |
| 2026-10-12 | Rename also renames an occurrence that already reads the new file name and a number, and sets each occurrence name through a temporary name first (spike 062: the browser keeps the old name otherwise) | Default | spec 02, task 065 |
| 2026-10-12 | When Inventor starts with no document open, the AWB Addin tab is made the active tab; opening files does not change it | Aidan | spec 01, task 066 |
| 2026-10-12 | The startup tab is set when Inventor is ready, and once more if Inventor switches away within 5 s; then the add-in leaves the tab alone. Not done when the add-in is loaded after startup | Default | spec 01, spike 062, task 066 |
| 2026-10-12 | Fix run 2 runs on the same branch in a local session: spike 062 with Aidan first, then 063 to 066, each built on Windows before its commit | Default | `docs/PLAN.md` |
| 2026-10-12 | Aidan's material library stays private and is never committed; it lives on the NAS at `B:\aidan\AWB_Design_Data` and Settings points at it. The repo ships a small generic starter library in `libraries/`, made from scratch, used by default; only that file is allowed past `.gitignore`. Replaces the 2026-10-06 decision that the add-in ships no library | Aidan | spec 03 |
| 2026-10-12 | Hardening is part of the material (for example 4140 flame hardened 55-58 HRC), not a finish, so a finish can go on top. One finish per part | Aidan | spec 03, question 1 |
| 2026-10-12 | The finish list is `libraries/finishes.csv` (Code, Name, Kind, AppliesTo, Appearance, ThicknessMin_mm, ThicknessMax_mm, DimensionsAfterFinish, PrintNote), editable in Excel; a setting can point at another copy. A finish changes the part's appearance and carries a thickness and a print note | Aidan | spec 03, questions 2, 4 |
| 2026-10-12 | Material and finish are chosen together in one window, with the Finishes list filtered by the chosen material | Aidan | spec 03 |
| 2026-10-12 | The finish is stored on the part as iProperties `Finish`, `Finish Code`, `Finish Thickness` and `Finish Note` | Aidan | spec 03, question 3 |
| 2026-10-12 | Preferred materials get a star in the window and a Preferred only filter. No Enable Filters checkbox | Aidan | spec 03, questions 5, 7 |
| 2026-10-12 | In an assembly the command applies to the selected components; with none selected it says to select some | Aidan | spec 03, question 6 |
| 2026-10-12 | Part Properties warns, never errors, when a part's material is not from the library | Aidan | spec 03, question 8 |
| 2026-10-12 | Print notes reach the drawing in M5 through the title block or a notes block; M3 only stores them on the part | Aidan | specs 03, 05 |
| 2026-10-12 | M4 starts with a hole generator for part files: every hole type Aidan has decided on, precision and dowel holes coloured by fit (tight red, regular blue, loose green). Details to be discussed before M4 is planned | Aidan | spec 04, `docs/PLAN.md` |
| 2026-10-12 | M3 is planned in a local session on Aidan's PC: spike 067 first, walked through with Aidan, then the rest | Default | `docs/PLAN.md` |
| 2026-10-12 | Materials are assigned straight from the library (`ActiveMaterial`), then the finish's appearance, in one transaction; materials are compared by display name (spike 067) | Default | spec 03, spike 067 |
| 2026-10-12 | The starter library is generated through the Inventor API from a data file in `libraries/`, and both files are committed (spike 067) | Default | spec 03, question 14 |
| 2026-10-12 | Until Aidan picks them, a finish whose appearance is missing or ambiguous is still applied and the part keeps its material's appearance, with a status line | Default | spec 03, questions 9, 10 |
| 2026-10-12 | Preferred materials are a list in Settings, toggled by the star; Show paints stays, ticked; Finish Thickness reads `0.013-0.025 mm`; sub-assemblies in a selection are skipped and named | Default | spec 03, questions 11 to 13, 15 |
| 2026-10-12 | The finish list uses spike 067's suggested appearance names for now (Iron Black, Chrome - Polished Blue, Nickel, Anodized - Light Gray, Anodized - Black, Anodized - Dark Gray, Satin - Black, Glossy - Black, Smooth - Yellow, Smooth - Medium Gray); Aidan edits the file later if he wants others | Aidan | spec 03, question 9 |
| 2026-10-12 | Appearances are looked up in the Autodesk Appearance Library first, then Inventor's other libraries, then the opened material library; the first library with a match decides | Default | spec 03, question 10, task 069 |
| 2026-10-12 | The starter library is `libraries/StarterMaterials.adsklib`, about 20 common materials generated from `libraries/starter-materials.csv` by a console tool in `tools/`; the add-in ships it and `finishes.csv` in a `libraries` folder beside the DLL | Default | spec 03, question 14, tasks 070, 071 |
| 2026-10-12 | Material and Finish is a small button in the Properties panel after Mass Update, on the Part and Assembly ribbons. Preferred only starts ticked when any preferred material exists. Part Properties' Change button is disabled for assemblies | Default | specs 01, 03, 07, tasks 072, 075, 076 |
| 2026-10-12 | M3 runs in this local session's order 068, 069, 070, 071, 072, 074, then 073 (needs Python), 075, 076 | Default | `docs/PLAN.md` |
