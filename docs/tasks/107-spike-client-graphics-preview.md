# 107: Spike: the AWB Hole preview drawn with client graphics

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window": live 3D preview; question 13)
- **Status:** done (round 1 with Aidan, 2026-10-28; the round 2 fix is checked in spike 108's build)
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

Round 1 with Aidan, 2026-10-28, branch `spike/107-client-graphics` (`4969f50`). The build read its variants from `spike107.txt` in the data folder at each preview. Each draw was one `ClientGraphics` on the part's `ComponentDefinition.ClientGraphicsCollection`, one `GraphicsNode`, and `SurfaceGraphics` from a transient body with `Color` (`TransientObjects.CreateColor(r, g, b, opacity)`), `BurnThrough`, `DepthPriority` and `GraphicsNode.OverrideOpacity`.

1. **Where the volume comes from.**
   - `faces` (a solid made with `TransientBRep.CreateSolidCylinderCone` for each cylindrical and conical face of the hole, over the face's length along its axis read from the centres of its circle edges, unioned with `DoBoolean`): Aidan said it looks good overall, faint and see-through like Inventor's, with end caps. It had two bugs, both explained by the log:
     - No red drill point from the side. `CreateSolidCylinderCone` gave E_FAIL each time a cone ran to radius 0 (the apex), so the drill point solid was never made.
     - A counterbore or countersink running off the edge of the part broke where the hole leaves the material. The partial face has arc edges, not circles, so it was skipped ("has no circle edge").
   - `feature` (`GraphicsNode.AddFeatureGraphics(hole)`): E_NOTIMPL, nothing drawn. Ruled out.
   - `copy` (`TransientBRep.Copy` of each face): the drill point is red from the side and a seat off the edge stays whole, but there are no end caps, so a through hole seen straight down the bore is not red. Aidan wants copy's full shape plus end caps: the whole hole tool shape (hole, seat, drill point, capped), matching Inventor's preview in every view.
2. **Drawing it.** `SurfaceGraphics.Color` with the colour's opacity and `GraphicsNode.OverrideOpacity` draws it see-through in the shaded view. Aidan's pick: **opacity 0.25** (0.15, 0.2, 0.3 and 0.5 also tried). The colour drawn was RGB 255, 0, 0.
3. **Over the model.** `BurnThrough = true` draws the whole volume over the part's faces. With `burn=no` it "looks terrible". `DepthPriority` 0 was enough; other values were not needed.
4. **Speed.** 8 to 21 ms per draw in all, 0 to 11 ms of it building the bodies, with a few draws at 80 to 160 ms (the first draw after a change of file or options). It is small next to the feature change. One node with every centre's solids unioned was used throughout, and the per-centre transform was not needed.
5. **Undo and transactions.** Drawn with `AddNonTransacting` inside the session's transactions: OK, Cancel and Undo after OK all leave no red behind. Pass. `transacting=yes` was not needed.
6. **Removing the paint.** Nothing of task 101's paint is needed: the paint transaction was not opened in any `faces` or `copy` draw.
7. **In place** (spike 103 question 8 confirmed): the preview is drawn on the part being edited inside an assembly and lands on it. Pass.

## Recommendations

For task 110:

- Draw the preview as solids of revolution made from the real hole's cylindrical and conical faces (the `faces` source), with two fixes:
  - Read each face's extent along its axis, and its radius at each end, from points on the face (its facets from `Face.CalculateFacets`, or every edge's points, arcs included), not from circle edges only. Each solid is then the full 360° revolution of the face over that extent, so a seat that leaves the part is drawn whole, with its caps.
  - A cone that runs to its apex (a drill point) gets a small non-zero radius at the tip, for example 0.001 cm, since `CreateSolidCylinderCone` refuses a radius of 0. Try putting the larger radius at the bottom point if it still fails.
  - These two fixes are drawn in spike 108's build (it is branched off this spike's build), so Aidan sees them while running spike 108; task 110 is final once he says they match Inventor's preview.
- Union the solids into one body (`DoBoolean`, `kBooleanTypeUnion`), one `SurfaceGraphics` on one `GraphicsNode`, on the part's `ClientGraphicsCollection`, made with `AddNonTransacting`.
- `BurnThrough = true`, opacity **0.25** (Aidan) on both the colour and `OverrideOpacity`. Colour RGB 255, 0, 0, as Aidan saw it.
- Not `AddFeatureGraphics` (E_NOTIMPL) and not face copies alone (no caps).
- Core gets the decisions: from each face's axis, radius and the projected facet points (read into a Core model in `Extraction/`), Core works out each solid's two end points and radii, with tests (cylinder, cone to an apex, a partial face, a flipped hole). The add-in only reads the faces and builds the bodies.
- Remove task 101's paint as task 110 already says.
