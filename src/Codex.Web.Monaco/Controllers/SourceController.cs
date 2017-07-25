using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Mvc.Html;
using System.Web.Razor.Parser.SyntaxTree;
using Codex.ObjectModel;
using Codex.Storage;
using Codex.Storage.ElasticProviders;
using Codex.Utilities;
using Codex.Web.Monaco.Models;
using Codex.Web.Monaco.Util;
using WebUI.Rendering;
using Span = Codex.Web.Monaco.Models.Span;

namespace WebUI.Controllers
{
    public class SourceController : Controller
    {
        private readonly IStorage Storage;
        private readonly ElasticsearchStorage EsStorage;

        private readonly static IEqualityComparer<SymbolReferenceEntry> m_referenceEquator = new EqualityComparerBuilder<SymbolReferenceEntry>()
            .CompareByAfter(rs => rs.Span.Reference.Id)
            .CompareByAfter(rs => rs.Span.Reference.ProjectId)
            .CompareByAfter(rs => rs.Span.Reference.ReferenceKind)
            .CompareByAfter(rs => rs.ReferringProjectId)
            .CompareByAfter(rs => rs.ReferringFilePath);

        public struct LinkEdit
        {
            public string Inserted;
            public int Offset;
            public int TruncateLength;
            public bool ReplacePrefix;
        }

        public Dictionary<string, LinkEdit> m_edits = new Dictionary<string, LinkEdit>(StringComparer.OrdinalIgnoreCase)
        {
            //{  "https://devdiv.visualstudio.com/DefaultCollection/DevDiv/_git/VS?path=",
            //    new LinkEdit() { Inserted = "/src", Offset = 3 } },
            //{  "https://mseng.visualstudio.com/DefaultCollection/VSIDEProj/_git/VSIDEProj.Threading#path=/src/",
            //    new LinkEdit() { TruncateLength = 11, Inserted = "?path=" } },
            //{  "https://mseng.visualstudio.com/DefaultCollection/VSIDEProj/_git/VSIDEProj.MEF#path=/src/",
            //    new LinkEdit() { ReplacePrefix = true, Inserted = "https://devdiv.visualstudio.com/DevDiv/_git/VSMEF?path=" } }
        };


        public SourceController(IStorage storage)
        {
            Storage = storage;
            EsStorage = (ElasticsearchStorage) storage;
        }

