using Microsoft.Extensions.Options;
using ZooStav.Web.Infrastructure;
using Xunit;

namespace ZooStav.Tests;

public class SubdomainRoutingTests
{
    private static ZooSubdomain Create(params string[] ignored)
    {
        var options = new ZooOptions
        {
            RootDomain = "zoostav.ru",
            MainSiteUrl = "https://zoostav.ru",
            Scheme = "https",
            TreatLocalhostAsRoot = true,
            IgnoredSubdomains = ignored.Length > 0 ? ignored : new[] { "www", "api", "admin" }
        };

        return new ZooSubdomain(Options.Create(options));
    }

    [Theory]
    [InlineData("raccoon.zoostav.ru", "raccoon")]
    [InlineData("RACCOON.ZOOSTAV.RU", "raccoon")]
    [InlineData("raccoon.zoostav.ru:5000", "raccoon")]
    [InlineData("raccoon.localhost", "raccoon")]
    [InlineData("raccoon.localhost:5045", "raccoon")]
    [InlineData("raccoon.localtest.me", "raccoon")]
    public void GetAnimalSlug_Определяет_животное_по_поддомену(string host, string expected)
    {
        Assert.Equal(expected, Create().GetAnimalSlug(host));
    }

    [Theory]
    [InlineData("zoostav.ru")]
    [InlineData("www.zoostav.ru")]
    [InlineData("api.zoostav.ru")]
    [InlineData("admin.zoostav.ru")]
    [InlineData("localhost")]
    [InlineData("localhost:5045")]
    [InlineData("127.0.0.1")]
    [InlineData("")]
    [InlineData(null)]
    public void GetAnimalSlug_Возвращает_null_для_корневого_и_служебных_доменов(string? host)
    {
        Assert.Null(Create().GetAnimalSlug(host));
    }

    [Fact]
    public void BuildAnimalUrl_Строит_адрес_поддомена()
    {
        var subdomains = Create();

        Assert.Equal("https://raccoon.zoostav.ru/", subdomains.BuildAnimalUrl("raccoon"));
        Assert.True(subdomains.IsRootDomain("zoostav.ru"));
        Assert.True(subdomains.IsRootDomain("www.zoostav.ru"));
        Assert.False(subdomains.IsRootDomain("raccoon.zoostav.ru"));
    }

    [Fact]
    public void SlugChecker_Разрешает_только_корректные_слаги()
    {
        Assert.True(SlugChecker.IsValid("raccoon"));
        Assert.True(SlugChecker.IsValid("raccoon-2024"));
        Assert.False(SlugChecker.IsValid("a"));
        Assert.False(SlugChecker.IsValid("ракун"));
        Assert.False(SlugChecker.IsValid("../../etc/passwd"));
        Assert.False(SlugChecker.IsValid(null));
    }
}
