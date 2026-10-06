using System.Text.Json.Nodes;
using InventorAddin.Core.Models;
using InventorAddin.Core.Serialization;

namespace InventorAddin.Core.Tests.Serialization;

public class ModelJsonTests
{
    private static T RoundTrip<T>(T model) where T : ModelData
    {
        var json = ModelJson.Serialize(model);
        var result = ModelJson.DeserializeModel(json);
        return Assert.IsType<T>(result);
    }

    [Fact]
    public void PartData_RoundTrips_AsPartData_WithFieldsIntact()
    {
        var part = new PartData
        {
            Kind = DocumentKind.SheetMetalPart,
            FullFileName = "C:/Models/Bracket.ipt",
            DisplayName = "Bracket.ipt",
            IsDirty = true,
            Common = new CommonProperties { PartNumber = "TEST-0001", Description = "Test bracket" },
            BomStructure = "Normal",
            Material = new AssetInfo { Name = "Steel", DisplayName = "Steel, Mild" },
            RangeBox = new BoundingBox
            {
                Min = new Point3D { X = -1, Y = 0, Z = 0.5 },
                Max = new Point3D { X = 4, Y = 2.5, Z = 1 },
            },
            SheetMetal = new SheetMetalData { ThicknessCm = 0.3, HasFlatPattern = true, BendCount = 2 },
            Holes =
            {
                new HoleData
                {
                    FeatureName = "Hole1",
                    HoleType = "Counterbore",
                    DiameterCm = 0.66,
                    CBoreDiameterCm = 1.1,
                    Tapped = true,
                    Thread = new ThreadData { Designation = "M6x1", RightHanded = true },
                    Centers = { new Point3D { X = 1, Y = 2, Z = 0 }, new Point3D { X = 3, Y = 2, Z = 0 } },
                },
            },
            FeatureCounts = { ["kExtrudeFeatureObject"] = 3 },
            SurfaceBodyCount = 1,
        };

        var back = RoundTrip(part);

        Assert.Equal(DocumentKind.SheetMetalPart, back.Kind);
        Assert.Equal("C:/Models/Bracket.ipt", back.FullFileName);
        Assert.Equal("Bracket.ipt", back.DisplayName);
        Assert.True(back.IsDirty);
        Assert.Equal("TEST-0001", back.Common.PartNumber);
        Assert.Equal("Test bracket", back.Common.Description);
        Assert.Equal("Normal", back.BomStructure);
        Assert.Equal("Steel, Mild", back.Material!.DisplayName);
        Assert.Null(back.Appearance);
        Assert.Equal(-1, back.RangeBox!.Min.X);
        Assert.Equal(2.5, back.RangeBox.Max.Y);
        Assert.Equal(0.3, back.SheetMetal!.ThicknessCm);
        Assert.Equal(2, back.SheetMetal.BendCount);
        var hole = Assert.Single(back.Holes);
        Assert.Equal("Counterbore", hole.HoleType);
        Assert.Equal(0.66, hole.DiameterCm);
        Assert.Equal(1.1, hole.CBoreDiameterCm);
        Assert.Null(hole.DepthCm);
        Assert.Equal("M6x1", hole.Thread!.Designation);
        Assert.Equal(2, hole.Centers.Count);
        Assert.Equal(3, hole.Centers[1].X);
        Assert.Equal(3, back.FeatureCounts["kExtrudeFeatureObject"]);
        Assert.Equal(1, back.SurfaceBodyCount);
    }

    [Fact]
    public void AssemblyData_RoundTrips_AsAssemblyData_WithFieldsIntact()
    {
        var assembly = new AssemblyData
        {
            Kind = DocumentKind.Assembly,
            FullFileName = "C:/Models/Frame.iam",
            BomStructure = "Normal",
            Physical = new PhysicalProperties { MassKg = 12.5, VolumeCm3 = 1600 },
            Occurrences =
            {
                new OccurrenceData
                {
                    Name = "Sub:1",
                    Visible = true,
                    Children = { new OccurrenceData { Name = "Bracket:1", Suppressed = true } },
                },
            },
            StructuredBom =
            {
                new BomRowData
                {
                    ItemNumber = "1",
                    PartNumber = "TEST-0002",
                    ItemQuantity = 4,
                    Children = { new BomRowData { ItemNumber = "1.1", ItemQuantity = 2 } },
                },
            },
        };

        var back = RoundTrip(assembly);

        Assert.Equal(DocumentKind.Assembly, back.Kind);
        Assert.Equal("C:/Models/Frame.iam", back.FullFileName);
        Assert.Equal("Normal", back.BomStructure);
        Assert.Equal(12.5, back.Physical!.MassKg);
        Assert.Equal(1600, back.Physical.VolumeCm3);
        var occ = Assert.Single(back.Occurrences);
        Assert.Equal("Sub:1", occ.Name);
        Assert.True(occ.Visible);
        var child = Assert.Single(occ.Children);
        Assert.Equal("Bracket:1", child.Name);
        Assert.True(child.Suppressed);
        var row = Assert.Single(back.StructuredBom);
        Assert.Equal("TEST-0002", row.PartNumber);
        Assert.Equal(4, row.ItemQuantity);
        Assert.Equal("1.1", Assert.Single(row.Children).ItemNumber);
        Assert.Empty(back.PartsOnlyBom);
    }

