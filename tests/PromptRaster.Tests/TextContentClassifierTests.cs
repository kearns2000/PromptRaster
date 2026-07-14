using FluentAssertions;
using PromptRaster.Internal;
using Xunit;

namespace PromptRaster.Tests;

public class TextContentClassifierTests
{
    private readonly TextContentClassifier _classifier = new();

    [Fact]
    public void NormalProse_IsProse()
    {
        const string text =
            "The committee convened early on Tuesday morning to review the quarterly findings. " +
            "Although the agenda promised a brisk session, the discussion of regional supply " +
            "variations stretched well past the scheduled hour. Several members observed that " +
            "seasonal demand had shifted noticeably compared with previous years.";

        _classifier.Classify(text).Should().Be(TextContentClassification.Prose);
    }

    [Fact]
    public void ProseWithUrlsAndEmails_IsStillProse()
    {
        const string text =
            "Please review the updated policy at https://example.com/policies/2026-retention " +
            "and send any comments to compliance-review@example.com before Friday. The document " +
            "expands on the guidance published last quarter, and the working group would like " +
            "feedback from every region before the next scheduled review meeting takes place. " +
            "You can also find the archive at www.example.org/archive for earlier versions.";

        _classifier.Classify(text).Should().Be(TextContentClassification.Prose);
    }

    [Fact]
    public void Json_IsStructured()
    {
        const string text =
            """
            {
              "customer": { "id": 42, "name": "Acme Ltd" },
              "orders": [
                { "id": 1001, "total": 199.95, "status": "shipped" },
                { "id": 1002, "total": 49.50, "status": "pending" }
              ]
            }
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Structured);
    }

    [Fact]
    public void Xml_IsStructured()
    {
        const string text =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <catalog>
              <book id="bk101"><author>Gambardella, Matthew</author><title>XML Developer's Guide</title></book>
              <book id="bk102"><author>Ralls, Kim</author><title>Midnight Rain</title></book>
            </catalog>
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Structured);
    }

    [Fact]
    public void Yaml_IsStructured()
    {
        const string text =
            """
            apiVersion: apps/v1
            kind: Deployment
            metadata:
              name: web-frontend
              labels:
                app: web
            spec:
              replicas: 3
              selector:
                matchLabels:
                  app: web
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Structured);
    }

    [Fact]
    public void CSharp_IsCode()
    {
        const string text =
            """
            public sealed class OrderService
            {
                private readonly IOrderRepository _repository;

                public OrderService(IOrderRepository repository)
                {
                    _repository = repository;
                }

                public async Task<Order?> GetAsync(int id, CancellationToken cancellationToken)
                {
                    return await _repository.FindAsync(id, cancellationToken);
                }
            }
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Code);
    }

    [Fact]
    public void JavaScript_IsCode()
    {
        const string text =
            """
            const express = require('express');
            const app = express();

            app.get('/orders/:id', async (req, res) => {
                const order = await repository.find(req.params.id);
                if (!order) {
                    return res.status(404).send();
                }
                res.json(order);
            });

            app.listen(3000);
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Code);
    }

    [Fact]
    public void MarkdownProse_IsProse()
    {
        const string text =
            """
            # Quarterly Review

            The committee convened early on Tuesday morning to review the quarterly findings.
            Although the agenda promised a brisk session, the discussion stretched past the hour.

            ## Regional Findings

            Several members observed that seasonal demand had shifted noticeably compared with
            previous years, and that the causes were unlikely to be explained by weather alone.
            The analysts argued that a gradual migration of customers toward digital channels
            had quietly reshaped expectations around delivery timelines for most regions.
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Prose);
    }

    [Fact]
    public void MarkdownWithLargeFencedCode_IsCode()
    {
        const string text =
            """
            # Setup Guide

            Run the following before starting:

            ```csharp
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddPromptRaster(options =>
            {
                options.MinimumTextLength = 6000;
                options.MaximumPages = 10;
            });
            var app = builder.Build();
            app.MapGet("/", () => "Hello");
            app.Run();
            ```

            Then verify the output.
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Code);
    }

    [Fact]
    public void Csv_IsTabular()
    {
        const string text =
            """
            id,name,region,total,status
            1001,Acme Ltd,North,199.95,shipped
            1002,Globex,South,49.50,pending
            1003,Initech,East,320.00,shipped
            1004,Umbrella,West,75.25,cancelled
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Tabular);
    }

    [Fact]
    public void PipeDelimitedTable_IsTabular()
    {
        const string text =
            """
            | Id   | Name     | Region | Total  |
            |------|----------|--------|--------|
            | 1001 | Acme Ltd | North  | 199.95 |
            | 1002 | Globex   | South  | 49.50  |
            | 1003 | Initech  | East   | 320.00 |
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Tabular);
    }

    [Fact]
    public void StackTrace_IsCode()
    {
        const string text =
            """
            System.InvalidOperationException: Sequence contains no elements
               at System.Linq.ThrowHelper.ThrowNoElementsException()
               at System.Linq.Enumerable.First[TSource](IEnumerable`1 source)
               at OrderService.GetLatestAsync(Int32 customerId) in /src/OrderService.cs:line 42
               at OrdersController.Get(Int32 id) in /src/OrdersController.cs:line 18
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Code);
    }

    [Fact]
    public void IniConfiguration_IsStructured()
    {
        const string text =
            """
            [database]
            host = db.internal.example.com
            port = 5432
            name = orders

            [cache]
            host = cache.internal.example.com
            ttl_seconds = 300
            enabled = true
            """;

        _classifier.Classify(text).Should().Be(TextContentClassification.Structured);
    }

    [Fact]
    public void LongIdentifiers_AreIdentifierHeavy()
    {
        var tokens = Enumerable.Range(0, 40)
            .Select(static i => $"a1b2c3d4-e5f6-7890-abcd-ef{i:00}34567890{Guid.NewGuid():N}");
        var text = "Session keys: " + string.Join(" ", tokens);

        _classifier.Classify(text).Should().Be(TextContentClassification.IdentifierHeavy);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void EmptyOrWhitespace_IsEmpty(string text)
    {
        _classifier.Classify(text).Should().Be(TextContentClassification.Empty);
    }

    [Fact]
    public void MalformedJsonLikeText_DoesNotThrow()
    {
        const string text = "{ this is not valid json, but it starts and ends with braces }";

        var act = () => _classifier.Classify(text);

        act.Should().NotThrow();
    }

    [Fact]
    public void MalformedXmlLikeText_DoesNotThrow()
    {
        const string text = "<note>this looks like xml but <is not closed properly>";

        var act = () => _classifier.Classify(text);

        act.Should().NotThrow();
    }
}
