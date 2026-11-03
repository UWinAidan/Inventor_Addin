using InventorAddin.Core;
using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

public class HolePreviewSolidsTests
{
    private const double Precision = 9;

    // A drill point of 118 degrees: half-angle 59 degrees.
    private static readonly double DrillHalfAngle = 59 * Math.PI / 180;

    private static HoleVector V(double x, double y, double z) => new(x, y, z);

    /// <summary>Points on a circle (or an arc of it) of radius <paramref name="r"/> about the Z axis at height z.</summary>
    private static IEnumerable<HoleVector> Arc(double r, double z, double fromDeg = 0, double toDeg = 360, int count = 25)
    {
        for (var i = 0; i < count; i++)
        {
            var a = (fromDeg + (toDeg - fromDeg) * i / (count - 1)) * Math.PI / 180;
            yield return V(r * Math.Cos(a), r * Math.Sin(a), z);
        }
    }

    private static HolePreviewFace Cylinder(HoleVector axisPoint, HoleVector axis, double r, IEnumerable<HoleVector> points) =>
        new(HolePreviewSurface.Cylinder, axisPoint, axis, r, 0, false, points.ToList());

    private static HolePreviewFace Cone(
        HoleVector axisPoint, HoleVector axis, double r, double halfAngle, bool expanding, IEnumerable<HoleVector> points) =>
        new(HolePreviewSurface.Cone, axisPoint, axis, r, halfAngle, expanding, points.ToList());

    private static void AssertPoint(HoleVector expected, HoleVector actual)
    {
        Assert.Equal(expected.X, actual.X, Precision);
        Assert.Equal(expected.Y, actual.Y, Precision);
        Assert.Equal(expected.Z, actual.Z, Precision);
    }

    [Fact]
    public void Graphics_AreAidansRedAtAQuarter_DrawnOverTheModel()
    {
        Assert.Equal(new HoleColour(255, 0, 0), HolePreviewGraphics.Colour);
        Assert.Equal(0.25, HolePreviewGraphics.Opacity);
        Assert.True(HolePreviewGraphics.BurnThrough);
        Assert.Equal(0, HolePreviewGraphics.DepthPriority);
        Assert.Equal(Branding.IdPrefix + "_HolePreview", HolePreviewGraphics.ClientId);
    }

    [Fact]
    public void Cylinder_RunsOverItsEdgesAlongTheAxis_AtItsRadius()
    {
        // A through hole 2 cm deep, 0.3 cm radius: a circle edge at the top and one at the bottom.
        var face = Cylinder(V(0, 0, 0), V(0, 0, 1), 0.3, Arc(0.3, 0).Concat(Arc(0.3, -2)));

        var solid = HolePreviewSolids.For(face)!.Value;

        AssertPoint(V(0, 0, -2), solid.Bottom);
        AssertPoint(V(0, 0, 0), solid.Top);
        Assert.Equal(0.3, solid.BottomRadiusCm, Precision);
        Assert.Equal(0.3, solid.TopRadiusCm, Precision);
    }

    [Fact]
    public void Cylinder_OffItsAxisPoint_AndAnAxisNotUnitLength_StaysOnTheAxis()
    {
        // The base point is somewhere else on the axis, and the axis is given 5 long.
        var points = Arc(0.3, 1).Concat(Arc(0.3, 4)).Select(p => V(p.X + 10, p.Y - 3, p.Z));
        var face = Cylinder(V(10, -3, -7), V(0, 0, 5), 0.3, points);

        var solid = HolePreviewSolids.For(face)!.Value;

        AssertPoint(V(10, -3, 1), solid.Bottom);
        AssertPoint(V(10, -3, 4), solid.Top);
    }

    [Fact]
    public void Cone_ToItsApex_GetsTheSmallestTipRadius()
    {
        // A blind hole's drill point: its circle edge at z = -2 and its apex vertex below it, where the radius is 0.
        var r = 0.25;
        var depth = r / Math.Tan(DrillHalfAngle);
        var points = Arc(r, -2).Append(V(0, 0, -2 - depth));
        var face = Cone(V(0, 0, -2), V(0, 0, 1), r, DrillHalfAngle, expanding: true, points);

        var solid = HolePreviewSolids.For(face)!.Value;

        AssertPoint(V(0, 0, -2 - depth), solid.Bottom);
        AssertPoint(V(0, 0, -2), solid.Top);
        Assert.Equal(HolePreviewSolids.MinRadiusCm, solid.BottomRadiusCm, Precision);
        Assert.Equal(0.001, HolePreviewSolids.MinRadiusCm);
        Assert.Equal(r, solid.TopRadiusCm, Precision);
    }

