using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public class FaceViewRuleTests
{
    private static void AssertVector((double X, double Y, double Z) expected, double x, double y, double z)
    {
        Assert.Equal(expected.X, x, 6);
        Assert.Equal(expected.Y, y, 6);
        Assert.Equal(expected.Z, z, 6);
    }

    [Fact]
    public void Face_in_the_XY_plane_with_its_longest_edge_along_X_gets_Y_up()
    {
        FaceViewOrientation? o = FaceViewRule.Choose(0, 0, 1, new[]
        {
            new FaceEdgeCm(1, 0, 0, 20), new FaceEdgeCm(0, 1, 0, 6),
        });

        Assert.NotNull(o);
        AssertVector((0, 0, 1), o!.NormalX, o.NormalY, o.NormalZ);
        AssertVector((0, 1, 0), o.UpX, o.UpY, o.UpZ);
    }

    [Fact]
    public void Longest_edge_along_Y_is_horizontal_so_up_is_X()
    {
        FaceViewOrientation? o = FaceViewRule.Choose(0, 0, 1, new[]
        {
            new FaceEdgeCm(0, -1, 0, 20), new FaceEdgeCm(1, 0, 0, 6),
        });

        Assert.NotNull(o);
        AssertVector((1, 0, 0), o!.UpX, o.UpY, o.UpZ);
    }

    [Fact]
    public void Flipping_the_edge_sign_gives_the_same_orientation()
    {
        FaceViewOrientation a = FaceViewRule.Choose(0, 0, 1, new[] { new FaceEdgeCm(1, 0, 0, 20) })!;
        FaceViewOrientation b = FaceViewRule.Choose(0, 0, 1, new[] { new FaceEdgeCm(-1, 0, 0, 20) })!;

        Assert.Equal(a.UpX, b.UpX, 9);
        Assert.Equal(a.UpY, b.UpY, 9);
        Assert.Equal(a.UpZ, b.UpZ, 9);
    }

    [Fact]
    public void Up_points_toward_plus_Z_when_the_face_is_vertical()
    {
        FaceViewOrientation? o = FaceViewRule.Choose(0, 1, 0, new[] { new FaceEdgeCm(-1, 0, 0, 30) });

        Assert.NotNull(o);
        AssertVector((0, 0, 1), o!.UpX, o.UpY, o.UpZ);
    }

    [Fact]
    public void Vectors_are_unit_length_and_perpendicular()
    {
        FaceViewOrientation o = FaceViewRule.Choose(1, 1, 1, new[] { new FaceEdgeCm(1, -1, 0, 9) })!;

        Assert.Equal(1.0, Math.Sqrt(o.NormalX * o.NormalX + o.NormalY * o.NormalY + o.NormalZ * o.NormalZ), 9);
        Assert.Equal(1.0, Math.Sqrt(o.UpX * o.UpX + o.UpY * o.UpY + o.UpZ * o.UpZ), 9);
        Assert.Equal(0.0, o.NormalX * o.UpX + o.NormalY * o.UpY + o.NormalZ * o.UpZ, 9);
    }

    [Fact]
    public void An_edge_off_the_plane_is_flattened_into_it()
    {
        FaceViewOrientation o = FaceViewRule.Choose(0, 0, 1, new[] { new FaceEdgeCm(1, 0, 0.3, 20) })!;

        Assert.Equal(0.0, o.NormalX * o.UpX + o.NormalY * o.UpY + o.NormalZ * o.UpZ, 9);
    }

    [Fact]
    public void An_edge_along_the_normal_is_skipped()
    {
        FaceViewOrientation? o = FaceViewRule.Choose(0, 0, 1, new[]
        {
            new FaceEdgeCm(0, 0, 1, 50), new FaceEdgeCm(1, 0, 0, 5),
        });

        Assert.NotNull(o);
        AssertVector((0, 1, 0), o!.UpX, o.UpY, o.UpZ);
    }

    [Fact]
    public void Returns_null_without_a_usable_normal_or_edge()
    {
        Assert.Null(FaceViewRule.Choose(0, 0, 0, new[] { new FaceEdgeCm(1, 0, 0, 5) }));
        Assert.Null(FaceViewRule.Choose(0, 0, 1, Array.Empty<FaceEdgeCm>()));
        Assert.Null(FaceViewRule.Choose(0, 0, 1, new[] { new FaceEdgeCm(0, 0, 0, 5), new FaceEdgeCm(1, 0, 0, 0) }));
        Assert.Null(FaceViewRule.Choose(0, 0, 1, new[] { new FaceEdgeCm(0, 0, 1, 5) }));
    }

    [Fact]
    public void A_shorter_edge_listed_first_does_not_win()
    {
        FaceViewOrientation? o = FaceViewRule.Choose(0, 0, 1, new[]
        {
            new FaceEdgeCm(0, 1, 0, 6), new FaceEdgeCm(1, 0, 0, 20),
        });

        Assert.NotNull(o);
        AssertVector((0, 1, 0), o!.UpX, o.UpY, o.UpZ);
    }

    [Fact]
    public void At_equal_length_the_first_edge_wins()
    {
        FaceViewOrientation? first = FaceViewRule.Choose(0, 0, 1, new[]
        {
            new FaceEdgeCm(1, 0, 0, 10), new FaceEdgeCm(0, 1, 0, 10),
        });
        FaceViewOrientation? swapped = FaceViewRule.Choose(0, 0, 1, new[]
        {
            new FaceEdgeCm(0, 1, 0, 10), new FaceEdgeCm(1, 0, 0, 10),
        });

        AssertVector((0, 1, 0), first!.UpX, first.UpY, first.UpZ);
        AssertVector((1, 0, 0), swapped!.UpX, swapped.UpY, swapped.UpZ);
    }
}
