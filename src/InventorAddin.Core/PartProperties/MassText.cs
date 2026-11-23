using System.Globalization;
using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.PartProperties;

/// <summary>
/// The text of the custom "Mass" property (task 222, spec 05 question 29): the mass with 2 decimals, the invariant period and
/// the unit after a space, "4.33 kg" on a metric document and "9.55 lb" on an imperial one. Inventor's mass is in kilograms.
/// </summary>
public static class MassText
{
    /// <summary>Pounds in one kilogram (the international avoirdupois pound, 0.45359237 kg).</summary>
    public const double PoundsPerKilogram = 1.0 / 0.45359237;

    /// <summary>The text for a mass in kilograms, or null when the mass is not a usable number (null, NaN, infinite or negative).</summary>
    public static string? For(double? massKg, PrintUnits units)
    {
        if (massKg is not { } kg || double.IsNaN(kg) || double.IsInfinity(kg) || kg < 0)
        {
            return null;
        }

        return units == PrintUnits.Imperial
            ? (kg * PoundsPerKilogram).ToString("0.00", CultureInfo.InvariantCulture) + " lb"
            : kg.ToString("0.00", CultureInfo.InvariantCulture) + " kg";
    }

    /// <summary>
    /// The write that brings the stored Mass up to date, or null when nothing is to be written: the mass is unusable, or the stored
    /// text (trimmed) already equals the new text. A stored value that differs is a refresh (<see cref="PropertyWrite.IsRefresh"/>),
    /// a blank one a fill.
    /// </summary>
    public static PropertyWrite? WriteFor(double? massKg, PrintUnits units, string? storedMass)
    {
        var text = For(massKg, units);
        if (text is null)
        {
            return null;
        }

        var stored = PropertyValues.NormaliseText(storedMass);
        if (string.Equals(stored, text, StringComparison.Ordinal))
        {
            return null;
        }

        return PropertyWrite.SetText(PartPropertyStorage.Mass, text) with { IsRefresh = stored.Length > 0 };
    }
}
