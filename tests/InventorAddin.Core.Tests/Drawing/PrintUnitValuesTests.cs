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

    // The text Inventor stores for the AWB template's own fields (read with a COM probe, task 244): the set is "User Defined Properties".
    private const string RealMass =
        "<StyleOverride FontSize='0.2036'><Property Document='model' PropertySet='User Defined Properties' Property='Mass' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}'>Mass</Property></StyleOverride>";

    private const string RealFinish =
        "<StyleOverride FontSize='0.2036'><Property Document='model' PropertySet='User Defined Properties' Property='Finish Spec' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}'>Finish Spec</Property></StyleOverride>";

    [Fact]
    public void PointAtDrawing_RepointsTheRealTemplateText()
    {
        string mass = PrintUnitValues.PointAtDrawing(RealMass, PrintUnitValues.ManagedNames);
        Assert.Contains("Document='drawing' PropertySet='User Defined Properties' Property='Mass'", mass);
        Assert.Equal(RealMass.Replace("Document='model'", "Document='drawing'"), mass);
        Assert.Equal(
            RealFinish.Replace("Document='model'", "Document='drawing'"),
            PrintUnitValues.PointAtDrawing(RealFinish, PrintUnitValues.ManagedNames));
    }

    [Fact]
    public void PointAtDrawing_FindsACustomFieldByFormatIdAlone()
    {
        const string odd = "<Property Document='model' PropertySet='Something Else' Property='Mass' FormatID='{d5cdd505-2e9c-101b-9397-08002b2cf9ae}'>Mass</Property>";
        Assert.Contains("Document='drawing'", PrintUnitValues.PointAtDrawing(odd, new[] { "Mass" }));
    }

    [Fact]
    public void PointAtModel_UndoesPointAtDrawing()
    {
        string onDrawing = PrintUnitValues.PointAtDrawing(RealMass + RealFinish, PrintUnitValues.ManagedNames);
        Assert.NotEqual(RealMass + RealFinish, onDrawing);
        Assert.Equal(RealMass + RealFinish, PrintUnitValues.PointAtModel(onDrawing, PrintUnitValues.ManagedNames));
    }

    [Fact]
    public void PointAtModel_LeavesAFieldAlreadyOnTheModelAlone()
    {
        Assert.Equal(RealMass, PrintUnitValues.PointAtModel(RealMass, PrintUnitValues.ManagedNames));
    }

    [Fact]
    public void CountFields_CountsCustomManagedFieldsWhicheverDocumentTheyRead()
    {
        Assert.Equal(2, PrintUnitValues.CountFields(RealMass + PrintUnitValues.PointAtDrawing(RealFinish, PrintUnitValues.ManagedNames), PrintUnitValues.ManagedNames));
        Assert.Equal(0, PrintUnitValues.CountFields("<StyleOverride>MM</StyleOverride>", PrintUnitValues.ManagedNames));
        Assert.Equal(0, PrintUnitValues.CountFields(null, PrintUnitValues.ManagedNames));
        const string builtIn = "<Property Document='model' PropertySet='Design Tracking Properties' Property='Mass' FormatID='{32853F0F}'>Mass</Property>";
        Assert.Equal(0, PrintUnitValues.CountFields(builtIn, PrintUnitValues.ManagedNames));
    }

    private static PrintUnitValues.Plan Plan(
        PrintUnits part, PrintUnits print, string? drawingMass = null, string? drawingSpec = null, bool assembly = false, double? kg = 4.5) =>
        PrintUnitValues.PlanFor(
            part, print, assembly, kg, "Test finish", "0.013-0.025 mm",
            FinishProperties.SpecText(List[0], part), List, drawingMass, drawingSpec);

    [Fact]
    public void Plan_InchPartOnMetricPrint_WritesBothAndReadsTheDrawing()
    {
        var plan = Plan(PrintUnits.Imperial, PrintUnits.Metric);
        Assert.Equal("4.50 kg", Text(plan.Writes, PartPropertyStorage.Mass));
        Assert.Contains("µm", Text(plan.Writes, PartPropertyStorage.FinishSpec));
        Assert.Equal(new[] { "Mass", "Finish Spec" }, plan.ReadsDrawing);
        Assert.Empty(plan.ReadsModel);
    }

    [Fact]
    public void Plan_MetricPartOnImperialPrint_WritesPoundsAndInches()
    {
        var plan = Plan(PrintUnits.Metric, PrintUnits.Imperial);
        Assert.Equal("9.92 lb", Text(plan.Writes, PartPropertyStorage.Mass));
        Assert.EndsWith(" IN", Text(plan.Writes, PartPropertyStorage.FinishSpec));
        Assert.Equal(2, plan.ReadsDrawing.Count);
    }

    [Fact]
    public void Plan_UpToDateOffUnitsPrint_WritesNothingButStillReadsTheDrawing()
    {
        var first = Plan(PrintUnits.Imperial, PrintUnits.Metric);
        var plan = Plan(
            PrintUnits.Imperial, PrintUnits.Metric,
            Text(first.Writes, PartPropertyStorage.Mass), "  " + Text(first.Writes, PartPropertyStorage.FinishSpec) + " ");
        Assert.Empty(plan.Writes);
        Assert.Equal(2, plan.ReadsDrawing.Count);
    }

    [Fact]
    public void Plan_ChangedMass_WritesOnlyTheMass()
    {
        var first = Plan(PrintUnits.Imperial, PrintUnits.Metric);
        var plan = Plan(PrintUnits.Imperial, PrintUnits.Metric, "1.00 kg", Text(first.Writes, PartPropertyStorage.FinishSpec));
        Assert.Equal("Mass", Assert.Single(plan.Writes).Location.PropertyName);
        Assert.Equal("4.50 kg", plan.Writes[0].TextValue);
    }

    [Theory]
    [InlineData(PrintUnits.Metric)]
    [InlineData(PrintUnits.Imperial)]
    public void Plan_SameUnits_ReadsTheModelAndWritesNothing(PrintUnits units)
    {
        var plan = Plan(units, units, "9.99 kg", "stale");
        Assert.Empty(plan.Writes);
        Assert.Empty(plan.ReadsDrawing);
        Assert.Equal(new[] { "Mass", "Finish Spec" }, plan.ReadsModel);
    }

    [Fact]
    public void Plan_AssemblyOffUnits_ReadsTheDrawingMassAndTheModelFinishSpec()
    {
        var plan = Plan(PrintUnits.Metric, PrintUnits.Imperial, assembly: true);
        Assert.Equal(new[] { "Mass" }, plan.ReadsDrawing);
        Assert.Equal(new[] { "Finish Spec" }, plan.ReadsModel);
    }

    [Fact]
    public void Plan_UnreadableMassWithAStoredDrawingMass_KeepsTheFieldOnTheDrawing()
    {
        var plan = Plan(PrintUnits.Metric, PrintUnits.Imperial, drawingMass: "9.92 lb", kg: null);
        Assert.DoesNotContain(plan.Writes, w => w.Location == PartPropertyStorage.Mass);
        Assert.Contains("Mass", plan.ReadsDrawing);
        Assert.Empty(plan.ReadsModel);
    }

    [Fact]
    public void Plan_NoFinishValueWithAStoredDrawingSpec_KeepsTheFieldOnTheDrawing()
    {
        var plan = PrintUnitValues.PlanFor(
            PrintUnits.Metric, PrintUnits.Imperial, false, 4.5, null, null, null, Array.Empty<Finish>(), "9.92 lb", "Stored spec IN");
        Assert.Empty(plan.Writes);
        Assert.DoesNotContain(plan.Writes, w => w.Location == PartPropertyStorage.FinishSpec);
        Assert.Contains("Finish Spec", plan.ReadsDrawing);
        Assert.Empty(plan.ReadsModel);
    }

    [Fact]
    public void Plan_AssemblyFinishSpecStaysOnTheModelEvenIfTheDrawingStoresOne()
    {
        var plan = Plan(PrintUnits.Metric, PrintUnits.Imperial, drawingSpec: "Stale", assembly: true);
        Assert.Equal(new[] { "Finish Spec" }, plan.ReadsModel);
    }

    [Fact]
    public void Plan_SameUnitsIgnoresStoredDrawingCopies()
    {
        var plan = Plan(PrintUnits.Metric, PrintUnits.Metric, "9.92 lb", "Stored", kg: null);
        Assert.Empty(plan.ReadsDrawing);
    }

    [Fact]
    public void Plan_UnusableMass_LeavesTheMassFieldOnTheModel()
    {
        var plan = Plan(PrintUnits.Metric, PrintUnits.Imperial, kg: null);
        Assert.Equal(new[] { "Finish Spec" }, plan.ReadsDrawing);
        Assert.Equal(new[] { "Mass" }, plan.ReadsModel);
    }

    [Fact]
    public void PointAtDrawing_LeavesOtherTextAlone()
    {
        const string formatted = "<StyleOverride>MM</StyleOverride>";
        Assert.Equal(formatted, PrintUnitValues.PointAtDrawing(formatted, new[] { "Mass" }));
    }
}
