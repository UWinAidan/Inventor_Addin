# 02: Part creation and numbering

Create a new part or assembly from the right template with a part number, part type and iProperties already assigned, and copy existing files under a new number.

Reference screenshots: none of the dialogs. `addin tab.png` shows the buttons only. This spec comes from the README and from Aidan's description, so most of it is still to be decided.

## What exists today

- `PropertyReader` reads iProperties, including part number, stock number, description, revision and designer (`CommonProperties` in Core).
- `DocumentKinds` distinguishes part, sheet metal part, assembly, weldment and drawing.
- Nothing writes to a document yet.

## Decided

- **A part number is assigned when the CAD file is created, and that is the only time.** It is never typed or edited by hand afterwards. The properties window (spec 07) shows it read-only.
- **A file without a number can be given one.** The properties window gets a "Generate part number" button that appears only when the file does not already have a number. A file that has a number can never be given another this way.
- The numbering configuration is not decided yet. Aidan will settle it when this milestone is planned.
- **The part name is linked to the file name** (spec 07). The properties window shows the name read-only and keeps the Description property equal to it. Renaming is done by a Rename command that belongs to this spec.

## Proposed, not yet confirmed

- **A register file owns the numbers.** One file lists every number handed out, with its description, part type, file path and date. The next number comes from the register, and before assigning it the add-in also checks that no file in the CAD folder already carries it.
- **The register lives on Aidan's NAS, with a local copy on each PC.** Only this small data file goes on the NAS, never the CAD. When the NAS cannot be reached the add-in works from the local copy and syncs when it reconnects, reporting any number handed out twice.
- **Existing CAD** gets numbers through the Generate button on one file at a time, and through a command that scans a folder, lists what each file has now, and registers or assigns numbers. Existing file names are left unchanged, because renaming breaks assembly references.

## Target

**New Part**

1. The user picks a file type: part, sheet metal part, or assembly.
2. The user picks a part type, such as manufactured or purchased.
3. The add-in assigns the next part number. Whether numbering differs by type is open question 8.
4. The user enters a description.
5. The add-in creates the document from the matching template, writes the iProperties, and saves it under the naming convention.

**Save As Copy.** Copy the active document under a new part number, with iProperties updated.

**Save As and Replace.** From inside an assembly: copy the selected component under a new number and replace that occurrence with the copy.

**Rename.** Change a file's name, and with it the part name, without breaking anything: rename the file on disk and update every assembly and drawing that references it. Opened from a Rename button in the properties window. This needs a Windows spike before it is planned, to prove how references in files that are not open get found and updated.

**Assembly browser folders.** The reference assembly groups components into browser folders by part type (`01_Fasteners`, `02_Manufactured`, `03_Purchased`, `04_Customer`). Whether our add-in creates and maintains these is an open question.

## Core logic (testable without Inventor)

- The numbering scheme: format, validation, and next-number allocation behind an interface, so the number source can change.
- The part type list and what each type implies: template, number range, BOM structure, browser folder.
- The file naming rule: part number and description to file name, with illegal characters handled.
- The set of iProperty values to write for a new document.
- View-model for the New Part dialog.

## Add-in side

- Create a document from a template file; write iProperties; save.
- Replace a component occurrence in an assembly.
- Create and populate browser folders, if that is wanted.

## Open questions

These block planning for this feature.

1. **What does a part number look like?** Format, prefixes per part type, number of digits.
2. **Where does the next number come from?** A counter file, a scan of existing files in the project folder, a spreadsheet, or typed in by hand and only validated.
3. **Which part types exist**, and what does each one change?
4. **File naming convention.** How are part number and description combined into the file name?
5. **Templates.** Where do the part, sheet metal, assembly and drawing templates live, and does the add-in ship them or point at a folder?
6. **Which iProperties get written** on creation, including any custom ones?
7. Should the add-in maintain the assembly browser folders?
8. **Do assemblies and parts use different numbering configurations?** For example separate prefixes or separate sequences. Raised by Aidan; to be decided when this milestone is planned.
9. **What counts as "already has a number"?** Inventor may report the file name as the Part Number when none was set (spec 07 checks this on Windows). If so, the rule cannot be "Part Number is not blank"; it has to be "the number is in the register" or a marker property the add-in writes when it assigns one.
10. **Is the register design above accepted**, including the NAS location and the local copy?
11. **How is the part name read from a file name that also carries a part number?** For example, everything after the number. Depends on the file naming convention in question 4.
12. **Where does Rename look for referencing files?** The active project's workspace, a folder set in settings, or only the assemblies and drawings that are open.
