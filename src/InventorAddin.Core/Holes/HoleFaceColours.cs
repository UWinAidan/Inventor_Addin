namespace InventorAddin.Core.Holes;

/// <summary>What a face of a hole is for (task 122): the part of the hole it was cut as.</summary>
public enum HoleFaceRole
{
    /// <summary>The hole's own cylinder: the clearance, tap drill or dowel diameter.</summary>
    Bore,

    /// <summary>A counterbore's (or spotface's) cylinder.</summary>
    SeatWall,

    /// <summary>The flat floor of a counterbore (or spotface).</summary>
    SeatFloor,

    /// <summary>A countersink's cone.</summary>
    CountersinkCone,

    /// <summary>The flat bottom of a blind hole.</summary>
    Bottom,

    /// <summary>The cone at the bottom of a blind hole with an angled drill point.</summary>
    DrillPoint,

    /// <summary>Any other surface Inventor made for the hole.</summary>
    Other,
}

/// <summary>The kind of surface a face of a hole lies on, as Inventor reports it.</summary>
public enum HoleFaceSurface
{
    Cylinder,
    Cone,
    Plane,
    Other,
}

/// <summary>A point or a direction in model space, in centimetres.</summary>
public readonly record struct HoleVector(double X, double Y, double Z)
{
    public static HoleVector operator -(HoleVector a, HoleVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public double Dot(HoleVector other) => X * other.X + Y * other.Y + Z * other.Z;

    public HoleVector Cross(HoleVector other) =>
        new(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);

    public double Length => Math.Sqrt(Dot(this));
}

/// <summary>
/// The shape of one face of a hole, read from Inventor (task 122).
/// </summary>
/// <param name="Surface">The kind of surface.</param>
/// <param name="AxisPoint">A point on the face's axis: a cylinder's or cone's base point; for a plane, the middle of
/// the face.</param>
/// <param name="AxisDirection">The axis: a cylinder's or cone's axis vector, a plane's normal. Need not be unit length;
/// its sign does not matter.</param>
/// <param name="MidPoint">The middle of the face (the middle of its range box).</param>
/// <param name="RadiusCm">A cylinder's radius; ignored for other surfaces.</param>
public sealed record HoleFaceShape(
    HoleFaceSurface Surface,
    HoleVector AxisPoint,
    HoleVector AxisDirection,
    HoleVector MidPoint,
    double RadiusCm);

/// <summary>Which face of a hole this is: its role at one of the hole's centres (the index into the centre list).</summary>
public readonly record struct HoleFaceKey(HoleFaceRole Role, int CentreIndex);

/// <summary>
/// Keeps the colours a user put on a hole's faces by hand when the hole is deleted and made again (task 122; spec 09,
/// "Dowel and precision holes": a hand colour is kept whenever the hole is changed). Each face of the old and of the
/// new hole is described by its role and the centre it belongs to (<see cref="Classify"/>); the new hole's faces take
/// the colours the old faces of the same role and centre had (<see cref="Carry"/>). AWB fit colours are not hand
/// colours: the add-in leaves them out before calling this, and task 096's rules handle them.
/// </summary>
public static class HoleFaceColours
{
    /// <summary>
    /// How far a face's axis may lie from a hole centre and still belong to it, in centimetres (0.5 mm; Default, task
    /// 122). A centre further than this from every face axis has no faces; a face further from every centre has no key.
    /// </summary>
    public const double CentreToleranceCm = 0.05;

    /// <summary>How much two cylinder radii may differ and still be the same diameter, in centimetres.</summary>
    public const double RadiusToleranceCm = 1e-4;

    /// <summary>
    /// The key of each face of a hole, in the order given; null for a face that could not be read or that lies on
    /// none of <paramref name="centres"/>. At each centre: with a counterbore seat, a cylinder wider than the
    /// narrowest one is the seat wall and the plane nearest the centre is the seat floor; with a countersink seat, the
    /// cone nearest the centre is the countersink; every other cylinder is the bore, every other plane the bottom and
    /// every other cone the drill point.
    /// </summary>
    /// <param name="faces">The faces of the hole; a null entry is a face whose shape could not be read.</param>
    /// <param name="centres">The hole's centres, on its sketch plane, in centimetres.</param>
    /// <param name="seat">The hole's seat. A spotface counts as a counterbore.</param>
    public static IReadOnlyList<HoleFaceKey?> Classify(
        IReadOnlyList<HoleFaceShape?> faces, IReadOnlyList<HoleVector> centres, HoleSeat seat)
    {
        ArgumentNullException.ThrowIfNull(faces);
        ArgumentNullException.ThrowIfNull(centres);

        var keys = new HoleFaceKey?[faces.Count];
        var centreOf = new int?[faces.Count];
        var depthOf = new double[faces.Count];
        for (int i = 0; i < faces.Count; i++)
        {
            if (faces[i] is not { } face || NearestCentre(face, centres) is not { } centre)
                continue;
            centreOf[i] = centre;
            depthOf[i] = Depth(face, centres[centre]);
        }

        for (int c = 0; c < centres.Count; c++)
        {
            var here = Enumerable.Range(0, faces.Count).Where(i => centreOf[i] == c).ToList();

            var cylinders = here.Where(i => faces[i]!.Surface == HoleFaceSurface.Cylinder).ToList();
            double narrowest = cylinders.Count == 0 ? 0 : cylinders.Min(i => faces[i]!.RadiusCm);
            foreach (int i in cylinders)
            {
                bool wall = seat == HoleSeat.Counterbore && faces[i]!.RadiusCm > narrowest + RadiusToleranceCm;
                keys[i] = new HoleFaceKey(wall ? HoleFaceRole.SeatWall : HoleFaceRole.Bore, c);
            }

            var planes = here.Where(i => faces[i]!.Surface == HoleFaceSurface.Plane).OrderBy(i => depthOf[i]).ToList();
            for (int n = 0; n < planes.Count; n++)
            {
                bool floor = seat == HoleSeat.Counterbore && n == 0;
                keys[planes[n]] = new HoleFaceKey(floor ? HoleFaceRole.SeatFloor : HoleFaceRole.Bottom, c);
            }

            var cones = here.Where(i => faces[i]!.Surface == HoleFaceSurface.Cone).OrderBy(i => depthOf[i]).ToList();
            for (int n = 0; n < cones.Count; n++)
            {
                bool countersink = seat == HoleSeat.Countersink && n == 0;
                keys[cones[n]] = new HoleFaceKey(countersink ? HoleFaceRole.CountersinkCone : HoleFaceRole.DrillPoint, c);
            }

            foreach (int i in here.Where(i => faces[i]!.Surface == HoleFaceSurface.Other))
                keys[i] = new HoleFaceKey(HoleFaceRole.Other, c);
        }

        return keys;
    }

    /// <summary>
    /// The hand colour each face of the new hole takes, in the order of <paramref name="newKeys"/>; null leaves a face
    /// as Inventor made it. When every face of the old hole had the same hand colour, every new face takes it. Else a
    /// new face takes the colour of the old faces with its key; when those differ, the first coloured one in the old
    /// hole's face order (Default, task 122). A new face whose key the old hole had no colour for (a seat that is new,
    /// a face that could not be read) takes none, and an old colour whose role the new hole has no face for (a seat
    /// removed) is dropped.
    /// </summary>
    /// <param name="oldKeys">The old hole's face keys (<see cref="Classify"/>).</param>
    /// <param name="oldColours">The old hole's hand colour on each face, by any identity the caller chooses (the add-in
    /// passes the appearance's name); null for a face with no hand colour.</param>
    /// <param name="newKeys">The new hole's face keys.</param>
    public static IReadOnlyList<string?> Carry(
        IReadOnlyList<HoleFaceKey?> oldKeys, IReadOnlyList<string?> oldColours, IReadOnlyList<HoleFaceKey?> newKeys)
    {
        ArgumentNullException.ThrowIfNull(oldKeys);
        ArgumentNullException.ThrowIfNull(oldColours);
        ArgumentNullException.ThrowIfNull(newKeys);
        if (oldKeys.Count != oldColours.Count)
            throw new ArgumentException("There must be one colour for each old face.", nameof(oldColours));

        var carried = new string?[newKeys.Count];
        if (AllOneColour(oldColours) is { } whole)
        {
            Array.Fill(carried, whole);
            return carried;
        }

        var byKey = new Dictionary<HoleFaceKey, string>();
        for (int i = 0; i < oldKeys.Count; i++)
        {
            if (oldKeys[i] is { } key && oldColours[i] is { } colour)
                byKey.TryAdd(key, colour);
        }

        for (int i = 0; i < newKeys.Count; i++)
        {
            if (newKeys[i] is { } key && byKey.TryGetValue(key, out string? colour))
                carried[i] = colour;
        }

        return carried;
    }

    /// <summary>True when any face of the old hole has a hand colour, so there is something to carry.</summary>
    public static bool AnyColour(IReadOnlyList<string?> oldColours)
    {
        ArgumentNullException.ThrowIfNull(oldColours);
        return oldColours.Any(c => c != null);
    }

    private static string? AllOneColour(IReadOnlyList<string?> colours)
    {
        if (colours.Count == 0 || colours[0] is not { } first)
            return null;
        return colours.All(c => string.Equals(c, first, StringComparison.Ordinal)) ? first : null;
    }

    private static int? NearestCentre(HoleFaceShape face, IReadOnlyList<HoleVector> centres)
    {
        double axisLength = face.AxisDirection.Length;
        if (axisLength <= 0 || double.IsNaN(axisLength))
            return null;

        int? nearest = null;
        double best = CentreToleranceCm;
        for (int c = 0; c < centres.Count; c++)
        {
            double distance = (centres[c] - face.AxisPoint).Cross(face.AxisDirection).Length / axisLength;
            if (distance <= best)
            {
                best = distance;
                nearest = c;
            }
        }

        return nearest;
    }

    /// <summary>How far along the hole's axis the middle of the face is from the centre (from the sketch plane).</summary>
    private static double Depth(HoleFaceShape face, HoleVector centre) =>
        Math.Abs((face.MidPoint - centre).Dot(face.AxisDirection)) / face.AxisDirection.Length;
}
