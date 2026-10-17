using System;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;

namespace InventorAddin.Holes
{
    /// <summary>
    /// The dowel and precision parts of an AWB hole (spec 09, "Dowel and precision holes"; spike 081 sections 4 and 5
    /// and round 5): the tolerance on the hole diameter, the fit colour on every face, and the stored data, applied
    /// together on OK or removed together when a dowel becomes another kind. Nothing here opens a transaction; a
    /// failure throws, so the caller can abort.
    /// </summary>
    public static class HoleFitWriter
    {
        private const string LogPrefix = "AWB Hole: ";

        /// <summary>
        /// Applies what the add-in leaves on a hole when it is kept (task 089, OK): for a dowel the tolerance and the
        /// fit colour, for any other kind no dowel tolerance, colour or data; then the stored data for
        /// <paramref name="build"/>'s resolved spec and the state applied.
        /// </summary>
        /// <param name="build">A successful <see cref="HoleRules.Build"/> result for the hole.</param>
        public static void Finish(PartDocument doc, HoleFeature hole, HoleBuildResult build, ILog log)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (build?.Geometry is not { } geometry)
                throw new ArgumentException("The hole build did not succeed.", nameof(build));
            log ??= NullLog.Instance;

            if (geometry.FitColour is { } colour)
            {
                ApplyTolerance(hole, geometry.Tolerance);
                ApplyColour(doc, hole, build.Resolved.PrecisionFit, colour);
            }
            else
            {
                RemoveDowel(hole, log);
            }

            HoleStoredDataStore.Write(hole, new HoleStoredData(build.Resolved, HoleAppliedState.From(geometry)));
        }

        /// <summary>
        /// The tolerance on the hole diameter: an ISO fit Inventor works out itself, or upper and lower deviations in
        /// centimetres; none puts the default back. The model stays at the nominal size (spike 081 section 4).
        /// </summary>
        public static void ApplyTolerance(HoleFeature hole, HoleTolerance? tolerance)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));

            Parameter diameter = hole.HoleDiameter;
            if (tolerance == null)
            {
                if (ComSafe.Get(() => (ToleranceTypeEnum?)diameter.Tolerance.ToleranceType) != ToleranceTypeEnum.kDefaultTolerance)
                    diameter.Tolerance.SetToDefault();
                return;
            }

            if (tolerance.IsoCode is { } code)
                diameter.Tolerance.SetToFits(ToleranceTypeEnum.kLimitsFitsStackedTolerance, code, "");
            else
                diameter.Tolerance.SetToDeviation(tolerance.UpperCm, tolerance.LowerCm);

            diameter.ModelValueType = ModelValueTypeEnum.kNominalValue;
        }

        /// <summary>Colours every face of the hole, the bottom included (spike 081 round 2), with the fit's appearance.</summary>
        public static void ApplyColour(PartDocument doc, HoleFeature hole, PrecisionFit fit, HoleColour colour)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));

            Asset appearance = FitAppearance(doc, fit, colour);
            foreach (Face face in hole.Faces)
                face.Appearance = appearance;
        }

        /// <summary>
        /// Takes the fit colour off the hole's faces: each face that carries one of the fit appearances goes back to
        /// the part's appearance (spike 081 round 5). Other appearances a user set are left.
        /// </summary>
        public static void RemoveColour(HoleFeature hole)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));

            foreach (Face face in hole.Faces)
            {
                string? name = ComSafe.Get(() => face.Appearance.DisplayName);
                if (HoleFitAppearances.IsFitAppearance(name))
                    face.AppearanceSourceType = AppearanceSourceTypeEnum.kPartAppearance;
            }
        }

        /// <summary>
        /// Removes everything dowel from a hole (spec 09; spike 081 round 5): the tolerance, the fit colour on every
        /// face and the <c>AWB_Hole</c> attribute set.
        /// </summary>
        public static void RemoveDowel(HoleFeature hole, ILog log)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            log ??= NullLog.Instance;

            // A tapped hole's diameter may have no tolerance to reset.
            if (!ComSafe.Get(() => hole.Tapped))
                ApplyTolerance(hole, null);
            RemoveColour(hole);
            HoleStoredDataStore.Delete(hole);
            log.Info(LogPrefix + $"dowel tolerance, colour and data removed from {ComSafe.Get(() => hole.Name)}.");
        }

        /// <summary>
        /// The fit's appearance in the part, made the first time (a Generic appearance) and found by its display
        /// name after that. Its colour is set from the workbook each time, so a colour changed in the workbook
        /// reaches the part.
        /// </summary>
        public static Asset FitAppearance(PartDocument doc, PrecisionFit fit, HoleColour colour)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (colour == null)
                throw new ArgumentNullException(nameof(colour));

            string displayName = HoleFitAppearances.DisplayName(fit);
            Asset? asset = null;
            foreach (Asset candidate in doc.Assets)
            {
                if (ComSafe.Get(() => (AssetTypeEnum?)candidate.AssetType) == AssetTypeEnum.kAssetTypeAppearance
                    && HoleFitAppearances.FitFor(ComSafe.Get(() => candidate.DisplayName)) == fit)
                {
                    asset = candidate;
                    break;
                }
            }

            asset ??= doc.Assets.Add(AssetTypeEnum.kAssetTypeAppearance, "Generic", HoleFitAppearances.Name(fit), displayName);

            var diffuse = (ColorAssetValue)asset["generic_diffuse"];
            Color? current = ComSafe.Get(() => diffuse.Value);
            bool same = current != null
                && ComSafe.Get(() => current.Red == colour.R && current.Green == colour.G && current.Blue == colour.B);
            if (!same)
                diffuse.Value = InventorHost.App.TransientObjects.CreateColor(colour.R, colour.G, colour.B);

            return asset;
        }
    }
}
