using System;
using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin
{
    /// <summary>Maps Inventor's DocumentType + SubType GUID to <see cref="DocumentKind"/>.</summary>
    public static class DocumentKinds
    {
        public const string PartSubType = "{4D29B490-49B2-11D0-93C3-7E0706000000}";
        public const string SheetMetalSubType = "{9C464203-9BAE-11D3-8BAD-0060B0CE6BB4}";
        public const string AssemblySubType = "{E60F81E1-49B3-11D0-93C3-7E0706000000}";
        public const string WeldmentSubType = "{28EC8354-9024-440F-A8A2-0E0E55D635B0}";
        public const string DrawingSubType = "{BBF9FDF1-52DC-11D0-8C04-0800090BE8EC}";

        public static DocumentKind Of(Document doc)
        {
            string sub = (doc.SubType ?? "").ToUpperInvariant();
            return doc.DocumentType switch
            {
                DocumentTypeEnum.kPartDocumentObject =>
                    sub == SheetMetalSubType ? DocumentKind.SheetMetalPart : DocumentKind.Part,
                DocumentTypeEnum.kAssemblyDocumentObject =>
                    sub == WeldmentSubType ? DocumentKind.WeldmentAssembly : DocumentKind.Assembly,
                DocumentTypeEnum.kDrawingDocumentObject => DocumentKind.Drawing,
                DocumentTypeEnum.kPresentationDocumentObject => DocumentKind.Presentation,
                _ => DocumentKind.Unknown,
            };
        }

        public static bool IsPart(this DocumentKind k) => k is DocumentKind.Part or DocumentKind.SheetMetalPart;
        public static bool IsAssembly(this DocumentKind k) => k is DocumentKind.Assembly or DocumentKind.WeldmentAssembly;
    }
}
