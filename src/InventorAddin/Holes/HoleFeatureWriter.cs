using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Holes
{
    /// <summary>
    /// Turns Core's <see cref="HoleGeometry"/> into a real Inventor <see cref="HoleFeature"/>, and changes one in place
    /// (spec 09; spike 081 Results 1 and Recommendations 1 to 3). It copies values and decides nothing: every size,
    /// seat and bottom comes from the geometry. Lengths go to Inventor in centimetres and angles in radians; the tap
    /// drill goes to the hole's tap info in the thread table's units, as the geometry carries it (task 095).
    /// </summary>
    /// <remarks>
    /// A tapped hole takes its seat the same way as any other: the <c>AddCBore...</c> and <c>AddCSink...</c> methods
    /// take the tap info in place of the diameter, and <c>SetCBore</c>, <c>SetCSink</c> and <c>SetDrilled</c> change
    /// the seat of a tapped hole in place, keeping its thread and tap drill (task 099 spike, Inventor 2026). A dowel's
    /// seat faces are among <see cref="HoleFeature.Faces"/>, so <see cref="HoleFitWriter"/> colours them too.
    /// Nothing here opens a transaction: the preview session (task 089) runs each call inside its own inner
    /// transaction and aborts it when the result is a refusal. After every create or change the document is
    /// updated and the hole's health and face count are checked, since a hole pointing the wrong way cuts nothing
    /// without an error (spike 081).
    /// </remarks>
    public static class HoleFeatureWriter
    {
        private const string LogPrefix = AwbHoleViewModel.CommandName + ": ";

        /// <summary>
        /// Makes a hole at <paramref name="centres"/> (sketch points, all in one feature).
        /// </summary>
        /// <param name="centres">The hole centres, as sketch points.</param>
        /// <param name="endFace">The face the hole ends at, when the termination is To; otherwise ignored.</param>
        public static HoleWriteResult Create(
            PartDocument doc, HoleGeometry geometry, ObjectCollection centres, Face? endFace, ILog log)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));
            if (centres == null)
                throw new ArgumentNullException(nameof(centres));
            log ??= NullLog.Instance;

            var notes = new List<string>();
            string? refusal = CheckInputs(geometry, centres, endFace);
            if (refusal != null)
                return HoleWriteResult.Refused(refusal, null, notes);

            HoleFeature hole;
            try
            {
                hole = Add(doc, geometry, centres, endFace, notes, log);
            }
            catch (Exception ex)
            {
                log.Warn(LogPrefix + "Inventor refused the new hole: " + ex.Message);
                return HoleWriteResult.Refused(HoleFeatureMessages.Refused(ex.Message), null, notes);
            }

            return Check(doc, hole, geometry, notes, recreated: false, log);
        }

        /// <summary>
        /// Changes <paramref name="hole"/> in place to <paramref name="geometry"/>: centres, diameter, depth,
        /// termination, seat and drill point. A change between tapped and untapped, or of the thread or its tap
        /// drill, is not made in place, so the feature is made again in the same place in the browser; the result
        /// then carries the new feature.
        /// </summary>
        public static HoleWriteResult Change(
            PartDocument doc, HoleFeature hole, HoleGeometry geometry, ObjectCollection centres, Face? endFace, ILog log)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc));
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (geometry == null)
                throw new ArgumentNullException(nameof(geometry));
            if (centres == null)
                throw new ArgumentNullException(nameof(centres));
            log ??= NullLog.Instance;

            var notes = new List<string>();
            string? refusal = CheckInputs(geometry, centres, endFace);
            if (refusal != null)
                return HoleWriteResult.Refused(refusal, hole, notes);

            if (NeedsRecreate(hole, geometry))
                return Recreate(doc, hole, geometry, centres, endFace, log);

            try
            {
                hole.HoleCenterPoints = centres;

                double? currentDiameter = ComSafe.Get(() => (double?)(double)hole.HoleDiameter.Value);
                PartFeatureExtentEnum? currentExtent = ComSafe.Get(() => (PartFeatureExtentEnum?)hole.ExtentType);
                double? currentDepth = currentExtent == PartFeatureExtentEnum.kDistanceExtent
                    ? ComSafe.Get(() => (double?)hole.Depth)
                    : null;

                // Inventor refuses an in-between state (a hole wider than its old counterbore, a counterbore deeper
                // than the old hole, a thread deeper than the drill), so a growing value goes after what bounds it
                // and a shrinking one before. Through All counts as infinitely deep.
                bool wider = currentDiameter == null || geometry.HoleDiameterCm >= currentDiameter.Value;
                bool deeper = geometry.Termination != HoleTermination.Distance
                    || (currentExtent != PartFeatureExtentEnum.kThroughAllExtent
                        && (currentDepth == null || (geometry.DepthCm ?? 0) >= currentDepth.Value));

                if (deeper)
                    SetExtent(hole, geometry, endFace);
                if (wider)
                    SetSeat(hole, geometry);
                SetDiameter(hole, geometry, currentDiameter);
                if (!wider)
                    SetSeat(hole, geometry);
                if (deeper)
                {
                    SetThreadDepth(hole, geometry);
                }
                else
                {
                    // A shallower hole: the thread first, so it is never deeper than the drill.
                    SetThreadDepth(hole, geometry);
                    SetExtent(hole, geometry, endFace);
                }

                WriteExpressions(hole, geometry);
            }
            catch (Exception ex)
            {
                log.Warn(LogPrefix + $"Inventor refused the change to {ComSafe.Get(() => hole.Name)}: " + ex.Message);
                return HoleWriteResult.Refused(HoleFeatureMessages.Refused(ex.Message), hole, notes);
            }

            return Check(doc, hole, geometry, notes, recreated: false, log);
        }

        /// <summary>The hole centres as Inventor takes them.</summary>
        public static ObjectCollection Centres(Application app, IEnumerable<SketchPoint> points)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            ObjectCollection collection = app.TransientObjects.CreateObjectCollection();
            foreach (SketchPoint point in points)
                collection.Add(point);
            return collection;
        }

        private static string? CheckInputs(HoleGeometry geometry, ObjectCollection centres, Face? endFace)
        {
            if (centres.Count == 0)
                return HoleFeatureMessages.NoCentres;
            if (geometry.Termination == HoleTermination.ToFace && endFace == null)
                return HoleFeatureMessages.NoEndFace;
            return null;
        }

        // ---- create ----

        private static HoleFeature Add(
            PartDocument doc, HoleGeometry geometry, ObjectCollection centres, Face? endFace, List<string> notes, ILog log)
        {
            HoleFeatures features = doc.ComponentDefinition.Features.HoleFeatures;
            if (geometry.Thread is not { } thread)
            {
                HoleFeature drilled = AddWith(
                    features, features.CreateSketchPlacementDefinition(centres), geometry.HoleDiameterCm, geometry, endFace);
                WriteExpressions(drilled, geometry);
                return drilled;
            }

            // Inventor's own tap info, so the hole shows its thread in the model (task 095: a hole made with the
            // hidden _CreateTapInfo has no pitch and shows none). The workbook's tap drill is written afterwards.
            object tapInfo = thread.FullDepth
                ? features.CreateTapInfo(true, thread.ThreadType, thread.Designation, thread.Class, true)
                : features.CreateTapInfo(true, thread.ThreadType, thread.Designation, thread.Class, false, thread.ThreadDepthCm);
            HoleFeature hole = AddWith(features, features.CreateSketchPlacementDefinition(centres), tapInfo, geometry, endFace);
            ComSafe.Run(() => doc.Update());
            SetTapDrill(hole, thread, notes, log);
            WriteExpressions(hole, geometry);
            return hole;
        }

        /// <summary>
        /// Models the workbook's tap drill on a tapped hole (task 095). With the document's Tapped Hole Diameter at
        /// its default, Minor, Inventor models the hole at <c>MinorDiameterMin</c>; at Tap Drill, at
        /// <c>TapDrillDiameter</c>. Both are set, in the thread table's units, so either setting models the
        /// workbook's tap drill, and the hole keeps its pitch and so its thread in the model. Neither can be set on
        /// a tap info before the hole is made. If Inventor refuses, the hole stays at the thread's minor diameter and
        /// the result says so.
        /// </summary>
        private static void SetTapDrill(HoleFeature hole, HoleThread thread, List<string> notes, ILog log)
        {
            try
            {
                var tap = (HoleTapInfo)hole.TapInfo;
                tap.MinorDiameterMin = thread.TapDrillInTableUnits;
                tap.TapDrillDiameter = thread.TapDrillInTableUnits;
                hole.TapInfo = tap;
            }
            catch (Exception ex)
            {
                string note = HoleFeatureMessages.TapDrillFallback(thread.Designation, ex.Message);
                notes.Add(note);
                log.Warn(LogPrefix + $"setting the tap drill failed ({ex.GetType().Name}: {ex.Message}). {note}");
            }
        }

        private static HoleFeature AddWith(
            HoleFeatures features, SketchHolePlacementDefinition place, object diameterOrTapInfo, HoleGeometry g, Face? endFace)
        {
            PartFeatureExtentDirectionEnum direction = Direction(g);
            (object pointType, object pointAngle) = DrillPoint(g);

            switch (g.Termination)
            {
                case HoleTermination.ThroughAll:
                    return g.Seat switch
                    {
                        HoleSeat.Counterbore => features.AddCBoreByThroughAllExtent(
                            place, diameterOrTapInfo, direction, Required(g.CounterboreDiameterCm), Required(g.CounterboreDepthCm)),
                        HoleSeat.Countersink => features.AddCSinkByThroughAllExtent(
                            place, diameterOrTapInfo, direction, Required(g.CountersinkDiameterCm), Radians(g.CountersinkAngleDeg)),
                        _ => features.AddDrilledByThroughAllExtent(place, diameterOrTapInfo, direction),
                    };

                case HoleTermination.Distance:
                    double depth = Required(g.DepthCm);
                    return g.Seat switch
                    {
                        HoleSeat.Counterbore => features.AddCBoreByDistanceExtent2(
                            place, diameterOrTapInfo, depth, direction, Required(g.CounterboreDiameterCm),
                            Required(g.CounterboreDepthCm), pointType, pointAngle),
                        HoleSeat.Countersink => features.AddCSinkByDistanceExtent2(
                            place, diameterOrTapInfo, depth, direction, Required(g.CountersinkDiameterCm),
                            Radians(g.CountersinkAngleDeg), pointType, pointAngle),
                        _ => features.AddDrilledByDistanceExtent2(place, diameterOrTapInfo, depth, direction, pointType, pointAngle),
                    };

                case HoleTermination.ToFace:
                    return g.Seat switch
                    {
                        HoleSeat.Counterbore => features.AddCBoreByToFaceExtent2(
                            place, diameterOrTapInfo, endFace!, false, Required(g.CounterboreDiameterCm),
                            Required(g.CounterboreDepthCm), pointType, pointAngle),
                        HoleSeat.Countersink => features.AddCSinkByToFaceExtent2(
                            place, diameterOrTapInfo, endFace!, false, Required(g.CountersinkDiameterCm),
                            Radians(g.CountersinkAngleDeg), pointType, pointAngle),
                        _ => features.AddDrilledByToFaceExtent2(place, diameterOrTapInfo, endFace!, false, pointType, pointAngle),
                    };

                default:
                    throw new ArgumentOutOfRangeException(nameof(g), g.Termination, null);
            }
        }

        // ---- change ----

        private static void SetDiameter(HoleFeature hole, HoleGeometry g, double? current)
        {
            // A tapped hole's diameter comes from its thread.
            if (g.Thread != null)
                return;
            if (current is { } now && Math.Abs(now - g.HoleDiameterCm) < HoleStateComparison.LengthToleranceCm)
                return;
            hole.HoleDiameter.Value = g.HoleDiameterCm;
        }

        /// <summary>
        /// Gives the hole the geometry's seat. <c>SetCBore</c> and <c>SetCSink</c> change a hole's seat, but on a hole
        /// that already has that seat they do nothing, without an error (task 093), so a hole that has the seat
        /// already gets its seat parameters written instead.
        /// </summary>
        private static void SetSeat(HoleFeature hole, HoleGeometry g)
        {
            HoleTypeEnum? current = ComSafe.Get(() => (HoleTypeEnum?)hole.HoleType);
            switch (g.Seat)
            {
                case HoleSeat.Counterbore:
                    if (current == HoleTypeEnum.kCounterBoreHole)
                    {
                        SetParameter(hole.CBoreDiameter, Required(g.CounterboreDiameterCm), HoleStateComparison.LengthToleranceCm);
                        // A depth that was an expression and is now a plain number is written even when its value
                        // is the same, so the expression goes (task 106); a new expression is written afterwards. Dowels
                        // only: a clearance or tapped counterbore keeps an expression set in Inventor, as before.
                        SetParameter(
                            hole.CBoreDepth, Required(g.CounterboreDepthCm), HoleStateComparison.LengthToleranceCm,
                            force: g.Kind == HoleKind.Dowel && g.CounterboreDepthExpression == null
                                && ComSafe.Get(() => hole.CBoreDepth.DrivenBy.Count > 0));
                    }
                    else
                    {
                        hole.SetCBore(Required(g.CounterboreDiameterCm), Required(g.CounterboreDepthCm));
                    }
                    break;
                case HoleSeat.Countersink:
                    if (current == HoleTypeEnum.kCounterSinkHole)
                    {
                        SetParameter(hole.CSinkDiameter, Required(g.CountersinkDiameterCm), HoleStateComparison.LengthToleranceCm);
                        SetParameter(hole.CSinkAngle, Radians(g.CountersinkAngleDeg), Radians(HoleReadBack.AngleToleranceDeg));
                    }
                    else
                    {
                        hole.SetCSink(Required(g.CountersinkDiameterCm), Radians(g.CountersinkAngleDeg));
                    }
                    break;
                default:
                    if (current != HoleTypeEnum.kDrilledHole)
                        hole.SetDrilled();
                    break;
            }
        }

        /// <summary>
        /// Writes <paramref name="value"/> (centimetres or radians) unless the parameter already has it, or always when
        /// <paramref name="force"/> is set.
        /// </summary>
        private static void SetParameter(Parameter parameter, double value, double tolerance, bool force = false)
        {
            double? now = ComSafe.Get(() => (double?)(double)parameter.Value);
            if (!force && now is { } v && Math.Abs(v - value) < tolerance)
                return;
            parameter.Value = value;
        }

        private static void SetExtent(HoleFeature hole, HoleGeometry g, Face? endFace)
        {
            (object pointType, object pointAngle) = DrillPoint(g);
            switch (g.Termination)
            {
                case HoleTermination.Distance:
                    hole.SetDistanceExtent2(Required(g.DepthCm), Direction(g), pointType, pointAngle);
                    break;
                case HoleTermination.ThroughAll:
                    hole.SetThroughAllExtent(Direction(g));
                    break;
                case HoleTermination.ToFace:
                    hole.SetToFaceExtent2(endFace!, false, pointType, pointAngle);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(g), g.Termination, null);
            }
        }

        private static void SetThreadDepth(HoleFeature hole, HoleGeometry g)
        {
            if (g.Thread is not { FullDepth: false } thread)
                return;
            if (ComSafe.Get(() => hole.TapInfo) is HoleTapInfo tap)
                tap.ThreadDepth.Value = thread.ThreadDepthCm;
        }

        /// <summary>
        /// Writes each depth typed as an expression into its parameter, so the hole stays parametric (task 100). The
        /// depths already hold the values the expressions came to, so this changes nothing unless the part's
        /// parameters moved since the window checked them. Each parameter first takes the window's length unit, so a
        /// number with no unit in the expression reads as the window read it (spike 100). A depth typed as a plain
        /// number is left as the value in centimetres written above: writing a value replaces an earlier expression
        /// (spike 100), and it keeps the hole's parameters in the units they were in. Inventor refuses an expression
        /// that refers to the hole's own parameters; that throws, and the caller refuses the change. A dowel's
        /// counterbore depth typed as an expression (task 106) is written the same way, whatever the termination.
        /// </summary>
        private static void WriteExpressions(HoleFeature hole, HoleGeometry g)
        {
            string unit = HoleExpressions.UnitName(g.ExpressionUnits);
            if (g.Seat == HoleSeat.Counterbore && g.CounterboreDepthExpression is { } cboreExpression)
                WriteExpression(hole.CBoreDepth, cboreExpression, unit);

            if (g.Termination != HoleTermination.Distance)
                return;

            if (g.Thread is { FullDepth: false, ThreadDepthExpression: { } threadExpression }
                && ComSafe.Get(() => hole.TapInfo) is HoleTapInfo tap)
                WriteExpression(tap.ThreadDepth, threadExpression, unit);
            if (g.DepthExpression is { } depthExpression && ComSafe.Get(() => hole.Extent) is DistanceExtent extent)
                WriteExpression(extent.Distance, depthExpression, unit);
        }

        private static void WriteExpression(Parameter parameter, string expression, string unit)
        {
            parameter.set_Units(unit);
            parameter.Expression = expression;
        }

        /// <summary>
        /// True when the hole cannot be changed in place into <paramref name="g"/>: tapped to untapped or back, or a
        /// tapped hole whose thread, full-depth setting or tap drill differs, or one made before task 095 that shows
        /// no thread.
        /// </summary>
        private static bool NeedsRecreate(HoleFeature hole, HoleGeometry g)
        {
            bool tapped = ComSafe.Get(() => hole.Tapped);
            if (tapped != (g.Thread != null))
                return true;
            if (g.Thread is not { } thread)
                return false;
            if (ComSafe.Get(() => hole.TapInfo) is not HoleTapInfo tap)
                return true;

            return !Same(ComSafe.Get(() => tap.ThreadDesignation), thread.Designation)
                || !Same(ComSafe.Get(() => tap.ThreadType), thread.ThreadType)
                || !Same(ComSafe.Get(() => tap.Class), thread.Class)
                || ComSafe.Get(() => (bool?)tap.FullTapDepth) != thread.FullDepth
                // No pitch: made by the hidden _CreateTapInfo before task 095, so it shows no thread. Made again.
                || ComSafe.Get(() => (double?)tap.Pitch) == null
                // A tap drill changed in the workbook; a value that cannot be read is not a change.
                || HoleThreadNames.TapDrillMatches(ComSafe.Get(() => tap.TapDrillDiameter), thread) == false
                || HoleThreadNames.TapDrillMatches(ComSafe.Get(() => tap.MinorDiameterMin), thread) == false;
        }

        /// <summary>
        /// Deletes the hole and makes it again where it was in the browser: the end-of-part marker is moved to just
        /// after the old hole, the old hole is deleted (its sketch and dependents kept), the new one is made there,
        /// and the marker goes back to the bottom. If the marker cannot be moved, the new hole is made at the end
        /// and the result says so.
        /// </summary>
        private static HoleWriteResult Recreate(
            PartDocument doc, HoleFeature old, HoleGeometry g, ObjectCollection centres, Face? endFace, ILog log)
        {
            var notes = new List<string>();
            PartComponentDefinition def = doc.ComponentDefinition;
            string? name = ComSafe.Get(() => old.Name);

            bool placeKept = true;
            try
            {
                old.SetEndOfPart(false);
            }
            catch (Exception ex)
            {
                placeKept = false;
                log.Warn(LogPrefix + $"could not move the end of part after {name}: " + ex.Message);
            }

            HoleFeature hole;
            try
            {
                old.Delete(true, true, true);
                hole = Add(doc, g, centres, endFace, notes, log);
            }
            catch (Exception ex)
            {
                if (placeKept)
                    ComSafe.Run(() => def.SetEndOfPartToTopOrBottom(false));
                log.Warn(LogPrefix + $"Inventor refused to make {name} again: " + ex.Message);
                return HoleWriteResult.Refused(HoleFeatureMessages.Refused(ex.Message), null, notes, recreated: true);
            }

            if (placeKept)
            {
                try
                {
                    def.SetEndOfPartToTopOrBottom(false);
                }
                catch (Exception ex)
                {
                    log.Warn(LogPrefix + "could not move the end of part back to the bottom: " + ex.Message);
                    return HoleWriteResult.Refused(HoleFeatureMessages.Refused(ex.Message), hole, notes, recreated: true);
                }
            }
            else
            {
                notes.Add(HoleFeatureMessages.RecreatedAtEnd);
            }

            if (!string.IsNullOrEmpty(name))
                ComSafe.Run(() => hole.Name = name!);

            log.Info(LogPrefix + $"{name} made again ({(placeKept ? "same place" : "at the end")}).");
            HoleWriteResult result = Check(doc, hole, g, notes, recreated: true, log);
            LogUnhealthyFeatures(def, name, log);
            return result;
        }

        /// <summary>
        /// After a hole is made again, logs every feature of the part that is not healthy, so a feature that used the
        /// old hole (a fillet on its edge, a pattern of it) shows in the log.
        /// </summary>
        private static void LogUnhealthyFeatures(PartComponentDefinition def, string? name, ILog log)
        {
            var unhealthy = new List<string>();
            ComSafe.Run(() =>
            {
                foreach (PartFeature feature in def.Features)
                {
                    HealthStatusEnum? health = ComSafe.Get(() => (HealthStatusEnum?)feature.HealthStatus);
                    if (health != null
                        && health != HealthStatusEnum.kUpToDateHealth
                        && health != HealthStatusEnum.kSuppressedHealth
                        && health != HealthStatusEnum.kBeyondStopNodeHealth)
                        unhealthy.Add($"{ComSafe.Get(() => feature.Name) ?? "?"} ({health})");
                }
            });

            if (unhealthy.Count > 0)
                log.Warn(LogPrefix + $"after making {name} again, these features are not healthy: {string.Join(", ", unhealthy)}.");
            else
                log.Info(LogPrefix + $"after making {name} again, every feature is healthy.");
        }

        // ---- shared ----

        private static HoleWriteResult Check(
            PartDocument doc, HoleFeature hole, HoleGeometry geometry, List<string> notes, bool recreated, ILog log)
        {
            ComSafe.Run(() => doc.Update());

            HealthStatusEnum? health = ComSafe.Get(() => (HealthStatusEnum?)hole.HealthStatus);
            if (health != HealthStatusEnum.kUpToDateHealth)
            {
                string status = health?.ToString() ?? "unknown";
                log.Warn(LogPrefix + $"hole {ComSafe.Get(() => hole.Name)} is unhealthy: {status}.");
                return HoleWriteResult.Refused(HoleFeatureMessages.Unhealthy(status), hole, notes, recreated);
            }

            int faces = ComSafe.Get(() => hole.Faces.Count);
            if (faces == 0)
            {
                log.Warn(LogPrefix + $"hole {ComSafe.Get(() => hole.Name)} has no faces.");
                return HoleWriteResult.Refused(HoleFeatureMessages.CutsNothing, hole, notes, recreated);
            }

            CheckSeat(hole, geometry, log);
            return HoleWriteResult.Made(hole, notes, recreated);
        }

        /// <summary>
        /// Reads the seat back and logs a warning for each value that does not match the geometry, so a seat that did
        /// not follow a change shows in the log (task 093).
        /// </summary>
        private static void CheckSeat(HoleFeature hole, HoleGeometry geometry, ILog log)
        {
            InventorHoleFacts facts = HoleFeatureReader.ReadFacts(hole);
            IReadOnlyList<string> differences = HoleSeatCheck.Differences(geometry, facts);
            if (differences.Count > 0)
                log.Warn(LogPrefix + $"hole {ComSafe.Get(() => hole.Name)} does not match its seat: {string.Join(" ", differences)}");
        }

        /// <summary>Into the material, for a sketch whose normal points out of it (spike 081); Flip reverses it.</summary>
        private static PartFeatureExtentDirectionEnum Direction(HoleGeometry g) =>
            g.Flip ? PartFeatureExtentDirectionEnum.kNegativeExtentDirection : PartFeatureExtentDirectionEnum.kPositiveExtentDirection;

        /// <summary>The drill point arguments of the <c>...Extent2</c> methods; left out when the hole has none.</summary>
        private static (object Type, object Angle) DrillPoint(HoleGeometry g)
        {
            if (g.DrillPoint is not { } point)
                return (Type.Missing, Type.Missing);
            if (point.IsFlat)
                return (HoleDrillPointTypeEnum.kFlatDrillPointType, Type.Missing);
            return (HoleDrillPointTypeEnum.kAngledDrillPointType, Radians(point.AngleDeg));
        }

        private static double Radians(double? degrees) => Required(degrees) * Math.PI / 180.0;

        private static double Required(double? value) =>
            value ?? throw new InvalidOperationException("The hole geometry is missing a value its seat or termination needs.");

        private static bool Same(string? a, string b) =>
            string.Equals((a ?? string.Empty).Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
