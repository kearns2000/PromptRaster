using System.Text.Json;
using System.Xml;

namespace PromptRaster.Internal;

/// <summary>
/// A conservative, deterministic heuristic classifier. It errs on the side of
/// returning a non-prose classification so that structured or accuracy-sensitive
/// content is kept as text. It never throws for malformed input.
/// </summary>
internal sealed class TextContentClassifier : ITextContentClassifier
{
    private const int MaxParseLength = 512 * 1024;

    private static readonly string[] CodeKeywords =
    [
        "public ", "private ", "protected ", "internal ", "static ", "void ",
        "function ", "func ", "def ", "class ", "interface ", "namespace ",
        "return ", "var ", "let ", "const ", "import ", "using ", "#include",
        "=> ", "async ", "await ", "new ", "if (", "for (", "while (", "foreach (",
    ];

    public TextContentClassification Classify(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return TextContentClassification.Empty;
        }

        var trimmed = text.AsSpan().Trim();

        if (LooksLikeJson(trimmed, text))
        {
            return TextContentClassification.Structured;
        }

        if (LooksLikeXml(trimmed, text))
        {
            return TextContentClassification.Structured;
        }

        var lines = SplitLines(text);
        var nonEmptyLines = lines.Where(static l => l.Trim().Length > 0).ToArray();

        if (nonEmptyLines.Length == 0)
        {
            return TextContentClassification.Empty;
        }

        if (LooksLikeStackTrace(nonEmptyLines))
        {
            return TextContentClassification.Code;
        }

        if (HasDominantFencedCode(lines, text.Length))
        {
            return TextContentClassification.Code;
        }

        if (LooksLikeSourceCode(nonEmptyLines))
        {
            return TextContentClassification.Code;
        }

        if (LooksLikeCsv(nonEmptyLines) || LooksLikePipeTable(nonEmptyLines))
        {
            return TextContentClassification.Tabular;
        }

        if (LooksLikeYamlOrConfig(nonEmptyLines))
        {
            return TextContentClassification.Structured;
        }

        if (IsIdentifierHeavy(text))
        {
            return TextContentClassification.IdentifierHeavy;
        }

