namespace InventorAddin.Core.HoleTable;

/// <summary>The hole table wizard's user-facing texts, in one place. Later tasks add to it.</summary>
public static class HoleTableMessages
{
    /// <summary>Spec 04 question 7 (Aidan): the wizard never invents an origin.</summary>
    public const string NoOrdinateOrigin =
        "This view has no ordinate dimension origin. Add one to the view before the hole table wizard can measure the holes.";

    /// <summary>Spec 04 question 17 (Default).</summary>
    public const string MoreThanOneOrdinateOrigin =
        "This view has more than one ordinate dimension origin. Remove the extra origins so the view has only one, then run the wizard again.";

    /// <summary>Spec 04 question 16: holes not seen end on are left out.</summary>
    public static string HolesLeftOut(int count) => count == 1
        ? "1 hole was left out because it is not seen end on in this view."
        : $"{count} holes were left out because they are not seen end on in this view.";
}
