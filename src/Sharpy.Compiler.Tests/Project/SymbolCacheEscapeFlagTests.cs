using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// The symbol cache carries <see cref="Symbol.IsNameBacktickEscaped"/> (schema v23, #1275/#1328).
///
/// <para>The flag is part of what a name DENOTES, not a formatting detail: an escape-declared
/// <c>class `int`</c> and a bare <c>class Foo</c> live in different namespaces, and every binding
/// seam decides between them by comparing the flag on the reference against the flag on the symbol
/// (#1325's identity rule). A cache that restores the flag as <c>false</c> therefore hands the warm
/// build a DIFFERENT symbol from the one the cold build declared — an escaped reference no longer
/// binds it (SPY0202), and where it does resolve the emitted spelling is the mangled one (CS0103).
/// Nothing about that failure looks like a caching bug at the point it surfaces, which is why it is
/// pinned here rather than left to the end-to-end suites.</para>
/// </summary>
public class SymbolCacheEscapeFlagTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public SymbolCacheEscapeFlagTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_escape_cache_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>Every symbol kind the serializer round-trips, escaped and bare.</summary>
    public static TheoryData<string, Symbol> EscapedSymbols()
    {
        var data = new TheoryData<string, Symbol>
        {
            { "Type", new TypeSymbol { Name = "int", Kind = SymbolKind.Type, IsNameBacktickEscaped = true } },
            { "Function", new FunctionSymbol { Name = "len", Kind = SymbolKind.Function, IsNameBacktickEscaped = true } },
            { "Variable", new VariableSymbol { Name = "str", Kind = SymbolKind.Variable, IsNameBacktickEscaped = true } },
            { "Module", new ModuleSymbol { Name = "list", Kind = SymbolKind.Module, FilePath = "lib.spy", IsNameBacktickEscaped = true } },
            { "TypeAlias", new TypeAliasSymbol { Name = "dict", Kind = SymbolKind.TypeAlias, IsNameBacktickEscaped = true } },
            { "TypeParameter", new TypeParameterSymbol { Name = "bool", Kind = SymbolKind.TypeParameter, IsNameBacktickEscaped = true } },
        };
        return data;
    }

    [Theory]
    [MemberData(nameof(EscapedSymbols))]
    public void SerializedSymbol_KeepsEscapeFlag_ThroughJsonRoundTrip(string kind, Symbol symbol)
    {
        var cached = SymbolSerializer.Serialize(symbol, "lib.spy");
        cached.IsNameBacktickEscaped.Should().BeTrue($"{kind} symbols carry the escape into the cache");

        // Through JSON, not just through the record: the cache is a file, and a property the
        // serializer sets but the DTO does not persist would pass an in-memory assertion.
        var json = JsonSerializer.Serialize(cached);
        var reloaded = JsonSerializer.Deserialize<CachedSymbol>(json)!;

        var restored = SymbolSerializer.Deserialize(reloaded, new Dictionary<string, Symbol>());
        restored.IsNameBacktickEscaped.Should().BeTrue(
            $"a restored {kind} symbol must denote the same name the cold build declared");
        restored.Name.Should().Be(symbol.Name);
    }

    [Fact]
    public void SerializedSymbol_BareDeclaration_StaysBare()
    {
        // The negative half: the flag must round-trip as a VALUE, not be forced on. Without this a
        // "restore everything as escaped" fix would satisfy the positive test above.
        var bare = new TypeSymbol { Name = "Widget", Kind = SymbolKind.Type };

        var restored = SymbolSerializer.Deserialize(
            JsonSerializer.Deserialize<CachedSymbol>(
                JsonSerializer.Serialize(SymbolSerializer.Serialize(bare, "lib.spy")))!,
            new Dictionary<string, Symbol>());

        restored.IsNameBacktickEscaped.Should().BeFalse();
    }

    [Fact]
    public void WarmBuild_EscapeDeclaredType_RestoredFromCache_StillBinds()
    {
        // Two files so the warm build genuinely exercises restore-then-bind: `main.spy` changes
        // between builds and is recompiled, while `lib.spy` is unchanged and comes back from the
        // symbol cache. A restored `class \`int\`` that lost its flag is no longer the symbol
        // main's escaped reference names, so this build fails with SPY0202 — which is exactly the
        // shape #1328 reported.
        var lib = WriteFile("lib.spy",
            "class `int`:\n"
            + "    value: str\n"
            + "\n"
            + "    def __init__(self, value: str):\n"
            + "        self.value = value\n");
        var main = WriteFile("main.spy",
            "from lib import `int`\n"
            + "\n"
            + "\n"
            + "def main() -> None:\n"
            + "    box: `int` = `int`(\"escaped\")\n"
            + "    print(box.value)\n");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { lib, main },
            Configuration = "Debug"
        };

        var compiler = new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance);

        var cold = compiler.CompileProject(config);
        cold.Success.Should().BeTrue(
            "cold build: " + string.Join("; ", cold.Diagnostics.GetErrors().Select(e => e.Message)));

        var symbolCache = Path.Combine(_tempDir, "obj", "Debug", ".sharpy-symbols");
        File.Exists(symbolCache).Should().BeTrue("the warm build must have a symbol cache to restore from");

        // Change main only. lib is now the cache-restored half.
        File.WriteAllText(main,
            "from lib import `int`\n"
            + "\n"
            + "\n"
            + "def main() -> None:\n"
            + "    box: `int` = `int`(\"escaped\")\n"
            + "    print(box.value, len(box.value))\n");

        var warm = new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance)
            .CompileProject(config);

        _output.WriteLine("warm diagnostics: "
            + string.Join("; ", warm.Diagnostics.GetErrors().Select(e => $"{e.Code}: {e.Message}")));

        warm.Success.Should().BeTrue(
            "a cache-restored escape-declared type must still be the symbol an escaped reference binds");
    }
}
