using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Holes;
using InventorAddin.Core.Logging;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Reads a hole's cylindrical and conical faces into Core's <see cref="HolePreviewFace"/> for the preview graphics
    /// (task 110; spike 107 round 4): each face's axis, radius, cone half-angle, and points sampled along each of its
    /// edges (<see cref="HolePreviewSolids.PointsPerEdge"/> per edge, arcs and lines included). An edge that cannot be
    /// sampled, such as a drill point's apex, gives its start and stop vertices instead. <c>Face.CalculateFacets</c>
    /// is not used: it throws DISP_E_TYPEMISMATCH from C# (spike 107 round 2). Nothing here throws; a face that
    /// cannot be read is logged and left out.
    /// </summary>
    public static class HolePreviewFaceReader
    {
        private const string LogPrefix = AwbHoleViewModel.CommandName + ": ";

        /// <summary>The hole's cylindrical and conical faces, every centre's, in the order Inventor lists them.</summary>
        public static List<HolePreviewFace> Read(HoleFeature hole, ILog log)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            log ??= NullLog.Instance;

            var faces = new List<HolePreviewFace>();
            Faces? all = ComSafe.Get(() => hole.Faces);
            if (all == null)
                return faces;

            ComSafe.Run(() =>
            {
                foreach (Face face in all)
                {
                    HolePreviewFace? read = ReadFace(face, log);
                    if (read != null)
                        faces.Add(read);
                }
            });

            return faces;
        }

        private static HolePreviewFace? ReadFace(Face face, ILog log)
        {
            object? geometry = ComSafe.Get(() => face.Geometry);
            try
            {
                if (geometry is Cylinder cylinder)
                {
                    return new HolePreviewFace(
                        HolePreviewSurface.Cylinder, Vector(cylinder.BasePoint), Vector(cylinder.AxisVector),
                        cylinder.Radius, 0, false, EdgePoints(face));
                }

                if (geometry is Cone cone)
                {
                    return new HolePreviewFace(
                        HolePreviewSurface.Cone, Vector(cone.BasePoint), Vector(cone.AxisVector),
                        cone.Radius, cone.HalfAngle, cone.IsExpanding, EdgePoints(face));
                }
            }
            catch (Exception ex)
            {
                log.Warn(LogPrefix + $"a {geometry?.GetType().Name ?? "hole"} face could not be read for the preview ({ex.Message}); it is not drawn.");
            }

            return null;
        }

        /// <summary>Points along every edge of the face; an edge that cannot be sampled gives its vertices.</summary>
        private static List<HoleVector> EdgePoints(Face face)
        {
            var points = new List<HoleVector>();
            ComSafe.Run(() =>
            {
                foreach (Edge edge in face.Edges)
                {
                    try
                    {
                        CurveEvaluator evaluator = edge.Evaluator;
                        evaluator.GetParamExtents(out double start, out double end);
                        double[] parameters = HolePreviewSolids.SampleParams(start, end);
                        double[] xyz = new double[3 * parameters.Length];
                        evaluator.GetPointAtParam(ref parameters, ref xyz);
                        points.AddRange(HolePreviewSolids.PointsOf(xyz));
                    }
                    catch (Exception)
                    {
                        // A drill point's apex is a degenerate edge (spike 107 round 4): its vertices stand in.
                        foreach (Vertex? vertex in new[] { ComSafe.Get(() => edge.StartVertex), ComSafe.Get(() => edge.StopVertex) })
                        {
                            Point? point = vertex == null ? null : ComSafe.Get(() => vertex.Point);
                            if (point != null)
                                points.Add(Vector(point));
                        }
                    }
                }
            });

            return points;
        }

        private static HoleVector Vector(Point point) => new HoleVector(point.X, point.Y, point.Z);

        private static HoleVector Vector(UnitVector vector) => new HoleVector(vector.X, vector.Y, vector.Z);
    }
}
