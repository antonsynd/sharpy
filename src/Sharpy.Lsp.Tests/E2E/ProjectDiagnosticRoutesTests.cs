using System.Text.Json.Nodes;
using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Utilities;
using Xunit;
using Xunit.Abstractions;
using IOPath = System.IO.Path;

namespace Sharpy.Lsp.Tests.E2E;

/// <summary>
/// The route × state matrix of project-mode diagnostics against the real server (#2242,
/// plan-6ce1a5 Task 8): after every route that can change what the editor holds, the LAST
/// <c>textDocument/publishDiagnostics</c> per file, as a code multiset, equals the build's
/// diagnostics for that file — <c>CompilerApi.AnalyzeProject</c> over the same on-disk + buffer
/// program, projected per file by the same rule the build's per-file view uses
/// (<c>ProjectAnalysisResult.GetFileResult</c>, falling back to
/// <c>ProjectModel.DiagnosticsForFile</c> for a file the analysis has no unit for).
/// </summary>
/// <remarks>
/// <para>Routes are the server's: the in-process parity sweep (FrontEndParityTests' LSP-project
/// pair) cannot open, edit, close, watch or reconfigure. A route that must publish is required to
/// publish every project file after its action — so a route that silently publishes nothing
/// cannot pass on the strength of an earlier, equal publication. A route that must NOT publish
/// (open, a comment-only edit, close) is drained for a quiet window long enough for the single-file
/// route's debounce, so a late wrong publication is seen.</para>
/// <para>The <c>clean</c> state is the positive control on every route: <c>∅</c> on every file.
/// The index cells also pin the oracle itself against the anchors the build prints.</para>
/// </remarks>
public sealed class ProjectDiagnosticRoutesTests
{
    private const string PublishDiagnostics = "textDocument/publishDiagnostics";

    // Long enough for the single-file route's 300 ms debounce plus an analysis on a loaded runner,
    // so a single-file publication the project route should have suppressed lands inside it.
    private static readonly TimeSpan QuietWindow = TimeSpan.FromSeconds(2);

    private const string Lib =
        "class Widget:\n"
        + "    label: str\n"
        + "\n"
        + "    def __init__(self, label: str) -> None:\n"
        + "        self.label = label\n"
        + "\n"
        + "\n"
        + "def build(label: str) -> Widget:\n"
        + "    return Widget(label)\n";

    private const string LibDuplicateClass = Lib + "\n\nclass Widget:\n    pass\n";

    private const string LibWithoutBuild =
        "class Widget:\n    label: str\n\n    def __init__(self, label: str) -> None:\n        self.label = label\n";

    private const string Main =
        "from lib import build\n\n\ndef main() -> None:\n    print(build(\"x\").label)\n";

    // didChange structural: two edits back to back. The first adds a type error; the second
    // replaces it with a clean function. Only the second's set is the build's.
    private const string MainEditWithTypeError = Main + "\n\ndef extra() -> int:\n    return \"s\"\n";
    private const string MainEditClean = Main + "\n\ndef extra() -> int:\n    return 2\n";

    private const string MainCommentOnly = Main + "\n# a comment\n";

    // A file outside the source set that imports a project file (mixed workspace).
    private const string Other = Main;

    private readonly ITestOutputHelper _output;

    public ProjectDiagnosticRoutesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<string, string> Cells()
    {
        var data = new TheoryData<string, string>();
        foreach (var route in new[]
        {
            "index", "open-during-indexing", "didOpen", "didChange-structural", "didChange-nochange",
            "didClose", "watched-sibling", "spyproj-reload", "config-features", "config-hints-only",
            "didOpen-non-project-importer", "didClose-after-unsaved-edit", "edit-then-immediate-close",
        })
        {
            foreach (var state in new[] { "clean", "missing", "unlisted", "dupclass" })
                data.Add(route, state);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public async Task LastPublicationPerFile_EqualsTheBuildsPerFileDiagnostics(string route, string state)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var dir = IOPath.Combine(IOPath.GetTempPath(), $"sharpy_routes_e2e_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        await using var client = LspTestClient.Start(_output);
        var session = new Session(client, dir);
        try
        {
            WriteState(dir, state);
            var buffers = new Dictionary<string, string>(StringComparer.Ordinal);
            var features = Array.Empty<string>();
            var mustPublish = true;
            var mainUri = session.UriOf("main.spy");

            await client.InitializeAsync(new Uri(dir).AbsoluteUri);

            if (route == "open-during-indexing")
            {
                // Opened before the index can have finished: a single-file publication may come
                // first; the project's must come last.
                await client.DidOpenAsync(mainUri, Main);
                buffers["main.spy"] = Main;
            }
            else
            {
                await session.ConvergeAsync(Oracle(dir, buffers, features), mustPublishFrom: 0, $"{route}/{state} index");
            }

            var mark = session.Count;
            switch (route)
            {
                case "index":
                case "open-during-indexing":
                    mark = 0;
                    break;

                case "didOpen":
                    await client.DidOpenAsync(mainUri, Main);
                    buffers["main.spy"] = Main;
                    mustPublish = false;
                    break;

                case "didChange-structural":
                    await client.DidOpenAsync(mainUri, Main);
                    await session.DrainQuietAsync();
                    mark = session.Count;
                    await client.DidChangeAsync(mainUri, MainEditWithTypeError, 2);
                    await client.DidChangeAsync(mainUri, MainEditClean, 3);
                    buffers["main.spy"] = MainEditClean;
                    break;

                case "didChange-nochange":
                    await client.DidOpenAsync(mainUri, Main);
                    await session.DrainQuietAsync();
                    await client.DidChangeAsync(mainUri, MainCommentOnly, 2);
                    buffers["main.spy"] = MainCommentOnly;
                    mustPublish = false;
                    break;

                case "didClose":
                    await client.DidOpenAsync(mainUri, Main);
                    await session.DrainQuietAsync();
                    await client.SendNotificationAsync("textDocument/didClose", new JsonObject
                    {
                        ["textDocument"] = new JsonObject { ["uri"] = mainUri },
                    });
                    mustPublish = false;
                    break;

                case "didClose-after-unsaved-edit":
                    {
                        // The buffer gains a type error (never saved) and is published; the close
                        // discards the buffer, so the last publication must be the DISK text's build.
                        await client.DidOpenAsync(mainUri, Main);
                        await session.DrainQuietAsync();
                        var editMark = session.Count;
                        await client.DidChangeAsync(mainUri, MainEditWithTypeError, 2);
                        var bufferBuild = Oracle(dir, new Dictionary<string, string> { ["main.spy"] = MainEditWithTypeError }, features);
                        if (state == "clean")
                        {
                            // The control that the buffer's set differs from the disk's and was shown
                            // (an import error stops the build before type checking in other states).
                            bufferBuild["main.spy"].Should().Contain("SPY0260");
                        }

                        await session.ConvergeAsync(bufferBuild, editMark, $"{route}/{state} unsaved edit");
                        mark = session.Count;
                        await client.SendNotificationAsync("textDocument/didClose", new JsonObject
                        {
                            ["textDocument"] = new JsonObject { ["uri"] = mainUri },
                        });
                        break;
                    }

                case "edit-then-immediate-close":
                    // The close lands while the edit's reanalysis may still run: no publication of
                    // the discarded buffer may be the last one.
                    await client.DidOpenAsync(mainUri, Main);
                    await session.DrainQuietAsync();
                    mark = session.Count;
                    await client.DidChangeAsync(mainUri, MainEditWithTypeError, 2);
                    await client.SendNotificationAsync("textDocument/didClose", new JsonObject
                    {
                        ["textDocument"] = new JsonObject { ["uri"] = mainUri },
                    });
                    break;

                case "watched-sibling":
                    {
                        // lib.spy is never open; it changes on disk and the client's watcher reports it.
                        var (text, created) = state switch
                        {
                            "clean" => (LibDuplicateClass, false),
                            "missing" => (Lib, true),
                            "unlisted" => (LibWithoutBuild, false),
                            _ => (Lib, false),
                        };
                        File.WriteAllText(IOPath.Combine(dir, "lib.spy"), text);
                        await client.SendNotificationAsync("workspace/didChangeWatchedFiles", WatchedFile(session.UriOf("lib.spy"), created ? 1 : 2));
                        break;
                    }

                case "spyproj-reload":
                    switch (state)
                    {
                        case "clean":
                        case "dupclass":
                            WriteProject(dir, "main.spy");
                            break;
                        case "missing":
                            File.WriteAllText(IOPath.Combine(dir, "lib.spy"), Lib);
                            WriteProject(dir, "main.spy", "lib.spy");
                            break;
                        default:
                            WriteProject(dir, "main.spy", "lib.spy");
                            break;
                    }

                    await client.SendNotificationAsync("workspace/didChangeWatchedFiles", WatchedFile(session.UriOf("test.spyproj"), 2));
                    break;

                case "config-features":
                    features = new[] { "defer" };
                    await client.SendNotificationAsync("workspace/didChangeConfiguration", new JsonObject
                    {
                        ["settings"] = new JsonObject { ["sharpy"] = new JsonObject { ["features"] = new JsonArray("defer") } },
                    });
                    break;

                case "config-hints-only":
                    // No feature changes, so nothing is reanalyzed; the project's results are
                    // republished so the hint filter applies.
                    await client.SendNotificationAsync("workspace/didChangeConfiguration", new JsonObject
                    {
                        ["settings"] = new JsonObject
                        {
                            ["sharpy"] = new JsonObject { ["transitionHints"] = new JsonObject { ["enabled"] = false } },
                        },
                    });
                    break;

                case "didOpen-non-project-importer":
                    // other.spy is outside the source set and imports lib.spy: the single-file
                    // route publishes other.spy, and must not touch the project's lib.spy.
                    File.WriteAllText(IOPath.Combine(dir, "other.spy"), Other);
                    await client.DidOpenAsync(session.UriOf("other.spy"), Other);
                    buffers["other.spy"] = Other;
                    mustPublish = false;
                    session.RequireNotificationFor("other.spy");
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(route), route, null);
            }

            if (route == "didClose")
                buffers.Remove("main.spy");

            var expected = Oracle(dir, buffers, features);
            if (route == "index")
                AssertAnchors(state, expected);

            await session.ConvergeAsync(expected, mustPublish ? mark : null, $"{route}/{state}");
            await session.DrainQuietAsync();
            session.AssertLastPerFile(expected, $"{route}/{state}");
        }
        finally
        {
            try
            {
                await client.ShutdownAsync();
            }
            catch (Exception)
            {
                // The server may already have exited.
            }

            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup.
            }

            _output.WriteLine($"cell {route}/{state}: {stopwatch.Elapsed.TotalSeconds:F1}s");
        }
    }

