using InventorAddin.Core.Holes;

namespace InventorAddin.Core.Tests.Holes;

public sealed class HoleFaceColoursTests
{
    // Holes drilled down into the part along -Z from sketch points on Z = 0.
    private static readonly HoleVector Up = new(0, 0, 1);
    private static readonly HoleVector[] OneCentre = [new(0, 0, 0)];
    private static readonly HoleVector[] TwoCentres = [new(0, 0, 0), new(5, 0, 0)];

    private static HoleFaceShape Cylinder(double x, double radius, double midZ, HoleVector? axis = null) =>
        new(HoleFaceSurface.Cylinder, new HoleVector(x, 0, 7), axis ?? Up, new HoleVector(x, 0, midZ), radius);

    private static HoleFaceShape Plane(double x, double z) =>
        new(HoleFaceSurface.Plane, new HoleVector(x, 0, z), Up, new HoleVector(x, 0, z), 0);

    private static HoleFaceShape Cone(double x, double midZ) =>
        new(HoleFaceSurface.Cone, new HoleVector(x, 0, -3), Up, new HoleVector(x, 0, midZ), 0);

    private static HoleFaceKey Key(HoleFaceRole role, int centre = 0) => new(role, centre);

    // A through clearance hole: the bore.
    private static readonly HoleFaceShape?[] Clearance = [Cylinder(0, 0.45, -1)];

    // The same hole tapped, blind, with a drill point: the bore and the cone.
    private static readonly HoleFaceShape?[] TappedBlind = [Cone(0, -2.1), Cylinder(0, 0.25, -1)];

    // A counterbored through hole: seat wall, seat floor, bore, in the order Inventor might list them.
    private static readonly HoleFaceShape?[] Counterbored = [Cylinder(0, 0.45, -1.4), Plane(0, -0.6), Cylinder(0, 0.8, -0.3)];

    [Fact]
    public void Classify_ThroughHole_IsOneBore()
    {
        Assert.Equal([Key(HoleFaceRole.Bore)], HoleFaceColours.Classify(Clearance, OneCentre, HoleSeat.None));
    }

    [Fact]
    public void Classify_Counterbore_FindsWallFloorAndBore()
    {
        var keys = HoleFaceColours.Classify(Counterbored, OneCentre, HoleSeat.Counterbore);
        Assert.Equal([Key(HoleFaceRole.Bore), Key(HoleFaceRole.SeatFloor), Key(HoleFaceRole.SeatWall)], keys);
    }

    [Fact]
    public void Classify_Spotface_CountsAsACounterbore()
    {
        // Task 112: the Spotface seat is classified as a counterbore is, as task 122 decided for a spot face.
        var keys = HoleFaceColours.Classify(Counterbored, OneCentre, HoleSeat.Spotface);
        Assert.Equal([Key(HoleFaceRole.Bore), Key(HoleFaceRole.SeatFloor), Key(HoleFaceRole.SeatWall)], keys);
    }

    [Fact]
    public void Classify_BlindCounterboreWithFlatBottom_FloorIsTheShallowerPlane()
    {
        HoleFaceShape?[] faces = [Plane(0, -2), Cylinder(0, 0.8, -0.3), Plane(0, -0.6), Cylinder(0, 0.45, -1.3)];
        var keys = HoleFaceColours.Classify(faces, OneCentre, HoleSeat.Counterbore);
        Assert.Equal(
            [Key(HoleFaceRole.Bottom), Key(HoleFaceRole.SeatWall), Key(HoleFaceRole.SeatFloor), Key(HoleFaceRole.Bore)],
            keys);
    }

    [Fact]
    public void Classify_BlindCountersinkWithDrillPoint_CountersinkIsTheShallowerCone()
    {
        HoleFaceShape?[] faces = [Cone(0, -2.2), Cylinder(0, 0.45, -1), Cone(0, -0.2)];
        var keys = HoleFaceColours.Classify(faces, OneCentre, HoleSeat.Countersink);
        Assert.Equal(
            [Key(HoleFaceRole.DrillPoint), Key(HoleFaceRole.Bore), Key(HoleFaceRole.CountersinkCone)],
            keys);
    }

    [Fact]
    public void Classify_NoSeat_ConeIsTheDrillPointAndPlaneTheBottom()
    {
        Assert.Equal(
            [Key(HoleFaceRole.DrillPoint), Key(HoleFaceRole.Bore)],
            HoleFaceColours.Classify(TappedBlind, OneCentre, HoleSeat.None));
        Assert.Equal(
            [Key(HoleFaceRole.Bottom), Key(HoleFaceRole.Bore)],
            HoleFaceColours.Classify([Plane(0, -2), Cylinder(0, 0.45, -1)], OneCentre, HoleSeat.None));
    }

