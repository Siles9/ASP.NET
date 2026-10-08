namespace ZooStav.Web.Infrastructure;

public class ZooOptions
{
    public const string SectionName = "Zoo";

    public string RootDomain { get; set; } = "zoostav.ru";

    public string MainSiteUrl { get; set; } = "https://zoostav.ru";

    public string Scheme { get; set; } = "https";

    public string[] IgnoredSubdomains { get; set; } = { "www", "api", "admin", "static", "cdn" };

    public bool TreatLocalhostAsRoot { get; set; } = true;

    public string CookieDomain { get; set; } = string.Empty;

    public bool UseHttpsRedirection { get; set; } = true;
}

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://zoostav.ru";

    public string Audience { get; set; } = "https://zoostav.ru/api";

    public string Key { get; set; } = "ZooStav-Super-Secret-Signing-Key-For-Demo-2026-Change-Me!";

    public int AccessTokenMinutes { get; set; } = 60;

    public int RefreshTokenDays { get; set; } = 14;
}
