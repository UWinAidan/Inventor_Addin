# Ribbon icons

The add-in's ribbon icons are drawn here as SVG and rendered to the PNG files the add-in loads.

| What | Where |
|---|---|
| Sources, 32 px drawing | `src/InventorAddin/UI/Icons/{Name}.svg` |
| Sources, 16 px drawing | `src/InventorAddin/UI/Icons/{Name}.16.svg` |
| Rendered icons (what the add-in uses) | `src/InventorAddin/UI/Icons/{Name}.{Light\|Dark}.{16\|32}.png` |
| Preview for review | `docs/ui-mockups/ribbon-icons.png` |
| Renderer | `tools/icons/render.py` |

`{Name}` is the icon name from `IconNames` in Core: `PartProperties`, `Settings`, `About`, `ExportModelData`, `ExportLibraries`. `Light` is the set for Inventor's light theme (dark strokes); `Dark` is the set for its dark theme (light strokes).

## Re-rendering

```
python3 tools/icons/render.py
```

Needs Python 3.8 or later and Pillow 10.1 or later (with FreeType, which the usual Pillow wheels include). Nothing else.

It rewrites all 20 PNGs and the preview. Commit the sources, the PNGs and the preview together. The output depends only on the sources and the Pillow version, so a second run gives byte-identical files.

## How it renders

ImageMagick was the first option, but the copy in the cloud session cannot read SVG at all (it hands SVG to `rsvg-convert`, which is not installed), and where it can, the result depends on which SVG library it finds. The 16 px drawings also rely on `crispEdges` (below) being applied in one exact way, which SVG renderers do not agree on. So `render.py` reads the SVG files itself and draws them with Pillow:

- The SVG files are the only place the shapes are defined. Any SVG viewer shows them, in the light-set colour.
- Each icon is drawn at 16 samples per pixel. Strokes are turned into polygons (with miter, bevel or round joins and butt, square or round caps), filled, then averaged down to the target size.
- A source with `shape-rendering="crispEdges"` is not anti-aliased: a pixel is on when at least half of it is covered. All the 16 px drawings use it, so their diagonals and circles are clean pixel steps instead of grey blur.
- Every stroke or fill colour in a source is replaced by the colour of the set being rendered. Each PNG therefore has exactly one colour, with transparency as its only other variation.
- A PNG is RGBA (32-bit) with a transparent background and no metadata, colour profile or dpi.
- The script stops with an error if any part of a drawing, including half the stroke width, lies outside the frame.

If a `{Name}.16.svg` is missing, the 32 px drawing is halved and drawn with a 1 px stroke. All five icons have their own 16 px drawing, because halving a 32 px drawing puts its lines between pixels.

### SVG features read

Elements `line`, `polyline`, `polygon`, `rect` (with `rx`), `circle`, `path` (commands `M L H V A Z` in either case; arcs without rotation), grouped with `g`. Attributes `stroke`, `stroke-width`, `stroke-linecap`, `stroke-linejoin`, `fill` and `shape-rendering`, inherited from `svg` and `g`. `title`, `desc` and comments are ignored. Transforms, curves (`C Q S T`), dashes, text and gradients are not read; the script stops with an error on an element it does not know.

## Drawing rules

- **Our own drawings.** Do not trace or copy Inventor's icons, or anyone else's.
- **Simple line icons, one weight.** 1 px strokes at 16 px and 1.5 px at 32 px. Filled shapes only for small details such as the dot of the `i`.
- **Frame.** `viewBox="0 0 16 16"` or `"0 0 32 32"`, matching the file. Leave about 1 px clear at the edges; nothing may cross the frame.
- **16 px: on the grid.** Put line centres on pixel centres (`x.5`), use `stroke-linecap="square"` so a line from `1.5` to `8.5` fills pixels 1 to 8 exactly, and keep `shape-rendering="crispEdges"` on the root. Keep diagonals at 45 degrees so they step one pixel at a time.
- **32 px: edges on the grid.** A 1.5 px line cannot be sharp on both sides. Put one edge on a pixel boundary: centre at `n.75` for a line whose edge is at `n`, or at `n.25` for a line whose edge is at `n + 1`. The outer edges of outlines go on pixel boundaries.
- **Colour.** Write `#3B4350` in the sources so they preview correctly. The renderer uses `#3B4350` for the light-theme set and `#E3E7EE` for the dark-theme set. No other colours.
- **Check the preview** after any change. It shows each icon at 16 and 32 px on Inventor's light (`#F3F4F6`) and dark (`#2B303B`) backgrounds, and at 32 px on the window header tile (`#DDE1E7` and `#3A4150`), enlarged four times without smoothing.

## The icons

| Name | Drawing |
|---|---|
| `PartProperties` | A sheet with a folded corner and three lines of text |
| `Settings` | Three sliders |
| `About` | A lower-case `i` in a circle |
| `ExportModelData` | A box in oblique view with an arrow leaving it to the top right |
| `ExportLibraries` | A stack of three books with the same arrow |