    [Fact]
    public void Classify_SplitBore_EveryPieceIsTheBore()
    {
        HoleFaceShape?[] faces = [Cylinder(0, 0.45, -0.5), Cylinder(0, 0.45, -2)];
        Assert.Equal(
            [Key(HoleFaceRole.Bore), Key(HoleFaceRole.Bore)],
            HoleFaceColours.Classify(faces, OneCentre, HoleSeat.Counterbore));
    }

    [Fact]
    public void Classify_TwoCentres_EachFaceTakesItsCentre()
    {
        HoleFaceShape?[] faces = [Cylinder(5, 0.45, -1), Plane(0, -0.6), Cylinder(0, 0.45, -1), Plane(5, -0.6)];
        var keys = HoleFaceColours.Classify(faces, TwoCentres, HoleSeat.Counterbore);
        Assert.Equal(
            [Key(HoleFaceRole.Bore, 1), Key(HoleFaceRole.SeatFloor, 0), Key(HoleFaceRole.Bore, 0), Key(HoleFaceRole.SeatFloor, 1)],
            keys);
    }

    [Fact]
    public void Classify_AxisPointingTheOtherWay_SameRoles()
    {
        var down = new HoleVector(0, 0, -1);
        HoleFaceShape?[] faces =
        [
            new(HoleFaceSurface.Cone, new HoleVector(0, 0, 4), down, new HoleVector(0, 0, -2.2), 0),
            new(HoleFaceSurface.Cone, new HoleVector(0, 0, 4), down, new HoleVector(0, 0, -0.2), 0),
        ];
        Assert.Equal(
            [Key(HoleFaceRole.DrillPoint), Key(HoleFaceRole.CountersinkCone)],
            HoleFaceColours.Classify(faces, OneCentre, HoleSeat.Countersink));
    }

    [Fact]
    public void Classify_FaceOnNoCentreOrUnread_HasNoKey()
    {
        HoleFaceShape?[] faces = [Cylinder(2, 0.45, -1), null, Cylinder(0, 0.45, -1, axis: new HoleVector(0, 0, 0))];
        Assert.Equal([null, null, null], HoleFaceColours.Classify(faces, OneCentre, HoleSeat.None));
    }

    [Fact]
    public void Classify_FaceJustOffTheCentre_StillBelongsToIt()
    {
        HoleFaceShape?[] faces = [Cylinder(0.03, 0.45, -1)];
        Assert.Equal([Key(HoleFaceRole.Bore)], HoleFaceColours.Classify(faces, OneCentre, HoleSeat.None));
    }

    [Fact]
    public void Carry_ClearanceBoreColour_GoesToTheTappedBore()
    {
        // A blind clearance hole with its bore coloured and its drill point not, made again tapped and blind.
        HoleFaceShape?[] clearanceBlind = [Cylinder(0, 0.45, -1), Cone(0, -2.1)];
        var oldKeys = HoleFaceColours.Classify(clearanceBlind, OneCentre, HoleSeat.None);
        var newKeys = HoleFaceColours.Classify(TappedBlind, OneCentre, HoleSeat.None);

        Assert.Equal([null, "Blue"], HoleFaceColours.Carry(oldKeys, ["Blue", null], newKeys));
    }

    [Fact]
    public void Carry_ThroughHoleBoreColour_IsEveryFace_SoTheWholeNewHoleTakesIt()
    {
        // A through clearance hole has one face; colouring it colours the whole hole.
        var oldKeys = HoleFaceColours.Classify(Clearance, OneCentre, HoleSeat.None);
        var newKeys = HoleFaceColours.Classify(TappedBlind, OneCentre, HoleSeat.None);
        Assert.Equal(["Blue", "Blue"], HoleFaceColours.Carry(oldKeys, ["Blue"], newKeys));
    }

    [Fact]
    public void Carry_TappedBoreColour_GoesBackToTheClearanceBore()
    {
        var oldKeys = HoleFaceColours.Classify(TappedBlind, OneCentre, HoleSeat.None);
        var newKeys = HoleFaceColours.Classify(Clearance, OneCentre, HoleSeat.None);
        Assert.Equal(["Blue"], HoleFaceColours.Carry(oldKeys, [null, "Blue"], newKeys));
    }

    [Fact]
    public void Carry_EveryFaceTheSameColour_ColoursTheWholeNewHole()
    {
        var oldKeys = HoleFaceColours.Classify(Clearance, OneCentre, HoleSeat.None);
        var newKeys = HoleFaceColours.Classify(Counterbored, OneCentre, HoleSeat.Counterbore);
        Assert.Equal(["Red", "Red", "Red"], HoleFaceColours.Carry(oldKeys, ["Red"], newKeys));
    }

