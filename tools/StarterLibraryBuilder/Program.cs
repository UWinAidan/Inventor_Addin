// Generates libraries/StarterMaterials.adsklib from libraries/starter-materials.csv (task 071, spike 067 section 6).
// Drives its own invisible Inventor over COM. See README.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Inventor;
using InventorAddin.Core.Materials;
using IOFile = System.IO.File;
using IOPath = System.IO.Path;

namespace StarterLibraryBuilder
{
    internal static class Program
    {
        /// <summary>The library Inventor loads with its stock appearances; searched first for an appearance name.</summary>
        private const string AppearanceLibraryName = "Autodesk Appearance Library";

        /// <summary>The Autodesk Appearance Library's internal name in Inventor 2026 (spike 067).</summary>
        private const string AppearanceLibraryInternalName = "314DE259-5443-4621-BFBD-1730C6CC9AE9";

        /// <summary>Inventor's stock material library, the only source of template materials.</summary>
        private const string StockMaterialLibraryName = "Inventor Material Library";

        /// <summary>The physical property that holds the density, in kg/m³ (spike 067).</summary>
        private const string DensityValueName = "structural_Density";

        /// <summary>The "(1)" Inventor adds to a copied appearance whose name is already used in the document.</summary>
        private static readonly Regex SuffixedName = new Regex(@"\(\d+\)$");

        private static Application _app = null!;

        [STAThread]
        private static int Main(string[] args)
        {
            string? csvPath = null;
            string? outPath = null;
            string mode = "build";
            string? pattern = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--check": mode = "check"; break;
                    case "--list": mode = "list"; pattern = i + 1 < args.Length ? args[++i] : "."; break;
                    case "--csv" when i + 1 < args.Length: csvPath = args[++i]; break;
                    case "--out" when i + 1 < args.Length: outPath = args[++i]; break;
                    default:
                        Console.Error.WriteLine("Usage: StarterLibraryBuilder [--check] [--list <regex>] [--csv <file>] [--out <file>]");
                        return 2;
                }
            }

            string? repo = FindRepoRoot();
            csvPath ??= repo == null ? null : IOPath.Combine(repo, "libraries", StarterMaterialsCsv.FileName);
            outPath ??= repo == null ? null : IOPath.Combine(repo, "libraries", LibraryPaths.StarterMaterialLibraryFileName);
            if (csvPath == null || outPath == null)
            {
                Console.Error.WriteLine("Could not find the repository (the folder holding InventorAddin.slnx); pass --csv and --out.");
                return 2;
            }

            csvPath = IOPath.GetFullPath(csvPath);
            outPath = IOPath.GetFullPath(outPath);

            List<StarterMaterial> materials = new List<StarterMaterial>();
            if (mode != "list")
            {
                StarterMaterialsReadResult read = StarterMaterialsCsv.Parse(IOFile.ReadAllText(csvPath));
                foreach (StarterMaterialProblem p in read.Problems)
                    Console.Error.WriteLine($"{IOPath.GetFileName(csvPath)}: {p}");
                if (read.HasProblems)
                    return 1;
                materials.AddRange(read.Materials);
                Console.WriteLine($"Read {materials.Count} materials from {csvPath}");
            }