        return TextContentClassification.Prose;
    }

    private static bool LooksLikeJson(ReadOnlySpan<char> trimmed, string original)
    {
        if (trimmed.Length < 2)
        {
            return false;
        }

        var first = trimmed[0];
        var last = trimmed[^1];
        var bracketed = (first == '{' && last == '}') || (first == '[' && last == ']');

        if (!bracketed || original.Length > MaxParseLength)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(
                original,
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

            // A bare JSON string or number is not meaningfully "structured".
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool LooksLikeXml(ReadOnlySpan<char> trimmed, string original)
    {
        if (trimmed.Length < 3 || trimmed[0] != '<' || trimmed[^1] != '>')
        {
            return false;
        }

        if (original.Length > MaxParseLength)
        {
            return false;
        }

        try
        {
            using var reader = XmlReader.Create(
                new StringReader(original),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });

            while (reader.Read())
            {
            }

            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }

    private static bool LooksLikeStackTrace(string[] nonEmptyLines)
    {
        var frameLines = nonEmptyLines.Count(static line =>
        {
            var t = line.TrimStart();
            return t.StartsWith("at ", StringComparison.Ordinal) &&
                   (t.Contains('(') || t.Contains(" in ", StringComparison.Ordinal)) ||
                   t.StartsWith("File \"", StringComparison.Ordinal) ||
                   t.StartsWith("Traceback (", StringComparison.Ordinal);
        });

        var hasExceptionLine = nonEmptyLines.Any(static line =>
            line.Contains("Exception", StringComparison.Ordinal) ||
            line.Contains("Error:", StringComparison.Ordinal));

        return frameLines >= 2 && (hasExceptionLine || frameLines >= 4);
    }

    private static bool HasDominantFencedCode(string[] lines, int totalLength)
    {
        var fencedCharacters = 0;
        var insideFence = false;

        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence)
            {
                fencedCharacters += line.Length + 1;
            }
        }

        return totalLength > 0 && fencedCharacters / (double)totalLength > 0.2;
    }

    private static bool LooksLikeSourceCode(string[] nonEmptyLines)
    {
        var codeLines = 0;

        foreach (var line in nonEmptyLines)
        {
            var trimmed = line.Trim();

            var endsLikeCode = trimmed.EndsWith(';') || trimmed is "{" or "}" ||
                               trimmed.EndsWith('{') || trimmed.EndsWith("});", StringComparison.Ordinal);

            var startsLikeComment = trimmed.StartsWith("//", StringComparison.Ordinal) ||
                                    trimmed.StartsWith("/*", StringComparison.Ordinal) ||
                                    trimmed.StartsWith("* ", StringComparison.Ordinal);

            var hasKeyword = CodeKeywords.Any(k => trimmed.Contains(k, StringComparison.Ordinal));

            if (endsLikeCode || startsLikeComment || (hasKeyword && trimmed.Contains('(')))
            {
                codeLines++;
            }
        }

        return codeLines / (double)nonEmptyLines.Length > 0.4;
    }

    private static bool LooksLikeCsv(string[] nonEmptyLines)
    {
        if (nonEmptyLines.Length < 3)
        {
            return false;
        }

        var sample = nonEmptyLines.Take(50).ToArray();
        var commaCounts = sample.Select(static l => l.Count(static c => c == ',')).ToArray();
        var expected = commaCounts[0];

        if (expected < 2)
        {
            return false;
        }

        var matching = commaCounts.Count(c => c == expected);
        return matching / (double)sample.Length > 0.8;
    }

    private static bool LooksLikePipeTable(string[] nonEmptyLines)
    {
        var tableLines = nonEmptyLines.Count(static l => l.Count(static c => c == '|') >= 2);
        return nonEmptyLines.Length >= 3 && tableLines / (double)nonEmptyLines.Length > 0.3;
    }

    private static bool LooksLikeYamlOrConfig(string[] nonEmptyLines)
    {
        if (nonEmptyLines.Length < 3)
        {
            return false;
        }

        var structuredLines = 0;

        foreach (var line in nonEmptyLines)
        {
            var trimmed = line.Trim();

            if (IsYamlKeyLine(trimmed) || IsIniLine(trimmed) || trimmed is "---" or "...")
            {
                structuredLines++;
            }
        }

        return structuredLines / (double)nonEmptyLines.Length > 0.5;
    }

    private static bool IsYamlKeyLine(string trimmed)
    {
        if (trimmed.StartsWith("- ", StringComparison.Ordinal))
        {
            return true;
        }

        var colonIndex = trimmed.IndexOf(':', StringComparison.Ordinal);

        if (colonIndex <= 0 || colonIndex > 60)
        {
            return false;
        }

        // A YAML key is a short token without spaces before the colon,
        // followed by end-of-line or a space. Prose such as "Note: this is..."
        // has a capitalised word which we still accept, so require the key to
        // contain no spaces and the whole line to not read like a sentence.
        var key = trimmed[..colonIndex];

        if (key.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        var rest = trimmed[(colonIndex + 1)..].Trim();

        // Prose sentences after a colon tend to be long; YAML values are short
        // scalars or empty (nested mappings).
        return rest.Length == 0 || (rest.Length <= 60 && !rest.Contains(". ", StringComparison.Ordinal));
    }

    private static bool IsIniLine(string trimmed)
    {
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            return true;
        }

        var equalsIndex = trimmed.IndexOf('=', StringComparison.Ordinal);
        return equalsIndex > 0 && equalsIndex <= 60 && !trimmed[..equalsIndex].TrimEnd().Contains(' ', StringComparison.Ordinal);
    }

    private static bool IsIdentifierHeavy(string text)
    {
        var tokens = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
        {
            return false;
        }

        var longTokens = 0;
        var punctuation = 0;
        var letters = 0;

        foreach (var token in tokens)
        {
            // URLs and email addresses embedded in prose are expected; do not
            // count them as accuracy-sensitive identifiers.
            if (token.Length > 25 && !IsUrlOrEmail(token))
            {
                longTokens++;
            }
        }

        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                letters++;
            }
            else if (!char.IsWhiteSpace(c))
            {
                punctuation++;
            }
        }

        var longTokenRatio = longTokens / (double)tokens.Length;
        var punctuationRatio = punctuation / (double)Math.Max(1, letters + punctuation);

        return longTokenRatio > 0.1 || punctuationRatio > 0.25;
    }

    private static bool IsUrlOrEmail(string token)
    {
        var cleaned = token.TrimEnd('.', ',', ';', ')', ']', '!', '?');

        return cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               cleaned.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
               (cleaned.Contains('@') && cleaned.Contains('.') && !cleaned.Contains(' '));
    }

    private static string[] SplitLines(string text) =>
        text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
}
