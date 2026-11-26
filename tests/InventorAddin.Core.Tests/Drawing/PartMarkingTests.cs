using InventorAddin.Core.Drawing;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class PartMarkingTests
{
    [Fact]
    public void Note_text_metric_with_extra_lines()
    {
        var o = new PartMarkingOptions { ExtraLines = ["DO NOT STAMP", "  ", " KEEP CLEAN "] };
        Assert.Equal(
            "LASER ENGRAVE PART NUMBER, 3 mm CHARACTERS\nDO NOT STAMP\nKEEP CLEAN",
            PartMarking.NoteText(o));
    }

    [Fact]
    public void Note_text_imperial_uses_inches()
    {
        var o = new PartMarkingOptions { Method = PartMarkingMethod.Stamp, Units = UnitsSystem.Imperial };
        Assert.Equal("STAMP PART NUMBER, 0.12 in CHARACTERS", PartMarking.NoteText(o));
    }

    [Fact]
    public void Method_none_gives_only_the_extra_lines()
    {
        var o = new PartMarkingOptions { Method = PartMarkingMethod.None, ExtraLines = ["DO NOT STAMP"] };
        Assert.Equal("DO NOT STAMP", PartMarking.NoteText(o));
        Assert.Equal(string.Empty, PartMarking.NoteText(o with { ExtraLines = [] }));
    }

    [Theory]
    [InlineData(PartMarkingMethod.Etch, "ETCH PART NUMBER, 3 mm CHARACTERS")]
    [InlineData(PartMarkingMethod.Ink, "INK MARK PART NUMBER, 3 mm CHARACTERS")]
    public void Each_method_has_its_phrase(PartMarkingMethod method, string expected) =>
        Assert.Equal(expected, PartMarking.NoteText(new PartMarkingOptions { Method = method }));

    [Fact]
    public void Zone_text_and_sizes()
    {
        Assert.Equal("MARKING MUST BE PLACED INSIDE THIS ZONE", PartMarking.ZoneText);
        Assert.Equal("40 x 15 mm", PartMarking.ZoneSizeText(UnitsSystem.Metric));
        Assert.Equal("1.5 x 0.6 in", PartMarking.ZoneSizeText(UnitsSystem.Imperial));
        Assert.Equal(4.0, PartMarking.ZoneWidthCm(UnitsSystem.Metric), 6);
        Assert.Equal(1.5, PartMarking.ZoneHeightCm(UnitsSystem.Metric), 6);
        Assert.Equal(3.81, PartMarking.ZoneWidthCm(UnitsSystem.Imperial), 6);
        Assert.Equal(0.3, PartMarking.CharacterHeightCm(UnitsSystem.Metric), 6);
        Assert.Equal(0.02, PartMarking.EngraveDepthCm(UnitsSystem.Metric), 6);
        Assert.Equal(0.02032, PartMarking.EngraveDepthCm(UnitsSystem.Imperial), 6);
    }

    [Fact]
    public void Engrave_feature_is_off_for_an_assembly_and_for_method_none()
    {
        var part = new PartMarkingOptions { EngraveFeature = true };
        Assert.True(PartMarking.WillEngraveFeature(part));
        Assert.False(PartMarking.WillEngraveFeature(part with { IsAssembly = true }));
        Assert.False(PartMarking.WillEngraveFeature(part with { Method = PartMarkingMethod.None }));
    }

    [Theory]
    [InlineData("#0070C0", "#0070C0")]
    [InlineData("ff8800", "#FF8800")]
    [InlineData("  #abcdef ", "#ABCDEF")]
    [InlineData("blue", "#0070C0")]
    [InlineData("#12345", "#0070C0")]
    [InlineData("#GG0000", "#0070C0")]
    [InlineData(null, "#0070C0")]
    public void Colour_is_normalised(string? input, string expected) =>
        Assert.Equal(expected, PartMarking.NormaliseColour(input));

    [Fact]
    public void Colour_rgb() => Assert.Equal(((byte)0, (byte)0x70, (byte)0xC0), PartMarking.ColourRgb("#0070C0"));

    [Fact]
    public void Settings_colour_defaults_and_normalises()
    {
        var s = new AddinSettings();
        Assert.Equal("#0070C0", s.PartMarkingColour);
        s.PartMarkingColour = "nonsense";
        Assert.Equal("#0070C0", s.PartMarkingColour);
        s.PartMarkingColour = null!;
        Assert.Equal("#0070C0", s.PartMarkingColour);
    }

    [Fact]
    public void ViewModel_defaults_and_note()
    {
        var vm = new PartMarkingViewModel(UnitsSystem.Metric, isAssembly: false);
        Assert.Equal("3 mm", vm.CharacterHeightText);
        Assert.Equal("0.2 mm", vm.EngraveDepthText);
        Assert.Equal("40 x 15 mm", vm.ZoneSizeText);
        Assert.Equal("#0070C0", vm.HighlightColour);
        Assert.True(vm.CanApply);

        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.ExtraLinesText = "DO NOT STAMP\r\n\r\nSECOND";
        Assert.Equal("LASER ENGRAVE PART NUMBER, 3 mm CHARACTERS\nDO NOT STAMP\nSECOND", vm.NoteText);
        Assert.Contains(nameof(PartMarkingViewModel.NoteText), changed);
    }

    [Fact]
    public void ViewModel_imperial_sizes()
    {
        var vm = new PartMarkingViewModel(UnitsSystem.Imperial, false);
        Assert.Equal("0.12 in", vm.CharacterHeightText);
        Assert.Equal("0.008 in", vm.EngraveDepthText);
        Assert.Equal("1.5 x 0.6 in", vm.ZoneSizeText);
    }

    [Fact]
    public void ViewModel_engrave_is_off_for_an_assembly()
    {
        var vm = new PartMarkingViewModel(UnitsSystem.Metric, isAssembly: true) { EngraveFeature = true };
        Assert.False(vm.CanEngraveFeature);
        Assert.False(vm.EngraveFeature);
        Assert.False(vm.Options.EngraveFeature);
    }

    [Fact]
    public void ViewModel_engrave_follows_method_and_keeps_the_choice()
    {
        var vm = new PartMarkingViewModel(UnitsSystem.Metric, false) { EngraveFeature = true };
        Assert.True(vm.EngraveFeature);
        vm.Method = PartMarkingMethod.None;
        Assert.False(vm.EngraveFeature);
        vm.Method = PartMarkingMethod.Etch;
        Assert.True(vm.EngraveFeature);
    }

    [Fact]
    public void ViewModel_method_none_without_lines_cannot_apply()
    {
        var vm = new PartMarkingViewModel(UnitsSystem.Metric, false) { Method = PartMarkingMethod.None };
        Assert.False(vm.CanApply);
        Assert.Equal(PartMarkingViewModel.NothingToPlaceText, vm.ProblemText);
        vm.ExtraLinesText = "DO NOT STAMP";
        Assert.True(vm.CanApply);
        Assert.Equal("DO NOT STAMP", vm.NoteText);
    }

    [Fact]
    public void ViewModel_target_zone()
    {
        var vm = new PartMarkingViewModel(UnitsSystem.Metric, false, "#112233") { Target = PartMarkingTarget.Zone };
        Assert.True(vm.IsZone);
        Assert.Equal(PartMarkingTarget.Zone, vm.Options.Target);
        Assert.Equal("#112233", vm.Options.HighlightColour);
    }
}
