using System.Buffers.Binary;

namespace PromptRaster.Tests.TestSupport;

/// <summary>
/// Minimal PNG inspection helpers: signature check and IHDR dimensions.
/// Deliberately avoids whole-image snapshot comparisons.
/// </summary>
internal static class Png
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool HasValidSignature(byte[] data) =>
        data.Length >= Signature.Length && data.AsSpan(0, Signature.Length).SequenceEqual(Signature);

    public static (int Width, int Height) ReadDimensions(byte[] data)
    {
        // The IHDR chunk always immediately follows the signature:
        // 8 (signature) + 4 (length) + 4 ("IHDR") = offset 16 for width, 20 for height.
        var width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4));
        return (width, height);
    }
}
