using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;
using WebUI.Rendering;

namespace WebUI.Controllers
{
    public class ProjectExplorerController : Controller
    {
        private readonly IStorage Storage;

        public ProjectExplorerController(IStorage storage)
        {
            Storage = storage;
        }

        [Route("projectexplorer/{projectId}")]
        [Route("repos/{repoName}/projectexplorer/{projectId}")]
        public async Task<ActionResult> ProjectExplorer(string projectId)
        {
            try
            {
                Requests.LogRequest(this);
                var projectContents = await Storage.GetProjectContentsAsync(this.GetSearchRepos(), projectId);
                var referencingProjects = await Storage.GetReferencingProjects(projectId);
                var renderer = new ProjectExplorerRenderer(projectContents, referencingProjects);
                var text = renderer.GenerateProjectExplorer();

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