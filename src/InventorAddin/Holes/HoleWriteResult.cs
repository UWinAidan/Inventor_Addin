using System.Collections.Generic;
using Inventor;

namespace InventorAddin.Holes
{
    /// <summary>
    /// What <see cref="HoleFeatureWriter"/> did. <see cref="Hole"/> is the hole feature, when one exists: on a
    /// refusal it may still be set (a hole that was made but is unhealthy), and the caller aborts the inner
    /// transaction that made it (spike 081 round 6). <see cref="Notes"/> are things the user should know although
    /// the hole was made, such as the tap drill fallback.
    /// </summary>
    public sealed class HoleWriteResult
    {
        private HoleWriteResult(HoleFeature? hole, string? refusal, IReadOnlyList<string> notes, bool recreated)
        {
            Hole = hole;
            Refusal = refusal;
            Notes = notes;
            Recreated = recreated;
        }

        public HoleFeature? Hole { get; }

        /// <summary>Why the hole could not be made or changed, or null when it was.</summary>
        public string? Refusal { get; }

        public IReadOnlyList<string> Notes { get; }

        /// <summary>
        /// The feature was deleted and made again (a change between tapped and untapped, or of the thread), so
        /// <see cref="Hole"/> is a new object; the caller replaces the one it holds.
        /// </summary>
        public bool Recreated { get; }

        public bool Succeeded => Refusal == null && Hole != null;

        internal static HoleWriteResult Made(HoleFeature hole, IReadOnlyList<string> notes, bool recreated) =>
            new HoleWriteResult(hole, null, notes, recreated);

        internal static HoleWriteResult Refused(string refusal, HoleFeature? hole, IReadOnlyList<string> notes, bool recreated = false) =>
            new HoleWriteResult(hole, refusal, notes, recreated);
    }
}
