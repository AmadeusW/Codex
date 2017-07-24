using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;
using Codex.Utilities;
using WebUI.Rendering;

namespace WebUI.Controllers
{
    public class SourceController : Controller
    {
        private readonly IStorage Storage;

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
                else
                {
                    return View((object)model);
                }
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
    }
}