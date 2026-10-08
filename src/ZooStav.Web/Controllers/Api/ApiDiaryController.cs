using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;
using ZooStav.Web.Mapping;
using ZooStav.Web.Services;
using ZooStav.Web.ViewModels;
using ZooStav.Web.ViewModels.Api;

namespace ZooStav.Web.Controllers.Api;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class ApiDiaryController(
    ZooDbContext db,
    IDiaryService diary) : ControllerBase
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

        entry.Animal = animal;
        entry.User = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == entry.UserId, ct);

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

        return Ok(ApiResponse.OkMessage($"Запись #{id} удалена."));
    }

    [HttpGet("diary/types")]
    [AllowAnonymous]
    public IActionResult GetTypes() =>
        Ok(ApiResponse<List<object>>.Ok(Enum.GetValues<DiaryEntryType>()
            .Select(t => (object)new { value = t.ToString(), id = (int)t, title = t.Title() })
            .ToList()));
}
