using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.PartProperties;

namespace InventorAddin.Extraction
{
    /// <summary>
    /// Applies a list of <see cref="PropertyWrite"/>s (the plan from <see cref="PartPropertiesWritePlan"/>) to a
    /// document. It starts no transaction: it runs inside whatever transaction the caller started, and a failed
    /// write throws so that the caller aborts that transaction.
    /// </summary>
    public static class PropertyWriter
    {
        public static void Apply(Document doc, IReadOnlyList<PropertyWrite> writes)
        {
            foreach (PropertyWrite write in writes)
            {
                PropertyLocation location = write.Location;
                if (write.Action == PropertyWriteAction.Remove && !location.IsCustom)
                {
                    throw new ArgumentException(
                        $"'{location.PropertyName}' is a standard property and cannot be removed.", nameof(writes));
                }

                // Log the action and location only. PropertyWrite.ToString() carries the value, which can be
                // personal data (a designer's name), so it is never logged.
                AddinServices.Log.Info($"Property write: {write.Action} {location.SetName}/{location.PropertyName}");

                try
                {
                    ApplyOne(doc, write);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(FailureMessage(write), ex);
                }
            }
        }

        private static void ApplyOne(Document doc, PropertyWrite write)
        {
            PropertyLocation location = write.Location;
            PropertySet set = doc.PropertySets[location.SetName];

            switch (write.Action)
            {
                case PropertyWriteAction.Set:
                {
                    object value = ValueOf(write);
                    if (!location.IsCustom)
                    {
                        set[location.PropertyName].Value = value;
                        return;
                    }

                    // The indexer throws when the property does not exist; that is the "not there" answer.
                    Property? existing = ComSafe.Get(() => set[location.PropertyName]);
                    if (existing != null)
                        existing.Value = value;
                    else
                        set.Add(value, location.PropertyName);
                    return;
                }

                case PropertyWriteAction.Remove:
                {
                    Property? existing = ComSafe.Get(() => set[location.PropertyName]);
                    existing?.Delete();
                    return;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(write), write.Action, "Unknown property write action.");
            }
        }

        /// <summary>The value to hand to Inventor: a string for Text, a decimal for Currency.</summary>
        private static object ValueOf(PropertyWrite write) => write.Location.Kind switch
        {
            PropertyValueKind.Text => (object)(write.TextValue
                ?? throw new ArgumentException($"No text value for '{write.Location.PropertyName}'.", nameof(write))),
            PropertyValueKind.Currency => (object)(write.CurrencyValue
                ?? throw new ArgumentException($"No currency value for '{write.Location.PropertyName}'.", nameof(write))),
            _ => throw new ArgumentOutOfRangeException(nameof(write), write.Location.Kind, "Unknown property value kind."),
        };

        /// <summary>A status-line message naming the property, without its value.</summary>
        private static string FailureMessage(PropertyWrite write) => write.Action == PropertyWriteAction.Remove
            ? $"Could not remove the property '{write.Location.PropertyName}'."
            : $"Could not set the property '{write.Location.PropertyName}'.";
    }
}
