using InventorAddin.Core.Drawing;

namespace InventorAddin.Core.Tests.Drawing;

public sealed class InitialViewsPlanTests
{
    private const double Tol = 1e-9;

    private static readonly SheetSizeSpec A3 = PrintTemplateLayout.IsoSheets.Single(s => s.Name == "A3");
    private static readonly SheetSizeSpec A4 = PrintTemplateLayout.IsoSheets.Single(s => s.Name == "A4");
    private static readonly InitialViewsBoxCm Box = new(10, 6, 4);

    private static PlannedView Get(InitialViewsResult r, InitialViewKind k) => r.Views.Single(v => v.Kind == k);

    private static double Left(PlannedView v) => v.CentreXCm - v.WidthCm / 2;
    private static double Right(PlannedView v) => v.CentreXCm + v.WidthCm / 2;
    private static double Bottom(PlannedView v) => v.CentreYCm - v.HeightCm / 2;
    private static double Top(PlannedView v) => v.CentreYCm + v.HeightCm / 2;

    private static void AssertInside(InitialViewsResult r, SheetAreaCm a)
    {
        foreach (PlannedView v in r.Views)
        {
            Assert.True(Left(v) >= a.LeftCm - Tol, $"{v.Kind} left");
            Assert.True(Right(v) <= a.RightCm + Tol, $"{v.Kind} right");
            Assert.True(Bottom(v) >= a.BottomCm - Tol, $"{v.Kind} bottom");
            Assert.True(Top(v) <= a.TopCm + Tol, $"{v.Kind} top");
        }
    }

    [Fact]
    public void UsableArea_IsInsideTheBorderAndAboveTheTitleBlock()
    {
        SheetAreaCm a = InitialViewsPlan.UsableArea(A3);
        Assert.Equal(1.0, a.LeftCm, 9); // centred border (task 215): 10 mm on every side, no filing edge
        Assert.Equal(1.0 + 5.5, a.BottomCm, 9);
        Assert.Equal(41.0, a.RightCm, 9);
        Assert.Equal(28.7 - 0.0, a.TopCm, 9);
    }

