using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace WebUI
{
    public static class ScopeHelpers
    {
        public const string RepoNameKey = "repoName";

        public static string[] GetSearchRepos(this Controller controller)
        {
            string repo = controller.RouteData.Values[RepoNameKey] as string;
            if (repo != null)
            {
                return new string[] { repo };
            }

            return new string[0];
        }

        public static string GetRootPrefix(this ViewContext viewContext)
        {
            return HttpUtility.JavaScriptStringEncode(GetRawRootPrefix(viewContext), true);
        }

        private static string GetRawRootPrefix(ViewContext viewContext)
        {
            var values = viewContext.RouteData.Values;
            string repo = values[RepoNameKey] as string;
            if (repo != null)
            {
                return $@"/Repos/{repo}";
            }

            return string.Empty;
        }
    }
}