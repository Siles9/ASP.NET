using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ZooStav.Web.Domain;

public class ZooUser : IdentityUser
{
    [MaxLength(128)]
    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<DiaryEntry> DiaryEntries { get; set; } = new List<DiaryEntry>();
}

public static class ZooRoles
{
    public const string Visitor = "Visitor";
    public const string Staff = "Staff";

    public const string StaffOnly = Staff;

    public static readonly string[] All = { Visitor, Staff };
}

public class Animal
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Species { get; set; } = string.Empty;

    [MaxLength(200)]
    public string LatinName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string History { get; set; } = string.Empty;

    public string Habitat { get; set; } = string.Empty;

    public string Diet { get; set; } = string.Empty;

    [MaxLength(64)]
    public string Enclosure { get; set; } = string.Empty;

    public DateOnly? BirthDate { get; set; }

    [MaxLength(64)]
    public string Gender { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Status { get; set; } = string.Empty;

    [MaxLength(512)]
    public string WebcamUrl { get; set; } = string.Empty;

    public ICollection<MediaItem> Media { get; set; } = new List<MediaItem>();
    public ICollection<DiaryEntry> DiaryEntries { get; set; } = new List<DiaryEntry>();
    public ICollection<Donation> Donations { get; set; } = new List<Donation>();
}

public enum MediaType
{
    Photo = 0,
    Video = 1
}

public class MediaItem
{
    public int Id { get; set; }

    public int AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public MediaType Type { get; set; }

    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(512)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(512)]
    public string PosterUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum DiaryEntryType
{
    Feeding = 0,
    Vaccination = 1,
    Mating = 2,
    Offspring = 3,
    Illness = 4,
    Treatment = 5,
    Measurement = 6,
    Relocation = 7,
    Observation = 8,
    Other = 9
}

public class DiaryEntry
{
    public int Id { get; set; }

    public int AnimalId { get; set; }
    public Animal? Animal { get; set; }

    public DiaryEntryType Type { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [MaxLength(200)]
    public string PerformedBy { get; set; } = string.Empty;

    [MaxLength(1200)]
    public string SearchText { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? UserId { get; set; }
    public ZooUser? User { get; set; }

    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(16)]
    public string CreatedVia { get; set; } = "Web";
}

public enum DonationStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}

public class Donation
{
    public int Id { get; set; }

    public int AnimalId { get; set; }
    public Animal? Animal { get; set; }

    [MaxLength(128)]
    public string DonorName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? DonorEmail { get; set; }

    [MaxLength(512)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Purpose { get; set; } = DonationPurposes.Food;

    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "RUB";

    public DonationStatus Status { get; set; } = DonationStatus.Succeeded;

    [MaxLength(64)]
    public string PaymentMethod { get; set; } = "free";

    [MaxLength(64)]
    public string PaymentReference { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class DonationPurposes
{
    public const string Food = "food";
    public const string Treatment = "treatment";
    public const string Care = "care";

    public static readonly string[] All = { Food, Treatment, Care };

    public static string Title(string purpose) => purpose switch
    {
        Treatment => "Лечение",
        Care => "Уход и обустройство вольера",
        _ => "Корм"
    };
}

public class AuditLog
{
    public long Id { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(64)]
    public string Action { get; set; } = string.Empty;

    public bool Success { get; set; }

    [MaxLength(64)]
    public string? UserId { get; set; }

    [MaxLength(128)]
    public string? UserName { get; set; }

    [MaxLength(32)]
    public string? Role { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(512)]
    public string Details { get; set; } = string.Empty;
}
