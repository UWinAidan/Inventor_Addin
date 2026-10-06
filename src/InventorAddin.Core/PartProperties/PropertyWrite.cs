namespace InventorAddin.Core.PartProperties;

/// <summary>What a <see cref="PropertyWrite"/> does.</summary>
public enum PropertyWriteAction
{
    /// <summary>Set the value, adding the property first when it is a custom one the file does not have.</summary>
    Set,

    /// <summary>Delete a custom property.</summary>
    Remove,
}

/// <summary>
/// One iProperty write the add-in makes on Apply. A Set carries a typed value matching the location's
/// <see cref="PropertyValueKind"/>: <see cref="TextValue"/> for Text, <see cref="CurrencyValue"/> for Currency.
/// A Remove carries no value and is only made for custom properties. Create with the factory methods.
/// </summary>
public sealed record PropertyWrite
{
    private PropertyWrite(PropertyWriteAction action, PropertyLocation location, string? textValue, decimal? currencyValue)
    {
        Action = action;
        Location = location;
        TextValue = textValue;
        CurrencyValue = currencyValue;
    }

    public PropertyWriteAction Action { get; }

    public PropertyLocation Location { get; }

    /// <summary>The text to write, for a Set of a Text property; null otherwise.</summary>
    public string? TextValue { get; }

    /// <summary>The value to write, for a Set of a Currency property; null otherwise.</summary>
    public decimal? CurrencyValue { get; }

    /// <summary>Sets a Text property to <paramref name="value"/> (which may be <c>""</c>).</summary>
    public static PropertyWrite SetText(PropertyLocation location, string value)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(value);
        if (location.Kind != PropertyValueKind.Text)
        {
            throw new ArgumentException($"'{location.PropertyName}' is not a text property.", nameof(location));
        }

        return new PropertyWrite(PropertyWriteAction.Set, location, value, null);
    }

    /// <summary>Sets a Currency property to <paramref name="value"/>.</summary>
    public static PropertyWrite SetCurrency(PropertyLocation location, decimal value)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (location.Kind != PropertyValueKind.Currency)
        {
            throw new ArgumentException($"'{location.PropertyName}' is not a currency property.", nameof(location));
        }

        return new PropertyWrite(PropertyWriteAction.Set, location, null, value);
    }

    /// <summary>Removes a custom property. Standard properties cannot be removed.</summary>
    public static PropertyWrite Remove(PropertyLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        if (!location.IsCustom)
        {
            throw new ArgumentException(
                $"'{location.PropertyName}' is a standard property and cannot be removed.", nameof(location));
        }

        return new PropertyWrite(PropertyWriteAction.Remove, location, null, null);
    }

    /// <summary>A line for the log, for example <c>Set Design Tracking Properties/Designer = "A. Person"</c>.</summary>
    public override string ToString()
    {
        var target = $"{Location.SetName}/{Location.PropertyName}";
        return Action switch
        {
            PropertyWriteAction.Remove => $"Remove {target}",
            _ when Location.Kind == PropertyValueKind.Currency =>
                $"Set {target} = {CurrencyValue?.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            _ => $"Set {target} = \"{TextValue}\"",
        };
    }
}
