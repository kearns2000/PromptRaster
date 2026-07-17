namespace PromptRaster.MicrosoftExtensionsAI;

/// <summary>
/// Thrown by <see cref="PromptRasterChatClient"/> when strict mode is enabled and
/// rasterisation cannot proceed.
/// </summary>
public sealed class PromptRasterException : Exception
{
    /// <summary>Creates an exception with the given message and optional decision reason.</summary>
    public PromptRasterException(string message, PromptRasterDecisionReason? reason = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
    }

    /// <summary>The decision reason associated with the failure, when available.</summary>
    public PromptRasterDecisionReason? Reason { get; }
}
