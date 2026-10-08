using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;
using ZooStav.Web.Infrastructure;
using ZooStav.Web.Services;
using ZooStav.Web.ViewModels;

namespace ZooStav.Web.Controllers;

[Authorize]
public class DiaryController(
    ZooDbContext db,
    IDiaryService diary,
    IAuditService audit,
    ZooSubdomain subdomains) : Controller
{
    [HttpGet]
    [Route("diary")]
    [Route("diary/index")]
    public async Task<IActionResult> Index(
        string? slug = null,
        DiaryEntryType? type = null,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        string? user = null,
        string? search = null,
        string sort = "date_desc",
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        var hostSlug = subdomains.GetAnimalSlug(Request.Host.Host);

        var filter = new DiaryFilter
        {
            Slug = hostSlug ?? slug,
            Type = type,
            DateFrom = dateFrom,
            DateTo = dateTo,
            UserName = user,
            Search = search,
            Sort = sort,
            Page = page,
            PageSize = pageSize
        };
        filter.Normalize();

        var result = await diary.GetAsync(filter, ct);

        var animal = filter.Slug is null
            ? null
            : await db.Animals.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == filter.Slug, ct);

        var allStats = await db.DiaryEntries
            .Where(d => filter.Slug == null || d.Animal!.Slug == filter.Slug)
            .GroupBy(d => d.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var model = new DiaryIndexViewModel
        {
            Page = result,
            Filter = filter,
            Animal = animal,
            TypeStats = allStats.ToDictionary(x => x.Type, x => x.Count),
            AvailableUsers = await db.Users
                .Where(u => u.DiaryEntries.Any(d => filter.Slug == null || d.Animal!.Slug == filter.Slug))
                .Select(u => u.UserName!)
                .ToListAsync(ct),
            Feed = await db.DiaryEntries
                .Include(d => d.Animal)
                .OrderByDescending(d => d.CreatedAtUtc)
                .Take(5)
                .AsNoTracking()
                .ToListAsync(ct)
        };

        ViewBag.IsStaff = User.IsInRole(ZooRoles.Staff);
        ViewBag.AnimalId = animal?.Id;

        return View(model);
    }

    [HttpGet("diary/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var entry = await db.DiaryEntries
            .Include(d => d.Animal)
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (entry is null)
        {
            return NotFound();
        }

        ViewBag.IsStaff = User.IsInRole(ZooRoles.Staff);
        return View(entry);
    }

    [Authorize(Roles = ZooRoles.Staff)]
    [HttpGet("diary/create")]
    public async Task<IActionResult> Create(int? animalId, CancellationToken ct)
    {
        var animals = await db.Animals.AsNoTracking().OrderBy(a => a.Id).ToListAsync(ct);
        var animal = animalId is not null
            ? animals.FirstOrDefault(a => a.Id == animalId)
            : animals.FirstOrDefault(a => a.Slug == (subdomains.GetAnimalSlug(Request.Host.Host) ?? "raccoon"));

        return View(new DiaryCreateViewModel
        {
            AnimalId = animal?.Id ?? animals.FirstOrDefault()?.Id ?? 0,
            Animals = animals,
            OccurredAt = DateTime.Now,
            PerformedBy = User.FindFirstValue("fullName") ?? User.Identity?.Name ?? string.Empty
        });
    }

    [Authorize(Roles = ZooRoles.Staff)]
    [HttpPost("diary/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DiaryCreateViewModel model, CancellationToken ct)
    {
        if (!await db.Animals.AnyAsync(a => a.Id == model.AnimalId, ct))
        {
            ModelState.AddModelError(nameof(model.AnimalId), "Животное не найдено.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var entry = new DiaryEntry
        {
            AnimalId = model.AnimalId,
            Type = model.Type,
            OccurredAtUtc = DateTime.SpecifyKind(model.OccurredAt.ToUniversalTime(), DateTimeKind.Utc),
            Title = model.Title,
            Description = model.Description,
            PerformedBy = string.IsNullOrWhiteSpace(model.PerformedBy)
                ? User.FindFirstValue("fullName") ?? User.Identity?.Name ?? string.Empty
                : model.PerformedBy,
            Location = model.Location,
            UserId = userId,
            CreatedVia = "Web"
        };

        await diary.AddAsync(entry, ct);
        await audit.WriteAsync("DiaryCreate", true, $"Создана запись дневника #{entry.Id} ({entry.Type}) — {entry.Title}");

        TempData["Toast"] = $"Запись «{entry.Title}» добавлена в дневник.";

        var slug = subdomains.GetAnimalSlug(Request.Host.Host);
        return RedirectToAction(nameof(Index), new { slug });
    }

    [Authorize(Roles = ZooRoles.Staff)]
    [HttpGet("diary/edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var entry = await db.DiaryEntries.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entry is null)
        {
            return NotFound();
        }

        return View(new DiaryCreateViewModel
        {
            AnimalId = entry.AnimalId,
            Animals = await db.Animals.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct),
            Type = entry.Type,
            OccurredAt = DateTime.SpecifyKind(entry.OccurredAtUtc, DateTimeKind.Utc).ToLocalTime(),
            Title = entry.Title,
            Description = entry.Description,
            PerformedBy = entry.PerformedBy,
            Location = entry.Location
        });
    }

    [Authorize(Roles = ZooRoles.Staff)]
    [HttpPost("diary/edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, DiaryCreateViewModel model, CancellationToken ct)
    {
        var entry = await db.DiaryEntries.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entry is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        entry.Type = model.Type;
        entry.OccurredAtUtc = DateTime.SpecifyKind(model.OccurredAt.ToUniversalTime(), DateTimeKind.Utc);
        entry.Title = model.Title;
        entry.Description = model.Description;
        entry.PerformedBy = model.PerformedBy;
        entry.Location = model.Location;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DiaryUpdate", true, $"Изменена запись дневника #{entry.Id} — {entry.Title}");

        TempData["Toast"] = "Изменения сохранены.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = ZooRoles.Staff)]
    [HttpPost("diary/delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entry = await db.DiaryEntries.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entry is null)
        {
            return NotFound();
        }

        db.DiaryEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DiaryDelete", true, $"Удалена запись дневника #{id} — {entry.Title}");

        TempData["Toast"] = "Запись удалена.";
        var slug = subdomains.GetAnimalSlug(Request.Host.Host);
        return RedirectToAction(nameof(Index), new { slug });
    }
}
