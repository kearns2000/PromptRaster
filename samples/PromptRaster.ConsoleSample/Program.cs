using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PromptRaster;
using PromptRaster.ConsoleSample;

var services = new ServiceCollection()
    .AddLogging(static builder => builder.AddSimpleConsole().SetMinimumLevel(LogLevel.Information))
    .AddPromptRaster()
    .BuildServiceProvider();

var rasterizer = services.GetRequiredService<IPromptRasterizer>();

// Use a caller-supplied prose file when given, otherwise generate representative prose.
var text = args.Length > 0 && File.Exists(args[0])
    ? await File.ReadAllTextAsync(args[0])
    : SampleProse.Generate(targetCharacterCount: 42_000);

Console.WriteLine($"Source: {text.Length.ToString("N0", CultureInfo.InvariantCulture)} characters");
Console.WriteLine();

AiProvider[] providers = [AiProvider.OpenAI, AiProvider.AzureOpenAI, AiProvider.Anthropic, AiProvider.Gemini];

foreach (var provider in providers)
{
    var result = await rasterizer.RasterizeAsync(text, provider);

    Console.WriteLine($"Provider:                    {provider}");
    Console.WriteLine($"Decision:                    {result.Decision.Reason}");
    Console.WriteLine($"                             {result.Decision.Description}");
    Console.WriteLine($"Character count:             {Format(result.CharacterCount)}");
    Console.WriteLine($"Page count:                  {Format(result.PageCount)}");
    Console.WriteLine($"Average characters per page: {Format((int)result.AverageCharactersPerPage)}");
    Console.WriteLine($"Required characters per page:{Format(result.RequiredCharactersPerPage),14}");
    Console.WriteLine($"Output encoding:             {result.Encoding}");
    Console.WriteLine($"Total PNG bytes:             {Format(result.Pages.Sum(static p => p.Data.Length))}");
    Console.WriteLine();

    if (result.Encoding == PromptRasterEncoding.Images)
    {
        var directory = Path.Combine("output", provider.ToString().ToLowerInvariant());
        Directory.CreateDirectory(directory);

        foreach (var page in result.Pages)
        {
            var path = Path.Combine(directory, $"page-{page.PageNumber:000}.png");
            await File.WriteAllBytesAsync(path, page.Data);
            Console.WriteLine($"  Saved {path} ({Format(page.Data.Length)} bytes, {Format(page.CharacterCount)} source characters)");
        }

        Console.WriteLine();
    }
}

static string Format(int value) => value.ToString("N0", CultureInfo.InvariantCulture);