    [Fact]
    public void DrawingData_RoundTrips_AsDrawingData_IncludingNestedModels()
    {
        var drawing = new DrawingData
        {
            Kind = DocumentKind.Drawing,
            FullFileName = "C:/Models/Bracket.idw",
            Sheets =
            {
                new SheetData
                {
                    Name = "Sheet:1",
                    IsActive = true,
                    WidthCm = 43.18,
                    HeightCm = 27.94,
                    Views = { new DrawingViewData { Name = "VIEW1", Scale = 0.5, CenterX = 10, CenterY = 12 } },
                    HoleTables =
                    {
                        new TableSummary
                        {
                            Columns = { "TAG", "SIZE" },
                            Rows = { new List<string> { "A1", "6.6" } },
                        },
                    },
                },
            },
            ReferencedModels = { new PartData { Kind = DocumentKind.Part, DisplayName = "Bracket.ipt" } },
        };

        var back = RoundTrip(drawing);

        Assert.Equal(DocumentKind.Drawing, back.Kind);
        Assert.Equal("C:/Models/Bracket.idw", back.FullFileName);
        var sheet = Assert.Single(back.Sheets);
        Assert.Equal("Sheet:1", sheet.Name);
        Assert.True(sheet.IsActive);
        Assert.Equal(43.18, sheet.WidthCm);
        Assert.Equal(27.94, sheet.HeightCm);
        var view = Assert.Single(sheet.Views);
        Assert.Equal("VIEW1", view.Name);
        Assert.Equal(0.5, view.Scale);
        Assert.Equal(12, view.CenterY);
        var table = Assert.Single(sheet.HoleTables);
        Assert.Equal(new[] { "TAG", "SIZE" }, table.Columns);
        Assert.Equal(new[] { "A1", "6.6" }, Assert.Single(table.Rows));
        var referenced = Assert.IsType<PartData>(Assert.Single(back.ReferencedModels));
        Assert.Equal("Bracket.ipt", referenced.DisplayName);
    }

    [Theory]
    [InlineData(typeof(PartData), "part")]
    [InlineData(typeof(AssemblyData), "assembly")]
    [InlineData(typeof(DrawingData), "drawing")]
    public void Serialize_WritesTypeDiscriminator(Type modelType, string expected)
    {
        var model = (ModelData)Activator.CreateInstance(modelType)!;

        var json = JsonNode.Parse(ModelJson.Serialize(model))!.AsObject();

        Assert.Equal(expected, json["$type"]!.GetValue<string>());
    }

    [Fact]
    public void Serialize_WritesEnumsAsStrings()
    {
        var json = JsonNode.Parse(ModelJson.Serialize(new PartData { Kind = DocumentKind.SheetMetalPart }))!;

        Assert.Equal("SheetMetalPart", json["Kind"]!.GetValue<string>());
    }

    [Fact]
    public void Serialize_OmitsNullProperties()
    {
        var part = new PartData { ModelStateName = null, Material = null, RangeBox = null };

        var json = JsonNode.Parse(ModelJson.Serialize(part))!.AsObject();

        Assert.False(json.ContainsKey("ModelStateName"));
        Assert.False(json.ContainsKey("Material"));
        Assert.False(json.ContainsKey("RangeBox"));
        Assert.False(json["Common"]!.AsObject().ContainsKey("PartNumber"));
        // Non-null values are still written.
        Assert.True(json.ContainsKey("FullFileName"));
    }

    [Fact]
    public void BoundingBox_Lengths_AreMaxMinusMin()
    {
        var box = new BoundingBox
        {
            Min = new Point3D { X = -2, Y = 1, Z = 0.5 },
            Max = new Point3D { X = 3, Y = 1, Z = 4 },
        };

        Assert.Equal(5, box.LengthX);
        Assert.Equal(0, box.LengthY);
        Assert.Equal(3.5, box.LengthZ);
    }

    [Fact]
    public void BoundingBox_Lengths_AreZeroForDefaultBox()
    {
        var box = new BoundingBox();

        Assert.Equal(0, box.LengthX);
        Assert.Equal(0, box.LengthY);
        Assert.Equal(0, box.LengthZ);
    }

    [Fact]
    public void BoundingBox_Lengths_AreSerialized_AndSurviveRoundTrip()
    {
        var part = new PartData
        {
            RangeBox = new BoundingBox
            {
                Min = new Point3D { X = 0, Y = 0, Z = 0 },
                Max = new Point3D { X = 2, Y = 3, Z = 4 },
            },
        };

        var json = JsonNode.Parse(ModelJson.Serialize(part))!;
        Assert.Equal(2, json["RangeBox"]!["LengthX"]!.GetValue<double>());

        var back = RoundTrip(part);
        Assert.Equal(2, back.RangeBox!.LengthX);
        Assert.Equal(3, back.RangeBox.LengthY);
        Assert.Equal(4, back.RangeBox.LengthZ);
    }
}
