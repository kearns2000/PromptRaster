using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace PromptRaster.Tests;

public class PromptRasterOptionsValidatorTests
{
    private readonly PromptRasterOptionsValidator _validator = new();

    [Fact]
    public void DefaultOptions_AreValid()
    {
        _validator.Validate(null, new PromptRasterOptions()).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void NonPositiveImageWidth_Fails(int width)
    {
        var result = _validator.Validate(null, new PromptRasterOptions { ImageWidth = width });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(PromptRasterOptions.ImageWidth));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveImageHeight_Fails(int height)
    {
        _validator.Validate(null, new PromptRasterOptions { ImageHeight = height }).Failed.Should().BeTrue();
    }

    [Fact]
    public void PaddingThatLeavesNoDrawableArea_Fails()
    {
        var result = _validator.Validate(null, new PromptRasterOptions
        {
            ImageWidth = 400,
            HorizontalPadding = 200,
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(PromptRasterOptions.HorizontalPadding));
    }

    [Fact]
    public void VerticalPaddingThatLeavesNoDrawableArea_Fails()
    {
        var result = _validator.Validate(null, new PromptRasterOptions
        {
            ImageHeight = 300,
            VerticalPadding = 150,
        });

        result.Failed.Should().BeTrue();
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(7.9f)]
    [InlineData(-17f)]
    public void FontSizeBelowSafeMinimum_Fails(float fontSize)
    {
        _validator.Validate(null, new PromptRasterOptions { FontSize = fontSize }).Failed.Should().BeTrue();
    }

    [Fact]
    public void MaximumPagesGreaterThanAbsoluteMaximum_Fails()
    {
        var result = _validator.Validate(null, new PromptRasterOptions
        {
            MaximumPages = 60,
            AbsoluteMaximumPages = 50,
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(PromptRasterOptions.AbsoluteMaximumPages));
    }

    [Theory]
    [InlineData(999)]
    [InlineData(0)]
    [InlineData(-1)]
    public void ThresholdsBelowOneThousand_Fail(int threshold)
    {
        var options = new PromptRasterOptions
        {
            OpenAIMinimumCharactersPerPage = threshold,
            AzureOpenAIMinimumCharactersPerPage = threshold,
            GeminiMinimumCharactersPerPage = threshold,
            AnthropicMinimumCharactersPerPage = threshold,
        };

        var result = _validator.Validate(null, options);

        result.Failed.Should().BeTrue();
        result.Failures.Should().HaveCount(4);
    }

    [Fact]
    public async Task InvalidOptions_FailWhenResolvedThroughDependencyInjection()
    {
        var provider = new ServiceCollection()
            .AddPromptRaster(static options => options.FontSize = 1)
            .BuildServiceProvider();

        var rasterizer = provider.GetRequiredService<IPromptRasterizer>();

        var act = async () => await rasterizer.RasterizeAsync("text", AiProvider.OpenAI);

        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public void DuplicateModelProfileIds_Fail()
    {
        var result = _validator.Validate(null, new PromptRasterOptions
        {
            ModelProfiles =
            [
                new ModelProfile { ModelId = "gpt-test", Provider = AiProvider.OpenAI },
                new ModelProfile { ModelId = "GPT-TEST", Provider = AiProvider.OpenAI },
            ],
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("duplicated");
    }

    [Fact]
    public void ModelProfileWithInvalidMaximumPages_Fails()
    {
        var result = _validator.Validate(null, new PromptRasterOptions
        {
            ModelProfiles =
            [
                new ModelProfile
                {
                    ModelId = "gpt-test",
                    Provider = AiProvider.OpenAI,
                    MaximumPages = 0,
                },
            ],
        });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(ModelProfile.MaximumPages));
    }
}