    [Fact]
    public void Carry_EveryFaceTheSameColour_EvenFacesWithNoKey()
    {
        var oldKeys = HoleFaceColours.Classify(Counterbored, OneCentre, HoleSeat.Counterbore);
        HoleFaceKey?[] newKeys = [Key(HoleFaceRole.Bore), null];
        Assert.Equal(["Red", "Red"], HoleFaceColours.Carry(oldKeys, ["Red", "Red", "Red"], newKeys));
    }

    [Fact]
    public void Carry_SeatRemoved_SeatColourDroppedBoreKept()
    {
        var oldKeys = HoleFaceColours.Classify(Counterbored, OneCentre, HoleSeat.Counterbore);
        var newKeys = HoleFaceColours.Classify(TappedBlind, OneCentre, HoleSeat.None);

        // Bore blue, floor uncoloured, seat wall red. The red goes with the seat; the drill point is new to the hole
        // (the old one went through), so it takes the bore's blue (task 140).
        Assert.Equal(["Blue", "Blue"], HoleFaceColours.Carry(oldKeys, ["Blue", null, "Red"], newKeys));
    }

    [Fact]
    public void Carry_SeatOnlyColour_StaysOnTheSeat()
    {
        var oldKeys = HoleFaceColours.Classify(Counterbored, OneCentre, HoleSeat.Counterbore);
        HoleFaceShape?[] tappedCounterbored = [Cylinder(0, 0.8, -0.3), Plane(0, -0.6), Cylinder(0, 0.25, -1.4)];
        var newKeys = HoleFaceColours.Classify(tappedCounterbored, OneCentre, HoleSeat.Counterbore);
        Assert.Equal(["Red", "Green", null], HoleFaceColours.Carry(oldKeys, [null, "Green", "Red"], newKeys));
    }

    [Fact]
    public void Carry_TwoColoursOnOneRole_TakesTheFirst()
    {
        HoleFaceKey?[] oldKeys = [Key(HoleFaceRole.Bore), Key(HoleFaceRole.Bore)];
        HoleFaceKey?[] newKeys = [Key(HoleFaceRole.Bore)];
        Assert.Equal(["Blue"], HoleFaceColours.Carry(oldKeys, [null, "Blue"], newKeys));
        Assert.Equal(["Green"], HoleFaceColours.Carry(oldKeys, ["Green", "Blue"], newKeys));
    }

    [Fact]
    public void Carry_ColourStaysAtItsCentre()
    {
        HoleFaceKey?[] oldKeys = [Key(HoleFaceRole.Bore, 0), Key(HoleFaceRole.Bore, 1)];
        HoleFaceKey?[] newKeys = [Key(HoleFaceRole.Bore, 1), Key(HoleFaceRole.Bore, 0)];
        Assert.Equal(["Blue", null], HoleFaceColours.Carry(oldKeys, [null, "Blue"], newKeys));
    }

    [Fact]
    public void Carry_NoColours_NothingCarried()
    {
        HoleFaceKey?[] keys = [Key(HoleFaceRole.Bore)];
        Assert.Equal([null], HoleFaceColours.Carry(keys, [null], keys));
        Assert.False(HoleFaceColours.AnyColour([null, null]));
        Assert.True(HoleFaceColours.AnyColour([null, "Blue"]));
    }

    [Fact]
    public void Carry_OldHoleWithNoFaces_NothingCarried()
    {
        HoleFaceKey?[] newKeys = [Key(HoleFaceRole.Bore)];
        Assert.Equal([null], HoleFaceColours.Carry([], [], newKeys));
    }

    [Fact]
    public void Carry_NewSeatOnABoreColouredHole_TakesTheBoreColour()
    {
        // A blind clearance hole, bore blue and drill point uncoloured, made again with a counterbore.
        HoleFaceShape?[] clearanceBlind = [Cylinder(0, 0.45, -1), Cone(0, -2.1)];
        HoleFaceShape?[] counterboredBlind = [Cylinder(0, 0.8, -0.3), Plane(0, -0.6), Cylinder(0, 0.45, -1.4), Cone(0, -2.1)];
        var oldKeys = HoleFaceColours.Classify(clearanceBlind, OneCentre, HoleSeat.None);
        var newKeys = HoleFaceColours.Classify(counterboredBlind, OneCentre, HoleSeat.Counterbore);

        // Wall and floor are new: they take the bore's blue. The drill point existed uncoloured and stays so.
        Assert.Equal(["Blue", "Blue", "Blue", null], HoleFaceColours.Carry(oldKeys, ["Blue", null], newKeys));
    }

