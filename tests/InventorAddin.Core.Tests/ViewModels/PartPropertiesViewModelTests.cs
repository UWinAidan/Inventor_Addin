using System.ComponentModel;
using System.Globalization;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class PartPropertiesViewModelTests
{
    private const string DefaultDesigner = "Test Designer";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private sealed class FakeTarget : IPropertyWriteTarget
    {
        public List<IReadOnlyList<PropertyWrite>> Calls { get; } = new();
        public Exception? ThrowOnApply { get; set; }

        public void Apply(IReadOnlyList<PropertyWrite> writes)
        {
            Calls.Add(writes);
            if (ThrowOnApply is not null)
                throw ThrowOnApply;
        }
    }

    private sealed class FakeAssigner : IPartNumberAssigner
    {
        public int Calls { get; private set; }
        public PartNumberAssignment Result { get; set; } = new("AWB-10001", false, []);
        public Exception? Throw { get; set; }

        public PartNumberAssignment Assign()
        {
            Calls++;
            if (Throw is not null)
                throw Throw;
            return Result;
        }
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Infos { get; } = new();
        public List<(string Message, Exception? Ex)> Errors { get; } = new();

        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) { }
        public void Error(string message, Exception? ex = null) => Errors.Add((message, ex));
    }

    /// <summary>A part whose every field already matches what the window shows: no pending changes.</summary>
    private static PartPropertiesSnapshot Complete() => new()
    {
        FullFileName = @"C:\Work\Bracket.ipt",
        DocumentKind = DocumentKind.Part,
        PartNumber = "AWB-10001",
        Description = "Bracket",
        PartType = "M",
        Designer = "Alex Example",
        Detailer = "Sam Sample",
        Cost = 12.5m,
        Material = "Test Steel",
        Finish = "Test Paint",
        WeightDisplay = "1.234 kg",
    };

    /// <summary>A part with every editable field blank.</summary>
    private static PartPropertiesSnapshot Blank(DocumentKind kind = DocumentKind.Part) => new()
    {
        FullFileName = @"C:\Work\Bracket.ipt",
        DocumentKind = kind,
        Description = "Bracket",
    };

    /// <summary>A register holding only <c>AWB-10001</c>, as the add-in's <c>RegisterStore.HasNumber</c> would answer.</summary>
    private static bool RegisteredOnly10001(string? partNumber) =>
        PartNumber.TryParse(partNumber, out var n) && n.Value == 10001;

    private static PartPropertiesViewModel Create(
        PartPropertiesSnapshot snapshot,
        FakeTarget? target = null,
        string? defaultDesigner = DefaultDesigner,
        CultureInfo? culture = null,
        ILog? log = null,
        string? windowsUserName = null,
        Func<string?, bool>? hasNumber = null,
        FakeAssigner? assigner = null) =>
        new(
            snapshot,
            target ?? new FakeTarget(),
            hasNumber ?? RegisteredOnly10001,
            assigner ?? new FakeAssigner(),
            defaultDesigner,
            windowsUserName,
            culture ?? Invariant,
            log);

    private static List<string> RecordPropertyChanges(INotifyPropertyChanged source)
    {
        var names = new List<string>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName!);
        return names;
    }

    // Construction and display

    [Fact]
    public void Constructor_RejectsNulls()
    {
        var target = new FakeTarget();
        var assigner = new FakeAssigner();
        Func<string?, bool> hasNumber = _ => false;
        Assert.Throws<ArgumentNullException>(() => new PartPropertiesViewModel(null!, target, hasNumber, assigner, null, null, Invariant));
        Assert.Throws<ArgumentNullException>(() => new PartPropertiesViewModel(Complete(), null!, hasNumber, assigner, null, null, Invariant));
        Assert.Throws<ArgumentNullException>(() => new PartPropertiesViewModel(Complete(), target, null!, assigner, null, null, Invariant));
        Assert.Throws<ArgumentNullException>(() => new PartPropertiesViewModel(Complete(), target, hasNumber, null!, null, null, Invariant));
        Assert.Throws<ArgumentNullException>(() => new PartPropertiesViewModel(Complete(), target, hasNumber, assigner, null, null, null!));
    }

    [Fact]
    public void ShowsReadOnlyValues()
    {
        var vm = Create(Complete());

        Assert.Equal("AWB-10001", vm.PartNumberDisplay);
        Assert.Equal("Test Steel", vm.MaterialDisplay);
        Assert.Equal("Test Paint", vm.FinishDisplay);
        Assert.Equal("1.234 kg", vm.Weight);
    }

    [Fact]
    public void ShowsFileValuesInEditableFields()
    {
        var vm = Create(Complete());

        Assert.Equal("M", vm.PartType);
        Assert.Equal("Manufactured", vm.PartTypeFullName);
        Assert.Equal("Alex Example", vm.Designer);
        Assert.Equal("Sam Sample", vm.Detailer);
        Assert.Equal("12.50", vm.CostText);
        Assert.True(vm.IsEditable);
    }

    [Fact]
    public void BlankValuesShowAsEmptyStrings_MissingWeightShowsDash()
    {
        var vm = Create(new PartPropertiesSnapshot { EditBlock = EditBlock.ReadOnlyFile });

        Assert.Equal("-", vm.Weight);
        Assert.Equal(string.Empty, vm.PartType);
        Assert.Equal(string.Empty, vm.Designer);
        Assert.Equal(string.Empty, vm.Detailer);
        Assert.Equal(string.Empty, vm.CostText);
    }

    [Theory]
    [InlineData("/home/work/Bracket.v2.ipt", "Bracket.v2.ipt")]
    [InlineData(@"C:\Work\Frame.iam", "Frame.iam")]
    [InlineData("   ", PartPropertiesViewModel.NotSavedFileName)]
    public void HeaderSubline_EndsWithTheNameWithExtension(string fullFileName, string expected)
    {
        var vm = Create(Blank() with { FullFileName = fullFileName });

        Assert.Equal("Part" + PartPropertiesViewModel.HeaderSeparator + expected, vm.HeaderSubline);
    }

    // Display text for the restyled window (spec 08)

    [Theory]
    [InlineData(@"C:\Work\Bracket.ipt", DocumentKind.Part, "Bracket", "Part · Bracket.ipt")]
    [InlineData(@"C:\Work\Cover.ipt", DocumentKind.SheetMetalPart, "Cover", "Sheet metal part · Cover.ipt")]
    [InlineData(@"C:\Work\Gearbox.iam", DocumentKind.Assembly, "Gearbox", "Assembly · Gearbox.iam")]
    [InlineData(@"C:\Work\Frame.v2.iam", DocumentKind.WeldmentAssembly, "Frame.v2", "Weldment · Frame.v2.iam")]
    [InlineData("", DocumentKind.Part, "New part", "Part · Not saved yet")]
    [InlineData("  ", DocumentKind.SheetMetalPart, "New sheet metal part", "Sheet metal part · Not saved yet")]
    [InlineData("", DocumentKind.Assembly, "New assembly", "Assembly · Not saved yet")]
    [InlineData("", DocumentKind.WeldmentAssembly, "New weldment", "Weldment · Not saved yet")]
    [InlineData("", DocumentKind.Unknown, "New document", "Document · Not saved yet")]
    public void Header_TitleAndSubline(string fullFileName, DocumentKind kind, string title, string subline)
    {
        var vm = Create(Blank(kind) with { FullFileName = fullFileName });

        Assert.Equal(title, vm.HeaderTitle);
        Assert.Equal(subline, vm.HeaderSubline);
    }

    [Fact]
    public void HeaderSubline_SeparatorIsSpaceMiddleDotSpace()
    {
        var vm = Create(Complete());

        Assert.Equal("Part\u0020\u00B7\u0020Bracket.ipt", vm.HeaderSubline);
        Assert.Equal(" \u00B7 ", PartPropertiesViewModel.HeaderSeparator);
    }

    // Part number display (spec 02, "Display in Part Properties")

    [Theory]
    [InlineData(@"C:\Work\AWB-10001,bracket.ipt", "AWB-10001", "AWB-10001", true, true, null)]
    [InlineData(@"C:\Work\AWB-10001,bracket.ipt", "  awb-10001 ", "AWB-10001", true, true, null)]
    [InlineData(@"C:\Work\bracket.ipt", "AWB-10500", "AWB-10500", true, false, "Not in the register")]
    [InlineData(@"C:\Work\bracket.ipt", "bracket", "Not assigned", false, false, null)]
    [InlineData(@"C:\Work\bracket.ipt", "PN-0001", "Not assigned", false, false, null)]
    [InlineData(@"C:\Work\bracket.ipt", "AWB-1001", "Not assigned", false, false, null)]
    [InlineData(@"C:\Work\bracket.ipt", "", "Not assigned", false, false, null)]
    [InlineData(@"C:\Work\bracket.ipt", null, "Not assigned", false, false, null)]
    [InlineData("", "Part1", "Not assigned", false, false, null)]
    [InlineData("", "AWB-10500", "AWB-10500", true, false, "Not in the register")]
    public void PartNumberDisplay_FollowsTheFormatAndTheRegister(
        string fullFileName, string? partNumber, string display, bool wellFormed, bool assigned, string? note)
    {
        var vm = Create(Blank() with { FullFileName = fullFileName, PartNumber = partNumber });

        Assert.Equal(display, vm.PartNumberDisplay);
        Assert.Equal(wellFormed, vm.IsPartNumberWellFormed);
        Assert.Equal(assigned, vm.IsPartNumberAssigned);
        Assert.Equal(wellFormed && !assigned, vm.IsPartNumberUnregistered);
        Assert.Equal(note, vm.PartNumberNote);
    }

    /// <summary>
    /// Inventor reports the file name as the Part Number when none is set. In the new file-name format that is
    /// <c>AWB-10001,test_plate</c>, which is not a part number, even when the number before the comma is registered.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Work\AWB-10001,test_plate.ipt", "AWB-10001,test_plate")]
    [InlineData(@"C:\Work\AWB-10001,a,b.ipt", "AWB-10001,a,b")]
    [InlineData(@"C:\Work\old_bracket.ipt", "old_bracket")]
    public void PartNumberDisplay_FileNameFallbackIsNotAssigned(string fullFileName, string partNumber)
    {
        var vm = Create(Blank() with { FullFileName = fullFileName, PartNumber = partNumber }, hasNumber: _ => false);

        Assert.Equal(PartNumberDisplay.NotAssignedText, vm.PartNumberDisplay);
        Assert.False(vm.IsPartNumberWellFormed);
        Assert.False(vm.IsPartNumberAssigned);
        Assert.Null(vm.PartNumberNote);
        Assert.True(vm.CanGenerate);
    }

    [Fact]
    public void IsPartNumberAssigned_IsWhatHasNumberSays_ForTheRawProperty()
    {
        var asked = new List<string?>();
        var vm = Create(Blank() with { PartNumber = " AWB-10002 " }, hasNumber: p => { asked.Add(p); return true; });

        Assert.Equal(new[] { " AWB-10002 " }, asked);
        Assert.True(vm.IsPartNumberAssigned);
        Assert.False(vm.IsPartNumberUnregistered);
        Assert.Null(vm.PartNumberNote);
    }

    [Fact]
    public void NotInRegisterNote_Text()
    {
        Assert.Equal("Not in the register", PartPropertiesViewModel.NotInRegisterNote);
    }

    [Theory]
    [InlineData("Test Steel", "Test Steel", true)]
    [InlineData("  Test Steel  ", "Test Steel", true)]
    [InlineData("", "Not set", false)]
    [InlineData("   ", "Not set", false)]
    [InlineData(null, "Not set", false)]
    public void MaterialAndFinishDisplay_ShowValueOrNotSet(string? value, string expected, bool isSet)
    {
        var vm = Create(Blank() with { Material = value, Finish = value });

        Assert.Equal(expected, vm.MaterialDisplay);
        Assert.Equal(isSet, vm.IsMaterialSet);
        Assert.Equal(expected, vm.FinishDisplay);
        Assert.Equal(isSet, vm.IsFinishSet);
    }

    [Fact]
    public void MaterialAndFinishDisplay_AreIndependent()
    {
        var vm = Create(Blank() with { Material = "Test Steel", Finish = null });

        Assert.Equal("Test Steel", vm.MaterialDisplay);
        Assert.True(vm.IsMaterialSet);
        Assert.Equal(PartPropertiesViewModel.NotSetText, vm.FinishDisplay);
        Assert.False(vm.IsFinishSet);
    }

    [Fact]
    public void DisplayText_ForAnEmptySnapshot()
    {
        var vm = Create(new PartPropertiesSnapshot { EditBlock = EditBlock.ReadOnlyFile });

        Assert.Equal("New document", vm.HeaderTitle);
        Assert.Equal("Document · Not saved yet", vm.HeaderSubline);
        Assert.Equal("Not assigned", vm.PartNumberDisplay);
        Assert.False(vm.IsPartNumberAssigned);
        Assert.False(vm.IsPartNumberWellFormed);
        Assert.Equal("Not set", vm.MaterialDisplay);
        Assert.Equal("Not set", vm.FinishDisplay);
    }

    [Fact]
    public void PartTypeOptions_BlankThenTheList()
    {
        var vm = Create(Complete());

        Assert.Equal(PartTypes.All.Count + 1, vm.PartTypeOptions.Count);
        Assert.Equal(PartTypeOption.Blank, vm.PartTypeOptions[0]);
        Assert.Equal(string.Empty, vm.PartTypeOptions[0].Code);
        Assert.Equal(string.Empty, vm.PartTypeOptions[0].DisplayText);
        Assert.Equal(PartTypes.All.Select(t => t.Code), vm.PartTypeOptions.Skip(1).Select(o => o.Code));
        Assert.Equal("PM: Purchased, modified", vm.PartTypeOptions.Single(o => o.Code == "PM").DisplayText);
    }

    [Fact]
    public void UnknownPartTypeCode_IsKeptAsIs_AndAddedAsAnOption()
    {
        var target = new FakeTarget();
        var vm = Create(Complete() with { PartType = "XQ" }, target);

        Assert.Equal("XQ", vm.PartType);
        Assert.Equal(PartTypes.UnknownCodeText, vm.PartTypeFullName);
        Assert.Equal(PartTypes.All.Count + 2, vm.PartTypeOptions.Count);
        var extra = vm.PartTypeOptions[^1];
        Assert.Equal("XQ", extra.Code);
        Assert.Equal("XQ: Unknown code", extra.DisplayText);
        Assert.False(vm.HasChanges);

        vm.OkCommand.Execute(null);
        Assert.Empty(target.Calls);
    }

    [Fact]
    public void UnknownPartTypeCode_CanBeChangedAndChosenAgain()
    {
        var vm = Create(Complete() with { PartType = "XQ" });

        vm.PartType = "P";
        Assert.True(vm.HasChanges);
        Assert.Equal("Purchased", vm.PartTypeFullName);

        vm.PartType = "XQ";
        Assert.False(vm.HasChanges);
        Assert.Equal("XQ", vm.PartTypeOptions[^1].Code);
    }

    [Fact]
    public void PartTypeInADifferentCase_ShowsTheListedCode_AndIsWrittenInTheListedSpelling()
    {
        var target = new FakeTarget();
        var vm = Create(Complete() with { PartType = " m " }, target);

        Assert.Equal("M", vm.PartType);
        Assert.Equal("Manufactured", vm.PartTypeFullName);
        Assert.Equal(PartTypes.All.Count + 1, vm.PartTypeOptions.Count);
        Assert.True(vm.HasChanges);
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);

        vm.ApplyCommand.Execute(null);

        var write = Assert.Single(Assert.Single(target.Calls));
        Assert.Equal(PartPropertyStorage.PartType, write.Location);
        Assert.Equal(PropertyWriteAction.Set, write.Action);
        Assert.Equal("M", write.TextValue);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void PartTypeWithOnlySurroundingWhitespace_IsNotAChange()
    {
        var vm = Create(Complete() with { PartType = " PM " });

        Assert.Equal("PM", vm.PartType);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void PartTypeSetter_UsesTheListedSpelling()
    {
        var vm = Create(Complete());

        vm.PartType = "pm";
        Assert.Equal("PM", vm.PartType);

        vm.PartType = null!;
        Assert.Equal(string.Empty, vm.PartType);
        Assert.Equal(string.Empty, vm.PartTypeFullName);
    }

    [Fact]
    public void PartType_RaisesPartTypeAndFullName()
    {
        var vm = Create(Complete());
        var changes = RecordPropertyChanges(vm);

        vm.PartType = "F";

        Assert.Contains(nameof(PartPropertiesViewModel.PartType), changes);
        Assert.Contains(nameof(PartPropertiesViewModel.PartTypeFullName), changes);
        Assert.Contains(nameof(PartPropertiesViewModel.PartTypeToolTip), changes);
        Assert.Contains(nameof(PartPropertiesViewModel.HasChanges), changes);
        Assert.Equal("Fastener", vm.PartTypeFullName);
    }

    [Theory]
    [InlineData("M", "Manufactured")]
    [InlineData("zz", PartTypes.UnknownCodeText)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void PartTypeToolTip_IsTheFullNameOrNullForABlankCode(string code, string? expected)
    {
        var vm = Create(Complete());

        vm.PartType = code;

        Assert.Equal(expected, vm.PartTypeToolTip);
    }

    [Theory]
    [InlineData(nameof(PartPropertiesViewModel.Designer))]
    [InlineData(nameof(PartPropertiesViewModel.Detailer))]
    [InlineData(nameof(PartPropertiesViewModel.CostText))]
    public void EditableFields_RaisePropertyChangedAndState(string property)
    {
        var vm = Create(Complete());
        var changes = RecordPropertyChanges(vm);
        var applyChanged = 0;
        var okChanged = 0;
        vm.ApplyCommand.CanExecuteChanged += (_, _) => applyChanged++;
        vm.OkCommand.CanExecuteChanged += (_, _) => okChanged++;

        typeof(PartPropertiesViewModel).GetProperty(property)!.SetValue(vm, "7");

        Assert.Contains(property, changes);
        Assert.Contains(nameof(PartPropertiesViewModel.HasChanges), changes);
        Assert.Contains(nameof(PartPropertiesViewModel.StatusText), changes);
        Assert.Contains(nameof(PartPropertiesViewModel.IsValid), changes);
        Assert.True(applyChanged > 0);
        Assert.True(okChanged > 0);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void SettingTheSameValue_RaisesNothing()
    {
        var vm = Create(Complete());
        var changes = RecordPropertyChanges(vm);

        vm.Designer = "Alex Example";
        vm.CostText = "12.50";
        vm.PartType = "M";

        Assert.Empty(changes);
    }

    // Pre-fills

    [Fact]
    public void BlankDesigner_IsPreFilledWithTheTrimmedDefault()
    {
        var vm = Create(Complete() with { Designer = "  " }, defaultDesigner: "  Test Designer ");

        Assert.Equal("Test Designer", vm.Designer);
        Assert.True(vm.HasChanges);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankDesigner_WithNoDefault_StaysBlank(string? defaultDesigner)
    {
        var vm = Create(Complete() with { Designer = null }, defaultDesigner: defaultDesigner);

        Assert.Equal(string.Empty, vm.Designer);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void FileDesigner_IsNotReplacedByTheDefault()
    {
        var vm = Create(Complete());

        Assert.Equal("Alex Example", vm.Designer);
    }

    [Theory]
    [InlineData(DocumentKind.Assembly, "A", "Assembly")]
    [InlineData(DocumentKind.WeldmentAssembly, "W", "Weldment")]
    public void BlankPartType_IsPreFilledForAssembliesAndWeldments(DocumentKind kind, string code, string fullName)
    {
        var vm = Create(Complete() with { DocumentKind = kind, PartType = null });

        Assert.Equal(code, vm.PartType);
        Assert.Equal(fullName, vm.PartTypeFullName);
        Assert.True(vm.HasChanges);
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);
    }

    [Theory]
    [InlineData(DocumentKind.Part)]
    [InlineData(DocumentKind.SheetMetalPart)]
    public void BlankPartType_StaysBlankForParts(DocumentKind kind)
    {
        var vm = Create(Complete() with { DocumentKind = kind, PartType = "" });

        Assert.Equal(string.Empty, vm.PartType);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void PreFilledPartType_CanBeChanged()
    {
        var vm = Create(Complete() with { DocumentKind = DocumentKind.Assembly, PartType = null });

        vm.PartType = "P";

        Assert.Equal("P", vm.PartType);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void BlankDetailer_FollowsDesignerThroughSeveralEdits_ThenStops()
    {
        var vm = Create(Blank());

        Assert.True(vm.DetailerFollowsDesigner);
        Assert.Equal(DefaultDesigner, vm.Detailer);

        vm.Designer = "First Person";
        Assert.Equal("First Person", vm.Detailer);

        vm.Designer = "Second Person";
        Assert.Equal("Second Person", vm.Detailer);

        vm.Designer = string.Empty;
        Assert.Equal(string.Empty, vm.Detailer);

        vm.Designer = "Third Person";
        Assert.Equal("Third Person", vm.Detailer);
        Assert.True(vm.DetailerFollowsDesigner);

        vm.Detailer = "Own Detailer";
        Assert.False(vm.DetailerFollowsDesigner);

        vm.Designer = "Fourth Person";
        Assert.Equal("Own Detailer", vm.Detailer);

        // Typing the Designer's value back in does not start following again.
        vm.Detailer = "Fourth Person";
        vm.Designer = "Fifth Person";
        Assert.Equal("Fourth Person", vm.Detailer);
        Assert.False(vm.DetailerFollowsDesigner);
    }

    [Fact]
    public void FollowingDetailer_SettingItToTheDesignerKeepsFollowing()
    {
        var vm = Create(Blank());

        vm.Detailer = DefaultDesigner;
        vm.Designer = "Other Person";

        Assert.True(vm.DetailerFollowsDesigner);
        Assert.Equal("Other Person", vm.Detailer);
    }

    [Fact]
    public void FollowingDetailer_RaisesDetailerWhenDesignerChanges()
    {
        var vm = Create(Blank());
        var changes = RecordPropertyChanges(vm);

        vm.Designer = "Other Person";

        Assert.Contains(nameof(PartPropertiesViewModel.Detailer), changes);
    }

    [Fact]
    public void FileWithADetailer_NeverFollows()
    {
        var vm = Create(Complete());

        Assert.False(vm.DetailerFollowsDesigner);
        vm.Designer = "Other Person";
        Assert.Equal("Sam Sample", vm.Detailer);
    }

    [Fact]
    public void BlankDetailer_WithBlankDesignerAndNoDefault_FollowsWithoutAChange()
    {
        var vm = Create(Blank() with { Designer = null }, defaultDesigner: null);

        Assert.True(vm.DetailerFollowsDesigner);
        Assert.Equal(string.Empty, vm.Detailer);
        Assert.False(vm.HasChanges);
        Assert.Equal(string.Empty, vm.StatusText);

        vm.Designer = "New Person";
        Assert.Equal("New Person", vm.Detailer);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void FollowedDetailerAlone_IsAPendingChange()
    {
        var vm = Create(Complete() with { Detailer = null });

        Assert.Equal("Alex Example", vm.Detailer);
        Assert.True(vm.HasChanges);
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);
        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void DescriptionSyncAlone_IsAPendingChange()
    {
        var target = new FakeTarget();
        var vm = Create(Complete() with { Description = "Old name" }, target);

        Assert.True(vm.HasChanges);
        Assert.Equal("Pre-filled values will be written when you apply.", vm.StatusText);
        Assert.True(vm.ApplyCommand.CanExecute(null));

        vm.ApplyCommand.Execute(null);

        var write = Assert.Single(Assert.Single(target.Calls));
        Assert.Equal(PartPropertyStorage.Description, write.Location);
        Assert.Equal("Bracket", write.TextValue);
    }

    [Fact]
    public void BlankFile_PreFillsAreWrittenOnApply()
    {
        var target = new FakeTarget();
        var vm = Create(Blank(DocumentKind.Assembly) with { Description = null }, target);

        Assert.True(vm.HasChanges);
        Assert.False(vm.HasError);
        vm.ApplyCommand.Execute(null);

        var writes = Assert.Single(target.Calls);
        Assert.Collection(
            writes,
            w => Assert.Equal((PartPropertyStorage.Description, "Bracket"), (w.Location, w.TextValue)),
            w => Assert.Equal((PartPropertyStorage.PartType, "A"), (w.Location, w.TextValue)),
            w => Assert.Equal((PartPropertyStorage.Designer, DefaultDesigner), (w.Location, w.TextValue)),
            w => Assert.Equal((PartPropertyStorage.Detailer, DefaultDesigner), (w.Location, w.TextValue)));
    }

    // Windows user name counts as blank (spec 02)

    private const string WindowsUser = "testuser";
    private const string Person = "A. Person";

    [Theory]
    [InlineData("testuser")]
    [InlineData("  TestUser ")]
    public void WindowsUserAsDesignerAndDetailer_PreFillsFromTheDefault(string stored)
    {
        var target = new FakeTarget();
        var vm = Create(
            Complete() with { Designer = stored, Detailer = stored },
            target,
            defaultDesigner: Person,
            windowsUserName: WindowsUser);

        Assert.Equal(Person, vm.Designer);
        Assert.Equal(Person, vm.Detailer);
        Assert.True(vm.DetailerFollowsDesigner);
        Assert.True(vm.HasChanges);
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);

        vm.ApplyCommand.Execute(null);

        Assert.Collection(
            Assert.Single(target.Calls),
            w => Assert.Equal((PartPropertyStorage.Designer, Person), (w.Location, w.TextValue)),
            w => Assert.Equal((PartPropertyStorage.Detailer, Person), (w.Location, w.TextValue)));
    }

    [Fact]
    public void WindowsUserAsDetailerOnly_FollowsTheFileDesigner()
    {
        var vm = Create(
            Complete() with { Detailer = WindowsUser },
            defaultDesigner: Person,
            windowsUserName: WindowsUser);

        Assert.Equal("Alex Example", vm.Designer);
        Assert.Equal("Alex Example", vm.Detailer);
        Assert.True(vm.DetailerFollowsDesigner);
        Assert.True(vm.HasChanges);
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);

        vm.Designer = "Jo Other";
        Assert.Equal("Jo Other", vm.Detailer);
    }

    [Fact]
    public void WindowsUserAsDesignerOnly_KeepsTheFileDetailer()
    {
        var vm = Create(
            Complete() with { Designer = WindowsUser },
            defaultDesigner: Person,
            windowsUserName: WindowsUser);

        Assert.Equal(Person, vm.Designer);
        Assert.Equal("Sam Sample", vm.Detailer);
        Assert.False(vm.DetailerFollowsDesigner);
        Assert.True(vm.HasChanges);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WindowsUser_WithNoDefaultDesigner_IsShownUnchanged(string? defaultDesigner)
    {
        var vm = Create(
            Complete() with { Designer = WindowsUser, Detailer = WindowsUser },
            defaultDesigner: defaultDesigner,
            windowsUserName: WindowsUser);

        Assert.Equal(WindowsUser, vm.Designer);
        Assert.Equal(WindowsUser, vm.Detailer);
        Assert.False(vm.DetailerFollowsDesigner);
        Assert.False(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.Equal(string.Empty, vm.StatusText);
    }

    [Theory]
    [InlineData(EditBlock.ReadOnlyFile)]
    [InlineData(EditBlock.NotModifiable)]
    public void WindowsUser_InAFileThatCannotBeEdited_IsShownUnchanged(EditBlock block)
    {
        var vm = Create(
            Complete() with { Designer = WindowsUser, Detailer = WindowsUser, EditBlock = block },
            defaultDesigner: Person,
            windowsUserName: WindowsUser);

        Assert.Equal(WindowsUser, vm.Designer);
        Assert.Equal(WindowsUser, vm.Detailer);
        Assert.False(vm.DetailerFollowsDesigner);
        Assert.False(vm.HasChanges);
    }

    [Theory]
    [InlineData(Person)]
    [InlineData(null)]
    public void DesignerOtherThanTheWindowsUser_IsKept(string? defaultDesigner)
    {
        var vm = Create(
            Complete() with { Designer = "testuser2", Detailer = "Sam Sample" },
            defaultDesigner: defaultDesigner,
            windowsUserName: WindowsUser);

        Assert.Equal("testuser2", vm.Designer);
        Assert.Equal("Sam Sample", vm.Detailer);
        Assert.False(vm.HasChanges);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoWindowsUserName_BehavesAsBefore(string? windowsUserName)
    {
        var vm = Create(
            Complete() with { Designer = WindowsUser, Detailer = WindowsUser },
            defaultDesigner: Person,
            windowsUserName: windowsUserName);

        Assert.Equal(WindowsUser, vm.Designer);
        Assert.Equal(WindowsUser, vm.Detailer);
        Assert.False(vm.HasChanges);
    }

    // State and commands

    [Fact]
    public void FileThatAlreadyMatches_HasNoPendingChanges_AndOkWritesNothing()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        Assert.False(vm.HasChanges);
        Assert.True(vm.IsValid);
        Assert.Null(vm.ValidationMessage);
        Assert.Equal(string.Empty, vm.StatusText);
        Assert.False(vm.HasError);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.True(vm.OkCommand.CanExecute(null));

        vm.OkCommand.Execute(null);

        Assert.Empty(target.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void EditingBackToTheOriginal_ClearsHasChanges()
    {
        var vm = Create(Complete());

        vm.Designer = "Other Person";
        Assert.True(vm.HasChanges);
        Assert.True(vm.ApplyCommand.CanExecute(null));

        vm.Designer = "Alex Example";
        Assert.False(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));

        vm.CostText = "12.5000";
        Assert.False(vm.HasChanges);

        vm.Detailer = "  Sam Sample  ";
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void StoredZeroCost_ShowsBlank_AndBlankIsNotAChange()
    {
        var vm = Create(Complete() with { Cost = 0m });

        Assert.Equal(string.Empty, vm.CostText);
        Assert.False(vm.HasChanges);

        vm.CostText = "0";
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void Cost_UsesTheGivenCulture()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target, culture: CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal("12,50", vm.CostText);

        vm.CostText = "1.234,5";
        vm.ApplyCommand.Execute(null);

        var write = Assert.Single(Assert.Single(target.Calls));
        Assert.Equal(1234.5m, write.CurrencyValue);
        Assert.Equal("1.234,50", vm.CostText);
    }

    [Theory]
    [InlineData("abc", PropertyValues.CostNotNumberMessage)]
    [InlineData("-1", PropertyValues.CostNegativeMessage)]
    [InlineData("1.23456", PropertyValues.CostTooManyDecimalsMessage)]
    public void InvalidCost_DisablesApplyAndOk(string cost, string message)
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.CostText = cost;

        Assert.False(vm.IsValid);
        Assert.Equal(message, vm.ValidationMessage);
        Assert.Equal(message, vm.StatusText);
        Assert.True(vm.HasError);
        Assert.True(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.OkCommand.CanExecute(null));

        vm.ApplyCommand.Execute(null);
        vm.OkCommand.Execute(null);
        Assert.Empty(target.Calls);
        Assert.Equal(0, closed);

        vm.CostText = "3";
        Assert.True(vm.IsValid);
        Assert.False(vm.HasError);
        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void InvalidText_UsesTheFieldName_DesignerFirst()
    {
        var vm = Create(Complete());

        vm.Detailer = "Line one\nline two";
        Assert.StartsWith("Detailer", vm.ValidationMessage);

        vm.Designer = new string('x', PropertyValues.MaxTextLength + 1);
        Assert.StartsWith("Designer", vm.ValidationMessage);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.OkCommand.CanExecute(null));

        vm.CostText = "abc";
        Assert.StartsWith("Designer", vm.ValidationMessage);
    }

    [Fact]
    public void ValidationMessage_TakesPriorityOverThePreFillNotice()
    {
        var vm = Create(Blank());
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);

        vm.CostText = "abc";
        Assert.Equal(PropertyValues.CostNotNumberMessage, vm.StatusText);

        vm.CostText = string.Empty;
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);
    }

    [Fact]
    public void PreFillNotice_GoesWhenThePreFillsAreUndone()
    {
        var vm = Create(Complete() with { Designer = null });
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);

        vm.Designer = string.Empty;

        Assert.False(vm.HasChanges);
        Assert.Equal(string.Empty, vm.StatusText);
    }

    [Fact]
    public void Apply_CallsTheTargetOnceWithTheWholePlan_ThenRebases()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);
        var changes = RecordPropertyChanges(vm);

        vm.PartType = "";
        vm.Designer = "Other Person";
        vm.Detailer = "";
        vm.CostText = " 7.5 ";
        vm.ApplyCommand.Execute(null);

        var writes = Assert.Single(target.Calls);
        Assert.Collection(
            writes,
            w => Assert.Equal((PropertyWriteAction.Remove, PartPropertyStorage.PartType), (w.Action, w.Location)),
            w => Assert.Equal((PartPropertyStorage.Designer, "Other Person"), (w.Location, w.TextValue)),
            w => Assert.Equal((PropertyWriteAction.Remove, PartPropertyStorage.Detailer), (w.Action, w.Location)),
            w => Assert.Equal((PartPropertyStorage.Cost, (decimal?)7.5m), (w.Location, w.CurrencyValue)));

        Assert.False(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.Equal(PartPropertiesViewModel.SavedNotice, vm.StatusText);
        Assert.Equal("Saved to the file. Save the document to keep the changes.", vm.StatusText);
        Assert.False(vm.HasError);
        Assert.Equal("7.50", vm.CostText);
        Assert.Contains(nameof(PartPropertiesViewModel.CostText), changes);

        // A second apply with nothing new does nothing.
        vm.ApplyCommand.Execute(null);
        Assert.Single(target.Calls);

        // Editing after the apply compares against what was written.
        vm.Designer = "Alex Example";
        Assert.True(vm.HasChanges);
        vm.ApplyCommand.Execute(null);
        Assert.Equal(2, target.Calls.Count);
        var write = Assert.Single(target.Calls[1]);
        Assert.Equal("Alex Example", write.TextValue);
    }

    [Fact]
    public void Apply_ClearingCost_WritesZero_AndShowsBlank()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);

        vm.CostText = "";
        vm.ApplyCommand.Execute(null);

        var write = Assert.Single(Assert.Single(target.Calls));
        Assert.Equal(0m, write.CurrencyValue);
        Assert.Equal(string.Empty, vm.CostText);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void Apply_PreFillNoticeIsReplacedAndDoesNotReturn()
    {
        var vm = Create(Blank());

        vm.ApplyCommand.Execute(null);
        Assert.Equal(PartPropertiesViewModel.SavedNotice, vm.StatusText);

        vm.Designer = "Other Person";
        Assert.Equal(PartPropertiesViewModel.SavedNotice, vm.StatusText);
    }

    [Fact]
    public void Apply_FollowingDetailerKeepsFollowingAfterwards()
    {
        var target = new FakeTarget();
        var vm = Create(Blank(), target);

        vm.ApplyCommand.Execute(null);
        vm.Designer = "Other Person";

        Assert.Equal("Other Person", vm.Detailer);
        vm.ApplyCommand.Execute(null);
        Assert.Equal(
            new[] { PartPropertyStorage.Designer, PartPropertyStorage.Detailer },
            target.Calls[1].Select(w => w.Location));
    }

    [Fact]
    public void Apply_LogsPropertyNamesAndCount_NeverValues()
    {
        var log = new RecordingLog();
        var vm = Create(Complete(), log: log);

        vm.Designer = "Secret Person";
        vm.Detailer = "";
        vm.CostText = "99.99";
        vm.ApplyCommand.Execute(null);

        var line = Assert.Single(log.Infos);
        Assert.Contains("3", line);
        Assert.Contains("Designer", line);
        Assert.Contains("Detailer", line);
        Assert.Contains("Cost", line);
        Assert.DoesNotContain("Secret Person", line);
        Assert.DoesNotContain("Sam Sample", line);
        Assert.DoesNotContain("99", line);
        Assert.Empty(log.Errors);
    }

    [Fact]
    public void ApplyFailure_KeepsChanges_LogsError_AndShowsTheMessage()
    {
        var target = new FakeTarget { ThrowOnApply = new InvalidOperationException("disk on fire") };
        var log = new RecordingLog();
        var vm = Create(Complete(), target, log: log);

        vm.Designer = "Other Person";
        vm.CostText = "7.5";
        vm.ApplyCommand.Execute(null);

        Assert.Single(target.Calls);
        Assert.True(vm.HasChanges);
        Assert.True(vm.ApplyCommand.CanExecute(null));
        Assert.Equal("Could not write the properties: disk on fire", vm.StatusText);
        Assert.True(vm.HasError);
        Assert.Equal("7.5", vm.CostText);
        var error = Assert.Single(log.Errors);
        Assert.Same(target.ThrowOnApply, error.Ex);
        Assert.Empty(log.Infos);

        // A retry that succeeds writes the same plan and replaces the error.
        target.ThrowOnApply = null;
        vm.ApplyCommand.Execute(null);
        Assert.Equal(2, target.Calls.Count);
        Assert.Equal(target.Calls[0], target.Calls[1]);
        Assert.Equal(PartPropertiesViewModel.SavedNotice, vm.StatusText);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void ValidationMessage_TakesPriorityOverAWriteError()
    {
        var target = new FakeTarget { ThrowOnApply = new InvalidOperationException("nope") };
        var vm = Create(Complete(), target);

        vm.Designer = "Other Person";
        vm.ApplyCommand.Execute(null);
        vm.CostText = "abc";

        Assert.Equal(PropertyValues.CostNotNumberMessage, vm.StatusText);
        Assert.True(vm.HasError);
    }

    [Fact]
    public void Ok_AppliesThenCloses()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.Designer = "Other Person";
        vm.OkCommand.Execute(null);

        Assert.Single(target.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Ok_WritesPendingPreFills()
    {
        var target = new FakeTarget();
        var vm = Create(Blank(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.OkCommand.Execute(null);

        Assert.Single(target.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Ok_WhenTheApplyFails_DoesNotClose()
    {
        var target = new FakeTarget { ThrowOnApply = new InvalidOperationException("nope") };
        var vm = Create(Complete(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.Designer = "Other Person";
        vm.OkCommand.Execute(null);

        Assert.Single(target.Calls);
        Assert.Equal(0, closed);
        Assert.Equal("Could not write the properties: nope", vm.StatusText);
    }

    // Files that cannot be edited

    [Theory]
    [InlineData(EditBlock.ReadOnlyFile, PartPropertiesSnapshot.ReadOnlyFileMessage)]
    [InlineData(EditBlock.NotModifiable, PartPropertiesSnapshot.NotModifiableMessage)]
    public void ReadOnlySnapshot_DisablesEditing_AndOkOnlyCloses(EditBlock block, string message)
    {
        var target = new FakeTarget();
        var snapshot = Blank(DocumentKind.Assembly) with
        {
            Description = "Old name",
            PartType = "m",
            EditBlock = block,
        };
        var vm = Create(snapshot, target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        Assert.False(vm.IsEditable);
        Assert.Equal(message, vm.EditBlockMessage);
        Assert.Equal(message, vm.StatusText);
        Assert.False(vm.HasError);
        Assert.False(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.True(vm.OkCommand.CanExecute(null));

        // No pre-fills: the window shows what the file holds (the listed spelling of the code aside).
        Assert.Equal("M", vm.PartType);
        Assert.Equal(string.Empty, vm.Designer);
        Assert.Equal(string.Empty, vm.Detailer);
        Assert.False(vm.DetailerFollowsDesigner);

        vm.ApplyCommand.Execute(null);
        vm.OkCommand.Execute(null);

        Assert.Empty(target.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void ReadOnlySnapshot_WithBlankType_IsNotPreFilled()
    {
        var vm = Create(Blank(DocumentKind.WeldmentAssembly) with { EditBlock = EditBlock.ReadOnlyFile });

        Assert.Equal(string.Empty, vm.PartType);
    }

    [Fact]
    public void ReadOnlySnapshot_EditBlockMessageOutranksValidation()
    {
        var vm = Create(Complete() with { EditBlock = EditBlock.ReadOnlyFile, Designer = "Line\nbreak" });

        Assert.False(vm.IsValid);
        Assert.Equal(PartPropertiesSnapshot.ReadOnlyFileMessage, vm.StatusText);
        Assert.False(vm.HasError);
        Assert.True(vm.OkCommand.CanExecute(null));
    }

    [Fact]
    public void EditableSnapshot_HasNoEditBlockMessage()
    {
        var vm = Create(Complete());

        Assert.Null(vm.EditBlockMessage);
    }

    // Generate part number (spec 02, "Commands: Generate part number")

    /// <summary>A saved part with no number: Inventor reports its file name as the Part Number.</summary>
    private static PartPropertiesSnapshot Unnumbered() => Complete() with
    {
        FullFileName = @"C:\Work\old_bracket.ipt",
        PartNumber = "old_bracket",
        Description = "old bracket",
    };

    [Fact]
    public void Generate_ShownForAFileWithoutANumber()
    {
        var vm = Create(Unnumbered());

        Assert.True(vm.CanGenerate);
        Assert.True(vm.GenerateCommand.CanExecute(null));
        Assert.Null(vm.GenerateNote);
        Assert.False(vm.HasGenerateNote);
    }

    [Fact]
    public void Generate_ShownForAnUnregisteredNumber()
    {
        var vm = Create(Complete() with { PartNumber = "AWB-10500" });

        Assert.True(vm.CanGenerate);
        Assert.True(vm.GenerateCommand.CanExecute(null));
    }

    [Fact]
    public void Generate_HiddenForARegisteredNumber()
    {
        var vm = Create(Complete());

        Assert.False(vm.CanGenerate);
        Assert.False(vm.GenerateCommand.CanExecute(null));
        Assert.Null(vm.GenerateNote);
    }

    [Theory]
    [InlineData(EditBlock.ReadOnlyFile)]
    [InlineData(EditBlock.NotModifiable)]
    public void Generate_HiddenWhenTheFileCannotBeEdited(EditBlock block)
    {
        var assigner = new FakeAssigner();
        var vm = Create(Unnumbered() with { EditBlock = block }, assigner: assigner);

        Assert.False(vm.CanGenerate);
        Assert.False(vm.GenerateCommand.CanExecute(null));
        vm.GenerateCommand.Execute(null);
        Assert.Equal(0, assigner.Calls);
    }

    [Fact]
    public void Generate_DisabledWithANoteForANeverSavedFile()
    {
        var assigner = new FakeAssigner();
        var vm = Create(Unnumbered() with { FullFileName = "" }, assigner: assigner);

        Assert.False(vm.IsSaved);
        Assert.True(vm.CanGenerate);
        Assert.False(vm.GenerateCommand.CanExecute(null));
        Assert.Equal("Save the file before giving it a part number.", vm.GenerateNote);
        Assert.True(vm.HasGenerateNote);

        vm.GenerateCommand.Execute(null);
        Assert.Equal(0, assigner.Calls);
    }

    [Fact]
    public void Generate_Success_ShowsTheNumberHidesTheButtonAndSaysSo()
    {
        var assigner = new FakeAssigner { Result = new PartNumberAssignment("AWB-10001", false, []) };
        var vm = Create(Unnumbered(), assigner: assigner);
        var changes = RecordPropertyChanges(vm);
        var canExecuteRaised = 0;
        vm.GenerateCommand.CanExecuteChanged += (_, _) => canExecuteRaised++;

        vm.GenerateCommand.Execute(null);

        Assert.Equal(1, assigner.Calls);
        Assert.Equal("AWB-10001", vm.PartNumberDisplay);
        Assert.True(vm.IsPartNumberWellFormed);
        Assert.True(vm.IsPartNumberAssigned);
        Assert.False(vm.IsPartNumberUnregistered);
        Assert.Null(vm.PartNumberNote);
        Assert.False(vm.CanGenerate);
        Assert.False(vm.GenerateCommand.CanExecute(null));
        Assert.Equal("Part number AWB-10001 assigned and written. Save the document to keep it.", vm.StatusText);
        Assert.False(vm.HasError);
        Assert.True(canExecuteRaised > 0);
        foreach (var name in new[]
        {
            nameof(vm.PartNumberDisplay), nameof(vm.IsPartNumberWellFormed), nameof(vm.IsPartNumberAssigned),
            nameof(vm.IsPartNumberUnregistered), nameof(vm.PartNumberNote), nameof(vm.CanGenerate),
            nameof(vm.StatusText), nameof(vm.HasError),
        })
        {
            Assert.Contains(name, changes);
        }
    }

    [Fact]
    public void Generate_Offline_AddsTheOfflineSentence()
    {
        var assigner = new FakeAssigner { Result = new PartNumberAssignment("AWB-10007", true, []) };
        var vm = Create(Unnumbered(), assigner: assigner);

        vm.GenerateCommand.Execute(null);

        Assert.Equal(
            "Part number AWB-10007 assigned and written. Save the document to keep it. " +
            "Assigned offline; it will be added to the shared register at the next sync.",
            vm.StatusText);
    }

    [Fact]
    public void Generate_ReplacesAnUnregisteredNumberAndItsNote()
    {
        var assigner = new FakeAssigner { Result = new PartNumberAssignment("AWB-10002", false, []) };
        var vm = Create(Complete() with { PartNumber = "AWB-10500" }, assigner: assigner);
        Assert.Equal("Not in the register", vm.PartNumberNote);

        vm.GenerateCommand.Execute(null);

        Assert.Equal("AWB-10002", vm.PartNumberDisplay);
        Assert.Null(vm.PartNumberNote);
        Assert.False(vm.CanGenerate);
    }

    [Fact]
    public void Generate_Failure_ShowsTheMessageAndChangesNothingElse()
    {
        var log = new RecordingLog();
        var assigner = new FakeAssigner { Throw = new InvalidOperationException("The register is busy.") };
        var vm = Create(Unnumbered(), assigner: assigner, log: log);

        vm.GenerateCommand.Execute(null);

        Assert.Equal("Generate part number failed: The register is busy.", vm.StatusText);
        Assert.True(vm.HasError);
        Assert.Equal(PartNumberDisplay.NotAssignedText, vm.PartNumberDisplay);
        Assert.False(vm.IsPartNumberAssigned);
        Assert.True(vm.CanGenerate);
        Assert.True(vm.GenerateCommand.CanExecute(null));
        Assert.Single(log.Errors);
        Assert.Same(assigner.Throw, log.Errors[0].Ex);
    }

    [Fact]
    public void Generate_CanBeRetriedAfterAFailure()
    {
        var assigner = new FakeAssigner { Throw = new InvalidOperationException("Busy.") };
        var vm = Create(Unnumbered(), assigner: assigner);
        vm.GenerateCommand.Execute(null);

        assigner.Throw = null;
        vm.GenerateCommand.Execute(null);

        Assert.Equal(2, assigner.Calls);
        Assert.Equal("AWB-10001", vm.PartNumberDisplay);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void Generate_ReportsDuplicatesOnce()
    {
        var messages = new[] { "AWB-10003 was given to two files: 'a' and 'b'." };
        var assigner = new FakeAssigner { Result = new PartNumberAssignment("AWB-10004", false, messages) };
        var vm = Create(Unnumbered(), assigner: assigner);
        var reported = new List<IReadOnlyList<string>>();
        vm.DuplicatesReported += (_, list) => reported.Add(list);

        vm.GenerateCommand.Execute(null);

        var only = Assert.Single(reported);
        Assert.Equal(messages, only);
    }

    [Fact]
    public void Generate_WithoutDuplicates_ReportsNothing()
    {
        var vm = Create(Unnumbered());
        var reported = 0;
        vm.DuplicatesReported += (_, _) => reported++;

        vm.GenerateCommand.Execute(null);

        Assert.Equal(0, reported);
    }

    [Fact]
    public void Generate_DoesNotWriteThroughApply_AndLeavesNoPendingChange()
    {
        var target = new FakeTarget();
        var vm = Create(Unnumbered(), target);
        Assert.False(vm.HasChanges);

        vm.GenerateCommand.Execute(null);

        Assert.False(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.Empty(target.Calls);
    }

    [Fact]
    public void Generate_KeepsPendingEdits_AndApplyThenWritesOnlyThem()
    {
        var target = new FakeTarget();
        var vm = Create(Unnumbered(), target);
        vm.Designer = "New Designer";

        vm.GenerateCommand.Execute(null);
        Assert.True(vm.HasChanges);
        vm.ApplyCommand.Execute(null);

        var writes = Assert.Single(target.Calls);
        Assert.DoesNotContain(writes, w => w.Location == PartPropertyStorage.PartNumber);
        Assert.Contains(writes, w => w.Location == PartPropertyStorage.Designer);
        Assert.Equal(PartPropertiesViewModel.SavedNotice, vm.StatusText);
        Assert.Equal("AWB-10001", vm.PartNumberDisplay);
    }

    [Fact]
    public void FormatDuplicates_HeadingThenOneLinePerDuplicate()
    {
        var text = PartPropertiesViewModel.FormatDuplicates(new[] { "first", "second" });

        Assert.Equal(
            PartPropertiesViewModel.DuplicatesHeading + Environment.NewLine + "- first" + Environment.NewLine + "- second",
            text);
    }
}
