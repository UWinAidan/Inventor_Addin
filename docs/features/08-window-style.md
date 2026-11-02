# 08: Window style and icons

One shared look for every window in the add-in, following Inventor's light or dark theme, plus icons for the ribbon buttons. Done now, while there are three windows, so every later window inherits it.

Reference: Aidan chose look "B · Grouped sections" from a set of mockups on 2026-10-06. The mockups are not in the repository, so this spec states the look in full.

## What exists today

- Three WPF windows with the default WPF look and no shared styles: `SettingsWindow`, `AboutWindow` and `PartPropertiesWindow`, all shown through `WindowHost`.
- Each window sets its own margins and colours inline.
- Ribbon buttons are text only.
- Nothing reads Inventor's theme.

## The look

**Principles**

- Fields are grouped into named sections.
- A value the user cannot change is shown as plain text, not as a greyed-out box. Boxes mean "you can edit this".
- A value that is missing says so in muted text ("Not set", "Not assigned") instead of leaving a blank.
- One accent colour, used only for the primary button and keyboard focus.
- Light and dark follow Inventor. No window sets a colour of its own.

**Type**

| Use | Font | Size | Weight |
|---|---|---|---|
| Everything by default | Segoe UI | 13 | regular |
| Header title | Segoe UI | 16 | semibold |
| Header subline, status line | Segoe UI | 12 | regular |
| Section heading | Segoe UI | 11 | semibold, upper case, wide letter spacing |
| Part number and other identifiers | Consolas | 13 | regular |

**Spacing and size**

| Item | Value |
|---|---|
| Window content padding | 22 left and right, 18 top and bottom |
| Gap between sections | 18 |
| Section heading to its first row | 9, with a 1 px divider under the heading |
| Gap between rows | 9 |
| Label column width | 100 |
| Gap between label and value | 12 |
| Control height (text box, dropdown, button) | 30 |
| Button minimum width | 80 |
| Gap between buttons | 8 |
| Corner radius on controls | 4 |
| Header icon tile | 36 square, radius 6 |

Windows size to their content. There must be no empty band between the last row and the buttons.

**Colours**

| Token | Dark | Light | Used for |
|---|---|---|---|
| `WindowBackground` | `#2B303B` | `#F3F4F6` | window body |
| `TextPrimary` | `#E8EBF0` | `#1B2029` | values, button text |
| `TextStrong` | `#FFFFFF` | `#11151C` | header title, text inside inputs |
| `TextLabel` | `#B9C1CE` | `#4A5361` | field labels |
| `TextMuted` | `#A9B2C1` | `#55606F` | header subline, status line, the full name beside a code |
| `TextPlaceholder` | `#8F99A9` | `#667080` | "Not set", "Not assigned" |
| `SectionHeading` | `#9AA8BC` | `#55606F` | section headings |
| `Divider` | `#3D4554` | `#C9CED6` | line under a section heading |
| `InputBackground` | `#1F232C` | `#FFFFFF` | text boxes, dropdowns |
| `InputBorder` | `#56617A` | `#8D96A5` | text boxes, dropdowns, secondary buttons |
| `IconTile` | `#3A4150` | `#DDE1E7` | header icon tile |
| `DisabledText` | `#7F8998` | `#7A8493` | disabled button text |
| `DisabledBorder` | `#434B5A` | `#C5CAD3` | disabled button border |
| `Accent` | `#0F6CB0` | `#0F6CB0` | primary button fill, focus outline |
| `OnAccent` | `#FFFFFF` | `#FFFFFF` | primary button text |
| `Error` | `#FF8A80` | `#B3261E` | error text in the status line |

**Buttons**

- Primary (the default button, usually OK or Save): `Accent` fill, `OnAccent` text, semibold.
- Secondary (Cancel, Close, Apply): transparent in dark and white in light, `InputBorder` border, `TextPrimary` text.
- Disabled: transparent, `DisabledBorder` border, `DisabledText` text.
- Keyboard focus shows a 2 px `Accent` outline on every control.

**Window layout**

Every window has the same three bands:

1. **Header.** An icon tile, a title, and one muted line under it.
2. **Body.** One or more sections. A section is a heading and a two-column grid of label and value.
3. **Footer.** The status line on the left, buttons on the right. The status line and the buttons share one row.

**Never taller than the screen** (Aidan, 2026-10-17). No window is taller than the working area of the screen it opens on (the screen less the taskbar), and none opens with its bottom off the screen. When the content is taller, the body scrolls and the header and footer stay visible. How it is built: each window's content is a Grid in the `DialogContent` style with the header, the body in a `ScrollViewer` in the `DialogBody` style on a star-sized row, and the footer; `WindowHost` caps the height and moves the window up if it would run off the bottom. A XAML rules test checks the layout of every window.

