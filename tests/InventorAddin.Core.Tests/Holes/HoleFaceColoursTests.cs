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

        // Bore blue, floor uncoloured, seat wall red.
        Assert.Equal([null, "Blue"], HoleFaceColours.Carry(oldKeys, ["Blue", null, "Red"], newKeys));
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
    public void Carry_ColoursNotMatchingFaces_Throws()
    {
        HoleFaceKey?[] keys = [Key(HoleFaceRole.Bore)];
        Assert.Throws<ArgumentException>(() => HoleFaceColours.Carry(keys, [], keys));
    }
}
