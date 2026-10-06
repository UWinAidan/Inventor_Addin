# 02: Part creation and numbering

Create a new part or assembly from the right template with a part number, part type and iProperties already assigned, and copy existing files under a new number.

Reference screenshots: none of the dialogs. `addin tab.png` shows the buttons only. This spec comes from the README and from Aidan's description, so most of it is still to be decided.

## What exists today

- `PropertyReader` reads iProperties, including part number, stock number, description, revision and designer (`CommonProperties` in Core).
- `DocumentKinds` distinguishes part, sheet metal part, assembly, weldment and drawing.
- Nothing writes to a document yet.

## Target

**New Part**

1. The user picks a file type: part, sheet metal part, or assembly.
2. The user picks a part type, such as manufactured or purchased.
3. The add-in assigns the next part number for that type.
4. The user enters a description.
5. The add-in creates the document from the matching template, writes the iProperties, and saves it under the naming convention.

**Save As Copy.** Copy the active document under a new part number, with iProperties updated.

**Save As and Replace.** From inside an assembly: copy the selected component under a new number and replace that occurrence with the copy.

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