**Resizable windows** (Aidan, 2026-10-17; Settings so far). The XAML sets `ResizeMode="CanResize"` and the root Grid uses `ResizableDialogContent`, which widens with the window. `WindowHost` gives it the `ResizableDialogWindow` style (620 wide to start, as tall as its content, at least 460 by 320). It is resized by dragging its edges. Like every dialog it has only the close button: no minimise or maximise button, and snapping it to the top of the screen does not maximise it (Aidan, 2026-10-17). It never opens taller than the working area. Shown with a state key, it reopens at the size and position it had when it closed, Cancel included, pulled back onto a screen that still exists (Default).

**Scroll bars** (Aidan, 2026-10-17). One implicit `ScrollBar` style in `Styles.xaml` for every scroll bar: 8 thick, no arrow buttons, a transparent track, a rounded thumb in the input border colour that turns the muted text colour on hover and while dragged; colours from the theme dictionaries only. In a window's body the bar sits in the right margin, outside the content column, 12 from the content, so the content's right edge lines up with the header and footer whether or not the bar shows (`DialogBody` and `DialogBodyContent`). In the Mass Update list (`ListScroller`) the bar keeps a 12 gap from the entries in a column that is always reserved.

**Collapsible sections** (Aidan, 2026-10-17). `CollapsibleSection` in `UI/Controls` is a section whose heading line is a button: a chevron, the heading, and, while collapsed, a one-line summary of the section's values in muted text, cut short with an ellipsis (the tooltip has it all). Sections start expanded. A window shown with a state key remembers which of its named sections were collapsed. Window state lives in `windows.json` beside `settings.json` (Default), written whenever the window closes.

## The three windows

**Part Properties**

- Header: the part name as the title. Under it, the kind of file and the file name, for example "Assembly · Gearbox.iam". For a file that was never saved, "Not saved yet".
- Section **Identity**
  - Part number: text in the identifier font. See "Part number display" below.
  - Part type: a narrow dropdown showing the code, with the full name of the selected code as muted text beside it. The dropdown entries show code and full name. The tooltip stays.
- Section **People**
  - Designer: text box.
  - Detailer: text box.
- Section **Cost and make-up**
  - Cost: text box, 120 wide.
  - Material: text, or "Not set".
  - Finish: text, or "Not set".
  - Weight: text, or a dash when it cannot be read.
- Footer: status line; Apply, OK (primary), Cancel.
- When the file cannot be edited, the text boxes and dropdown are disabled and the status line says why, as now.

**Settings**

