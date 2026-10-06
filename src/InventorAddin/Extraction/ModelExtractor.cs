using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Models;

namespace InventorAddin.Extraction
{
    /// <summary>Single entry point: turns any open Inventor document into a <see cref="ModelData"/> snapshot.</summary>
    public static class ModelExtractor
    {
        public static ModelData Extract(Document doc, ExtractionOptions? options = null)
        {
            options ??= ExtractionOptions.Default;
            DocumentKind kind = DocumentKinds.Of(doc);

            ModelData data = kind switch
            {
                _ when kind.IsPart() => new PartData(),
                _ when kind.IsAssembly() => new AssemblyData(),
                DocumentKind.Drawing => new DrawingData(),
                _ => throw new NotSupportedException($"Unsupported document type: {doc.DocumentType}"),
            };

            FillCommon(data, doc, kind, options);

            switch (data)
            {
                case PartData part:
                    PartExtractor.Fill(part, (PartDocument)doc, options);
                    break;
                case AssemblyData asm:
                    AssemblyExtractor.Fill(asm, (AssemblyDocument)doc, options);
                    break;
                case DrawingData dwg:
                    DrawingExtractor.Fill(dwg, (DrawingDocument)doc, options);
                    break;
            }
            return data;
        }

        private static void FillCommon(ModelData data, Document doc, DocumentKind kind, ExtractionOptions options)
        {
            var w = data.Warnings;
            data.Kind = kind;
            data.FullFileName = doc.FullFileName;
            data.FullDocumentName = doc.FullDocumentName;
            data.DisplayName = doc.DisplayName;
            data.SubTypeId = doc.SubType;
            data.InternalName = doc.InternalName;
            data.IsDirty = doc.Dirty;
            data.IsReadOnly = ComSafe.Get(() => IsFileReadOnly(doc.FullFileName));
            data.ModelStateName = ComSafe.Get(() => ActiveModelStateName(doc));

            data.Common = PropertyReader.ReadCommon(doc);
            data.Properties = PropertyReader.ReadAll(doc, w);
            data.Parameters = ParameterReader.Read(doc, w);

            data.ILogicRules = ILogicReader.Read(InventorHost.App, doc, w);
            if (!options.IncludeILogicRuleText)
                data.ILogicRules.ForEach(r => r.Text = null);

            ComSafe.Run(() =>
            {
                var refs = new List<string>();
                foreach (Document r in doc.AllReferencedDocuments)
                    refs.Add(r.FullFileName);
                data.ReferencedFiles = refs;
            }, w, "Referenced documents");
        }

        private static string? ActiveModelStateName(Document doc) => doc switch
        {
            PartDocument p => p.ComponentDefinition.ModelStates.ActiveModelState.Name,
            AssemblyDocument a => a.ComponentDefinition.ModelStates.ActiveModelState.Name,
            _ => null,
        };

        private static bool IsFileReadOnly(string path) =>
            System.IO.File.Exists(path) && new System.IO.FileInfo(path).IsReadOnly;
    }
}
