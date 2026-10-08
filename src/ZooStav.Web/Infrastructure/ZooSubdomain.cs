using Microsoft.Extensions.Options;

namespace ZooStav.Web.Infrastructure;

/// <summary>
/// Работа с поддоменами зоопарка.
///
/// Правило роутинга:
///   zoostav.ru            -> главная страница зоопарка (внешняя ссылка https://zoostav.ru)
///   raccoon.zoostav.ru    -> персональная страница животного "raccoon" (наш сайт)
///   www./api./admin. ...  -> служебные поддомены, поддомен-роутинг не применяется
///
/// Для локальной отладки аналог: raccoon.localhost:5000 (браузеры резолвят *.localhost в 127.0.0.1).
/// </summary>
public class ZooSubdomain(IOptions<ZooOptions> options)
{
    private readonly ZooOptions _options = options.Value;

    public string RootDomain => _options.RootDomain;

    public string MainSiteUrl => _options.MainSiteUrl;

    /// <summary>Извлекает поддомен животного из host. Возвращает null, если запрос идёт на корневой домен.</summary>
    public string? GetAnimalSlug(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        // host может содержать порт: raccoon.localhost:5000
        var hostWithoutPort = host.Split(':')[0].Trim().TrimEnd('.').ToLowerInvariant();

        if (_options.TreatLocalhostAsRoot &&
            (hostWithoutPort == "localhost" || hostWithoutPort == "127.0.0.1"))
        {
            return null;
        }

        var root = _options.RootDomain.ToLowerInvariant();

        // Вариант 1: host заканчивается на корневой домен: raccoon.zoostav.ru
        if (hostWithoutPort.EndsWith("." + root, StringComparison.Ordinal))
        {
            var sub = hostWithoutPort[..^(root.Length + 1)];
            return Normalize(sub);
        }

        // Вариант 2: локальная отладка через *.localhost или *.localtest.me / nip.io
        foreach (var suffix in new[] { ".localhost", ".localtest.me", ".lvh.me", ".nip.io" })
        {
            if (hostWithoutPort.EndsWith(suffix, StringComparison.Ordinal))
            {
                return Normalize(hostWithoutPort[..^suffix.Length]);
            }
        }

        // Вариант 3: host не принадлежит корневому домену (например, домен песочницы) —
        // поддомен определяется только по первому сегменту, если он не является частью корня.
        if (!hostWithoutPort.Contains(root, StringComparison.Ordinal) &&
            (hostWithoutPort.EndsWith(".ru", StringComparison.Ordinal) || hostWithoutPort.Contains('.')))
        {
            // Внешний/отладочный домен: считаем его корневым, чтобы страница животного открывалась по /raccoon.
            return null;
        }

        return null;

        string? Normalize(string candidate)
        {
            candidate = candidate.Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(candidate))
            {
                return null;
            }

            // Отбрасываем вложенные поддомены: a.b.zoostav.ru -> b рассматриваем как один уровень
            var firstSegment = candidate.Split('.')[0];
            if (firstSegment.Length == 0 ||
                _options.IgnoredSubdomains.Contains(firstSegment, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            return firstSegment;
        }
    }

    /// <summary>Строит абсолютный адрес страницы животного на его поддомене.</summary>
    public string BuildAnimalUrl(string slug) => $"{_options.Scheme}://{slug}.{_options.RootDomain}/";

    /// <summary>Является ли host корневым доменом зоопарка (zoostav.ru, www.zoostav.ru)?</summary>
    public bool IsRootDomain(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        var hostWithoutPort = host.Split(':')[0].Trim().TrimEnd('.').ToLowerInvariant();
        var root = _options.RootDomain.ToLowerInvariant();

        return hostWithoutPort == root || hostWithoutPort == "www." + root;
    }

    /// <summary>Строит абсолютный адрес страницы животного с учётом текущего host (для локальной отладки).</summary>
    public string BuildAnimalUrlForRequest(string slug, HttpRequest request)
    {
        var host = request.Host.Host.ToLowerInvariant();

        // Боевой домен — ссылка строго на поддомен: https://raccoon.zoostav.ru/
        if (IsRootDomain(host) || host.EndsWith("." + _options.RootDomain, StringComparison.Ordinal))
        {
            return BuildAnimalUrl(slug);
        }

        // Локальная отладка: браузеры резолвят *.localhost в 127.0.0.1 -> http://raccoon.localhost:5000/
        if (_options.TreatLocalhostAsRoot && host is "localhost" or "127.0.0.1" or "::1")
        {
            var port = request.Host.Port is null or 80 or 443 ? string.Empty : ":" + request.Host.Port;
            return $"{request.Scheme}://{slug}.localhost{port}/";
        }

        // Прочие окружения (локальный стенд, туннель, демо-песочница): путь-фолбэк,
        // чтобы страница животного гарантированно открывалась.
        return $"{request.Scheme}://{request.Host}/animal/{slug}";
    }
}
