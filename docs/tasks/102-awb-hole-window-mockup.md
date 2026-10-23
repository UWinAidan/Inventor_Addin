# 102: AWB Hole window redesign: mockup for Aidan

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); `docs/features/08-window-style.md`
- **Status:** done (mockup made; Aidan approves it or asks for changes before 104)
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

## Verification

Filled in by the implementer.

- **Ran:** headless Edge (`msedge --headless --screenshot`, 1000 by 1190, scale 1) on `docs/ui-mockups/awb-hole-window.html`, giving `docs/ui-mockups/awb-hole-window.png`; viewed the PNG at 100 percent after each render and fixed what was hard to read (icons redrawn with shaded material, the tooltip moved off the diagram and the Flip label, the thread label moved inside the panel). `dotnet build src/InventorAddin.Core`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 3382 passed, 0 failed.
- **Made:** `docs/ui-mockups/awb-hole-window.html` (source; the render command is in a comment at its top, and the diagrams are drawn by its script from each hole's values), `docs/ui-mockups/awb-hole-window.png`, and an entry in `docs/ui-mockups/README.md` with the 11 numbered choices for Aidan to comment on.
- **Fields shown, checked against `AwbHoleWindow.xaml` and `AwbHoleViewModel` after task 101:** Units, Type, Seat, Standard, Size, Fastener type, Fit with its colour, Tolerance (task 098's `H7  +0.012 / 0`), Termination, Depth (with an expression, task 100), Drill point, Direction (Flip), Centres with its help, the footer status, OK and Cancel; on the right, Thread depth and Drill depth with a problem under a field (`HoleExpressions.NotALengthText`), and Termination To with `ToFaceNoteText`. The note for a hole made by Inventor's Hole command (top of the body) is not drawn; the README says it stays where it is.
- **Nothing copied:** `ADDIN_PICS/` and other software's screenshots were not opened. The icons, the diagrams and the header icon (our `AwbHole.svg`, strokes lightened for the dark tile) are ours. Values are made up; the part is `Bracket.ipt`.
- **Not compiled:** nothing under `src/` changed.
- **Inventor API members not confirmed:** none used.
- **Preview:** the README says the 3D preview stays solid red (Aidan, this session; spec 09 question 13).
- **Manual check for Aidan:** open `docs/ui-mockups/awb-hole-window.png` (or the HTML in a browser) at 100 percent and comment on choices 1 to 11 in the README by number. The task's last acceptance box (approval before 104) is his.

## Follow-ups

- Window width: spec 08's 100-wide label column makes the window about 360 wide, a little wider than Inventor's Hole panel. If Aidan wants it narrower, a smaller label column for this window only is a change to spec 08's rule; record it there if so (README note at the end of the entry).
- The `= 31 mm` value beside a depth expression (choice 6) needs the view-model to expose the evaluated depth as text; task 104 should add it if Aidan approves.
