using System.Text.RegularExpressions;

namespace ZooStav.Web.Infrastructure;

/// <summary>Проверка допустимости слага животного (используется в "красивом" маршруте /{slug}).</summary>
public static partial class SlugChecker
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,63}$", RegexOptions.IgnoreCase)]
    private static partial Regex SlugPattern();

    public static bool IsValid(string? slug) => !string.IsNullOrWhiteSpace(slug) && SlugPattern().IsMatch(slug);

    /// <summary>Приводит произвольную строку к слагу: "Енот-полоскун" -> "raccoon" (транслитерация не выполняется).</summary>
    public static string Normalize(string value)
    {
        var slug = value.Trim().ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\-]+", "-");
        slug = Regex.Replace(slug, "-{2,}", "-").Trim('-');
        return slug;
    }
}
