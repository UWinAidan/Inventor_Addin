# 02: Part creation and numbering

Create a new part or assembly from a template with a part number, part type and iProperties already assigned. Copy, rename and print existing files under the same numbering, and bring old files into the register.

Reference screenshots: none of the dialogs. `addin tab.png` shows the buttons only. This spec comes from the README and from Aidan's description. Aidan answered the numbering questions on 2026-10-10.

## What exists today

- `PropertyReader` reads iProperties; `PropertyWriter` sets and removes them inside a transaction (M1).
- `DocumentKinds` distinguishes part, sheet metal part, assembly, weldment and drawing.
- The Part Properties window (spec 07) shows the part number read-only and keeps Description equal to the part name.
- `PartNumberDisplay` in Core decides whether a Part Number counts as assigned. It is a stand-in until this spec: a Part Number equal to the file name counts as none.
- `PartName.FromFileName` reads the part name as the whole file name without its extension.

## Decided by Aidan

Earlier (2026-10-06):

- **A part number is assigned when the CAD file is created, and that is the only time.** It is never typed or edited by hand afterwards. The properties window shows it read-only.
- **A file without a number can be given one** with a "Generate part number" button in the properties window, shown only when the file does not already have a number. A file that has a number can never be given another this way.
- **The part name is linked to the file name** (spec 07). Renaming is done by a Rename command that belongs to this spec.

On 2026-10-10:

