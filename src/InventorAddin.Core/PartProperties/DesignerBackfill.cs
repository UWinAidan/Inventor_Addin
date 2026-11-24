namespace InventorAddin.Core.PartProperties;

/// <summary>
/// Create Print's Designer rule (task 236; Aidan, 2026-11-23): an old file shows the Windows login as Designer because Inventor
/// set it before the add-in existed. The login is replaced by the Settings name; any other name is left alone.
/// </summary>
public static class DesignerBackfill
{
    /// <summary>
    /// The writes that replace a login-name Designer with <paramref name="settingsName"/>: Designer when it equals
    /// <paramref name="windowsLogin"/> (trimmed, ignoring case), and Author the same way, since Author is kept equal to Designer
    /// (spec 02). Empty when the login or the Settings name is blank, when the Settings name is the same text already, or
    /// when the stored value is blank or another name. Other names that mean the user are not used here.
    /// </summary>
    public static IReadOnlyList<PropertyWrite> For(
        string? storedDesigner, string? storedAuthor, string? windowsLogin, string? settingsName)
    {
        var login = PropertyValues.NormaliseText(windowsLogin);
        var name = PropertyValues.NormaliseText(settingsName);
        var writes = new List<PropertyWrite>();
        if (login.Length == 0 || name.Length == 0)
        {
            return writes.AsReadOnly();
        }

        AddIfLogin(writes, PartPropertyStorage.Designer, storedDesigner, login, name);
        AddIfLogin(writes, PartPropertyStorage.Author, storedAuthor, login, name);
        return writes.AsReadOnly();
    }

    private static void AddIfLogin(
        List<PropertyWrite> writes, PropertyLocation location, string? stored, string login, string name)
    {
        var text = PropertyValues.NormaliseText(stored);
        if (string.Equals(text, login, StringComparison.OrdinalIgnoreCase) && !string.Equals(text, name, StringComparison.Ordinal))
        {
            writes.Add(PropertyWrite.SetText(location, name));
        }
    }
}
