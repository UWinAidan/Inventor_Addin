using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin.Extraction
{
    public static class ParameterReader
    {
        public static List<ParameterData> Read(Document doc, List<string> warnings)
        {
            var list = new List<ParameterData>();
            Parameters? parameters = GetParameters(doc);
            if (parameters == null)
                return list;

            foreach (Parameter p in parameters)
            {
                list.Add(new ParameterData
                {
                    Name = p.Name,
                    ParameterType = TypeName(ComSafe.Get(() => p.ParameterType)),
                    Expression = ComSafe.Get(() => p.Expression) ?? "",
                    Value = ComSafe.Get(() => p.Value),
                    Units = ComSafe.Get(() => p.get_Units()) ?? "",
                    Comment = ComSafe.Get(() => p.Comment),
                    IsKey = ComSafe.Get(() => p.IsKey),
                    ExposedAsProperty = ComSafe.Get(() => p.ExposedAsProperty),
                });
            }
            return list;
        }

        public static Parameters? GetParameters(Document doc) => doc switch
        {
            PartDocument part => part.ComponentDefinition.Parameters,
            AssemblyDocument asm => asm.ComponentDefinition.Parameters,
            DrawingDocument dwg => dwg.Parameters,
            _ => null,
        };

        private static string TypeName(ParameterTypeEnum t) => t switch
        {
            ParameterTypeEnum.kModelParameter => "Model",
            ParameterTypeEnum.kUserParameter => "User",
            ParameterTypeEnum.kReferenceParameter => "Reference",
            ParameterTypeEnum.kDerivedParameter => "Derived",
            ParameterTypeEnum.kTableParameter => "Table",
            _ => t.ToString(),
        };
    }
}
