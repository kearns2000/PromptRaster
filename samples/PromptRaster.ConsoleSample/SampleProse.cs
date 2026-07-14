using System.Text;

namespace PromptRaster.ConsoleSample;

/// <summary>
/// Generates representative long-form prose so the sample does not need to
/// ship a large text file or call any external service.
/// </summary>
internal static class SampleProse
{
    private static readonly string[] Paragraphs =
    [
        "The committee convened early on Tuesday morning to review the quarterly findings, and " +
        "although the agenda promised a brisk session, the discussion of regional supply variations " +
        "stretched well past the scheduled hour. Several members observed that seasonal demand had " +
        "shifted noticeably compared with previous years, and that the causes were unlikely to be " +
        "explained by weather patterns alone.",

        "In the second half of the report, attention turned to the long-term implications of the " +
        "distribution changes. The analysts argued that a gradual migration of customers toward " +
        "digital channels had quietly reshaped expectations around delivery timelines, and that the " +
        "organisation's existing commitments would need to be revisited before the next planning cycle.",

        "History offers a useful parallel here. When the original network was designed, planners " +
        "assumed that regional hubs would remain the primary points of coordination for decades. " +
        "Within ten years, however, improvements in transport and communication had rendered several " +
        "of those hubs redundant, and the organisations that adapted earliest captured a durable advantage.",

        "The interviews conducted for this study suggest a similar dynamic today. Respondents " +
        "repeatedly described a widening gap between what their tools could measure and what their " +
        "teams actually needed to know. One participant summarised the situation succinctly: the " +
        "numbers were plentiful, but the understanding was scarce, and no dashboard could substitute " +
        "for a colleague who had seen the pattern before.",

        "None of this implies that the current strategy has failed. On the contrary, the evidence " +
        "indicates steady progress against the stated objectives, with measured improvements in " +
        "customer satisfaction and a modest reduction in operating costs. What the evidence does " +
        "suggest is that the pace of external change has accelerated, and that the comfortable margin " +
        "the organisation once enjoyed has narrowed considerably.",
    ];

    public static string Generate(int targetCharacterCount)
    {
        var builder = new StringBuilder(targetCharacterCount + 512);
        var index = 0;

        while (builder.Length < targetCharacterCount)
        {
            builder.Append(Paragraphs[index % Paragraphs.Length]);

            // Documents such as reports and transcripts have long paragraphs;
            // only break occasionally so the pages stay dense.
            builder.Append(index % 6 == 5 ? "\n\n" : " ");
            index++;
        }

        return builder.ToString();
    }
}
