using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Codex.Import;
using Codex.ObjectModel;
using Microsoft.CodeAnalysis;
using static Codex.Analysis.Xml.Linq.XElementExtensions;
using static Codex.Analysis.BoundSourceFileBuilder;


namespace Codex.Analysis
{
    public class AnalyzedProjectContext
    {
        public readonly AnalyzedProject Project;

        public AnalyzedProjectContext(AnalyzedProject project)
        {
            Project = project;
        }

        public ConcurrentDictionary<string, DefinitionSymbol> ReferenceDefinitionMap = new ConcurrentDictionary<string, DefinitionSymbol>();

        public ConcurrentDictionary<string, ReferencedProject> ReferencedProjects = new ConcurrentDictionary<string, ReferencedProject>();

        private ConcurrentDictionary<string, NamespaceExtensionData> m_extensionData = new ConcurrentDictionary<string, NamespaceExtensionData>();

        public void ReportDocument(BoundSourceFile boundSourceFile, RepoFile file)
        {
            foreach (var reference in boundSourceFile.References)
            {

            }
        }

        public class NamespaceExtensionData : ExtensionData
        {
            public string Namespace;
            public string Qualifier;
            private XElement NamespaceElement;

            public XElement GetNamespaceElement(XElement parent)
            {
                if (NamespaceElement?.Parent != parent)
                {
                    NamespaceElement = null;
                }

                NamespaceElement = NamespaceElement ?? parent.CreateChild("Namespace", el => el.AddAttribute("Name", Namespace));
                return NamespaceElement;
            }
        }

        public NamespaceExtensionData GetReferenceNamespaceData(string nspaceId)
        {
            var namespaceSymbol = ReferenceDefinitionMap[nspaceId];
            NamespaceExtensionData extData = namespaceSymbol.ExtData as NamespaceExtensionData;
            if (extData == null)
            {
                extData = m_extensionData.GetOrAdd(nspaceId, k => new NamespaceExtensionData());
                extData.Namespace = namespaceSymbol.DisplayName;
                extData.Qualifier = namespaceSymbol.DisplayName + ".";
                namespaceSymbol.ExtData = extData;
            }

            return extData;
        }

        public void Finish(RepoProject repoProject)
        {
            foreach (var entry in ReferenceDefinitionMap)
            {
                var def = entry.Value;
                var project = ReferencedProjects.GetOrAdd(def.ProjectId, id => new ReferencedProject()
                {
                    ProjectId = id,
                    DisplayName = id,
                });

                project.Definitions.Add(def);
            }

            foreach (var referencedProject in ReferencedProjects.Values.OrderBy(rp => rp.ProjectId, StringComparer.OrdinalIgnoreCase))
            {
                if (referencedProject.ProjectId != Project.Id)
                {
                    Project.ReferencedProjects.Add(referencedProject);
                }

                // Remove all the namespaces
                referencedProject.Definitions.RemoveAll(ds => ds.Kind == nameof(SymbolKind.Namespace));

                // Sort the definitions by qualified name
                referencedProject.Definitions.Sort((d1, d2) => d1.DisplayName.CompareTo(d2.DisplayName));
            }

            //CreateNamespaceFile();
            CreateReferencedProjectFiles(repoProject);
        }

        private void CreateReferencedProjectFiles(RepoProject repoProject)
        {
            foreach (var project in ReferencedProjects.Values)
            {
                if (project.Definitions.Count == 0)
                {
                    continue;
                }

                CreateMetadataFile(repoProject, GetProjectReferenceSymbolsPath(project.ProjectId), () =>
                {
                    XElement container = null;
                    return Element("ReferenceSymbols")
                    .AddAttribute("Count", project.Definitions.Count)
                    .ForEach(project.Definitions, (el, definition) =>
                    {
                        var nsdata = definition.ExtData as NamespaceExtensionData;
                        container = nsdata?.GetNamespaceElement(el) ?? el;

                        if (definition.DisplayName.StartsWith(nsdata?.Qualifier ?? string.Empty))
                        {
                            ReferenceSymbol reference = definition;
                            reference.ReferenceKind = nameof(ReferenceKind.ProjectLevelReference);
                            reference.ExcludeFromSearch = true;
                            container.AddElement("Symbol", symbolElement =>
                                symbolElement
                                .AddAttribute("Name", definition.DisplayName
                                    .Substring(nsdata?.Qualifier.Length ?? 0), reference)
                                .AddAttribute("ReferenceCount", definition.ReferenceCount.ToString()));
                        }
                    });
                });
            }

            CreateMetadataFile(repoProject, "ReferenceProjects.xml", () =>
                {
                    return Element("ReferenceProjects").ForEach(ReferencedProjects.Values
                        .OrderBy(rp => rp.ProjectId, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(rp => rp.DisplayName, StringComparer.OrdinalIgnoreCase), (el, project) =>
                    {
                        ReferenceSymbol fileRef = project.Definitions.Count == 0 ? null : CreateFileReferenceSymbol(
                                    GetMetadataFilePath(GetProjectReferenceSymbolsPath(project.ProjectId)),
                                    Project.Id);
                        el.AddElement("Project", projElement =>
                            projElement
                            .AddAttribute("ReferenceCount", project.Definitions.Count.ToString(), fileRef)
                            .AddAttribute("Name", project.ProjectId)
                            .AddAttribute("FullName", project.DisplayName));
                    });
                });
        }

        private string GetProjectReferenceSymbolsPath(string projectId)
        {
            return $@"ReferenceSymbols\{projectId}.xml";
        }

        private string GetMetadataFilePath(string fileName)
        {
            return $@"[Metadata]\{fileName}";
        }

        private void CreateMetadataFile(RepoProject project, string fileName, Func<XElement> elementFactory)
        {
            var metadataFileBuilder = elementFactory().CreateAnnotatedSourceBuilder(
                new SourceFileInfo()
                {
                    Path = GetMetadataFilePath(fileName),
                    Language = "xml",
                }, Project.Id);

            var repoFile = project.AddFile(
                $@"\\Codex\ProjectMetadata\{project.ProjectId}\{metadataFileBuilder.SourceFile.Info.Path}",
                metadataFileBuilder.SourceFile.Info.Path);

            repoFile.InMemorySourceFileBuilder = metadataFileBuilder;

            metadataFileBuilder.BoundSourceFile.ExcludeFromSearch = true;

            repoFile.Analyze();
        }
    }
}