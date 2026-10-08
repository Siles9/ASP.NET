using ZooStav.Web.Domain;
using ZooStav.Web.Infrastructure;
using ZooStav.Web.Services;
using ZooStav.Web.ViewModels.Api;

namespace ZooStav.Web.Mapping;

/// <summary>Преобразование сущностей в DTO для REST API.</summary>
public static class ZooMapper
{
    public static ApiAnimalDto ToDto(this Animal animal, ZooSubdomain subdomains, bool diaryAvailable, string? baseUrl = null)
    {
        var age = animal.BirthDate is null
            ? (int?)null
            : Math.Max(0, (int)((DateTime.UtcNow - animal.BirthDate.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365.25));

        var donations = animal.Donations ?? new List<Donation>();

        return new ApiAnimalDto
        {
            Id = animal.Id,
            Slug = animal.Slug,
            Name = animal.Name,
            Species = animal.Species,
            LatinName = animal.LatinName,
            Summary = animal.Summary,
            History = animal.History,
            Habitat = animal.Habitat,
            Diet = animal.Diet,
            Enclosure = animal.Enclosure,
            Gender = animal.Gender,
            Status = animal.Status,
            BirthDate = animal.BirthDate,
            AgeYears = age,
            WebcamUrl = Resolve(animal.WebcamUrl, baseUrl),
            SubdomainUrl = subdomains.BuildAnimalUrl(animal.Slug),
            ZooMainSiteUrl = subdomains.MainSiteUrl,
            DiaryAvailable = diaryAvailable,
            Photos = animal.Media.Where(m => m.Type == MediaType.Photo).Select(m => m.ToDto(baseUrl)).ToList(),
            Videos = animal.Media.Where(m => m.Type == MediaType.Video).Select(m => m.ToDto(baseUrl)).ToList(),
            Donations = new ApiDonationSummaryDto
            {
                Total = donations.Where(d => d.Status == DonationStatus.Succeeded).Sum(d => d.Amount),
                Count = donations.Count(d => d.Status == DonationStatus.Succeeded),
                FoodTotal = donations.Where(d => d.Status == DonationStatus.Succeeded && d.Purpose == DonationPurposes.Food).Sum(d => d.Amount),
                TreatmentTotal = donations.Where(d => d.Status == DonationStatus.Succeeded && d.Purpose == DonationPurposes.Treatment).Sum(d => d.Amount),
                CareTotal = donations.Where(d => d.Status == DonationStatus.Succeeded && d.Purpose == DonationPurposes.Care).Sum(d => d.Amount),
                Recent = donations
                    .Where(d => d.Status == DonationStatus.Succeeded)
                    .OrderByDescending(d => d.CreatedAtUtc)
                    .Take(10)
                    .Select(d => d.ToDto())
                    .ToList()
            }
        };
    }

    public static ApiMediaDto ToDto(this MediaItem media, string? baseUrl = null) => new()
    {
        Id = media.Id,
        Type = media.Type == MediaType.Video ? "video" : "photo",
        Title = media.Title,
        Url = Resolve(media.Url, baseUrl),
        PosterUrl = Resolve(media.PosterUrl, baseUrl)
    };

    public static ApiDiaryEntryDto ToDto(this DiaryEntry entry) => new()
    {
        Id = entry.Id,
        AnimalId = entry.AnimalId,
        AnimalSlug = entry.Animal?.Slug ?? string.Empty,
        Type = entry.Type.ToString(),
        TypeTitle = entry.Type.Title(),
        OccurredAtUtc = DateTime.SpecifyKind(entry.OccurredAtUtc, DateTimeKind.Utc),
        Title = entry.Title,
        Description = entry.Description,
        PerformedBy = entry.PerformedBy,
        Location = entry.Location,
        UserId = entry.UserId,
        UserName = entry.User?.UserName ?? entry.User?.Email,
        CreatedVia = entry.CreatedVia,
        CreatedAtUtc = DateTime.SpecifyKind(entry.CreatedAtUtc, DateTimeKind.Utc)
    };

    public static ApiDonationDto ToDto(this Donation donation) => new()
    {
        Id = donation.Id,
        AnimalId = donation.AnimalId,
        DonorName = donation.DonorName,
        Amount = donation.Amount,
        Currency = donation.Currency,
        Purpose = donation.Purpose,
        PurposeTitle = DonationPurposes.Title(donation.Purpose),
        Message = donation.Message,
        Status = donation.Status.ToString(),
        PaymentReference = donation.PaymentReference,
        CreatedAtUtc = DateTime.SpecifyKind(donation.CreatedAtUtc, DateTimeKind.Utc)
    };

    public static ApiUserDto ToDto(this ZooUser user, IReadOnlyList<string> roles) => new()
    {
        Id = user.Id,
        Email = user.Email ?? string.Empty,
        FullName = user.FullName,
        Roles = roles,
        CreatedAtUtc = DateTime.SpecifyKind(user.CreatedAtUtc, DateTimeKind.Utc)
    };

    /// <summary>Локальные ссылки делаем абсолютными (удобно для внешних API-клиентов).</summary>
    private static string Resolve(string url, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || baseUrl is null)
        {
            return url;
        }

        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : baseUrl.TrimEnd('/') + "/" + url.TrimStart('/');
    }
}
