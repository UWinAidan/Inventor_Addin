namespace InventorAddin.Core.PartProperties;

/// <summary>
/// Rules about the person names a file holds (spec 02, "Windows user name as Designer or Detailer" and "Other names
/// that mean me"). Old files carry the Windows login name, or another name of the same person, as Designer and
/// Detailer; Part Properties treats such a name as blank and Mass Update offers to replace it.
/// </summary>
public static class PersonNames
{
    /// <summary>
    /// The names that mean the user: <paramref name="windowsUserName"/> first, then each comma-separated entry of
    /// <paramref name="otherNames"/> (Settings, Other names that mean me). Each is trimmed, blanks are dropped, and a
    /// name equal to an earlier one ignoring case is dropped. Empty when both are blank.
    /// </summary>
    public static IReadOnlyList<string> NamesThatMeanMe(string? windowsUserName, string? otherNames)
    {
        var names = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? candidate)
        {
            var name = PropertyValues.NormaliseText(candidate);
            if (name.Length > 0 && seen.Add(name))
            {
                names.Add(name);
            }
        }

        Add(windowsUserName);
        foreach (var entry in (otherNames ?? string.Empty).Split(','))
        {
            Add(entry);
        }

        return names.AsReadOnly();
    }

    /// <summary>
    /// True when <paramref name="value"/>, trimmed, equals one of <paramref name="names"/> (each trimmed), ignoring
    /// case. A blank value or a blank name never matches.
    /// </summary>
    public static bool MeansMe(string? value, IReadOnlyCollection<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var text = PropertyValues.NormaliseText(value);
        if (text.Length == 0)
            return false;

        return names.Any(n => string.Equals(PropertyValues.NormaliseText(n), text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// True when <paramref name="value"/> means the user (<see cref="MeansMe"/>), so it counts as blank. False when
    /// <paramref name="defaultDesigner"/> is blank: with nothing to replace it the name is kept as it is (a default),
    /// so Apply never clears a Designer.
    /// </summary>
    public static bool IsMePlaceholder(string? value, IReadOnlyCollection<string> names, string? defaultDesigner)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (PropertyValues.NormaliseText(defaultDesigner).Length == 0)
            return false;

        return MeansMe(value, names);
    }
}
