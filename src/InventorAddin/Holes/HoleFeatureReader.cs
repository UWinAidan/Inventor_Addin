using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Holes;

namespace InventorAddin.Holes
{
    /// <summary>
    /// Reads a hole feature into Core's models: the facts of its geometry (<see cref="InventorHoleFacts"/>), the
    /// state that shows the AWB fit (<see cref="HoleAppliedState"/>), and the <see cref="HoleSpec"/> to open the
    /// AWB Hole window with (spec 09, "Editing reads the hole first"). Every COM read goes through
    /// <see cref="ComSafe"/>, so a value that does not apply reads as null. Inventor's lengths are centimetres and
    /// its angles radians; angles are turned into degrees here.
    /// </summary>
    public static class HoleFeatureReader
    {
        /// <summary>
        /// The spec for edit mode: from the hole's stored data when it can be read, else the closest spec
        /// <paramref name="workbook"/> builds from its geometry, marked as an Inventor hole.
        /// </summary>
        public static HoleReadBackResult ReadSpec(
            HoleFeature hole, HoleWorkbook workbook, HoleUnits defaultUnits, List<string>? warnings = null)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (workbook == null)
                throw new ArgumentNullException(nameof(workbook));

            HoleStoredDataReadResult? stored = ComSafe.Get(() => HoleStoredDataStore.Read(hole), warnings, "AWB hole data");
            return HoleReadBack.Read(stored, ReadFacts(hole, warnings), workbook, defaultUnits);
        }

