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
[Route("api/donations")]
[Produces("application/json")]
public class ApiDonationsController(
    ZooDbContext db,
    IPaymentService payments,
    ILogger<ApiDonationsController> logger) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ApiDonationDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] ApiDonationCreateRequest request, CancellationToken ct)
    {
        var animal = request.AnimalId is not null
            ? await db.Animals.FirstOrDefaultAsync(a => a.Id == request.AnimalId, ct)
            : await db.Animals.FirstOrDefaultAsync(a => a.Slug == (request.AnimalSlug ?? "raccoon"), ct);

        if (animal is null)
        {
            return NotFound(ApiResponse.Fail("Животное не найдено."));
        }

        if (request.Amount <= 0)
        {
            return BadRequest(ApiResponse.Fail("Сумма доната должна быть больше нуля."));
        }

        if (!DonationPurposes.All.Contains(request.Purpose))
        {
            return BadRequest(ApiResponse.Fail($"Недопустимое назначение. Доступные: {string.Join(", ", DonationPurposes.All)}"));
        }

        var payment = await payments.ProcessAsync(new PaymentRequest(
            animal.Id, request.Amount, request.Purpose, request.DonorName, request.DonorEmail, request.Message), ct);

        var donation = new Donation
        {
            AnimalId = animal.Id,
            DonorName = request.DonorName,
            DonorEmail = request.DonorEmail,
            Amount = request.Amount,
            Purpose = request.Purpose,
            Message = request.Message,
            Status = payment.Success ? DonationStatus.Succeeded : DonationStatus.Failed,
            PaymentMethod = "free",
            PaymentReference = payment.Reference
        };

        db.Donations.Add(donation);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("API-донат #{Id}: {Amount} ₽ для {Animal}", donation.Id, donation.Amount, animal.Slug);

        return CreatedAtAction(nameof(GetByAnimal), new { slug = animal.Slug },
            ApiResponse<ApiDonationDto>.Ok(donation.ToDto()));
    }

    [HttpGet("{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByAnimal(string slug, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var animal = await db.Animals.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug, ct);
        if (animal is null)
        {
            return NotFound(ApiResponse.Fail($"Животное «{slug}» не найдено."));
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var query = db.Donations
            .Where(d => d.AnimalId == animal.Id && d.Status == DonationStatus.Succeeded)
            .OrderByDescending(d => d.CreatedAtUtc);

        var total = await query.CountAsync(ct);

        var sum = (await query.Select(d => d.Amount).ToListAsync(ct)).Sum();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            animal = new { animal.Id, animal.Slug, animal.Name },
            totalAmount = sum,
            totalCount = total,
            page,
            pageSize,
            items = items.Select(d => d.ToDto()).ToList()
        }));
    }

    [HttpGet("{slug}/summary")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ApiDonationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(string slug, CancellationToken ct)
    {
        var animal = await db.Animals.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug, ct);
        if (animal is null)
        {
            return NotFound(ApiResponse.Fail($"Животное «{slug}» не найдено."));
        }

        var donations = await db.Donations
            .Where(d => d.AnimalId == animal.Id && d.Status == DonationStatus.Succeeded)
            .OrderByDescending(d => d.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync(ct);

        var summary = new ApiDonationSummaryDto
        {
            Total = donations.Sum(d => d.Amount),
            Count = donations.Count,
            FoodTotal = donations.Where(d => d.Purpose == DonationPurposes.Food).Sum(d => d.Amount),
            TreatmentTotal = donations.Where(d => d.Purpose == DonationPurposes.Treatment).Sum(d => d.Amount),
            CareTotal = donations.Where(d => d.Purpose == DonationPurposes.Care).Sum(d => d.Amount),
            Recent = donations.Take(10).Select(d => d.ToDto()).ToList()
        };

        return Ok(ApiResponse<ApiDonationSummaryDto>.Ok(summary));
    }
}
