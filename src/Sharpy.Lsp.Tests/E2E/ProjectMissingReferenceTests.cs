using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests.E2E;

/// <summary>
/// Project mode publishes a <c>&lt;Reference&gt;</c> that resolves to no file as SPY0305 (#2223,
/// plan-05dd79 Phase 3): <c>CompilerApi.AnalyzeProject</c> — the editor's project door — merges
/// the registry's SPY0305 into its result, and the file-less entry is published on the project's
/// unattributed target (the entry point; D3/D5 of #2242), with no new placement rule.
/// </summary>
/// <remarks>
/// The registry behind that door is cached by reference path + mtime. The reference appearing must
/// clear the diagnostic on the next analysis (the positive control that the cache does not pin the
/// error), and the reference vanishing again must bring it back. A <c>.dll</c> is not a watched
/// file, so each step triggers the re-analysis with a watched change to <c>main.spy</c>.
/// </remarks>
public sealed class ProjectMissingReferenceTests
{
    private const string PublishDiagnostics = "textDocument/publishDiagnostics";
    private const string Spy0305 = "SPY0305";

    private readonly ITestOutputHelper _output;

    public ProjectMissingReferenceTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task MissingReference_IsPublishedOnTheEntryFile_AndClearsWhenItAppears_AndReturnsWhenItVanishes()
    {
        var dir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_missing_ref_e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(IOPath.Combine(dir, "main.spy"), "def main() -> None:\n    print(\"hi\")\n");
            File.WriteAllText(IOPath.Combine(dir, "test.spyproj"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Project>\n    <PropertyGroup>\n"
                + "        <RootNamespace>Test</RootNamespace>\n        <OutputType>exe</OutputType>\n"
                + "        <EntryPoint>main.spy</EntryPoint>\n"
                + "    </PropertyGroup>\n    <ItemGroup>\n"
                + "        <SpyFile Include=\"main.spy\" />\n"
                + "        <Reference Include=\"Absent\"><HintPath>libs/Absent.dll</HintPath></Reference>\n"
                + "    </ItemGroup>\n</Project>\n");
            var referencePath = IOPath.Combine(dir, "libs", "Absent.dll");
            var mainUri = new Uri(IOPath.Combine(dir, "main.spy")).AbsoluteUri;
            var received = new System.Collections.Generic.List<string>();

            await using var client = LspTestClient.Start(_output);
            await client.InitializeAsync(new Uri(dir).AbsoluteUri);

            var indexed = await AwaitMainAsync(client, received, codes => codes.Contains(Spy0305));
            indexed.Should().BeTrue($"index: SPY0305 is published on the entry file; received {Describe(received)}");

            EmitTinyAssembly(referencePath);
            await client.SendNotificationAsync("workspace/didChangeWatchedFiles", WatchedChange(mainUri));
            var cleared = await AwaitMainAsync(client, received, codes => !codes.Contains(Spy0305));
            cleared.Should().BeTrue($"appear: the reference exists now, so SPY0305 clears; received {Describe(received)}");

            File.Delete(referencePath);
            await client.SendNotificationAsync("workspace/didChangeWatchedFiles", WatchedChange(mainUri));
            var returned = await AwaitMainAsync(client, received, codes => codes.Contains(Spy0305));
            returned.Should().BeTrue($"vanish: the reference is gone again, so SPY0305 returns; received {Describe(received)}");
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Reads publications until one for main.spy satisfies <paramref name="done"/>, or the arrival timeout.</summary>
    private static async Task<bool> AwaitMainAsync(
        LspTestClient client, System.Collections.Generic.List<string> received, Func<string[], bool> done)
    {
        var deadline = DateTime.UtcNow + LspTestClient.ArrivalTimeout;
        while (true)
        {
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero)
                return false;

            JsonNode notification;
            try
            {
                notification = await client.WaitForNotificationAsync(PublishDiagnostics, left);
            }
            catch (TimeoutException)
            {
                return false;
            }

            var file = IOPath.GetFileName(new Uri(notification["uri"]!.GetValue<string>()).LocalPath);
            var codes = notification["diagnostics"]!.AsArray()
                .Select(d => d?["code"]?.ToString() ?? "?")
                .OrderBy(c => c, StringComparer.Ordinal)
                .ToArray();
            received.Add($"{file}=[{string.Join(",", codes)}]");
            if (file == "main.spy" && done(codes))
                return true;
        }
    }

    private static JsonObject WatchedChange(string uri) => new()
    {
        ["changes"] = new JsonArray(new JsonObject { ["uri"] = uri, ["type"] = 2 }),
    };

    private static string Describe(System.Collections.Generic.List<string> received)
        => received.Count == 0 ? "(nothing)" : string.Join(" → ", received);

    /// <summary>A uniquely named, loadable assembly, so its load collides with nothing the server holds.</summary>
    private static void EmitTinyAssembly(string path)
    {
        Directory.CreateDirectory(IOPath.GetDirectoryName(path)!);
        var compilation = CSharpCompilation.Create(
            "RefProbe_" + Guid.NewGuid().ToString("N"),
            new[] { CSharpSyntaxTree.ParseText("public static class RefProbe { public static int One() => 1; }") },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics));
    }
}
