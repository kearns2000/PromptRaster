using FluentAssertions;
using PromptRaster.Internal;
using Xunit;

namespace PromptRaster.Tests;

public class ExactContentDetectorTests
{
    private readonly ExactContentDetector _detector = new();

    [Fact]
    public void OrdinaryProse_IsNotExact()
    {
        const string text =
            "The committee reviewed the quarterly findings and noted that regional supply " +
            "patterns had shifted meaningfully during recent months across several markets.";

        _detector.LooksExactOrSensitive(text, out var reason).Should().BeFalse();
        reason.Should().BeNull();
    }

    [Fact]
    public void ApiKeyKeyword_IsExact()
    {
        const string text =
            "Deploy using api_key=sk-test-not-a-real-secret and restart the worker after " +
            "the configuration change has been applied to every region.";

        _detector.LooksExactOrSensitive(text, out var reason).Should().BeTrue();
        reason.Should().Be("secret_or_credential");
    }

    [Fact]
    public void LongHashes_AreExact()
    {
        var text =
            "Checksums follow: " +
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 " +
            "d2ddea18f00665ce8623e36bd4e3ace6e1e544e06b135dac0e628c085f5b2f1a " +
            "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b";

        _detector.LooksExactOrSensitive(text, out var reason).Should().BeTrue();
        reason.Should().Be("long_hash");
    }

    [Fact]
    public void GuidHeavyContent_IsExact()
    {
        var text =
            "Ids: 11111111-1111-1111-1111-111111111111 " +
            "22222222-2222-2222-2222-222222222222 " +
            "33333333-3333-3333-3333-333333333333 " +
            "44444444-4444-4444-4444-444444444444";

        _detector.LooksExactOrSensitive(text, out var reason).Should().BeTrue();
        reason.Should().Be("guid_heavy");
    }

    [Fact]
    public void WindowsPaths_AreExact()
    {
        const string text =
            "Copy C:\\ProgramData\\App\\config\\prod.json and also " +
            "D:\\Shares\\Archive\\2026\\ledger\\entry-final.json before continuing.";

        _detector.LooksExactOrSensitive(text, out var reason).Should().BeTrue();
        reason.Should().Be("path_heavy");
    }

    [Fact]
    public void ProseWithHttpsUrls_IsNotPathHeavy()
    {
        const string text =
            "Please review the updated policy at https://example.com/policies/2026-retention " +
            "and send any comments to compliance-review@example.com before Friday. The document " +
            "expands on the guidance published last quarter, and the working group would like " +
            "feedback from every region before the next scheduled review meeting takes place. " +
            "You can also find the archive at www.example.org/archive for earlier versions.";

        _detector.LooksExactOrSensitive(text, out _).Should().BeFalse();
    }
}
