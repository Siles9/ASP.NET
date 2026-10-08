using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;
using ZooStav.Web.Infrastructure;
using ZooStav.Web.Mapping;
using ZooStav.Web.Services;
using ZooStav.Web.ViewModels;

namespace ZooStav.Web.Controllers;

/// <summary>
/// Страница животного. Доступна по поддомену: https://raccoon.zoostav.ru/
/// На корневом домене — каталог животных по адресу /raccoon (и редирект на поддомен).
/// </summary>
public class AnimalController(
    ZooDbContext db,
    ZooSubdomain subdomains,
    IDiaryService diary,
    IOptionsSnapshot<ZooOptions> zooOptions,
    ILogger<AnimalController> logger) : Controller
{
    private readonly ZooOptions _zoo = zooOptions.Value;

    /// <summary>Список животных зоопарка (на корневом домене).</summary>
    [HttpGet]
    [Route("animals")]
    [Route("animal")]
    [Route("zoo/animals")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
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

        return View(model);
    }

    /// <summary>
    /// Персональная страница животного.
    /// Поддомен: https://raccoon.zoostav.ru/   (промежуточное ПО переписывает путь на /animal/raccoon)
    /// Корневой домен: https://zoostav.ru/raccoon -> 301 на поддомен
    /// Локальный стенд/песочница: /animal/raccoon
    /// </summary>
    [HttpGet]
    [Route("animal/{slug}", Name = "animal-page")]
    [Route("{slug}", Name = "animal-pretty-path", Order = 100)]
    public async Task<IActionResult> Page(string? slug, CancellationToken ct)
    {
        var hostSlug = subdomains.GetAnimalSlug(Request.Host.Host);
        var requestedSlug = hostSlug ?? slug;

        // Публичные страницы разрешены всем (в т.ч. неавторизованным посетителям).
        if (string.IsNullOrWhiteSpace(requestedSlug) || !SlugChecker.IsValid(requestedSlug))
        {
            // Корневой домен без указания животного — отправляем на каталог.
            return RedirectToAction(nameof(Index));
        }

        var animal = await db.Animals
            .Include(a => a.Media)
            .Include(a => a.Donations)
            .AsSplitQuery()   // две коллекции: разбиваем на отдельные SQL-запросы
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == requestedSlug, ct);

        if (animal is null)
        {
            return NotFound($"Животное «{requestedSlug}» не найдено в базе зоопарка.");
        }

        // На боевом корневом домене короткий адрес zoostav.ru/raccoon уводит на поддомен животного.
        if (hostSlug is null && subdomains.IsRootDomain(Request.Host.Host))
        {
            var target = subdomains.BuildAnimalUrl(animal.Slug);
            logger.LogInformation("Редирект на поддомен животного: {Target}", target);
            return Redirect(target);
        }

        var recentDiary = await diary.BuildQuery(new DiaryFilter { AnimalId = animal.Id })
            .Take(6)
            .AsNoTracking()
            .ToListAsync(ct);

        var model = new AnimalPageViewModel
        {
            Animal = animal,
            Photos = animal.Media.Where(m => m.Type == MediaType.Photo).ToList(),
            Videos = animal.Media.Where(m => m.Type == MediaType.Video).ToList(),
            RecentDiary = recentDiary,
            DiaryEntriesCount = await db.DiaryEntries.CountAsync(d => d.AnimalId == animal.Id, ct),
            DonationsTotal = animal.Donations.Where(d => d.Status == DonationStatus.Succeeded).Sum(d => d.Amount),
            DonationsCount = animal.Donations.Count(d => d.Status == DonationStatus.Succeeded),
            RecentDonations = animal.Donations
                .Where(d => d.Status == DonationStatus.Succeeded)
                .OrderByDescending(d => d.CreatedAtUtc)
                .Take(5)
                .ToList(),
            PublicUrl = subdomains.BuildAnimalUrlForRequest(animal.Slug, Request),
            ZooMainSiteUrl = _zoo.MainSiteUrl
        };

        return View(model);
    }

    /// <summary>Информация о поддомен-роутинге (демонстрация для защиты работы).</summary>
    [HttpGet("routing", Name = "animal-routing")]
    public async Task<IActionResult> Routing(CancellationToken ct)
    {
        var animals = await db.Animals.AsNoTracking().Select(a => a.Slug).ToListAsync(ct);
        ViewBag.Host = Request.Host.Value;
        ViewBag.DetectedSlug = subdomains.GetAnimalSlug(Request.Host.Host);
        ViewBag.RootDomain = _zoo.RootDomain;
        ViewBag.MainSite = _zoo.MainSiteUrl;
        ViewBag.AnimalUrls = animals.Select(subdomains.BuildAnimalUrl).ToList();
        ViewBag.AnimalUrlsLocal = animals.Select(s => subdomains.BuildAnimalUrlForRequest(s, Request)).ToList();
        return View();
    }
}