        /// <summary>The hole's geometry as Inventor reports it.</summary>
        public static InventorHoleFacts ReadFacts(HoleFeature hole, List<string>? warnings = null)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));

            HoleSeat? seat = ComSafe.Get(() => (HoleTypeEnum?)hole.HoleType, warnings, "Hole type") switch
            {
                HoleTypeEnum.kCounterBoreHole => HoleSeat.Counterbore,
                HoleTypeEnum.kCounterSinkHole => HoleSeat.Countersink,
                HoleTypeEnum.kSpotFaceHole => null,
                _ => HoleSeat.None,
            };

            PartFeatureExtentEnum? extent = ComSafe.Get(() => (PartFeatureExtentEnum?)hole.ExtentType, warnings, "Hole termination");
            HoleTermination? termination = extent switch
            {
                PartFeatureExtentEnum.kDistanceExtent => HoleTermination.Distance,
                PartFeatureExtentEnum.kThroughAllExtent => HoleTermination.ThroughAll,
                PartFeatureExtentEnum.kToExtent => HoleTermination.ToFace,
                _ => null,
            };
            bool blind = termination == HoleTermination.Distance;

            bool tapped = ComSafe.Get(() => hole.Tapped);
            HoleTapInfo? tap = tapped ? ComSafe.Get(() => hole.TapInfo as HoleTapInfo) : null;
            bool fullThread = tap == null || ComSafe.Get(() => tap.FullTapDepth);

            HoleDrillPointTypeEnum? pointType = blind
                ? ComSafe.Get(() => (HoleDrillPointTypeEnum?)hole.DrillPointType)
                : null;
            bool flat = pointType == HoleDrillPointTypeEnum.kFlatDrillPointType;

            (string? isoCode, double? upper, double? lower) = ReadTolerance(hole, warnings);

            return new InventorHoleFacts(
                seat,
                tapped,
                tap == null ? null : ComSafe.Get(() => tap.ThreadDesignation),
                tap == null ? null : ComSafe.Get(() => tap.ThreadType),
                tap == null || fullThread ? null : Length(() => tap.ThreadDepth),
                Length(() => hole.HoleDiameter),
                termination,
                blind ? ComSafe.Get(() => (double?)hole.Depth, warnings, "Hole depth") : null,
                seat == HoleSeat.Counterbore ? Length(() => hole.CBoreDiameter) : null,
                seat == HoleSeat.Counterbore ? Length(() => hole.CBoreDepth) : null,
                seat == HoleSeat.Countersink ? Length(() => hole.CSinkDiameter) : null,
                seat == HoleSeat.Countersink ? Degrees(() => hole.CSinkAngle) : null,
                flat,
                blind && !flat ? Degrees(() => hole.BottomTipAngle) : null,
                isoCode,
                upper,
                lower,
                ReadFlip(hole))
            {
                ThreadDepthExpression = tap == null || fullThread || !blind ? null : Expression(() => tap.ThreadDepth),
                DepthExpression = blind ? Expression(() => ((DistanceExtent)hole.Extent).Distance) : null,
                // A dowel's counterbore depth may be an expression (task 106); read for any counterbore.
                CounterboreDepthExpression = seat == HoleSeat.Counterbore ? Expression(() => hole.CBoreDepth) : null,
            };
        }

        /// <summary>
        /// What shows the AWB fit on the hole now, for comparing with its stored data (task 092): the diameter, the
        /// tolerance (the ISO code only when the tolerance is a fit) and whether any face carries a fit appearance.
        /// </summary>
        public static HoleAppliedState ReadAppliedState(HoleFeature hole, List<string>? warnings = null)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));

            (string? isoCode, double? upper, double? lower) = ReadTolerance(hole, warnings);
            return new HoleAppliedState(
                Length(() => hole.HoleDiameter) ?? 0,
                isoCode,
                upper,
                lower,
                IsColoured(hole));
        }

        /// <summary>True when any face of the hole carries one of the fit appearances.</summary>
        public static bool IsColoured(HoleFeature hole)
        {
            Faces? faces = ComSafe.Get(() => hole.Faces);
            if (faces == null)
                return false;

            foreach (Face face in faces)
            {
                if (HoleFitAppearances.IsFitAppearance(ComSafe.Get(() => face.Appearance.DisplayName)))
                    return true;
            }

            return false;
        }

        private static (string? IsoCode, double? Upper, double? Lower) ReadTolerance(HoleFeature hole, List<string>? warnings)
        {
            Tolerance? tolerance = ComSafe.Get(() => hole.HoleDiameter.Tolerance, warnings, "Hole tolerance");
            if (tolerance == null)
                return (null, null, null);

            ToleranceTypeEnum? type = ComSafe.Get(() => (ToleranceTypeEnum?)tolerance.ToleranceType);
            if (type == null || type == ToleranceTypeEnum.kDefaultTolerance)
                return (null, null, null);

            bool fits = type == ToleranceTypeEnum.kLimitsFitsStackedTolerance
                || type == ToleranceTypeEnum.kLimitsFitsLinearTolerance
                || type == ToleranceTypeEnum.kLimitsFitsShowSizeTolerance
                || type == ToleranceTypeEnum.kLimitsFitsShowTolerance;
            string? code = fits ? ComSafe.Get(() => tolerance.HoleTolerance) : null;
            if (string.IsNullOrWhiteSpace(code))
                code = null;

            return (
                code?.Trim(),
                ComSafe.Get(() => (double?)tolerance.Upper),
                ComSafe.Get(() => (double?)tolerance.Lower));
        }

        /// <summary>True when the hole runs in the negative direction; the add-in makes holes positive (spike 081).</summary>
        private static bool ReadFlip(HoleFeature hole)
        {
            PartFeatureExtent? extent = ComSafe.Get(() => hole.Extent);
            PartFeatureExtentDirectionEnum? direction = extent switch
            {
                DistanceExtent distance => ComSafe.Get(() => (PartFeatureExtentDirectionEnum?)distance.Direction),
                ThroughAllExtent through => ComSafe.Get(() => (PartFeatureExtentDirectionEnum?)through.Direction),
                _ => null,
            };
            return direction == PartFeatureExtentDirectionEnum.kNegativeExtentDirection;
        }

        private static double? Length(Func<Parameter> parameter) =>
            ComSafe.Get(() => (double?)(double)parameter().Value);

        /// <summary>
        /// The parameter's expression when other parameters drive it (<c>d0</c>, <c>1.5 ul * d0 + 1 mm</c>; task 100),
        /// or null for a plain length such as <c>12 mm</c>.
        /// </summary>
        private static string? Expression(Func<Parameter> parameter) =>
            ComSafe.Get(() =>
            {
                Parameter p = parameter();
                return p.DrivenBy.Count > 0 ? p.Expression : null;
            });

        private static double? Degrees(Func<Parameter> parameter) =>
            ComSafe.Get(() => (double?)((double)parameter().Value * 180.0 / Math.PI));
    }
}
