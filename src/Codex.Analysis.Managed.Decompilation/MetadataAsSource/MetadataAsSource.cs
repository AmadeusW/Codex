using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Codex.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Perspectil;

namespace Codex.Analysis.Managed
{
    public class MetadataAsSource
    {
        private static Func<Document, ISymbol, CancellationToken, Task<Document>> addSourceToAsync = null;

        private static Func<Document, ISymbol, CancellationToken, Task<Document>> ReflectAddSourceToAsync(object service)
        {
            var assembly = Assembly.Load("Microsoft.CodeAnalysis.Features");
            var type = assembly.GetType("Microsoft.CodeAnalysis.MetadataAsSource.IMetadataAsSourceService");
            var method = type.GetMethod("AddSourceToAsync");
            return (Func<Document, ISymbol, CancellationToken, Task<Document>>)
                Delegate.CreateDelegate(typeof(Func<Document, ISymbol, CancellationToken, Task<Document>>), service, method);
        }

        public static MetadataReference CreateReferenceFromFilePath(string assemblyFilePath)
        {
            var documentationProvider = GetDocumentationProvider(
                assemblyFilePath,
                Path.GetFileNameWithoutExtension(assemblyFilePath));

            return MetadataReference.CreateFromFile(assemblyFilePath, documentation: documentationProvider);
        }

        public static async Task<Solution> LoadMetadataAsSourceSolution(string assemblyFilePath, string projectDirectory)
        {
            try
            {
                var assemblyName = Path.GetFileNameWithoutExtension(assemblyFilePath);

                var solution = new AdhocWorkspace(WorkspaceHacks.Pack.Value).CurrentSolution;
                var workspace = solution.Workspace;
                var project = solution.AddProject(assemblyName, assemblyName, LanguageNames.CSharp);
                var metadataReference = CreateReferenceFromFilePath(assemblyFilePath);

                //var referencePaths = MetadataReading.GetReferencePaths(metadataReference);
                //foreach (var referencePath in referencePaths)
                //{
                //    project = project.AddMetadataReference(CreateReferenceFromFilePath(referencePath));
                //}

                try
                {
                    project = project.AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
                }
                catch
                {
                    Console.WriteLine("Could add reference to mscorlib");
                }

                try
                {
                    var generator = PerspectiveAssemblyGenerator.CreateForFile(assemblyFilePath);
                    generator.Generate();

                    foreach (var assemblyDefinition in generator.PerspectiveAssemblies)
                    {
                        using (var stream = new MemoryStream())
                        {
                            try
                            {
                                stream.SetLength(0);
                                assemblyDefinition.Write(stream);
                                //stream.Position = 0;
                                //PEReader reader = new PEReader(stream);
                                //var hasMetadata = reader.HasMetadata;
                                //if (!hasMetadata)
                                //{
                                //}

                                stream.Position = 0;
                                project = project.AddMetadataReference(MetadataReference.CreateFromStream(stream));
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("Could not generate perspective assembly: " + assemblyDefinition.Name);
                            }
                        }
                    }
                }
                catch
                {
                    Console.WriteLine("Could not generate perspective assemblies");
                }

                var projectWithReference = project.AddMetadataReference(metadataReference);
                var compilation = await projectWithReference.GetCompilationAsync();
                var assemblyOrModuleSymbol = compilation.GetAssemblyOrModuleSymbol(metadataReference);
                IAssemblySymbol assemblySymbol = assemblyOrModuleSymbol as IAssemblySymbol;
                IModuleSymbol moduleSymbol = assemblyOrModuleSymbol as IModuleSymbol;
                if (moduleSymbol != null && assemblySymbol == null)
                {
                    assemblySymbol = moduleSymbol.ContainingAssembly;
                }

                INamespaceSymbol namespaceSymbol = null;
                if (assemblySymbol != null)
                {
                    namespaceSymbol = assemblySymbol.GlobalNamespace;
                }
                else if (moduleSymbol != null)
                {
                    namespaceSymbol = moduleSymbol.GlobalNamespace;
                }

                var types = GetTypes(namespaceSymbol)
                    .OfType<INamedTypeSymbol>()
                    .Where(t => t.CanBeReferencedByName).ToArray();

                var tempDocument = projectWithReference.AddDocument("temp", SourceText.From(""), null);
                var metadataAsSourceService = WorkspaceHacks.GetMetadataAsSourceService(tempDocument);
                if (addSourceToAsync == null)
                {
                    addSourceToAsync = ReflectAddSourceToAsync(metadataAsSourceService);
                }

                var texts = new Dictionary<INamedTypeSymbol, string>();

                List<Task<string>> sourceTextTasks = new List<Task<string>>();
                foreach (var type in types)
                {
                    sourceTextTasks.Add(GetTextAsync(addSourceToAsync(tempDocument, type, CancellationToken.None), assemblyFilePath));
                }

                var sourceTexts = await Task.WhenAll(sourceTextTasks);

                List<Task<SourceText>> textTasks = new List<Task<SourceText>>();
                int typeIndex = 0;
                foreach (string sourceText in sourceTexts)
                {
                    texts.Add(types[typeIndex], sourceText);
                    typeIndex++;
                }

                HashSet<string> existingFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kvp in texts)
                {
                    var tempProject = AddDocument(project, projectDirectory, kvp, existingFileNames);

                    // tempProject can be null if the document was in an unutterable namespace
                    // we want to skip such documents
                    if (tempProject != null)
                    {
                        project = tempProject;
                    }
                }

                //const string assemblyAttributesFileName = "AssemblyAttributes.cs";
                //project = project.AddDocument(
                //    assemblyAttributesFileName,
                //    assemblyAttributesFileText,
                //    filePath: assemblyAttributesFileName).Project;

                solution = project.Solution;
                return solution;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to run metadata as source for: {assemblyFilePath}\n{ex.ToString()}" + assemblyFilePath);
                return null;
            }
        }

