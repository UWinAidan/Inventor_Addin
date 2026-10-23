# 104: AWB Hole window redesign

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); `docs/features/08-window-style.md`
- **Status:** todo
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

- [ ] The layout follows the revised mockup: Units toggle; Hole section with Type and Seat icon rows (chosen name in muted text after them), the diagram, Standard, Size, Fastener type under Size (clearance and tapped seated holes only), the dowel seat boxes (task 106), Fit with its colour on the same row, Tolerance; Depth and direction with Termination icon row, Depth (or Thread and Drill depth for blind tapped holes), Drill point icon row, Flip; Centres; footer with status, OK and Cancel. Every field and message the window has today is still there; fields that do not apply are hidden as now; access keys kept.
- [ ] Icon buttons: about 40 by 32, icon only, a tooltip naming each, the chosen one with the icon tile fill and a lighter border (not the accent), keyboard reachable and selectable (arrow keys or Space), and screen-reader names. They bind to the existing view-model options; nothing about which options are offered changes.
- [ ] The icons are clearly different (Aidan): Termination Distance does not look like Drill point 118 deg; 118 deg and 90 deg show clearly different cone angles; Tapped shows visible threads, Clearance does not; To (edited holes ending at a face) is distinct from both other terminations. Drawn by us, light and dark themes both legible.
- [ ] The diagram is drawn from the current values and redraws on every change: outline, seat, thread with its size (tapped), bottom (118, 90, flat, none for through), the dimension values for diameter (with tolerance for a dowel), depth (with *fx* when an expression), thread depth, seat diameter and depth or angle, and the drill point angle. It follows the light or dark theme and is not drawn in the fit colour.
- [ ] The diagram is smaller than in the first mockup (no taller than about 160) and collapsible from its header; the state is remembered across windows and restarts.
- [ ] Fits the screen at 150 percent scale on a 1080p display (working area about 690 tall): the window's height is capped to the working area, the body scrolls (themed scroll bar) and the header and the footer with OK and Cancel are always visible. With the diagram collapsed, the common cases (blind clearance, blind tapped) need no scrolling at that size.
- [ ] 360 wide, spec 08's look (colours, fonts, spacing, label column), light and dark.
- [ ] A depth box holding an expression shows `= <value> <unit>` in muted text after the unit; plain numbers show nothing.
- [ ] The XAML rules test passes; `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds.

## Notes for the implementer

- Approved by Aidan: choices 1 to 11 as shown, 360 wide, `= 31 mm` kept. His changes A (fit the screen; collapsible, smaller, remembered diagram; scrolling body, fixed footer), B (distinct icons) and C (dowel seats, task 106) are in `DECISIONS.md` rows dated 2026-10-23. The mockup HTML holds the icon drawings and the diagram script; port their shapes, do not copy anyone else's.
- Keep every decision in Core: which dimensions the diagram shows and their text belong in the diagram model, so the control only draws lines, polygons and labels.
- Existing pieces to reuse: the themed scroll bar (task 080), the screen cap of windows (task 077), the collapsible section style of Settings (task 079), `WindowHost`.
- Task 098's Tolerance item template hides the row name in the closed box with a `ComboBoxItem` ancestor binding that fails on purpose (a harmless binding trace). A `SelectionBoxItemTemplate` or a template selector in `UI/Theme` would be cleaner; check the closed box is wide enough for `G7  +0.016 / +0.004`.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups
