using System;
using System.Collections.Generic;

namespace InventorAddin
{
    /// <summary>
    /// Inventor COM properties frequently throw when a value doesn't apply
    /// (e.g. CBoreDiameter on a drilled hole, Definition on a suppressed occurrence).
    /// These helpers turn that into null and optionally record a warning.
    /// </summary>
    public static class ComSafe
    {
        public static T? Get<T>(Func<T> getter, List<string>? warnings = null, string? context = null)
        {
            try
            {
                return getter();
            }
            catch (Exception ex)
            {
                if (warnings != null && context != null)
                    warnings.Add($"{context}: {ex.Message}");
                return default;
            }
        }

        public static void Run(Action action, List<string>? warnings = null, string? context = null)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                if (warnings != null && context != null)
                    warnings.Add($"{context}: {ex.Message}");
            }
        }
    }
}
