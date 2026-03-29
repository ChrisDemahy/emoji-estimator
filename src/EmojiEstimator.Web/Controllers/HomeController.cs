using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using EmojiEstimator.Web.Models;

namespace EmojiEstimator.Web.Controllers;

public class HomeController : Controller
{
    private static readonly HomePageViewModel IndexViewModel = new()
    {
        RouteTemplate = "/{username}/{repository}",
        ExampleRoutes =
        [
            "/dotnet/aspnetcore",
            "/openclaw/clawhub",
            "/prisma/web",
            "/apache/superset",
            "/chroma-core/chroma"
        ]
    };

    public IActionResult Index()
    {
        return View(IndexViewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
