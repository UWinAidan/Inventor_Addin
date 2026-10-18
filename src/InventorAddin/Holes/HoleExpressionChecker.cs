using System;
using Inventor;
using InventorAddin.Core.Holes;

namespace InventorAddin.Holes
{
    /// <summary>
    /// Reads the text of a depth box as Inventor does, against the part's own parameters (task 100): the part
    /// document's <see cref="UnitsOfMeasure"/>, so <c>d0</c> resolves, with a number that has no unit read in the
    /// window's length unit. Inventor gives no reason for text it cannot read (spike 100), so an invalid check carries
    /// none and the window shows <see cref="HoleExpressions.NotALengthText"/>.
    /// </summary>
    public sealed class HoleExpressionChecker : IHoleExpressionChecker
    {
        private readonly PartDocument _doc;

        public HoleExpressionChecker(PartDocument doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public HoleExpressionCheck Check(string expression, HoleUnits units)
        {
            string? text = HoleExpressions.Clean(expression);
            if (text == null)
                return HoleExpressionCheck.Invalid();

            object unitsType = units == HoleUnits.Imperial
                ? UnitsTypeEnum.kInchLengthUnits
                : UnitsTypeEnum.kMillimeterLengthUnits;
            try
            {
                UnitsOfMeasure measure = _doc.UnitsOfMeasure;
                if (!measure.IsExpressionValid(text, unitsType))
                    return HoleExpressionCheck.Invalid();

                // Centimetres, Inventor's internal length unit.
                return HoleExpressionCheck.Valid(Convert.ToDouble(measure.GetValueFromExpression(text, unitsType)));
            }
            catch (Exception)
            {
                // GetValueFromExpression fails with "Unspecified error" on text it cannot read (spike 100).
                return HoleExpressionCheck.Invalid();
            }
        }
    }
}
