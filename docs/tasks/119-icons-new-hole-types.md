# 119: Icons for Simple, Taper Tapped and Spotface

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); `docs/features/08-window-style.md` (icons)
- **Status:** done
- **Depends on:** 104
- **Needs:** **Python with Pillow** (as task 087)
- **Parallel-safe with:** 110 to 113, 117

## Goal

The window's icon buttons for the two new types and the new seat (Aidan, 2026-10-24), in the style of task 104's icons and each clearly different from the others (Aidan, 2026-10-23): Simple unlike Clearance, Taper Tapped showing tapered threads unlike Tapped, Spotface a shallow wide seat unlike Counterbore.

## Scope

- `tools/icons/render.py` and the icon sources it draws from; the rendered light and dark PNGs; the `HoleIcon.{name}.Material` keys in `Styles.xaml` for the names task 112 adds to `HoleIcons`.
- If task 112 is not done, use the names `TypeSimple`, `TypeTaperTapped` and `SeatSpotface` and tell the parent, so 112 uses them.

## Acceptance criteria

- [x] Three new icons in both themes, at the size the window uses, visibly different from every existing hole icon.
- [x] Every `HoleIcons.All` name has a `Styles.xaml` key (the `TODO.md` follow-up from task 104, folded in here as a Core test).

## Verification

Filled in by the implementer.

- **How the icons are made.** The scope names `tools/icons/render.py` and rendered PNGs, but the hole icons are not PNGs: task 104 drew them as XAML `Geometry` resources in `src/InventorAddin/UI/Theme/Styles.xaml` (`HoleIcon.{name}.Material`, `.Line`, `.Thick`), drawn by `UI/Controls/HoleIcon` in the button's text colour, so one drawing serves both themes. `render.py` only renders the ribbon icons. The three new icons follow task 104's way: geometry keys in `Styles.xaml`, no PNGs, no `.csproj` change. Names are task 112's: `Simple`, `TaperTapped`, `Spotface`.
- **The drawings** (20 by 20, the part in section, material shaded):
  - `Simple`: a wider plain hole than Clearance (walls 1 to 5.5) with its diameter dimensioned across the top of the hole, arrows out to the walls (`.Line`). Clearance has no marks and a narrower hole.
  - `TaperTapped`: Tapped's saw-tooth thread, but the thread line runs on a slant so the hole narrows from 6 at the top to 3 at the bottom.
  - `Spotface`: a seat 2 deep and wide (a 2 wide lip left at the edge), against Counterbore's 5.5 deep step from a 2.5 wide lip.
- **Ran (Windows, Inventor 2026 installed, Inventor not running):**
  - `dotnet build src/InventorAddin.Core`: succeeded.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4113 passed, 0 failed. New in `Holes/HoleIconsAndDiagramStateTests`: every `HoleIcons.All` name has a `HoleIcon.{name}.Material` key in `Styles.xaml`; every `HoleIcon.*` key in `Styles.xaml` is a known name and part (`Material`, `Line`, `Thick`); no two icons have the same drawing. The test reads `Styles.xaml` as plain XML, finding the repository root by `InventorAddin.slnx` above the test output folder, as `RibbonIconsTests` does.
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors; the only warnings are the CS8600 ones in the Create Print, New Part, Save As Copy and Save As Replace commands that were there before.
  - A preview script in the session scratchpad (not committed) read the geometry straight from `Styles.xaml` and drew all 14 icons at 24 px (the size the window shows) and enlarged, in the light and dark colours, with the template's 45 percent fill and 1.2 outline. Checked by eye: Simple unlike Clearance and Through All, Taper Tapped's slanted thread unlike Tapped, Spotface shallow and wide unlike Counterbore and Seat None.
- **Not compiled (changed under `src/InventorAddin`):** none. `UI/Theme/Styles.xaml` was built on Windows. It has not been shown inside Inventor yet.
- **Inventor API members not confirmed:** none. No Inventor API code changed.
- **Manual check in Inventor** (build with Inventor closed, then start it; open a part with a sketch point):
  1. Press **AWB Hole**. The Type row shows four buttons. Hover each: Clearance (plain narrow hole), Simple (wider hole with a small double arrow across its top), Tapped (straight saw-tooth walls), Taper Tapped (saw-tooth walls closing in towards the bottom).
  2. The Seat row shows four buttons: None, Counterbore (deep step), Countersink (cone), Spotface (shallow, wide step).
  3. Choose each one: the chosen one has the tile fill and lighter border, and the icon reads clearly at 100 and 150 percent scale.
  4. Switch Inventor to the other theme and open the window again: the three new icons read in both themes.

## Defaults

- The look of each icon (above) was picked by the implementer within Aidan's rule that they are clearly different (Simple unlike Clearance, Taper Tapped with tapered threads, Spotface shallow and wide). Aidan can ask for a different drawing.

## Follow-ups

- Spec 08's AWB Hole icon sentence ("The AWB Hole window", icon buttons) lists the drawings of task 104 only; add Simple, Taper Tapped and Spotface.
- The task's scope names `tools/icons/render.py` and PNGs for the hole icons; they are XAML geometry. If a review image of the hole icons is wanted in `docs/ui-mockups/`, a small renderer reading `Styles.xaml` (like the scratchpad one) could be added to `tools/icons/`.
