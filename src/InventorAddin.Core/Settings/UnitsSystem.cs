namespace InventorAddin.Core.Settings;

/// <summary>
/// The units new files are created in (spec 02, "Templates"): which of Inventor's default templates is used.
/// Stored in the settings file as the member name (<c>"Metric"</c>, <c>"Imperial"</c>).
/// </summary>
public enum UnitsSystem
{
    Metric,
    Imperial,
}
