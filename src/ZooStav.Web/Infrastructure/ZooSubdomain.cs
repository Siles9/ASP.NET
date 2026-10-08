using Microsoft.Extensions.Options;

namespace ZooStav.Web.Infrastructure;

public class ZooSubdomain(IOptions<ZooOptions> options)
{
    private readonly ZooOptions _options = options.Value;

    public string RootDomain => _options.RootDomain;

    public string MainSiteUrl => _options.MainSiteUrl;

    public string? GetAnimalSlug(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var hostWithoutPort = host.Split(':')[0].Trim().TrimEnd('.').ToLowerInvariant();

        if (_options.TreatLocalhostAsRoot &&
            (hostWithoutPort == "localhost" || hostWithoutPort == "127.0.0.1"))
        {
            return null;
        }

        var root = _options.RootDomain.ToLowerInvariant();

        if (hostWithoutPort.EndsWith("." + root, StringComparison.Ordinal))
        {
            var sub = hostWithoutPort[..^(root.Length + 1)];
            return Normalize(sub);
        }

        foreach (var suffix in new[] { ".localhost", ".localtest.me", ".lvh.me", ".nip.io" })
        {
            if (hostWithoutPort.EndsWith(suffix, StringComparison.Ordinal))
            {
                return Normalize(hostWithoutPort[..^suffix.Length]);
            }
        }

        if (!hostWithoutPort.Contains(root, StringComparison.Ordinal) &&
            (hostWithoutPort.EndsWith(".ru", StringComparison.Ordinal) || hostWithoutPort.Contains('.')))
        {
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

            var firstSegment = candidate.Split('.')[0];
            if (firstSegment.Length == 0 ||
                _options.IgnoredSubdomains.Contains(firstSegment, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            return firstSegment;
        }
    }

    public string BuildAnimalUrl(string slug) => $"{_options.Scheme}://{slug}.{_options.RootDomain}/";

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

    public string BuildAnimalUrlForRequest(string slug, HttpRequest request)
    {
        var host = request.Host.Host.ToLowerInvariant();

        if (IsRootDomain(host) || host.EndsWith("." + _options.RootDomain, StringComparison.Ordinal))
        {
            return BuildAnimalUrl(slug);
        }

        if (_options.TreatLocalhostAsRoot && host is "localhost" or "127.0.0.1" or "::1")
        {
            var port = request.Host.Port is null or 80 or 443 ? string.Empty : ":" + request.Host.Port;
            return $"{request.Scheme}://{slug}.localhost{port}/";
        }

        return $"{request.Scheme}://{request.Host}/animal/{slug}";
    }
}
