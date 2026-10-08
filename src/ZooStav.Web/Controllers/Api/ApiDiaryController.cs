using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;
using ZooStav.Web.Hubs;
using ZooStav.Web.Mapping;
using ZooStav.Web.Services;
using ZooStav.Web.ViewModels;
using ZooStav.Web.ViewModels.Api;

namespace ZooStav.Web.Controllers.Api;

/// <summary>
/// API дневника наблюдений (кормёжка, вакцинация, спаривание, потомство, болезни и пр.).
///
/// ЧТЕНИЕ:
///   GET /api/animals/{slug}/diary  — записи конкретного животного
///   GET /api/diary                 — все записи, с фильтрацией
///   GET /api/diary/{id}            — одна запись
/// ЗАПИСЬ (только роль Staff):
///   POST   /api/animals/{slug}/diary
///   PUT    /api/diary/{id}
///   DELETE /api/diary/{id}
///
/// Параметры фильтрации (query string):
///   type=Feeding|Vaccination|Mating|Offspring|Illness|Treatment|Measurement|Relocation|Observation|Other
///   dateFrom=2026-01-01  dateTo=2026-12-31
///   user=keeper@zoostav.ru | userId=&lt;guid&gt;
///   search=текст  sort=date_desc|date_asc|type|user|created_desc  page=1  pageSize=20
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ApiDiaryController(
    ZooDbContext db,
    IDiaryService diary,
    IAuditService audit,
    IHubContext<ZooHub> hub) : ControllerBase
{
    [HttpGet("animals/{slug}/diary")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAnimal(string slug, [FromQuery] DiaryFilter filter, CancellationToken ct)
    {
        var animal = await db.Animals.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug, ct);
        if (animal is null)
        {
            return NotFound(ApiResponse.Fail($"Животное «{slug}» не найдено."));
        }

        filter.AnimalId = animal.Id;
        filter.Slug = null;

        var page = await diary.GetAsync(filter, ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            animal = new { animal.Id, animal.Slug, animal.Name },
            filter = new
            {
                filter.Type,
                dateFrom = filter.DateFrom?.ToString("yyyy-MM-dd"),
                dateTo = filter.DateTo?.ToString("yyyy-MM-dd"),
                filter.UserName,
                filter.Search,
                filter.Sort,
                filter.Page,
                filter.PageSize
            },
            totalCount = page.TotalCount,
            totalPages = page.TotalPages,
            page = page.Page,
            pageSize = page.PageSize,
            items = page.Items.Select(x => x.ToDto()).ToList()
        }));
    }

    [HttpGet("diary")]
    [Authorize(Roles = ZooRoles.Staff)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] DiaryFilter filter, CancellationToken ct)
    {
        var page = await diary.GetAsync(filter, ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            totalCount = page.TotalCount,
            totalPages = page.TotalPages,
            page = page.Page,
            pageSize = page.PageSize,
            items = page.Items.Select(x => x.ToDto()).ToList()
        }));
    }

    [HttpGet("diary/{id:int}")]
    [Authorize(Roles = ZooRoles.Staff)]
    [ProducesResponseType(typeof(ApiResponse<ApiDiaryEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var entry = await db.DiaryEntries
            .Include(d => d.Animal)
            .Include(d => d.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        return entry is null
            ? NotFound(ApiResponse.Fail($"Запись #{id} не найдена."))
            : Ok(ApiResponse<ApiDiaryEntryDto>.Ok(entry.ToDto()));
    }

    [HttpPost("animals/{slug}/diary")]
    [Authorize(Roles = ZooRoles.Staff)]
    [ProducesResponseType(typeof(ApiResponse<ApiDiaryEntryDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(string slug, [FromBody] ApiDiaryCreateRequest request, CancellationToken ct)
    {
        var animal = await db.Animals.FirstOrDefaultAsync(a => a.Slug == slug, ct);
        if (animal is null)
        {
            return NotFound(ApiResponse.Fail($"Животное «{slug}» не найдено."));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(ApiResponse.Fail("Поле title обязательно."));
        }

        var entry = new DiaryEntry
        {
            AnimalId = animal.Id,
            Type = request.Type,
            OccurredAtUtc = DateTime.SpecifyKind(request.OccurredAtUtc?.ToUniversalTime() ?? DateTime.UtcNow, DateTimeKind.Utc),
            Title = request.Title,
            Description = request.Description,
            PerformedBy = string.IsNullOrWhiteSpace(request.PerformedBy)
                ? User.FindFirstValue("fullName") ?? User.Identity?.Name ?? string.Empty
                : request.PerformedBy,
            Location = request.Location,
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedVia = "Api"
        };

        await diary.AddAsync(entry, ct);
        await audit.WriteAsync("ApiDiaryCreate", true, $"API: создана запись дневника #{entry.Id} — {entry.Title}");

        entry.Animal = animal;
        entry.User = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == entry.UserId, ct);

        await hub.Clients.All.SendAsync("diaryEntryCreated", new
        {
            id = entry.Id,
            animalSlug = animal.Slug,
            type = entry.Type.ToString(),
            title = entry.Title,
            occurredAtUtc = entry.OccurredAtUtc
        }, ct);

        return CreatedAtAction(nameof(GetById), new { id = entry.Id }, ApiResponse<ApiDiaryEntryDto>.Ok(entry.ToDto()));
    }

    [HttpPut("diary/{id:int}")]
    [Authorize(Roles = ZooRoles.Staff)]
    [ProducesResponseType(typeof(ApiResponse<ApiDiaryEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ApiDiaryUpdateRequest request, CancellationToken ct)
    {
        var entry = await db.DiaryEntries.Include(d => d.Animal).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entry is null)
        {
            return NotFound(ApiResponse.Fail($"Запись #{id} не найдена."));
        }

        entry.Type = request.Type;
        entry.OccurredAtUtc = DateTime.SpecifyKind(request.OccurredAtUtc?.ToUniversalTime() ?? entry.OccurredAtUtc, DateTimeKind.Utc);
        entry.Title = string.IsNullOrWhiteSpace(request.Title) ? entry.Title : request.Title;
        entry.Description = request.Description;
        entry.PerformedBy = request.PerformedBy;
        entry.Location = request.Location;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("ApiDiaryUpdate", true, $"API: изменена запись дневника #{entry.Id} — {entry.Title}");

        return Ok(ApiResponse<ApiDiaryEntryDto>.Ok(entry.ToDto()));
    }

    [HttpDelete("diary/{id:int}")]
    [Authorize(Roles = ZooRoles.Staff)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var entry = await db.DiaryEntries.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entry is null)
        {
            return NotFound(ApiResponse.Fail($"Запись #{id} не найдена."));
        }

        db.DiaryEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("ApiDiaryDelete", true, $"API: удалена запись дневника #{id} — {entry.Title}");

        return Ok(ApiResponse.OkMessage($"Запись #{id} удалена."));
    }

    /// <summary>Справочник типов записей дневника (для клиентов и фильтров).</summary>
    [HttpGet("diary/types")]
    [AllowAnonymous]
    public IActionResult GetTypes() =>
        Ok(ApiResponse<List<object>>.Ok(Enum.GetValues<DiaryEntryType>()
            .Select(t => (object)new { value = t.ToString(), id = (int)t, title = t.Title() })
            .ToList()));
}
