using System.Web;
using System.Web.Optimization;

namespace WebUI
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/ScriptsExternal/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/ScriptsExternal/jquery.validate*"));

            bundles.Add(new ScriptBundle("~/bundles/header").Include(
                "~/node_modules/monaco-editor/dev/vs/loader.js",
                "~/Scripts/search.js",
                "~/ScriptsExternal/split.js",
                "~/Scripts/scripts.js",
                "~/Scripts/rpc.js",
                "~/Scripts/state.js",
                "~/Scripts/codexEditor.js",
                "~/Scripts/types.js"
                ));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                "~/ScriptsExternal/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/ScriptsExternal/bootstrap.js",
                      "~/ScriptsExternal/respond.js"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/site.css",
                      "~/Content/results.css",
                      "~/Content/styles.css",
                      "~/Content/header.css"
                      ));

        }
    }
}
