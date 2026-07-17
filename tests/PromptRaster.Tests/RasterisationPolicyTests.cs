using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PromptRaster.Tests.TestSupport;
using Xunit;

namespace PromptRaster.Tests;

public class RasterisationPolicyTests
{
    private static IRasterisationPolicy CreatePolicy(Action<PromptRasterOptions>? configure = null)
    {
        var services = new ServiceCollection();
        if (configure is null)
        {
            services.AddPromptRaster();
        }
        else
        {
            services.AddPromptRaster(configure);
        }

        return services.BuildServiceProvider().GetRequiredService<IRasterisationPolicy>();
    }

    [Fact]
    public async Task UnsupportedModelId_Rejects()
    {
        var policy = CreatePolicy();

        var decision = await policy.EvaluateAsync(new RasterisationCandidate(
            ProseGenerator.Generate(8_000),
            AiProvider.OpenAI,
            ModelId: "unknown-model-xyz"));

        decision.ShouldRasterise.Should().BeFalse();
        decision.Reason.Should().Be(PromptRasterDecisionReason.UnsupportedModel);
    }

    [Fact]
    public async Task RegisteredEnabledModel_PassesPreLayoutChecks()
    {
        var policy = CreatePolicy(options =>
        {
            options.MinimumTextLength = 1_000;
            options.ModelProfiles.Add(new ModelProfile
            {
                ModelId = "gpt-test",
                Enabled = true,
                Provider = AiProvider.OpenAI,
            });
        });

        var decision = await policy.EvaluateAsync(new RasterisationCandidate(
            ProseGenerator.Generate(2_000),
            AiProvider.OpenAI,
            ModelId: "gpt-test"));

        decision.ShouldRasterise.Should().BeTrue();
    }

    [Fact]
    public async Task ExactContent_Rejects()
    {
        var policy = CreatePolicy(static options => options.MinimumTextLength = 10);
        var text =
            "api_key=sk-test-not-a-real-secret and then a long explanation about why the " +
            "deployment pipeline must rotate credentials after every region is updated.";

        var decision = await policy.EvaluateAsync(new RasterisationCandidate(text, AiProvider.OpenAI));

        decision.ShouldRasterise.Should().BeFalse();
        decision.Reason.Should().Be(PromptRasterDecisionReason.ExactContentDetected);
    }

    [Fact]
    public async Task ExplicitExclusion_Rejects()
    {
        var policy = CreatePolicy();

        var decision = await policy.EvaluateAsync(new RasterisationCandidate(
            ProseGenerator.Generate(8_000),
            AiProvider.OpenAI,
            Request: new PromptRasterRequest { ExcludeFromRasterisation = true }));

        decision.ShouldRasterise.Should().BeFalse();
        decision.Reason.Should().Be(PromptRasterDecisionReason.ExplicitlyExcluded);
    }

    [Fact]
    public async Task Rasterizer_WithUnknownModelId_RemainsText()
    {
        var rasterizer = RasterizerFactory.Create();

        var result = await rasterizer.RasterizeAsync(
            ProseGenerator.Generate(12_000),
            AiProvider.OpenAI,
            new PromptRasterRequest { ModelId = "no-such-model", TreatAsProse = true });

        result.Encoding.Should().Be(PromptRasterEncoding.Text);
        result.Decision.Reason.Should().Be(PromptRasterDecisionReason.UnsupportedModel);
    }

    [Fact]
    public async Task ProfileProviderMismatch_Rejects()
    {
        var policy = CreatePolicy(options =>
        {
            options.MinimumTextLength = 1_000;
            options.ModelProfiles.Add(new ModelProfile
            {
                ModelId = "gpt-test",
                Enabled = true,
                Provider = AiProvider.OpenAI,
            });
        });

        var decision = await policy.EvaluateAsync(new RasterisationCandidate(
            ProseGenerator.Generate(2_000),
            AiProvider.Anthropic,
            ModelId: "gpt-test"));

        decision.ShouldRasterise.Should().BeFalse();
        decision.Reason.Should().Be(PromptRasterDecisionReason.UnsupportedModel);
    }

    [Fact]
    public async Task ProfileMaximumPages_IsEnforced()
    {
        var policy = CreatePolicy(options =>
        {
            options.MinimumTextLength = 1_000;
            options.MaximumPages = 8;
            options.ModelProfiles.Add(new ModelProfile
            {
                ModelId = "gpt-test",
                Enabled = true,
                Provider = AiProvider.OpenAI,
                MaximumPages = 2,
                MinimumCharactersPerPage = 1_000,
            });
        });

        var decision = await policy.EvaluateAsync(new RasterisationCandidate(
            ProseGenerator.Generate(2_000),
            AiProvider.OpenAI,
            ModelId: "gpt-test",
            PageCount: 3,
            AverageCharactersPerPage: 5_000,
            RequiredCharactersPerPage: 1_000));

        decision.ShouldRasterise.Should().BeFalse();
        decision.Reason.Should().Be(PromptRasterDecisionReason.MaximumPageCountExceeded);
    }
}
