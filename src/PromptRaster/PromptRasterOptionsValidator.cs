using Microsoft.Extensions.Options;

namespace PromptRaster;

/// <summary>
/// Validates <see cref="PromptRasterOptions"/> when the options instance is first created.
/// </summary>
internal sealed class PromptRasterOptionsValidator : IValidateOptions<PromptRasterOptions>
{
    private const float MinimumSafeFontSize = 8f;
    private const int MinimumDrawableSize = 100;
    private const int MinimumThreshold = 1_000;

    public ValidateOptionsResult Validate(string? name, PromptRasterOptions options)
    {
        var failures = new List<string>();

        if (options.MinimumTextLength < 0)
        {
            failures.Add($"{nameof(options.MinimumTextLength)} must not be negative.");
        }

        if (options.MaximumPages < 1)
        {
            failures.Add($"{nameof(options.MaximumPages)} must be at least 1.");
        }

        if (options.AbsoluteMaximumPages < 1)
        {
            failures.Add($"{nameof(options.AbsoluteMaximumPages)} must be at least 1.");
        }

        if (options.MaximumPages > options.AbsoluteMaximumPages)
        {
            failures.Add(
                $"{nameof(options.MaximumPages)} ({options.MaximumPages}) must not exceed " +
                $"{nameof(options.AbsoluteMaximumPages)} ({options.AbsoluteMaximumPages}).");
        }

        if (options.ImageWidth <= 0)
        {
            failures.Add($"{nameof(options.ImageWidth)} must be positive.");
        }

        if (options.ImageHeight <= 0)
        {
            failures.Add($"{nameof(options.ImageHeight)} must be positive.");
        }

        if (options.HorizontalPadding < 0)
        {
            failures.Add($"{nameof(options.HorizontalPadding)} must not be negative.");
        }

        if (options.VerticalPadding < 0)
        {
            failures.Add($"{nameof(options.VerticalPadding)} must not be negative.");
        }

        if (options.ImageWidth > 0 &&
            options.HorizontalPadding >= 0 &&
            options.ImageWidth - (2 * options.HorizontalPadding) < MinimumDrawableSize)
        {
            failures.Add(
                $"{nameof(options.HorizontalPadding)} leaves fewer than {MinimumDrawableSize} " +
                $"drawable horizontal pixels.");
        }

        if (options.ImageHeight > 0 &&
            options.VerticalPadding >= 0 &&
            options.ImageHeight - (2 * options.VerticalPadding) < MinimumDrawableSize)
        {
            failures.Add(
                $"{nameof(options.VerticalPadding)} leaves fewer than {MinimumDrawableSize} " +
                $"drawable vertical pixels.");
        }

        if (options.FontSize < MinimumSafeFontSize)
        {
            failures.Add($"{nameof(options.FontSize)} must be at least {MinimumSafeFontSize}.");
        }

        if (options.LineSpacingMultiplier < 1f)
        {
            failures.Add($"{nameof(options.LineSpacingMultiplier)} must be at least 1.");
        }

        ValidateThreshold(failures, nameof(options.OpenAIMinimumCharactersPerPage), options.OpenAIMinimumCharactersPerPage);
        ValidateThreshold(failures, nameof(options.AzureOpenAIMinimumCharactersPerPage), options.AzureOpenAIMinimumCharactersPerPage);
        ValidateThreshold(failures, nameof(options.GeminiMinimumCharactersPerPage), options.GeminiMinimumCharactersPerPage);
        ValidateThreshold(failures, nameof(options.AnthropicMinimumCharactersPerPage), options.AnthropicMinimumCharactersPerPage);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateThreshold(List<string> failures, string propertyName, int value)
    {
        if (value < MinimumThreshold)
        {
            failures.Add($"{propertyName} must be at least {MinimumThreshold} characters per page.");
        }
    }
}
