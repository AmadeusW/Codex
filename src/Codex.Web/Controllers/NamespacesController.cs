using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;
using WebUI.Rendering;

namespace WebUI.Controllers
{
    public class NamespacesController : Controller
    {
        private readonly IStorage Storage;

        public NamespacesController(IStorage storage)
        {
            Storage = storage;
        }

        [Route("namespaces/{projectId}")]
        public async Task<ActionResult> Namespaces(string projectId)
        {
            try
            {
                Requests.LogRequest(this);
                var renderer = new NamespacesRenderer(Storage, projectId);
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