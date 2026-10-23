using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Ribbon;
using InventorAddin.Holes;

namespace InventorAddin.Commands
{
    /// <summary>
    /// Our Edit Feature for holes (spec 09, "Editing opens AWB Hole"; task 092). It has no ribbon button:
    /// <see cref="HoleEditRedirect"/> puts it in place of Inventor's Edit Feature on the browser's right-click menu and
    /// on the viewport's mini toolbar, with Inventor's icon, while AWB Hole has taken over Inventor's Hole. Pressing it
    /// opens the AWB Hole window on the selected hole.
    /// </summary>
    /// <remarks>
    /// Like AWB Hole, it opens the window whose session changes hole features, so its type is <c>kShapeEditCmdType</c>,
    /// and every press first tells the launcher (<see cref="AwbHoleWindowLauncher.CommandPressed"/>): the open session
    /// counts it as one of its own commands and restarts the picking it stopped (<see cref="HolePickingWatch"/>).
    /// </remarks>
    public sealed class HoleEditFeatureCommand : AddinCommand
    {
        public override string InternalName => CommandNames.AwbHoleEdit;
        public override string DisplayName => HoleEditRules.EditFeatureText;
        public override string Description => HoleEditRules.EditFeatureDescription;

        protected override CommandTypesEnum CommandType => CommandTypesEnum.kShapeEditCmdType;

        protected override void Execute()
        {
            AwbHoleWindowLauncher.CommandPressed();

            HoleEditRedirect? redirect = HoleEditRedirect.Active;
            if (redirect == null)
            {
                AddinServices.Log.Warn($"Command {InternalName} not run: the hole edit redirect is not running.");
                return;
            }

            redirect.EditFeaturePressed();
        }
    }
}
