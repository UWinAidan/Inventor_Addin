using System.Collections.Generic;
using InventorAddin.Core.Models;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Reads Finish features (Inventor 2023+). Late-bound because the member set
    /// has changed between releases; anything missing is simply left null.
    /// </summary>
    public static class FinishReader
    {
        /// <param name="features">PartFeatures or AssemblyFeatures.</param>
        public static List<FinishData> Read(object features, List<string> warnings)
        {
            var list = new List<FinishData>();
            ComSafe.Run(() =>
            {
                dynamic finishes = ((dynamic)features).FinishFeatures;
                foreach (dynamic f in finishes)
                {
                    list.Add(new FinishData
                    {
                        FeatureName = (string)f.Name,
                        Suppressed = ComSafe.Get(() => (bool)f.Suppressed),
                        FinishType = ComSafe.Get(() => f.FinishType?.ToString() as string),
                        ProcessName = ComSafe.Get(() => (string)f.ProcessName),
                        AppearanceName = ComSafe.Get(() => (string)f.Appearance.DisplayName),
                        Comments = ComSafe.Get(() => (string)f.Comments),
                    });
                }
            }, warnings, "Finish features");
            return list;
        }
    }
}
