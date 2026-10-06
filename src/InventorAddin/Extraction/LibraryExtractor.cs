using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin.Extraction
{
    /// <summary>Lists the material and appearance libraries Inventor currently has loaded.</summary>
    public static class LibraryExtractor
    {
        public static LibraryData Extract(Application app)
        {
            var data = new LibraryData
            {
                ActiveMaterialLibrary = ComSafe.Get(() => app.ActiveMaterialLibrary.DisplayName),
                ActiveAppearanceLibrary = ComSafe.Get(() => app.ActiveAppearanceLibrary.DisplayName),
            };

            foreach (AssetLibrary lib in app.AssetLibraries)
            {
                var libData = new AssetLibraryData
                {
                    DisplayName = lib.DisplayName,
                    InternalName = lib.InternalName,
                    FullFileName = ComSafe.Get(() => lib.FullFileName),
                    IsReadOnly = ComSafe.Get(() => lib.IsReadOnly),
                };

                ComSafe.Run(() =>
                {
                    foreach (Asset a in lib.MaterialAssets)
                        libData.Materials.Add(ToInfo(a, lib));
                });
                ComSafe.Run(() =>
                {
                    foreach (Asset a in lib.AppearanceAssets)
                        libData.Appearances.Add(ToInfo(a, lib));
                });

                data.Libraries.Add(libData);
            }
            return data;
        }

        private static AssetInfo ToInfo(Asset a, AssetLibrary lib) => new()
        {
            Name = a.Name,
            DisplayName = a.DisplayName,
            Category = ComSafe.Get(() => a.CategoryName),
            LibraryName = lib.DisplayName,
        };
    }
}