    [Fact]
    public void Cone_WhoseSamplesSitAtOneHeight_RunsToItsApex()
    {
        // The apex edge could not be sampled and gave no vertex: only the circle at z = -2 is known.
        var r = 0.25;
        var depth = r / Math.Tan(DrillHalfAngle);
        var face = Cone(V(0, 0, -2), V(0, 0, 1), r, DrillHalfAngle, expanding: true, Arc(r, -2));

        var solid = HolePreviewSolids.For(face)!.Value;

        AssertPoint(V(0, 0, -2 - depth), solid.Bottom);
        AssertPoint(V(0, 0, -2), solid.Top);
        Assert.Equal(HolePreviewSolids.MinRadiusCm, solid.BottomRadiusCm, Precision);
        Assert.Equal(r, solid.TopRadiusCm, Precision);
    }

    [Fact]
    public void Cone_BetweenTwoCircles_TakesTheRadiusAtEachEnd()
    {
        // A 90 degree countersink from radius 0.6 at the top face down to the bore's radius 0.3.
        var half = Math.PI / 4;
        var face = Cone(V(0, 0, 0), V(0, 0, 1), 0.6, half, expanding: true, Arc(0.6, 0).Concat(Arc(0.3, -0.3)));

        var solid = HolePreviewSolids.For(face)!.Value;

        AssertPoint(V(0, 0, -0.3), solid.Bottom);
        AssertPoint(V(0, 0, 0), solid.Top);
        Assert.Equal(0.3, solid.BottomRadiusCm, Precision);
        Assert.Equal(0.6, solid.TopRadiusCm, Precision);
    }

    [Fact]
    public void PartialFace_WhoseSamplesAreArcs_IsDrawnWhole()
    {
        // A counterbore running off the edge of the part: its edges are a quarter arc at the top, a quarter arc at the
        // floor, and the two straight lines where it leaves the part. The solid is the full counterbore.
        var r = 0.5;
        var points = Arc(r, 0, 0, 90)
            .Concat(Arc(r, -0.4, 0, 90))
            .Concat([V(r, 0, 0), V(r, 0, -0.2), V(r, 0, -0.4)])
            .Concat([V(0, r, 0), V(0, r, -0.4)]);
        var face = Cylinder(V(0, 0, 0), V(0, 0, 1), r, points);

        var solid = HolePreviewSolids.For(face)!.Value;

        AssertPoint(V(0, 0, -0.4), solid.Bottom);
        AssertPoint(V(0, 0, 0), solid.Top);
        Assert.Equal(r, solid.BottomRadiusCm, Precision);
        Assert.Equal(r, solid.TopRadiusCm, Precision);
    }

    [Fact]
    public void FlippedCylinder_GivesTheSameSolid_EndsSwapped()
    {
        // A hole drilled upward from z = 0 to z = 1.5, read once with its axis up and once with it down.
        var points = Arc(0.2, 0).Concat(Arc(0.2, 1.5)).ToList();
        var up = HolePreviewSolids.For(Cylinder(V(0, 0, 0), V(0, 0, 1), 0.2, points))!.Value;
        var down = HolePreviewSolids.For(Cylinder(V(0, 0, 1.5), V(0, 0, -1), 0.2, points))!.Value;

        AssertPoint(up.Bottom, down.Top);
        AssertPoint(up.Top, down.Bottom);
        AssertPoint(V(0, 0, 0), up.Bottom);
        AssertPoint(V(0, 0, 1.5), up.Top);
    }

    [Fact]
    public void FlippedCone_GivesTheSameSolid_EndsSwapped()
    {
        // The same drill point read with its axis up (expanding) and down (not expanding): the apex is at the same
        // place either way.
        var r = 0.25;
        var depth = r / Math.Tan(DrillHalfAngle);
        var points = Arc(r, -2).ToList();
        var up = HolePreviewSolids.For(Cone(V(0, 0, -2), V(0, 0, 1), r, DrillHalfAngle, expanding: true, points))!.Value;
        var down = HolePreviewSolids.For(Cone(V(0, 0, -2), V(0, 0, -1), r, DrillHalfAngle, expanding: false, points))!.Value;

        AssertPoint(up.Bottom, down.Top);
        AssertPoint(up.Top, down.Bottom);
        Assert.Equal(up.BottomRadiusCm, down.TopRadiusCm, Precision);
        Assert.Equal(up.TopRadiusCm, down.BottomRadiusCm, Precision);
        AssertPoint(V(0, 0, -2 - depth), up.Bottom);
    }

