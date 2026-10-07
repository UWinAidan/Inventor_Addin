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
- Section **General**: Default designer (text box) with its one-line explanation under it in muted text.
- Section **Developer**: Show developer tools (checkbox), with the note that it takes effect when Inventor next starts.
- Footer: error text when a save fails; Save (primary), Cancel.

**About**

- Header: "AWB Addin", with the version under it.
- One section, no heading: Built, Inventor version, Log folder, as label and text rows. The log folder is in the identifier font.
- Footer: Close (primary).

## Part number display

Decided by Aidan on 2026-10-06: a file that has not been given a part number must not show its name as if it were one.

Inventor reports the file name as the Part Number when none has been set (confirmed in Inventor). Until numbering exists (spec 02), the window treats a Part Number equal to the file name without its extension as "no number" and shows **Not assigned** in placeholder colour. Any other value is shown as it is. The comparison ignores case and surrounding spaces.

This is a display rule only. Nothing is written, and the Part Number property is not changed. Spec 02 replaces the rule with its own definition of "has a number" when it adds the Generate button.

## Following Inventor's theme

- When a window opens, the add-in asks Inventor which theme is active and loads the matching colour set. If the theme cannot be read, use light.
- A window that is already open does not need to change if the user switches theme.
- The window's title bar is drawn by Windows. Making it dark to match is wanted if it is simple and safe; otherwise leave it and record that in the task's follow-ups.

## Ribbon icons

- Every ribbon button gets an icon in 16 px and 32 px: Part Properties, Settings, About, Export Model Data, Export Libraries.
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