        [Route("repos/{repoName}/sourcecontent/{projectId}")]
        [Route("sourcecontent/{projectId}")]
        public async Task<ActionResult> Contents(string projectId, string filename)
        {
            try
            {
                Requests.LogRequest(this);

                if (string.IsNullOrEmpty(filename))
                {
                    return this.HttpNotFound();
                }

                return WrapTheModel(await GetSourceFileAsync(projectId, filename));
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        private JsonResult WrapTheModel(object model)
        {
            if (model == null)
            {
                return new JsonResult();
            }

            return new JsonResult()
            {
                JsonRequestBehavior = JsonRequestBehavior.AllowGet,
                Data = model,
            };
        }

        private async Task<SourceFileContentsModel> GetSourceFileAsync(string projectId, string filename)
        {
            var boundSourceFile = await Storage.GetBoundSourceFileAsync(this.GetSearchRepos(), projectId, filename);
            if (boundSourceFile == null)
            {
                return null;
            }

            Responses.PrepareResponse(Response);

            return new SourceFileContentsModel()
            {
                contents = await boundSourceFile.SourceFile.GetContentsAsync(),
            };
        }

        [Route("repos/{repoName}/source/{projectId}")]
        [Route("source/{projectId}")]
        public async Task<ActionResult> Index(string projectId, string filename, bool partial = false)
        {
            try
            {
                Requests.LogRequest(this);

                if (string.IsNullOrEmpty(filename))
                {
                    return this.HttpNotFound();
                }

                var boundSourceFile = await Storage.GetBoundSourceFileAsync(this.GetSearchRepos(), projectId, filename);
                if (boundSourceFile == null)
                {
                    return PartialView("~/Views/Source/Index.cshtml", new EditorModel { Error = $"Bound source file for {filename} in {projectId} not found." });
                }

                var renderer = new SourceFileRenderer(boundSourceFile, projectId);

                Responses.PrepareResponse(Response);

                var model = await renderer.RenderAsync();
                foreach (var editEntry in m_edits)
                {
                    if (model.WebLink?.StartsWith(editEntry.Key, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        var truncateLength = editEntry.Value.TruncateLength;
                        if (editEntry.Value.ReplacePrefix)
                        {
                            truncateLength = editEntry.Key.Length;
                        }

                        var start = model.WebLink.Substring(0, editEntry.Key.Length - truncateLength);
                        var end = model.WebLink.Substring(editEntry.Key.Length + editEntry.Value.Offset);

                        model.WebLink = start + editEntry.Value.Inserted + end;
                    }
                }

                if (partial)
                {
                    return PartialView("~/Views/Source/Index.cshtml", (object)model);
                }

                return View((object)model);
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        [Route("repos/{repoName}/definitionAtPosition/{projectId}")]
        [Route("definitionAtPosition/{projectId}")]
        public async Task<ActionResult> GoToDefinitionAtPositionAsync(string projectId, string filename, int position)
        {
            try
            {
                Requests.LogRequest(this);
                var boundSourceFile = await Storage.GetBoundSourceFileAsync(this.GetSearchRepos(), projectId, filename);
                if (boundSourceFile == null)
                {
                    return null;
                }

                var matchingSpans = boundSourceFile.FindOverlappingReferenceSpans(new Range(start: position, length: 0));
                var matchingSpan = matchingSpans.FirstOrDefault(span => span.Reference != null && !span.Reference.IsImplicitlyDeclared);

                if (matchingSpan == null)
                {
                    return null;
                }

                Responses.PrepareResponse(Response);

                return
                    WrapTheModel(new ResultModel()
                    {
                        url = $"/definitionscontents/{matchingSpan.Reference.ProjectId}?symbolId={matchingSpan.Reference.Id.Value}",
                        symbolId = matchingSpan.Reference.Id.Value,
                        projectId = matchingSpan.Reference.ProjectId
                    });
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        [Route("repos/{repoName}/definitions/{projectId}")]
        [Route("definitions/{projectId}")]
        public async Task<ActionResult> GoToDefinitionAsync(string projectId, string symbolId)
        {
            try
            {
                Requests.LogRequest(this);

                var definitions = await Storage.GetReferencesToSymbolAsync(
                    this.GetSearchRepos(),
                    new Symbol()
                    {
                        ProjectId = projectId,
                        Id = SymbolId.UnsafeCreateWithValue(symbolId),
                        Kind = nameof(ReferenceKind.Definition)
                    });

                definitions.Entries = definitions.Entries.Distinct(m_referenceEquator).ToList();

                if (definitions.Entries.Count == 1)
                {
                    var definitionReference = definitions.Entries[0];
                    return await Index(definitionReference.ReferringProjectId, definitionReference.File, partial: true);
                }
                else
                {
                    var definitionResult = await Storage.GetDefinitionsAsync(this.GetSearchRepos(), projectId, symbolId);
                    var symbolName = definitionResult?.FirstOrDefault()?.Span.Definition.DisplayName ?? symbolId;
                    definitions.SymbolName = symbolName ?? definitions.SymbolName;

                    if (definitions.Entries.Count == 0)
                    {
                        definitions = await Storage.GetReferencesToSymbolAsync(
                            this.GetSearchRepos(),
                            new Symbol()
                            {
                                ProjectId = projectId,
                                Id = SymbolId.UnsafeCreateWithValue(symbolId)
                            });
                    }

                    var referencesText = ReferencesController.GenerateReferencesHtml(definitions);
                    if (string.IsNullOrEmpty(referencesText))
                    {
                        referencesText = "No defintions found.";
                    }
                    else
                    {
                        referencesText = "<!--Definitions-->" + referencesText;
                    }

                    Responses.PrepareResponse(Response);

                    return PartialView("~/Views/References/References.cshtml", referencesText);
                }
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        [Route("definitionscontents/{projectId}")]
        public async Task<ActionResult> GoToDefinitionGetContentAsync(string projectId, string symbolId)
        {
            try
            {
                Requests.LogRequest(this);

                var definitions = await Storage.GetReferencesToSymbolAsync(
                    this.GetSearchRepos(),
                    new Symbol()
                    {
                        ProjectId = projectId,
                        Id = SymbolId.UnsafeCreateWithValue(symbolId),
                        Kind = nameof(ReferenceKind.Definition)
                    });

                definitions.Entries = definitions.Entries.Distinct(m_referenceEquator).ToList();

                if (definitions.Entries.Count == 1)
                {
                    var definitionReference = definitions.Entries[0];
                    var sourceFile = await GetSourceFileAsync(definitionReference.ReferringProjectId, definitionReference.File);
                    if (sourceFile != null)
                    {
                        var referringSpan = definitions.Entries[0].ReferringSpan;
                        var position = new Span()
                        {
                            lineNumber = referringSpan.LineNumber + 1,
                            column = referringSpan.LineSpanEnd + 1,
                            length = referringSpan.Length,
                        };
                        sourceFile.position = position;
                    }

                    return WrapTheModel(sourceFile);
                }
                else
                {
                    var definitionResult = await Storage.GetDefinitionsAsync(this.GetSearchRepos(), projectId, symbolId);
                    var symbolName = definitionResult?.FirstOrDefault()?.Span.Definition.DisplayName ?? symbolId;
                    definitions.SymbolName = symbolName ?? definitions.SymbolName;

                    if (definitions.Entries.Count == 0)
                    {
                        definitions = await Storage.GetReferencesToSymbolAsync(
                            this.GetSearchRepos(),
                            new Symbol()
                            {
                                ProjectId = projectId,
                                Id = SymbolId.UnsafeCreateWithValue(symbolId)
                            });
                    }

                    var referencesText = ReferencesController.GenerateReferencesHtml(definitions);
                    if (string.IsNullOrEmpty(referencesText))
                    {
                        referencesText = "No defintions found.";
                    }
                    else
                    {
                        referencesText = "<!--Definitions-->" + referencesText;
                    }

                    Responses.PrepareResponse(Response);

                    return PartialView("~/Views/References/References.cshtml", referencesText);
                }
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }
    }
}