    /// <summary>The anchors the build prints for each state's base program — so the oracle is not
    /// just whatever the compiler happens to say (measured with `sharpyc project` @ 7f06491a0).</summary>
    private static void AssertAnchors(string state, IReadOnlyDictionary<string, string[]> oracle)
    {
        string[] Of(string file) => oracle.TryGetValue(file, out var codes) ? codes : Array.Empty<string>();
        switch (state)
        {
            case "clean":
                oracle.Values.Should().AllSatisfy(codes => codes.Should().BeEmpty());
                break;
            case "missing":
                Of("main.spy").Should().Equal("SPY0300");
                break;
            case "unlisted":
                Of("main.spy").Should().Equal("SPY0313");
                Of("lib.spy").Should().BeEmpty();
                break;
            case "dupclass":
                Of("lib.spy").Should().Equal("SPY0204");
                Of("main.spy").Should().BeEmpty();
                break;
        }
    }

    private static void WriteState(string dir, string state)
    {
        File.WriteAllText(IOPath.Combine(dir, "main.spy"), Main);
        switch (state)
        {
            case "clean":
                File.WriteAllText(IOPath.Combine(dir, "lib.spy"), Lib);
                WriteProject(dir, "main.spy", "lib.spy");
                break;
            case "missing":
                WriteProject(dir, "main.spy");
                break;
            case "unlisted":
                File.WriteAllText(IOPath.Combine(dir, "lib.spy"), Lib);
                WriteProject(dir, "main.spy");
                break;
            case "dupclass":
                File.WriteAllText(IOPath.Combine(dir, "lib.spy"), LibDuplicateClass);
                WriteProject(dir, "main.spy", "lib.spy");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    private static void WriteProject(string dir, params string[] listed)
    {
        var items = string.Concat(listed.Select(f => $"        <SpyFile Include=\"{f}\" />\n"));
        File.WriteAllText(
            IOPath.Combine(dir, "test.spyproj"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Project>\n    <PropertyGroup>\n"
            + "        <RootNamespace>Test</RootNamespace>\n        <OutputType>exe</OutputType>\n"
            + $"    </PropertyGroup>\n    <ItemGroup>\n{items}    </ItemGroup>\n</Project>\n");
    }

    private static JsonObject WatchedFile(string uri, int type) => new()
    {
        ["changes"] = new JsonArray(new JsonObject { ["uri"] = uri, ["type"] = type }),
    };

    /// <summary>
    /// The build's diagnostics per file, by file name: every <c>.spy</c> in the directory, project
    /// file or not. Open buffers overlay the disk text, as the editor's analysis does; the options
    /// are the editor's (<c>CompilerOptionsFactory.ForLsp</c> with the configured features).
    /// </summary>
    private static Dictionary<string, string[]> Oracle(
        string dir, IReadOnlyDictionary<string, string> buffers, IReadOnlyList<string> features)
    {
        var config = ProjectFileParser.Load(ProjectFileParser.FindProjectFile(dir)!);
        var overlay = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in config.SourceFiles)
        {
            if (buffers.TryGetValue(IOPath.GetFileName(source), out var text))
                overlay[source] = text;
        }

        if (overlay.Count > 0)
            config = config.WithInMemorySources(overlay);

        var options = CompilerOptionsFactory.ForLsp(config, FeatureFlags.None.Enable(features));
        var result = new CompilerApi().AnalyzeProject(config, options);

        var projectFiles = config.SourceFiles.Select(PathNormalizer.Normalize).ToHashSet(StringComparer.Ordinal);
        var oracle = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.GetFiles(dir, "*.spy"))
        {
            var key = PathNormalizer.Normalize(path);
            if (!projectFiles.Contains(key) && buffers.TryGetValue(IOPath.GetFileName(path), out var openText))
            {
                // An open file outside the source set is the single-file route's: its own analysis,
                // its own share of it (an imported project file's share is the project's).
                var single = new CompilerApi().AnalyzeDocument(openText, path, options, null, CancellationToken.None);
                oracle[IOPath.GetFileName(path)] = Codes(single.Diagnostics
                    .Where(d => string.IsNullOrEmpty(d.FilePath) || PathNormalizer.Normalize(d.FilePath) == key)
                    .Select(d => d.Code));
                continue;
            }

            var diagnostics = result.GetFileResult(path)?.Diagnostics
                ?? result.ProjectModel.DiagnosticsForFile(path, result.Diagnostics);
            oracle[IOPath.GetFileName(path)] = Codes(diagnostics.Select(d => d.Code));
        }

        return oracle;
    }

    private static string[] Codes(IEnumerable<string?> codes)
        => codes.Select(c => c ?? "?").OrderBy(c => c, StringComparer.Ordinal).ToArray();

    /// <summary>Every publishDiagnostics notification of one server session, in arrival order.</summary>
    private sealed class Session
    {
        private readonly LspTestClient _client;
        private readonly string _dir;
        private readonly System.Collections.Generic.List<(string File, string[] Codes)> _received = new();
        private readonly HashSet<string> _required = new(StringComparer.OrdinalIgnoreCase);

        public Session(LspTestClient client, string dir)
        {
            _client = client;
            _dir = dir;
        }

        public int Count => _received.Count;

        public string UriOf(string fileName) => new Uri(IOPath.Combine(_dir, fileName)).AbsoluteUri;

        public void RequireNotificationFor(string fileName) => _required.Add(fileName);

        /// <summary>
        /// Reads notifications until the last one per file equals <paramref name="expected"/>
        /// and — when <paramref name="mustPublishFrom"/> is set — every project file has been
        /// published at or after that index; fails with the observed state on the arrival timeout.
        /// </summary>
        public async Task ConvergeAsync(IReadOnlyDictionary<string, string[]> expected, int? mustPublishFrom, string cell)
        {
            var projectFiles = ProjectFileNames();
            var deadline = DateTime.UtcNow + LspTestClient.ArrivalTimeout;
            while (!Converged(expected, projectFiles, mustPublishFrom))
            {
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero)
                    break;

                try
                {
                    Record(await _client.WaitForNotificationAsync(PublishDiagnostics, left));
                }
                catch (TimeoutException)
                {
                    break;
                }
            }

            Converged(expected, projectFiles, mustPublishFrom).Should().BeTrue(
                $"{cell}: expected {Describe(expected)}; every project file published since #{mustPublishFrom?.ToString() ?? "-"}; received {DescribeReceived()}");
        }

        /// <summary>Reads notifications until none arrives for <see cref="QuietWindow"/>.</summary>
        public async Task DrainQuietAsync()
        {
            foreach (var notification in await DrainHelpers.DrainUntilQuietAsync(
                quiet => _client.WaitForNotificationAsync(PublishDiagnostics, quiet), QuietWindow))
            {
                Record(notification);
            }
        }

        public void AssertLastPerFile(IReadOnlyDictionary<string, string[]> expected, string cell)
        {
            var last = LastPerFile();
            foreach (var (file, codes) in expected)
            {
                if (last.TryGetValue(file, out var shown))
                    shown.Should().Equal(codes, $"{cell}: the editor's last publication for {file} is the build's set; received {DescribeReceived()}");
                else
                    codes.Should().BeEmpty($"{cell}: {file} was never published, so the build must report nothing on it");
            }

            foreach (var file in ProjectFileNames().Concat(_required))
                last.Should().ContainKey(file, $"{cell}: {file} must have been published");

            foreach (var file in last.Keys)
                expected.Should().ContainKey(file, $"{cell}: a publication for a file that does not exist");
        }

        private bool Converged(IReadOnlyDictionary<string, string[]> expected, IReadOnlyCollection<string> projectFiles, int? mustPublishFrom)
        {
            if (mustPublishFrom is { } from)
            {
                var since = _received.Skip(from).Select(r => r.File).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!projectFiles.All(since.Contains))
                    return false;
            }

            var last = LastPerFile();
            foreach (var file in _required)
            {
                if (!last.ContainsKey(file))
                    return false;
            }

            foreach (var (file, codes) in expected)
            {
                if (last.TryGetValue(file, out var shown) ? !shown.SequenceEqual(codes) : codes.Length > 0)
                    return false;
            }

            return true;
        }

        private Dictionary<string, string[]> LastPerFile()
        {
            var last = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (file, codes) in _received)
                last[file] = codes;
            return last;
        }

        private IReadOnlyCollection<string> ProjectFileNames()
            => ProjectFileParser.Load(ProjectFileParser.FindProjectFile(_dir)!).SourceFiles
                .Select(IOPath.GetFileName)
                .ToList()!;

        private void Record(JsonNode notification)
        {
            var file = IOPath.GetFileName(new Uri(notification["uri"]!.GetValue<string>()).LocalPath);
            var codes = Codes(notification["diagnostics"]!.AsArray().Select(d => d?["code"]?.ToString()));
            _received.Add((file, codes));
        }

        private string DescribeReceived()
            => _received.Count == 0
                ? "(nothing)"
                : string.Join(" → ", _received.Select(r => $"{r.File}=[{string.Join(",", r.Codes)}]"));

        private static string Describe(IReadOnlyDictionary<string, string[]> expected)
            => string.Join("; ", expected.Select(e => $"{e.Key}=[{string.Join(",", e.Value)}]"));
    }
}