- Header: "Settings", with "AWB Addin" under it.
- **One scrolling page of collapsible sections** (Aidan, 2026-10-17, replacing tabs, whose labels were cut off and showed too little at once): General, Part numbers, Folders, Material and finish, Holes, Developer, in that order, each a `CollapsibleSection`. Collapsed summaries (Default wording): General "designer, N other names, units" ("No default designer" when blank); Part numbers the register folder or "No shared register"; Folders "work folder, N other folders"; Material and finish each file's name, or "starter library" and "starter finish list"; Holes the workbook's file name or "starter hole data", then "AWB Hole" or "Inventor's Hole"; Developer "Developer tools shown" or "hidden". The window is resizable by its edges, with only the close button, and remembered ("Resizable windows" above); Save and Cancel stay visible below the scrolling page.
- Section **General**: Default designer (text box) with its one-line explanation under it in muted text.
- Section **General** also has Other names (text box, under Default designer; help text "Names in old files that mean you, separated by commas, for example Aidan.Brown, 4aida. Your Windows user name always counts.") and New file units (dropdown: Metric, Imperial; was "Units for new files", shortened to fit the label column, task 049).
- Section **Part numbers**: Register folder (text box) with a Browse button beside it in the side button style, and its help text under it (tasks 037 and 049).
- Section **Folders** (task 049): Work folder (text box, Browse beside it; help text "Where New Part and Mass Update start, and the first folder Rename searches."), and Other folders, the Other CAD folders setting (a four-line text box, one folder per line; help text "Other folders that hold your CAD. Rename searches these too."). The label is shortened so it fits the 100-wide label column.
- Section **Material and finish** (task 070): Material library and Finish list (text boxes, Browse beside each opening a file picker for `*.adsklib` or `*.csv`; help text says blank uses the starter file shipped with the add-in). Whether the file exists is checked on Save.
- Section **Holes** (task 086; spec 09, "Settings"): Hole data (text box, Browse beside it opening a file picker for `*.xlsx`; help text says blank uses the starter workbook shipped with the add-in; checked like the finish list, existence on Save for a changed path), and Use Inventor's Hole command (checkbox, off by default), with a note under it that on gives the H shortcut and the Hole button back to Inventor's Hole and that it takes effect when Settings is saved.
- Labels are kept short enough for the 100-wide label column; the help text carries the explanation. Access keys are unique across the window: D, N, U, R, B (Browse, register folder), W, E (Browse, work folder), O, M, F, H (Hole data), I (Use Inventor's Hole command), S. The Browse buttons in Material and finish and in Holes have none: every letter of "Browse" is taken.
- Section **Developer**: Show developer tools (checkbox), with the note that it takes effect when Inventor next starts.
- Footer: error text when a save fails; Save (primary), Cancel.

**About**

- Header: "AWB Addin", with the version under it.
- One section, no heading: Built, Inventor version, Log folder, as label and text rows. The log folder is in the identifier font.
- Footer: Close (primary).

**Select Material and Finish** (spec 03, "Target window"; task 075)

- Header: the `MaterialFinish` icon; the part's file name, or "N parts selected" in an assembly, as the title. Under it, the current material and finish with its code, for example "Steel, 1018 · Hard chrome (HC)", or "No material", "No finish", "Varies".
- Section **Filter**: Category (dropdown, "All" first), then *Preferred only* and *Show paints* as a line of checkboxes.
- Two columns side by side, 12 apart. Left, section **Materials**: Find material (search box), then the materials list, each entry a star (filled when preferred; click it, or press Space on the entry, to change it) and the name. Right, section **Finishes**: Find finish, then the finishes list, each entry the name with its code on the right in the identifier font. The lists are pick lists: 260 wide and 280 tall, so the window keeps its size while filters change, and they scroll.
- Row **Selected**, always shown: the selected material and finish, "No material selected" in placeholder colour when there is none, and a muted note when a filter or search hides the selection from its list.
- Footer: status line (its tooltip lists every note behind it); Apply, OK (primary), Cancel.
- Keyboard: Enter in a search box moves to its list. Double-clicking a material moves to Finishes; double-clicking a finish does OK. Access keys: C, P, S, M, F, A.
- Shared pieces added for it in `Styles.xaml`: `PickList` and `PickListItem` (a list to pick one entry from: input fill and border, Divider fill and semibold on the selected entry, an Accent outline on the entry with keyboard focus), `StarButton`, and `LeftColumn` (12 to the right). `DialogFooter` gained `StatusToolTip`, a tooltip on the status line only.

**AWB Hole** (spec 09, "The window"; task 090; laid out by task 104 as Aidan approved the task 102 mockup on 2026-10-23, `docs/ui-mockups/awb-hole-window.png`)

- **Modeless** (the first): it stays open while the user picks hole centres in the model. `WindowHost.ShowModeless` gives it what a dialog gets (owned by Inventor's main window, the theme, the header icon, kept on the screen) and keyboard input through `ElementHost.EnableModelessKeyboardInterop`; it is opened through `Dispatcher.BeginInvoke` after Inventor's command has returned (spike 081 round 3). One at a time: AWB Hole pressed while it is open brings it to the front.
- **360 wide** (Aidan): the root Grid uses `CompactDialogContent`, whose content is always 316 wide; the label column (100) and the gaps are the usual ones.
- **Fits the screen** (Aidan, 2026-10-23): `WindowHost` caps its height at the working area; the header and the footer stay put and the body scrolls with the shared scroll bar. A `FooterDivider` line runs across the window above the footer, so the scrolled body has a visible edge.
- Header: the `AwbHole` icon; "AWB Hole", or "Edit AWB Hole" for an existing hole, with the part's file name under it. A hole made by Inventor's Hole opens with a muted note at the top of the body that OK makes it an AWB hole, and the read-back's notes under it.
- **Units** on its own row above the sections: a two-part toggle, Metric and Imperial (`SegmentedChoiceList`).
- Section **Hole**:
  - **Type** and **Seat** as rows of icon buttons, the chosen name after them in muted text.
  - **The diagram**, at the full width of the content: a section view of the hole drawn from the values in the window (`HoleDiagramView` drawing Core's `HoleDiagram`): the part hatched, the outline with its seat, thread and bottom, the centre line, and dimensions for the diameter (with a dowel's tolerance), the depth, the seat, the thread and the drill point; a value typed as an expression has a small *fx*. Theme colours only, never the fit colour. 160 tall with its header (the drawing 134). Its header is a `CollapsibleSection` in the `DiagramSection` style: a chevron and "Diagram", and while collapsed a one-line summary such as "⌀6 H7, 31 deep, counterbore ⌀11 by 6.5". Open or collapsed is kept in the settings file (`HoleDiagramExpanded`), so every window and every restart opens it the same way; it starts open.
  - Standard, Size (dropdowns); then Fastener type for a seated clearance or tapped hole, or a dowel's seat boxes (Cbore dia and Cbore depth, or Csink dia and Csink angle; task 106).
  - **Fit** in a narrower dropdown (`ShortComboBox`), a dowel's fit colour and its name on the same row (`ColourSwatch`, the one place a data colour is drawn); Tolerance.
- Section **Depth and direction**: **Termination** as icon buttons (Distance, Through All, and To only for an edited hole that ends at a face, with its note); Thread depth and Drill depth (tapped, blind) or Depth (clearance and dowel, blind); **Drill point** as icon buttons (blind); Direction (Flip).
- **Icon buttons** (`ChoiceList` in the `IconChoiceList` style): 40 by 32, 4 apart, icon only, a tooltip naming each and the same name for screen readers. Not chosen: muted; under the mouse: an input border; chosen: the icon tile fill, a border in the label colour and the strong text colour, not the accent. Tab moves into a row at its chosen button and out again; the arrow keys or Space choose; the label's access key goes to the chosen button. The icons (`HoleIcon`, geometry in `Styles.xaml`) are ours: the part in section, its material shaded and the hole empty, each one different (Aidan): straight walls for Clearance and saw-tooth threads for Tapped; Distance a blind hole with its depth dimensioned beside it, Through All an arrow leaving the bottom, To a heavy end face; drill points as close-ups with a shallow cone (118 deg and any angle from 104), a steep one (below 104) or a flat bottom.
- **Depth boxes** (`DepthTextBox`, 104 wide) with the unit (mm or in) after them and, when the text is an expression, what it comes to in muted text (`= 31 mm`, `ExpressionValueText`). Text that is not a length stays in the box with the reason under it and OK disabled.
- Section **Centres**: the number picked, the problem when none is, and how to pick (or that an edited hole's centres are not sketch points and cannot be changed).
- A field that does not apply is not shown. Each field's problem is shown under it in the error colour.
- Footer: status line (the workbook's problems at open, Inventor's refusal of a change in the error style, or the add-in's notes on the hole as made); OK (primary), Cancel.
- Keyboard: Enter commits the box being typed in and does OK only when OK is enabled; Esc is Cancel. Access keys: U, Y, E, S, Z, F, A, B, K, G, I, T, R, H, D, P, O, L (the dowel seat boxes take A, B, K and G, as the mockup shows).

## Part number display

Decided by Aidan on 2026-10-06: a file that has not been given a part number must not show its name as if it were one.

Inventor reports the file name as the Part Number when none has been set (confirmed in Inventor). Until numbering exists (spec 02), the window treats a Part Number equal to the file name without its extension as "no number" and shows **Not assigned** in placeholder colour. Any other value is shown as it is. The comparison ignores case and surrounding spaces.

This is a display rule only. Nothing is written, and the Part Number property is not changed. Spec 02 replaces the rule with its own definition of "has a number" when it adds the Generate button.

Replaced in M2 (task 039) by spec 02, "Part number": a Part Number in the `AWB-10001` format is shown, with a muted "Not in the register" note when the register does not hold it; anything else shows **Not assigned**.

## Following Inventor's theme

- When a window opens, the add-in asks Inventor which theme is active and loads the matching colour set. If the theme cannot be read, use light.
- A window that is already open does not need to change if the user switches theme.
- The window's title bar is drawn by Windows. Making it dark to match is wanted if it is simple and safe; otherwise leave it and record that in the task's follow-ups.

## Ribbon icons

- Every ribbon button gets an icon in 16 px and 32 px. The ribbon shows the 32 px icon on a large button and the 16 px icon on a small one. The buttons on the ribbon today, by panel, with their size (sizes: Aidan, 2026-10-11, spec 02; panels: Aidan, 2026-10-12, spec 01):
  - Create: New Part (large), Save As Copy (small), Save As and Replace (small), AWB Hole (large, icon `AwbHole`, task 087; button task 090)
  - Properties: Part Properties (large), Mass Update (small), Material and Finish (small, icon `MaterialFinish`, task 073; button task 075)
  - Drawing: Create Print (large)
  - Developer: Export Model Data (small), Export Libraries (small)
  - AWB: Settings (small), About (small)
- Simple line icons, one visual weight, readable on both Inventor themes. Provide a light-theme and a dark-theme version if one drawing does not read on both.
- Our own drawings only. Do not copy Inventor's or anyone else's icons.
- A command with no icon keeps working as text only.

## How it is built

- **One place for the look.** Shared resource dictionaries in the add-in project hold the colour sets (one for dark, one for light) and the styles for text box, dropdown, checkbox, buttons, section heading, label, value text, placeholder text, header and footer. Windows contain layout and bindings only.
- **No colours, font sizes or margins typed into a window's XAML.** A window uses the shared styles. This is checked in review.
- `WindowHost` applies the theme to a window before showing it, so no window has to do it itself.
- A new window is built from the shared header, section and footer pieces, so it matches without extra work.

## Core logic (testable without Inventor)

- Choosing the colour set from the theme name Inventor reports, with the fallback to light.
- The part number display rule, as a property on the view-model.
- "Not set" for a blank material or finish, as view-model properties, so the window binds to text that is ready to show.
- The header subline for Part Properties: kind of file and file name.
- The ribbon layout gains an icon name per button.

## Add-in side

- The shared resource dictionaries and styles.
- Reading Inventor's active theme.
- The three windows rewritten on the shared styles.
- Loading icons and attaching them to the button definitions.

## Also in this milestone

Two small things proposed alongside the styling work. Aidan has not confirmed them yet; leave them out of the plan if he says no:

- **Reviewer checklist.** Add to `.claude/agents/reviewer.md`: a window uses only shared styles, sets no colour, font size or margin of its own, has a label for every input, and can be worked by keyboard alone.
- **Build script.** A PowerShell script at the repo root that stops with a clear message if Inventor is running, builds the solution, confirms the add-in was copied into Inventor's add-ins folder, and prints the build time to compare with the About window. `README.md` and `docs/WORKFLOW.md` point to it.

## Notes for planning

- **Unconfirmed API behaviour, to verify on Windows:**
  - how to read Inventor's active theme and what names it reports for light and dark
  - how an icon is handed to a button definition from .NET 8, and which image form it needs
  - whether resource dictionaries in the add-in assembly load from Inventor's process the same way the windows' own XAML does
- The last point decides the whole approach, so make it a **spike task flagged `needs: windows`** before the windows are rewritten: one shared dictionary, used by one window, built and opened in Inventor.
- Icons are independent of the window work and can be planned as a separate group of tasks, after the windows.
- Follow-ups in `TODO.md` that touch the same code and should be folded in: the empty gap in Part Properties, the Settings error text that stays visible, and the build skipping the copy when Inventor is open.

## Manual checklist for Inventor (to be completed by the tasks)

1. Each of the three windows in Inventor's dark theme, then in its light theme.
2. Part Properties on a file with no number shows "Not assigned"; on a file whose Part Number was typed in Inventor's own dialog it shows that value.
3. Tab moves through the editable controls in order, the focus outline is visible, Enter and Esc work.
4. A read-only file still reads clearly.
5. Ribbon icons at both sizes, on both themes.

## Open questions

None block planning; each has a default above.

1. The accent colour. Default: blue `#0F6CB0`.
2. Should the title bar be darkened to match? Default: only if simple and safe.
3. Icon style. Default: simple line icons, drawn twice (dark strokes for Inventor's light theme, light strokes for its dark theme). The ribbon picks the set for the theme Inventor has when it starts.

Added while planning M1b (2026-10-07). Each has a default the tasks build:

4. What a never-saved file's header shows. Default: title "New part" (or "New assembly", and so on), subline "Part · Not saved yet".
5. The part number of a never-saved file. With no file name to compare against, the default shows any non-blank Part Number as it is and a blank one as "Not assigned". Task 029 records what Inventor reports for a new part (it may be `Part1`), which may change this.
6. Which theme names count as dark. Default: any name containing "dark", until spike 020 records the names Inventor 2026 reports.
7. The About window's Inventor version row. Default: the release year (2026), worked out from Inventor's major version (30).
8. A test (task 031) that fails when a window's XAML sets its own colour, font size, margin or fixed size, so the rule does not depend only on review. Default: added.

Added while building M1b (2026-10-07):

9. The secondary button's fill. The spec says "transparent in dark, white in light", but no colour token does both. Default (task 026): the input background, which is white in light and slightly darker than the window in dark. The alternative is a new colour token.
