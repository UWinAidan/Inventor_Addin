using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Inventor;
using InventorAddin.Core.Drawing;
using InventorAddin.Core.Logging;
using IODirectory = System.IO.Directory;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;
using SysEnvironment = System.Environment;

// The Drawing folder holds only the print template builder; its namespace names the feature, not the folder (Inventor has types named Drawing*).
namespace InventorAddin.PrintTemplates
{
    /// <summary>
    /// Draws one AWB print template (task 187) from <see cref="PrintTemplateLayout"/> and saves it as an <c>.idw</c>: a copy of Inventor's own ISO
    /// (metric) or ANSI (inches) template with its title block, border and sheet formats replaced by ours. It only draws: every position, size
    /// and text comes from Core. The calls are the ones spike 183 confirmed (rounds 1 to 3) and the hole table code already uses; what the spike
    /// did not cover is listed in the task's Verification section.
    /// </summary>
    /// <remarks>
    /// There is no transaction: the document is a new, unsaved drawing this class makes and discards on failure, so there is nothing for an
    /// Undo to protect. Millimetres in Core, centimetres in Inventor: every coordinate is converted once, in <see cref="P"/> and <see cref="Cm"/>.
    /// </remarks>
    internal sealed class PrintTemplateBuilder
    {
        private const string TitleBlockName = "AWB Title Block";
        private const string TriadName = "AWB Triad";

        // Property sets and ids from spike 183, round 1 (built-in properties carry their id; custom ones are matched by name).
        private const string DesignTracking = "{32853F0F-3444-11D1-9E93-0060B03C1CA6}";
        private const string SummaryInfo = "{F29F85E0-4FF9-1068-AB91-08002B27B3D9}";
        private const string UserDefined = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}";
        private const string DesignTrackingSet = "Design Tracking Properties";
        private const string SummarySet = "Inventor Summary Information";
        private const string UserDefinedSet = "Inventor User Defined Properties";

        private const string InventorYear = "2026";

        private readonly Application _app;
        private readonly TransientGeometry _geometry;
        private readonly string _logoPath;
        private readonly ILog _log;
        private readonly List<string> _warnings = new List<string>();
        private readonly List<double> _widthRatios = new List<double>();

