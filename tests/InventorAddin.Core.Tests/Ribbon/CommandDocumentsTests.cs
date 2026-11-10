using System.Reflection;
using InventorAddin.Core.Ribbon;

namespace InventorAddin.Core.Tests.Ribbon;

public sealed class CommandDocumentsTests
{
    [Theory]
    [InlineData(CommandNames.PartProperties, CommandDocument.Edited)]
    [InlineData(CommandNames.MaterialFinish, CommandDocument.Edited)]
    [InlineData(CommandNames.CreatePrint, CommandDocument.Edited)]
    [InlineData(CommandNames.SaveAsCopy, CommandDocument.Edited)]
    [InlineData(CommandNames.ExportModelData, CommandDocument.Edited)]
    [InlineData(CommandNames.AwbHole, CommandDocument.Edited)]
    [InlineData(CommandNames.AwbHoleEdit, CommandDocument.Edited)]
    [InlineData(CommandNames.SaveAsReplace, CommandDocument.TopLevel)]
    [InlineData(CommandNames.NewPart, CommandDocument.TopLevel)]
    [InlineData(CommandNames.Settings, CommandDocument.None)]
    [InlineData(CommandNames.About, CommandDocument.None)]
    [InlineData(CommandNames.InventorHole, CommandDocument.None)]
    [InlineData(CommandNames.UseInventorHoleByDefault, CommandDocument.None)]
    [InlineData(CommandNames.MassUpdate, CommandDocument.None)]
    [InlineData(CommandNames.ExportLibraries, CommandDocument.None)]
    public void For_GivesTheCommandsRow(string commandName, CommandDocument expected)
    {
        Assert.Equal(expected, CommandDocuments.For(commandName));
    }

    [Theory]
    [InlineData("Awb_NotACommand")]
    [InlineData("")]
    public void For_UnknownName_GivesEdited(string commandName)
    {
        Assert.Equal(CommandDocument.Edited, CommandDocuments.For(commandName));
    }

    [Fact]
    public void EveryCommandName_HasAnExplicitRow()
    {
        string[] names = typeof(CommandNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        Assert.NotEmpty(names);
        string[] missing = names.Where(n => !CommandDocuments.Rows.ContainsKey(n)).ToArray();
        Assert.True(missing.Length == 0, "Commands with no row in CommandDocuments: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryRow_IsACommandName()
    {
        var names = typeof(CommandNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

        Assert.All(CommandDocuments.Rows.Keys, key => Assert.Contains(key, names));
    }
}
