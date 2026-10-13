using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Materials;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Finds an Inventor appearance by display name (spec 03, "Finding an appearance by name"; spike 067 section 5).
    /// The appearances of every loaded library are read once, when the finder is built, and each lookup goes through
    /// <see cref="AppearanceLookup.Find"/> with the ranks from <see cref="AppearanceLibraries.Rank"/>: the Autodesk
    /// Appearance Library, then the other libraries Inventor loaded, then the material library the add-in opened.
    /// Build one finder per command, not per part.
    /// </summary>
    public sealed class AppearanceFinder
    {
        private readonly List<(AppearanceCandidate Candidate, Asset Asset)> _appearances;

        private AppearanceFinder(List<(AppearanceCandidate Candidate, Asset Asset)> appearances) =>
            _appearances = appearances;

        /// <summary>Every appearance read, as Core sees it.</summary>
        public IReadOnlyList<AppearanceCandidate> Candidates => _appearances.ConvertAll(a => a.Candidate);

        /// <summary>
        /// Reads the appearances of every library in <c>Application.AssetLibraries</c>, and of <paramref name="opened"/>
        /// when it is not among them. A library or appearance that cannot be read is left out and recorded in
        /// <paramref name="warnings"/>.
        /// </summary>
        /// <param name="opened">The material library the add-in opened, or null.</param>
        public static AppearanceFinder Read(Application app, AssetLibrary? opened, List<string> warnings)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            var libraries = new List<AssetLibrary>();
            var loaded = new List<LoadedLibrary>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(AssetLibrary library)
            {
                string internalName = ComSafe.Get(() => library.InternalName) ?? string.Empty;
                if (internalName.Length > 0 && !seen.Add(internalName))
                    return;

                libraries.Add(library);
                loaded.Add(new LoadedLibrary(internalName, ComSafe.Get(() => library.DisplayName) ?? string.Empty));
            }

            ComSafe.Run(() =>
            {
                foreach (AssetLibrary library in app.AssetLibraries)
                    Add(library);
            }, warnings, "Loaded asset libraries");

            if (opened != null)
                Add(opened);

            string? openedInternalName = opened == null ? null : ComSafe.Get(() => opened.InternalName);
            IReadOnlyList<int> ranks = AppearanceLibraries.Rank(loaded, openedInternalName);

            var appearances = new List<(AppearanceCandidate, Asset)>();
            for (int i = 0; i < libraries.Count; i++)
            {
                AssetLibrary library = libraries[i];
                int rank = ranks[i];
                string libraryName = loaded[i].DisplayName;

                ComSafe.Run(() =>
                {
                    foreach (Asset asset in library.AppearanceAssets)
                    {
                        string? displayName = ComSafe.Get(() => asset.DisplayName);
                        string? assetName = ComSafe.Get(() => asset.Name);
                        if (string.IsNullOrWhiteSpace(displayName) || assetName == null)
                            continue;
                        appearances.Add((new AppearanceCandidate(rank, displayName!, assetName), asset));
                    }
                }, warnings, $"Appearances in library '{libraryName}'");
            }

            return new AppearanceFinder(appearances);
        }

        /// <summary>
        /// Looks up <paramref name="name"/>. <paramref name="asset"/> is the appearance to set when the result is Found,
        /// otherwise null.
        /// </summary>
        public AppearanceLookupResult Find(string? name, out Asset? asset)
        {
            AppearanceLookupResult result = AppearanceLookup.Find(name, _appearances.ConvertAll(a => a.Candidate));
            asset = null;
            if (result.Status != AppearanceLookupStatus.Found)
                return result;

            foreach ((AppearanceCandidate candidate, Asset found) in _appearances)
            {
                if (candidate.LibraryRank == result.LibraryRank
                    && string.Equals(candidate.AssetName, result.AssetName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.DisplayName.Trim(), (name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    asset = found;
                    break;
                }
            }

            return result;
        }
    }
}
