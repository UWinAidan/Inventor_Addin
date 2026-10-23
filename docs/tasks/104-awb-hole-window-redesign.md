# 104: AWB Hole window redesign

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); `docs/features/08-window-style.md`
- **Status:** done (common holes still scroll at 150 percent; spec 09 question 15)
- **Depends on:** 102 (approved by Aidan 2026-10-23, with changes), 101, 106
- **Needs:** windows build
- **Parallel-safe with:** 105

## Goal

Build the AWB Hole window as Aidan approved it in task 102's mockup (`docs/ui-mockups/awb-hole-window.png`, choices 1 to 11 in `docs/ui-mockups/README.md`), with his changes of 2026-10-23: it fits a 1080p screen at 150 percent scale, the diagram is smaller and collapsible, and every icon is clearly different.

## Scope

- Core: a hole diagram model (`src/InventorAddin.Core/Holes/HoleDiagram.cs` or similar): the part's section, the hole outline with seat, thread and bottom, the centre line, and dimension lines with their label text, worked out from the view-model's current values in diagram units, with no WPF types. The `= value` text for a depth typed as an expression (choice 6) on the view-model. The diagram's expanded state in `AddinSettings` (default expanded) and its round-trip. Tests for all of it.
- Add-in: `UI/AwbHoleWindow.xaml` and its code-behind; a control in `UI/Controls/` that draws the diagram model with the shared theme's brushes; the icon toggle-button style and a Units two-part toggle in `UI/Theme/` if shared, else local; the icons as XAML vector geometry (our own drawings, following the revised mockup's icons).
- Spec 08: the AWB Hole window's layout section updated to match.

Out of scope: dowel seat behaviour (task 106 builds it; this task only lays its boxes out as the mockup shows), the 3D preview (stays solid red), in-place edit (105).

## Acceptance criteria

