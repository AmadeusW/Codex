using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using Codex.ObjectModel;
using Codex.Storage;
using WebUI.Models;

namespace WebUI.Controllers
{
    public class SearchController : Controller
    {
        private readonly ElasticsearchStorage storage;

        public SearchController(IStorage storage)
        {
            this.storage = (ElasticsearchStorage)storage;
        }

        // GET: Results
        [System.Web.Mvc.Route("repos/{repoName}/search/ResultsAsHtml")]
        [System.Web.Mvc.Route("search/ResultsAsHtml")]
        public async Task<ActionResult> ResultsAsHtml([FromUri(Name = "q")] string searchTerm)
        {
            try
            {
                Requests.LogRequest(this, searchTerm);
                searchTerm = HttpUtility.UrlDecode(searchTerm);

                if (string.IsNullOrWhiteSpace(searchTerm))
                {
                    //Still render view even if we have an invalid search term - it'll display a "results not found" message
                    Debug.WriteLine("GetSearchResult - searchTerm is null or whitespace");
                    return PartialView();
                }

                if (searchTerm.StartsWith("`"))
                {
                    return await TextSearchResults(searchTerm);
                }

                SymbolSearchResult searchResult = null;
                string term;
                Classification? classification;
                ParseSearchTerm(searchTerm, out term, out classification);

                Responses.PrepareResponse(Response);

                searchResult = await storage.SearchAsync(this.GetSearchRepos(), searchTerm);

                if (searchResult.Total == 0)
                {
                    return await TextSearchResults(searchTerm);
                }

                return PartialView(searchResult);
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        private async Task<ActionResult> TextSearchResults(string searchTerm)
        {
            var result = await storage.TextSearchAsync(this.GetSearchRepos(), searchTerm.TrimStart('`'), 500);
            if (result.Count == 0)
            {
                return PartialView(null);
            }

            SymbolReferenceResult referencesResult = FromTextEntries(result);
            referencesResult.SymbolName = searchTerm;

            return PartialView("~/Views/References/References.cshtml", (object)ReferencesController.GenerateReferencesHtml(referencesResult));
        }

        private SymbolReferenceResult FromTextEntries(List<TextReferenceEntry> entries)
        {
            return new SymbolReferenceResult()
            {
                Total = entries.Count,
                Entries = entries.Select(textEntry => new SymbolReferenceEntry()
                {
                    ReferringSpan = textEntry.ReferringSpan.CreateReference(new ReferenceSymbol()
                    {
                        ReferenceKind = "Text",
                        Id = SymbolId.UnsafeCreateWithValue(textEntry.Span.LineNumber.ToString())
                    }),
                    ReferringFilePath = textEntry.ReferringFilePath,
                    ReferringProjectId = textEntry.ReferringProjectId,
                }).ToList()
            };
        }

        private static void ParseSearchTerm(string searchTerm, out string term, out Classification? classification)
        {
            term = searchTerm;
            classification = null;

            var pieces = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 2)
            {
                Classification parsedClassification;
                if (Enum.TryParse(pieces[0], true, out parsedClassification))
                {
                    classification = parsedClassification;
                    term = pieces[1];
                }
            }
        }
    }
}