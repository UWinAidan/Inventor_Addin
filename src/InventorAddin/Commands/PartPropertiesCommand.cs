using System;
using System.Collections.Generic;
using System.Globalization;
using Inventor;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.Ribbon;
using InventorAddin.Core.ViewModels;
using InventorAddin.Extraction;
using InventorAddin.UI;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Opens the Part Properties window (spec 07) for the document being edited. The window stays outside any
    /// transaction; each Apply (or OK with changes) writes its iProperties inside one transaction, so one Undo
    /// in Inventor reverts it.
    /// </summary>
    public sealed class PartPropertiesCommand : DocumentEditCommand
    {
        public override string InternalName => CommandNames.PartProperties;
        public override string DisplayName => "Part Properties";
        public override string Description => "View and edit the properties of this part or assembly.";

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kFilePropertyEditCmdType;

        protected override bool CanRunOn(DocumentKind kind) => kind.IsPart() || kind.IsAssembly();

        protected override string WrongDocumentMessage(DocumentKind kind) =>
            "Part Properties works on parts and assemblies.";

        protected override void Run(Document doc)
        {
            PartPropertiesSnapshot snapshot = PartPropertiesReader.Read(doc);
            var target = new TransactionWriteTarget(this, doc);
            var viewModel = new PartPropertiesViewModel(
                snapshot,
                target,
                AddinServices.Settings.DefaultDesigner,
                CultureInfo.CurrentCulture,
                AddinServices.Log);

            WindowHost.ShowDialog(new PartPropertiesWindow(viewModel));
        }

        /// <summary>Not used: <see cref="Run"/> writes through <see cref="TransactionWriteTarget"/> instead.</summary>
        protected override void Execute(Document doc) =>
            throw new NotSupportedException($"{nameof(PartPropertiesCommand)} writes through its window, not through Execute.");

        /// <summary>Applies one list of writes per call, each inside its own transaction named after the command.</summary>
        private sealed class TransactionWriteTarget : IPropertyWriteTarget
        {
            private readonly PartPropertiesCommand _command;
            private readonly Document _doc;

            public TransactionWriteTarget(PartPropertiesCommand command, Document doc)
            {
                _command = command;
                _doc = doc;
            }

            public void Apply(IReadOnlyList<PropertyWrite> writes) =>
                _command.RunInTransaction(_doc, () => PropertyWriter.Apply(_doc, writes));
        }
    }
}
