# 118: The AWB Hole window fits without scrolling at 150 percent on 1080p

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"; question 15)
- **Status:** done (measured in a render harness on Windows; not yet seen in Inventor)
- **Depends on:** 114, 115 (they add rows; this task lays out the final set)
- **Needs:** windows build
- **Parallel-safe with:** 116, 117

## Goal

Aidan, 2026-10-24 (question 15): the common holes fit at 150 percent scale on a 1080p display without scrolling. Scrolling stays as the fallback for tall holes. The diagram stays: Aidan really likes it.

## Scope

- `src/InventorAddin/UI/AwbHoleWindow.xaml(.cs)` and this window's own styles only (spec 08's shared spacing stays for the other windows):
  - a smaller diagram, still collapsible, its state remembered;
  - tighter row and section gaps in this window;
  - the centres as one line ("3 picked", the help in its tooltip) at the end of the depth and direction rows, with no Centres section (question 15 b);
  - the Units toggle on the header's line (question 15 c).
- Measure as task 104 did (the working area of a 1080p screen at 150 percent, about 690 tall) and record in Verification the height each of these needs, diagram open and collapsed: a blind clearance hole, a blind tapped hole, a counterbored blind tapped hole, a dowel with a counterbore, a toleranced clearance hole, a Simple hole.
- Update `docs/ui-mockups/README.md` if it describes the layout.

## Acceptance criteria

- [x] A new blind clearance hole and a blind tapped hole fit without scrolling with the diagram open at 150 percent on 1080p; the measured heights are in Verification.
- [x] Taller holes scroll, and the footer with OK and Cancel stays visible.
- [x] Keyboard access keys and tab order still work; the window passes the XAML rules test.
- [x] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

- **Ran (Windows, Inventor 2026 installed, Inventor not running):**
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings.
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors. The 4 warnings were there before (CS8600 in the Create Print, New Part, Save As Copy and Save As Replace commands).
  - `dotnet test tests/InventorAddin.Core.Tests`: 4349 tests, 2 of them new. One full run passed all 4349. Three other full runs had 2, 3 and 8 failures, all in the file-system and timing tests under `Numbering` (`RegisterStoreTests.RealEnvironment_...`, `RenamePlanTests.ListFilesToSearch_ASlowListing_...`, `SaveAsReplacerTests.Replace_NameThatCannotBeSet_...`). These are the known flaky tests listed in `TODO.md`, and this task does not touch them. The XAML rules test passes in every run.
- **How the heights were measured.** A render harness in the session scratchpad (not committed) built the real `AwbHoleWindow` from the built add-in, with the starter workbook, three centres picked, and the theme dictionaries merged as `WindowHost` merges them. It showed each window sized to its content and read its height. It ran on this PC at 150 percent display scale, where the title bar and frame take 37.3. A 1080p screen at 150 percent leaves 690 for the window, so the body can show 526 of content. Each figure below is the whole window, title bar included. A height of 690 or less fits without scrolling.

  | Hole (new, starter workbook) | Diagram open | Diagram collapsed |
  |---|---|---|
  | Blind clearance, M2 or M6 (Fit, Toleranced, Depth, Drill point) | **679, fits** | 575 |
  | Blind tapped, M2 or M6 (Thread depth, Drill depth) | **657, fits** | 553 |
  | Counterbored blind tapped, M6 (Fastener type, both Cbore boxes) | 759, scrolls | 655, fits |
  | Dowel with a counterbore, blind, 6 mm (Cbore boxes, Fit with colour, Tolerance) | 759, scrolls | 655, fits |
  | Dowel with a counterbore, through all | 693, scrolls | 589, fits |
  | Toleranced clearance, through all (Tolerance row) | 647, fits | 543, fits |
  | Toleranced clearance, blind | 713, scrolls | 609, fits |
  | Simple, through all, Diameter still empty (its problem line showing) | 561, fits | 457 |
  | Simple, blind, Diameter empty (two problem lines) | 643, fits | 539 |
  | Clearance, through all (the window as it opens) | 613, fits | 509 |
  | Tapped, through all, Full depth unticked | 613, fits | 509 |

  Before this task, a blind clearance hole needed 935 with the diagram open and 801 collapsed, and a blind tapped hole 909 and 775, measured the same way. Collapsing the diagram now saves 104 (it saved 134). A problem line under a box adds about 16.
