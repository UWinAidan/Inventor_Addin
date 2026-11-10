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
                bool wall = (seat is HoleSeat.Counterbore or HoleSeat.Spotface)
                    && faces[i]!.RadiusCm > narrowest + RadiusToleranceCm;
                keys[i] = new HoleFaceKey(wall ? HoleFaceRole.SeatWall : HoleFaceRole.Bore, c);
            }

            var planes = here.Where(i => faces[i]!.Surface == HoleFaceSurface.Plane).OrderBy(i => depthOf[i]).ToList();
            for (int n = 0; n < planes.Count; n++)
            {
                bool floor = (seat is HoleSeat.Counterbore or HoleSeat.Spotface) && n == 0;
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
    /// hole's face order (Default, task 122). A new face whose role and centre the old hole had no face for (a seat
    /// that is new) takes the hand colour of the old bore at the same centre, when that bore had one (Aidan,
    /// 2026-11-07; task 140); a face the old hole had but did not colour stays as Inventor made it. A new face with no
    /// key (one that could not be read) takes none, and an old colour whose role the new hole has no face for (a seat
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
        var oldFaces = new HashSet<HoleFaceKey>();
        for (int i = 0; i < oldKeys.Count; i++)
        {
            if (oldKeys[i] is not { } key)
                continue;
            oldFaces.Add(key);
            if (oldColours[i] is { } colour)
                byKey.TryAdd(key, colour);
        }

        for (int i = 0; i < newKeys.Count; i++)
        {
            if (newKeys[i] is not { } key)
                continue;
            if (byKey.TryGetValue(key, out string? colour))
                carried[i] = colour;
            else if (!oldFaces.Contains(key)
                && byKey.TryGetValue(new HoleFaceKey(HoleFaceRole.Bore, key.CentreIndex), out string? bore))
                carried[i] = bore;
        }

        return carried;
    }

    /// <summary>
    /// The hand colour each face takes after the hole is changed in place (task 152), in the order of
    /// <paramref name="newKeys"/>; null leaves a face as it is. Inventor keeps a face's colour across an in-place change
    /// and makes the faces it adds uncoloured, so only a face that is new, one whose role and centre the old hole had no
    /// face for, takes a colour, by the <see cref="Carry"/> rule: the bore's colour at its centre, or the whole hole's
    /// one colour. A face the old hole had keeps its own colour or lack of one, and a face with no key takes none (it
    /// may be one that existed).
    /// </summary>
    /// <param name="oldKeys">The hole's face keys before the change (<see cref="Classify"/>).</param>
    /// <param name="oldColours">The hand colour on each face before the change; null for none.</param>
    /// <param name="newKeys">The hole's face keys after the change.</param>
    public static IReadOnlyList<string?> CarryToNewFaces(
        IReadOnlyList<HoleFaceKey?> oldKeys, IReadOnlyList<string?> oldColours, IReadOnlyList<HoleFaceKey?> newKeys)
    {
        IReadOnlyList<string?> carried = Carry(oldKeys, oldColours, newKeys);
        var oldFaces = new HashSet<HoleFaceKey>(oldKeys.Where(k => k != null).Select(k => k!.Value));

        var result = new string?[newKeys.Count];
        for (int i = 0; i < newKeys.Count; i++)
        {
            if (newKeys[i] is { } key && !oldFaces.Contains(key))
                result[i] = carried[i];
        }

        return result;
    }

    /// <summary>
    /// The appearance sources Inventor reports for a colour the hole inherits rather than one set on it: from the body,
    /// the part, the material, or none at all. Names of Inventor's <c>AppearanceSourceTypeEnum</c>.
    /// </summary>
    private static readonly HashSet<string> InheritedSources = new(StringComparer.Ordinal)
    {
        "kBodyAppearance", "kPartAppearance", "kMaterialAppearance", "kDefaultAppearance",
    };

    /// <summary>
    /// True when a hole feature's appearance source, the name of Inventor's <c>AppearanceSourceTypeEnum</c> value,
    /// means a colour set on the feature by hand, so it is kept when the hole is made again (task 140). Any source but
    /// the body, part, material or default counts: a Feature Properties colour may report a feature or an override
    /// source. Null (a source that could not be read) does not.
    /// </summary>
    public static bool IsFeatureHandSource(string? source) =>
        !string.IsNullOrEmpty(source) && !InheritedSources.Contains(source);

    /// <summary>
    /// True when a hole feature's own colour is kept across a remake (task 140): its source is one set on the feature
    /// (<see cref="IsFeatureHandSource"/>) and at least one face reports a source other than an override, so some face
    /// takes its colour from the feature. When every face carries its own override, the feature's source may only
    /// reflect those overrides, and keeping it would spread one face's colour over the whole new hole; the faces'
    /// colours are carried face by face instead. A face whose source could not be read (null) proves nothing.
    /// </summary>
    /// <param name="featureSource">The feature's <c>AppearanceSourceTypeEnum</c> name.</param>
    /// <param name="faceSources">Each face's <c>AppearanceSourceTypeEnum</c> name; null where unread.</param>
    public static bool KeepFeatureColour(string? featureSource, IReadOnlyList<string?> faceSources)
    {
        ArgumentNullException.ThrowIfNull(faceSources);
        return IsFeatureHandSource(featureSource)
            && faceSources.Any(s => !string.IsNullOrEmpty(s) && s != OverrideSource);
    }

    private const string OverrideSource = "kOverrideAppearance";

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