        public PrintTemplateBuilder(Application app, string logoPath, ILog log)
        {
            _app = app ?? throw new ArgumentNullException(nameof(app));
            _geometry = app.TransientGeometry;
            _logoPath = logoPath ?? throw new ArgumentNullException(nameof(logoPath));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>What the builder is doing now; read it when <see cref="Build"/> throws to say which step failed.</summary>
        public string Step { get; private set; } = "starting";

        /// <summary>Things that did not go as expected but did not stop the build (logged, and listed in the report).</summary>
        public IReadOnlyList<string> Warnings => _warnings;

        public static string FileNameFor(PrintUnits units) => units == PrintUnits.Metric ? "AWB Metric.idw" : "AWB Imperial.idw";

        /// <summary>
        /// Makes the template for <paramref name="units"/> and saves it as <paramref name="finalPath"/>. It saves beside it under a
        /// temporary name first and moves it over any older file only when the build is complete, so a failure never damages an existing
        /// template. On a failure the half-made drawing is closed without saving and the temporary file is removed.
        /// </summary>
        public void Build(PrintUnits units, string finalPath)
        {
            PrintTemplateLayout layout = PrintTemplateLayout.For(units);
            string folder = IOPath.GetDirectoryName(finalPath) ?? throw new ArgumentException("The path has no folder.", nameof(finalPath));
            string tempPath = IOPath.Combine(folder, IOPath.GetFileNameWithoutExtension(finalPath) + ".building.idw");
            DrawingDocument? doc = null;

            try
            {
                Step = "finding Inventor's starting drawing template";
                string start = FindStartTemplate(units);
                _log.Info($"Build Print Template ({units}): starting from '{start}'.");

                Step = "creating the drawing";
                Document created = _app.Documents.Add(DocumentTypeEnum.kDrawingDocumentObject, start, true);
                doc = (DrawingDocument)created;

                Step = "clearing Inventor's own sheet formats and title block";
                Sheet sheet = doc.Sheets[1];
                ClearSheet(sheet);
                foreach (SheetFormat format in doc.SheetFormats.Cast<SheetFormat>().ToList())
                    { SheetFormat old = format; ComSafe.Run(() => old.Delete(), _warnings, "Deleting Inventor's own sheet format"); }

                Step = "setting the standard (third angle, period decimal marker)";
                DrawingStandardStyle standard = ConfigureStandard(doc, units);

                Step = "setting the white sheet colour";
                doc.SheetSettings.SheetColor = _app.TransientObjects.CreateColor(255, 255, 255, 1.0);

                if (units == PrintUnits.Imperial)
                {
                    // Spike 183, round 3: changing the unit after the field was placed did not change "kg"; so set it first.
                    Step = "setting the mass unit to lb";
                    doc.UnitsOfMeasure.MassUnits = UnitsTypeEnum.kLbMassMassUnits;
                }

                Step = "making the parts list and revision table styles";
                ConfigureTableStyles(standard);

                Step = "copying the dimension styles to AWB styles with a period decimal marker";
                SetStyleDecimalMarkers(standard.ActiveObjectDefaults, units);

                Step = "drawing the title block";
                TitleBlockDefinition titleBlock = DrawTitleBlock(doc, layout);

                Step = "drawing the X/Y triad symbol";
                SketchedSymbolDefinition triad = DrawTriad(doc);

                MakeSheetFormats(doc, sheet, layout, titleBlock, triad);

                Step = "removing Inventor's own title block definition";
                foreach (TitleBlockDefinition old in doc.TitleBlockDefinitions.Cast<TitleBlockDefinition>().ToList())
                {
                    string? name = ComSafe.Get(() => old.Name);
                    if (name != TitleBlockName)
                        ComSafe.Run(() => old.Delete(), _warnings, $"Deleting the title block definition '{name}'");
                }

                Step = $"saving '{tempPath}'";
                IODirectory.CreateDirectory(folder);
                if (IOFile.Exists(tempPath))
                    IOFile.Delete(tempPath);
                doc.SaveAs(tempPath, false);

                // Close(true) skips saving (spike 032): it was just saved. A template cannot be used while it is open (spike 183, round 2).
                Step = "closing the template";
                doc.Close(true);
                doc = null;

                Step = $"moving it to '{finalPath}'";
                IOFile.Move(tempPath, finalPath, true);

                foreach (string warning in _warnings)
                    _log.Warn($"Build Print Template ({units}): {warning}");
                _log.Info($"Build Print Template ({units}): saved '{finalPath}' with {_warnings.Count} warning(s).");
            }
            catch (Exception)
            {
                if (doc != null)
                {
                    try { doc.Close(true); }
                    catch (Exception closeEx) { _log.Error("Build Print Template: closing the half-made drawing failed.", closeEx); }
                }

                try
                {
                    if (IOFile.Exists(tempPath))
                        IOFile.Delete(tempPath);
                }
                catch (Exception deleteEx)
                {
                    _log.Error($"Build Print Template: removing '{tempPath}' failed.", deleteEx);
                }

                throw;
            }
        }

        // ---- Start file and styles

        /// <summary>
        /// Inventor's ISO (mm) or ANSI (in) drawing template: beside the default drawing template the lookup returns, then under the
        /// public documents folder (spike 183, round 2: <c>Templates\en-US\Metric\ISO.idw</c>). ANSI (in) is the file Inventor ships
        /// in <c>Templates\en-US\English</c>; spike 183 did not use it.
        /// </summary>
        private string FindStartTemplate(PrintUnits units)
        {
            string relative = units == PrintUnits.Metric
                ? IOPath.Combine("Metric", "ISO.idw")
                : IOPath.Combine("English", "ANSI (in).idw");

            var candidates = new List<string>();
            string? found = ComSafe.Get(() => _app.FileManager.GetTemplateFile(
                DocumentTypeEnum.kDrawingDocumentObject, SystemOfMeasureEnum.kDefaultSystemOfMeasure,
                DraftingStandardEnum.kDefault_DraftingStandard, Type.Missing));
            string? foundFolder = string.IsNullOrWhiteSpace(found) ? null : IOPath.GetDirectoryName(found);
            if (foundFolder != null)
                candidates.Add(IOPath.Combine(foundFolder, relative));
            candidates.Add(IOPath.Combine(
                SysEnvironment.GetFolderPath(SysEnvironment.SpecialFolder.CommonDocuments),
                "Autodesk", $"Inventor {InventorYear}", "Templates", "en-US", relative));

            foreach (string candidate in candidates)
            {
                if (IOFile.Exists(candidate))
                    return candidate;
            }

            throw new InvalidOperationException("Inventor's own template was not found. Looked for: " + string.Join("; ", candidates));
        }

        /// <summary>
        /// A local copy of the active standard with third angle projection and a period decimal marker, made active (spike 183, round 2:
        /// setting the library style does not survive a save; a local copy does). The decimal marker member is confirmed, its value not.
        /// </summary>
        private DrawingStandardStyle ConfigureStandard(DrawingDocument doc, PrintUnits units)
        {
            DrawingStylesManager styles = doc.StylesManager;
            var copy = (DrawingStandardStyle)styles.ActiveStandardStyle.Copy($"AWB {units}");
            copy.FirstAngleProjection = false;
            ComSafe.Run(() => copy.DecimalMarkerType = DecimalMarkerTypeEnum.kPeriodDecimalMarker, _warnings, "Setting the period decimal marker");
            styles.ActiveStandardStyle = copy;
            return copy;
        }

        /// <summary>
        /// Tasks 187, 223 and 226. Spike 225: the drawing's dimensions all use one library style ('Default (ISO)', 'Default (ANSI)' on
        /// imperial) and Inventor replaces a template's copy of a library style with the comma library version on every
        /// <c>Documents.Add</c>. So on metric (the imperial ANSI styles already print a period) each distinct dimension style the object defaults name is copied to an "AWB ..." local style, the
        /// copy gets the period, and every object defaults member that named the original is pointed at the copy. The feature control
        /// frame style gets the same, with <c>LinearUnits</c> set first (a default unit made the marker write fail with E_FAIL) and no
        /// alternate units. The parts list, revision table and hole table styles have no marker member. <paramref name="defaults"/> is
        /// the standard's own (local) object defaults, so the members can be reassigned.
        /// </summary>
        private void SetStyleDecimalMarkers(ObjectDefaultsStyle defaults, PrintUnits units)
        {
            const DecimalMarkerTypeEnum period = DecimalMarkerTypeEnum.kPeriodDecimalMarker;
            var done = new List<string>();
            int notPeriod = 0;

            // Every DimensionStyle member of ObjectDefaultsStyle (confirmed by reflection on the interop DLL), with its setter.
            var members = new List<(Func<DimensionStyle> Get, Action<DimensionStyle> Set)>
            {
                (() => defaults.AngularDimensionStyle, v => defaults.AngularDimensionStyle = v),
                (() => defaults.BaselineDimensionStyle, v => defaults.BaselineDimensionStyle = v),
                (() => defaults.BendNoteStyle, v => defaults.BendNoteStyle = v),
                (() => defaults.BendTagStyle, v => defaults.BendTagStyle = v),
                (() => defaults.ChainDimensionStyle, v => defaults.ChainDimensionStyle = v),
                (() => defaults.ChamferNoteStyle, v => defaults.ChamferNoteStyle = v),
                (() => defaults.DiameterDimensionStyle, v => defaults.DiameterDimensionStyle = v),
                (() => defaults.HoleNoteStyle, v => defaults.HoleNoteStyle = v),
                (() => defaults.HoleTagStyle, v => defaults.HoleTagStyle = v),
                (() => defaults.LeaderTextStyle, v => defaults.LeaderTextStyle = v),
                (() => defaults.LinearDimensionStyle, v => defaults.LinearDimensionStyle = v),
                (() => defaults.OrdinateDimensionStyle, v => defaults.OrdinateDimensionStyle = v),
                (() => defaults.OrdinateSetDimensionStyle, v => defaults.OrdinateSetDimensionStyle = v),
                (() => defaults.PunchNoteStyle, v => defaults.PunchNoteStyle = v),
                (() => defaults.RadialDimensionStyle, v => defaults.RadialDimensionStyle = v),
                (() => defaults.ThreadNoteStyle, v => defaults.ThreadNoteStyle = v),
            };

            var copies = new Dictionary<string, DimensionStyle?>(StringComparer.Ordinal);
            // Imperial: 'Default (ANSI)' already prints a period (spike 225), so only metric copies the dimension styles.
            foreach (var member in units == PrintUnits.Metric ? members : members.Take(0))
            {
                DimensionStyle? original = ComSafe.Get(member.Get);
                string? name = original == null ? null : ComSafe.Get(() => original.Name);
                if (original == null || name == null)
                    continue;

                if (!copies.TryGetValue(name, out DimensionStyle? copy))
                {
                    string copyName = name.StartsWith("AWB ", StringComparison.Ordinal) ? name : "AWB " + name;
                    copy = null;
                    if (copyName != name)
                    {
                        copy = ComSafe.Get(() => (DimensionStyle)original.Copy(copyName));
                        if (copy == null)
                            _warnings.Add($"Dimension style '{name}' could not be copied to '{copyName}'; its decimal marker was set in place and Inventor may restore the library's comma on a new drawing.");
                    }

                    DimensionStyle target = copy ?? original;
                    ComSafe.Run(() => target.DecimalMarkerType = period, _warnings, $"Setting the decimal marker of dimension style '{copyName}'");
                    ComSafe.Run(() => target.AlternateDecimalMarkerType = period, _warnings, $"Setting the alternate decimal marker of dimension style '{copyName}'");
                    if (ComSafe.Get(() => (DecimalMarkerTypeEnum?)target.DecimalMarkerType) != period)
                        notPeriod++;
                    copies[name] = copy;
                    done.Add(copy != null ? copyName : name);
                }

                if (copy != null)
                {
                    DimensionStyle assign = copy;
                    ComSafe.Run(() => member.Set(assign), _warnings, $"Pointing the object defaults at dimension style '{(name.StartsWith("AWB ", StringComparison.Ordinal) ? name : "AWB " + name)}'");
                }
            }

            FeatureControlFrameStyle? frame = ComSafe.Get(() => defaults.FeatureControlFrameStyle);
            string? frameName = frame == null ? null : ComSafe.Get(() => frame.Name);
            if (frame != null && frameName != null)
            {
                string copyName = frameName.StartsWith("AWB ", StringComparison.Ordinal) ? frameName : "AWB " + frameName;
                FeatureControlFrameStyle? copy = copyName == frameName ? null : ComSafe.Get(() => (FeatureControlFrameStyle)frame.Copy(copyName));
                if (copy == null && copyName != frameName)
                    _warnings.Add($"Feature control frame style '{frameName}' could not be copied to '{copyName}'; its decimal marker was set in place.");

                FeatureControlFrameStyle target = copy ?? frame;
                UnitsTypeEnum linear = units == PrintUnits.Metric ? UnitsTypeEnum.kMillimeterLengthUnits : UnitsTypeEnum.kInchLengthUnits;
                ComSafe.Run(() => target.LinearUnits = linear, _warnings, $"Setting the linear units of feature control frame style '{copyName}'");
                ComSafe.Run(() => target.DecimalMarkerType = period, _warnings, $"Setting the decimal marker of feature control frame style '{copyName}'");
                if (ComSafe.Get(() => (DecimalMarkerTypeEnum?)target.DecimalMarkerType) != period)
                    notPeriod++;
                done.Add(copy != null ? copyName : frameName);

                if (copy != null)
                {
                    FeatureControlFrameStyle assign = copy;
                    ComSafe.Run(() => defaults.FeatureControlFrameStyle = assign, _warnings, $"Pointing the object defaults at feature control frame style '{copyName}'");
                }
            }

            _log.Info($"Build Print Template: period decimal marker set on {done.Count} style(s): {string.Join(", ", done)}; {notPeriod} did not read back as a period.");
            if (notPeriod > 0)
                _warnings.Add($"{notPeriod} style(s) did not read back with a period decimal marker.");
        }

        /// <summary>
        /// Copies the parts list and revision table styles, adds the Make/Buy, manufacturer and manufacturer part number columns to the
        /// parts list, and makes the copies the standard's defaults through a copied object defaults style (spike 183, round 3).
        /// The revision table keeps Inventor's columns (follow-up).
        /// </summary>
        private void ConfigureTableStyles(DrawingStandardStyle standard)
        {
            ObjectDefaultsStyle defaults = standard.ActiveObjectDefaults;
            var partsList = (PartsListStyle)defaults.PartsListStyle.Copy("AWB Parts List");
            var revisionTable = (RevisionTableStyle)defaults.RevisionTableStyle.Copy("AWB Revision Table");

            HorizontalTextAlignmentEnum left = HorizontalTextAlignmentEnum.kAlignTextLeft;
            foreach (TableColumn column in PrintTemplateLayout.PartsListColumns)
            {
                double widthCm = Cm(column.WidthMm);
                switch (column.Source)
                {
                    case "Make/Buy":
                        AddCustomColumn(partsList, column, widthCm, left);
                        break;
                    case "Vendor":
                        partsList.AddColumn(PropertyTypeEnum.kFileProperty, DesignTracking, 30, column.Title, widthCm, left, left);
                        break;
                    case "Stock Number":
                        partsList.AddColumn(PropertyTypeEnum.kFileProperty, DesignTracking, 55, column.Title, widthCm, left, left);
                        break;
                    // Item, Quantity, Part Number and Description are the style's own columns.
                }
            }

            var defaultsCopy = (ObjectDefaultsStyle)defaults.Copy("AWB Object Defaults");
            defaultsCopy.PartsListStyle = partsList;
            defaultsCopy.RevisionTableStyle = revisionTable;
            standard.ActiveObjectDefaults = defaultsCopy;
        }

        /// <summary>
        /// The Make/Buy column reads a custom property by name. Spike 183 passed "no property set" (<c>Missing</c>); a typed call cannot,
        /// so an empty string is tried first, then the user-defined property set's id.
        /// </summary>
        private void AddCustomColumn(PartsListStyle style, TableColumn column, double widthCm, HorizontalTextAlignmentEnum alignment)
        {
            try
            {
                style.AddColumn(PropertyTypeEnum.kCustomProperty, string.Empty, column.Source, column.Title, widthCm, alignment, alignment);
            }
            catch (Exception first)
            {
                _warnings.Add($"The {column.Title} column with an empty property set was refused ({first.Message}); tried the user-defined set's id.");
                style.AddColumn(PropertyTypeEnum.kCustomProperty, UserDefined, column.Source, column.Title, widthCm, alignment, alignment);
            }
        }

        // ---- Title block

        private TitleBlockDefinition DrawTitleBlock(DrawingDocument doc, PrintTemplateLayout layout)
        {
            TitleBlockDefinition def = doc.TitleBlockDefinitions.Add(TitleBlockName);
            DrawingSketch sketch = EditSketch(def);
            try
            {
                var drawn = new HashSet<string>(StringComparer.Ordinal);

                // Cell edges first, then the layout's own lines, so the heavier outline is drawn last. A cell edge on the block's outline is
                // left to the outline; an edge two cells share is drawn once.
                foreach (TitleBlockCell cell in layout.Cells)
                {
                    DrawCellEdge(sketch, drawn, cell.XMm, cell.YMm, cell.RightMm, cell.YMm);
                    DrawCellEdge(sketch, drawn, cell.RightMm, cell.YMm, cell.RightMm, cell.TopMm);
                    DrawCellEdge(sketch, drawn, cell.RightMm, cell.TopMm, cell.XMm, cell.TopMm);
                    DrawCellEdge(sketch, drawn, cell.XMm, cell.TopMm, cell.XMm, cell.YMm);
                }

                foreach (TitleBlockLine line in layout.Lines)
                    AddLine(sketch, line.X1Mm, line.Y1Mm, line.X2Mm, line.Y2Mm, line.WeightMm);

                foreach (TitleBlockCell cell in layout.Cells.Where(c => c.Field != null))
                    DrawFieldCell(sketch, layout, cell);

                foreach (TitleBlockText text in layout.Texts)
                {
                    TitleBlockCell? home = layout.Cells.FirstOrDefault(c => c.Name == text.CellName);
                    AddText(sketch, EscapeText(text.Text), text.SizeMm, text.Bold, text.XMm, text.BaselineYMm,
                        text.Anchor == TextAnchor.Centre, false, $"text '{text.Text}'", home, text.Text);
                }

                DrawProjectionSymbol(sketch, layout.Symbol);
                DrawLogo(sketch, layout.Logo);
                DrawSmallPartNumber(sketch, layout);

                def.ExitEdit(true, Type.Missing);

                if (_widthRatios.Count > 0)
                {
                    _log.Info(
                        $"Build Print Template: drawn width over PrintTextFit width (about 1 means FontSize is the cap height as sized) for {_widthRatios.Count} plain text(s): " +
                        $"min {_widthRatios.Min():0.00}, mean {_widthRatios.Average():0.00}, max {_widthRatios.Max():0.00}.");
                }
            }
            catch (Exception)
            {
                try { def.ExitEdit(false, Type.Missing); }
                catch (Exception exitEx) { _log.Error("Build Print Template: leaving the title block editor failed.", exitEx); }
                throw;
            }

            return def;
        }

        private void DrawCellEdge(DrawingSketch sketch, HashSet<string> drawn, double x1, double y1, double x2, double y2)
        {
            const double tolerance = 1e-6;
            bool onOutline =
                (Math.Abs(y1) < tolerance && Math.Abs(y2) < tolerance)
                || (Math.Abs(y1 - PrintTemplateLayout.TitleBlockHeightMm) < tolerance && Math.Abs(y2 - PrintTemplateLayout.TitleBlockHeightMm) < tolerance)
                || (Math.Abs(x1) < tolerance && Math.Abs(x2) < tolerance)
                || (Math.Abs(x1 - PrintTemplateLayout.TitleBlockWidthMm) < tolerance && Math.Abs(x2 - PrintTemplateLayout.TitleBlockWidthMm) < tolerance);
            if (onOutline)
                return;

            // The same edge from either end is one key.
            string a = Key(x1, y1), b = Key(x2, y2);
            string key = string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;
            if (!drawn.Add(key))
                return;

            AddLine(sketch, x1, y1, x2, y2, PrintTemplateLayout.CellLineWeightMm);
        }

        private static string Key(double x, double y) =>
            Math.Round(x, 3).ToString("0.###", CultureInfo.InvariantCulture) + "," + Math.Round(y, 3).ToString("0.###", CultureInfo.InvariantCulture);

        private void DrawFieldCell(DrawingSketch sketch, PrintTemplateLayout layout, TitleBlockCell cell)
        {
            if (cell.Label != null)
            {
                string label = cell.Inline ? cell.Label + ":" : cell.Label;
                AddText(sketch, EscapeText(label), PrintTemplateLayout.LabelSizeMm, false, cell.LabelXMm, cell.LabelBaselineYMm,
                    false, false, $"label '{label}'", cell, label);
            }

            bool centre = cell.ValueAnchor == TextAnchor.Centre;
            // The placeholder is what the definition shows and measures; a centred value is a short one (a revision letter), so a short
            // placeholder keeps the centring close to the real value's.
            string placeholder = centre ? "A" : (cell.Label ?? cell.Name);
            string content = FieldContent(cell.Field!.Value, layout, placeholder);
            // The range box of a field's text measures the property's name (REV: "REVISION NUMBER", 80 mm; SHEET: "Sheet Number OF
            // Number of sheets"), not what prints, so it says nothing about the cell (task 223). The cell is checked against a typical
            // value in Core instead.
            AddText(sketch, content, cell.ValueSizeMm, cell.Bold, cell.ValueXMm, cell.ValueBaselineYMm, centre, false, $"value of '{cell.Name}'");
            double over = PrintTemplateLayout.SampleOverflowMm(cell);
            if (over > 0.1)
                _warnings.Add($"A typical value ('{PrintTemplateLayout.SampleValue(cell.Field!.Value)}') of '{cell.Name}' extends past its cell by {over:0.##} mm.");
        }

        /// <summary>
        /// The formatted text inside a title block text for a layout field: the property field syntax spike 183 confirmed, or plain text
        /// for the Units cell. Built-in properties carry their id; custom ones match by name and carry none (round 2).
        /// </summary>
        private static string FieldContent(PrintField field, PrintTemplateLayout layout, string placeholder)
        {
            switch (field)
            {
                case PrintField.PartNumber: return Property("model", DesignTrackingSet, DesignTracking, "Part Number", 5, placeholder);
                case PrintField.Description: return Property("model", DesignTrackingSet, DesignTracking, "Description", 29, placeholder);
                case PrintField.Revision: return Property("model", SummarySet, SummaryInfo, "Revision Number", 9, placeholder);
                case PrintField.Material: return Property("model", DesignTrackingSet, DesignTracking, "Material", 20, placeholder);
                case PrintField.Designer: return Property("model", DesignTrackingSet, DesignTracking, "Designer", 41, placeholder);
                case PrintField.Project: return Property("model", DesignTrackingSet, DesignTracking, "Project", 7, placeholder);
                case PrintField.Status: return Property("model", DesignTrackingSet, DesignTracking, "User Status", 17, placeholder);
                case PrintField.MaterialSize: return CustomProperty("Material Size", placeholder);
                case PrintField.FinishSpec: return CustomProperty("Finish Spec", placeholder);
                case PrintField.DrawingAuthor: return Property("drawing", SummarySet, SummaryInfo, "Author", 4, placeholder);
                case PrintField.DrawingCreationDate: return Property("drawing", DesignTrackingSet, DesignTracking, "Creation Time", 4, placeholder);
                // Task 222: the model's custom "Mass" text ("4.33 kg" / "9.55 lb"), not the live mass, which prints "lbmass" (spike 225).
                case PrintField.Mass: return CustomProperty("Mass", placeholder);
                case PrintField.Scale: return Derived(29707, placeholder);
                case PrintField.SheetNumberOfCount: return Derived(29704, "SHEET NUMBER") + " OF " + Derived(29703, "SHEET COUNT");
                case PrintField.SheetSize: return Derived(29705, placeholder);
                case PrintField.FileName: return Derived(29697, placeholder);
                case PrintField.Units: return EscapeText(layout.UnitsText);
                default: throw new ArgumentOutOfRangeException(nameof(field), field, "A print field the builder has no property for.");
            }
        }

        private static string Property(string document, string set, string formatId, string name, int id, string label) =>
            $"<Property Document='{document}' PropertySet='{set}' Property='{name}' FormatID='{formatId}' PropertyID='{id.ToString(CultureInfo.InvariantCulture)}'>{EscapeText(label)}</Property>";

        private static string CustomProperty(string name, string label) =>
            $"<Property Document='model' PropertySet='{UserDefinedSet}' Property='{name}' FormatID='{UserDefined}'>{EscapeText(label)}</Property>";

        private static string Derived(int id, string label) =>
            $"<DerivedProperty DerivedID='{id.ToString(CultureInfo.InvariantCulture)}'>{EscapeText(label)}</DerivedProperty>";

        private static string EscapeText(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        /// <summary>
        /// A text whose lower-left corner (or lower centre, when <paramref name="centre"/>) is at the point. <c>AddFitted</c>'s point depends
        /// on the box's justification and there is no baseline option, so, as the hole table does, the box is justified (left or centre,
        /// lower) and then moved by what its range box says, so the bottom of its box sits at <paramref name="yMm"/>. For a text rotated
        /// to read upward (<paramref name="rotated"/>) the box's right edge is put at <paramref name="xMm"/>. Font, height and bold are
        /// formatting overrides in the text (spike 183, round 1: bold runs; Inventor's own title block uses the size override, in cm).
        /// </summary>
        private TextBox AddText(
            DrawingSketch sketch, string content, double sizeMm, bool bold, double xMm, double yMm, bool centre, bool rotated, string what,
            TitleBlockCell? bounds = null, string? exactText = null)
        {
            // The layout's size is the em (the mockup's font-size); Inventor's FontSize override is the cap height (task 215), so convert.
            double fontSizeCm = Cm(sizeMm * PrintTextFit.CapHeightEm);
            string formatted =
                $"<StyleOverride Font='{PrintTemplateLayout.FontName}' FontSize='{fontSizeCm.ToString("0.####", CultureInfo.InvariantCulture)}'" +
                (bold ? " Bold='True'" : string.Empty) + $">{content}</StyleOverride>";

            TextBox box = sketch.TextBoxes.AddFitted(P(xMm, yMm), formatted, Type.Missing);
            ComSafe.Run(() =>
            {
                box.HorizontalJustification = centre ? HorizontalTextAlignmentEnum.kAlignTextCenter : HorizontalTextAlignmentEnum.kAlignTextLeft;
                box.VerticalJustification = VerticalTextAlignmentEnum.kAlignTextLower;
                if (rotated)
                    box.Rotation = Math.PI / 2;

                Box2d range = box.RangeBox;
                Point2d origin = box.Origin;
                double x = Cm(xMm), y = Cm(yMm);
                double dx = centre ? x - (range.MinPoint.X + range.MaxPoint.X) / 2 : rotated ? x - range.MaxPoint.X : x - range.MinPoint.X;
                double dy = y - range.MinPoint.Y;
                box.Origin = _geometry.CreatePoint2d(origin.X + dx, origin.Y + dy);
            }, _warnings, $"Placing {what}");

            if (!rotated)
                MeasureText(box, what, sizeMm, bold, bounds, exactText);
            return box;
        }

        /// <summary>
        /// Reads the drawn text's range box back: warns when it reaches past its cell (the cell's own bounds, in the title block's
        /// coordinates), and for text whose content is plain (labels, fixed text) records the width against <see cref="PrintTextFit.WidthMm"/>
        /// so the log shows whether FontSize really is the cap height (ratio near 1).
        /// </summary>
        private void MeasureText(TextBox box, string what, double sizeMm, bool bold, TitleBlockCell? cell, string? exactText)
        {
            Box2d? range = ComSafe.Get(() => box.RangeBox);
            if (range == null)
                return;

            double minX = range.MinPoint.X * 10, maxX = range.MaxPoint.X * 10, minY = range.MinPoint.Y * 10, maxY = range.MaxPoint.Y * 10;

            if (exactText != null && exactText.Length >= 4)
            {
                double estimate = PrintTextFit.WidthMm(exactText, sizeMm, bold);
                if (estimate > 0)
                    _widthRatios.Add((maxX - minX) / estimate);
            }

            if (cell == null)
                return;

            const double tolerance = 0.1;
            var past = new List<string>();
            if (minX < cell.XMm - tolerance) past.Add($"left by {cell.XMm - minX:0.##}");
            if (maxX > cell.RightMm + tolerance) past.Add($"right by {maxX - cell.RightMm:0.##}");
            if (minY < cell.YMm - tolerance) past.Add($"bottom by {cell.YMm - minY:0.##}");
            if (maxY > cell.TopMm + tolerance) past.Add($"top by {maxY - cell.TopMm:0.##}");
            if (past.Count > 0)
            {
                _warnings.Add($"The {what} extends past its cell '{cell.Name}' {string.Join(", ", past)} mm.");
            }
        }

        private SketchLine AddLine(DrawingSketch sketch, double x1Mm, double y1Mm, double x2Mm, double y2Mm, double weightMm)
        {
            SketchLine line = sketch.SketchLines.AddByTwoPoints(P(x1Mm, y1Mm), P(x2Mm, y2Mm));
            ComSafe.Run(() => line.LineWeight = Cm(weightMm), _warnings, "Line weight");
            return line;
        }

        private void DrawProjectionSymbol(DrawingSketch sketch, ProjectionSymbol symbol)
        {
            const double weight = PrintTemplateLayout.CellLineWeightMm;
            for (int i = 0; i < symbol.Frustum.Count; i++)
            {
                var from = symbol.Frustum[i];
                var to = symbol.Frustum[(i + 1) % symbol.Frustum.Count];
                AddLine(sketch, from.X, from.Y, to.X, to.Y, weight);
            }

            foreach (double radiusMm in new[] { symbol.OuterRadiusMm, symbol.InnerRadiusMm })
            {
                SketchCircle circle = sketch.SketchCircles.AddByCenterRadius(P(symbol.CircleCentre.X, symbol.CircleCentre.Y), Cm(radiusMm));
                ComSafe.Run(() => circle.LineWeight = Cm(weight), _warnings, "Circle line weight");
            }

            foreach (TitleBlockLine centre in symbol.CentreLines)
            {
                SketchLine line = AddLine(sketch, centre.X1Mm, centre.Y1Mm, centre.X2Mm, centre.Y2Mm, centre.WeightMm);
                ComSafe.Run(() => line.LineType = LineTypeEnum.kChainLineType, _warnings, "Centre line type");
            }
        }

        /// <summary>The logo embedded (not linked), top-left at the layout's point; width then height, as spike 183 round 2 found.</summary>
        private void DrawLogo(DrawingSketch sketch, TitleBlockLogo logo)
        {
            Point2d topLeft = P(logo.XMm, logo.YMm + logo.HeightMm);
            SketchImage image = sketch.SketchImages.Add(_logoPath, topLeft, false);
            image.Width = Cm(logo.WidthMm);
            image.Height = Cm(logo.HeightMm);
            ComSafe.Run(() => image.Position = topLeft, _warnings, "Logo position");
            if (ComSafe.Get(() => image.LinkedToFile) == true)
                _warnings.Add("The logo is linked to its file, not embedded.");
        }

        /// <summary>The part number again, small and rotated, in the right margin level with the border's bottom (same on every size of one set).</summary>
        private void DrawSmallPartNumber(DrawingSketch sketch, PrintTemplateLayout layout)
        {
            SheetSizeSpec first = layout.SheetSizes[0];
            double x = first.SmallPartNumberMm.X - first.TitleBlockOriginMm.X;
            double y = first.SmallPartNumberMm.Y - first.TitleBlockOriginMm.Y;
            foreach (SheetSizeSpec spec in layout.SheetSizes)
            {
                double sx = spec.SmallPartNumberMm.X - spec.TitleBlockOriginMm.X;
                double sy = spec.SmallPartNumberMm.Y - spec.TitleBlockOriginMm.Y;
                if (Math.Abs(sx - x) > 1e-6 || Math.Abs(sy - y) > 1e-6)
                    throw new InvalidOperationException($"The small part number sits differently on sheet {spec.Name}; one title block cannot serve every size.");
            }

            TitleBlockMarginField field = layout.SmallPartNumber;
            if (Math.Abs(field.RotationDegrees - 90) > 1e-9)
                throw new InvalidOperationException("The builder only draws the small part number rotated 90 degrees.");
            AddText(sketch, FieldContent(field.Field, layout, "PART NUMBER"), field.SizeMm, false, x, y, false, true, "the small part number");
        }

        // ---- Triad

        /// <summary>
        /// The X/Y triad as a sketched symbol: axes, arrowheads and the two labels, all from <see cref="PrintTemplateLayout.Triad"/>. The
        /// sketch's (0, 0) is the symbol's insertion point (as the hole table symbols do), so placing it at
        /// <see cref="SheetSizeSpec.TriadOriginMm"/> puts the triad's origin exactly there. The extent actually drawn is logged.
        /// </summary>
        private SketchedSymbolDefinition DrawTriad(DrawingDocument doc)
        {
            TriadSpec t = PrintTemplateLayout.Triad;
            SketchedSymbolDefinition def = doc.SketchedSymbolDefinitions.Add(TriadName);
            DrawingSketch sketch;
            def.Edit(out sketch);

            double maxX = 0, maxY = 0;
            try
            {
                SketchPoint insertion = sketch.SketchPoints.Add(P(0, 0), false);
                insertion.InsertionPoint = true;

                double w = t.AxisWeightMm;

                // X axis: shaft to AxisLength, arrowhead from there to ArrowLength.
                AddLine(sketch, 0, 0, t.AxisLengthMm, 0, w);
                AddLine(sketch, t.AxisLengthMm, t.ArrowHalfWidthMm, t.ArrowLengthMm, 0, w);
                AddLine(sketch, t.ArrowLengthMm, 0, t.AxisLengthMm, -t.ArrowHalfWidthMm, w);
                AddLine(sketch, t.AxisLengthMm, -t.ArrowHalfWidthMm, t.AxisLengthMm, t.ArrowHalfWidthMm, w);

                // Y axis.
                AddLine(sketch, 0, 0, 0, t.AxisLengthMm, w);
                AddLine(sketch, t.ArrowHalfWidthMm, t.AxisLengthMm, 0, t.ArrowLengthMm, w);
                AddLine(sketch, 0, t.ArrowLengthMm, -t.ArrowHalfWidthMm, t.AxisLengthMm, w);
                AddLine(sketch, -t.ArrowHalfWidthMm, t.AxisLengthMm, t.ArrowHalfWidthMm, t.AxisLengthMm, w);

                // Labels: X beyond the x axis's end, Y above the y axis's end.
                TextBox? x = AddText(sketch, EscapeText(t.XLabel), t.LabelSizeMm, false, t.XLabelMm.X, t.XLabelMm.Y, false, false, "triad label X");
                TextBox? y = AddText(sketch, EscapeText(t.YLabel), t.LabelSizeMm, false, t.YLabelMm.X, t.YLabelMm.Y, false, false, "triad label Y");
                maxX = (ComSafe.Get(() => (double?)x!.RangeBox.MaxPoint.X) ?? 0) * 10;
                maxY = (ComSafe.Get(() => (double?)y!.RangeBox.MaxPoint.Y) ?? 0) * 10;

                def.ExitEdit(true);
            }
            catch (Exception)
            {
                try { def.ExitEdit(false); }
                catch (Exception exitEx) { _log.Error("Build Print Template: leaving the triad editor failed.", exitEx); }
                throw;
            }

            _log.Info(
                $"Build Print Template: the triad's labels reach x {maxX:0.0} mm and y {maxY:0.0} mm from its origin " +
                $"(the layout allows {PrintTemplateLayout.TriadExtentMm} mm).");
            return def;
        }

        // ---- Sheet formats

        /// <summary>
        /// One sheet format per size, all made on the template's one sheet: clear it, set size and orientation, add the default border
        /// with the layout's zones and margins, the title block and the triad, then record the format (spike 183, round 2). The default
        /// size is made last, so the saved template's own sheet is the one Create Print will most often want.
        /// </summary>
        private void MakeSheetFormats(
            DrawingDocument doc, Sheet sheet, PrintTemplateLayout layout, TitleBlockDefinition titleBlock,
            SketchedSymbolDefinition triad)
        {
            foreach (SheetSizeSpec spec in layout.SheetSizes.OrderBy(s => s.Name == layout.DefaultSheetName ? 1 : 0))
            {
                string formatName = spec.FormatName;
                Step = $"making the sheet format '{formatName}'";

                ClearSheet(sheet);
                sheet.Size = SheetSizeFor(spec);
                sheet.Orientation = spec.IsPortrait ? PageOrientationTypeEnum.kPortraitPageOrientation : PageOrientationTypeEnum.kLandscapePageOrientation;
                double widthMm = sheet.Width * 10, heightMm = sheet.Height * 10;
                if (Math.Abs(widthMm - spec.WidthMm) > 1 || Math.Abs(heightMm - spec.HeightMm) > 1)
                    _warnings.Add($"{formatName}: the sheet is {widthMm:0.#} x {heightMm:0.#} mm, the layout says {spec.WidthMm} x {spec.HeightMm} mm.");

                // Spike 183: numbers left to right, letters from the top, lines between zones, centring arrows. The margins are in cm.
                DefaultBorder border = sheet.AddDefaultBorder(
                    spec.ZonesAcross, BorderLabelModeEnum.kBorderLabelModeNumeric,
                    spec.ZonesDown, BorderLabelModeEnum.kBorderLabelModeAlphabetical,
                    false, true, true,
                    Cm(spec.OtherMarginMm), Cm(spec.OtherMarginMm), Cm(spec.FilingMarginMm), Cm(spec.OtherMarginMm),
                    Type.Missing, Type.Missing, Type.Missing);
                CheckBorder(formatName, border, spec);

                TitleBlock placed = sheet.AddTitleBlock(titleBlock, TitleBlockLocationEnum.kBottomRightPosition, Type.Missing);
                Box2d? range = ComSafe.Get(() => placed.RangeBox);
                if (range != null
                    && (Math.Abs(range.MinPoint.X * 10 - spec.TitleBlockOriginMm.X) > 0.5 || Math.Abs(range.MinPoint.Y * 10 - spec.TitleBlockOriginMm.Y) > 0.5))
                {
                    _warnings.Add(
                        $"{formatName}: the title block's bottom-left is at ({range.MinPoint.X * 10:0.##}, {range.MinPoint.Y * 10:0.##}) mm, " +
                        $"the layout says ({spec.TitleBlockOriginMm.X:0.##}, {spec.TitleBlockOriginMm.Y:0.##}) mm.");
                }

                // The triad's origin is given from the border's bottom-left corner; the symbol has its insertion point at that origin.
                var origin = spec.TriadOriginMm;
                double xMm = spec.BorderLeftMm + origin.X;
                double yMm = spec.BorderBottomMm + origin.Y;
                SketchedSymbol symbol = sheet.SketchedSymbols.Add(triad, P(xMm, yMm), 0, 1, Type.Missing);
                Point2d? placedAt = ComSafe.Get(() => symbol.Position);
                _log.Info(
                    $"Build Print Template: {formatName}: triad symbol position asked ({xMm:0.#}, {yMm:0.#}) mm, read back " +
                    (placedAt == null ? "nothing" : $"({placedAt.X * 10:0.#}, {placedAt.Y * 10:0.#}) mm") + ".");

                doc.SheetFormats.Add(sheet, formatName);
            }
        }

        private void CheckBorder(string formatName, DefaultBorder border, SheetSizeSpec spec)
        {
            double top = (ComSafe.Get(() => (double?)border.TopMargin) * 10) ?? double.NaN;
            double bottom = (ComSafe.Get(() => (double?)border.BottomMargin) * 10) ?? double.NaN;
            double left = (ComSafe.Get(() => (double?)border.LeftMargin) * 10) ?? double.NaN;
            double right = (ComSafe.Get(() => (double?)border.RightMargin) * 10) ?? double.NaN;
            if (!Near(top, spec.OtherMarginMm) || !Near(bottom, spec.OtherMarginMm) || !Near(left, spec.FilingMarginMm) || !Near(right, spec.OtherMarginMm))
            {
                _warnings.Add(
                    $"{formatName}: the border margins read back top {top:0.##}, bottom {bottom:0.##}, left {left:0.##}, right {right:0.##} mm; " +
                    $"the layout says {spec.OtherMarginMm}, {spec.OtherMarginMm}, {spec.FilingMarginMm}, {spec.OtherMarginMm} mm.");
            }

            int? across = ComSafe.Get(() => (int?)border.HorizontalZoneCount);
            int? down = ComSafe.Get(() => (int?)border.VerticalZoneCount);
            if (across != spec.ZonesAcross || down != spec.ZonesDown)
                _warnings.Add($"{formatName}: the border has {across} x {down} zones; the layout says {spec.ZonesAcross} x {spec.ZonesDown}.");
        }

        private static bool Near(double a, double b) => Math.Abs(a - b) <= 0.05;

        /// <summary>Deletes the sheet's symbols, title block and border (in that order, as spike 183 did).</summary>
        private static void ClearSheet(Sheet sheet)
        {
            foreach (SketchedSymbol symbol in sheet.SketchedSymbols.Cast<SketchedSymbol>().ToList())
                symbol.Delete();

            TitleBlock? titleBlock = ComSafe.Get(() => sheet.TitleBlock);
            titleBlock?.Delete();

            Border? border = ComSafe.Get(() => sheet.Border);
            border?.Delete();
        }

        private static DrawingSheetSizeEnum SheetSizeFor(SheetSizeSpec spec) => (spec.Units, spec.Name) switch
        {
            (PrintUnits.Metric, "A4") => DrawingSheetSizeEnum.kA4DrawingSheetSize,
            (PrintUnits.Metric, "A3") => DrawingSheetSizeEnum.kA3DrawingSheetSize,
            (PrintUnits.Metric, "A2") => DrawingSheetSizeEnum.kA2DrawingSheetSize,
            (PrintUnits.Metric, "A1") => DrawingSheetSizeEnum.kA1DrawingSheetSize,
            (PrintUnits.Metric, "A0") => DrawingSheetSizeEnum.kA0DrawingSheetSize,
            (PrintUnits.Imperial, "A") => DrawingSheetSizeEnum.kADrawingSheetSize,
            (PrintUnits.Imperial, "B") => DrawingSheetSizeEnum.kBDrawingSheetSize,
            (PrintUnits.Imperial, "C") => DrawingSheetSizeEnum.kCDrawingSheetSize,
            (PrintUnits.Imperial, "D") => DrawingSheetSizeEnum.kDDrawingSheetSize,
            (PrintUnits.Imperial, "E") => DrawingSheetSizeEnum.kEDrawingSheetSize,
            _ => throw new ArgumentOutOfRangeException(nameof(spec), spec.Name, "A sheet size the builder has no Inventor size for."),
        };

        // ---- Inventor helpers

        /// <summary>
        /// Starts editing a title block definition and returns its sketch. <c>Edit</c> takes the sketch by reference; the typed call is tried
        /// first (the hole table code uses it on sketched symbols), then the by-ref <c>IDispatch</c> call spike 160 and 183 used.
        /// </summary>
        private DrawingSketch EditSketch(TitleBlockDefinition def)
        {
            try
            {
                DrawingSketch sketch;
                def.Edit(out sketch);
                return sketch;
            }
            catch (Exception typed)
            {
                _warnings.Add($"The typed TitleBlockDefinition.Edit failed ({typed.Message}); used the IDispatch call.");
                object?[] args = { new DispatchWrapper(null) };
                var modifier = new ParameterModifier(1);
                modifier[0] = true;
                def.GetType().InvokeMember("Edit", BindingFlags.InvokeMethod, null, def, args, new[] { modifier }, null, null);
                return (DrawingSketch)args[0]!;
            }
        }

        /// <summary>A point on the sheet or in a sketch, from millimetres.</summary>
        private Point2d P(double xMm, double yMm) => _geometry.CreatePoint2d(Cm(xMm), Cm(yMm));

        private static double Cm(double mm) => mm / 10;
    }
}