- **The window scrolls with the footer in view.** The harness also capped each window at 690 and saved images in the dark and light themes. A counterbored blind tapped hole and a blind dowel scroll, with the header, the line above the footer, and OK and Cancel all in view. When the content fits, no scroll bar shows.
- **Fit appearing and disappearing on a seated clearance hole (task 137).** A clearance hole with a counterbore or countersink was measured at every starter size, diagram open. Each size with a seat row shows Fastener type and is 681 tall. M2 and M2.5 have no row: they show Fit, and the empty seat boxes have their two problem lines, so they are 713 tall. Fastener type and Fit are the same height, so the swap itself does not change the window's height. Only the problem lines do (+32). Going past 690, the window is capped and the body scrolls. `WindowHost` keeps the window on the screen as it grows. The seat boxes move up one row when Fastener type (above them) gives way to Fit (below them); see Follow-ups.
- **Keyboard (harness).** Tab order for a blind clearance hole: Units, Type, Seat, Diagram header, Standard, Size, Fit, Toleranced, Termination, Depth, Drill point, Flip, OK, Cancel. It was also checked for a blind dowel with Custom tolerance, a Simple toleranced hole with a spotface, and a Through All tapped countersunk hole with Full depth unticked: every box in order, Units first. Each label's access key reached its control: A, B, C, E, G, H, I, K, L, M, N, O, P, R, S, T, W, Y and Z. Real Alt+U, Alt+T, Alt+R, Alt+Y and Alt+L key presses, sent to the window while it was in front, moved the focus to Units, Tolerance, Termination, Type and Flip.
- **Not compiled (changed under `src/InventorAddin`):** none. Everything changed was built on Windows: `UI/AwbHoleWindow.xaml` and `.xaml.cs`, `UI/Controls/DialogHeader.cs` and `UI/Theme/Styles.xaml`. None of it has run inside Inventor.
- **Inventor API members not confirmed:** none. No Inventor API member was added or changed.
- **What changed:**
  - `UI/Theme/Styles.xaml`:
    - `CompactDialogContent` gives this window's rows, sections and header tighter styles through its `Style.Resources`: `CompactFieldRow` (4 between rows), `CompactFormSection` (6 under a heading, 10 between sections) and `CompactDialogHeader` (12 under the header). The content margin is 14 at top and bottom. Other windows keep spec 08's spacing.
    - The `FooterDivider` has 10 under it.
    - Icon buttons are 40 by 28.
    - Units has 10 either side of each name.
    - The diagram is 104 tall, its header 22, with 4 under it.
    - The shared `DialogHeader` template has a third column for a `Trailing` control, collapsed when there is none.
  - `UI/Controls/DialogHeader.cs`: a `Trailing` property, kept as a logical child so bindings and names work.
  - `UI/AwbHoleWindow.xaml`:
    - Units is the header's `Trailing` control, with no label and the tooltip "Units (Alt+U)".
    - The Units section and the Centres section are gone. Centres is the last row of Depth and direction: "3 picked", with the help in the row's tooltip, the note after it, and the problem under it.
  - `UI/AwbHoleWindow.xaml.cs`: Alt+U focuses the chosen Units part. Registering the list with `AccessKeyManager` did not work: the harness showed the key was not taken.
  - Core:
    - `HoleDiagram.DefaultHeight` is 104 (was 134).
    - `AwbHoleViewModel.CentresNote` and `CentresFixedNoteText` are new: "cannot be changed" after the count when the centres are not sketch points. The longer reason stays in `CentresHelp`, the tooltip.
    - Two tests in `AwbHoleWindowViewModelTests`.
  - Docs:
    - Spec 08's AWB Hole layout, and its header band (`Trailing`).
    - `docs/ui-mockups/README.md`, "Changed by task 118".
