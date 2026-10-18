# 102: AWB Hole window redesign: mockup for Aidan

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); `docs/features/08-window-style.md`
- **Status:** todo
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

- **Ran:**

## Follow-ups
