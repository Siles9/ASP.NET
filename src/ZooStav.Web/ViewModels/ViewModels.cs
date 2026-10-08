using System.ComponentModel.DataAnnotations;
using ZooStav.Web.Domain;
using ZooStav.Web.Services;

namespace ZooStav.Web.ViewModels;

public class AnimalPageViewModel
{
    public Animal Animal { get; set; } = new();
    public List<MediaItem> Photos { get; set; } = new();
    public List<MediaItem> Videos { get; set; } = new();
    public List<DiaryEntry> RecentDiary { get; set; } = new();
    public int DiaryEntriesCount { get; set; }
    public decimal DonationsTotal { get; set; }
    public int DonationsCount { get; set; }
    public List<Donation> RecentDonations { get; set; } = new();

    public string PublicUrl { get; set; } = string.Empty;

    public string ZooMainSiteUrl { get; set; } = "https://zoostav.ru";
}

public class DiaryIndexViewModel
{
    public PagedResult<DiaryEntry> Page { get; set; } = new(Array.Empty<DiaryEntry>(), 0, 1, 20);
    public DiaryFilter Filter { get; set; } = new();
    public Animal? Animal { get; set; }
    public List<string> AvailableUsers { get; set; } = new();

    public Dictionary<DiaryEntryType, int> TypeStats { get; set; } = new();
}

public class AnimalListViewModel
{
    public List<AnimalCardViewModel> Animals { get; set; } = new();
    public string MainSiteUrl { get; set; } = "https://zoostav.ru";
}

public class AnimalCardViewModel
{
    public Animal Animal { get; set; } = new();
    public string PhotoUrl { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public int DiaryEntriesCount { get; set; }
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Укажите e-mail")]
    [EmailAddress(ErrorMessage = "Некорректный e-mail")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите пароль")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Запомнить меня")]
    public bool RememberMe { get; set; } = true;

    public string? ReturnUrl { get; set; }

    public string? AnimalHost { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Укажите имя")]
    [MaxLength(128)]
    [Display(Name = "Имя и фамилия")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите e-mail")]
    [EmailAddress(ErrorMessage = "Некорректный e-mail")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите пароль")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Пароль должен быть не короче 6 символов")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Повторите пароль")]
    [Compare(nameof(Password), ErrorMessage = "Пароли не совпадают")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Даю согласие на обработку персональных данных")]
    public bool Consent { get; set; } = true;
}

public class DiaryCreateViewModel
{
    public int AnimalId { get; set; }

    public List<Animal> Animals { get; set; } = new();

    [Required(ErrorMessage = "Выберите тип записи")]
    [Display(Name = "Тип записи")]
    public DiaryEntryType Type { get; set; } = DiaryEntryType.Feeding;

    [Required(ErrorMessage = "Укажите дату и время")]
    [Display(Name = "Дата и время события")]
    public DateTime OccurredAt { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Укажите заголовок")]
    [MaxLength(300)]
    [Display(Name = "Заголовок")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Описание")]
    public string Description { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Кто выполнил")]
    public string PerformedBy { get; set; } = string.Empty;

    [MaxLength(200)]
    [Display(Name = "Место")]
    public string Location { get; set; } = string.Empty;
}

public class DonationCreateViewModel
{
    public int AnimalId { get; set; }

    public string AnimalName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Укажите имя")]
    [MaxLength(128)]
    [Display(Name = "Ваше имя (или название организации)")]
    public string DonorName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Некорректный e-mail")]
    [Display(Name = "E-mail для чека (необязательно)")]
    public string? DonorEmail { get; set; }

    [Range(1, 1000000, ErrorMessage = "Сумма должна быть больше нуля")]
    [Display(Name = "Сумма, ₽")]
    public decimal Amount { get; set; } = 500m;

    [Display(Name = "Назначение")]
    public string Purpose { get; set; } = DonationPurposes.Food;

    [MaxLength(512)]
    [Display(Name = "Сообщение животному")]
    public string Message { get; set; } = string.Empty;
}

public class AccessDeniedViewModel
{
    public string? AttemptedAction { get; set; }
    public string? ReturnUrl { get; set; }
    public bool IsAnonymous { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }

    public static ApiResponse<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponse<T> Fail(string error) => new() { Success = false, Error = error };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse OkMessage(string message) => new() { Success = true, Data = new { message } };
    public static new ApiResponse Fail(string error) => new() { Success = false, Error = error };
}