    [Fact]
    public void FlippedHole_DrilledDownTheMinusXAxis_RunsAlongIt()
    {
        // A hole drilled from the face x = 5 towards -x, 1 cm deep, with a drill point.
        var r = 0.3;
        var depth = r / Math.Tan(DrillHalfAngle);
        var ring = (double x) => Arc(r, 0).Select(p => V(x, p.X, p.Y));
        var faces = new[]
        {
            Cylinder(V(5, 0, 0), V(-1, 0, 0), r, ring(5).Concat(ring(4))),
            Cone(V(4, 0, 0), V(-1, 0, 0), r, DrillHalfAngle, expanding: false, ring(4)),
        };

        var solids = HolePreviewSolids.For(faces);

        Assert.Equal(2, solids.Count);
        AssertPoint(V(5, 0, 0), solids[0].Bottom);
        AssertPoint(V(4, 0, 0), solids[0].Top);
        AssertPoint(V(4, 0, 0), solids[1].Bottom);
        AssertPoint(V(4 - depth, 0, 0), solids[1].Top);
        Assert.Equal(r, solids[1].BottomRadiusCm, Precision);
        Assert.Equal(HolePreviewSolids.MinRadiusCm, solids[1].TopRadiusCm, Precision);
    }

    [Fact]
    public void CylinderWhoseSamplesSitAtOneHeight_HasNoSolid()
    {
        Assert.Null(HolePreviewSolids.For(Cylinder(V(0, 0, 0), V(0, 0, 1), 0.3, Arc(0.3, 0))));
    }

    [Fact]
    public void FaceWithNoPoints_OrNoAxis_HasNoSolid()
    {
        Assert.Null(HolePreviewSolids.For(Cylinder(V(0, 0, 0), V(0, 0, 1), 0.3, [])));
        Assert.Null(HolePreviewSolids.For(Cone(V(0, 0, 0), V(0, 0, 1), 0.3, DrillHalfAngle, true, [])));
        Assert.Null(HolePreviewSolids.For(Cylinder(V(0, 0, 0), V(0, 0, 0), 0.3, Arc(0.3, 0).Concat(Arc(0.3, 1)))));
    }

    [Fact]
    public void PointsThatAreNotNumbers_AreLeftOut()
    {
        var points = Arc(0.3, 0).Concat(Arc(0.3, -1)).Append(V(double.NaN, 0, 5)).Append(V(0, 0, double.PositiveInfinity));
        var solid = HolePreviewSolids.For(Cylinder(V(0, 0, 0), V(0, 0, 1), 0.3, points))!.Value;

        AssertPoint(V(0, 0, -1), solid.Bottom);
        AssertPoint(V(0, 0, 0), solid.Top);
    }

    [Fact]
    public void For_Faces_LeavesOutThoseWithNoSolid_InOrder()
    {
        var faces = new[]
        {
            Cylinder(V(0, 0, 0), V(0, 0, 1), 0.3, Arc(0.3, 0).Concat(Arc(0.3, -1))),
            Cylinder(V(0, 0, 0), V(0, 0, 1), 0.3, []),
            Cylinder(V(0, 0, 0), V(0, 0, 1), 0.5, Arc(0.5, 0).Concat(Arc(0.5, -0.2))),
        };

        var solids = HolePreviewSolids.For(faces);

        Assert.Equal(2, solids.Count);
        Assert.Equal(0.3, solids[0].TopRadiusCm, Precision);
        Assert.Equal(0.5, solids[1].TopRadiusCm, Precision);
    }

    [Fact]
    public void SampleParams_Are25_EvenlySpread_EndsIncluded()
    {
        var values = HolePreviewSolids.SampleParams(1, 3);

        Assert.Equal(25, values.Length);
        Assert.Equal(25, HolePreviewSolids.PointsPerEdge);
        Assert.Equal(1, values[0], Precision);
        Assert.Equal(3, values[^1], Precision);
        Assert.Equal(1 + 2.0 / 24, values[1], Precision);
    }

    [Fact]
    public void PointsOf_ReadsThreeCoordinatesAPoint_DroppingATrailingPart()
    {
        var points = HolePreviewSolids.PointsOf([1, 2, 3, 4, 5, 6, 7]);

        Assert.Equal([V(1, 2, 3), V(4, 5, 6)], points);
    }
}