- **Part number format: `AWB-10001`.** The prefix `AWB`, a dash, and five digits. The first number is `AWB-10001`. Parts, sheet metal parts and assemblies share **one sequence**. The part type is not in the number.
- **File names: number, comma, name**, with underscores for spaces: `AWB-10001,mounting_bracket.ipt`. The part name is everything after the first comma, with underscores shown as spaces: `mounting bracket`.
- **The register design is accepted.** One register file in a folder set in Settings, by default `B:\aidan\AWB_Design_Data` (Aidan's NAS, reached over Tailscale), with a local copy on each PC. When the NAS cannot be reached, numbers are assigned from the local copy, synced later, and any number handed out twice is reported.
- **"Already has a number"** means the Part Number matches the format **and** is in the register.
- **Templates:** Inventor's own default templates for now. Metric by default, with an imperial option. A templates-folder setting comes later, when Aidan has made his own templates.
- **Written on creation:** Part Number, Description (the part name), Designer, Detailer, Part Type.
- **No assembly browser folder management.**
- **Rename searches the active Inventor project's workspace** for files that reference the renamed file.
- **A Create Print ribbon button:** a new drawing from the default drawing template, with a base view of the active part, named and numbered the same as the part.
- **Old files carry the Windows login name** (for example `4aida`) as Designer and Detailer. Part Properties treats a Designer or Detailer equal to the Windows user name as blank and pre-fills it from Settings. The folder scan or a Mass Update command replaces it in bulk.
- **A custom drawing template is a separate item for M5**, not part of this spec. Its title block must read every property from the model, not copy them into the drawing.

## Part number

- Format `AWB-` followed by exactly five digits, `AWB-10001` to `AWB-99999`. Parsing ignores surrounding spaces and the case of the prefix (`awb-10001` reads as `AWB-10001`); writing always uses `AWB` in capitals.
- The next number is one more than the highest number in the register, or `AWB-10001` when the register is empty. Gaps are never filled. After `AWB-99999` the add-in refuses to assign and says the sequence is full.
- **Has a number:** the Part Number parses in this format and the register holds that number. Which file the register entry names does not matter, because files move.
- **Display in Part Properties:** a Part Number in the format is shown as the number. When it is not in the register, a muted note beside it reads "Not in the register" and the Generate button is offered (Default, open question 13). Anything else, including blank and Inventor's file-name fallback, shows "Not assigned". This replaces the stand-in rule in `PartNumberDisplay` (spec 08, "Part number display").

## File names and the part name

- **Building a file name** from a number, a part name and an extension: `{number},{name}{extension}`, with each run of spaces in the name replaced by one underscore. A file without a number (an old file renamed) is just `{name}{extension}`.
- **The name as typed** is trimmed and runs of white space become one space. The case is kept as typed (Default, open question 14). An underscore typed in the name reads back as a space, so typing `mounting_bracket` and `mounting bracket` give the same file.
- **Characters not allowed** in a Windows file name (`\ / : * ? " < > |` and control characters) are rejected with a message naming the character, not replaced. So are a blank name, a name ending in a dot, and Windows reserved names (`CON`, `NUL`, `COM1` and the rest). A comma inside the name is allowed: only the first comma separates the number.
- **Reading the part name** from a file name: the file name without folder and extension; if it contains a comma, everything after the first comma; then underscores shown as spaces, trimmed. A file name with no comma (an old file) gives its whole name, also with underscores shown as spaces (Default). `PartName.FromFileName` changes to this rule, so the Part Properties header and the Description it writes follow it.
- **The full path** of a new file must be shorter than 260 characters. The add-in checks this before it assigns a number.

## The register

**Location.**

| File | Where | Holds |
|---|---|---|
| Shared register | `{RegisterFolder}\part-register.csv`, `RegisterFolder` from Settings, default `B:\aidan\AWB_Design_Data` | every number handed out |
| Lock file | `{RegisterFolder}\part-register.lock` | exists only while a PC is writing |
| Local copy | `%APPDATA%\AwbAddin\register\part-register.csv` | the shared register as of the last sync |
| Pending entries | `%APPDATA%\AwbAddin\register\pending.csv` | numbers this PC assigned while the NAS could not be reached |

The local file names are Defaults. Only this small data file goes on the NAS, never the CAD. A blank `RegisterFolder` means no shared register: every number is assigned from this PC's copy, as when the NAS is unreachable (Default).

**Format** (Default): CSV with a header row, so Aidan can open it in Excel. One row per number handed out:

| Column | Holds |
|---|---|
| `Number` | `AWB-10001` |
| `Name` | the part name, with spaces |
| `PartType` | the short code, or blank |
| `Kind` | `Part`, `SheetMetalPart` or `Assembly` |
| `FilePath` | full path of the file when the number was assigned |
| `AssignedAt` | UTC time, ISO 8601 (`2026-10-10T14:03:00Z`) |
| `AssignedBy` | Windows user name |
| `Computer` | computer name |

Values with a comma, quote or line break are quoted, quotes doubled. Rows are never removed or reordered by the add-in.

**Assigning a number with the NAS reachable:**

1. Take the lock: create the lock file exclusively. If it exists, retry for up to 5 seconds. A lock file older than 2 minutes is treated as left over from a crash and removed (Default).
2. Read the shared register and sync the pending entries into it (below).
3. Pick the next number, append its row, and write the shared register to a temporary file that then replaces it.
4. Release the lock and refresh the local copy.

**Assigning with the NAS unreachable:** the next number is one more than the highest in the local copy and the pending entries together. Its row goes to the pending entries, and the user is told the number was assigned offline and will be synced.

**Syncing** happens at every assignment and when Mass Update runs (Default; not at Inventor start, because a missing network drive can block for many seconds). Each pending row is appended to the shared register. A pending number that the shared register already holds is a **duplicate**: both rows are kept, and the add-in reports each duplicate (number and both file paths) in a message and in the log. Aidan resolves duplicates by hand (Default, open question 15).

**Reachability:** the register folder counts as unreachable when it does not exist or cannot be read within 3 seconds (Default). The check runs off the UI thread with a timeout, because a disconnected mapped drive can block.

**Reading only** (Part Properties deciding "has a number"): reads the local copy, never the NAS, so the window opens quickly. Generate syncs before it assigns, and re-checks.

**No folder scan on each assignment** (Default). The proposed check that no file in the CAD folder already carries a new number is dropped: the register is the source of numbers, and Mass Update brings old files into it. Before saving, the add-in only checks that the new file does not already exist.

## Templates

- Inventor's default template for each kind of file, in metric or imperial units, found through Inventor's own template lookup. The Windows spike (task 032) records how that lookup behaves on Aidan's install.
- A `DefaultUnits` setting, Metric by default. The New Part dialog has a Units choice, pre-set from the setting.
- No templates folder setting yet; it comes when Aidan makes his own templates.

## Written on creation

| Property | Value |
|---|---|
| Part Number | the assigned number |
| Description | the part name, with spaces |
| Designer | `DefaultDesigner` from Settings; not written when that is blank |
| Detailer | the same as Designer (spec 07, "Detailer follows Designer") |
| Part Type | the code chosen in the dialog; not written when blank |

Each goes where spec 07's storage table puts it. Nothing else is written.

## Commands

**New Part** (CAD Automation panel; Part, Assembly and ZeroDoc ribbons, Default)

1. The dialog asks for: file type (part, sheet metal part, assembly), part type (pre-filled A for an assembly, as in spec 07), part name, units (metric or imperial, from Settings), and folder.
2. The folder defaults to the active document's folder, or the active project's workspace when nothing is open, with a Browse button (Default).
3. A preview line shows the file name the part will get, with the number shown as `AWB-?????` until it is assigned.
4. On Create the add-in checks the name and path, assigns the next number, creates the document from the template, writes the properties above, and saves it under its file name straight away (Default), so the name and number are on disk.
5. If creating or saving fails after the number was assigned, the number stays in the register (rows are never removed) and the error says which number was used.

Designer and Detailer are not in the dialog; they come from Settings and can be changed afterwards in Part Properties (Default).

**Generate part number** (button in Part Properties)

- Shown only when the file does not have a number. Disabled, with a note, for a file that has never been saved.
- Syncs with the register, re-checks, assigns the next number, and writes Part Number in one transaction. The file is not renamed (Default); Rename does that.

**Save As Copy** (CAD Automation panel; Part and Assembly ribbons)

- Copies the active part or assembly under a new number into the same folder. The dialog asks for the name and part type, pre-filled from the original.
- Writes the creation properties on the copy (number, Description, part type, and Designer and Detailer from Settings); everything else is kept from the original (Default). Opens the copy.

**Save As and Replace** (CAD Automation panel; Assembly ribbon)

- With one component selected in the active assembly: makes a copy as Save As Copy does, then replaces **that occurrence only** with the copy (Default, open question 16).

**Rename** (button beside the part name in Part Properties)

- Asks for a new part name. The file gets `{number},{new_name}` if it has a number, or `{new_name}` if not.
- Renames the file and updates every assembly, drawing and presentation in the active project's workspace that references it, then sets Description to the new name and updates the file path in the register entry.
- Skips Inventor's `OldVersions` folders. Lists what it updated, and anything it could not update.
- How references in files that are not open get found and updated is settled by the Windows spike (task 032) before the command is built.

**Create Print** (Drawing Tools panel, large button; Part and Assembly ribbons)

- Needs the model saved. Creates a drawing from Inventor's default drawing template in the units set by `DefaultUnits` (Default), places a front base view of the model centred on the first sheet at a standard scale that fits, and saves it beside the model with the same file name and the drawing template's extension (`AWB-10001,mounting_bracket.idw`).
- Writes the model's Part Number into the drawing's Part Number when the model has a number. Nothing else is copied; the M5 title block will read the model's properties.
- If the drawing file already exists, offers to open it instead (Default).
- Works on assemblies too (Default; Aidan said "part").

**Mass Update** (Modelling panel; Part, Assembly and ZeroDoc ribbons) (Default: the folder scan and Mass Update are one command)

- Scans a folder, by default the active project's workspace, with Browse. Includes subfolders, skips `OldVersions`, reads `.ipt` and `.iam` files.
- Lists every file with its file name, Part Number, whether it has a number (in the register), and whether Designer or Detailer equals the Windows user name.
- Offers three actions, each on the files ticked: **register** a number that is in the format but not in the register; **assign** a new number to a file without one (the file is not renamed); **replace** a Designer or Detailer equal to the Windows user name with the values from Settings.
- Each changed file is saved. This cannot be undone with Inventor's Undo, so the add-in asks for confirmation first, saying how many files will change.
- Files open in Inventor are skipped and listed as "open in Inventor".

## Windows user name as Designer or Detailer

- In Part Properties, a Designer or Detailer equal to the Windows user name (ignoring case and surrounding spaces) counts as blank. Designer pre-fills from `DefaultDesigner`; Detailer then follows Designer. As with every pre-fill, nothing is written until Apply.
- When `DefaultDesigner` is blank the rule does not apply and the value is shown as it is (Default), so Apply never clears a Designer with nothing to replace it.

## Core logic (testable without Inventor)

- Part number format: parse, format, next number.
- File name rule: build, read the part name, validate the name and the path length.
- The register: CSV read and write, next number, has a number, sync and duplicates.
- The register store: shared folder and local copy on disk, lock, offline assignment. File handling is Core because it needs no Inventor; tests use temporary folders.
- The creation property set, and the New Part view-model.
- The Windows-user-name rule in the Part Properties view-model.
- The Mass Update scan result and its view-model.
- Create Print: the drawing file name, and the view scale from the model's size and the sheet size.
- Rename: the new file name, which files in the workspace to search.

## Add-in side

- Find the default template, create a document from it, write properties, save.
- Generate part number and Rename buttons in the Part Properties window.
- Replace a component occurrence.
- Create a drawing, add a base view, save.
- Open files invisibly, read and write properties, save (Mass Update, Rename).
- Update references to a renamed file.

## Open questions

Answered by Aidan on 2026-10-10:

1. **What does a part number look like?** `AWB-` and five digits, from `AWB-10001`. No prefix per part type.
2. **Where does the next number come from?** The register.
3. **Which part types exist, and what does each change?** The list from spec 07. The type is not in the number and changes no template or browser folder.
4. **File naming convention.** `{number},{name with underscores}.ext`.
5. **Templates.** Inventor's default templates, metric by default, imperial as an option. A templates folder setting later.
6. **Which iProperties get written on creation?** Part Number, Description, Designer, Detailer, Part Type.
7. **Browser folders?** No.
8. **Different numbering for assemblies and parts?** No: one shared sequence.
9. **What counts as "already has a number"?** In the format and in the register.
10. **Is the register design accepted?** Yes, with the folder in Settings and the default above.
11. **How is the part name read from a file name?** Everything after the first comma, underscores as spaces.
12. **Where does Rename look for referencing files?** The active project's workspace.

Still open (each has a Default above, so none blocks planning):

13. A Part Number in the format that is not in the register (a number from another PC not yet synced, or typed by hand). Default: it counts as no number, the window shows it with "Not in the register" and offers Generate, and Mass Update offers to register it. Should Part Properties offer "Register this number" instead of Generate?
14. Case in file names. Default: kept as typed (`AWB-10001,Mounting_Bracket.ipt` stays). Should names be made lower case, as in the example?
15. Duplicates found at sync. Default: both rows kept and reported; Aidan fixes them by hand. Should the add-in offer to renumber the offline file?
16. Save As and Replace. Default: replaces only the selected occurrence. Should it replace every occurrence of that part in the assembly?
17. Should New Part, Save As Copy and Create Print open the new file's Part Properties window straight after creating it?
18. A comma in the name of a file with no part number. The name is read from after the first comma, so `a,b.ipt` reads back as `b`. Default: a name containing a comma is refused when the file has no part number (Rename on an old file); the reading rule stays as Aidan set it. Should the name instead be read from after the first comma only when the text before it is a part number?
19. A register row that cannot be read (a bad date or number after an edit in Excel), or a column deleted from the file. Writing the file back would drop it, and rows are never removed by the add-in. Default: the add-in refuses to assign or sync while the shared register has such a row or column, and names the lines to fix. Should a row with a good Number but another bad value still count as issued, so assignment can continue?
20. Mass Update on a file whose Designer is a real name and whose Detailer is the Windows user name. Default: Detailer is set to the Default designer from Settings. Part Properties instead makes Detailer follow the file's Designer. Should Mass Update do the same?
21. A file carrying a number the register holds for a different file (for example a copy made in Explorer). Default: it counts as having a number, because the check ignores the path. Should Mass Update flag a number carried by two scanned files even when it is registered?
22. A file named only `AWB-10001.ipt`, with no Part Number set. Inventor's fallback is `AWB-10001`, which is in the format, so the window shows it as a number (and as assigned if the register holds it). Default: accepted, since files in the new format always have a comma. Should the add-in check the Part Number against the file name to catch this?