- **Manual checklist for Inventor** (build with Inventor closed; Windows display at 150 percent, ideally on a 1080p screen; any part with a sketch holding a few points):
  1. Press **AWB Hole**. The header reads "AWB Hole" and the part's name, with the **Metric | Imperial** toggle on the same line at the right. There is no Units row in the body and no Centres section.
  2. With the diagram open, choose **Distance** in Termination. The whole window, OK and Cancel included, is on the screen with no scroll bar in the body. Choose **Tapped**, keep Distance: still no scroll bar.
  3. Choose Seat **Counterbore** on the tapped hole, with a size that has a counterbore row (for example M6). The body now scrolls with the thin scroll bar, and the header, the line above the footer, OK and Cancel stay in view. Collapse the diagram: the scroll bar goes.
  4. Collapse the diagram, then Cancel and press AWB Hole again: it is still collapsed. Open it again for the next steps.
  5. The last row of Depth and direction reads **Centres  3 picked** (or the number you have). Hover over it: the tooltip says how to pick. Click points in the model: the count follows. Unpick them all: "None picked", with the problem under it in red, and OK disabled.
  6. Edit a hole whose centres are not sketch points, if you have one: the row reads "N picked  cannot be changed", and the tooltip explains why. Edit any hole: the header reads "Edit AWB Hole" in full, with the toggle after it.
  7. Keyboard: the window opens with focus on the chosen Units part. Tab goes to Type, Seat, the Diagram header, then the boxes in order, Flip, OK and Cancel. Alt+U from anywhere goes back to Units. Spot-check Alt+Y, Alt+E, Alt+Z, Alt+P and Alt+L.
  8. Choose Clearance, Seat Counterbore, and move Size between M3 and M2 (M2 has no counterbore row in the starter workbook). Fastener type and Fit swap. The window grows only by the two problem lines under the empty boxes, and it stays on the screen.
  9. Tick **Toleranced** on a clearance hole: the checkbox sits in the value column, under Fit's dropdown, and Tolerance appears under it.
  10. Switch Inventor to the other theme and open the window again. The header toggle, the tighter rows and the diagram read well in both themes. The Settings and Part Properties windows keep their usual, roomier spacing.

## Defaults

These were chosen to keep the work moving. Aidan can overturn any of them.

- **The spacing in this window:** 4 between rows, 6 from a heading to its first row, 10 between sections, 12 under the header, 14 above and below the content, and 10 under the footer's divider. Spec 08 uses 9, 9, 18, 18, 18 and 12. Every gap is even, so at 150 percent each is a whole number of pixels.
- **The diagram is 104 tall** (128 with its header and frame), down from 134 (160). The drawing scales into the smaller box. The text stays at 11.
- **Icon buttons are 40 by 28** (were 40 by 32).
- **Units has no label on the header's line.** With the label, "Edit AWB Hole" was cut short. Alt+U still works, and the toggle's tooltip says "Units (Alt+U)". There is no underline cue for it.
- **The centres row:** the label "Centres", then "3 picked". How to pick is in the row's tooltip. For centres that are not sketch points, a muted "cannot be changed" follows the count, and the tooltip gives the reason. The problem when none is picked shows under the row.
- **Toleranced and Fit stay on two rows.** The spec gives Toleranced its own row. The two would only fit on one line if the Fit dropdown were narrowed.

## Follow-ups

- **The seat boxes move up one row when Fastener type gives way to Fit** on a seated clearance hole whose size has no seat row (task 137): Fastener type sits above the seat boxes and Fit below them. The window keeps its height, but the boxes shift. Putting Fit in Fastener type's place for this case, or keeping a row's space, would stop the shift. Ask Aidan whether it matters.
- **Still scrolling with the diagram open:** a counterbored blind tapped hole (759), a blind dowel with a counterbore (759) and a toleranced blind clearance hole (713). All of them fit with the diagram collapsed. If Aidan wants these to fit as well, the remaining options are Fit and Toleranced on one row, a smaller diagram, or dropping the "Hole" heading.
- **A screenshot of the built window** next to the mockup in `docs/ui-mockups/` could replace the mockup image, which no longer matches. The harness can produce one.
- **The 690 figure assumes the title bar is 37.3 tall**, as measured on this PC at 150 percent. In Inventor on a 1080p screen, check step 2 of the checklist. A blind clearance hole has 11 to spare.
