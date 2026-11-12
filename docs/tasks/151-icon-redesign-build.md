# 151: Icon redesign: window icons, Outline chosen state, ribbon option C, and the 7j AWB Hole icon

- **Milestone:** M1b (window style and icons, spec 08), after M4 part 1 is merged
- **Feature spec:** `docs/features/08-window-style.md`, `docs/features/09-awb-hole.md`
- **Status:** done (built on Windows, not yet run in Inventor)
- **Depends on:** 148 (mockup, approved by Aidan 2026-11-09); PR #29 merged to `main`
- **Needs:** windows build, **Python with Pillow** (icon renders)
- **Parallel-safe with:** none
- **Branch:** a new branch off `main` after PR #29 is merged (Aidan, 2026-11-09). Not on `claude/m4-hole-generator`.

## Goal

Build the icon redesign Aidan approved from the task 148 mockup on 2026-11-09 (`DECISIONS.md`):

- **Window icons:** the new look from `docs/ui-mockups/awb-icons-redesign.png` (section A). The icons are full colour with a solid hatched material fill and a firmer outline. In dark mode they use the blue shown.
- **Chosen state:** the **Outline** version from section B: the input fill with a 2 px dark blue border.
- **Ribbon:** **option C**, an accent colour by panel plus tinted shapes, in the new style (sections D and E).
- **AWB Hole icon:** Aidan's design **7j**, built from his files in `docs/ui-mockups/awb-hole-icon-7j/` (light and dark SVGs at 32 x 32, plus 128 px previews). It is an isometric half-section of a grey block through a counterbored blind hole with a drill point. The hole's interior is light green, it has no edge line, and it is turned toward the cut face. Use those files' shapes and colours exactly, **do not redraw them**. 7j is used for the ribbon button, the split button, the mini toolbar entry and the window header tile.

## Scope

- `src/InventorAddin/UI/Theme/` (`Styles.xaml` and the theme colour dictionaries): the new hole icon tokens and template (hatch clipped to the material), and the chosen state on `IconChoiceItem` and `SegmentedChoiceItem`. Colours come from section C of the mockup (`docs/ui-mockups/awb-icons-redesign.html` holds the hex values).
- `src/InventorAddin/UI/Icons/` and `tools/icons/`:
  - The ribbon icons are redrawn in option C, at 16 and 32 px, light and dark.
  - `AwbHole` is replaced by 7j. The 32 px renders come from Aidan's SVGs. A 16 px version is needed for the small ribbon and split button sizes. Halve 7j only if it stays readable; otherwise draw a pixel-fitted 16 px simplification from the same shapes and colours, and show it to Aidan before shipping.
  - `render.py` gains what it needs: more than one fill colour per icon, and polygons and paths with fills.
  - Strip the `<metadata>` (C2PA manifest) from rendered and shipped copies; keep Aidan's source files as they are.
