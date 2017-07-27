using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;
using System.Linq;
using Codex.Web.Monaco.Models;

namespace WebUI.Controllers
{
    public class ToolTipController : Controller
    {
        private readonly IStorage Storage;

        public ToolTipController(IStorage storage)
        {
            Storage = storage;
        }

        [Route("repos/{repoName}/tooltip/{projectId}")]
        [Route("tooltip/{projectId}")]
        public async Task<ActionResult> GetToolTip(string projectId, string symbolId)
        {
            try
            {
                Requests.LogRequest(this);

                var definitionResult = await (Storage).GetDefinitionsAsync(this.GetSearchRepos(), projectId, symbolId);
                var definitionSpan = definitionResult?.FirstOrDefault()?.Span;
                var definition = definitionSpan?.Definition;

                Responses.PrepareResponse(Response);

                // Calling back end once again to get 'LineSpanText'.
                // Not good, but leaving for now.
                var actualDefinitions =
                    (await Storage.GetReferencesToSymbolAsync(new Symbol()
                    {
                        ProjectId = projectId,
                        Id = SymbolId.UnsafeCreateWithValue(symbolId),
                        Kind = nameof(ReferenceKind.Definition)
                    })).Entries.FirstOrDefault();

                var toolTip =
                    definitionSpan != null
                    ? new ToolTipModel()
                    {
                        comment = definition.Comment,
                        definitionText = actualDefinitions?.Span.LineSpanText,
                        fullName = definition.DisplayName,
                        projectId = definition.ProjectId,
                        symbolKind = definition.Kind,
                        typeName = definition.TypeName,
                    }
                    : new ToolTipModel() { };

                return WrapTheModel(toolTip);

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
    }
}