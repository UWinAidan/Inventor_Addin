using System;
using System.Collections.Generic;
using Inventor;
using InventorAddin.Core.Drawing;

// The Drawing folder holds the drawing-side helpers; the namespace names the feature, not the folder (Inventor has types named Drawing*).
namespace InventorAddin.PrintViews
{
    /// <summary>
    /// Reads an assembly's browser tree into <see cref="BatchPrintComponent"/>s for Batch Print (task 202): the top-level occurrences
    /// in browser order (<c>Occurrences</c>), and a sub-assembly's children through the occurrence's <c>SubOccurrences</c>, also in
    /// browser order. Suppressed occurrences and virtual components (no file) are left out. A Content Center part
    /// (<c>PartComponentDefinition.IsContentMember</c>) or a file the project finds in a library
    /// (a file under one of the active project's library paths, <c>DesignProject.LibraryPaths</c>) is marked <c>IsLibrary</c>, and its children are
    /// not read.
    /// </summary>
    internal static class BatchPrintTreeReader
    {
        /// <summary>The assembly's tree. The root's own library state is the caller's to decide with <see cref="LibraryFolders"/>.</summary>
        public static BatchPrintComponent Read(AssemblyDocument assembly, string assemblyPath, IReadOnlyList<string> libraryFolders)
        {
            var children = new List<BatchPrintComponent>();
            ComponentOccurrences? occurrences = ComSafe.Get(() => assembly.ComponentDefinition.Occurrences);
            if (occurrences != null)
                foreach (ComponentOccurrence occ in occurrences)
                    AddOccurrence(occ, libraryFolders, children);
            return new BatchPrintComponent(assemblyPath, false, children);
        }

        /// <summary>
        /// The active project's library paths. Any failure reading the project gives fewer folders, so a file is then taken as not
        /// library unless it is a Content Center part.
        /// </summary>
        public static IReadOnlyList<string> LibraryFolders(Application app)
        {
            var folders = new List<string>();
            string projectFile = ComSafe.Get(() => app.DesignProjectManager.ActiveDesignProject.FullFileName) ?? string.Empty;
            ComSafe.Run(() =>
            {
                ProjectPaths paths = app.DesignProjectManager.ActiveDesignProject.LibraryPaths;
                foreach (ProjectPath p in paths)
                    folders.Add(BatchPrintPlan.ResolveProjectFolder(projectFile, p.Path));
            });
            return folders;
        }

        private static void AddOccurrence(ComponentOccurrence occ, IReadOnlyList<string> libraryFolders, List<BatchPrintComponent> into)
        {
            if (ComSafe.Get(() => occ.Suppressed))
                return;

            if (ComSafe.Get(() => occ.Definition is VirtualComponentDefinition))
                return; // a virtual component has no file

            string? path = ComSafe.Get(() => occ.ReferencedDocumentDescriptor.FullDocumentName);
            if (string.IsNullOrWhiteSpace(path))
                return; // a virtual component has no file

            bool library = IsLibrary(occ, path!, libraryFolders);
            var children = new List<BatchPrintComponent>();
            if (!library && ComSafe.Get(() => occ.DefinitionDocumentType) == DocumentTypeEnum.kAssemblyDocumentObject)
            {
                ComponentOccurrencesEnumerator? subs = ComSafe.Get(() => occ.SubOccurrences);
                if (subs != null)
                    foreach (ComponentOccurrence sub in subs)
                        AddOccurrence(sub, libraryFolders, children);
            }

            into.Add(new BatchPrintComponent(path!, library, children));
        }

        private static bool IsLibrary(ComponentOccurrence occ, string path, IReadOnlyList<string> libraryFolders)
        {
            if (ComSafe.Get(() => occ.Definition is PartComponentDefinition part && part.IsContentMember))
                return true;
            return BatchPrintPlan.IsUnderAnyFolder(path, libraryFolders);
        }
    }
}
