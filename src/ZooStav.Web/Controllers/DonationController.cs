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

public class DonationController(
    ZooDbContext db,
    IPaymentService payments,
    IAuditService audit,
    ILogger<DonationController> logger) : Controller
{
    [HttpGet]
    [Route("donate/{slug?}")]
    public async Task<IActionResult> Create(string? slug, CancellationToken ct)
    {
        var animal = await db.Animals.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == (slug ?? "raccoon"), ct);

        if (animal is null)
        {
            return NotFound();
        }

        ViewBag.Animal = animal;
        ViewBag.DonationsTotal = await SumDonationsAsync(animal.Id, ct);

        return View(new DonationCreateViewModel
        {
            AnimalId = animal.Id,
            AnimalName = animal.Name,
            DonorName = User.Identity?.IsAuthenticated == true
                ? User.FindFirstValue("fullName") ?? User.Identity.Name ?? string.Empty
                : string.Empty,
            DonorEmail = User.FindFirstValue(ClaimTypes.Email)
        });
    }

    [HttpPost]
    [Route("donate/{slug?}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DonationCreateViewModel model, string? slug, CancellationToken ct)
    {
        var animal = await db.Animals.FirstOrDefaultAsync(a => a.Id == model.AnimalId, ct);
        if (animal is null)
        {
            return NotFound();
        }

        if (!DonationPurposes.All.Contains(model.Purpose))
        {
            ModelState.AddModelError(nameof(model.Purpose), "Недопустимое назначение доната.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Animal = animal;
            ViewBag.DonationsTotal = await SumDonationsAsync(animal.Id, ct);
            return View(model);
        }

        var payment = await payments.ProcessAsync(
            new PaymentRequest(animal.Id, model.Amount, model.Purpose, model.DonorName, model.DonorEmail, model.Message), ct);

        var donation = new Donation
        {
            AnimalId = animal.Id,
            DonorName = model.DonorName,
            DonorEmail = model.DonorEmail,
            Amount = model.Amount,
            Purpose = model.Purpose,
            Message = model.Message,
            Status = payment.Success ? DonationStatus.Succeeded : DonationStatus.Failed,
            PaymentMethod = "free",
            PaymentReference = payment.Reference
        };

        db.Donations.Add(donation);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync("Donation", payment.Success,
            $"Донат {donation.Amount:0.##} ₽ на «{DonationPurposes.Title(donation.Purpose)}» для {animal.Name} ({payment.Reference})");

        logger.LogInformation("Донат #{Id} сохранён: {Amount} ₽ ({Purpose})", donation.Id, donation.Amount, donation.Purpose);

        return RedirectToAction(nameof(Thanks), new { id = donation.Id });
    }

    [HttpGet]
    [Route("donate/thanks/{id:int}")]
    public async Task<IActionResult> Thanks(int id, CancellationToken ct)
    {
        var donation = await db.Donations
            .Include(d => d.Animal)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (donation is null)
        {
            return NotFound();
        }

        return View(donation);
    }

    [HttpGet]
    [Route("donations")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var donations = await db.Donations
            .Include(d => d.Animal)
            .Where(d => d.Status == DonationStatus.Succeeded)
            .OrderByDescending(d => d.CreatedAtUtc)
            .Take(100)
            .AsNoTracking()
            .ToListAsync(ct);

        ViewBag.Total = donations.Sum(d => d.Amount);
        return View(donations);
    }

    private async Task<decimal> SumDonationsAsync(int animalId, CancellationToken ct)
    {
        var amounts = await db.Donations
            .Where(d => d.AnimalId == animalId && d.Status == DonationStatus.Succeeded)
            .Select(d => d.Amount)
            .ToListAsync(ct);

        return amounts.Sum();
    }
}
