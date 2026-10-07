# 07: Part properties window

One window to see and edit the information a part or assembly carries: who designed it, what it is called, its type, cost and finish. It can be opened at any time, on new files and old ones.

This is an add-in window, not an iLogic form. It exists once in the add-in, so nothing has to be copied into each file, and it can grow to assign part numbers and offer finish lists later.

Reference screenshots: none. This is our own design.

## What exists today

- `PropertyReader` reads every iProperty and pulls out the common ones (`CommonProperties` in Core: part number, description, designer, material and others).
- `PartExtractor` reads mass (`PhysicalProperties.MassKg` and `MassDisplay`).
- `DocumentEditCommand` runs an editing command inside one transaction. Nothing uses it yet; this feature is its first user.
- The Settings and About windows set the pattern for WPF windows: a view-model in Core, a thin XAML window, shown through `WindowHost`.
- Nothing writes an iProperty yet.

## Where the data lives

The window stores nothing itself. Every field is an iProperty in the file, so Inventor's own iProperties dialog, parts lists and title blocks see the same values, and an edit made in either place shows up in the other.

| Field | Editable | Stored in |
|---|---|---|
| Part number | never | standard Part Number |
| Part name | no, follows the file name | the file name; standard Description is kept equal to it |
| Part type | yes, from a list | custom property `Part Type`, stored as the short code |
| Designer | yes | standard Designer |
| Detailer | yes, follows Designer by default | custom property `Detailer` |
| Cost | yes | standard Cost (shown in Inventor as Estimated Cost) |
| Material | no, shown only | the document's material, set in the Material and Finish window (spec 03) |
| Finish | no, shown only | custom property `Finish`, set in the Material and Finish window (spec 03) |
| Weight | no, shown only | not stored; read from the model's mass in the document's units |

Standard properties are used wherever Inventor has one.

## Rules

- **Part number is read-only in this window, always.** A number is assigned when the CAD file is created (spec 02) and does not change afterwards. This version only displays it.
- **A file with no number shows "Not assigned".** Inventor reports the file name as the Part Number when none has been set, and the window must not show the name as if it were a number. The exact rule is in spec 08, "Part number display".
- **"Generate part number" comes later.** When numbering exists (spec 02), the window gains a button that assigns a number, shown only when the file does not already have one. It is not part of this feature, and no placeholder button is added now.
- **Part name is linked to the file name.** The part name is the file name without its extension, and the window shows it read-only. When the user applies, the standard Description property is set to the same text if it differs, so drawings and parts lists show the same name as the file. Changing the name means renaming the file, which is a separate Rename command planned with spec 02, because a rename has to update every assembly and drawing that references the file. When that command exists, a Rename button beside this field opens it; no placeholder button is added now. Once numbering puts the part number in new file names, spec 02 decides how the name is read out of the file name.
- **Part type** is chosen from a list of short codes. The field shows the code, each entry in the dropdown shows the code and its full name, and hovering over the field shows the full name of the current code beneath it. The short code is what is stored.

  | Code | Full name |
  |---|---|
  | P | Purchased |
  | M | Manufactured |
  | PM | Purchased, modified |
  | F | Fastener |
  | A | Assembly |
  | W | Weldment |
  | R | Reference: shown for context, not bought or made |
  | C | Customer supplied |

  Blank is allowed. The list lives in Core as data so codes can be added without touching the window. In this version the type is only stored; it changes nothing else.
- **Part type default for assemblies.** When the type is blank, an assembly file is pre-filled with A and a weldment with W. Any code can still be chosen, so a bought-in unit modelled as an assembly can be P. As with every pre-fill, nothing is written until the user applies.
- **Designer** is pre-filled from a new setting, `DefaultDesigner`, when the file's Designer is blank. The pre-fill is not written until the user applies.
- **Detailer follows Designer by default.** When the file has no Detailer, the field shows the Designer and keeps following it as the Designer is edited. Once the user types a different Detailer, it stops following. On apply the Detailer is written as its own value.
- **Cost** is a number, zero or more, blank allowed.
- **Material and Finish are shown, not edited here.** Both are chosen in a separate Material and Finish window (spec 03), which uses Aidan's own material library. That window is a later milestone. Until it exists, Material shows what the file has and Finish shows what the `Finish` property holds, usually nothing. When it exists, a Change button beside these two fields opens it; no placeholder button is added now.
- **Weight** is display only. If the mass cannot be read, show a dash.
- **Only changed fields are written.** Opening the window and pressing OK without changes writes nothing and does not mark the document as modified.
- **One undo.** All writes from one Apply are a single transaction, so one Undo in Inventor reverts them.
- **Files that cannot be edited** (read-only or library files) open the window with every field disabled and a line saying why.

