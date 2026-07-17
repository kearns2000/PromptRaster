using System.Text;

namespace PromptRaster.MicrosoftExtensionsAI.Tests.TestSupport;

internal static class Prose
{
    private const string Sentence =
        "The committee reviewed the quarterly findings and noted that regional supply " +
        "patterns had shifted meaningfully during recent months. ";

    public static string Generate(int targetLength)
    {
        var builder = new StringBuilder(targetLength + Sentence.Length);

        while (builder.Length < targetLength)
        {
            builder.Append(Sentence);
        }

        return builder.ToString();
    }
}
