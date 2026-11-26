using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public class IsoViewRuleTests
{
    private const double Third = 0.5773502691896258; // 1 / sqrt(3)

    private static void AssertNormal(FaceViewOrientation o, double x, double y, double z)
    {
        Assert.Equal(x, o.NormalX, 9);
        Assert.Equal(y, o.NormalY, 9);
        Assert.Equal(z, o.NormalZ, 9);
    }

    [Fact]
    public void StandardFrontGivesTheStandardIsoCorner()
    {
        FaceViewOrientation iso = IsoViewRule.Choose(FrontViewRule.FrontOrientation);

        // Inventor's top-right iso looks from +X, +Y, +Z.
        AssertNormal(iso, Third, Third, Third);
        Assert.True(IsoViewRule.IsStandardIso(FrontViewRule.FrontOrientation));
        Assert.True(IsoViewRule.IsStandardIso(null));
    }

    [Theory]
    [InlineData(FrontViewChoice.Back, -1, 1, -1)]
    [InlineData(FrontViewChoice.Left, -1, 1, 1)]
    [InlineData(FrontViewChoice.Right, 1, 1, -1)]
    [InlineData(FrontViewChoice.Top, 1, 1, -1)]
    [InlineData(FrontViewChoice.Bottom, 1, -1, 1)]
    public void TheIsoLooksAtTheFrontTopRightCornerOfTheChosenFront(FrontViewChoice choice, double x, double y, double z)
    {
        FaceViewOrientation front = FrontViewRule.Named(choice);
        FaceViewOrientation iso = IsoViewRule.Choose(front);

        AssertNormal(iso, x * Third, y * Third, z * Third);
        Assert.False(IsoViewRule.IsStandardIso(front));

        // The corner is on the front's viewer side, its top side and its right side.
        double rx = front.UpY * front.NormalZ - front.UpZ * front.NormalY;
        double ry = front.UpZ * front.NormalX - front.UpX * front.NormalZ;
        double rz = front.UpX * front.NormalY - front.UpY * front.NormalX;
        Assert.True(iso.NormalX * front.NormalX + iso.NormalY * front.NormalY + iso.NormalZ * front.NormalZ > 0.5);
        Assert.True(iso.NormalX * front.UpX + iso.NormalY * front.UpY + iso.NormalZ * front.UpZ > 0.5);
        Assert.True(iso.NormalX * rx + iso.NormalY * ry + iso.NormalZ * rz > 0.5);
    }

    [Theory]
    [InlineData(FrontViewChoice.Front)]
    [InlineData(FrontViewChoice.Back)]
    [InlineData(FrontViewChoice.Top)]
    [InlineData(FrontViewChoice.Bottom)]
    [InlineData(FrontViewChoice.Left)]
    [InlineData(FrontViewChoice.Right)]
    public void UpIsUnitLengthAcrossTheViewingDirectionAndOnTheFrontsUpSide(FrontViewChoice choice)
    {
        FaceViewOrientation front = FrontViewRule.Named(choice);
        FaceViewOrientation iso = IsoViewRule.Choose(front);

        Assert.Equal(1, Math.Sqrt(iso.UpX * iso.UpX + iso.UpY * iso.UpY + iso.UpZ * iso.UpZ), 9);
        Assert.Equal(0, iso.UpX * iso.NormalX + iso.UpY * iso.NormalY + iso.UpZ * iso.NormalZ, 9);
        Assert.True(iso.UpX * front.UpX + iso.UpY * front.UpY + iso.UpZ * front.UpZ > 0.5);
    }

    [Fact]
    public void AnArbitraryFrontIsFollowedToo()
    {
        // A front that looks along +Z with +X up (the Auto rule's tall part): right is u x f = X x Z = -Y.
        FaceViewOrientation iso = IsoViewRule.Choose(new FaceViewOrientation(0, 0, 1, 1, 0, 0));

        AssertNormal(iso, Third, -Third, Third);
    }

    [Fact]
    public void ADegenerateFrontIsRejected()
    {
        Assert.Throws<ArgumentException>(() => IsoViewRule.Choose(new FaceViewOrientation(0, 0, 0, 0, 0, 0)));
    }
}
