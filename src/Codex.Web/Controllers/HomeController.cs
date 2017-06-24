using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Codex.ObjectModel;
using WebUI.Models;
using WebUI.Util;

namespace WebUI.Controllers
{
    public class HomeController : Controller
    {
        private readonly IStorage storage;

        public HomeController(IStorage storage)
        {
            this.storage = storage;
        }

        public async Task<ActionResult> Index()
        {
            try
            {
                Requests.LogRequest(this);
                var stateModel = await GetModel();
                return View(stateModel);
            }
            catch (Exception ex)
            {
                return Responses.Exception(ex);
            }
        }

        private async Task<StateModel> GetModel()
        {
            var model = new StateModel();
            model.windowTitle = "'Index'";

            PopulateModelFromParameters(model);
            await InferAdditionalInformation(model);
            QuoteAllFields(model);

            return model;
        }

        private void PopulateModelFromParameters(StateModel model)
        {
            if (this.Request.QueryString.Count > 0)
            {
                foreach (var key in this.Request.QueryString.AllKeys)
                {
                    var value = this.Request.Unvalidated[key];
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    switch (key)
                    {
                        case "left":
                            model.leftPaneContent = value;
                            break;
                        case "right":
                            model.rightPaneContent = value;
                            break;
                        case "query":
                            model.leftPaneContent = "search";
                            model.searchText = value;
                            break;
                        case "leftProject":
                            if (model.leftPaneContent == null)
                            {
                                model.leftPaneContent = "project";
                            }

                            model.leftProjectId = value;
                            break;
                        case "leftSymbol":
                            model.leftPaneContent = "references";
                            model.leftSymbolId = value;
                            break;
                        case "projectScope":
                            model.projectScope = value;
                            break;
                        case "rightProject":
                            model.rightProjectId = value;
                            break;
                        case "file":
                            if (model.rightPaneContent == null)
                            {
                                model.rightPaneContent = "file";
                            }

                            model.filePath = value;
                            break;
                        case "rightSymbol":
                            model.rightPaneContent = "symbol";
                            model.rightSymbolId = value;
                            break;
                        case "line":
                            model.rightPaneContent = "line";
                            model.lineNumber = value;
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private async Task InferAdditionalInformation(StateModel model)
        {
            // if right projectId is missing, but left is present, use left
            if ((model.rightPaneContent == "file" || model.rightPaneContent == "line" || model.rightPaneContent == "symbol") && model.rightProjectId == null && model.leftProjectId != null)
            {
                model.rightProjectId = model.leftProjectId;
            }

            // if they passed a file, but rightSymbolId is null and we're in a FAR situation, use the leftSymbolId
            if (model.rightPaneContent == "file" && model.rightSymbolId == null && model.leftSymbolId != null)
            {
                model.rightSymbolId = model.leftSymbolId;
                model.rightPaneContent = "symbol";
            }

            // recover the file if they only passed the right project and symbol id
            if (model.rightPaneContent == "symbol" && model.rightProjectId != null && model.rightSymbolId != null && model.filePath == null)
            {
                model.filePath = await storage.GetFirstDefinitionFilePath(model.rightProjectId, model.rightSymbolId);
            }

            if (model.rightPaneContent == null)
            {
                model.rightPaneContent = "overview";
            }
        }

        private void QuoteAllFields(StateModel model)
        {
            model.filePath = Quote(model.filePath);
            model.leftPaneContent = Quote(model.leftPaneContent);
            model.leftProjectId = Quote(model.leftProjectId);
            model.leftSymbolId = Quote(model.leftSymbolId);
            model.projectScope = Quote(model.projectScope);
            model.lineNumber = Quote(model.lineNumber);
            model.rightPaneContent = Quote(model.rightPaneContent);
            model.rightProjectId = Quote(model.rightProjectId);
            model.rightSymbolId = Quote(model.rightSymbolId);
            model.searchText = Quote(model.searchText);
        }

        private string Quote(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "null";
            }

            text = text.AsJavaScriptStringEncoded();

            return "'" + text + "'";
        }
    }
}