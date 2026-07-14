![PromptRaster](https://raw.githubusercontent.com/kearns2000/PromptRaster/main/icon.png)

# PromptRaster

[![NuGet](https://img.shields.io/nuget/v/PromptRaster?style=flat&logo=nuget)](https://www.nuget.org/packages/PromptRaster)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Build](https://github.com/kearns2000/PromptRaster/actions/workflows/build.yml/badge.svg)](https://github.com/kearns2000/PromptRaster/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/github/license/kearns2000/PromptRaster)](LICENSE)
[![Tests](https://img.shields.io/badge/tests-xUnit-5C2D91?style=flat&logo=xunit)](tests/PromptRaster.Tests)

**Target framework:** `net8.0` · **Language:** C# 12 · **Test runner:** xUnit

**PromptRaster turns long prose into cheaper pixels.**

PromptRaster converts long plain-text content into one or more OCR-friendly PNG images when doing so is likely to use fewer AI input tokens than sending the original text.

> PromptRaster uses conservative character-density heuristics to decide when long prose is likely to cost fewer AI input tokens as an image. It keeps the original text when the content is short, structured, accuracy-sensitive, or insufficiently dense.

PromptRaster never sends anything to an AI provider itself, and it never modifies, truncates, or discards your source content. It only decides, lays out, and renders — you stay in control of what actually gets sent.

## Why images can cost fewer tokens

Text models charge roughly one token per 3–4 characters of English text, so a 40,000-character document costs on the order of 10,000+ input tokens. Multimodal models charge for images by tile or by resolution, largely independent of how much text the image contains. A 1024×1536 page rendered at a readable 17 px costs a fixed number of image tokens regardless of whether it holds 2,000 or 8,000 characters.

When a page carries enough characters, the image representation crosses over and becomes cheaper than the text representation. PromptRaster measures the actual character density your text achieves after layout and compares it against a conservative per-provider threshold. Long, dense prose may reduce input usage by approximately 20–50%, depending on the model and image-token rules.

**Savings are estimates, not guarantees.** Provider pricing and image-token rules change, model OCR accuracy varies, and dense pages can degrade answer quality for some tasks. Benchmark against your real workload — with your model, your documents, and your quality bar — before enabling this in production.

## Limitations

- The model must *read* your text via OCR instead of receiving it losslessly. Reading accuracy is high for clean prose on modern multimodal models, but it is not perfect.
- Character-exact tasks (quoting, extracting identifiers, diffing) should not use rasterised input. PromptRaster's classifier refuses such content in automatic mode for this reason.
- Thresholds are conservative heuristics at the provider level. PromptRaster deliberately does not do exact token accounting, model-specific pricing lookups, or remote catalogue fetching.
- Rendered pages are larger *payloads* (bytes on the wire) than the text they replace, even when they are cheaper in tokens.

## What it is good for

Automatic rasterisation suits tasks where the model reads and reasons over long prose:

- document summarisation
- classification
- policy review
- article analysis
- transcripts
- long email bodies
- broad information extraction

## What it refuses (in automatic mode)

Automatic rasterisation is disabled when the content is likely to be:

- source code
- JSON
- XML
- YAML
- tables (CSV, pipe-delimited, tabular data)
- configuration
- stack traces
- exact quotations
- identifiers where character accuracy is critical

These are accuracy-sensitive: a single misread character changes meaning. They stay as text unless you explicitly force rasterisation with `PromptRasterMode.Always`.

## Installation

```bash
dotnet add package PromptRaster
```

For the Microsoft.Extensions.AI integration:

```bash
dotnet add package PromptRaster.MicrosoftExtensionsAI
```

## Dependency injection setup

```csharp
services.AddPromptRaster();
```

Or with options:

```csharp
services.AddPromptRaster(options =>
{
    options.MinimumTextLength = 6_000;
    options.MaximumPages = 10;
});
```

All services are stateless, thread-safe singletons. Options are validated at startup; invalid values (non-positive dimensions, padding that leaves no drawable area, thresholds below 1,000 characters, and so on) fail fast with a descriptive message.

## Basic usage

```csharp
public sealed class DocumentAnalyzer(IPromptRasterizer rasterizer)
{
    public async Task AnalyzeAsync(string documentText, CancellationToken cancellationToken)
    {
        PromptRasterResult result = await rasterizer.RasterizeAsync(
            documentText,
            AiProvider.OpenAI,
            cancellationToken: cancellationToken);

        if (result.Encoding == PromptRasterEncoding.Images)
        {
            foreach (PromptRasterPage page in result.Pages)
            {
                // page.Data is a PNG; page.MediaType is "image/png".
                // page.SourceStartIndex / page.SourceLength map back to the source text.
            }
        }
        else
        {
            // result.OriginalText is your input, unchanged.
        }

        Console.WriteLine(result.Decision.Description);
        // e.g. "The content was kept as text because its average density was
        //       4,920 characters per page, below the OpenAI threshold of 6,000."
    }
}
```

The result always includes the decision reason, the source SHA-256 (uppercase hex, for traceability), the character count, the page count, and the measured character density.

## Microsoft.Extensions.AI usage

The integration package provides `IPromptRasterContentFactory`, which builds `AIContent` items ready for any `IChatClient`. Rasterisation is explicit — the factory only ever converts the document you pass it, never arbitrary chat messages.

```csharp
services.AddPromptRasterMicrosoftExtensionsAI();
```

```csharp
var content = await promptRasterContentFactory.CreateAsync(
    "Summarise the following document.",
    documentText,
    AiProvider.OpenAI,
    cancellationToken: cancellationToken);

var response = await chatClient.GetResponseAsync(
    new ChatMessage(ChatRole.User, content),
    cancellationToken: cancellationToken);
```

The instruction is always included as text. When PromptRaster decides on text, the document follows as text content. When it decides on images, a short note tells the model to read all pages in numerical order, followed by one `DataContent` PNG per page.

## Provider thresholds

PromptRaster rasterises only when the *average characters per rendered page* meets the provider's threshold:

| Provider | Default minimum characters per page |
|---|---|
| OpenAI | 6,000 |
| Azure OpenAI | 6,000 |
| Gemini | 5,500 |
| Anthropic | 8,000 |
| Unknown | never automatically rasterised |

These defaults are intentionally conservative: they only trigger when the saving is likely to be comfortably real, not marginal.

### Overriding thresholds

Globally, via options:

```csharp
services.AddPromptRaster(options =>
{
    options.AnthropicMinimumCharactersPerPage = 7_000;
});
```

Per request:

```csharp
var result = await rasterizer.RasterizeAsync(text, AiProvider.Anthropic,
    new PromptRasterRequest { MinimumCharactersPerPage = 7_000 });
```

## Forcing or disabling rasterisation

```csharp
// Always rasterise (skips classification and thresholds; still enforces
// the absolute page limit and rejects empty input):
new PromptRasterRequest { Mode = PromptRasterMode.Always }

// Never rasterise (returns text without any layout work):
new PromptRasterRequest { Mode = PromptRasterMode.Never }

// Skip content-type detection but keep the density check:
new PromptRasterRequest { TreatAsProse = true }
```

## The decision algorithm (Auto mode)

1. Empty or whitespace-only input stays text.
2. Unknown providers stay text.
3. Input shorter than `MinimumTextLength` (default 5,000) stays text.
4. Content classified as structured, code, tabular, or identifier-heavy stays text.
5. The text is laid out into 1024×1536 pages using measured pixel widths.
6. More than `MaximumPages` (default 8) stays text.
7. Rasterise only if `characters ÷ pages ≥ provider threshold`; otherwise stay text.

PNG encoding happens only after the decision to return images, and every decision is explained in `Result.Decision.Description`.

## Security and privacy

- PromptRaster performs no network calls of any kind. All classification, layout, and rendering happen in-process.
- Logging emits metadata only (provider, decision reason, counts, sizes, timings). The source text, snippets of it, and image bytes are never logged.
- The `SourceSha256` hash lets you correlate results with source documents without storing the text.
- Remember that a rendered page *contains* the source text as pixels. Treat page images with the same confidentiality as the text itself.

## Architecture

The solution contains two packages and a sample:

- **PromptRaster** — the core. No AI provider SDK dependencies; only `Microsoft.Extensions.*` abstractions and SkiaSharp.
- **PromptRaster.MicrosoftExtensionsAI** — a thin content factory over the core that produces `Microsoft.Extensions.AI` content items.
- **PromptRaster.ConsoleSample** — a runnable demo that renders sample prose for every provider and writes the PNGs to `output/<provider>/page-NNN.png`. No API key required.

Internally, the core separates four small, individually testable concerns behind internal interfaces:

1. **Classification** (`TextContentClassifier`) — cheap deterministic heuristics that detect JSON/XML (prefix check, then a real parse that never throws outward), YAML/INI configuration, source code, Markdown dominated by fenced code, CSV and pipe tables, stack traces, and identifier-heavy or punctuation-heavy content. Ordinary prose containing URLs or email addresses is still prose.
2. **Layout** (`SkiaTextPageLayoutEngine`) — wraps text by measured pixel width (`SKFont.BreakText`), preserves paragraph and line breaks, avoids splitting words unless a single token is wider than a line, and paginates. Every page records the exact source range it represents; concatenating all ranges reconstructs the input byte-for-byte. Layout is decision input — no pixels are rendered at this stage.
3. **Decision** (`PromptRasterizer`) — implements the algorithm above and produces a fully explained result.
4. **Rendering** (`SkiaTextImageRenderer`) — draws the already-computed layout as anti-aliased black text on a white background and encodes grayscale PNG (small payloads, OCR-unaffected). Uses `SKTypeface.Default`, so no font file ships with the package.

Significant design decisions:

- **Density is measured, not estimated.** The layout runs first and the decision uses the real achieved characters-per-page, so wrapping behaviour, paragraph structure, and header space are all accounted for. There is no token-estimation cleverness anywhere.
- **Source fidelity is an invariant, not a goal.** The layout engine's contract (verified by tests) is that page source ranges are contiguous, non-overlapping, and reconstruct the input exactly. Nothing is trimmed or normalised.
- **Conservative by default.** Unknown providers, short text, structured content, low density, or too many pages all fall back to text. Text is always the safe answer; images are only used when the case is strong.
- **Thread safety through statelessness.** Every service is immutable; SkiaSharp objects are created and disposed per call. All services register as singletons.

## Building from source

```bash
dotnet restore
dotnet build --configuration Release
dotnet test --configuration Release --no-build
dotnet pack --configuration Release --no-build
```

Run the sample:

```bash
dotnet run --project samples/PromptRaster.ConsoleSample
```

See [PUBLISHING.md](PUBLISHING.md) for how releases are published to NuGet.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for setup, project layout, and how to add classifier heuristics or provider thresholds.

Please read our [Code of Conduct](CODE_OF_CONDUCT.md) before participating.

Quick start for contributors:

```bash
git clone https://github.com/kearns2000/PromptRaster.git
cd PromptRaster
dotnet build -c Release
dotnet test -c Release
```

Open a pull request with tests for any behaviour change. CI runs build and test on every PR.

## License

MIT
