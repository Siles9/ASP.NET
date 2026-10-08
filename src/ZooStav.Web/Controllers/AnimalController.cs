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

public class AnimalController(
    ZooDbContext db,
    ZooSubdomain subdomains,
    IDiaryService diary,
    IOptionsSnapshot<ZooOptions> zooOptions,
    ILogger<AnimalController> logger) : Controller
{
    private readonly ZooOptions _zoo = zooOptions.Value;

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

    [HttpGet]
    [Route("animal/{slug}", Name = "animal-page")]
    [Route("{slug}", Name = "animal-pretty-path", Order = 100)]
    public async Task<IActionResult> Page(string? slug, CancellationToken ct)
    {
        var hostSlug = subdomains.GetAnimalSlug(Request.Host.Host);
        var requestedSlug = hostSlug ?? slug;

        if (string.IsNullOrWhiteSpace(requestedSlug) || !SlugChecker.IsValid(requestedSlug))
        {
            return RedirectToAction(nameof(Index));
        }

        var animal = await db.Animals
            .Include(a => a.Media)
            .Include(a => a.Donations)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == requestedSlug, ct);

        if (animal is null)
        {
            return NotFound($"Животное «{requestedSlug}» не найдено в базе зоопарка.");
        }

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

}
