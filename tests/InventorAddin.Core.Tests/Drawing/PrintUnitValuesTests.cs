using InventorAddin.Core.Drawing;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class PrintUnitValuesTests
{
    private static readonly Finish[] List =
    {
        new("TF1", "Test finish", FinishKind.Plating, [], null, "0.013", "0.025", true, "NOTE.", null),
    };

    private static IReadOnlyList<PropertyWrite> Writes(PrintUnits part, PrintUnits print, bool assembly = false, string? spec = "", double? kg = 4.5) =>
        PrintUnitValues.WritesFor(part, print, assembly, kg, "Test finish", "0.013-0.025 mm", spec, List);

    private static string? Text(IReadOnlyList<PropertyWrite> writes, PropertyLocation location) =>
        writes.SingleOrDefault(w => w.Location == location)?.TextValue;

    [Fact]
    public void SameUnits_WritesNothing() => Assert.Empty(Writes(PrintUnits.Metric, PrintUnits.Metric));

    [Fact]
    public void InchPartOnMetricPrint_GivesMicrometresAndKilograms()
    {
        var writes = Writes(PrintUnits.Imperial, PrintUnits.Metric, spec: FinishProperties.SpecText(List[0], PrintUnits.Imperial));
        Assert.Equal(FinishProperties.SpecText(List[0], PrintUnits.Metric), Text(writes, PartPropertyStorage.FinishSpec));
        Assert.Contains("µm", Text(writes, PartPropertyStorage.FinishSpec));
        Assert.Equal("4.50 kg", Text(writes, PartPropertyStorage.Mass));
    }

    [Fact]
    public void MetricPartOnImperialPrint_GivesInchesAndPounds()
    {
        var writes = Writes(PrintUnits.Metric, PrintUnits.Imperial, spec: FinishProperties.SpecText(List[0], PrintUnits.Metric));
        Assert.EndsWith(" IN", Text(writes, PartPropertyStorage.FinishSpec));
        Assert.Equal("9.92 lb", Text(writes, PartPropertyStorage.Mass));
    }

    [Fact]
    public void BlankStoredSpec_IsBuiltFromTheFinish()
    {
        var writes = Writes(PrintUnits.Metric, PrintUnits.Imperial, spec: null);
        Assert.Equal(FinishProperties.SpecText(List[0], PrintUnits.Imperial), Text(writes, PartPropertyStorage.FinishSpec));
    }

    [Fact]
    public void HandWrittenSpec_IsCopiedAsItIs()
    {
        var writes = Writes(PrintUnits.Metric, PrintUnits.Imperial, spec: "Hand written finish");
        Assert.Equal("Hand written finish", Text(writes, PartPropertyStorage.FinishSpec));
    }

    [Fact]
    public void Assembly_GetsMassOnly()
    {
        var writes = Writes(PrintUnits.Metric, PrintUnits.Imperial, assembly: true, spec: "-");
        Assert.Single(writes);
        Assert.Equal(PartPropertyStorage.Mass, writes[0].Location);
    }

    [Fact]
    public void UnusableMass_IsLeftOut()
    {
        var writes = Writes(PrintUnits.Metric, PrintUnits.Imperial, kg: null);
        Assert.Null(Text(writes, PartPropertyStorage.Mass));
        Assert.NotNull(Text(writes, PartPropertyStorage.FinishSpec));
    }

    [Fact]
    public void PointAtDrawing_ChangesOnlyTheNamedFields()
    {
        const string formatted =
            "<StyleOverride Font='x'><Property Document='model' PropertySet='Inventor User Defined Properties' Property='Mass' FormatID='{D5CDD505}'>Mass</Property>" +
            "<Property Document=\"model\" PropertySet='Inventor User Defined Properties' Property=\"Finish Spec\" FormatID='{D5CDD505}'>F</Property>" +
            "<Property Document='model' PropertySet='Inventor User Defined Properties' Property='Material Size' FormatID='{D5CDD505}'>M</Property></StyleOverride>";

        string result = PrintUnitValues.PointAtDrawing(formatted, new[] { "Mass", "Finish Spec" });

        Assert.Contains("Document='drawing' PropertySet='Inventor User Defined Properties' Property='Mass'", result);
        Assert.Contains("Document=\"drawing\" PropertySet='Inventor User Defined Properties' Property=\"Finish Spec\"", result);
        Assert.Contains("Document='model' PropertySet='Inventor User Defined Properties' Property='Material Size'", result);
    }

    [Fact]
    public void PointAtDrawing_NeverRepointsABuiltInField()
    {
        const string builtIn = "<Property Document='model' PropertySet='Design Tracking Properties' Property='Mass' FormatID='{32853F0F}' PropertyID='99'>Mass</Property>";
        Assert.Equal(builtIn, PrintUnitValues.PointAtDrawing(builtIn, new[] { "Mass" }));
    }

    [Fact]
    public void PointAtDrawing_KeepsAPropertyIdOnACustomField()
    {
        const string custom = "<Property Document='model' PropertySet='Inventor User Defined Properties' Property='Mass' FormatID='{D5CDD505}' PropertyID='7'>Mass</Property>";
        Assert.Contains("Document='drawing'", PrintUnitValues.PointAtDrawing(custom, new[] { "Mass" }));
        Assert.Contains("PropertyID='7'", PrintUnitValues.PointAtDrawing(custom, new[] { "Mass" }));
    }

    [Fact]
    public void PointAtDrawing_LeavesOtherTextAlone()
    {
        const string formatted = "<StyleOverride>MM</StyleOverride>";
        Assert.Equal(formatted, PrintUnitValues.PointAtDrawing(formatted, new[] { "Mass" }));
    }
}
