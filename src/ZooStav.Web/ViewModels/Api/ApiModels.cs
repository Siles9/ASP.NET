using System.ComponentModel.DataAnnotations;
using ZooStav.Web.Domain;

namespace ZooStav.Web.ViewModels.Api;

public class ApiRegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string FullName { get; set; } = string.Empty;
}

public class ApiLoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; } = true;
}

public class ApiRefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class ApiAuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAtUtc { get; set; }
    public ApiUserDto User { get; set; } = new();
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class ApiUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public DateTime CreatedAtUtc { get; set; }
}

public class ApiAnimalDto
{
    public int Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;
    public string LatinName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string History { get; set; } = string.Empty;
    public string Habitat { get; set; } = string.Empty;
    public string Diet { get; set; } = string.Empty;
    public string Enclosure { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public int? AgeYears { get; set; }
    public string WebcamUrl { get; set; } = string.Empty;
    public string SubdomainUrl { get; set; } = string.Empty;
    public string ZooMainSiteUrl { get; set; } = string.Empty;

    public bool DiaryAvailable { get; set; }

    public List<ApiMediaDto> Photos { get; set; } = new();
    public List<ApiMediaDto> Videos { get; set; } = new();
    public ApiDonationSummaryDto Donations { get; set; } = new();
}

public class ApiMediaDto
{
    public int Id { get; set; }
    public string Type { get; set; } = "photo";
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string PosterUrl { get; set; } = string.Empty;
}

public class ApiDonationSummaryDto
{
    public decimal Total { get; set; }
    public int Count { get; set; }
    public decimal FoodTotal { get; set; }
    public decimal TreatmentTotal { get; set; }
    public decimal CareTotal { get; set; }
    public List<ApiDonationDto> Recent { get; set; } = new();
}

public class ApiDiaryEntryDto
{
    public int Id { get; set; }
    public int AnimalId { get; set; }
    public string AnimalSlug { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string TypeTitle { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string CreatedVia { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class ApiDiaryCreateRequest
{
    public int? AnimalId { get; set; }
    public string? AnimalSlug { get; set; }

    [Required]
    public DiaryEntryType Type { get; set; }

    public DateTime? OccurredAtUtc { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(200)]
    public string PerformedBy { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;
}

public class ApiDiaryUpdateRequest
{
    public DiaryEntryType Type { get; set; }
    public DateTime? OccurredAtUtc { get; set; }

    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(200)]
    public string PerformedBy { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;
}

public class ApiDonationCreateRequest
{
    public int? AnimalId { get; set; }
    public string? AnimalSlug { get; set; }

    [Required, MaxLength(128)]
    public string DonorName { get; set; } = string.Empty;

    [EmailAddress, MaxLength(256)]
    public string? DonorEmail { get; set; }

    [Range(1, 1000000)]
    public decimal Amount { get; set; }

    public string Purpose { get; set; } = DonationPurposes.Food;

    [MaxLength(512)]
    public string Message { get; set; } = string.Empty;
}

public class ApiDonationDto
{
    public int Id { get; set; }
    public int AnimalId { get; set; }
    public string DonorName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "RUB";
    public string Purpose { get; set; } = string.Empty;
    public string PurposeTitle { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentReference { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
