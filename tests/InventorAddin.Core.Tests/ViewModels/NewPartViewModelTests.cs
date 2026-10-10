using System.ComponentModel;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Settings;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class NewPartViewModelTests
{
    private const string Folder = @"C:\Work\Parts";

    private static NewPartViewModel Create(
        string? designer = "Test Designer",
        UnitsSystem units = UnitsSystem.Metric,
        string? folder = Folder) => new(designer, units, folder);

    private static List<string?> Track(INotifyPropertyChanged vm)
    {
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    // Defaults

    [Fact]
    public void Defaults()
    {
        var vm = Create(designer: "  Test Designer ", units: UnitsSystem.Imperial);

        Assert.Equal(NewFileKind.Part, vm.Kind);
        Assert.Equal(string.Empty, vm.PartType);
        Assert.Equal(string.Empty, vm.PartName);
        Assert.Equal(UnitsSystem.Imperial, vm.Units);
        Assert.Equal(Folder, vm.Folder);
        Assert.Equal("Test Designer", vm.Designer);
        Assert.Null(vm.Request);
    }

    [Fact]
    public void NullFolder_IsBlank()
    {
        var vm = Create(folder: null);

        Assert.Equal(string.Empty, vm.Folder);
    }

    [Fact]
    public void Header_IsNewPartOverProductName()
    {
        var vm = Create();

        Assert.Equal("New Part", vm.HeaderTitle);
        Assert.Equal(Branding.ProductName, vm.HeaderSubline);
    }

    [Fact]
    public void PartTypeOptions_BlankFirstThenTheList()
    {
        var vm = Create();

        Assert.Equal(PartTypeOption.Blank, vm.PartTypeOptions[0]);
        Assert.Equal(PartTypes.All.Select(PartTypeOption.From), vm.PartTypeOptions.Skip(1));
    }

    [Fact]
    public void KindOptions_AreTheThreeKinds()
    {
        var vm = Create();

        Assert.Equal(
            new[]
            {
                new NewFileKindOption(NewFileKind.Part, "Part"),
                new NewFileKindOption(NewFileKind.SheetMetalPart, "Sheet metal part"),
                new NewFileKindOption(NewFileKind.Assembly, "Assembly"),
            },
            vm.KindOptions);
    }

    [Fact]
    public void UnitsOptions_AreMetricThenImperial()
    {
        var vm = Create();

        Assert.Equal(new[] { UnitsSystem.Metric, UnitsSystem.Imperial }, vm.UnitsOptions.Select(o => o.Units));
    }

    // Part type following the kind

    [Fact]
    public void PartType_FollowsKind_UntilChosen()
    {
        var vm = Create();

        vm.Kind = NewFileKind.Assembly;
        Assert.Equal("A", vm.PartType);
        Assert.Equal("Assembly", vm.PartTypeFullName);

        vm.Kind = NewFileKind.SheetMetalPart;
        Assert.Equal(string.Empty, vm.PartType);

        vm.Kind = NewFileKind.Assembly;
        Assert.Equal("A", vm.PartType);
    }

    [Fact]
    public void PartType_Chosen_NoLongerFollowsKind()
    {
        var vm = Create();

        vm.PartType = "M";
        vm.Kind = NewFileKind.Assembly;

        Assert.Equal("M", vm.PartType);
    }

    [Fact]
    public void PartType_ChosenBlankForAnAssembly_StaysBlank()
    {
        var vm = Create();
        vm.Kind = NewFileKind.Assembly;

        vm.PartType = string.Empty;
        vm.Kind = NewFileKind.Part;
        vm.Kind = NewFileKind.Assembly;

        Assert.Equal(string.Empty, vm.PartType);
    }

    [Fact]
    public void PartType_SetToTheSameValue_IsNotAChoice()
    {
        var vm = Create();
        vm.Kind = NewFileKind.Assembly;

        vm.PartType = "A";
        vm.Kind = NewFileKind.Part;

        Assert.Equal(string.Empty, vm.PartType);
    }

    [Fact]
    public void PartType_LowerCase_HeldInTheListsSpelling()
    {
        var vm = Create();

        vm.PartType = " pm ";

        Assert.Equal("PM", vm.PartType);
    }

    [Fact]
    public void Kind_Change_RaisesPartTypeChange()
    {
        var vm = Create();
        var names = Track(vm);

        vm.Kind = NewFileKind.Assembly;

        Assert.Contains(nameof(NewPartViewModel.Kind), names);
        Assert.Contains(nameof(NewPartViewModel.PartType), names);
        Assert.Contains(nameof(NewPartViewModel.PartTypeFullName), names);
        Assert.Contains(nameof(NewPartViewModel.FileNamePreview), names);
    }

    // Preview

    [Fact]
    public void Placeholder_IsAsLongAsARealNumber()
    {
        Assert.Equal(new PartNumber(PartNumber.LastValue).ToString().Length, NewPartViewModel.NumberPlaceholder.Length);
        Assert.StartsWith(PartNumber.Prefix + PartNumber.Separator, NewPartViewModel.NumberPlaceholder);
    }

    [Theory]
    [InlineData(NewFileKind.Part, "  mounting   bracket ", "AWB-?????,mounting_bracket.ipt")]
    [InlineData(NewFileKind.SheetMetalPart, "Cover_Plate", "AWB-?????,Cover_Plate.ipt")]
    [InlineData(NewFileKind.Assembly, "gearbox", "AWB-?????,gearbox.iam")]
    [InlineData(NewFileKind.Part, "plate, 10mm", "AWB-?????,plate,_10mm.ipt")]
    public void FileNamePreview_ShowsPlaceholderNameAndExtension(NewFileKind kind, string name, string expected)
    {
        var vm = Create();

        vm.Kind = kind;
        vm.PartName = name;

        Assert.Equal(expected, vm.FileNamePreview);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a?b")]
    [InlineData("name.")]
    public void FileNamePreview_InvalidName_IsEmpty(string name)
    {
        var vm = Create();

        vm.PartName = name;

        Assert.Equal(string.Empty, vm.FileNamePreview);
    }

    [Fact]
    public void FileNamePreview_ShownWhenOnlyTheFolderIsMissing()
    {
        var vm = Create(folder: "");

        vm.PartName = "plate";

        Assert.Equal("AWB-?????,plate.ipt", vm.FileNamePreview);
        Assert.Equal(NewPartViewModel.ChooseFolderMessage, vm.ValidationMessage);
    }

    // Validation

    [Fact]
    public void Validation_NameComesFirst()
    {
        var vm = Create(folder: "");

        Assert.Equal(PartFileName.ValidateName(""), vm.ValidationMessage);
        Assert.False(vm.CanCreate);
        Assert.False(vm.CreateCommand.CanExecute(null));
    }

    [Fact]
    public void Validation_InvalidCharacter_NamesIt()
    {
        var vm = Create();

        vm.PartName = "a*b";

        Assert.Equal(PartFileName.ValidateName("a*b"), vm.ValidationMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validation_BlankFolder_AsksForOne(string folder)
    {
        var vm = Create();
        vm.PartName = "plate";

        vm.Folder = folder;

        Assert.Equal("Choose a folder.", vm.ValidationMessage);
        Assert.False(vm.CanCreate);
    }

    [Fact]
    public void Validation_PathTooLong_ComesAfterTheFolder()
    {
        var vm = Create();
        vm.PartName = "plate";
        var fileName = "AWB-?????,plate.ipt";

        // Folder + separator + file name: exactly at the limit is accepted, one more is not.
        vm.Folder = new string('f', PartFileName.MaxPathLength - fileName.Length - 1);
        Assert.Null(vm.ValidationMessage);

        vm.Folder += "f";
        Assert.Equal(PartFileName.ValidatePath(vm.Folder, fileName), vm.ValidationMessage);
        Assert.NotNull(vm.ValidationMessage);
        Assert.False(vm.CanCreate);
    }

    [Fact]
    public void Validation_PathUsesTheTrimmedFolder()
    {
        var vm = Create();
        vm.PartName = "plate";
        var folder = new string('f', PartFileName.MaxPathLength - "AWB-?????,plate.ipt".Length - 1);

        vm.Folder = "  " + folder + "  ";

        Assert.Null(vm.ValidationMessage);
    }

    [Fact]
    public void Validation_AssemblyExtensionCountsInThePath()
    {
        var vm = Create();
        vm.PartName = "gearbox";
        vm.Folder = new string('f', PartFileName.MaxPathLength - "AWB-?????,gearbox.ipt".Length - 1);
        Assert.Null(vm.ValidationMessage);

        // Same length: .iam and .ipt. Still valid.
        vm.Kind = NewFileKind.Assembly;
        Assert.Null(vm.ValidationMessage);
    }

    [Fact]
    public void Validation_Valid_CanCreate()
    {
        var vm = Create();

        vm.PartName = "plate";

        Assert.Null(vm.ValidationMessage);
        Assert.True(vm.CanCreate);
        Assert.True(vm.CreateCommand.CanExecute(null));
    }

    [Fact]
    public void PartName_Change_RaisesStateAndCanExecuteChanged()
    {
        var vm = Create();
        var names = Track(vm);
        var raised = 0;
        vm.CreateCommand.CanExecuteChanged += (_, _) => raised++;

        vm.PartName = "plate";

        Assert.Contains(nameof(NewPartViewModel.PartName), names);
        Assert.Contains(nameof(NewPartViewModel.FileNamePreview), names);
        Assert.Contains(nameof(NewPartViewModel.ValidationMessage), names);
        Assert.Contains(nameof(NewPartViewModel.CanCreate), names);
        Assert.True(raised > 0);
    }

    // Create

    [Fact]
    public void Create_ProducesTheRequestAndAsksToClose()
    {
        var vm = Create(units: UnitsSystem.Metric, folder: @"  C:\Work\Parts  ");
        vm.Kind = NewFileKind.Assembly;
        vm.PartName = "  main_frame  ";
        vm.Units = UnitsSystem.Imperial;
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.CreateCommand.Execute(null);

        Assert.Equal(
            new NewPartRequest(NewFileKind.Assembly, "main frame", "A", UnitsSystem.Imperial, @"C:\Work\Parts"),
            vm.Request);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Create_BlankPartType_IsEmptyInTheRequest()
    {
        var vm = Create();
        vm.PartName = "plate";

        vm.CreateCommand.Execute(null);

        Assert.Equal(string.Empty, vm.Request!.PartType);
    }

    [Fact]
    public void Create_WhenInvalid_DoesNothing()
    {
        var vm = Create();
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.CreateCommand.Execute(null);

        Assert.Null(vm.Request);
        Assert.Equal(0, closed);
    }
}
