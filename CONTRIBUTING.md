# Contributing to PromptRaster

Thanks for your interest in contributing. PromptRaster is a small, focused library — contributions that improve the rasterisation decision, layout fidelity, or provider heuristics are welcome.

## Before you start

- Search [existing issues](https://github.com/kearns2000/PromptRaster/issues) to avoid duplicate work.
- For large changes (new providers, API changes, architecture), open an issue first to discuss approach.
- Keep pull requests focused. One feature or fix per PR is easier to review.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- Any editor (VS, VS Code, Rider)
- On Linux, SkiaSharp needs `libfontconfig1` (`sudo apt-get install libfontconfig1`)

## Getting started

```bash
git clone https://github.com/kearns2000/PromptRaster.git
cd PromptRaster
dotnet build
dotnet test
```

Run the sample:

```bash
dotnet run --project samples/PromptRaster.ConsoleSample
```

## Project layout

```text
src/PromptRaster/                        # Core library (no AI provider SDKs, no MEAI)
  Internal/                              # Classifier, policy, layout, renderer, rasteriser
src/PromptRaster.MicrosoftExtensionsAI/  # Content factory + DelegatingChatClient middleware
tests/PromptRaster.Tests/                # xUnit tests for the core
tests/PromptRaster.MicrosoftExtensionsAI.Tests/
samples/PromptRaster.ConsoleSample/
```

## Making changes

### Bug fixes

1. Add a failing test in `tests/PromptRaster.Tests/` that reproduces the bug.
2. Fix the issue in `src/PromptRaster/`.
3. Ensure `dotnet test -c Release` passes.

### Classifier heuristics

The classifier (`Internal/TextContentClassifier.cs`) decides which content is unsuitable for automatic rasterisation. It must stay cheap, deterministic, and conservative.

1. Add representative positive *and* negative examples to `TextContentClassifierTests.cs` first.
2. Implement the heuristic. Prefer prefix checks before any parsing, and never let a parse failure escape.
3. Ordinary prose must never be rejected merely because it contains URLs, email addresses, or the odd identifier.

### Provider thresholds and new providers

1. Add the enum value to `AiProvider` and a `<Provider>MinimumCharactersPerPage` option to `PromptRasterOptions`.
2. Wire it into `PromptRasterOptionsValidator` and `ProviderThresholdResolver`.
3. Choose a conservative default and explain the reasoning in the PR description.
4. Add decision tests and update the threshold table in `README.md`.

### Layout and rendering

The layout engine's contract is an invariant verified by tests: page source ranges are contiguous, non-overlapping, and reconstruct the input exactly. Any layout change must keep `SourceRanges_ReconstructOriginalTextExactly` and its siblings green.

### Public API changes

- Keep the public surface small; implementation types stay `internal`.
- Update `README.md` for any user-visible API change.
- Avoid breaking changes in patch/minor releases without discussion.

## Code guidelines

- Use nullable reference types; avoid suppressing null warnings without reason.
- The build treats warnings as errors — keep it that way.
- Use primary constructors for injected dependencies; register stateless services as singletons.
- Dispose every SkiaSharp resource; services must remain safe for concurrent use.
- Never log source text, snippets, or image bytes — metadata only.
- No token-estimation cleverness: the decision uses measured character density only.
- Match existing naming and file structure.

## Testing expectations

All PRs should pass:

```bash
dotnet build -c Release
dotnet test -c Release
```

Add tests when you:

- Fix a bug
- Add or change a classifier heuristic
- Add a provider or change threshold resolution
- Touch layout, pagination, or rendering
- Change the decision algorithm or its descriptions

Avoid brittle whole-image snapshot assertions; inspect PNG headers, dimensions, page metadata, and reconstructed source ranges instead.

## Pull request checklist

- [ ] `dotnet build -c Release` succeeds
- [ ] `dotnet test -c Release` passes
- [ ] Tests added or updated for the change
- [ ] README updated if public API or behaviour changed
- [ ] No unrelated formatting or drive-by refactors

## Code of conduct

This project follows the [Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to uphold it.

## Questions

Open a [GitHub issue](https://github.com/kearns2000/PromptRaster/issues) for questions or ideas.
