namespace PromptRaster.Internal;

/// <summary>
/// The coarse content category produced by <see cref="ITextContentClassifier"/>.
/// Only <see cref="Prose"/> is eligible for automatic rasterisation.
/// </summary>
internal enum TextContentClassification
{
    /// <summary>Ordinary prose, including Markdown prose without substantial code blocks.</summary>
    Prose,

    /// <summary>JSON, XML, YAML, configuration, or similarly structured content.</summary>
    Structured,

    /// <summary>Source code, stack traces, or Markdown dominated by fenced code blocks.</summary>
    Code,

    /// <summary>CSV, pipe-delimited tables, or other tabular data.</summary>
    Tabular,

    /// <summary>Content dominated by long unbroken identifiers or unusually heavy punctuation.</summary>
    IdentifierHeavy,

    /// <summary>Empty or whitespace-only content.</summary>
    Empty
}
