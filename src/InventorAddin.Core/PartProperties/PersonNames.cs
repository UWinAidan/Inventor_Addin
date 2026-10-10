namespace InventorAddin.Core.PartProperties;

/// <summary>
/// Rules about the person names a file holds (spec 02, "Windows user name as Designer or Detailer").
/// Old files carry the Windows login name as Designer and Detailer; Part Properties treats that name as blank.
/// </summary>
public static class PersonNames
{
    /// <summary>
    /// True when <paramref name="value"/> equals <paramref name="windowsUserName"/>, ignoring case and surrounding
    /// spaces on both sides, so it counts as blank. False when <paramref name="windowsUserName"/> is blank, and
    /// false when <paramref name="defaultDesigner"/> is blank: with nothing to replace it the name is kept as it is
    /// (a default), so Apply never clears a Designer.
    /// </summary>
    public static bool IsWindowsUserPlaceholder(string? value, string? windowsUserName, string? defaultDesigner)
    {
        if (PropertyValues.NormaliseText(defaultDesigner).Length == 0)
            return false;

        var user = PropertyValues.NormaliseText(windowsUserName);
        if (user.Length == 0)
            return false;

        return string.Equals(PropertyValues.NormaliseText(value), user, StringComparison.OrdinalIgnoreCase);
    }
}
