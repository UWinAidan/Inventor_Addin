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
using Forms = System.Windows.Forms;
using SysEnvironment = System.Environment;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Opens the Part Properties window (spec 07) for the document being edited. The window stays outside any
    /// transaction; each Apply (or OK with changes) writes its iProperties inside one transaction, so one Undo
    /// in Inventor reverts it. Generate part number (spec 02) assigns a number from the session's register and then
    /// writes Part Number in a transaction of its own, so one Undo removes the written number; the register row stays.
    /// For a part, the material library is read when the window opens, for the not-from-the-library warning, and Change
    /// beside Material opens the Material and Finish window over it (task 076).
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

            // The library is read for a part only: the not-from-the-library warning and Change are for parts (task 076).
            // A failure leaves the warning off and has been logged once by the launcher or the reader.
            MaterialLibrary? library = snapshot.DocumentKind.IsPart() ? ReadLibrary() : null;
            var materialFinish = new MaterialFinishChanger(this, doc, library);

            // "Has a number" reads only this PC's copy of the register, so the window opens without touching the NAS.
            // The assigner reads the file's path when Generate is pressed, so a Rename made first registers the new path.
            var register = AddinServices.RegisterStore;
            var assigner = new CurrentPathAssigner(() => new RegisterPartNumberAssigner(
                register,
                snapshot with { FullFileName = ComSafe.Get(() => doc.FullFileName) ?? snapshot.FullFileName },
                target,
                AddinServices.Log));

            var settings = AddinServices.Settings;
            var viewModel = new PartPropertiesViewModel(
                snapshot,
                target,
                register.HasNumber,
                assigner,
                settings.DefaultDesigner,
                PersonNames.NamesThatMeanMe(SysEnvironment.UserName, settings.OtherNamesForMe),
                CultureInfo.CurrentCulture,
                AddinServices.Log,
                new RenameService(doc, target),
                library?.Materials,
                materialFinish);

            var window = new PartPropertiesWindow(viewModel);
            materialFinish.Owner = window;
            WindowHost.ShowDialog(window, IconNames.PartProperties);
        }

        /// <summary>
        /// Opens the material library for the warning, with the wait cursor (about a second from the NAS, spike 067). Null
        /// when it could not be read; the window still opens.
        /// </summary>
        private static MaterialLibrary? ReadLibrary()
        {
            Forms.Cursor? previousCursor = Forms.Cursor.Current;
            Forms.Cursor.Current = Forms.Cursors.WaitCursor;
            try
            {
                // The failure is logged once, by the launcher or the reader; nothing more is logged here.
                MaterialLibrary? library = MaterialFinishWindowLauncher.OpenLibrary(out string? error);
                return error == null ? library : null;
            }
            finally
            {
                Forms.Cursor.Current = previousCursor;
            }
        }

        /// <summary>Not used: <see cref="Run"/> writes through <see cref="TransactionWriteTarget"/> instead.</summary>
        protected override void Execute(Document doc) =>
            throw new NotSupportedException($"{nameof(PartPropertiesCommand)} writes through its window, not through Execute.");

        /// <summary>
        /// Change beside Material (task 076): shows the Material and Finish window for this part over the Part Properties
        /// window, then reads the part's material, finish and weight again. Each Apply in that window runs in its own
        /// transaction on the document open in Inventor's window, as the Material and Finish command does (spike 067
        /// section 4), so one Undo reverts it; it writes none of the properties Part Properties edits, so edits pending
        /// there are kept.
        /// </summary>
        private sealed class MaterialFinishChanger : IMaterialFinishChanger
        {
            private readonly PartPropertiesCommand _command;
            private readonly Document _doc;
            private readonly MaterialLibrary? _library;

            /// <param name="library">The library read when Part Properties opened, or null to open it on Change.</param>
            public MaterialFinishChanger(PartPropertiesCommand command, Document doc, MaterialLibrary? library)
            {
                _command = command;
                _doc = doc;
                _library = library;
            }

            /// <summary>The Part Properties window, which the Material and Finish window opens over.</summary>
            public System.Windows.Window? Owner { get; set; }

            public PartMaterialFinish? Change()
            {
                Document transactionDoc = ComSafe.Get(() => InventorHost.ActiveDocument) ?? _doc;
                if (!MaterialFinishWindowLauncher.Show(
                        _doc, edit => _command.RunInTransaction(transactionDoc, edit), _library, Owner))
                    return null;

                PartPropertiesSnapshot reread = PartPropertiesReader.Read(_doc);
                return new PartMaterialFinish(reread.Material, reread.Finish, reread.WeightDisplay);
            }
        }

        /// <summary>Builds the Generate assigner when Generate is pressed, from the file as it is then.</summary>
        private sealed class CurrentPathAssigner : IPartNumberAssigner
        {
            private readonly Func<IPartNumberAssigner> _create;

            public CurrentPathAssigner(Func<IPartNumberAssigner> create) => _create = create;

            public PartNumberAssignment Assign() => _create().Assign();
        }

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
