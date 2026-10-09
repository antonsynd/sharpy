using System.Text.RegularExpressions;
using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;
// The enclosing `Sharpy` namespace exposes Sharpy.List<T>, which shadows System's List<T> here.
using SCG = System.Collections.Generic;
using LspRange = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace Sharpy.Lsp.Tests.Conformance;

/// <summary>
/// Keeps the examples in <c>docs/tooling/formatter.md</c>'s "In the editor (LSP)" section executed
/// (#2168, plan P22e Phase 4): every example is driven through the real formatting handlers by
/// <see cref="LspFormattingDriver"/>, and the text the handlers' edits produce must be the doc's.
///
/// <para>
/// An example is an HTML comment followed by its fenced <c>python</c> blocks:
/// <c>&lt;!-- editor-example: REQUEST[, REQUEST…][; unchanged] --&gt;</c>, where a REQUEST is
/// <c>document</c>, <c>selection A-B</c> (lines A..B, 1-based as an editor shows them, selected
/// whole) or <c>on-type N</c> (1-based line). Without <c>unchanged</c>: one request and two blocks,
/// before and after. With it: one block, and every request returns no edits.
/// </para>
/// </summary>
public sealed class FormatterDocEditorExamplesTests : IDisposable
{
    private const string SectionHeading = "## In the editor (LSP)";
    private const string Fence = "```python";

    private static readonly Regex MarkerPattern = new(@"^<!-- editor-example: (?<body>.+?) -->$");

    private readonly LspFormattingDriver _driver = new();

    public void Dispose() => _driver.Dispose();

    private sealed record Request(string Kind, int StartLine, int EndLine);

    private sealed record Example(int MarkerLine, SCG.IReadOnlyList<Request> Requests, string Before, string? After)
    {
        public override string ToString() => $"formatter.md:{MarkerLine}";
    }

    [Fact]
    public void EveryEditorExample_IsWhatTheHandlersApply()
    {
        var examples = ReadExamples();

        // The instrument: the extractor found the examples the doc has (a literal, not a count of the
        // same source), and at least one changes its text — so equality is not passing on identity.
        examples.Should().HaveCount(9);
        examples.SelectMany(e => e.Requests).Should().HaveCount(17);
        examples.Count(e => e.After is not null && e.After != e.Before).Should().Be(3);

        foreach (var example in examples)
        {
            foreach (var request in example.Requests)
            {
                var (edits, applied) = Drive(example.Before, request);
                if (example.After is null)
                {
                    edits.Should().BeEmpty($"{example} {request} is documented as applying nothing");
                    applied.Should().Be(example.Before, $"{example} {request}");
                }
                else
                {
                    applied.Should().Be(example.After, $"{example} {request} is documented with this result");
                }
            }
        }
    }

    /// <summary>
    /// Positive control for the "applies nothing" examples: each one that does not itself request Format
    /// Document is a file Format Document DOES change — so its empty edit list is a refusal, not a file
    /// with nothing to format.
    /// </summary>
    [Fact]
    public void EveryUnchangedSelectionOrOnTypeExample_IsAFileFormatDocumentChanges()
    {
        var controls = ReadExamples()
            .Where(e => e.After is null && e.Requests.All(r => r.Kind != "document"))
            .ToList();

        controls.Should().HaveCount(2);
        foreach (var example in controls)
            _driver.Full(example.Before).Applied.Should().NotBe(example.Before, $"{example}");
    }

    private (SCG.IReadOnlyList<TextEdit> Edits, string Applied) Drive(string text, Request request)
    {
        switch (request.Kind)
        {
            case "document":
                return _driver.Full(text);
            case "selection":
                var lines = text.Split('\n');
                var end = request.EndLine - 1;
                return _driver.Range(text, new LspRange(
                    new Position(request.StartLine - 1, 0), new Position(end, lines[end].Length)));
            case "on-type":
                return _driver.OnType(text, request.StartLine - 1);
            default:
                throw new InvalidOperationException($"unknown request kind '{request.Kind}'");
        }
    }

    private static SCG.List<Example> ReadExamples()
    {
        var path = System.IO.Path.Combine(FindRepoRoot(), "docs", "tooling", "formatter.md");
        var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');

        var start = Array.IndexOf(lines, SectionHeading);
        start.Should().BeGreaterThanOrEqualTo(0, $"formatter.md has the section '{SectionHeading}'");
        var end = start + 1;
        while (end < lines.Length && !lines[end].StartsWith("## ", StringComparison.Ordinal))
            end++;

        var examples = new SCG.List<Example>();
        (int Line, SCG.List<Request> Requests, bool Unchanged)? marker = null;
        var blocks = new SCG.List<string>();

        void Close()
        {
            if (marker is null)
                return;
            var (line, requests, unchanged) = marker.Value;
            if (unchanged)
            {
                blocks.Should().HaveCount(1, $"the example at formatter.md:{line} applies nothing, so it has one block");
                examples.Add(new Example(line, requests, blocks[0], null));
            }
            else
            {
                blocks.Should().HaveCount(2, $"the example at formatter.md:{line} has a before and an after block");
                requests.Should().HaveCount(1, $"the example at formatter.md:{line} has one result, so one request");
                examples.Add(new Example(line, requests, blocks[0], blocks[1]));
            }
            blocks.Clear();
        }

        for (var i = start + 1; i < end; i++)
        {
            var match = MarkerPattern.Match(lines[i]);
            if (match.Success)
            {
                Close();
                marker = ParseMarker(match.Groups["body"].Value, i + 1);
                continue;
            }
            if (lines[i] != Fence)
                continue;

            // Every Sharpy block in the section belongs to an example, so a new one cannot go unexecuted.
            marker.Should().NotBeNull($"the block at formatter.md:{i + 1} follows an editor-example marker");
            var body = new SCG.List<string>();
            i++;
            while (i < end && lines[i] != "```")
                body.Add(lines[i++]);
            (i < end).Should().BeTrue($"the block before formatter.md:{i + 1} is closed");
            blocks.Add(string.Concat(body.Select(l => l + "\n")));
        }
        Close();
        return examples;
    }

    private static (int, SCG.List<Request>, bool) ParseMarker(string body, int line)
    {
        var parts = body.Split(';', StringSplitOptions.TrimEntries);
        var unchanged = parts.Length == 2 && parts[1] == "unchanged";
        (parts.Length == 1 || unchanged).Should().BeTrue($"formatter.md:{line} marker '{body}' is REQUESTS[; unchanged]");

        var requests = new SCG.List<Request>();
        foreach (var spec in parts[0].Split(',', StringSplitOptions.TrimEntries))
        {
            var words = spec.Split(' ');
            requests.Add(words switch
            {
                ["document"] => new Request("document", 0, 0),
                ["selection", var range] when range.Split('-') is [var a, var b]
                    => new Request("selection", int.Parse(a), int.Parse(b)),
                ["on-type", var n] => new Request("on-type", int.Parse(n), int.Parse(n)),
                _ => throw new InvalidOperationException($"formatter.md:{line}: unknown request '{spec}'"),
            });
        }
        return (line, requests, unchanged);
    }

    private static string FindRepoRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(System.IO.Path.Combine(dir, "sharpy.sln")))
                return dir;
            dir = System.IO.Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not locate the repo root (sharpy.sln) above the test assembly.");
    }
}
