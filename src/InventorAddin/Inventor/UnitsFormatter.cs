using Inventor;

namespace InventorAddin
{
    /// <summary>Formats Inventor database values (cm, kg, rad) in a document's display units.</summary>
    public sealed class UnitsFormatter
    {
        private readonly UnitsOfMeasure _uom;

        public UnitsFormatter(Document doc) => _uom = doc.UnitsOfMeasure;

        public string? Length(double? cm) => Format(cm, UnitsTypeEnum.kDefaultDisplayLengthUnits);
        public string? Angle(double? rad) => Format(rad, UnitsTypeEnum.kDefaultDisplayAngleUnits);
        public string? Mass(double? kg) => Format(kg, UnitsTypeEnum.kDefaultDisplayMassUnits);

        public string? Area(double? cm2) => cm2 == null ? null
            : ComSafe.Get(() => _uom.GetStringFromValue(cm2.Value, LengthUnitName() + "^2"));

        public string? Volume(double? cm3) => cm3 == null ? null
            : ComSafe.Get(() => _uom.GetStringFromValue(cm3.Value, LengthUnitName() + "^3"));

        /// <summary>Converts a length in cm to the document's length units.</summary>
        public double ToDocumentLength(double cm) =>
            _uom.ConvertUnits(cm, UnitsTypeEnum.kDatabaseLengthUnits, _uom.LengthUnits);

        private string LengthUnitName() => _uom.GetStringFromType(_uom.LengthUnits);

        private string? Format(double? value, UnitsTypeEnum units) => value == null ? null
            : ComSafe.Get(() => _uom.GetStringFromValue(value.Value, units));
    }
}
