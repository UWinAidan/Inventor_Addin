# UI Mockups

Drop screenshots / sketches of the desired UI here. Name them so they sort and
describe the screen, e.g.:

- `01-ribbon-tab.png`
- `02-part-number-dialog.png`
- `03-material-finish-library.png`
- `04-hole-table-tool.png`

If a picture needs explanation (behavior, which fields are editable, where data
comes from), add a matching `.md` file with the same name.

## AWB Hole window (task 102)

![AWB Hole window mockup](awb-hole-window.png)

`awb-hole-window.png` is rendered from `awb-hole-window.html` with headless Edge. The command is in a comment at the top of the HTML. Every icon and diagram was drawn for this mockup; nothing is taken from Inventor or any other software. The look is spec 08's dark set: its colours, Segoe UI 13, a 100-wide label column, 30-high controls, 9 between rows and 18 between sections. All values are made up.

**For task 104:**
- Each icon is one or two path strings in a 20 by 20 box, using only M, H, V, L and Z, so it ports to a WPF geometry as it is.
- The diagram script works the outline out in millimetres (`holeProfile`) before it maps anything to pixels. Its dimension helpers show which dimensions each kind of hole gets.

**What the image shows:** the window drawn at its height on a 1080p screen at 150 percent, 690 tall. The hole is a new blind dowel with a counterbore (AWB Dowel, 6 mm, Press, H7, Cbore depth `0.25*d0+1.5`, Depth `1.5*d0+1`, 118 deg, three centres). It is drawn twice:
- with the body scrolled to the top;
- with the body scrolled to the bottom.

The other panels show:
- the diagram collapsed;
- the Termination row of an edited hole that ends at a face;
- the depth rows of a blind tapped hole, with a problem under a field;
- the diagram for a blind tapped hole;
- a counterbored clearance hole, with its Fastener type row;
- a countersunk dowel hole, with its seat boxes.

The note shown at the top of the body for a hole made by Inventor's Hole command is not drawn. It stays where it is today.

### Approved by Aidan on 2026-10-23 (yellow numbers)

Choices 1 to 11 were approved as first shown, along with the 360 width and the `= 31 mm` value. Choices 2, 3 and 4 have changed since then, following his comments, and are marked **changed**.

1. **Units** is a two-part toggle (Metric, Imperial) on its own row above the sections, in place of a dropdown.
2. **Icon buttons** for Type, Seat, Termination and Drill point.
   - Each button is 40 by 32, icon only, with a tooltip giving the name (shown on the 90 deg button). The icons show the part in section, with the material shaded and the hole left empty.
   - The chosen button has the icon tile fill and a lighter border, not the accent colour. The Seat button shows the focus outline.
   - The chosen name is repeated in muted text after the buttons.
   - **Changed: the icons are redrawn so that no two look alike.**
     - Clearance has straight walls; Tapped has saw-tooth thread walls.
     - Distance is a narrow blind hole with a flat stop and its depth dimensioned beside it.
     - Through All is a hole through the part with the arrow leaving the bottom.
     - To goes through one plate to the face of the next, with that face as a thick line.
     - The drill points are close-ups of the bottom with the walls wide apart: 118 deg has a shallow cone, 90 deg a steep one, and Flat a flat bottom.
3. **The diagram** sits in the Hole section under Type and Seat, at the full width of the content.
   - It is a section view: the part hatched, the hole outline, a centre line, and dimension lines with their values.
   - It shows the diameter with its tolerance, the depth, the seat, the thread (tapped) and the drill point. A value typed as an expression has a small *fx*.
   - It is not drawn in the fit colour, and it follows the theme.
   - **Changed: it is smaller, 160 tall with its header (the drawing itself 134), and it can be collapsed (see 12).**
4. **Fastener type goes under Size.** **Changed: it shows only for a clearance or tapped hole with a seat** (shown in the counterbored clearance panel). A dowel with a seat has its own seat boxes instead (see 14 and 15).
5. **The fit colour** sits on Fit's own row, beside a narrower dropdown.
6. **Depth boxes** show the text as typed, the unit after the box, and the value it comes to in muted text (`= 31 mm`) when the text is an expression.
7. **Direction** stays a Flip checkbox.
8. **Centres** is unchanged: the count, then how to pick.
9. **Footer** is unchanged: the status line (its tooltip lists every note), OK and Cancel. The 3D preview in the model stays solid red (Aidan, spec 09 question 13); the window says nothing about it.
10. **A blind tapped hole** shows Thread depth and Drill depth in place of Depth. A field's problem stays under it in the error colour.
11. **To** is a third Termination button only for an edited hole that already ends at a face, with the note under it, as today.

### New since 2026-10-23 (green numbers)

12. **The diagram can be collapsed.**
    - Its header line works like a collapsible section's heading: a chevron and "Diagram" when open. When collapsed, it adds a one-line summary such as "⌀6 H7, 31 deep, counterbore ⌀11 by 6.5".
    - Clicking the header opens or closes it. Collapsing it makes the window 134 shorter.
    - The state is remembered across windows and restarts, and it starts open.
13. **The window fits a 1080p screen at 150 percent.**
    - The window is at most 690 tall (the working area).
    - The header and the footer with OK and Cancel stay fixed.
    - The body between them scrolls with the themed scroll bar (8 wide, in the right margin).
    - The footer has a divider line above it, so the scrolled body has a visible edge.
14. **A dowel with a counterbore** has editable **Cbore dia** and **Cbore depth** boxes under Size, in place of Fastener type (task 106).
    - The depth box takes expressions and shows its value, like the other depth boxes.
    - The diagram's counterbore values come from these boxes.
    - Access keys: A and B.
15. **A countersunk dowel** has **Csink dia** (mm) and **Csink angle** (deg) boxes in the same place. Access keys: K and G.

The labels follow the Dowel tab's column names, shortened to fit the 100-wide label column.
