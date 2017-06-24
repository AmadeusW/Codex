using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;

namespace WebUI.Controllers
{
    public class DownloadController : Controller
    {
        private readonly IStorage Storage;

        public DownloadController(IStorage storage)
        {
            Storage = storage;
        }

        [Route("download/{projectId}")]
        public async Task<ActionResult> Download(string projectId, string filePath)
        {
            try
            {
                Requests.LogRequest(this);
                var boundSourceFile = await Storage.GetBoundSourceFileAsync(projectId, filePath);
                if (boundSourceFile == null)
                {
                    return Responses.Message($"File {filePath} not found in project {projectId}.");
                }

                Responses.PrepareResponse(Response);

                var fileText = await boundSourceFile.SourceFile.GetContentsAsync();
                var bytes = Encoding.UTF8.GetBytes(fileText);
                return new FileContentResult(bytes, "text/plain")
                {
                    FileDownloadName = Path.GetFileName(filePath)
                };
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }
    }
}