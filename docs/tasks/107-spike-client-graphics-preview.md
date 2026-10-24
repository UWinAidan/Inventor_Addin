# 107: Spike: the AWB Hole preview drawn with client graphics

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window": live 3D preview; question 13)
- **Status:** todo
- **Depends on:** 101
- **Needs:** **windows** (spike, with Aidan in Inventor)
- **Parallel-safe with:** 103, 108 (other spikes, separate branches)

## Goal

Aidan, 2026-10-24: the solid red paint of task 101 looks like a finished dowel hole. The preview must match Inventor's own Hole preview: a faint, see-through red copy of the hole volume drawn over the model with client graphics, end caps included, so a through hole looks red when seen straight down the bore. No faces are painted. Find out how to draw that through the API before task 110 builds it. Nothing from this spike is merged.

## Questions

1. **Where the volume comes from.** Options, in order of preference: (a) a solid built with `TransientBRep` from the hole's geometry (cylinder, seat, drill point cone) that Core already works out for the diagram (`HoleGeometry`); (b) the faces of the real hole feature, copied to transient faces; (c) the tool body Inventor removes, if the API exposes it. Which gives closed end caps on a through hole and follows a seat and a drill point?
2. **Drawing it.** `ComponentDefinition.ClientGraphicsCollection.Add`, a `GraphicsNode`, `SurfaceGraphics` from the transient body; the colour and opacity (`GraphicsNode.Appearance` or a `Color` with `Opacity`, `GraphicsNode.RenderStyle`); whether it is drawn see-through in the shaded view (task 101 found face appearances drawn opaque), and the opacity that reads as "faint" next to Inventor's own (start at 0.3).
3. **Over the model.** With the real hole cut (the preview is still the real feature, spec 09), does the volume show through the part's faces where it is inside the part, or only where it is not hidden? Inventor's own preview draws it over the model; try `GraphicsNode.DepthPriority`/`OverlayGraphics` or the equivalent if it is hidden.
4. **Speed.** Rebuilding the graphics on each change on top of the 30 to 60 ms the feature change takes: is it still smooth? Is it faster to transform one node per centre, or one body with all centres?
5. **Undo and transactions.** Client graphics are not in the undo list: confirm that adding and deleting them inside the session's outer transaction leaves nothing behind after OK, Cancel and Undo, and that a crash leaves nothing saved in the file.
6. **Removing the paint.** Confirm nothing of task 101's paint transaction is needed once the graphics draw the preview.

## How

- A spike branch `spike/107-client-graphics`, off `claude/m4-hole-generator`, with a temporary build that draws the preview each way and logs timings to `awbaddin.log`. Aidan compares it with Inventor's own Hole preview, on a through hole, a blind hole with a drill point, and a counterbored hole, in the light and dark themes. Results go in this file.
- Check every member against the Inventor API reference before trying it (`CLAUDE.md`).

## Results

Filled in after the rounds with Aidan.

## Recommendations

Filled in after the rounds with Aidan; task 110 is written from them.
