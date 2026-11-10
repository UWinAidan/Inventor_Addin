using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Holes;

namespace InventorAddin.Holes
{
    /// <summary>
    /// Writes and reads Core's <see cref="HoleStoredData"/> as the <c>AWB_Hole</c> attribute set on a hole feature
    /// (spec 09, "The command"; spike 081 section 6). Core decides the attributes; this copies them.
    /// </summary>
    public static class HoleStoredDataStore
    {
        /// <summary>True when the hole has an <c>AWB_Hole</c> attribute set.</summary>
        public static bool Has(HoleFeature hole) =>
            ComSafe.Get(() => hole.AttributeSets.NameIsUsed[HoleStoredData.AttributeSetName]);

        /// <summary>
        /// Replaces the hole's <c>AWB_Hole</c> attribute set with <paramref name="data"/>. The spec in the data must be
        /// resolved (<see cref="HoleBuildResult.Resolved"/>); <see cref="HoleStoredData.ToAttributes"/> throws
        /// otherwise, before anything is changed.
        /// </summary>
        public static void Write(HoleFeature hole, HoleStoredData data)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            IReadOnlyList<HoleAttribute> attributes = data.ToAttributes();

            Delete(hole);
            AttributeSet set = hole.AttributeSets.Add(HoleStoredData.AttributeSetName, true);
            foreach (HoleAttribute attribute in attributes)
                set.Add(attribute.Name, ValueType(attribute.Value), attribute.Value);
        }

        /// <summary>
        /// The hole's stored data, or why it cannot be read; null when the hole has no <c>AWB_Hole</c> attribute set
        /// (a hole made by Inventor's command).
        /// </summary>
        public static HoleStoredDataReadResult? Read(HoleFeature hole)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (!Has(hole))
                return null;

            var attributes = new List<HoleAttribute>();
            AttributeSet set = hole.AttributeSets[HoleStoredData.AttributeSetName];
            foreach (Inventor.Attribute attribute in set)
            {
                string? name = ComSafe.Get(() => attribute.Name);
                object? value = ComSafe.Get(() => attribute.Value);
                if (name != null && value != null)
                    attributes.Add(new HoleAttribute(name, value));
            }

            return HoleStoredData.Read(attributes);
        }

        /// <summary>Deletes the hole's <c>AWB_Hole</c> attribute set, when it has one.</summary>
        public static void Delete(HoleFeature hole)
        {
            if (hole == null)
                throw new ArgumentNullException(nameof(hole));
            if (Has(hole))
                hole.AttributeSets[HoleStoredData.AttributeSetName].Delete();
        }

        private static ValueTypeEnum ValueType(object value) => value switch
        {
            string => ValueTypeEnum.kStringType,
            int => ValueTypeEnum.kIntegerType,
            double => ValueTypeEnum.kDoubleType,
            _ => throw new ArgumentException($"An attribute value of type {value.GetType().Name} cannot be stored.", nameof(value)),
        };
    }
}
