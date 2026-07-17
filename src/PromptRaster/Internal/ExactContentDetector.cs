using System.Globalization;
using System.Text.RegularExpressions;

namespace PromptRaster.Internal;

/// <summary>
/// Conservative heuristics for exact or sensitive content. Matches are intentionally
/// biased toward false positives so that doubtful material stays as text.
/// </summary>
internal sealed partial class ExactContentDetector : IExactContentDetector
{
    private static readonly string[] SecretKeywords =
    [
        "api_key", "apikey", "api-key", "secret_key", "secretkey", "access_token",
        "refresh_token", "bearer ", "authorization:", "password=", "passwd=",
        "client_secret", "private_key", "-----begin ", "connectionstring",
        "accountkey=", "sharedaccesssignature",
    ];

    public bool LooksExactOrSensitive(string text, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            reason = null;
            return false;
        }

        if (ContainsSecretKeyword(text))
        {
            reason = "secret_or_credential";
            return true;
        }

        if (HasLongHashDensity(text))
        {
            reason = "long_hash";
            return true;
        }

        if (HasGuidDensity(text))
        {
            reason = "guid_heavy";
            return true;
        }

        if (HasPathDensity(text))
        {
            reason = "path_heavy";
            return true;
        }

        if (HasDenseNumericData(text))
        {
            reason = "dense_numeric";
            return true;
        }

        reason = null;
        return false;
    }

    private static bool ContainsSecretKeyword(string text)
    {
        foreach (var keyword in SecretKeywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLongHashDensity(string text)
    {
        var matches = HexHashRegex().Matches(text);
        if (matches.Count == 0)
        {
            return false;
        }

        var hashCharacters = matches.Sum(static m => m.Length);
        return matches.Count >= 3 || hashCharacters / (double)text.Length > 0.08;
    }

    private static bool HasGuidDensity(string text)
    {
        var matches = GuidRegex().Matches(text);
        return matches.Count >= 3 || (matches.Count >= 1 && matches.Count / (double)Math.Max(1, CountTokens(text)) > 0.05);
    }

    private static bool HasPathDensity(string text)
    {
        var matches = PathRegex().Matches(text)
            .Where(m => !IsUrlEmbeddedPath(m, text))
            .ToArray();

        if (matches.Length < 2)
        {
            return false;
        }

        var pathCharacters = matches.Sum(static m => m.Length);
        return pathCharacters / (double)text.Length > 0.05;
    }

    private static bool IsUrlEmbeddedPath(Match match, string text)
    {
        var start = match.Index;
        if (start == 0)
        {
            return false;
        }

        // Ignore path-like fragments that sit inside http(s) URLs or www. hosts.
        var lookbehind = text[Math.Max(0, start - 64)..start];
        var schemeIndex = lookbehind.LastIndexOf("://", StringComparison.Ordinal);
        if (schemeIndex >= 0 && !lookbehind[schemeIndex..].Contains(' '))
        {
            return true;
        }

        var wwwIndex = lookbehind.LastIndexOf("www.", StringComparison.OrdinalIgnoreCase);
        return wwwIndex >= 0 && !lookbehind[wwwIndex..].Contains(' ');
    }

    private static bool HasDenseNumericData(string text)
    {
        var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length < 5)
        {
            return false;
        }

        var numericLines = 0;

        foreach (var line in lines.Take(80))
        {
            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                continue;
            }

            var numericTokens = tokens.Count(static t =>
                double.TryParse(t.TrimEnd(',', ';'), NumberStyles.Float, CultureInfo.InvariantCulture, out _));

            if (numericTokens / (double)tokens.Length >= 0.6 && tokens.Length >= 3)
            {
                numericLines++;
            }
        }

        return numericLines / (double)Math.Min(lines.Length, 80) > 0.4;
    }

    private static int CountTokens(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    [GeneratedRegex(@"\b[0-9a-fA-F]{32,128}\b", RegexOptions.CultureInvariant)]
    private static partial Regex HexHashRegex();

    [GeneratedRegex(
        @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex GuidRegex();

    [GeneratedRegex(
        @"((?:[A-Za-z]:\\|\\\\|/)[^\s""']+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex PathRegex();
}
