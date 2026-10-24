# 118: The AWB Hole window fits without scrolling at 150 percent on 1080p

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"; question 15)
- **Status:** todo
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

- [ ] A new blind clearance hole and a blind tapped hole fit without scrolling with the diagram open at 150 percent on 1080p; the measured heights are in Verification.
- [ ] Taller holes scroll, and the footer with OK and Cancel stays visible.
- [ ] Keyboard access keys and tab order still work; the window passes the XAML rules test.
- [ ] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

## Follow-ups