        public static ImmutableArray<AssemblyIdentity> GetReferences(IAssemblySymbol assemblySymbol)
        {
            return assemblySymbol.Modules
                .SelectMany(m => m.ReferencedAssemblies)
                .Distinct()
                .ToImmutableArray();
        }

        public static async Task<string> GetTextAsync(Task<Document> documentTask, string assemblyFilePath)
        {
            try
            {
                var document = await documentTask;

                var sourceText = await document.GetTextAsync();

                var text = sourceText.ToString();

                text = text.Replace(assemblyFilePath, "Metadata As Source Generated Code");

                return text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when adding a MAS document to texts: {assemblyFilePath} \n{ex.ToString()}");
            }

            return null;
        }


        private static Dictionary<string, string> assemblyNameToXmlDocFileMap = null;

        /// <summary>
        /// This has to be unique, there shouldn't be a project with this name ever
        /// </summary>
        public const string GeneratedAssemblyAttributesFileName = "GeneratedAssemblyAttributes0e71257b769ef";

        private static Dictionary<string, string> AssemblyNameToXmlDocFileMap
        {
            get
            {
                if (assemblyNameToXmlDocFileMap == null)
                {
                    assemblyNameToXmlDocFileMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    //foreach (var path in Paths.XmlDocPaths)
                    //{
                    //    if (Directory.Exists(path))
                    //    {
                    //        foreach (var file in Directory.GetFiles(path))
                    //        {
                    //            assemblyNameToXmlDocFileMap[Path.GetFileNameWithoutExtension(file)] = file;
                    //        }
                    //    }
                    //}
                }

                return assemblyNameToXmlDocFileMap;
            }
        }

        private static DocumentationProvider GetDocumentationProvider(string assemblyFilePath, string assemblyName)
        {
            var result = DocumentationProvider.Default;
            string xmlFile;
            //if (AssemblyNameToXmlDocFileMap.TryGetValue(assemblyName, out xmlFile))
            //{
            //    result = new XmlDocumentationProvider(xmlFile);
            //}

            return result;
        }

        private static Project AddDocument(
            Project project,
            string projectDirectory,
            KeyValuePair<INamedTypeSymbol, string> symbolAndText,
            HashSet<string> existingFileNames)
        {
            var symbol = symbolAndText.Key;
            var text = symbolAndText.Value;
            var sanitizedTypeName = Paths.SanitizeFileName(symbol.Name);
            if (symbol.IsGenericType)
            {
                sanitizedTypeName = sanitizedTypeName + "`" + symbol.TypeParameters.Length;
            }

            var fileName = sanitizedTypeName + ".cs";
            var folders = GetFolderChain(symbol);
            if (folders == null)
            {
                // There was an unutterable namespace name - abort the entire document
                return null;
            }

            var foldersString = string.Join(".", folders ?? Enumerable.Empty<string>());
            var fileNameAndFolders = foldersString + fileName;
            int index = 1;
            while (!existingFileNames.Add(fileNameAndFolders))
            {
                fileName = sanitizedTypeName + index + ".cs";
                fileNameAndFolders = foldersString + fileName;
                index++;
            }

            project = project.AddDocument(fileName, text, folders, Path.Combine(projectDirectory, fileName)).Project;
            return project;
        }

        private static string[] GetFolderChain(INamedTypeSymbol symbol)
        {
            var containingNamespace = symbol.ContainingNamespace;
            var folders = new List<string>();
            while (containingNamespace != null && !containingNamespace.IsGlobalNamespace)
            {
                if (!containingNamespace.CanBeReferencedByName)
                {
                    // namespace name is mangled - we don't want it
                    return null;
                }

                var sanitizedNamespaceName = Paths.SanitizeFolder(containingNamespace.Name);
                folders.Add(sanitizedNamespaceName);
                containingNamespace = containingNamespace.ContainingNamespace;
            }

            folders.Reverse();
            return folders.ToArray();
        }

        private static IEnumerable<ISymbol> GetTypes(INamespaceSymbol namespaceSymbol)
        {
            var results = new List<ISymbol>();
            EnumSymbols(namespaceSymbol, results.Add);
            return results;
        }

        private static void EnumSymbols(INamespaceSymbol namespaceSymbol, Action<ISymbol> action)
        {
            foreach (var subNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                EnumSymbols(subNamespace, action);
            }

            foreach (var topLevelType in namespaceSymbol.GetTypeMembers())
            {
                action(topLevelType);
            }
        }
    }
}
