using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;

namespace WebUI.Controllers
{
    public class OverviewController : Controller
    {
        private readonly IStorage Storage;

        public OverviewController(IStorage storage)
        {
            Storage = storage;
        }

        [Route("overview")]
        public async Task<ActionResult> Overview()
        {
            try
            {
                Requests.LogRequest(this);
                Responses.PrepareResponse(Response);

                return PartialView();
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }
    }
}