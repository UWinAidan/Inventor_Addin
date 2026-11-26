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

    [Theory]
    [InlineData(PartMarkingMethod.LaserEngrave, "LASER ENGRAVE PART NUMBER, 3 mm CHARACTERS")]
    [InlineData(PartMarkingMethod.Stamp, "STAMP PART NUMBER, 3 mm CHARACTERS")]
    [InlineData(PartMarkingMethod.Etch, "ETCH PART NUMBER, 3 mm CHARACTERS")]
    [InlineData(PartMarkingMethod.Ink, "INK MARK PART NUMBER, 3 mm CHARACTERS")]
    public void LeaderNoteLines_puts_the_part_number_after_the_method_line(PartMarkingMethod method, string methodLine)
    {
        var options = new PartMarkingOptions { Method = method, ExtraLines = ["DO NOT STAMP", "  ", "KEEP CLEAN"] };
        var lines = PartMarking.LeaderNoteLines(options);
        Assert.Equal(
            [new LeaderNoteLine(methodLine, false), new LeaderNoteLine(string.Empty, true), new LeaderNoteLine("DO NOT STAMP", false), new LeaderNoteLine("KEEP CLEAN", false)],
            lines);
    }

    [Fact]
    public void LeaderNoteLines_method_none_has_no_part_number_line()
    {
        var options = new PartMarkingOptions { Method = PartMarkingMethod.None, ExtraLines = ["DO NOT STAMP", "KEEP CLEAN"] };
        Assert.Equal([new LeaderNoteLine("DO NOT STAMP", false), new LeaderNoteLine("KEEP CLEAN", false)], PartMarking.LeaderNoteLines(options));
    }

    [Fact]
    public void LeaderNoteLines_method_none_without_lines_is_empty()
    {
        Assert.Empty(PartMarking.LeaderNoteLines(new PartMarkingOptions { Method = PartMarkingMethod.None }));
    }

    [Fact]
    public void LeaderNoteLines_a_typed_placeholder_is_not_the_part_number_line()
    {
        var options = new PartMarkingOptions { Method = PartMarkingMethod.None, ExtraLines = [PartMarking.PartNumberPlaceholder] };
        Assert.All(PartMarking.LeaderNoteLines(options), l => Assert.False(l.IsPartNumber));
    }

    [Fact]
    public void ViewModel_preview_shows_the_part_number_line_for_a_leader_only()
    {
        var vm = new PartMarkingViewModel(UnitsSystem.Metric, false) { ExtraLinesText = "DO NOT STAMP" };
        Assert.Equal("LASER ENGRAVE PART NUMBER, 3 mm CHARACTERS\n<part number>\nDO NOT STAMP", vm.PreviewText);
        Assert.DoesNotContain("<part number>", vm.NoteText);

        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.Target = PartMarkingTarget.Zone;
        Assert.Contains(nameof(PartMarkingViewModel.PreviewText), changed);
        Assert.Equal(vm.NoteText, vm.PreviewText);

        vm.Target = PartMarkingTarget.Leader;
        vm.Method = PartMarkingMethod.None;
        Assert.Equal("DO NOT STAMP", vm.PreviewText);
    }

    [Fact]
    public void Engrave_text_is_the_part_number_field_at_the_character_height()
    {
        string metric = PartMarking.EngraveFormattedText(UnitsSystem.Metric);
        Assert.StartsWith("<StyleOverride FontSize='0.3'>", metric);
        Assert.Contains("Property='Part Number'", metric);
        Assert.Contains("PropertyID='5'", metric);
        Assert.Contains("Document='model'", metric);
        Assert.StartsWith("<StyleOverride FontSize='0.305'>", PartMarking.EngraveFormattedText(UnitsSystem.Imperial));
    }

    [Fact]
    public void Engrave_feature_is_named_so_it_can_be_found() =>
        Assert.Equal("AWB Part Number", PartMarking.EngraveFeatureName);

    [Fact]
    public void Largest_area_picks_the_biggest_first_and_handles_none()
    {
        Assert.Equal(2, PartMarking.LargestAreaIndex([1.0, 4.5, 9.0, 3.0]));
        Assert.Equal(0, PartMarking.LargestAreaIndex([5.0, 5.0]));
        Assert.Equal(-1, PartMarking.LargestAreaIndex([]));
        Assert.Equal(-1, PartMarking.LargestAreaIndex([0.0]));
    }

    [Fact]
    public void View_model_offers_the_engrave_tick_only_for_a_part_with_a_method()
    {
        var vm = new PartMarkingViewModel(UnitsSystem.Metric, false, "#0070C0") { EngraveFeature = true };
        Assert.True(vm.Options.EngraveFeature);
        vm.Method = PartMarkingMethod.None;
        Assert.False(vm.CanEngraveFeature);
        Assert.False(vm.Options.EngraveFeature);
        Assert.False(new PartMarkingViewModel(UnitsSystem.Metric, true, "#0070C0") { EngraveFeature = true }.Options.EngraveFeature);
    }

    [Theory]
    [InlineData(10.0, 9.99, true)]
    [InlineData(10.0, 10.0, false)]
    [InlineData(10.0, 10.0 - 1e-12, false)]
    [InlineData(10.0, 10.5, false)]
    public void Engrave_removed_material_needs_a_real_volume_drop(double before, double after, bool expected) =>
        Assert.Equal(expected, PartMarking.EngraveRemovedMaterial(before, after));
}
