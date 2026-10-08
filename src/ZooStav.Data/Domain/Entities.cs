using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ZooStav.Web.Domain;

/// <summary>Пользователь системы (посетитель или работник зоопарка).</summary>
public class ZooUser : IdentityUser
{
    [MaxLength(128)]
    public string FullName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Записи дневника, созданные этим пользователем.</summary>
    public ICollection<DiaryEntry> DiaryEntries { get; set; } = new List<DiaryEntry>();
}

/// <summary>Роли: Visitor (посетитель) и Staff (работник зоопарка).</summary>
public static class ZooRoles
{
    public const string Visitor = "Visitor";
    public const string Staff = "Staff";

    /// <summary>Роли, которые имеют доступ к дневнику наблюдений (чтение и запись).</summary>
    public const string StaffOnly = Staff;

    public static readonly string[] All = { Visitor, Staff };
}

/// <summary>Животное (особь) или группа животных (популяция) зоопарка.</summary>
public class Animal
{
    public int Id { get; set; }

    /// <summary>Латиница-ключ поддомена, например "raccoon" -> https://raccoon.zoostav.ru/</summary>
    [Required, MaxLength(64)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Енот</summary>
    [Required, MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Енот-полоскун</summary>
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

    /// <summary>Адрес веб-камеры вольера (используется на странице животного).</summary>
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

    /// <summary>Путь к файлу в wwwroot или абсолютный URL (для видео с внешнего хостинга).</summary>
    [MaxLength(512)]
    public string Url { get; set; } = string.Empty;

    [MaxLength(512)]
    public string PosterUrl { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Тип записи в дневнике наблюдений.</summary>
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

/// <summary>Запись дневника особи/популяции: кормёжка, вакцинация, спаривание и пр.</summary>
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

    /// <summary>
    /// Нормализованный текст для поиска (заголовок + описание, нижний регистр).
    /// Заполняется автоматически в ZooDbContext.SaveChanges: нужен потому, что функция LOWER()
    /// в SQLite не обрабатывает кириллицу, а поиск должен работать одинаково на разных СУБД.
    /// </summary>
    [MaxLength(1200)]
    public string SearchText { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? UserId { get; set; }
    public ZooUser? User { get; set; }

    [MaxLength(200)]
    public string Location { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Кем создана запись: через веб-интерфейс (MVC) или через REST API.</summary>
    [MaxLength(16)]
    public string CreatedVia { get; set; } = "Web";
}

public enum DonationStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2
}

/// <summary>Донат на корм / лечение животного.</summary>
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

/// <summary>Журнал действий (для работников зоопарка).</summary>
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
