using System.Text;

namespace PromptRaster.Tests.TestSupport;

/// <summary>
/// Generates deterministic long-form prose for tests. The output is dense
/// (few paragraph breaks) so that full pages carry roughly 7,000 characters
/// with the default render settings.
/// </summary>
internal static class ProseGenerator
{
    private static readonly string[] Sentences =
    [
        "The committee reviewed the quarterly findings and noted that regional supply patterns had shifted meaningfully during recent months.",
        "Several members observed that customer expectations continued to evolve across a number of important markets.",
        "The analysts argued that a gradual migration toward digital channels had quietly reshaped assumptions about delivery timelines.",
        "History offers a useful parallel because earlier networks were designed around hubs that later became redundant.",
        "Respondents repeatedly described a widening gap between what their tools could measure and what their teams actually needed to know.",
        "None of this implies that the current strategy has failed, and the evidence indicates steady progress against the stated objectives.",
    ];

    public static string Generate(int targetLength, int sentencesPerParagraph = 40)
    {
        var builder = new StringBuilder(targetLength + 256);
        var index = 0;

        while (builder.Length < targetLength)
        {
            builder.Append(Sentences[index % Sentences.Length]);
            builder.Append(index % sentencesPerParagraph == sentencesPerParagraph - 1 ? "\n\n" : " ");
            index++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Long, paragraph-sparse prose for cross-platform threshold tests.
    /// </summary>
    public static string GenerateForThresholdTests(int targetLength = 80_000) =>
        Generate(targetLength, sentencesPerParagraph: 200);
}
