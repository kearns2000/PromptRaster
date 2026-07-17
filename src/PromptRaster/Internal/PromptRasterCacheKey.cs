using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PromptRaster.Internal;

/// <summary>
/// Builds a stable cache key from source content and the rendering settings that
/// affect PNG output. Identical inputs produce identical keys.
/// </summary>
internal static class PromptRasterCacheKey
{
    /// <summary>Increment when PNG encoding or layout semantics change incompatibly.</summary>
    public const string RendererVersion = "1";

    public static string Create(string text, PromptRasterRenderSettings settings, ModelProfile? profile)
    {
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"v{RendererVersion}|{contentHash}|{settings.ImageWidth}x{settings.ImageHeight}|f{settings.FontSize}|ls{settings.LineSpacingMultiplier}|hp{settings.HorizontalPadding}|vp{settings.VerticalPadding}|h{(settings.IncludePageHeader ? 1 : 0)}|p{profile?.ModelId ?? "-"}|{profile?.MaximumPages.ToString(CultureInfo.InvariantCulture) ?? "-"}");

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}
