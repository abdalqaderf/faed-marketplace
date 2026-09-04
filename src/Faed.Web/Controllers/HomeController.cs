using Faed.Web.Services.Marketplace;
using Faed.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace Faed.Web.Controllers
{
    public class HomeController(IPublicMarketplaceService marketplace) : Controller
    {
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var home = await marketplace.GetHomePageAsync(cancellationToken);
            return View(home);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        /// <summary>
        /// The shared empty/error page for non-2xx responses re-executed by
        /// <c>UseStatusCodePagesWithReExecute</c> — most importantly 404, which a guessed or stale listing/store
        /// slug now reaches instead of the framework's bare status page.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [Route("/status/{code:int}")]
        public IActionResult StatusCodePage(int code)
        {
            Response.StatusCode = code;
            ViewData["Title"] = code == 404 ? "Page not found" : "Something went wrong";
            return View("StatusCode", code);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