    [Fact]
    public void Carry_NewSeatWithNoBoreColour_TakesNone()
    {
        HoleFaceShape?[] clearanceBlind = [Cylinder(0, 0.45, -1), Cone(0, -2.1)];
        var oldKeys = HoleFaceColours.Classify(clearanceBlind, OneCentre, HoleSeat.None);
        var newKeys = HoleFaceColours.Classify(Counterbored, OneCentre, HoleSeat.Counterbore);

        // Only the drill point was coloured; the new hole has none, and nothing takes its colour.
        Assert.Equal([null, null, null], HoleFaceColours.Carry(oldKeys, [null, "Red"], newKeys));
    }

    [Fact]
    public void Carry_ExistingSeatWithItsOwnColour_KeepsIt()
    {
        // A counterbore with its wall red and bore blue, made again as a spotface.
        var oldKeys = HoleFaceColours.Classify(Counterbored, OneCentre, HoleSeat.Counterbore);
        HoleFaceShape?[] spotfaced = [Cylinder(0, 0.8, -0.1), Plane(0, -0.2), Cylinder(0, 0.45, -1.2)];
        var newKeys = HoleFaceColours.Classify(spotfaced, OneCentre, HoleSeat.Spotface);

        // Bore blue, floor uncoloured (it existed, so it does not take the bore's colour), wall red.
        Assert.Equal(["Red", null, "Blue"], HoleFaceColours.Carry(oldKeys, ["Blue", null, "Red"], newKeys));
    }

    [Fact]
    public void Carry_NewSeatTakesTheBoreColourOfItsOwnCentre()
    {
        HoleFaceKey?[] oldKeys = [Key(HoleFaceRole.Bore, 0), Key(HoleFaceRole.Bore, 1)];
        HoleFaceKey?[] newKeys =
        [
            Key(HoleFaceRole.SeatWall, 0), Key(HoleFaceRole.Bore, 0),
            Key(HoleFaceRole.SeatWall, 1), Key(HoleFaceRole.Bore, 1),
        ];
        Assert.Equal([null, null, "Blue", "Blue"], HoleFaceColours.Carry(oldKeys, [null, "Blue"], newKeys));
    }

    [Fact]
    public void Carry_NewFaceWithNoKey_TakesNoBoreColour()
    {
        HoleFaceKey?[] oldKeys = [Key(HoleFaceRole.Bore), Key(HoleFaceRole.DrillPoint)];
        HoleFaceKey?[] newKeys = [Key(HoleFaceRole.Bore), null];
        Assert.Equal(["Blue", null], HoleFaceColours.Carry(oldKeys, ["Blue", null], newKeys));
    }

    [Theory]
    [InlineData("kFeatureAppearance", true)]
    [InlineData("kOverrideAppearance", true)]
    [InlineData("kBodyAppearance", false)]
    [InlineData("kPartAppearance", false)]
    [InlineData("kMaterialAppearance", false)]
    [InlineData("kDefaultAppearance", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsFeatureHandSource_OnlySourcesSetOnTheFeature(string? source, bool expected) =>
        Assert.Equal(expected, HoleFaceColours.IsFeatureHandSource(source));

    [Fact]
    public void KeepFeatureColour_SomeFaceTakesTheFeatureColour_Kept()
    {
        Assert.True(HoleFaceColours.KeepFeatureColour("kOverrideAppearance", ["kFeatureAppearance", "kFeatureAppearance"]));
        Assert.True(HoleFaceColours.KeepFeatureColour("kFeatureAppearance", ["kOverrideAppearance", "kFeatureAppearance"]));
    }

    [Fact]
    public void KeepFeatureColour_EveryFaceOverridden_NotKept()
    {
        // A one-face hole whose bore was coloured by hand: the feature's override source may only reflect that face.
        Assert.False(HoleFaceColours.KeepFeatureColour("kOverrideAppearance", ["kOverrideAppearance"]));
        Assert.False(HoleFaceColours.KeepFeatureColour("kOverrideAppearance", ["kOverrideAppearance", null]));
    }

    [Fact]
    public void KeepFeatureColour_NoFacesOrNoneRead_NotKept()
    {
        Assert.False(HoleFaceColours.KeepFeatureColour("kOverrideAppearance", []));
        Assert.False(HoleFaceColours.KeepFeatureColour("kOverrideAppearance", [null, ""]));
    }

    [Fact]
    public void KeepFeatureColour_InheritedSource_NotKept()
    {
        Assert.False(HoleFaceColours.KeepFeatureColour("kBodyAppearance", ["kBodyAppearance"]));
        Assert.False(HoleFaceColours.KeepFeatureColour(null, ["kFeatureAppearance"]));
    }

    [Fact]
    public void Carry_ColoursNotMatchingFaces_Throws()
    {
        HoleFaceKey?[] keys = [Key(HoleFaceRole.Bore)];
        Assert.Throws<ArgumentException>(() => HoleFaceColours.Carry(keys, [], keys));
    }
}
