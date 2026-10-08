using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;
using ZooStav.Web.Infrastructure;
using ZooStav.Web.Models;
using ZooStav.Web.ViewModels;

namespace ZooStav.Web.Controllers;

/// <summary>
/// Главная страница приложения (в демо — «витрина» зоопарка).
/// Именно отсюда идёт обязательная ссылка на официальный сайт зоопарка https://zoostav.ru
/// и ссылки на персональные поддомены животных.
/// </summary>
public class HomeController(
    ZooDbContext db,
    ZooSubdomain subdomains,
    IOptionsSnapshot<ZooOptions> zooOptions) : Controller
{
    private readonly ZooOptions _zoo = zooOptions.Value;

    [HttpGet]
    [Route("")]
    [Route("index")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        // Если открыт поддомен животного, показываем страницу животного.
        var hostSlug = subdomains.GetAnimalSlug(Request.Host.Host);
        if (hostSlug is not null)
        {
            // Подстраховка: если middleware не переписал путь, показываем страницу животного.
            return RedirectToAction("Page", "Animal", new { slug = hostSlug });
        }

        var animals = await db.Animals
            .Include(a => a.Media)
            .Include(a => a.DiaryEntries)
            .AsSplitQuery()
            .OrderBy(a => a.Id)
            .AsNoTracking()
            .ToListAsync(ct);

        var model = new AnimalListViewModel
        {
            MainSiteUrl = _zoo.MainSiteUrl,
            Animals = animals.Select(a => new AnimalCardViewModel
            {
                Animal = a,
                PhotoUrl = a.Media.FirstOrDefault(m => m.Type == MediaType.Photo)?.Url ?? "/images/placeholder.svg",
                PublicUrl = subdomains.BuildAnimalUrlForRequest(a.Slug, Request),
                DiaryEntriesCount = a.DiaryEntries.Count
            }).ToList()
        };

        ViewBag.Stats = new
        {
            Animals = animals.Count,
            DiaryEntries = animals.Sum(a => a.DiaryEntries.Count),
            Media = animals.Sum(a => a.Media.Count)
        };

        return View(model);
    }

    [HttpGet]
    [Route("about")]
    public IActionResult About() => View();

    [HttpGet("error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel
    {
        RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier
    });
}
