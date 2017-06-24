using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Codex.ObjectModel;
using Codex.Search;
using Codex.Storage;
using WebUI.Util;
using Reference = Codex.ObjectModel.SymbolReferenceEntry;

namespace WebUI.Controllers
{
    public class ReferencesController : Controller
    {
        private readonly ElasticsearchStorage Storage;

        public ReferencesController(IStorage storage)
        {
            Storage = (ElasticsearchStorage)storage;
        }
        [Route("repos/{repoName}/references/{projectId}")]
        [Route("references/{projectId}")]
        public async Task<ActionResult> References(string projectId, string symbolId, string projectScope = null)
        {
            try
            {
                Requests.LogRequest(this);

                var definitionResult = await ((IStorage)Storage).GetDefinitionsAsync(this.GetSearchRepos(), projectId, symbolId);
                var definitionSpan = definitionResult?.FirstOrDefault()?.Span;
                var definition = definitionSpan?.Definition;
                var symbolName = definition?.ShortName ?? symbolId;

                Responses.PrepareResponse(Response);

                var referencesResult = await Storage.GetReferencesToSymbolAsync(
                    this.GetSearchRepos(),
                    new Symbol()
                    {
                        ProjectId = projectId,
                        Id = SymbolId.UnsafeCreateWithValue(symbolId),
                    }.SetProjectScope(projectScope));

                if (referencesResult.Entries.Count != 0)
                {
                    if (definition != null)
                    {
                        if (projectScope == null)
                        {
                            var relatedDefinitions = await Storage.GetRelatedDefinitions(this.GetSearchRepos(),
                                definition.Id.Value,
                                definition.ProjectId);

                            referencesResult.RelatedDefinitions = relatedDefinitions;
                        }
                        else
                        {
                            var definitionReferences = await Storage.GetReferencesToSymbolAsync(
                                this.GetSearchRepos(),
                                new Symbol()
                                {
                                    ProjectId = projectId,
                                    Id = SymbolId.UnsafeCreateWithValue(symbolId),
                                    Kind = nameof(ReferenceKind.Definition)
                                });

                            referencesResult.Entries.InsertRange(0, definitionReferences.Entries);
                        }
                    }

                    referencesResult.SymbolName = symbolName;
                    return PartialView((object)GenerateReferencesHtml(referencesResult));
                }

                return PartialView((object)$"No references to project {projectId} and symbol {symbolId} found.");
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        private static void WriteImplementedInterfaceMembers(StringWriter writer, List<SymbolSearchResultEntry> implementedInterfaceMembers)
        {
            Write(writer, string.Format(@"<div class=""rH"" onclick=""ToggleExpandCollapse(this);return false;"">Implemented interface member{0}:</div>", implementedInterfaceMembers.Count > 1 ? "s" : ""));

            Write(writer, @"<div class=""rK"">");

            foreach (var implementedInterfaceMember in implementedInterfaceMembers)
            {
                writer.Write(GetSymbolText(implementedInterfaceMember));
            }

            writer.Write("</div>");
        }

        public static string GetSymbolText(SymbolSearchResultEntry searchResult)
        {
            var resultText = $@"<a onclick=""LoadSourceCode('{searchResult.Symbol.ProjectId.AsJavaScriptStringEncoded()}', '{searchResult.File.AsJavaScriptStringEncoded()}', '{searchResult.Symbol.Id}');return false;"" href=""/?rightProject={searchResult.Symbol.ProjectId}&rightSymbol={searchResult.Symbol.Id}"">
 <div class=""resultItem"">
 <img src=""/content/icons/{searchResult.Glyph}"" height=""16"" width=""16"" /><div class=""resultKind"">{searchResult.Symbol.Kind.ToLowerInvariant()}</div><div class=""resultName"">{searchResult.Symbol.ShortName}</div><div class=""resultDescription"">{searchResult.DisplayName}</div>
 </div>
 </a>";

            return resultText;
        }

        private static void WriteBaseMember(StringWriter writer, SymbolSearchResultEntry baseDefinition)
        {
            Write(writer, string.Format(@"<div class=""rH"" onclick=""ToggleExpandCollapse(this);return false;"">Base:</div>"));

            Write(writer, @"<div class=""rK"">");

            writer.Write(GetSymbolText(baseDefinition));

            writer.Write("</div>");
        }

        public static string GenerateReferencesHtml(SymbolReferenceResult symbolReferenceResult)
        {
            var references = symbolReferenceResult.Entries;
            var definitionProjectId = symbolReferenceResult.ProjectId;
            var symbolId = symbolReferenceResult.SymbolId;
            var symbolName = symbolReferenceResult.SymbolName;

            int totalReferenceCount = 0;
            var referenceKindGroups = CreateReferences(references, out totalReferenceCount);

            using (var writer = new StringWriter())
            {
                if ((symbolReferenceResult.RelatedDefinitions?.Count ?? 0) != 0)
                {
                    var baseMember = symbolReferenceResult.RelatedDefinitions
                        .Where(r => r.ReferenceKind == nameof(ReferenceKind.Override))
                        .FirstOrDefault();
                    if (baseMember != null)
                    {
                        WriteBaseMember(writer, baseMember);
                    }

                    var implementedMembers = symbolReferenceResult.RelatedDefinitions
                        .Where(r => r.ReferenceKind == nameof(ReferenceKind.InterfaceMemberImplementation))
                        .ToList();

                    if (implementedMembers.Count != 0)
                    {
                        WriteImplementedInterfaceMembers(writer, implementedMembers);
                    }
                }

                foreach (var referenceKind in referenceKindGroups.OrderBy(t => (int)t.Item1))
                {
                    string formatString = "";

                    switch (referenceKind.Item1)
                    {
                        case ReferenceKind.Reference:
                            formatString = "{0} reference{1} to {2}";
                            break;
                        case ReferenceKind.Definition:
                            formatString = "{0} definition{1} of {2}";
                            break;
                        case ReferenceKind.Constructor:
                            formatString = "{0} constructor{1} of {2}";
                            break;
                        case ReferenceKind.Instantiation:
                            formatString = "{0} instantiation{1} of {2}";
                            break;
                        case ReferenceKind.DerivedType:
                            formatString = "{0} type{1} derived from {2}";
                            break;
                        case ReferenceKind.InterfaceInheritance:
                            formatString = "{0} interface{1} inheriting from {2}";
                            break;
                        case ReferenceKind.InterfaceImplementation:
                            formatString = "{0} implementation{1} of {2}";
                            break;
                        case ReferenceKind.Override:
                            formatString = "{0} override{1} of {2}";
                            break;
                        case ReferenceKind.InterfaceMemberImplementation:
                            formatString = "{0} implementation{1} of {2}";
                            break;
                        case ReferenceKind.Write:
                            formatString = "{0} write{1} to {2}";
                            break;
                        case ReferenceKind.Read:
                            formatString = "{0} read{1} of {2}";
                            break;
                        case ReferenceKind.GuidUsage:
                            formatString = "{0} usage{1} of Guid {2}";
                            break;
                        case ReferenceKind.EmptyArrayAllocation:
                            formatString = "{0} allocation{1} of empty arrays";
                            break;
                        case ReferenceKind.MSBuildPropertyAssignment:
                            formatString = "{0} assignment{1} to MSBuild property {2}";
                            break;
                        case ReferenceKind.MSBuildPropertyUsage:
                            formatString = "{0} usage{1} of MSBuild property {2}";
                            break;
                        case ReferenceKind.MSBuildItemAssignment:
                            formatString = "{0} assignment{1} to MSBuild item {2}";
                            break;
                        case ReferenceKind.MSBuildItemUsage:
                            formatString = "{0} usage{1} of MSBuild item {2}";
                            break;
                        case ReferenceKind.MSBuildTargetDeclaration:
                            formatString = "{0} declaration{1} of MSBuild target {2}";
                            break;
                        case ReferenceKind.MSBuildTargetUsage:
                            formatString = "{0} usage{1} of MSBuild target {2}";
                            break;
                        case ReferenceKind.MSBuildTaskDeclaration:
                            formatString = "{0} import{1} of MSBuild task {2}";
                            break;
                        case ReferenceKind.MSBuildTaskUsage:
                            formatString = "{0} call{1} to MSBuild task {2}";
                            break;
                        case ReferenceKind.Text:
                            formatString = "{0} text search hit{1} for '{2}'";
                            break;
                        default:
                            throw new NotImplementedException("Missing case for " + referenceKind.Item1);
                    }

                    var referencesOfSameKind = referenceKind.Item2.OrderBy(g => g.Item1, StringComparer.OrdinalIgnoreCase);
                    totalReferenceCount = CountItems(referenceKind);
                    string headerText = string.Format(
                        formatString,
                        totalReferenceCount,
                        totalReferenceCount == 1 ? "" : "s",
                        symbolName);

                    Write(writer, string.Format(@"<div class=""rH"" onclick=""ToggleExpandCollapse(this); return false;"">{0}</div>", headerText));

                    Write(writer, @"<div class=""rK"">");

                    foreach (var sameAssemblyReferencesGroup in referencesOfSameKind)
                    {
                        string assemblyName = sameAssemblyReferencesGroup.Item1;
                        Write(writer, "<div class=\"rA\" onclick=\"ToggleExpandCollapse(this); return false;\">{0} ({1})</div>", assemblyName, CountItems(sameAssemblyReferencesGroup));
                        Write(writer, "<div class=\"rG\" id=\"{0}\">", assemblyName);

                        foreach (var sameFileReferencesGroup in sameAssemblyReferencesGroup.Item2.OrderBy(g => g.Item1))
                        {
                            Write(writer, "<div class=\"rF\">");
                            var fileName = sameFileReferencesGroup.Item1;
                            var glyph = "url('content/icons/" + GetGlyph(fileName) + ".png');";
                            WriteLine(writer, "<div class=\"rN\" style=\"background-image: {2}\">{0} ({1})</div>", fileName, CountItems(sameFileReferencesGroup), glyph);

                            foreach (var sameLineReferencesGroup in sameFileReferencesGroup.Item2)
                            {
                                var first = sameLineReferencesGroup.First();
                                var lineNumber = first.ReferringSpan.LineNumber + 1;
                                string onClick = $@"LoadSourceCode('{first.ReferringProjectId}', '{first.ReferringFilePath.AsJavaScriptStringEncoded()}', null, '{lineNumber}');return false;";
                                var url = $"/?leftProject={definitionProjectId}&leftSymbol={symbolId}&rightProject={first.ReferringProjectId}&file={HttpUtility.UrlEncode(first.ReferringFilePath)}&line={lineNumber}";
                                Write(writer, "<a class=\"rL\" onclick=\"{0}\" href=\"{1}\">", onClick, url);

                                Write(writer, "<b>{0}</b>", sameLineReferencesGroup.Key);
                                MergeOccurrences(writer, sameLineReferencesGroup);
                                WriteLine(writer, "</a>");
                            }

                            WriteLine(writer, "</div>");
                        }

                        WriteLine(writer, "</div>");
                    }

                    WriteLine(writer, "</div>");
                }

                return writer.ToString();
            }
        }

        private static string GetGlyph(string fileName)
        {
            return ModelConverter.GetFileNameGlyph(fileName);
        }

        private static int CountItems(Tuple<string, IEnumerable<IGrouping<int, Reference>>> sameFileReferencesGroup)
        {
            int count = 0;

            foreach (var line in sameFileReferencesGroup.Item2)
            {
                foreach (var occurrence in line)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountItems(
            Tuple<string, IEnumerable<Tuple<string, IEnumerable<IGrouping<int, Reference>>>>> resultsInAssembly)
        {
            int count = 0;
            foreach (var file in resultsInAssembly.Item2)
            {
                count += CountItems(file);
            }

            return count;
        }

        private static int CountItems(
            Tuple<ReferenceKind, IEnumerable<Tuple<string, IEnumerable<Tuple<string, IEnumerable<IGrouping<int, Reference>>>>>>> results)
        {
            int count = 0;
            foreach (var item in results.Item2)
            {
                count += CountItems(item);
            }

            return count;
        }

        private static
            IEnumerable<Tuple<ReferenceKind,
                IEnumerable<Tuple<string,
                    IEnumerable<Tuple<string,
                        IEnumerable<IGrouping<int, Reference>>
                    >>
                >>
            >> CreateReferences(
            IEnumerable<SymbolReferenceEntry> list,
            out int totalReferenceCount)
        {
            totalReferenceCount = 0;

            var result = list.GroupBy
            (
                r0 => ParseReferenceKind(r0.ReferringSpan.Reference.ReferenceKind),
                (kind, referencesOfSameKind) => Tuple.Create
                (
                    kind,
                    referencesOfSameKind.GroupBy
                    (
                        r1 => r1.ReferringProjectId,
                        (assemblyName, referencesInSameAssembly) => Tuple.Create
                        (
                            assemblyName,
                            referencesInSameAssembly.GroupBy
                            (
                                r2 => r2.ReferringFilePath,
                                (filePath, referencesInSameFile) => Tuple.Create
                                (
                                    filePath,
                                    referencesInSameFile.GroupBy
                                    (
                                        r3 => r3.ReferringSpan.LineNumber + 1
                                    )
                                )
                            )
                        )
                    )
                )
            );

            return result;
        }

        private static ReferenceKind ParseReferenceKind(string kind)
        {
            ReferenceKind referenceKind;
            if (!Enum.TryParse<ReferenceKind>(kind, true, out referenceKind))
            {
                referenceKind = ReferenceKind.Reference;
            }

            return referenceKind;
        }

        private static void MergeOccurrences(TextWriter writer, IEnumerable<SymbolReferenceEntry> referencesOnTheSameLineEx)
        {
            foreach (var referencesOnTheSameLineGroup in referencesOnTheSameLineEx.GroupBy(r => r.ReferringSpan.LineSpanText))
            {
                var text = referencesOnTheSameLineGroup.Key;
                var referencesOnTheSameLine = referencesOnTheSameLineGroup.OrderBy(r => r.ReferringSpan.LineSpanStart);
                int current = 0;
                foreach (var occurrence in referencesOnTheSameLine)
                {
                    if (occurrence.ReferringSpan.LineSpanStart < current)
                    {
                        continue;
                    }

                    if (occurrence.ReferringSpan.LineSpanStart > current)
                    {
                        var length = occurrence.ReferringSpan.LineSpanStart - current;
                        length = Math.Min(length, text.Length);
                        var substring = text.Substring(current, length);
                        Write(writer, HttpUtility.HtmlEncode(substring));
                    }

                    Write(writer, "<i>");

                    var highlightStart = occurrence.ReferringSpan.LineSpanStart;
                    var highlightLength = occurrence.ReferringSpan.Length;
                    if (highlightStart >= 0 && highlightStart < text.Length && highlightStart + highlightLength <= text.Length)
                    {
                        var highlightText = text.Substring(highlightStart, highlightLength);
                        Write(writer, HttpUtility.HtmlEncode(highlightText));
                    }

                    Write(writer, "</i>");
                    current = occurrence.ReferringSpan.LineSpanEnd;
                }

                if (current < text.Length)
                {
                    Write(writer, HttpUtility.HtmlEncode(text.Substring(current, text.Length - current)));
                }
            }
        }

        private static void Write(TextWriter sw, string text)
        {
            sw.Write(text);
        }

        private static void Write(TextWriter sw, string format, params object[] args)
        {
            sw.Write(string.Format(format, args));
        }

        private static void WriteLine(TextWriter sw, string text)
        {
            sw.WriteLine(text);
        }

        private static void WriteLine(TextWriter sw, string format, params object[] args)
        {
            sw.WriteLine(string.Format(format, args));
        }
    }
}