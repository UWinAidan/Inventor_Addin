# 102: AWB Hole window redesign: mockup for Aidan

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); `docs/features/08-window-style.md`
- **Status:** done (approved by Aidan 2026-10-23 with changes; revised)
- **Depends on:** 098, 099, 100 (so the mockup shows the fields as they now are)
- **Needs:** cloud or windows (an image only)
- **Parallel-safe with:** 091, 092

## Goal

Aidan, 090 check: the window should be closer to Inventor's Hole panel. Before anything is built, a mockup in the add-in's dark look for Aidan to approve.

## Scope

- `docs/ui-mockups/awb-hole-window.png` (and its source, for example an HTML page rendered by headless Edge, or a script; keep the source beside it so it can be changed after Aidan's comments)
- `docs/ui-mockups/README.md` entry
- Nothing under `src/`

## What the mockup shows

- Compact, about as wide as Inventor's Hole panel, in the add-in's dark look (spec 08's colours, fonts and spacing; the header with the icon, the footer with status, OK and Cancel).
- **Icon buttons** in place of dropdowns for Type (Clearance, Tapped), Seat (None, Counterbore, Countersink), Termination (Distance, Through All) and Drill point (118 deg, 90 deg, Flat), each a row of small toggle buttons with our own simple line icons (drawn by us; never copied from Inventor or any other software), the chosen one highlighted, a tooltip naming it.
- **A live diagram of the hole** in section, drawn from the current values: the hole outline with its seat and bottom, and dimension lines with values for the diameter, depth, thread depth (tapped), counterbore diameter and depth or countersink diameter and angle, and the drill point angle. Two or three small panels beside the main one show how the diagram changes (a blind tapped hole; a counterbored through hole; a countersunk hole).
- The other fields kept compact: Units, Standard, Fastener type, Size, Fit with its colour, Tolerance (task 098's text), the depth boxes (with an expression shown, task 100), Flip, Centres.
- A short note under the image naming each choice that is ours to make, so Aidan can comment on each.

## Notes from earlier tasks

- For tapped and dowel holes the Fastener type list depends on the size (task 099): consider putting Fastener type under Size.

## Acceptance criteria

- [ ] The image is readable at 100 percent and shows every field the window has after 101.
- [ ] Nothing in it is copied from Inventor's or anyone's screenshots or icons; `ADDIN_PICS/` is not used.
- [ ] Aidan has approved it or asked for changes before task 104 starts.

## Revision after Aidan's comments (2026-10-23)

Aidan approved choices 1 to 11 as shown, the 360 width and the `= 31 mm` value. Before task 104 the mockup and README change:

- **A. Fits the screen** at 150 percent scale on 1080p (working area about 690 tall): the diagram smaller (no taller than about 160) and collapsible, with a collapse control on its header (show it expanded in the main window and collapsed in one extra small view, or say so in the README); the body scrolls with a themed scroll bar; the header and the footer with OK and Cancel always visible. Draw the main window at that height, with the body scrolled to show it does.
- **B. Clearly different icons:** Termination Distance must not look like Drill point 118 deg (for example a blind hole with a depth arrow and a flat stop for Distance, against a cone tip close-up for the drill points); 118 deg and 90 deg at clearly different cone angles; Tapped with visible thread lines, Clearance plain; To distinct from both.
- **C. Dowel seats** (task 106): the main window is a dowel with a counterbore, so Fastener type goes and editable Counterbore diameter and Counterbore depth boxes appear (the depth able to show an expression and its value); add a small panel for a countersunk dowel's Countersink diameter and angle boxes. Update the diagram values to match.
- README: update choices 2, 3, 4 and add the new ones, mark choices 1 to 11 as approved (Aidan, 2026-10-23), and say which parts are new since then.

## Verification

Filled in by the implementer.

- **Ran:** headless Edge (`msedge --headless --screenshot`, 1000 by 1190, scale 1) on `docs/ui-mockups/awb-hole-window.html`, giving `docs/ui-mockups/awb-hole-window.png`; viewed the PNG at 100 percent after each render and fixed what was hard to read (icons redrawn with shaded material, the tooltip moved off the diagram and the Flip label, the thread label moved inside the panel). `dotnet build src/InventorAddin.Core`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 3382 passed, 0 failed.
- **Made:** `docs/ui-mockups/awb-hole-window.html` (source; the render command is in a comment at its top, and the diagrams are drawn by its script from each hole's values), `docs/ui-mockups/awb-hole-window.png`, and an entry in `docs/ui-mockups/README.md` with the 11 numbered choices for Aidan to comment on.
- **Fields shown, checked against `AwbHoleWindow.xaml` and `AwbHoleViewModel` after task 101:** Units, Type, Seat, Standard, Size, Fastener type, Fit with its colour, Tolerance (task 098's `H7  +0.012 / 0`), Termination, Depth (with an expression, task 100), Drill point, Direction (Flip), Centres with its help, the footer status, OK and Cancel; on the right, Thread depth and Drill depth with a problem under a field (`HoleExpressions.NotALengthText`), and Termination To with `ToFaceNoteText`. The note for a hole made by Inventor's Hole command (top of the body) is not drawn; the README says it stays where it is.
- **Nothing copied:** `ADDIN_PICS/` and other software's screenshots were not opened. The icons, the diagrams and the header icon (our `AwbHole.svg`, strokes lightened for the dark tile) are ours. Values are made up; the part is `Bracket.ipt`.
- **Not compiled:** nothing under `src/` changed.
- **Inventor API members not confirmed:** none used.
- **Preview:** the README says the 3D preview stays solid red (Aidan, this session; spec 09 question 13).
- **Revision after Aidan's comments (2026-10-23):** rewrote `awb-hole-window.html` and re-rendered the PNG (headless Edge, 1340 by 1110, scale 1); viewed it at 100 percent and checked the icons at 3x zoom. A: the window drawn 690 tall twice (body scrolled to the top, then to the bottom) with a fixed header and footer (divider above the footer) and the themed scroll bar; the diagram is 160 tall with its header and collapsible, shown open in the windows and collapsed in its own panel. B: every icon redrawn (Tapped with saw-tooth walls, Distance a narrow flat-stopped hole with a depth dimension beside it, drill points as bottom close-ups with 118 shallow and 90 steep, To ending on a thick face line); material fill raised to 45 percent so the shaded parts read. C: Fastener type replaced by Cbore dia and Cbore depth (`0.25*d0+1.5`, `= 6.5 mm`) in the main window, a countersunk dowel panel with Csink dia and Csink angle, Fastener type shown in the counterbored clearance panel, and the diagram values updated to match. README: choices 1 to 11 marked approved, choices 2, 3 and 4 marked changed, new choices 12 to 15 added; green badges in the image mark the new ones. The icon paths use only M/H/V/L/Z, and the diagram's mm geometry (`holeProfile`) is separate from its drawing, for task 104. Access keys for the new boxes: A, B (counterbore), K, G (countersink); none clash with the window's keys. `src/`, STATUS, TODO, DECISIONS, PLAN and the spec unchanged.
- **Manual check for Aidan:** open `docs/ui-mockups/awb-hole-window.png` (or the HTML in a browser) at 100 percent and comment on choices 1 to 11 in the README by number. The task's last acceptance box (approval before 104) is his.

## Follow-ups

- ~~Window width~~: settled, Aidan kept 360 (2026-10-23).
- ~~The `= 31 mm` value~~: approved (2026-10-23); task 104's scope already has it on the view-model.
- The collapsed diagram's summary line ("⌀6 H7, 31 deep, counterbore ⌀11 by 6.5") is a wording default for task 104's diagram model; Aidan has not seen it before this revision.
