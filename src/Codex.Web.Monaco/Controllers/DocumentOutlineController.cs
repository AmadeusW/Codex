using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;
using WebUI.Rendering;

namespace WebUI.Controllers
{
    public class DocumentOutlineController : Controller
    {
        private readonly IStorage Storage;

        public DocumentOutlineController(IStorage storage)
        {
            Storage = storage;
        }

        [Route("documentoutline/{projectId}")]
        public async Task<ActionResult> DocumentOutline(string projectId, string filePath)
        {
            try
            {
                Requests.LogRequest(this);
                var boundSourceFile = await Storage.GetBoundSourceFileAsync(
                    new string[0], 
                    projectId, 
                    filePath, 
                    includeDefinitions: true);
                if (boundSourceFile == null)
                {
                    return PartialView("~/Views/DocumentOutline/DocumentOutline.cshtml", new EditorModel { Error = $"Bound source file for {filePath} in {projectId} not found." });
                }

                var renderer = new DocumentOutlineRenderer(projectId, boundSourceFile);
                var text = renderer.Generate();

                Responses.PrepareResponse(Response);

                return PartialView((object)text);
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }
    }
}