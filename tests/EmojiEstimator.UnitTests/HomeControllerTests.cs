using EmojiEstimator.Web.Controllers;
using EmojiEstimator.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EmojiEstimator.UnitTests;

public sealed class HomeControllerTests
{
    [Fact]
    public void IndexReturnsLandingPageModel()
    {
        var controller = new HomeController();

        var result = controller.Index();

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<HomePageViewModel>(viewResult.Model);
        Assert.Equal("/{username}/{repository}", model.RouteTemplate);
        Assert.Equal(
        [
            "/dotnet/aspnetcore",
            "/openclaw/clawhub",
            "/prisma/web",
            "/apache/superset",
            "/chroma-core/chroma"
        ],
        model.ExampleRoutes);
    }
}