## The window

The layout and look are defined in spec 08 (window style): a header with the part name, sections for Identity, People, and Cost and make-up, and a footer with the status line and buttons. What the window must contain:

- Title: "Part Properties". The header shows the part name and the file name.
- Every field in the table above. Fields the user cannot change are shown as text, not as disabled boxes.
- Buttons: **Apply** (writes, stays open), **OK** (writes if needed, closes), **Cancel** (discards, closes). Apply is enabled only when something changed and everything is valid.
- A status line for validation messages and write errors.

## Where it appears

- A **Part Properties** button on the CAD Automation panel of the Part and Assembly ribbons. Not on the Drawing ribbon and not with no document open.
- It acts on the document being edited, so editing a part in place inside an assembly opens that part's properties.

## Core logic (testable without Inventor)

- A model of the fields and their values, and the mapping from each field to a property set, property name and value type.
- Comparing the edited values with the originals to produce the list of writes, including removing a custom property when its field is cleared.
- Validation: cost parsing in the user's number format, trimming, length limits.
- The part type list: codes, full names, and the default for a document kind.
- Deriving the part name from a file name, and deciding whether Description needs updating.
- The Detailer-follows-Designer rule.
- The `DefaultDesigner` setting and the pre-fill rule.
- The view-model: dirty tracking, Apply and OK enablement, status text, the disabled state for files that cannot be edited.
- The ribbon layout entry.

## Add-in side

- Read the fields from the active document, reusing `PropertyReader` and the mass reading in `PartExtractor`.
- A property writer: set a standard property, add or update a custom property, remove a custom property.
- The command, built on `DocumentEditCommand`, and the WPF window.
- The Settings window gains the Default designer field.

## Notes for planning

- **`DocumentEditCommand` needs a step before the transaction.** Task 006's follow-up records that it has no hook for gathering input, so a dialog would open inside the transaction. Add that hook first: show the window, and start a transaction only around each write.
- **Undo during in-place edit** is unproven (task 006 follow-up). Put it on the manual checklist: edit a part in place inside an assembly, apply a change, and check that one Undo from the assembly reverts it.
- **Unconfirmed API behaviour, to verify on Windows:**
  - how the standard Cost property accepts a value (it is a currency type)
  - adding and deleting a custom property
  - how to tell that a document cannot be modified
  - whether Inventor reports the file name as the Part Number when none has been set. If it does, that is expected here and matters to spec 02.
- **Carry-over:** `.claude/agents/implementer.md` and `reviewer.md` still call the product "Inventor Workflow Tools" (task 010 follow-up). Fix the wording in a task of this milestone.

## Open questions

None of these block planning; each has a default above or in the question.

1. Are there more fields to add? Candidates: revision, vendor and vendor part number for purchased parts, project, notes.
2. Should assemblies show a different set of fields from parts?
3. A file that has never been saved has no file name. Default: the part name shows as blank and Description is left alone until the file is saved.
4. Blank cost. The standard Cost property is a currency value and probably cannot be empty. Default: a stored cost of 0 shows as a blank field, and clearing the field writes 0. (Added while planning M1.)
5. Do pre-fills count as changes? Opening a file whose Designer, Detailer or Part Type is blank, or whose Description differs from the file name, shows values the file does not hold yet. Default: they count as pending changes, so Apply is enabled when the window opens, the status line says that pre-filled values will be written on Apply, OK writes them, and Cancel writes nothing. "OK without changes writes nothing" then holds for a file that already matches what the window shows. (Added while planning M1.)
6. A part type code in a different case. A file can hold `m` (typed in Inventor's own dialog) where the list has `M`. Default: the window shows and selects `M`, it counts as a pending change like a pre-fill (question 5), and Apply or OK writes `M`. The alternative is to keep `m` as an extra "Unknown code" entry and write nothing. (Added while running M1, task 016.)
