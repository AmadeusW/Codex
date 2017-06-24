using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;

namespace WebUI.Controllers
{
    public class AboutController : Controller
    {
        private readonly IStorage Storage;

        public AboutController(IStorage storage)
        {
            Storage = storage;
        }

        [Route("about")]
        public async Task<ActionResult> About()
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