- [x] The layout follows the revised mockup: Units toggle; Hole section with Type and Seat icon rows (chosen name in muted text after them), the diagram, Standard, Size, Fastener type under Size (clearance and tapped seated holes only), the dowel seat boxes (task 106), Fit with its colour on the same row, Tolerance; Depth and direction with Termination icon row, Depth (or Thread and Drill depth for blind tapped holes), Drill point icon row, Flip; Centres; footer with status, OK and Cancel. Every field and message the window has today is still there; fields that do not apply are hidden as now; access keys kept.
- [x] Icon buttons: about 40 by 32, icon only, a tooltip naming each, the chosen one with the icon tile fill and a lighter border (not the accent), keyboard reachable and selectable (arrow keys or Space), and screen-reader names. They bind to the existing view-model options; nothing about which options are offered changes.
- [x] The icons are clearly different (Aidan): Termination Distance does not look like Drill point 118 deg; 118 deg and 90 deg show clearly different cone angles; Tapped shows visible threads, Clearance does not; To (edited holes ending at a face) is distinct from both other terminations. Drawn by us, light and dark themes both legible.
- [x] The diagram is drawn from the current values and redraws on every change: outline, seat, thread with its size (tapped), bottom (118, 90, flat, none for through), the dimension values for diameter (with tolerance for a dowel), depth (with *fx* when an expression), thread depth, seat diameter and depth or angle, and the drill point angle. It follows the light or dark theme and is not drawn in the fit colour.
- [x] The diagram is smaller than in the first mockup (no taller than about 160) and collapsible from its header; the state is remembered across windows and restarts.
- [ ] (Partly met: the height is capped, the body scrolls, the header and footer stay put; the common cases still scroll by about 85 to 100, see Follow-ups and spec 09 question 15.) Fits the screen at 150 percent scale on a 1080p display (working area about 690 tall): the window's height is capped to the working area, the body scrolls (themed scroll bar) and the header and the footer with OK and Cancel are always visible. With the diagram collapsed, the common cases (blind clearance, blind tapped) need no scrolling at that size.
- [x] 360 wide, spec 08's look (colours, fonts, spacing, label column), light and dark.
- [x] A depth box holding an expression shows `= <value> <unit>` in muted text after the unit; plain numbers show nothing.
- [x] The XAML rules test passes; `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds.

## Notes for the implementer

- Approved by Aidan: choices 1 to 11 as shown, 360 wide, `= 31 mm` kept. His changes A (fit the screen; collapsible, smaller, remembered diagram; scrolling body, fixed footer), B (distinct icons) and C (dowel seats, task 106) are in `DECISIONS.md` rows dated 2026-10-23. The mockup HTML holds the icon drawings and the diagram script; port their shapes, do not copy anyone else's.
- Keep every decision in Core: which dimensions the diagram shows and their text belong in the diagram model, so the control only draws lines, polygons and labels.
- Existing pieces to reuse: the themed scroll bar (task 080), the screen cap of windows (task 077), the collapsible section style of Settings (task 079), `WindowHost`.
- Task 098's Tolerance item template hides the row name in the closed box with a `ComboBoxItem` ancestor binding that fails on purpose (a harmless binding trace). A `SelectionBoxItemTemplate` or a template selector in `UI/Theme` would be cleaner; check the closed box is wide enough for `G7  +0.016 / +0.004`.

## Verification

Filled in by the implementer.

- **Ran (Windows, Inventor 2026 installed, Inventor not running):**
  - `dotnet build src/InventorAddin.Core`: succeeded.
  - `dotnet test tests/InventorAddin.Core.Tests`: 3515 passed, 0 failed (3446 before). New: `Holes/HoleDiagramTests`, `Holes/HoleIconsAndDiagramStateTests`, `ViewModels/AwbHoleDiagramViewModelTests`, two settings round-trip tests and a row in the settings equality test. The XAML rules test passes; it now also accepts `CompactDialogContent` as a window's root style.
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded with no new warnings (the CS8600 warnings in the Create Print, New Part, Save As Copy and Save As Replace commands were there before).
  - A render harness in the session scratchpad (not committed) built the real `AwbHoleWindow` from the built add-in with the starter workbook, merged the theme dictionaries as `WindowHost` does, capped it at 690 tall, and saved PNGs in the dark and light themes: a blind dowel with a counterbore (body at the top and at the bottom), a countersunk dowel, a counterbored clearance hole, a blind tapped hole, a blind clearance hole, the diagram collapsed, and an edited hole ending at a face. They match the mockup's layout.
  - The harness also checked the keyboard: the window opens with focus on the chosen Units button; focusing a row moves focus to its chosen button; Right and Left choose the next and previous seat and drill point. The buttons' tooltips and screen-reader names are the choice's text. The diagram section is 160 tall open and 26 collapsed (134 shorter).
  - Measured at 690 tall, the body shows 526. With the diagram collapsed, a blind clearance hole's body is 611 and a blind tapped hole's 627 (with a problem under Drill depth). With it open, add about 134. So the common cases scroll; see Follow-ups and spec 09 question 15.
- **Not compiled (changed under `src/InventorAddin`):** none. Everything under `src/InventorAddin` was built on Windows: `UI/AwbHoleWindow.xaml` and `.xaml.cs`, `UI/Theme/Styles.xaml`, `UI/Controls/ChoiceList.cs`, `UI/Controls/HoleIcon.cs`, `UI/Controls/HoleDiagramView.cs` and `Commands/AwbHoleCommand.cs`. None of it has run inside Inventor yet.
- **Inventor API members not confirmed:** none. No Inventor API member was added or changed. The command change only reads and saves the add-in's settings (`AddinServices.Settings`, `AddinServices.UpdateSettings`, `AddinServices.DataFolder`, `SettingsStore.Save`), as `HoleSession` already does.
- **Manual checklist for Inventor** (build with Inventor closed, then start it; open any part with a sketch holding a few points):
  1. Press **AWB Hole**. The window is about 360 wide. The header and the OK and Cancel row stay put, with a thin line above the footer. If the body is taller than the screen, it scrolls with the thin themed scroll bar. On a 1080p screen at 150 percent, check that the window's bottom is on the screen.
  2. **Units** is a two-part toggle at the top. Click Imperial and back; the Size list changes with it.
  3. **Type** and **Seat** are icon buttons with the chosen name after them in muted text. Hover over each one: its tooltip names it. The chosen one has the tile fill and a lighter border, not blue. Clearance has straight walls; Tapped has saw-tooth walls.
  4. Press Tab from Units: focus lands on the chosen Type button with the blue outline. Right and Left change the type, and Tab leaves the row. Press Alt+E: focus goes to the chosen Seat button.
  5. The **diagram** shows the hole. A clearance hole through all shows its diameter only. Pick Counterbore: the counterbore's diameter shows above it and its depth on the right. Pick Distance in Termination: the depth shows on the left and the drill point angle under the tip. Pick 90 deg, then Flat in Drill point: the cone gets steeper, then the bottom goes flat and the angle disappears. Every change redraws at once.
  6. Collapse the diagram from its header. One line remains, with a summary such as "⌀6.6, 12 deep, counterbore ⌀11 by 6.5". Click Cancel and press AWB Hole again: the diagram is still collapsed. Restart Inventor: still collapsed. Open it again, and it stays open the next time.
  7. Choose Type **Tapped**, Size M6 and Distance. Thread depth and Drill depth replace Depth, and the diagram shows the thread line, "M6x1", both depths and the tap drill. Type `d0+5 mmm` in Drill depth: the error shows under it and OK is disabled.
  8. In **Depth**, type an expression that uses a part parameter (for example `2*d0` in a part with `d0`). A muted `= 40 mm` (its value) appears after "mm", and the diagram's depth shows a small *fx*. A plain number shows nothing after the unit.
  9. Choose Standard **AWB Dowel** and Seat Counterbore. Fastener type is not there; Cbore dia and Cbore depth (Alt+A, Alt+B) are. Fit has its colour square and name on the same row, with Tolerance under it. The diagram's diameter shows the tolerance (for example "⌀6 N7") and is drawn in grey, not in the fit colour. Choose Countersink: Csink dia and Csink angle (Alt+K, Alt+G) appear.
  10. Check the Termination and Drill point icons. Distance (a blind hole with a depth arrow beside it) does not look like 118 deg. The 118 deg and 90 deg icons show clearly different cones. Through All has its arrow leaving the bottom.
  11. Edit a hole that ends at a face (made with Inventor's Hole, To a face). Termination shows three buttons with To chosen, and the note under them.
  12. Switch Inventor to the other theme and open the window again: everything, including the diagram and the icons, reads in both themes.
  13. OK makes the hole as before, and Cancel puts the part back. Enter in a depth box commits the value and does OK when OK is enabled; Esc cancels.

## Follow-ups

- **The common cases still scroll at 150 percent on a 1080p screen** (the "Fits the screen" criterion is only partly met). Built as the approved mockup at spec 08's spacing, the body needs 611 (blind clearance) or 627 (blind tapped) with the diagram collapsed, and only 526 is visible. The mockup's own drawing is as tall. What to change is for Aidan to choose; the options are in spec 09 question 15 (centres on one line, Units in the header, tighter gaps, or accept the scrolling).
- **The dowel seat boxes' access keys changed** to the mockup's (README choices 14 and 15): Cbore dia is A (was C), Cbore depth B, Csink dia K, Csink angle G (was A). The labels are the mockup's short ones.
- **Tolerance closed-box template** (Notes for the implementer): not changed. The closed box is 204 wide, enough for `G7  +0.016 / +0.004` and for the starter workbook's `N7  -0.004 / -0.016` (seen in the render). Replacing the deliberately failing `ComboBoxItem` ancestor binding with a template selector in `UI/Theme` is still open.
- **A hole ending at a face** is drawn in the diagram as a hole through the part, since the window does not know the end face. The summary says "to a face".
- **The diagram's frame has no fill of its own.** The mockup's panel colour (#252A33) is not a spec 08 token, so the frame is a divider-coloured line on the window background.
- The harness PNGs are not committed. If Aidan wants a screenshot of the built window beside the mockup in `docs/ui-mockups/`, the harness can be run again.
