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

        ValidateModelProfiles(failures, options);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateModelProfiles(List<string> failures, PromptRasterOptions options)
    {
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < options.ModelProfiles.Count; i++)
        {
            var profile = options.ModelProfiles[i];
            var label = $"{nameof(options.ModelProfiles)}[{i}]";

            if (profile is null)
            {
                failures.Add($"{label} must not be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(profile.ModelId))
            {
                failures.Add($"{label}.{nameof(profile.ModelId)} must not be empty.");
            }
            else if (!seenIds.Add(profile.ModelId))
            {
                failures.Add($"{label}.{nameof(profile.ModelId)} '{profile.ModelId}' is duplicated.");
            }

            if (profile.MaximumPages < 1)
            {
                failures.Add($"{label}.{nameof(profile.MaximumPages)} must be at least 1.");
            }

            if (profile.MinimumCharactersPerPage < MinimumThreshold)
            {
                failures.Add(
                    $"{label}.{nameof(profile.MinimumCharactersPerPage)} must be at least {MinimumThreshold}.");
            }

            if (profile.ImageWidth <= 0 || profile.ImageHeight <= 0)
            {
                failures.Add($"{label} image dimensions must be positive.");
            }

            if (profile.FontSize < MinimumSafeFontSize)
            {
                failures.Add($"{label}.{nameof(profile.FontSize)} must be at least {MinimumSafeFontSize}.");
            }

            if (profile.HorizontalPadding < 0 || profile.VerticalPadding < 0)
            {
                failures.Add($"{label} padding values must not be negative.");
            }
        }
    }

    private static void ValidateThreshold(List<string> failures, string propertyName, int value)
    {
        if (value < MinimumThreshold)
        {
            failures.Add($"{propertyName} must be at least {MinimumThreshold} characters per page.");
        }
    }
}
