using System.Text.Json;

namespace Mozaika.Api.Security;

public sealed class RuntimeCorsOriginsStore
{
    private readonly object _sync = new();
    private string[] _origins = ["*"];
    private HashSet<string> _originSet = ["*"];
    private bool _allowAny = true;

    public RuntimeCorsOriginsStore(IEnumerable<string>? initialOrigins)
    {
        Set(initialOrigins);
    }

    public string[] GetSnapshot()
    {
        lock (_sync)
        {
            return [.. _origins];
        }
    }

    public void Set(IEnumerable<string>? origins)
    {
        var normalized = NormalizeOrigins(origins);
        var allowAny = normalized.Any(item => item == "*");
        if (allowAny)
        {
            normalized = ["*"];
        }

        var nextSet = normalized.ToHashSet(StringComparer.OrdinalIgnoreCase);
        lock (_sync)
        {
            _origins = normalized;
            _originSet = nextSet;
            _allowAny = allowAny;
        }
    }

    public bool IsAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        var normalized = NormalizeSingleOrigin(origin);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        lock (_sync)
        {
            return _allowAny || _originSet.Contains(normalized);
        }
    }

    public static string[] NormalizeOrigins(IEnumerable<string>? origins)
    {
        if (origins is null)
        {
            return ["*"];
        }

        var values = origins
            .Select(NormalizeSingleOrigin)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return values.Length == 0 ? ["*"] : values;
    }

    public static string SerializeOrigins(IEnumerable<string>? origins)
    {
        var normalized = NormalizeOrigins(origins);
        return JsonSerializer.Serialize(normalized);
    }

    public static string[] ParseOriginsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<string[]>(json) ?? [];
            return NormalizeOrigins(raw);
        }
        catch
        {
            return [];
        }
    }

    private static string NormalizeSingleOrigin(string origin)
    {
        var raw = origin.Trim().Trim('"').TrimEnd('/');
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        if (raw == "*")
        {
            return "*";
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return $"{uri.Scheme.ToLowerInvariant()}://{uri.Authority.ToLowerInvariant()}";
        }

        return raw.ToLowerInvariant();
    }
}
