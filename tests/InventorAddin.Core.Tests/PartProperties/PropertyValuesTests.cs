using System.Globalization;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.PartProperties;

/// <summary>
/// Every test passes its culture explicitly, so none depends on the machine's culture.
/// </summary>
public sealed class PropertyValuesTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");
    private static readonly CultureInfo FrFr = CultureInfo.GetCultureInfo("fr-FR");

    public static TheoryData<string> Cultures() => new() { "en-US", "de-DE", "fr-FR" };

    private static CultureInfo Culture(string name) => CultureInfo.GetCultureInfo(name);

    // ---- NormaliseText ----

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\t \r\n", "")]
    [InlineData("  Bracket  ", "Bracket")]
    [InlineData("Base  plate", "Base  plate")]
    [InlineData(" A B ", "A B")]
    public void NormaliseText_TrimsAndKeepsInnerWhitespace(string? input, string expected)
    {
        Assert.Equal(expected, PropertyValues.NormaliseText(input));
    }

    // ---- ValidateText ----

    [Fact]
    public void MaxTextLength_Is255()
    {
        Assert.Equal(255, PropertyValues.MaxTextLength);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("J. Smith")]
    [InlineData("  padded  ")]
    [InlineData("trailing line break is trimmed\r\n")]
    public void ValidateText_ValidText_ReturnsNull(string? text)
    {
        Assert.Null(PropertyValues.ValidateText("Designer", text));
    }

    [Fact]
    public void ValidateText_ExactlyMaxLength_IsValid()
    {
        Assert.Null(PropertyValues.ValidateText("Designer", new string('x', 255)));
    }

    [Fact]
    public void ValidateText_MaxLengthAfterTrimming_IsValid()
    {
        Assert.Null(PropertyValues.ValidateText("Designer", "  " + new string('x', 255) + "  "));
    }

    [Fact]
    public void ValidateText_TooLong_NamesFieldAndLimit()
    {
        var message = PropertyValues.ValidateText("Detailer", new string('x', 256));

        Assert.NotNull(message);
        Assert.StartsWith("Detailer", message);
        Assert.Contains("255", message);
    }

    [Theory]
    [InlineData("two\nlines")]
    [InlineData("two\r\nlines")]
    [InlineData("two\rlines")]
    [InlineData("two\u2028lines")]
    public void ValidateText_LineBreak_NamesFieldAndSaysOneLine(string text)
    {
        var message = PropertyValues.ValidateText("Designer", text);

        Assert.NotNull(message);
        Assert.StartsWith("Designer", message);
        Assert.Contains("one line", message);
    }

    [Theory]
    [InlineData("tab\there")]
    [InlineData("bell\u0007here")]
    [InlineData("null\0here")]
    public void ValidateText_ControlCharacter_NamesFieldAndSaysControlCharacters(string text)
    {
        var message = PropertyValues.ValidateText("Designer", text);

        Assert.NotNull(message);
        Assert.StartsWith("Designer", message);
        Assert.Contains("control characters", message);
    }

    // ---- TryParseCost: blank ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseCost_Blank_IsValidAndNull(string? text)
    {
        foreach (var culture in new[] { EnUs, DeDe, FrFr })
        {
            Assert.True(PropertyValues.TryParseCost(text, culture, out var cost, out var error));
            Assert.Null(cost);
            Assert.Null(error);
        }
    }

    // ---- TryParseCost: accepted numbers ----

    [Theory]
    [InlineData("en-US", "12", "12")]
    [InlineData("en-US", "12.5", "12.5")]
    [InlineData("en-US", " 12.50 ", "12.5")]
    [InlineData("en-US", "0", "0")]
    [InlineData("en-US", "0.0001", "0.0001")]
    [InlineData("en-US", "12.3456", "12.3456")]
    [InlineData("en-US", "1,234.5", "1234.5")]
    [InlineData("en-US", "1,234,567.89", "1234567.89")]
    [InlineData("en-US", "$12.50", "12.5")]
    [InlineData("en-US", "$1,234.50", "1234.5")]
    [InlineData("de-DE", "12", "12")]
    [InlineData("de-DE", "12,5", "12.5")]
    [InlineData("de-DE", "1.234,5", "1234.5")]
    [InlineData("de-DE", "12,50 \u20AC", "12.5")]
    [InlineData("de-DE", "12,50\u20AC", "12.5")]
    [InlineData("de-DE", "1.234,50 \u20AC", "1234.5")]
    [InlineData("fr-FR", "12,5", "12.5")]
    [InlineData("fr-FR", "1 234,5", "1234.5")]
    [InlineData("fr-FR", "1\u00A0234,5", "1234.5")]
    [InlineData("fr-FR", "1\u202F234,5", "1234.5")]
    [InlineData("fr-FR", "12,50 \u20AC", "12.5")]
    [InlineData("fr-FR", "1 234,50 \u20AC", "1234.5")]
    public void TryParseCost_AcceptsCultureFormat(string cultureName, string text, string expected)
    {
        Assert.True(PropertyValues.TryParseCost(text, Culture(cultureName), out var cost, out var error), error);
        Assert.Null(error);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), cost);
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void TryParseCost_AcceptsWhatTheCultureFormatsWithCurrencySymbol(string cultureName)
    {
        var culture = Culture(cultureName);
        var text = 1234.5m.ToString("C", culture);

        Assert.True(PropertyValues.TryParseCost(text, culture, out var cost, out var error), $"{text}: {error}");
        Assert.Equal(1234.5m, cost);
    }

    [Fact]
    public void TryParseCost_DropsTrailingZeros()
    {
        Assert.True(PropertyValues.TryParseCost("12.5000", EnUs, out var cost, out _));

        Assert.Equal("12.5", cost!.Value.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TryParseCost_TrailingZerosBeyondFourPlaces_AreNotCountedAsDecimalPlaces()
    {
        Assert.True(PropertyValues.TryParseCost("12.50000", EnUs, out var cost, out _));
        Assert.Equal(12.5m, cost);
    }

    // ---- TryParseCost: rejected ----

    [Theory]
    [InlineData("en-US", "-1")]
    [InlineData("en-US", "-0.01")]
    [InlineData("en-US", "-$12.50")]
    [InlineData("de-DE", "-12,5")]
    [InlineData("fr-FR", "-1 234,5")]
    public void TryParseCost_Negative_IsRejected(string cultureName, string text)
    {
        Assert.False(PropertyValues.TryParseCost(text, Culture(cultureName), out var cost, out var error));
        Assert.Null(cost);
        Assert.Equal("Cost must be zero or more.", error);
    }

    [Theory]
    [InlineData("en-US", "abc")]
    [InlineData("en-US", "12abc")]
    [InlineData("en-US", "12.5.1")]
    [InlineData("en-US", "1e3")]
    [InlineData("en-US", "\u20AC12.50")]
    [InlineData("en-US", "12 50")]
    [InlineData("de-DE", "12,5,1")]
    [InlineData("de-DE", "$12")]
    [InlineData("fr-FR", "douze")]
    public void TryParseCost_NotANumber_IsRejected(string cultureName, string text)
    {
        Assert.False(PropertyValues.TryParseCost(text, Culture(cultureName), out var cost, out var error));
        Assert.Null(cost);
        Assert.Equal(PropertyValues.CostNotNumberMessage, error);
        Assert.Contains("number", error);
    }

    [Theory]
    [InlineData("en-US", "12.34567")]
    [InlineData("en-US", "0.00001")]
    [InlineData("de-DE", "12,34567")]
    [InlineData("fr-FR", "12,34567")]
    public void TryParseCost_FiveDecimalPlaces_IsRejected(string cultureName, string text)
    {
        Assert.False(PropertyValues.TryParseCost(text, Culture(cultureName), out var cost, out var error));
        Assert.Null(cost);
        Assert.Equal("Cost can have at most 4 decimal places.", error);
    }

    [Fact]
    public void TryParseCost_BeyondCurrencyRange_IsRejected()
    {
        Assert.False(PropertyValues.TryParseCost("1000000000000000", EnUs, out var cost, out var error));
        Assert.Null(cost);
        Assert.Equal(PropertyValues.CostTooLargeMessage, error);
    }

    [Fact]
    public void TryParseCost_LargestCurrencyValue_IsAccepted()
    {
        Assert.True(PropertyValues.TryParseCost("922337203685477.5807", EnUs, out var cost, out _));
        Assert.Equal(PropertyValues.MaxCost, cost);
    }

    [Fact]
    public void TryParseCost_UsesGivenCultureNotSeparatorGuessing()
    {
        // "1.234" is one thousand two hundred and thirty-four in de-DE, and 1.234 in en-US.
        Assert.True(PropertyValues.TryParseCost("1.234", DeDe, out var de, out _));
        Assert.True(PropertyValues.TryParseCost("1.234", EnUs, out var en, out _));

        Assert.Equal(1234m, de);
        Assert.Equal(1.234m, en);
    }

    // ---- FormatCost ----

    [Theory]
    [MemberData(nameof(Cultures))]
    public void FormatCost_NullAndZero_AreBlank(string cultureName)
    {
        var culture = Culture(cultureName);

        Assert.Equal(string.Empty, PropertyValues.FormatCost(null, culture));
        Assert.Equal(string.Empty, PropertyValues.FormatCost(0m, culture));
        Assert.Equal(string.Empty, PropertyValues.FormatCost(0.0000m, culture));
    }

    [Theory]
    [InlineData("12.5", "12.50")]
    [InlineData("12", "12.00")]
    [InlineData("12.3456", "12.3456")]
    [InlineData("12.345", "12.345")]
    [InlineData("12.3000", "12.30")]
    [InlineData("0.0001", "0.0001")]
    [InlineData("1234.5", "1,234.50")]
    [InlineData("1234567.891", "1,234,567.891")]
    public void FormatCost_EnUs(string value, string expected)
    {
        Assert.Equal(expected, PropertyValues.FormatCost(decimal.Parse(value, CultureInfo.InvariantCulture), EnUs));
    }

    [Theory]
    [InlineData("12.5", "12,50")]
    [InlineData("12.3456", "12,3456")]
    [InlineData("1234.5", "1.234,50")]
    public void FormatCost_DeDe(string value, string expected)
    {
        Assert.Equal(expected, PropertyValues.FormatCost(decimal.Parse(value, CultureInfo.InvariantCulture), DeDe));
    }

    [Fact]
    public void FormatCost_FrFr_UsesCultureSeparators()
    {
        var text = PropertyValues.FormatCost(1234.5m, FrFr);
        var format = FrFr.NumberFormat;

        Assert.Equal($"1{format.NumberGroupSeparator}234{format.NumberDecimalSeparator}50", text);
        Assert.Equal("12,50", PropertyValues.FormatCost(12.5m, FrFr));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void FormatCost_HasNoCurrencySymbol(string cultureName)
    {
        var culture = Culture(cultureName);

        Assert.DoesNotContain(culture.NumberFormat.CurrencySymbol, PropertyValues.FormatCost(1234.5m, culture));
    }

    // ---- Round trip ----

    public static TheoryData<string, string> RoundTripCases()
    {
        var data = new TheoryData<string, string>();
        foreach (var culture in new[] { "en-US", "de-DE", "fr-FR" })
        {
            foreach (var value in new[] { "0.0001", "1", "12.5", "12.3456", "999.99", "1234.5", "1234567.8912" })
            {
                data.Add(culture, value);
            }
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(RoundTripCases))]
    public void FormatThenParse_RoundTrips(string cultureName, string value)
    {
        var culture = Culture(cultureName);
        var original = decimal.Parse(value, CultureInfo.InvariantCulture);

        var text = PropertyValues.FormatCost(original, culture);

        Assert.True(PropertyValues.TryParseCost(text, culture, out var parsed, out var error), $"{text}: {error}");
        Assert.Equal(original, parsed);
        Assert.Equal(text, PropertyValues.FormatCost(parsed, culture));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void StoredZero_ShowsBlank_AndBlankStoresZero(string cultureName)
    {
        var culture = Culture(cultureName);

        var shown = PropertyValues.FormatCost(0m, culture);
        Assert.Equal(string.Empty, shown);

        Assert.True(PropertyValues.TryParseCost(shown, culture, out var parsed, out _));
        Assert.Null(parsed);
        Assert.Equal(0m, PropertyValues.CostToStore(parsed));
    }

    [Theory]
    [MemberData(nameof(Cultures))]
    public void TypedZero_IsValid_AndShowsBlank(string cultureName)
    {
        var culture = Culture(cultureName);

        Assert.True(PropertyValues.TryParseCost("0", culture, out var parsed, out _));
        Assert.Equal(0m, parsed);
        Assert.Equal(string.Empty, PropertyValues.FormatCost(parsed, culture));
    }

    // ---- CostToStore ----

    [Fact]
    public void CostToStore_NullIsZero()
    {
        Assert.Equal(0m, PropertyValues.CostToStore(null));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("12.5")]
    [InlineData("12.3456")]
    public void CostToStore_ValueIsUnchanged(string value)
    {
        var cost = decimal.Parse(value, CultureInfo.InvariantCulture);

        Assert.Equal(cost, PropertyValues.CostToStore(cost));
    }
}