            Console.WriteLine("Starting an invisible Inventor...");
            _app = (Application)Activator.CreateInstance(Type.GetTypeFromProgID("Inventor.Application", true)!)!;
            try
            {
                _app.Visible = false;
                Console.WriteLine($"Inventor {_app.SoftwareVersion.DisplayVersion}");
                return mode switch
                {
                    "check" => Check(outPath, materials),
                    "list" => List(pattern!),
                    _ => Build(outPath, materials),
                };
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAILED: " + ex);
                return 1;
            }
            finally
            {
                List<Document> open = Get(() => _app.Documents.Cast<Document>().ToList()) ?? new List<Document>();
                foreach (Document d in open)
                    Try(() => d.Close(true), "close document");
                Try(() => _app.Quit(), "quit Inventor");
            }
        }

        // ---- Build ----

        private static int Build(string outPath, List<StarterMaterial> materials)
        {
            // Find the template and appearance libraries before touching the output file.
            AssetLibrary stock = StockMaterialLibrary();
            List<(AssetLibrary Library, int Rank)> appearanceLibraries = AppearanceLibraries(stock);
            Console.WriteLine($"Templates from '{stock.DisplayName}' ({stock.FullFileName})");

            if (IOFile.Exists(outPath))
                IOFile.Delete(outPath);

            AssetLibrary library = _app.AssetLibraries.Add(outPath);
            Console.WriteLine($"Created library '{library.DisplayName}' at {outPath}");

            // One scratch part for every material. A new part per material does not work: duplicates of the same base
            // material in different parts share their physical properties' name, so CopyTo(library, true) overwrites
            // the first one's density with the last one's.
            PartDocument part = (PartDocument)_app.Documents.Add(DocumentTypeEnum.kPartDocumentObject, PartTemplate(), false);

            int failed = 0;
            foreach (StarterMaterial row in materials)
            {
                string? error = AddMaterial(row, part, stock, appearanceLibraries, library);
                if (error == null)
                {
                    Console.WriteLine($"  ok      {row.Name}  [{row.Category}]  {row.DensityKgM3.ToString(CultureInfo.InvariantCulture)} kg/m3  {AppearanceLabel(row)}");
                }
                else
                {
                    failed++;
                    Console.WriteLine($"  FAILED  {row.Name}: {error}");
                }
            }

            part.Close(true);

            Console.WriteLine();
            Console.WriteLine($"{materials.Count - failed} of {materials.Count} materials written to {outPath}" +
                              $" ({library.MaterialAssets.Count} materials, {library.MaterialAssetCategories.Count} categories," +
                              $" {library.AppearanceAssets.Count} appearances in the library); {failed} failed.");
            return failed == 0 ? 0 : 1;
        }

        private static string AppearanceLabel(StarterMaterial row) =>
            row.Appearance.Length == 0 ? "(base material's appearance)" : row.Appearance;

        /// <summary>Adds one material to the library. Returns null on success, or what went wrong.</summary>
        private static string? AddMaterial(
            StarterMaterial row,
            PartDocument part,
            AssetLibrary stock,
            List<(AssetLibrary Library, int Rank)> appearanceLibraries,
            AssetLibrary library)
        {
            try
            {
                if (library.MaterialAssets.Cast<Asset>().Any(a => Same(a.DisplayName, row.Name)))
                    return "a material of that name is already in the library";

                Asset? baseMaterial = stock.MaterialAssets.Cast<Asset>().FirstOrDefault(a => Same(a.DisplayName, row.BaseMaterial));
                if (baseMaterial == null)
                    return $"base material '{row.BaseMaterial}' is not in '{stock.DisplayName}'";

                Asset? appearance = null;
                if (row.Appearance.Length > 0)
                {
                    appearance = FindAppearance(row.Appearance, appearanceLibraries, out string? why);
                    if (appearance == null)
                        return why;
                }

                // Spike 067 section 6: assign the stock material to a scratch part, duplicate the part's copy, edit it.
                part.ActiveMaterial = baseMaterial;
                MaterialAsset local = (MaterialAsset)(object)part.ActiveMaterial;
                MaterialAsset material = (MaterialAsset)local.Duplicate(row.Name);
                ((Asset)(object)material).DisplayName = row.Name;

                FloatAssetValue? density = material.PhysicalPropertiesAsset.Cast<AssetValue>()
                    .FirstOrDefault(v => v.Name == DensityValueName) as FloatAssetValue;
                if (density == null)
                    return $"base material '{row.BaseMaterial}' has no {DensityValueName} value";
                density.Value = row.DensityKgM3;

                if (appearance != null)
                    material.AppearanceAsset = appearance;

                Asset inLibrary = ((Asset)(object)material).CopyTo(library, true);

                AssetCategory? category = library.MaterialAssetCategories.Cast<AssetCategory>()
                    .FirstOrDefault(c => Same(c.DisplayName, row.Category));
                if (category == null)
                    library.MaterialAssetCategories.Add(row.Category, inLibrary);
                else
                    inLibrary.Category = category;

                if (!Same(inLibrary.CategoryName, row.Category))
                    return $"category reads '{inLibrary.CategoryName}' after setting '{row.Category}'";
                if (row.Appearance.Length > 0)
                {
                    // When the base material's own appearance has the wanted name, Duplicate gives the copy's
                    // appearance the name "<name>(1)" in the part, and the library copy keeps it. Rename it there.
                    Asset libraryAppearance = (Asset)(object)((MaterialAsset)(object)inLibrary).AppearanceAsset;
                    string read = libraryAppearance.DisplayName;
                    if (!Same(read, row.Appearance) && SuffixedName.IsMatch(read) && Same(SuffixedName.Replace(read, ""), row.Appearance))
                        libraryAppearance.DisplayName = row.Appearance;

                    string? now = Get(() => ((MaterialAsset)(object)inLibrary).AppearanceAsset.DisplayName);
                    if (!Same(now, row.Appearance))
                        return $"the library copy's appearance reads '{now}', not '{row.Appearance}'";
                }

                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ---- Appearances ----

        /// <summary>
        /// The only libraries searched for an appearance: the Autodesk Appearance Library first (rank 0), then Inventor's
        /// stock material library (rank 1). Other loaded libraries, which can be private, are never searched.
        /// </summary>
        private static List<(AssetLibrary Library, int Rank)> AppearanceLibraries(AssetLibrary stock)
        {
            List<AssetLibrary> all = _app.AssetLibraries.Cast<AssetLibrary>().ToList();
            AssetLibrary appearances =
                all.FirstOrDefault(l => string.Equals(l.InternalName, AppearanceLibraryInternalName, StringComparison.OrdinalIgnoreCase))
                ?? all.FirstOrDefault(l => l.DisplayName == AppearanceLibraryName && IsInstalledFile(l))
                ?? throw new InvalidOperationException($"Inventor has not loaded the '{AppearanceLibraryName}'.");

            return new List<(AssetLibrary, int)> { (appearances, 0), (stock, 1) };
        }

        /// <summary>
        /// Inventor's stock material library, found by display name and by its file being in Inventor's public Design Data
        /// or under Program Files. Never <c>Application.ActiveMaterialLibrary</c>, which follows the active project and can
        /// be a private library with the same file name.
        /// </summary>
        private static AssetLibrary StockMaterialLibrary()
        {
            List<AssetLibrary> stock = _app.AssetLibraries.Cast<AssetLibrary>()
                .Where(l => l.DisplayName == StockMaterialLibraryName && IsInstalledFile(l))
                .ToList();
            if (stock.Count != 1)
            {
                string loaded = string.Join("; ", _app.AssetLibraries.Cast<AssetLibrary>().Select(l => $"'{l.DisplayName}' {Get(() => l.FullFileName)}"));
                throw new InvalidOperationException(
                    $"Found {stock.Count} '{StockMaterialLibraryName}' libraries in Inventor's own folders; expected 1. Loaded: {loaded}");
            }

            return stock[0];
        }

        /// <summary>Whether the library's file is one Inventor installs: under the public Autodesk documents or Program Files.</summary>
        private static bool IsInstalledFile(AssetLibrary library)
        {
            string? file = Get(() => library.FullFileName);
            if (string.IsNullOrWhiteSpace(file))
                return false;

            string full = IOPath.GetFullPath(file);
            string[] roots =
            {
                IOPath.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonDocuments), "Autodesk"),
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles),
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonProgramFiles),
            };
            return roots.Any(r => r.Length > 0
                && full.StartsWith(r.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
        }

        private static Asset? FindAppearance(string name, List<(AssetLibrary Library, int Rank)> libraries, out string? why)
        {
            List<(Asset Asset, AppearanceCandidate Candidate)> all = libraries
                .SelectMany(l => l.Library.AppearanceAssets.Cast<Asset>()
                    .Select(a => (a, new AppearanceCandidate(l.Rank, a.DisplayName, a.Name))))
                .ToList();

            AppearanceLookupResult found = AppearanceLookup.Find(name, all.Select(c => c.Candidate));
            switch (found.Status)
            {
                case AppearanceLookupStatus.Found:
                    why = null;
                    return all.First(c => c.Candidate.LibraryRank == found.LibraryRank
                                          && string.Equals(c.Candidate.AssetName, found.AssetName, StringComparison.OrdinalIgnoreCase)).Asset;
                case AppearanceLookupStatus.Ambiguous:
                    why = $"appearance '{name}' names {found.Count} different appearances";
                    return null;
                default:
                    why = $"appearance '{name}' is not in the {AppearanceLibraryName} or the {StockMaterialLibraryName}";
                    return null;
            }
        }

        // ---- Check ----

        /// <summary>Opens the generated library and lists every material with its category, appearance and density.</summary>
        private static int Check(string outPath, List<StarterMaterial> expected)
        {
            if (!IOFile.Exists(outPath))
            {
                Console.Error.WriteLine($"{outPath} does not exist; run without --check first.");
                return 1;
            }

            AssetLibrary library = _app.AssetLibraries.Open(outPath);
            Console.WriteLine($"Opened '{library.DisplayName}' ({library.MaterialAssets.Count} materials)");

            int bad = 0;
            Dictionary<string, MaterialAsset> byName = new Dictionary<string, MaterialAsset>(StringComparer.OrdinalIgnoreCase);
            foreach (MaterialAsset m in library.MaterialAssets)
            {
                Asset a = (Asset)(object)m;
                string category = Get(() => a.CategoryName) ?? "(unreadable)";
                string appearance = Get(() => ((Asset)(object)m.AppearanceAsset).DisplayName) ?? "(unreadable)";
                double? density = Get(() => (double?)Density(m));
                Console.WriteLine($"  {a.DisplayName}  [{category}]  {density?.ToString(CultureInfo.InvariantCulture) ?? "(unreadable)"} kg/m3  {appearance}");
                byName[a.DisplayName] = m;
            }

            foreach (StarterMaterial row in expected)
            {
                if (!byName.TryGetValue(row.Name, out MaterialAsset? m))
                {
                    bad++;
                    Console.WriteLine($"  MISSING {row.Name}");
                    continue;
                }

                Asset a = (Asset)(object)m;
                List<string> wrong = new List<string>();
                if (!Same(Get(() => a.CategoryName), row.Category))
                    wrong.Add("category");
                if (Get(() => (double?)Density(m)) is not double d || Math.Abs(d - row.DensityKgM3) > 0.001)
                    wrong.Add("density");
                if (row.Appearance.Length > 0 && !Same(Get(() => ((Asset)(object)m.AppearanceAsset).DisplayName), row.Appearance))
                    wrong.Add("appearance");
                if (wrong.Count > 0)
                {
                    bad++;
                    Console.WriteLine($"  WRONG   {row.Name}: {string.Join(", ", wrong)}");
                }
            }

            // Appearance names in the library must be unique, or the add-in's lookup by name finds them ambiguous.
            foreach (IGrouping<string, Asset> same in library.AppearanceAssets.Cast<Asset>()
                         .GroupBy(a => a.DisplayName.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            {
                bad++;
                Console.WriteLine($"  DUPLICATE appearance name '{same.Key}' ({same.Count()} appearances)");
            }

            Console.WriteLine($"  {library.MaterialAssetCategories.Count} categories, {library.AppearanceAssets.Count} appearances");

            int extra = byName.Keys.Count(n => !expected.Any(r => Same(r.Name, n)));
            Console.WriteLine();
            Console.WriteLine($"Check: {expected.Count - bad} of {expected.Count} materials as in the CSV; {extra} not in the CSV.");
            return bad == 0 && extra == 0 ? 0 : 1;
        }

        private static double Density(MaterialAsset m) =>
            ((FloatAssetValue)m.PhysicalPropertiesAsset.Cast<AssetValue>().First(v => v.Name == DensityValueName)).Value;

        // ---- List ----

        /// <summary>
        /// Lists the libraries Inventor has loaded (names and files only), then the stock materials and the searched
        /// appearances whose display name matches a pattern.
        /// </summary>
        private static int List(string pattern)
        {
            Regex rx = new Regex(pattern, RegexOptions.IgnoreCase);
            Console.WriteLine("Loaded libraries:");
            foreach (AssetLibrary l in _app.AssetLibraries)
                Console.WriteLine($"  '{l.DisplayName}'  {Get(() => l.FullFileName)}");

            AssetLibrary stock = StockMaterialLibrary();
            Console.WriteLine($"Materials in '{stock.DisplayName}':");
            foreach (MaterialAsset m in stock.MaterialAssets)
            {
                Asset a = (Asset)(object)m;
                if (rx.IsMatch(a.DisplayName))
                    Console.WriteLine($"  {a.DisplayName}  [{Get(() => a.CategoryName)}]  {Get(() => (double?)Density(m))} kg/m3  {Get(() => ((Asset)(object)m.AppearanceAsset).DisplayName)}");
            }

            Console.WriteLine("Appearances:");
            foreach (var (library, rank) in AppearanceLibraries(stock).OrderBy(l => l.Rank))
                foreach (Asset a in library.AppearanceAssets)
                    if (rx.IsMatch(a.DisplayName))
                        Console.WriteLine($"  [{rank}] {library.DisplayName}: {a.DisplayName}  {{{Get(() => a.CategoryName)}}}  {a.Name}");
            return 0;
        }

        // ---- Helpers ----

        private static string PartTemplate() =>
            _app.FileManager.GetTemplateFile(
                DocumentTypeEnum.kPartDocumentObject,
                SystemOfMeasureEnum.kMetricSystemOfMeasure,
                DraftingStandardEnum.kDefault_DraftingStandard,
                Type.Missing);

        private static bool Same(string? a, string? b) =>
            string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

        private static T? Get<T>(Func<T> f)
        {
            try { return f(); }
            catch (Exception) { return default; }
        }

        private static void Try(Action a, string what)
        {
            try { a(); }
            catch (Exception ex) { Console.Error.WriteLine($"Could not {what}: {ex.Message}"); }
        }

        /// <summary>The folder holding InventorAddin.slnx, searched up from the current folder and then the exe's folder.</summary>
        private static string? FindRepoRoot()
        {
            foreach (string start in new[] { System.IO.Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (System.IO.DirectoryInfo? dir = new System.IO.DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    if (IOFile.Exists(IOPath.Combine(dir.FullName, "InventorAddin.slnx")))
                        return dir.FullName;
                }
            }

            return null;
        }
    }
}
