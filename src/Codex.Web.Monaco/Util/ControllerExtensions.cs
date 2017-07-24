using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Codex.Web.Monaco.Util
{
    public static partial class ControllerExtensions
    {
        public static string RenderPartialViewToString(this ControllerBase controller, PartialViewResult viewResult)
        {
            if (viewResult == null)
            {
                return string.Empty;
            }

            using (StringWriter sw = new StringWriter())
            {
                ViewContext viewContext = new ViewContext(controller.ControllerContext, viewResult.View, controller.ViewData, controller.TempData, sw);
                // copy model state items to the html helper 
                foreach (var item in viewContext.Controller.ViewData.ModelState)
                    if (!viewContext.ViewData.ModelState.Keys.Contains(item.Key))
                    {
                        viewContext.ViewData.ModelState.Add(item);
                    }


                viewResult.View.Render(viewContext, sw);

                return sw.GetStringBuilder().ToString();
            }
        }
    }
}