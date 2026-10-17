using InventorAddin.Core.Logging;
using InventorAddin.Core.Materials;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class MaterialFinishViewModelTests
{
    // Invented test data.
    private static readonly LibraryMaterial Steel = new("Steel, Test 100", "Steel", "Test Steel Look");
    private static readonly LibraryMaterial HardSteel = new("Steel, Test 200 hardened", "Steel", "Test Steel Look");
    private static readonly LibraryMaterial Alu = new("Aluminum, Test 300", "Aluminum", "Test Alu Look");
    private static readonly LibraryMaterial Plastic = new("Plastic, Test 400", "Plastic", null);

    private static readonly Finish Chrome = MakeFinish("TC", "Test chrome", FinishKind.Plating, ["Steel"], "Test Blue Chrome");
    private static readonly Finish Oxide = MakeFinish("TO", "Test oxide", FinishKind.Conversion, ["Steel"], null);
    private static readonly Finish Anodize = MakeFinish("TA", "Test anodize", FinishKind.Anodize, ["Aluminum"], "Test Anodize Look");
    private static readonly Finish Paint = MakeFinish("TP", "Test paint", FinishKind.Paint, [], "Test Paint Look");

    private static readonly LibraryMaterial[] AllMaterials = [Steel, HardSteel, Alu, Plastic];
    private static readonly Finish[] AllFinishes = [Chrome, Oxide, Anodize, Paint];

    private static Finish MakeFinish(string code, string name, FinishKind kind, string[] appliesTo, string? appearance) =>
        new(code, name, kind, appliesTo, appearance, "0.01", "0.02", true, $"Note for {name}");

    private sealed class FakeTarget : IMaterialFinishTarget
    {
        public List<MaterialFinishChoice> Calls { get; } = new();
        public MaterialFinishResult Result { get; set; } = MaterialFinishResult.Success("Applied to the part.");
        public Exception? Throw { get; set; }

        public MaterialFinishResult Apply(MaterialFinishChoice choice)
        {
            Calls.Add(choice);
            if (Throw is not null)
                throw Throw;
            return Result;
        }
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Errors { get; } = new();

        public void Info(string message) { }

        public void Warn(string message) { }

        public void Error(string message, Exception? ex = null) => Errors.Add(message);
    }

    private sealed class Fixture
    {
        public FakeTarget Target { get; } = new();
        public List<string> Saved { get; } = new();
        public Exception? SaveThrows { get; set; }
        public RecordingLog Log { get; } = new();
    }

    private static MaterialFinishViewModel Create(
        Fixture fixture,
        MaterialFinishCurrent? current = null,
        IEnumerable<string>? preferred = null,
        IEnumerable<FinishListProblem>? problems = null,
        IEnumerable<LibraryMaterial>? materials = null,
        IEnumerable<Finish>? finishes = null) =>
        new(
            current ?? MaterialFinishCurrent.ForPart("Test part.ipt", null, null, null),
            materials ?? AllMaterials,
            finishes ?? AllFinishes,
            problems ?? [],
            preferred ?? [],
            name =>
            {
                if (fixture.SaveThrows is not null)
                    throw fixture.SaveThrows;
                fixture.Saved.Add(name);
            },
            fixture.Target,
            fixture.Log);

    private static MaterialRow Row(MaterialFinishViewModel vm, LibraryMaterial material) =>
        vm.Materials.Single(r => ReferenceEquals(r.Material, material));

    private static string[] MaterialNames(MaterialFinishViewModel vm) => vm.Materials.Select(r => r.Name).ToArray();

    private static string[] FinishNames(MaterialFinishViewModel vm) => vm.Finishes.Select(r => r.Name).ToArray();

    // Header.

    [Fact]
    public void Header_shows_the_part_and_its_current_values()
    {
        var vm = Create(new Fixture(), MaterialFinishCurrent.ForPart("Bracket.ipt", "Steel, Test 100", "Test chrome", "TC"));

        Assert.Equal("Bracket.ipt", vm.HeaderTitle);
        Assert.Equal("Steel, Test 100", vm.CurrentMaterialText);
        Assert.Equal("Test chrome (TC)", vm.CurrentFinishText);
        Assert.Equal("Steel, Test 100 · Test chrome (TC)", vm.HeaderSubline);
    }

    [Fact]
    public void Header_says_no_material_and_no_finish_when_blank()
    {
        var vm = Create(new Fixture());

        Assert.Equal(MaterialFinishViewModel.NoMaterialText, vm.CurrentMaterialText);
        Assert.Equal(MaterialFinishViewModel.NoFinishText, vm.CurrentFinishText);
    }

    [Fact]
    public void ForParts_counts_the_parts_and_shows_varies_where_they_differ()
    {
        var current = MaterialFinishCurrent.ForParts(new[]
        {
            ((string?)"Steel, Test 100", (string?)"Test chrome", (string?)"TC"),
            ("steel, test 100 ", "Test oxide", "TO"),
        });

        Assert.Equal("2 parts selected", current.Target);
        Assert.Equal("Steel, Test 100", current.Material);
        Assert.Equal(MaterialFinishCurrent.VariesText, current.FinishName);
        Assert.Equal(MaterialFinishCurrent.VariesText, current.FinishCode);

        var vm = Create(new Fixture(), current);
        Assert.Equal(MaterialFinishCurrent.VariesText, vm.CurrentFinishText);
    }

    [Fact]
    public void Combine_treats_blank_as_a_value()
    {
        Assert.Equal(MaterialFinishCurrent.VariesText, MaterialFinishCurrent.Combine(["Test chrome", null]));
        Assert.Equal("", MaterialFinishCurrent.Combine([null, " "]));
        Assert.Equal("", MaterialFinishCurrent.Combine([]));
        Assert.Equal("1 part selected", MaterialFinishCurrent.PartsSelectedText(1));
    }

    // Filters.

    [Fact]
    public void Opens_with_all_categories_paints_shown_and_preferred_only_off_without_preferred_materials()
    {
        var vm = Create(new Fixture());

        Assert.Equal(["All", "Aluminum", "Plastic", "Steel"], vm.Categories);
        Assert.Equal(MaterialFilter.All, vm.SelectedCategory);
        Assert.False(vm.PreferredOnly);
        Assert.True(vm.ShowPaints);
        Assert.Equal(4, vm.Materials.Count);
    }

    [Fact]
    public void Preferred_only_is_ticked_at_open_when_a_preferred_material_is_in_the_library()
    {
        var vm = Create(new Fixture(), preferred: [" aluminum, test 300 "]);

        Assert.True(vm.PreferredOnly);
        Assert.Equal(["Aluminum, Test 300"], MaterialNames(vm));
        Assert.True(Row(vm, Alu).IsPreferred);
    }

    [Fact]
    public void Preferred_only_is_unticked_when_no_preferred_material_is_in_the_library()
    {
        var vm = Create(new Fixture(), preferred: ["Not in this library"]);

        Assert.False(vm.PreferredOnly);
        Assert.Equal(4, vm.Materials.Count);
    }

    [Fact]
    public void Category_preferred_only_and_search_filter_the_materials()
    {
        var vm = Create(new Fixture(), preferred: ["Steel, Test 100"]);
        vm.PreferredOnly = false;

        vm.SelectedCategory = "Steel";
        Assert.Equal(["Steel, Test 100", "Steel, Test 200 hardened"], MaterialNames(vm));

        vm.MaterialSearch = "harden";
        Assert.Equal(["Steel, Test 200 hardened"], MaterialNames(vm));

        vm.MaterialSearch = "";
        vm.PreferredOnly = true;
        Assert.Equal(["Steel, Test 100"], MaterialNames(vm));

        vm.SelectedCategory = null!;
        Assert.Equal(MaterialFilter.All, vm.SelectedCategory);
    }

    [Fact]
    public void A_selected_material_hidden_by_the_category_stays_selected_and_shows_again()
    {
        var fixture = new Fixture();
        var vm = Create(fixture);
        var steel = Row(vm, Steel);
        vm.SelectedMaterial = steel;
        vm.SelectedFinish = vm.Finishes.Single(f => f.Code == "TC");

        vm.SelectedCategory = "Aluminum";

        Assert.Same(steel, vm.SelectedMaterial);
        Assert.Null(vm.MaterialListSelection);
        Assert.True(vm.SelectionIsHidden);
        Assert.Equal("TC", vm.SelectedFinish!.Code);
        Assert.Contains(vm.Finishes, f => f.Code == "TC");
        Assert.True(vm.ApplyCommand.CanExecute(null));

        // A list box writes null when its items change; the hidden selection is kept.
        vm.MaterialListSelection = null;
        Assert.Same(steel, vm.SelectedMaterial);

        vm.ApplyCommand.Execute(null);
        Assert.Equal("Steel, Test 100", Assert.Single(fixture.Target.Calls).MaterialName);

        vm.SelectedCategory = MaterialFilter.All;
        Assert.Same(steel, vm.MaterialListSelection);
        Assert.False(vm.SelectionIsHidden);
    }

    [Fact]
    public void A_selected_material_hidden_by_the_search_stays_selected()
    {
        var vm = Create(new Fixture());
        var steel = Row(vm, Steel);
        vm.SelectedMaterial = steel;

        vm.MaterialSearch = "Aluminum";

        Assert.Same(steel, vm.SelectedMaterial);
        Assert.Null(vm.MaterialListSelection);

        vm.MaterialSearch = "";
        Assert.Same(steel, vm.MaterialListSelection);
    }

    [Fact]
    public void Unstarring_the_selected_material_with_preferred_only_keeps_it_selected()
    {
        var vm = Create(new Fixture(), preferred: ["Steel, Test 100", "Aluminum, Test 300"]);
        var steel = Row(vm, Steel);
        vm.SelectedMaterial = steel;

        steel.ToggleStarCommand.Execute(null);

        Assert.DoesNotContain(steel, vm.Materials);
        Assert.Same(steel, vm.SelectedMaterial);
    }

    [Fact]
    public void Rows_stay_the_same_objects_when_filters_change()
    {
        var vm = Create(new Fixture());
        var row = Row(vm, Steel);
        vm.SelectedMaterial = row;

        vm.MaterialSearch = "Steel";

        Assert.Same(row, vm.SelectedMaterial);
        Assert.Same(row, Row(vm, Steel));
    }

    // Stars.

    [Fact]
    public void Star_toggles_the_material_and_saves()
    {
        var fixture = new Fixture();
        var vm = Create(fixture);
        var row = Row(vm, Alu);

        row.ToggleStarCommand.Execute(null);
        Assert.True(row.IsPreferred);

        row.ToggleStarCommand.Execute(null);
        Assert.False(row.IsPreferred);

        Assert.Equal(["Aluminum, Test 300", "Aluminum, Test 300"], fixture.Saved);
    }

    [Fact]
    public void Unstarring_with_preferred_only_removes_the_row()
    {
        var vm = Create(new Fixture(), preferred: ["Steel, Test 100", "Aluminum, Test 300"]);
        Assert.Equal(2, vm.Materials.Count);

        Row(vm, Steel).ToggleStarCommand.Execute(null);

        Assert.Equal(["Aluminum, Test 300"], MaterialNames(vm));
    }

    [Fact]
    public void Starring_with_preferred_only_off_keeps_the_list()
    {
        var vm = Create(new Fixture());

        Row(vm, Plastic).ToggleStarCommand.Execute(null);

        Assert.Equal(4, vm.Materials.Count);
        vm.PreferredOnly = true;
        Assert.Equal(["Plastic, Test 400"], MaterialNames(vm));
    }

    [Fact]
    public void A_failed_save_leaves_the_star_and_shows_an_error()
    {
        var fixture = new Fixture { SaveThrows = new IOException("disk full") };
        var vm = Create(fixture);
        var row = Row(vm, Alu);

        row.ToggleStarCommand.Execute(null);

        Assert.False(row.IsPreferred);
        Assert.True(vm.StatusIsError);
        Assert.Equal(MaterialFinishViewModel.PreferredSaveErrorPrefix + "disk full", vm.StatusText);
        Assert.Single(fixture.Log.Errors);
    }

    // Finishes.

    [Fact]
    public void Finishes_are_empty_until_a_material_is_selected()
    {
        var vm = Create(new Fixture());

        Assert.Empty(vm.Finishes);
        Assert.Null(vm.SelectedFinish);
    }

    [Fact]
    public void Finishes_list_none_first_then_the_finishes_that_suit_the_material()
    {
        var vm = Create(new Fixture());

        vm.SelectedMaterial = Row(vm, Steel);

        Assert.Equal(["None", "Test chrome", "Test oxide", "Test paint"], FinishNames(vm));
        Assert.Same(FinishRow.None, vm.SelectedFinish);
        Assert.True(vm.SelectedFinish!.IsNone);
    }

    [Fact]
    public void Show_paints_and_search_filter_the_finishes_but_keep_none()
    {
        var vm = Create(new Fixture());
        vm.SelectedMaterial = Row(vm, Steel);

        vm.ShowPaints = false;
        Assert.Equal(["None", "Test chrome", "Test oxide"], FinishNames(vm));

        vm.FinishSearch = "TO";
        Assert.Equal(["None", "Test oxide"], FinishNames(vm));
    }

    [Fact]
    public void A_finish_that_still_suits_stays_selected_when_the_material_changes()
    {
        var vm = Create(new Fixture());
        vm.SelectedMaterial = Row(vm, Steel);
        vm.SelectedFinish = vm.Finishes.Single(f => f.Code == "TC");

        vm.SelectedMaterial = Row(vm, HardSteel);

        Assert.Equal("TC", vm.SelectedFinish!.Code);
    }

    [Fact]
    public void A_finish_that_stops_suiting_goes_back_to_none()
    {
        var vm = Create(new Fixture());
        vm.SelectedMaterial = Row(vm, Steel);
        vm.SelectedFinish = vm.Finishes.Single(f => f.Code == "TC");

        vm.SelectedMaterial = Row(vm, Alu);

        Assert.Equal(["None", "Test anodize", "Test paint"], FinishNames(vm));
        Assert.Same(FinishRow.None, vm.SelectedFinish);
    }

    [Fact]
    public void A_selected_finish_hidden_by_show_paints_stays_selected()
    {
        var fixture = new Fixture();
        var vm = Create(fixture);
        vm.SelectedMaterial = Row(vm, Steel);
        var paint = vm.Finishes.Single(f => f.Code == "TP");
        vm.SelectedFinish = paint;

        vm.ShowPaints = false;

        Assert.Same(paint, vm.SelectedFinish);
        Assert.Null(vm.FinishListSelection);
        Assert.True(vm.SelectionIsHidden);

        vm.ApplyCommand.Execute(null);
        Assert.Same(Paint, Assert.Single(fixture.Target.Calls).Finish);

        vm.ShowPaints = true;
        Assert.Same(paint, vm.FinishListSelection);
        Assert.False(vm.SelectionIsHidden);
    }

    [Fact]
    public void A_selected_finish_hidden_by_the_search_stays_selected_and_shows_again()
    {
        var fixture = new Fixture();
        var vm = Create(fixture);
        vm.SelectedMaterial = Row(vm, Steel);
        var chrome = vm.Finishes.Single(f => f.Code == "TC");
        vm.SelectedFinish = chrome;

        vm.FinishSearch = "oxide";

        Assert.Equal(["None", "Test oxide"], FinishNames(vm));
        Assert.Same(chrome, vm.SelectedFinish);
        Assert.Null(vm.FinishListSelection);
        Assert.Equal("Steel, Test 100 · Test chrome (TC)", vm.SelectionText);

        // A list box writes null when its items change; the hidden selection is kept.
        vm.FinishListSelection = null;
        Assert.Same(chrome, vm.SelectedFinish);

        vm.ApplyCommand.Execute(null);
        Assert.Same(Chrome, Assert.Single(fixture.Target.Calls).Finish);

        vm.FinishSearch = "";
        Assert.Same(chrome, vm.FinishListSelection);
    }

    [Fact]
    public void Picking_a_finish_in_the_list_selects_it()
    {
        var vm = Create(new Fixture());
        vm.MaterialListSelection = Row(vm, Steel);

        vm.FinishListSelection = vm.Finishes.Single(f => f.Code == "TO");

        Assert.Same(Row(vm, Steel), vm.SelectedMaterial);
        Assert.Equal("TO", vm.SelectedFinish!.Code);
        Assert.Equal("Steel, Test 100 · Test oxide (TO)", vm.SelectionText);
    }

    [Fact]
    public void Selection_text_is_empty_without_a_material_and_says_none_for_no_finish()
    {
        var vm = Create(new Fixture());
        Assert.Equal("", vm.SelectionText);

        vm.SelectedMaterial = Row(vm, Plastic);
        Assert.Equal("Plastic, Test 400 · None", vm.SelectionText);
    }

    // Preselection.

    [Fact]
    public void The_current_material_and_finish_are_selected_at_open()
    {
        var vm = Create(new Fixture(), MaterialFinishCurrent.ForPart("P.ipt", "steel, test 100", "Test oxide", "to"));

        Assert.Same(Row(vm, Steel), vm.SelectedMaterial);
        Assert.Equal("TO", vm.SelectedFinish!.Code);
        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void The_current_finish_is_found_by_name_when_it_has_no_code()
    {
        var vm = Create(new Fixture(), MaterialFinishCurrent.ForPart("P.ipt", "Steel, Test 100", "Test chrome", null));

        Assert.Equal("TC", vm.SelectedFinish!.Code);
    }

    [Fact]
    public void A_current_finish_that_does_not_suit_leaves_none_selected()
    {
        var vm = Create(new Fixture(), MaterialFinishCurrent.ForPart("P.ipt", "Aluminum, Test 300", "Test chrome", "TC"));

        Assert.Same(Row(vm, Alu), vm.SelectedMaterial);
        Assert.Same(FinishRow.None, vm.SelectedFinish);
    }

    [Fact]
    public void Nothing_is_selected_when_the_current_material_is_not_in_the_list()
    {
        var vm = Create(new Fixture(), MaterialFinishCurrent.ForPart("P.ipt", "Imported material", null, null));

        Assert.Null(vm.SelectedMaterial);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.OkCommand.CanExecute(null));
    }

    [Fact]
    public void Nothing_is_selected_when_the_current_material_varies()
    {
        var vm = Create(
            new Fixture(),
            new MaterialFinishCurrent("2 parts selected", MaterialFinishCurrent.VariesText, null, null),
            materials: [Steel, new LibraryMaterial(MaterialFinishCurrent.VariesText, "Steel", null)]);

        Assert.Null(vm.SelectedMaterial);
    }

    [Fact]
    public void Preferred_only_starts_unticked_when_the_current_material_is_in_the_library_but_not_preferred()
    {
        var vm = Create(
            new Fixture(),
            MaterialFinishCurrent.ForPart("P.ipt", "Steel, Test 100", null, null),
            preferred: ["Aluminum, Test 300"]);

        Assert.False(vm.PreferredOnly);
        Assert.Equal(4, vm.Materials.Count);
        Assert.Same(Row(vm, Steel), vm.SelectedMaterial);
        Assert.Same(Row(vm, Steel), vm.MaterialListSelection);
    }

    [Fact]
    public void Preferred_only_starts_ticked_when_the_current_material_is_preferred()
    {
        var vm = Create(
            new Fixture(),
            MaterialFinishCurrent.ForPart("P.ipt", "Steel, Test 100", null, null),
            preferred: ["Steel, Test 100", "Aluminum, Test 300"]);

        Assert.True(vm.PreferredOnly);
        Assert.Same(Row(vm, Steel), vm.SelectedMaterial);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Imported material")]
    [InlineData(MaterialFinishCurrent.VariesText)]
    public void Preferred_only_starts_ticked_when_the_current_material_is_blank_varies_or_not_in_the_library(string? material)
    {
        var vm = Create(
            new Fixture(),
            new MaterialFinishCurrent("P.ipt", material, null, null),
            preferred: ["Aluminum, Test 300"]);

        Assert.True(vm.PreferredOnly);
        Assert.Null(vm.SelectedMaterial);
    }

    // Enabling.

    [Fact]
    public void Apply_and_ok_are_enabled_when_a_material_is_selected_and_cancel_always()
    {
        var vm = Create(new Fixture());
        var raised = 0;
        vm.ApplyCommand.CanExecuteChanged += (_, _) => raised++;

        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.OkCommand.CanExecute(null));
        Assert.True(vm.CancelCommand.CanExecute(null));

        vm.SelectedMaterial = Row(vm, Plastic);

        Assert.True(vm.ApplyCommand.CanExecute(null));
        Assert.True(vm.OkCommand.CanExecute(null));
        Assert.True(raised > 0);
    }

    // Apply.

    [Fact]
    public void Apply_sends_the_material_finish_and_property_writes()
    {
        var fixture = new Fixture();
        var vm = Create(fixture);
        vm.SelectedMaterial = Row(vm, Steel);
        vm.SelectedFinish = vm.Finishes.Single(f => f.Code == "TC");

        vm.ApplyCommand.Execute(null);

        var choice = Assert.Single(fixture.Target.Calls);
        Assert.Equal("Steel, Test 100", choice.MaterialName);
        Assert.Same(Chrome, choice.Finish);
        Assert.Equal(FinishProperties.For(Chrome), choice.PropertyWrites);
    }

    [Fact]
    public void Apply_with_none_sends_a_null_finish_that_removes_the_properties()
    {
        var fixture = new Fixture();
        var vm = Create(fixture);
        vm.SelectedMaterial = Row(vm, Plastic);

        vm.ApplyCommand.Execute(null);

        var choice = Assert.Single(fixture.Target.Calls);
        Assert.Null(choice.Finish);
        Assert.All(choice.PropertyWrites, w => Assert.Equal(PropertyWriteAction.Remove, w.Action));
    }

    [Fact]
    public void A_successful_apply_shows_the_message_and_refreshes_the_header()
    {
        var fixture = new Fixture();
        fixture.Target.Result = MaterialFinishResult.Success("Applied to 2 parts.");
        var vm = Create(fixture, MaterialFinishCurrent.ForPart("P.ipt", null, null, null));
        vm.SelectedMaterial = Row(vm, Steel);
        vm.SelectedFinish = vm.Finishes.Single(f => f.Code == "TC");
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.ApplyCommand.Execute(null);

        Assert.Equal("Applied to 2 parts.", vm.StatusText);
        Assert.False(vm.StatusIsError);
        Assert.Null(vm.StatusDetails);
        Assert.Equal("Steel, Test 100", vm.CurrentMaterialText);
        Assert.Equal("Test chrome (TC)", vm.CurrentFinishText);
        Assert.Contains(nameof(MaterialFinishViewModel.HeaderSubline), changed);
    }

    [Fact]
    public void A_successful_apply_with_no_message_uses_the_default_text()
    {
        var fixture = new Fixture();
        fixture.Target.Result = MaterialFinishResult.Success(" ");
        var vm = Create(fixture);
        vm.SelectedMaterial = Row(vm, Steel);

        vm.ApplyCommand.Execute(null);

        Assert.Equal(MaterialFinishViewModel.AppliedText, vm.StatusText);
    }

    [Fact]
    public void Applying_none_clears_the_header_finish()
    {
        var fixture = new Fixture();
        var vm = Create(fixture, MaterialFinishCurrent.ForPart("P.ipt", "Steel, Test 100", "Test chrome", "TC"));
        vm.SelectedFinish = FinishRow.None;

        vm.ApplyCommand.Execute(null);

        Assert.Equal(MaterialFinishViewModel.NoFinishText, vm.CurrentFinishText);
    }

    [Fact]
    public void Notes_are_counted_in_the_status_and_listed_in_the_details()
    {
        var fixture = new Fixture();
        fixture.Target.Result = MaterialFinishResult.Success(
            "Applied to 1 part.",
            ["Appearance 'Test Blue Chrome' not found; the part keeps its material's appearance", "Skipped: ASSY_X (not a part)"]);
        var vm = Create(fixture);
        vm.SelectedMaterial = Row(vm, Steel);

        vm.ApplyCommand.Execute(null);

        Assert.Equal("Applied to 1 part. 2 notes.", vm.StatusText);
        Assert.True(vm.HasStatusDetails);
        Assert.Equal(2, vm.StatusNotes.Count);
        Assert.Contains("Skipped: ASSY_X (not a part)", vm.StatusDetails);
        Assert.Contains("Test Blue Chrome", vm.StatusDetails);
    }

    [Fact]
    public void A_failed_result_shows_in_the_error_style_and_keeps_the_header()
    {
        var fixture = new Fixture();
        fixture.Target.Result = MaterialFinishResult.Failure("Could not apply to Part B.ipt.", ["Material is read-only"]);
        var vm = Create(fixture, MaterialFinishCurrent.ForPart("P.ipt", "Aluminum, Test 300", null, null));
        vm.SelectedMaterial = Row(vm, Steel);

        vm.ApplyCommand.Execute(null);

        Assert.True(vm.StatusIsError);
        Assert.Equal("Could not apply to Part B.ipt. 1 note.", vm.StatusText);
        Assert.Equal("Aluminum, Test 300", vm.CurrentMaterialText);
    }

    [Fact]
    public void A_failed_result_with_no_message_uses_the_default_text()
    {
        var fixture = new Fixture();
        fixture.Target.Result = MaterialFinishResult.Failure("");
        var vm = Create(fixture);
        vm.SelectedMaterial = Row(vm, Steel);

        vm.ApplyCommand.Execute(null);

        Assert.True(vm.StatusIsError);
        Assert.Equal(MaterialFinishViewModel.ApplyFailedText, vm.StatusText);
    }

    [Fact]
    public void An_exception_shows_in_the_error_style_and_is_logged()
    {
        var fixture = new Fixture();
        fixture.Target.Throw = new InvalidOperationException("boom");
        var vm = Create(fixture);
        vm.SelectedMaterial = Row(vm, Steel);

        vm.ApplyCommand.Execute(null);

        Assert.True(vm.StatusIsError);
        Assert.Equal(MaterialFinishViewModel.ApplyErrorPrefix + "boom", vm.StatusText);
        Assert.Single(fixture.Log.Errors);
        Assert.Equal(MaterialFinishViewModel.NoMaterialText, vm.CurrentMaterialText);
    }

    [Fact]
    public void Apply_does_not_close_the_window()
    {
        var vm = Create(new Fixture());
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;
        vm.SelectedMaterial = Row(vm, Steel);

        vm.ApplyCommand.Execute(null);

        Assert.Equal(0, closed);
    }

    // OK and Cancel.

    [Fact]
    public void Ok_applies_then_closes()
    {
        var fixture = new Fixture();
        var vm = Create(fixture);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;
        vm.SelectedMaterial = Row(vm, Steel);

        vm.OkCommand.Execute(null);

        Assert.Single(fixture.Target.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Ok_does_not_close_when_the_apply_fails()
    {
        var fixture = new Fixture();
        fixture.Target.Result = MaterialFinishResult.Failure("No.");
        var vm = Create(fixture);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;
        vm.SelectedMaterial = Row(vm, Steel);

        vm.OkCommand.Execute(null);
        Assert.Equal(0, closed);

        fixture.Target.Result = MaterialFinishResult.Success("ok");
        fixture.Target.Throw = new InvalidOperationException("boom");
        vm.OkCommand.Execute(null);
        Assert.Equal(0, closed);
    }

    [Fact]
    public void Cancel_closes_without_applying()
    {
        var fixture = new Fixture();
        var vm = Create(fixture);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;
        vm.SelectedMaterial = Row(vm, Steel);

        vm.CancelCommand.Execute(null);

        Assert.Empty(fixture.Target.Calls);
        Assert.Equal(1, closed);
    }

    // Finish list problems.

    [Fact]
    public void Finish_list_problems_are_shown_at_open_not_as_an_error()
    {
        var problems = new[]
        {
            new FinishListProblem(4, "Kind \"Plate\" is not known; row left out."),
            new FinishListProblem(4, "ThicknessMin_mm is not a number; row left out."),
            new FinishListProblem(7, "Code \"TC\" is used twice; row left out."),
        };

        var vm = Create(new Fixture(), problems: problems);

        Assert.Equal("2 finish rows could not be read; see the log.", vm.StatusText);
        Assert.False(vm.StatusIsError);
        Assert.Equal(3, vm.StatusNotes.Count);
        Assert.StartsWith("Line 4: ", vm.StatusNotes[0]);
    }

    [Fact]
    public void One_bad_row_is_singular()
    {
        var vm = Create(new Fixture(), problems: [new FinishListProblem(3, "Bad; row left out.")]);

        Assert.Equal("1 finish row could not be read; see the log.", vm.StatusText);
    }

    [Fact]
    public void A_finish_list_with_nothing_loaded_says_it_could_not_be_read()
    {
        var vm = Create(new Fixture(), problems: [new FinishListProblem(null, "The finish list is empty.")], finishes: []);

        Assert.Equal(MaterialFinishViewModel.FinishListUnreadableText, vm.StatusText);
        Assert.False(vm.StatusIsError);

        vm.SelectedMaterial = Row(vm, Steel);
        Assert.Equal(["None"], FinishNames(vm));
    }

    [Fact]
    public void No_problems_means_an_empty_status()
    {
        var vm = Create(new Fixture());

        Assert.Equal("", vm.StatusText);
        Assert.False(vm.HasStatusDetails);
    }
}
