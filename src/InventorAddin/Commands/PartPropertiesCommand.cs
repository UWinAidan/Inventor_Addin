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
using SysEnvironment = System.Environment;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Opens the Part Properties window (spec 07) for the document being edited. The window stays outside any
    /// transaction; each Apply (or OK with changes) writes its iProperties inside one transaction, so one Undo
    /// in Inventor reverts it. Generate part number (spec 02) assigns a number from the session's register and then
    /// writes Part Number in a transaction of its own, so one Undo removes the written number; the register row stays.
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

            // "Has a number" reads only this PC's copy of the register, so the window opens without touching the NAS.
            var register = AddinServices.RegisterStore;
            var assigner = new RegisterPartNumberAssigner(register, snapshot, target, AddinServices.Log);

            var viewModel = new PartPropertiesViewModel(
                snapshot,
                target,
                register.HasNumber,
                assigner,
                AddinServices.Settings.DefaultDesigner,
                SysEnvironment.UserName,
                CultureInfo.CurrentCulture,
                AddinServices.Log);

            WindowHost.ShowDialog(new PartPropertiesWindow(viewModel), IconNames.PartProperties);
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
