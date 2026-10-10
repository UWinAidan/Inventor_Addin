using System.ComponentModel;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class SaveAsCopyViewModelTests
{
    private const string Original = @"C:\Work\Parts\AWB-10001,mounting_bracket.ipt";

    private static SaveAsCopyViewModel Create(
        string original = Original,
        DocumentKind kind = DocumentKind.Part,
        string? partType = "M",
        string? designer = "Test Designer") => new(original, kind, partType, designer);

    private static List<string?> Track(INotifyPropertyChanged vm)
    {
        var names = new List<string?>();
        vm.PropertyChanged += (_, e) => names.Add(e.PropertyName);
        return names;
    }

    // Pre-fills

    [Fact]
    public void Defaults_ComeFromTheOriginal()
    {
        var vm = Create(designer: "  Test Designer ");

        Assert.Equal("mounting bracket", vm.PartName);
        Assert.Equal("M", vm.PartType);
        Assert.Equal("Manufactured", vm.PartTypeFullName);
        Assert.Equal(@"C:\Work\Parts", vm.Folder);
        Assert.Equal(DocumentKind.Part, vm.Kind);
        Assert.Equal("Test Designer", vm.Designer);
        Assert.Equal("AWB-?????,mounting_bracket.ipt", vm.FileNamePreview);
        Assert.Null(vm.ValidationMessage);
        Assert.True(vm.CanSave);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Null(vm.Request);
    }

    [Fact]
    public void Header_IsSaveAsCopy_OverTheOriginalsFileName()
    {
        var vm = Create();

        Assert.Equal("Save As Copy", vm.HeaderTitle);
        Assert.Equal("AWB-10001,mounting_bracket.ipt", vm.HeaderSubline);
    }

    [Fact]
    public void OldFileWithoutANumber_PrefillsItsWholeName()
    {
        var vm = Create(original: "/home/user/cad/Old_bracket.v2.ipt");

        Assert.Equal("Old bracket.v2", vm.PartName);
        Assert.Equal("/home/user/cad", vm.Folder);
        Assert.Equal("Old_bracket.v2.ipt", vm.HeaderSubline);
        Assert.Equal("AWB-?????,Old_bracket.v2.ipt", vm.FileNamePreview);
    }

    [Theory]
    [InlineData(DocumentKind.Part, false)]
    [InlineData(DocumentKind.SheetMetalPart, false)]
    [InlineData(DocumentKind.Assembly, true)]
    [InlineData(DocumentKind.WeldmentAssembly, true)]
    public void Note_OnlyForAssemblies_SaysTheComponentsAreShared(DocumentKind kind, bool hasNote)
    {
        var vm = Create(original: @"C:\Work\AWB-10001,frame.iam", kind: kind);

        Assert.Equal(hasNote, vm.HasNote);
        Assert.Equal(hasNote ? SaveAsCopyCreator.AssemblyNote : string.Empty, vm.Note);
    }

    [Fact]
    public void AssemblyCopy_KeepsTheAssemblyExtension()
    {
        var vm = Create(original: @"C:\Work\AWB-10001,frame.iam", kind: DocumentKind.Assembly, partType: "A");

        Assert.Equal("AWB-?????,frame.iam", vm.FileNamePreview);
    }

    // Part type

    [Theory]
    [InlineData(" pm ", "PM")]
    [InlineData("a", "A")]
    public void PartType_ListedCode_IsHeldInTheListsSpelling(string fromFile, string expected)
    {
        Assert.Equal(expected, Create(partType: fromFile).PartType);
    }

    [Theory]
    [InlineData(DocumentKind.Part, null, "")]
    [InlineData(DocumentKind.Part, "  ", "")]
    [InlineData(DocumentKind.Assembly, null, "A")]
    [InlineData(DocumentKind.WeldmentAssembly, "", "W")]
    public void PartType_BlankOnTheOriginal_PrefillsTheKindsDefault(DocumentKind kind, string? fromFile, string expected)
    {
        Assert.Equal(expected, Create(kind: kind, partType: fromFile).PartType);
    }

    [Fact]
    public void PartType_UnknownCode_IsKept_AndAddedToTheList()
    {
        var vm = Create(partType: " XYZ ");

        Assert.Equal("XYZ", vm.PartType);
        Assert.Equal(PartTypes.UnknownCodeText, vm.PartTypeFullName);
        Assert.Equal(PartTypeOption.Unknown("XYZ"), vm.PartTypeOptions[^1]);
        Assert.Equal(PartTypes.All.Count + 2, vm.PartTypeOptions.Count);
    }

    [Fact]
    public void PartTypeOptions_BlankThenTheList_ForAListedCode()
    {
        var vm = Create();

        Assert.Equal(PartTypeOption.Blank, vm.PartTypeOptions[0]);
        Assert.Equal(PartTypes.All.Select(PartTypeOption.From), vm.PartTypeOptions.Skip(1));
    }

    [Fact]
    public void PartType_Set_NormalisesAndRaisesTheFullName()
    {
        var vm = Create();
        var changed = Track(vm);

        vm.PartType = " f ";

        Assert.Equal("F", vm.PartType);
        Assert.Equal("Fastener", vm.PartTypeFullName);
        Assert.Equal([nameof(vm.PartType), nameof(vm.PartTypeFullName)], changed);
    }

    // Name, preview and validation (as New Part)

    [Fact]
    public void PartName_Set_UpdatesThePreview_AndRaisesItsProperties()
    {
        var vm = Create();
        var changed = Track(vm);
        var canExecuteChanged = 0;
        vm.SaveCommand.CanExecuteChanged += (_, _) => canExecuteChanged++;

        vm.PartName = "left  bracket";

        Assert.Equal("AWB-?????,left_bracket.ipt", vm.FileNamePreview);
        Assert.Contains(nameof(vm.PartName), changed);
        Assert.Contains(nameof(vm.FileNamePreview), changed);
        Assert.Contains(nameof(vm.ValidationMessage), changed);
        Assert.Contains(nameof(vm.CanSave), changed);
        Assert.Equal(1, canExecuteChanged);
    }

    [Theory]
    [InlineData("", "Enter a part name.")]
    [InlineData("a:b", "A part name cannot contain the character ':'.")]
    [InlineData("CON", "'CON' is a name Windows keeps for itself.")]
    public void BadName_DisablesSave_AndSaysWhy(string name, string message)
    {
        var vm = Create();

        vm.PartName = name;

        Assert.Equal(message, vm.ValidationMessage);
        Assert.Equal(string.Empty, vm.FileNamePreview);
        Assert.False(vm.CanSave);
        Assert.False(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Comma_IsAccepted()
    {
        var vm = Create();

        vm.PartName = "bracket, left";

        Assert.Null(vm.ValidationMessage);
        Assert.Equal("AWB-?????,bracket,_left.ipt", vm.FileNamePreview);
    }

    [Fact]
    public void PathTooLong_DisablesSave()
    {
        var vm = Create();

        vm.PartName = new string('x', 300);

        Assert.Equal(PartFileName.ValidatePath(vm.Folder, vm.FileNamePreview), vm.ValidationMessage);
        Assert.NotNull(vm.ValidationMessage);
        Assert.False(vm.CanSave);
    }

    [Fact]
    public void PreviewAndValidation_MatchNewPart()
    {
        foreach (var name in new[] { "mounting bracket", "a?b", "", new string('y', 250) })
        {
            var copy = Create();
            copy.PartName = name;
            var newPart = new NewPartViewModel(null, InventorAddin.Core.Settings.UnitsSystem.Metric, copy.Folder) { PartName = name };

            Assert.Equal(newPart.FileNamePreview, copy.FileNamePreview);
            Assert.Equal(newPart.ValidationMessage, copy.ValidationMessage);
        }
    }

    // Save

    [Fact]
    public void Save_StoresTheRequest_AndAsksToClose()
    {
        var vm = Create();
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;
        vm.PartName = "  new_bracket  ";
        vm.PartType = "pm";

        vm.SaveCommand.Execute(null);

        Assert.Equal(new SaveAsCopyRequest(Original, DocumentKind.Part, "new bracket", "PM"), vm.Request);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Save_WhileInvalid_DoesNothing()
    {
        var vm = Create();
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;
        vm.PartName = "";

        vm.SaveCommand.Execute(null);

        Assert.Null(vm.Request);
        Assert.Equal(0, closed);
    }

    [Fact]
    public void Constructor_RejectsANullOriginal()
    {
        Assert.Throws<ArgumentNullException>(() => new SaveAsCopyViewModel(null!, DocumentKind.Part, null, null));
    }
}
