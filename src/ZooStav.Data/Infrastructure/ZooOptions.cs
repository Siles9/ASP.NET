namespace ZooStav.Web.Infrastructure;

/// <summary>
/// Настройки приложения, включая роутинг поддомена животного.
/// Пример: https://raccoon.zoostav.ru/  (поддомен)  +  https://zoostav.ru  (главная зоопарка)
/// </summary>
public class ZooOptions
{
    public const string SectionName = "Zoo";

    /// <summary>Корневой домен зоопарка, например "zoostav.ru".</summary>
    public string RootDomain { get; set; } = "zoostav.ru";

    /// <summary>Официальный сайт зоопарка (ссылка на главную обязательна на странице животного).</summary>
    public string MainSiteUrl { get; set; } = "https://zoostav.ru";

    /// <summary>Схема, используемая при построении абсолютных ссылок на поддомены.</summary>
    public string Scheme { get; set; } = "https";

    /// <summary>Поддомены, зарезервированные под инфраструктуру (в них поддомен-роутинг отключён).</summary>
    public string[] IgnoredSubdomains { get; set; } = { "www", "api", "admin", "static", "cdn" };

    /// <summary>Считать ли "localhost" корневым доменом (для локальной отладки в браузере).</summary>
    public bool TreatLocalhostAsRoot { get; set; } = true;

    /// <summary>
    /// Домен cookie аутентификации. Для боевого домена — ".zoostav.ru", чтобы вход действовал
    /// на всех поддоменах. Пусто — cookie привязана к текущему хосту (режим отладки).
    /// </summary>
    public string CookieDomain { get; set; } = string.Empty;

    /// <summary>Перенаправлять HTTP на HTTPS (отключается в локальной отладке).</summary>
    public bool UseHttpsRedirection { get; set; } = true;
}

/// <summary>Настройки выдачи JWT-токенов.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://zoostav.ru";

    public string Audience { get; set; } = "https://zoostav.ru/api";

    /// <summary>Секрет подписи. В продакшене задаётся через переменные окружения или User Secrets.</summary>
    public string Key { get; set; } = "ZooStav-Super-Secret-Signing-Key-For-Demo-2026-Change-Me!";

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 14;
}
