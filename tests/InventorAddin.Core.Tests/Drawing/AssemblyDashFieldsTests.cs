using InventorAddin.Core.Drawing;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class AssemblyDashFieldsTests
{
    // The MATERIAL field's text as Inventor stores it in our template (read with a COM probe, task 245).
    private const string MaterialAtModel =
        "<StyleOverride FontSize='0.2036'><Property Document='model' PropertySet='Design Tracking Properties' Property='Material' FormatID='{32853F0F-3444-11D1-9E93-0060B03C1CA6}' PropertyID='20'>MATERIAL</Property></StyleOverride>";

    // The same field after the swap, as Inventor rewrites it (it adds PropertyID='3').
    private const string MaterialAtDrawing =
        "<StyleOverride FontSize='0.2036'><Property Document='drawing' PropertySet='User Defined Properties' Property='Material' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}' PropertyID='3'>MATERIAL</Property></StyleOverride>";

    private static AssemblyDashFields.Plan Plan(
        string? material = null, string? size = null, string? spec = null,
        string? dMaterial = null, string? dSize = null, string? dSpec = null) =>
        AssemblyDashFields.PlanFor(material, size, spec, dMaterial, dSize, dSpec);

    [Fact]
    public void AllBlank_WritesThreeDashesOnTheDrawing_AndReadsTheDrawing()
    {
        var plan = Plan();
        Assert.Equal(
            [
                PropertyWrite.SetText(AssemblyDashFields.DrawingMaterial, "—"),
                PropertyWrite.SetText(PartPropertyStorage.MaterialSize, "—"),
                PropertyWrite.SetText(PartPropertyStorage.FinishSpec, "—"),
            ],
            plan.Writes);
        Assert.True(plan.MaterialReadsDrawing);
        Assert.Equal(["Material Size", "Finish Spec"], plan.ReadsDrawing);
        Assert.Empty(plan.ReadsModel);
    }

    [Fact]
    public void WhitespaceCountsAsBlank()
    {
        var plan = Plan("  ", "", "\t");
        Assert.True(plan.MaterialReadsDrawing);
        Assert.Equal(2, plan.ReadsDrawing.Count);
    }

    [Fact]
    public void DashAlreadyOnTheDrawing_WritesNothing()
    {
        var plan = Plan(dMaterial: "—", dSize: "—", dSpec: " — ");
        Assert.Empty(plan.Writes);
        Assert.True(plan.MaterialReadsDrawing);
        Assert.Equal(2, plan.ReadsDrawing.Count);
    }

    [Fact]
    public void ValueSet_ReadsTheModel_AndWritesNothingForIt()
    {
        var plan = Plan(material: "Mixed", size: "n/a", spec: "Coat", dMaterial: "—", dSize: "—", dSpec: "—");
        Assert.Empty(plan.Writes);
        Assert.False(plan.MaterialReadsDrawing);
        Assert.Empty(plan.ReadsDrawing);
        Assert.Equal(["Material Size", "Finish Spec"], plan.ReadsModel);
    }

    [Fact]
    public void FillingOneField_SendsOnlyThatFieldBackToTheModel()
    {
        var plan = Plan(size: "1 x 2 x 3 mm", dMaterial: "—", dSize: "—", dSpec: "—");
        Assert.Empty(plan.Writes);
        Assert.True(plan.MaterialReadsDrawing);
        Assert.Equal(["Finish Spec"], plan.ReadsDrawing);
        Assert.Equal(["Material Size"], plan.ReadsModel);
    }

    [Fact]
    public void Combine_Part_LeavesTheUnitsPlanAlone()
    {
        var units = new PrintUnitValues.Plan([], ["Mass"], ["Finish Spec"]);
        Assert.Same(units, AssemblyDashFields.Combine(units, null));
    }

    [Fact]
    public void Combine_Assembly_DashDecidesSizeAndSpec_UnitsKeepMass()
    {
        var massWrite = PropertyWrite.SetText(PartPropertyStorage.Mass, "0.07 kg");
        var units = new PrintUnitValues.Plan([massWrite], ["Mass"], ["Finish Spec"]);
        var combined = AssemblyDashFields.Combine(units, Plan());

        Assert.Equal(massWrite, combined.Writes[0]);
        Assert.Equal(4, combined.Writes.Count);
        Assert.Equal(["Mass", "Material Size", "Finish Spec"], combined.ReadsDrawing);
        Assert.Empty(combined.ReadsModel);
    }

    [Fact]
    public void Combine_Assembly_ValueSet_FinishSpecReadsTheModel()
    {
        var units = new PrintUnitValues.Plan([], ["Mass"], ["Finish Spec"]);
        var combined = AssemblyDashFields.Combine(units, Plan(spec: "Coat", dSize: "—"));

        Assert.Equal(["Mass", "Material Size"], combined.ReadsDrawing);
        Assert.Equal(["Finish Spec"], combined.ReadsModel);
    }

    [Fact]
    public void PointMaterial_ToDrawing_SwapsTheDesignTrackingTag_KeepingTheTextAndStyle()
    {
        var swapped = AssemblyDashFields.PointMaterial(MaterialAtModel, onDrawing: true);
        Assert.Equal(
            "<StyleOverride FontSize='0.2036'><Property Document='drawing' PropertySet='User Defined Properties' Property='Material' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}'>MATERIAL</Property></StyleOverride>",
            swapped);
    }

    [Fact]
    public void PointMaterial_BackToModel_RestoresTheDesignTrackingTag_EvenWithInventorsAddedPropertyId()
    {
        Assert.Equal(MaterialAtModel, AssemblyDashFields.PointMaterial(MaterialAtDrawing, onDrawing: false));
    }

    [Fact]
    public void PointMaterial_RoundTrip_GivesTheOriginal()
    {
        var there = AssemblyDashFields.PointMaterial(MaterialAtModel, true);
        Assert.Equal(MaterialAtModel, AssemblyDashFields.PointMaterial(there, false));
    }

    [Fact]
    public void PointMaterial_AlreadyRight_ReturnsTheSameText()
    {
        Assert.Equal(MaterialAtModel, AssemblyDashFields.PointMaterial(MaterialAtModel, false));
        Assert.Equal(MaterialAtDrawing, AssemblyDashFields.PointMaterial(MaterialAtDrawing, true));
    }

    [Fact]
    public void PointMaterial_LeavesOtherFieldsAlone()
    {
        const string size =
            "<Property Document='model' PropertySet='User Defined Properties' Property='Material Size' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}'>MATERIAL SIZE</Property>";
        const string desc =
            "<Property Document='model' PropertySet='Design Tracking Properties' Property='Description' FormatID='{32853F0F-3444-11D1-9E93-0060B03C1CA6}' PropertyID='29'>X</Property>";
        Assert.Equal(size + desc, AssemblyDashFields.PointMaterial(size + desc, true));
        Assert.Equal(size + desc, AssemblyDashFields.PointMaterial(size + desc, false));
    }

    [Fact]
    public void HasMaterialField_FindsEitherTag()
    {
        Assert.True(AssemblyDashFields.HasMaterialField(MaterialAtModel));
        Assert.True(AssemblyDashFields.HasMaterialField(MaterialAtDrawing));
        Assert.False(AssemblyDashFields.HasMaterialField("<Property Document='model' PropertySet='User Defined Properties' Property='Mass' FormatID='{D5CDD505-2E9C-101B-9397-08002B2CF9AE}'>x</Property>"));
        Assert.False(AssemblyDashFields.HasMaterialField(null));
    }
}