    [Fact]
    public void Part_HasFrontTopRightAndIso_InThatOrder()
    {
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, InitialViewsPlan.UsableArea(A3));
        Assert.Equal(new[] { InitialViewKind.Front, InitialViewKind.Top, InitialViewKind.Right, InitialViewKind.Iso },
            r.Views.Select(v => v.Kind).ToArray());
        Assert.True(r.Fits);
        Assert.Empty(r.Messages);
    }

    [Fact]
    public void Part_ThirdAngleArrangement()
    {
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, InitialViewsPlan.UsableArea(A3));
        PlannedView front = Get(r, InitialViewKind.Front), top = Get(r, InitialViewKind.Top),
            right = Get(r, InitialViewKind.Right), iso = Get(r, InitialViewKind.Iso);

        Assert.True(top.CentreYCm > front.CentreYCm);
        Assert.Equal(front.CentreXCm, top.CentreXCm, 9);
        Assert.True(right.CentreXCm > front.CentreXCm);
        Assert.Equal(front.CentreYCm, right.CentreYCm, 9);
        Assert.True(iso.CentreXCm > front.CentreXCm);
        Assert.True(iso.CentreYCm > front.CentreYCm);
    }

    [Fact]
    public void Part_GapsBetweenViewsAreFifteenMillimetres()
    {
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, InitialViewsPlan.UsableArea(A3));
        PlannedView front = Get(r, InitialViewKind.Front), top = Get(r, InitialViewKind.Top),
            right = Get(r, InitialViewKind.Right), iso = Get(r, InitialViewKind.Iso);

        Assert.Equal(1.5, Bottom(top) - Top(front), 9);
        Assert.Equal(1.5, Left(right) - Right(front), 9);
        Assert.True(Left(iso) - Right(front) >= 1.5 - Tol);
        Assert.True(Bottom(iso) - Top(front) >= 1.5 - Tol);
        Assert.Equal(1.5, InitialViewsPlan.ViewGapCm);
    }

    [Fact]
    public void Part_AllViewsFitInsideTheUsableArea()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, area);
        AssertInside(r, area);
    }

    [Fact]
    public void Part_ViewSizesFollowTheBoxAtTheScale()
    {
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, InitialViewsPlan.UsableArea(A3));
        double s = r.Scale;
        PlannedView front = Get(r, InitialViewKind.Front);
        Assert.Equal(10 * s, front.WidthCm, 9);
        Assert.Equal(6 * s, front.HeightCm, 9);
        PlannedView top = Get(r, InitialViewKind.Top);
        Assert.Equal(10 * s, top.WidthCm, 9);
        Assert.Equal(4 * s, top.HeightCm, 9);
        PlannedView right = Get(r, InitialViewKind.Right);
        Assert.Equal(4 * s, right.WidthCm, 9);
        Assert.Equal(6 * s, right.HeightCm, 9);
    }

    [Fact]
    public void Scale_IsTheLargestStandardScaleThatFits()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, area);

        Assert.Contains(r.Scale, PrintPlan.StandardScales);
        var (isoW, isoH) = InitialViewsPlan.IsoSizeCm(Box);
        double Width(double s) => (Box.WidthCm + Math.Max(Box.DepthCm, isoW)) * s + InitialViewsPlan.ViewGapCm;
        double Height(double s) => (Box.HeightCm + Math.Max(Box.DepthCm, isoH)) * s + InitialViewsPlan.ViewGapCm;
        double maxW = area.WidthCm * PrintPlan.SheetFillFraction, maxH = area.HeightCm * PrintPlan.SheetFillFraction;

        Assert.True(Width(r.Scale) <= maxW && Height(r.Scale) <= maxH);
        // The next standard scale up must not fit within the fill fraction.
        double larger = PrintPlan.StandardScales.Where(x => x > r.Scale).Min();
        Assert.True(Width(larger) > maxW || Height(larger) > maxH);
    }

    [Fact]
    public void Scale_FallsAsTheModelGrows()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        double small = InitialViewsPlan.Plan(InitialViewModelKind.Part, new InitialViewsBoxCm(5, 3, 2), null, area).Scale;
        double big = InitialViewsPlan.Plan(InitialViewModelKind.Part, new InitialViewsBoxCm(80, 50, 40), null, area).Scale;
        Assert.True(small > big);
    }

    [Fact]
    public void Scale_DoesNotFit_UsesTheSmallestAndSaysSo()
    {
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, new InitialViewsBoxCm(50000, 50000, 50000), null,
            InitialViewsPlan.UsableArea(A4));
        Assert.Equal(PrintPlan.StandardScales[^1], r.Scale);
        Assert.False(r.Fits);
        Assert.Contains(InitialViewsPlan.DoesNotFitMessage, r.Messages);
    }

    [Fact]
    public void IsoSize_IsTheIsometricOutlineOfTheBox()
    {
        var (w, h) = InitialViewsPlan.IsoSizeCm(new InitialViewsBoxCm(10, 6, 4));
        Assert.Equal(Math.Sqrt(0.5) * 14, w, 9);
        Assert.Equal(Math.Sqrt(2.0 / 3.0) * 6 + Math.Sqrt(2.0 / 3.0) * 0.5 * 14, h, 9);
    }

    [Fact]
    public void SheetMetal_FoldedFrontAndFlatPatternSideBySide()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var r = InitialViewsPlan.Plan(InitialViewModelKind.SheetMetalPart, new InitialViewsBoxCm(8, 5, 2),
            new FlatPatternSizeCm(12, 6), area);

        PlannedView folded = Get(r, InitialViewKind.FoldedFront), flat = Get(r, InitialViewKind.FlatPattern);
        Assert.Equal(3, r.Views.Count);
        Assert.Equal(folded.CentreYCm, flat.CentreYCm, 9);
        Assert.Equal(1.5, Left(flat) - Right(folded), 9);
        Assert.Equal(12 * r.Scale, flat.WidthCm, 9);
        Assert.Equal(6 * r.Scale, flat.HeightCm, 9);
        AssertInside(r, area);
    }

    [Fact]
    public void SheetMetal_FlatPatternSizeUnknown_LeavesItOutAndSaysSo()
    {
        var r = InitialViewsPlan.Plan(InitialViewModelKind.SheetMetalPart, new InitialViewsBoxCm(8, 5, 2), null,
            InitialViewsPlan.UsableArea(A3));

        Assert.Equal(new[] { InitialViewKind.FoldedFront, InitialViewKind.Iso }, r.Views.Select(v => v.Kind).ToArray());
        Assert.Contains(InitialViewsPlan.FlatPatternUnknownMessage, r.Messages);
    }

    [Fact]
    public void SheetMetal_IsoSitsAboveTheFoldedFront()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var r = InitialViewsPlan.Plan(InitialViewModelKind.SheetMetalPart, new InitialViewsBoxCm(8, 5, 2),
            new FlatPatternSizeCm(12, 6), area);

        PlannedView folded = Get(r, InitialViewKind.FoldedFront), iso = Get(r, InitialViewKind.Iso);
        Assert.Equal(folded.CentreXCm, iso.CentreXCm, 9);
        Assert.True(iso.CentreYCm > folded.CentreYCm);
        AssertInside(r, area);
    }

    [Fact]
    public void SheetMetal_FlatPatternIsConsideredForTheScale()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A4);
        var box = new InitialViewsBoxCm(4, 3, 1);
        double without = InitialViewsPlan.Plan(InitialViewModelKind.SheetMetalPart, box, null, area).Scale;
        double with = InitialViewsPlan.Plan(InitialViewModelKind.SheetMetalPart, box, new FlatPatternSizeCm(60, 20), area).Scale;
        Assert.True(with < without);
    }

    [Fact]
    public void Assembly_FrontAndIsoSideBySide()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Assembly, Box, null, area);

        Assert.Equal(new[] { InitialViewKind.Front, InitialViewKind.Iso }, r.Views.Select(v => v.Kind).ToArray());
        PlannedView front = Get(r, InitialViewKind.Front), iso = Get(r, InitialViewKind.Iso);
        Assert.Equal(front.CentreYCm, iso.CentreYCm, 9);
        Assert.Equal(1.5, Left(iso) - Right(front), 9);
        AssertInside(r, area);
    }

    [Fact]
    public void Subset_DroppingTop_LeavesNoGapWhereItWas()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, area,
            new[] { InitialViewKind.Front, InitialViewKind.Right });

        Assert.Equal(2, r.Views.Count);
        PlannedView front = Get(r, InitialViewKind.Front), right = Get(r, InitialViewKind.Right);
        Assert.Equal(front.CentreYCm, right.CentreYCm, 9);
        // Centred in the area vertically: no empty row above.
        Assert.Equal(area.BottomCm + area.HeightCm / 2, front.CentreYCm, 9);
        Assert.Equal(1.5, Left(right) - Right(front), 9);
    }

    [Fact]
    public void Subset_DroppingFrontAndTop_CentresTheRest()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, area,
            new[] { InitialViewKind.Iso });

        PlannedView iso = Get(r, InitialViewKind.Iso);
        Assert.Equal(area.LeftCm + area.WidthCm / 2, iso.CentreXCm, 9);
        Assert.Equal(area.BottomCm + area.HeightCm / 2, iso.CentreYCm, 9);
    }

    [Fact]
    public void Subset_FewerViewsCanTakeALargerScale()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A4);
        var box = new InitialViewsBoxCm(12, 10, 8);
        double all = InitialViewsPlan.Plan(InitialViewModelKind.Part, box, null, area).Scale;
        double frontOnly = InitialViewsPlan.Plan(InitialViewModelKind.Part, box, null, area, new[] { InitialViewKind.Front }).Scale;
        Assert.True(frontOnly > all);
    }

    [Fact]
    public void Subset_IgnoresViewsTheModelKindDoesNotOffer()
    {
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Assembly, Box, null, InitialViewsPlan.UsableArea(A3),
            new[] { InitialViewKind.Top, InitialViewKind.Front });
        Assert.Equal(new[] { InitialViewKind.Front }, r.Views.Select(v => v.Kind).ToArray());
    }

    [Fact]
    public void Subset_Empty_PlansNothing()
    {
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, InitialViewsPlan.UsableArea(A3),
            Array.Empty<InitialViewKind>());
        Assert.Empty(r.Views);
        Assert.Contains(InitialViewsPlan.NoViewsMessage, r.Messages);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, double.NaN)]
    [InlineData(double.PositiveInfinity, 1, 1)]
    public void Plan_RejectsABoxThatIsNotPositive(double w, double h, double d)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InitialViewsPlan.Plan(InitialViewModelKind.Part,
            new InitialViewsBoxCm(w, h, d), null, InitialViewsPlan.UsableArea(A3)));
    }

    [Fact]
    public void Plan_RejectsAnEmptyArea()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null,
            new SheetAreaCm(1, 1, 1, 5)));
    }

    [Theory]
    [InlineData("A4")]
    [InlineData("A2")]
    [InlineData("A0")]
    public void Part_FitsOnEveryIsoSheet(string name)
    {
        SheetSizeSpec sheet = PrintTemplateLayout.IsoSheets.Single(s => s.Name == name);
        SheetAreaCm area = InitialViewsPlan.UsableArea(sheet);
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null, area);
        Assert.True(r.Fits);
        AssertInside(r, area);
    }

    // Task 227: the box outline is only an upper bound, so measured sizes of the placed views replace it.
    private static readonly InitialViewsBoxCm TallIsoBox = new(20, 10, 8);

    [Fact]
    public void MeasuredSizes_NullOrEmpty_KeepTheEstimate()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var estimate = InitialViewsPlan.Plan(InitialViewModelKind.Part, TallIsoBox, null, area);
        var none = InitialViewsPlan.Plan(InitialViewModelKind.Part, TallIsoBox, null, area, null, null,
            new Dictionary<InitialViewKind, ViewSizeCm>());
        Assert.Equal(estimate.Scale, none.Scale);
        Assert.Equal(estimate.Views, none.Views);
    }

    [Fact]
    public void MeasuredIso_SmallerThanTheBoxOutline_OffersALargerScale()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);

        // The box's isometric outline is 19.8 x 19.6 cm at scale 1, so with the front and the top it does not fit at 1:2 (13.3 cm of height).
        var estimate = InitialViewsPlan.Plan(InitialViewModelKind.Part, TallIsoBox, null, area);
        Assert.Equal(0.25, estimate.Scale, 9);

        // A placed iso of the real model measured 14 x 9.5 cm: at 1:2 the views fit.
        var measured = InitialViewsPlan.Plan(InitialViewModelKind.Part, TallIsoBox, null, area, null, null,
            new Dictionary<InitialViewKind, ViewSizeCm> { [InitialViewKind.Iso] = new ViewSizeCm(14, 9.5) });
        Assert.Equal(0.5, measured.Scale, 9);
        Assert.True(measured.Fits);
        Assert.Equal(7.0, Get(measured, InitialViewKind.Iso).WidthCm, 9);
        Assert.Equal(4.75, Get(measured, InitialViewKind.Iso).HeightCm, 9);
        AssertInside(measured, area);
    }

    [Fact]
    public void MeasuredSizes_AreUsedAtAFixedScaleToo()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var big = new Dictionary<InitialViewKind, ViewSizeCm> { [InitialViewKind.Iso] = new ViewSizeCm(60, 60) };
        var r = InitialViewsPlan.Plan(InitialViewModelKind.Part, TallIsoBox, null, area, null, 0.5, big);
        Assert.False(r.Fits);
        Assert.Contains(InitialViewsPlan.DoesNotFitAtScaleMessage, r.Messages);
    }

    [Fact]
    public void MeasuredAssembly_WithAHugeRangeBox_GetsTheScaleItsGeometryFits()
    {
        SheetAreaCm area = InitialViewsPlan.UsableArea(A3);
        var hugeBox = new InitialViewsBoxCm(400, 300, 300);
        var estimate = InitialViewsPlan.Plan(InitialViewModelKind.Assembly, hugeBox, null, area);
        var measured = InitialViewsPlan.Plan(InitialViewModelKind.Assembly, hugeBox, null, area, null, null,
            new Dictionary<InitialViewKind, ViewSizeCm>
            {
                [InitialViewKind.Front] = new ViewSizeCm(100, 60),
                [InitialViewKind.Iso] = new ViewSizeCm(90, 80),
            });
        Assert.True(measured.Scale > estimate.Scale);
        Assert.Equal(0.1, measured.Scale, 9); // 100 + 1.5 + 90*s fits 24 cm wide only at 1:10: 10 + 1.5 + 9 = 20.5
        Assert.True(measured.Fits);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, -1)]
    [InlineData(double.NaN, 5)]
    public void MeasuredSizes_MustBePositive(double w, double h)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InitialViewsPlan.Plan(InitialViewModelKind.Part, Box, null,
            InitialViewsPlan.UsableArea(A3), null, null,
            new Dictionary<InitialViewKind, ViewSizeCm> { [InitialViewKind.Iso] = new ViewSizeCm(w, h) }));
    }
}