- The window header tile for AWB Hole uses 7j (`WindowHost`'s `HeaderIcon`).
- Spec 08: answer question 3 (icon style) with this decision; describe the tokens and the chosen state. Spec 09: name 7j as the AWB Hole icon.

Out of scope:

- Icon shapes for the window rows. Section A keeps today's shapes; only colour, fill and outline change.
- Behaviour changes.

## Acceptance criteria

- [ ] Window icons and chosen states match the mockup's Outline version in light and dark, at 100 and 150 percent.
- [ ] Ribbon icons match option C in the new style, light and dark, 16 and 32 px.
- [ ] 7j is the AWB Hole icon on the ribbon button, the split button, the mini toolbar entry and the header tile. Its 32 px render matches Aidan's SVGs.
- [ ] No shipped icon carries the C2PA metadata.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Read task 148's Follow-ups first; they list what this build needs. The mini toolbar and the split button show the icon of our `ButtonDefinition`, so check that they pick up the new pictures, and that the ring (task 142) shows 7j too. The focus ring next to the Outline border must still be visible; check it in Inventor.

## Verification

- **Ran:**
  - `python tools/icons/render.py` (twice; 48 PNGs and `docs/ui-mockups/ribbon-icons.png`), then `python tools/icons/preview_151.py` (`docs/ui-mockups/awb-icons-151-preview.png`). Pillow 12.3.0.
  - Inventor was not running. `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 9 warnings (all existing nullable warnings in the command files; none from the XAML or icon changes).
  - `dotnet test tests/InventorAddin.Core.Tests`: 4569 passed, 0 failed. The `ColourSets_DefineTheSameKeys_ExactlyTheSpecTokens` test lists the three new tokens (`HoleIconInk`, `HoleIconMaterial`, `HoleIconChosenBorder`); `Styles_TypeNoColourOfTheirOwn` passes.
  - No shipped icon carries metadata: no `c2pa` or `<metadata>` in `src/InventorAddin/UI/Icons/*`, and the PNGs are written from a fresh image. Aidan's files in `docs/ui-mockups/awb-hole-icon-7j/` are untouched.
  - The 32 px AwbHole renders were compared with Aidan's 128 px previews (shrunk to 32): mean difference 3.1 of 255, max 29 (anti-aliasing only).
- **Built on Windows, but not run in Inventor.** Nothing under `src/InventorAddin` has been seen working; the build proves it compiles and the XAML parses.
- **Not compiled (changed under `src/InventorAddin`):** none; all of it was built on Windows. Changed: `UI/Theme/Styles.xaml`, `Colors.Light.xaml`, `Colors.Dark.xaml`, the SVG sources and PNGs in `UI/Icons/`. No C# under `src/InventorAddin` changed: `IconLoader`, `WindowHost` (header tile) and `RibbonSetup` load `AwbHole.{Light|Dark}.{16|32}.png` by name, so the new pictures reach the ribbon button, the split button, the mini toolbar entry, the ring and the header tile without code changes.
- **Inventor API members not confirmed:** none used. WPF: `Path.Clip` bound with `TemplateBinding` to a `Geometry` property (the hatch clipped to the material), and a `ControlTemplate` trigger on `IsEnabled` setting `Opacity` of a named `Canvas`. They compile; whether the clip behaves as intended is in the checklist.
- **16 px AWB Hole:** a pixel-fitted simplification, not a halving (halving 7j gives 0.6 px lines that blur). After Aidan's call on the preview it has no interior edge lines: the block's three faces (top, front, right) are told apart by 7j's three greys, the hole is a T-shaped light-green fill (counterbore, bore, drill point) with a lighter green for the ledge and floor, and the only line is a 1 px silhouette outline in 7j's ink colour. Files: `AwbHole.16.svg`, `AwbHole.Dark.16.svg`; the 32 px files are unchanged. The icon PNG names are unchanged, so the csproj's embedded-resource wildcard is unaffected and no rebuild was needed (nothing under `src/` besides these icons changed since the last build).
- **Preview:** `docs/ui-mockups/awb-icons-151-preview.png`. The window buttons in it are a Pillow approximation of the WPF template, not a screenshot.
- **Manual checklist for Inventor** (close Inventor, build with `dotnet build InventorAddin.slnx`, start Inventor):
  1. Open any part. On the Create panel, the AWB Hole button shows 7j (large) in the light theme; switch to the dark theme and restart Inventor: the dark 7j shows. Compare with `docs/ui-mockups/awb-hole-icon-7j/*-128.png`.
  2. Look at the other buttons on every panel (New Part, Save As Copy, Save As and Replace, Part Properties, Mass Update, Material and Finish, Create Print, Export Model Data, Export Libraries, Settings, About; the last two on the AWB panel): coloured by panel (Create green, Properties blue, Drawing orange, Developer purple, AWB teal) with pale tinted shapes, readable in both themes at 32 and 16 px. New Part's plus has a gap around it, not a coloured box.
  3. The split button, with its small 16 px icon: the AWB Hole entry shows 7j. Inventor's Hole entry still shows Inventor's own icon.
  4. In a part, select a sketch point and use the mini toolbar's Hole entry, and right-click for the marking-menu ring (south-east): both show 7j for AWB Hole.
  5. Click AWB Hole. The header tile shows 7j (light and dark).
  6. In the window, rows Type, Seat, Termination, Drill point: every icon is in dark blue on a blue-grey fill with hatch lines (light), pale on a blue fill (dark), none muted. Hatching must stay inside the material (not over the hole).
  7. The chosen button in each row is white (dark: near black) with a 2 px dark blue (dark: light blue) border. Move the mouse over another: a thin grey border. Tab to a row: the blue focus ring shows 3 px outside the chosen button's border and the two read as separate (task 148 follow-up).
  8. Counterbore on Taper Tapped (listed but disabled): the icon is faded to 40 percent.
  9. The Units toggle: the chosen part has a white fill, a 2 px blue border and blue bold text; the other is muted. The border must sit inside the frame and not move the text.
  10. Repeat 6 to 9 at 150 percent display scale (the 24 px icon should stay sharp) and in the other theme.

## Follow-ups

- The 16 px icons (all twelve; AWB Hole as described above) are my own pixel-fitted drawings from the option C look. The mockup only showed them as halves, so Aidan may want changes: the 16 px Material and Finish hatch and Mass Update check mark are the busiest.
- The ribbon Developer and AWB panel accents and the dark ribbon colour (`#3A4150`) are stand-ins taken from the task 148 mockup, not measured from Inventor. If one reads badly on Inventor's real ribbon, change `RIBBON_PANELS` in `tools/icons/render.py` and the SVG sources together.
- `tools/icons/render.py` previews the dark set on `#3A4150`; the old preview used `#2B303B`. The header tile in `ribbon-icons.png` is now drawn on the dark window colour for the dark set.
- The ribbon SVG sources were written by a one-off script that is not kept; to change a palette colour, edit the SVGs and `render.py` by hand (noted in `tools/icons/README.md`).
- `Foreground` is no longer set on `IconChoiceItem`; if a later icon row uses text instead of `HoleIcon`, it needs its own colour.
