using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;
using ZooStav.Web.Infrastructure;
using ZooStav.Web.Mapping;
using ZooStav.Web.ViewModels;
using ZooStav.Web.ViewModels.Api;

namespace ZooStav.Web.Controllers.Api;

/// <summary>
/// Публичный API информации о животных.
///
/// GET /api/animals            — список животных
/// GET /api/animals/{slug}     — подробная карточка (описание, история, фото, видео, веб-камера, донаты)
/// GET /api/animals/{slug}/media — только медиатека
///
/// Доступ: анонимно — только информационный блок;
///        с Bearer-токеном роли Staff — дополнительно флаг доступности дневника.
/// </summary>
[ApiController]
[Route("api/animals")]
[Produces("application/json")]
public class ApiAnimalsController(
    ZooDbContext db,
    ZooSubdomain subdomains) : ControllerBase
{
    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";
    private bool IsStaff => User.Identity?.IsAuthenticated == true && User.IsInRole(ZooRoles.Staff);

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<ApiAnimalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var animals = await db.Animals
            .Include(a => a.Media)
            .Include(a => a.Donations)
            .AsSplitQuery()
            .OrderBy(a => a.Id)
            .AsNoTracking()
            .ToListAsync(ct);

        var dto = animals.Select(a => a.ToDto(subdomains, IsStaff, BaseUrl)).ToList();
        return Ok(ApiResponse<List<ApiAnimalDto>>.Ok(dto));
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ApiAnimalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var animal = await db.Animals
            .Include(a => a.Media)
            .Include(a => a.Donations)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == slug, ct);

        if (animal is null)
        {
            return NotFound(ApiResponse.Fail($"Животное «{slug}» не найдено."));
        }

        return Ok(ApiResponse<ApiAnimalDto>.Ok(animal.ToDto(subdomains, IsStaff, BaseUrl)));
    }

    [HttpGet("{slug}/media")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<ApiMediaDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMedia(string slug, CancellationToken ct)
    {
        var animal = await db.Animals
            .Include(a => a.Media)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == slug, ct);

        if (animal is null)
        {
            return NotFound(ApiResponse.Fail($"Животное «{slug}» не найдено."));
        }

        var media = animal.Media.OrderBy(m => m.Type).ThenBy(m => m.Id).Select(m => m.ToDto(BaseUrl)).ToList();
        return Ok(ApiResponse<List<ApiMediaDto>>.Ok(media));
    }
}
