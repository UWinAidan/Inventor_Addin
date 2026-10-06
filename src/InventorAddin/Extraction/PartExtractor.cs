using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin.Extraction
{
    public static class PartExtractor
    {
        public static void Fill(PartData data, PartDocument doc, ExtractionOptions options)
        {
            var w = data.Warnings;
            var units = new UnitsFormatter((Document)doc);
            PartComponentDefinition def = doc.ComponentDefinition;

            data.BomStructure = ComSafe.Get(() => def.BOMStructure.ToString(), w, "BOM structure");
            data.IsContentCenterPart = ComSafe.Get(() => def.IsContentMember);

            data.Material = ComSafe.Get(() => ReadAsset(doc.ActiveMaterial), w, "Material");
            data.Appearance = ComSafe.Get(() => ReadAsset(doc.ActiveAppearance), w, "Appearance");
            data.RangeBox = ComSafe.Get(() => ReadBox(def.RangeBox), w, "Range box");
            data.SurfaceBodyCount = ComSafe.Get(() => def.SurfaceBodies.Count);

            if (options.IncludeMassProperties)
                data.Physical = ComSafe.Get(() => ReadMass(def.MassProperties, units), w, "Mass properties");

            if (def is SheetMetalComponentDefinition sm)
                data.SheetMetal = ComSafe.Get(() => ReadSheetMetal(sm, units), w, "Sheet metal");

            data.Holes = ReadHoles(def, units, w);
            data.Finishes = FinishReader.Read(def.Features, w);
            data.FeatureCounts = CountFeatures(def, w);
        }

        public static AssetInfo? ReadAsset(Asset? asset)
        {
            if (asset == null)
                return null;
            return new AssetInfo
            {
                Name = asset.Name,
                DisplayName = asset.DisplayName,
                Category = ComSafe.Get(() => asset.CategoryName),
                LibraryName = ComSafe.Get(() => (asset.Parent as AssetLibrary)?.DisplayName),
            };
        }

        public static BoundingBox ReadBox(Box box) => new()
        {
            Min = ToPoint(box.MinPoint),
            Max = ToPoint(box.MaxPoint),
        };

        public static PhysicalProperties ReadMass(MassProperties mp, UnitsFormatter units)
        {
            double mass = mp.Mass, volume = mp.Volume, area = mp.Area;
            return new PhysicalProperties
            {
                MassKg = mass,
                VolumeCm3 = volume,
                AreaCm2 = area,
                // kg / cm^3 → g / cm^3
                DensityGPerCm3 = volume > 0 ? mass * 1000.0 / volume : null,
                CenterOfMass = ComSafe.Get(() => ToPoint(mp.CenterOfMass)),
                MassDisplay = units.Mass(mass),
                VolumeDisplay = units.Volume(volume),
                AreaDisplay = units.Area(area),
            };
        }

        private static SheetMetalData ReadSheetMetal(SheetMetalComponentDefinition sm, UnitsFormatter units)
        {
            double thickness = (double)sm.Thickness.Value;
            var data = new SheetMetalData
            {
                StyleName = ComSafe.Get(() => sm.ActiveSheetMetalStyle.Name),
                ThicknessCm = thickness,
                ThicknessDisplay = units.Length(thickness),
                HasFlatPattern = sm.HasFlatPattern,
                BendCount = ComSafe.Get(() => (int?)sm.Bends.Count),
            };

            if (sm.HasFlatPattern)
            {
                FlatPattern flat = sm.FlatPattern;
                data.FlatLengthCm = ComSafe.Get(() => (double?)flat.Length);
                data.FlatWidthCm = ComSafe.Get(() => (double?)flat.Width);
                data.FlatLengthDisplay = units.Length(data.FlatLengthCm);
                data.FlatWidthDisplay = units.Length(data.FlatWidthCm);
            }
            return data;
        }

        private static List<HoleData> ReadHoles(PartComponentDefinition def, UnitsFormatter units, List<string> w)
        {
            var list = new List<HoleData>();
            HoleFeatures? holes = ComSafe.Get(() => def.Features.HoleFeatures, w, "Hole features");
            if (holes == null)
                return list;

            foreach (HoleFeature hole in holes)
            {
                ComSafe.Run(() => list.Add(ReadHole(hole, units)), w, $"Hole '{hole.Name}'");
            }
            return list;
        }

        private static HoleData ReadHole(HoleFeature hole, UnitsFormatter units)
        {
            var data = new HoleData
            {
                FeatureName = hole.Name,
                Suppressed = hole.Suppressed,
                HoleType = hole.HoleType switch
                {
                    HoleTypeEnum.kDrilledHole => "Drilled",
                    HoleTypeEnum.kCounterBoreHole => "Counterbore",
                    HoleTypeEnum.kCounterSinkHole => "Countersink",
                    HoleTypeEnum.kSpotFaceHole => "Spotface",
                    var t => t.ToString(),
                },
                Termination = ComSafe.Get(() => hole.ExtentType.ToString()),
                DiameterCm = ParamValue(() => hole.HoleDiameter),
                CBoreDiameterCm = ParamValue(() => hole.CBoreDiameter),
                CBoreDepthCm = ParamValue(() => hole.CBoreDepth),
                CSinkDiameterCm = ParamValue(() => hole.CSinkDiameter),
                CSinkAngleRad = ParamValue(() => hole.CSinkAngle),
                SpotFaceDiameterCm = ParamValue(() => hole.SpotFaceDiameter),
                SpotFaceDepthCm = ParamValue(() => hole.SpotFaceDepth),
                Tapped = ComSafe.Get(() => hole.Tapped),
            };

            if (hole.Extent is DistanceExtent distance)
                data.DepthCm = ComSafe.Get(() => (double?)(double)distance.Distance.Value);

            data.DiameterDisplay = units.Length(data.DiameterCm);
            data.DepthDisplay = units.Length(data.DepthCm);

            if (data.Tapped)
                data.Thread = ReadThread(hole.TapInfo);

            ComSafe.Run(() =>
            {
                foreach (object center in hole.HoleCenterPoints)
                {
                    Point? p = center switch
                    {
                        SketchPoint sp => sp.Geometry3d,
                        SketchPoint3D sp3 => sp3.Geometry,
                        WorkPoint wp => wp.Point,
                        _ => null,
                    };
                    if (p != null)
                        data.Centers.Add(ToPoint(p));
                }
            });

            return data;
        }

        // TapInfo is HoleTapInfo or TaperedThreadInfo; late-bound to read whichever members exist.
        private static ThreadData? ReadThread(object? tapInfo)
        {
            if (tapInfo == null)
                return null;
            dynamic t = tapInfo;
            return new ThreadData
            {
                ThreadType = ComSafe.Get(() => (string)t.ThreadType),
                Designation = ComSafe.Get(() => (string)t.ThreadDesignation),
                NominalSize = ComSafe.Get(() => (string)t.NominalSize),
                ThreadClass = ComSafe.Get(() => (string)t.Class),
                RightHanded = ComSafe.Get(() => (bool?)(bool)t.RightHanded),
                FullDepth = ComSafe.Get(() => (bool?)(bool)t.FullTapDepth),
                ThreadDepthCm = ComSafe.Get(() => (double?)(double)t.ThreadDepth.Value),
            };
        }

        private static Dictionary<string, int> CountFeatures(PartComponentDefinition def, List<string> w)
        {
            var counts = new Dictionary<string, int>();
            ComSafe.Run(() =>
            {
                foreach (PartFeature f in def.Features)
                {
                    string key = f.Type.ToString();
                    counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            }, w, "Feature count");
            return counts;
        }

        private static double? ParamValue(System.Func<Parameter> getter) =>
            ComSafe.Get(() => (double?)(double)getter().Value);

        public static Point3D ToPoint(Point p) => new() { X = p.X, Y = p.Y, Z = p.Z };
    }
}
