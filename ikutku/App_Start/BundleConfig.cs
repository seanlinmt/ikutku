using System.Web.Optimization;

namespace ikutku.App_Start
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            BundleTable.EnableOptimizations = true;

            var cssBundle = new Bundle("~/css/all");
            cssBundle.Include(
                "~/Content/css/style.css",
                "~/Content/css/jquery-ui-1.10.3.custom.css",
                "~/Content/jgrowl/jquery.jgrowl.css"
                );
            //cssBundle.Transforms.Add(cssTransformer);
            //cssBundle.Orderer = new DefaultBundleOrderer();

            bundles.Add(cssBundle);

            var jscoreBundle = new Bundle("~/js/corenew");
            jscoreBundle.IncludeDirectory("~/Scripts", "*.js");
            //jscoreBundle.Transforms.Add(jsTransformer);
            //jscoreBundle.Orderer = new DefaultBundleOrderer();
            bundles.Add(jscoreBundle);
        }
    }
}