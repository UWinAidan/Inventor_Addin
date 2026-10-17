using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Materials;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// A material library opened by path: its materials as Core reads them, and the Inventor assets behind them so a
    /// choice can be applied. <see cref="Library"/> is null when the library could not be opened; <see cref="Error"/>
    /// then says why.
    /// </summary>
    public sealed class MaterialLibrary
    {
        private readonly IReadOnlyList<Asset> _assets;

        internal MaterialLibrary(
            LibraryPath path, AssetLibrary? library, IReadOnlyList<LibraryMaterial> materials, IReadOnlyList<Asset> assets, string? error)
        {
            Path = path;
            Library = library;
            Materials = materials;
            _assets = assets;
            Error = error;
        }

        /// <summary>The file that was opened, and whether it is the starter library.</summary>
        public LibraryPath Path { get; }

        /// <summary>The opened library, or null when it could not be opened.</summary>
        public AssetLibrary? Library { get; }

        /// <summary>The library's materials, in the library's order; empty when it could not be opened.</summary>
        public IReadOnlyList<LibraryMaterial> Materials { get; }

        /// <summary>Why the library cannot be used (missing, not opened, or empty); null when it can.</summary>
        public string? Error { get; }

        /// <summary>
        /// The library asset of the material with this display name (ignoring case and surrounding spaces), or null when
        /// the library has none. This is what <c>PartDocument.ActiveMaterial</c> is set to (spike 067).
        /// </summary>
        public Asset? FindMaterial(string? displayName)
        {
            int index = MaterialFinishTargeting.IndexOfMaterial(displayName, Materials);
            return index < 0 ? null : _assets[index];
        }
    }

    /// <summary>
    /// Opens a material library by path (spec 03, "Add-in side"; spike 067: <c>AssetLibraries.Open</c>, about 1 s from
    /// the NAS, and opening a path already open returns the same library) and reads its materials.
    /// </summary>
    public static class MaterialLibraryReader
    {
        /// <summary>
        /// Opens <paramref name="path"/> and reads every material's display name, category and appearance name through
        /// <see cref="ComSafe"/> (spike 067: one material's appearance throws, and a category can be empty). Never throws
        /// for a missing or unreadable file: the result's <see cref="MaterialLibrary.Error"/> names the path and whether it
        /// is the starter library or the one set in Settings. A material whose display name cannot be read is left out
        /// and recorded in <paramref name="warnings"/>.
        /// </summary>
        public static MaterialLibrary Open(Application app, LibraryPath path, List<string> warnings)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var materials = new List<LibraryMaterial>();
            var assets = new List<Asset>();

            bool exists;
            try
            {
                exists = System.IO.File.Exists(path.Path);
            }
            catch (Exception)
            {
                exists = false;
            }

            if (!exists)
                return Failed(path, MaterialLibraryMessages.Missing(path));

            AssetLibrary library;
            try
            {
                library = app.AssetLibraries.Open(path.Path);
            }
            catch (Exception ex)
            {
                AddinServices.Log.Error($"Could not open the material library {path.Path}.", ex);
                return Failed(path, MaterialLibraryMessages.OpenFailed(path, ex.Message));
            }

            if (library == null)
                return Failed(path, MaterialLibraryMessages.OpenFailed(path, null));

            ComSafe.Run(() =>
            {
                int position = 0;
                foreach (Asset asset in library.MaterialAssets)
                {
                    position++;
                    string? name = ComSafe.Get(() => asset.DisplayName, warnings, $"Material {position} in the library: display name");
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string category = ComSafe.Get(() => asset.CategoryName) ?? string.Empty;

                    // Spike 067: reading one material's appearance throws (E_FAIL); the material is still listed.
                    string? appearance = ComSafe.Get(
                        () => ((MaterialAsset)asset).AppearanceAsset.DisplayName, warnings, $"Material '{name}': appearance");

                    materials.Add(new LibraryMaterial(name!, category, appearance));
                    assets.Add(asset);
                }
            }, warnings, "Materials in the library");

            string? error = materials.Count == 0 ? MaterialLibraryMessages.Empty(path) : null;
            return new MaterialLibrary(path, library, materials.AsReadOnly(), assets.AsReadOnly(), error);
        }

        private static MaterialLibrary Failed(LibraryPath path, string message)
        {
            AddinServices.Log.Warn(message);
            return new MaterialLibrary(path, null, Array.Empty<LibraryMaterial>(), Array.Empty<Asset>(), message);
        }
    }
}
