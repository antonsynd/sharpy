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
/// The symbol cache carries <see cref="Symbol.NameDeclarationColumnEnd"/> — the RECORDED name
/// extent, not a re-derivation of it (schema v26, #1454).
///
/// <para>Every assertion here reads the raw nullable field rather than
/// <see cref="Symbol.EffectiveNameColumnEnd"/>. The accessor falls back to
/// <c>EffectiveNameColumn + Name.Length + backtick pair</c> for symbols that never had a parsed
/// node, so a cache that dropped the field entirely would still answer a plausible number and an
/// assertion on the accessor would pass vacuously. What the cache has to preserve is the fact that
/// the parser MEASURED the extent; that is what the raw field says and the fallback cannot.</para>
///
/// <para>This is the same cold/warm divergence class as v23's
/// <see cref="Symbol.IsNameBacktickEscaped"/> (see <see cref="SymbolCacheEscapeFlagTests"/>): a
/// dropped fact makes a warm build hand the LSP a differently-shaped symbol from the one the cold
/// build declared, and nothing about that failure looks like a caching bug where it surfaces.</para>
/// </summary>
public class SymbolCacheNameExtentTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _tempDir;

    public SymbolCacheNameExtentTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_extent_cache_{Guid.NewGuid():N}");
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

    /// <summary>Every symbol kind the serializer round-trips, each carrying a recorded extent.</summary>
    public static TheoryData<string, Symbol> SymbolsWithRecordedExtents()
        => new()
        {
            { "Type", new TypeSymbol { Name = "Widget", Kind = SymbolKind.Type, NameDeclarationLine = 1, NameDeclarationColumn = 7, NameDeclarationColumnEnd = 13 } },
            { "Function", new FunctionSymbol { Name = "render", Kind = SymbolKind.Function, NameDeclarationLine = 1, NameDeclarationColumn = 5, NameDeclarationColumnEnd = 11 } },
            { "Variable", new VariableSymbol { Name = "count", Kind = SymbolKind.Variable, NameDeclarationLine = 1, NameDeclarationColumn = 1, NameDeclarationColumnEnd = 6 } },
            { "Module", new ModuleSymbol { Name = "lib", Kind = SymbolKind.Module, FilePath = "lib.spy", NameDeclarationLine = 1, NameDeclarationColumn = 8, NameDeclarationColumnEnd = 11 } },
            { "TypeAlias", new TypeAliasSymbol { Name = "Pair", Kind = SymbolKind.TypeAlias, NameDeclarationLine = 1, NameDeclarationColumn = 6, NameDeclarationColumnEnd = 10 } },
            { "TypeParameter", new TypeParameterSymbol { Name = "T", Kind = SymbolKind.TypeParameter, NameDeclarationLine = 1, NameDeclarationColumn = 12, NameDeclarationColumnEnd = 13 } },
        };

    [Theory]
    [MemberData(nameof(SymbolsWithRecordedExtents))]
    public void SerializedSymbol_KeepsRecordedNameExtent_ThroughJsonRoundTrip(string kind, Symbol symbol)
    {
        var expected = symbol.NameDeclarationColumnEnd;
        expected.Should().NotBeNull($"the {kind} specimen must exercise the field non-vacuously");

        var cached = SymbolSerializer.Serialize(symbol, "lib.spy");

        // Through JSON, not just through the record: the cache is a file, and a property the
        // serializer sets but the DTO does not persist would pass an in-memory assertion.
        var json = JsonSerializer.Serialize(cached);
        var reloaded = JsonSerializer.Deserialize<CachedSymbol>(json)!;

        var restored = SymbolSerializer.Deserialize(reloaded, new Dictionary<string, Symbol>());

        restored.NameDeclarationColumnEnd.Should().Be(expected,
            $"a restored {kind} symbol must carry the extent the cold build MEASURED, not one the "
            + "fallback re-derives");
        restored.NameDeclarationColumn.Should().Be(symbol.NameDeclarationColumn);
    }

    [Fact]
    public void SerializedSymbol_WithNoRecordedExtent_StaysUnrecorded_AndUsesTheFallback()
    {
        // The negative half: the field round-trips as a VALUE. A CLR-imported symbol has no parsed
        // node and therefore no recorded extent — restoring it as "recorded" would be a lie that a
        // "populate the end on restore" fix would tell, and that fix would satisfy the positive
        // theory above.
        var nodeless = new TypeSymbol
        {
            Name = "Widget",
            Kind = SymbolKind.Type,
            DeclarationLine = 1,
            DeclarationColumn = 7,
        };

        var restored = SymbolSerializer.Deserialize(
            JsonSerializer.Deserialize<CachedSymbol>(
                JsonSerializer.Serialize(SymbolSerializer.Serialize(nodeless, "lib.spy")))!,
            new Dictionary<string, Symbol>());

        restored.NameDeclarationColumnEnd.Should().BeNull(
            "a symbol with no parsed node has no measured extent to carry");
        restored.EffectiveNameColumnEnd.Should().Be(7 + "Widget".Length,
            "the accessor's fallback is what node-less symbols answer with");
    }

    [Fact]
    public void EffectiveNameColumnEnd_OnAnEscapedNodelessSymbol_SpansTheBackticks()
    {
        // The fallback is the ONE remaining place that reconstructs an extent from the spelling
        // (plan-80eee2 Design Decision 7). Pinning it here is what lets the rename handler stop
        // carrying its own copy of the arithmetic.
        var escaped = new TypeSymbol
        {
            Name = "int",
            Kind = SymbolKind.Type,
            IsNameBacktickEscaped = true,
            DeclarationLine = 1,
            DeclarationColumn = 7,
        };

        escaped.EffectiveNameColumnEnd.Should().Be(7 + "`int`".Length);
    }

    [Fact]
    public void WarmBuild_RestoredSymbol_CarriesTheColdBuildsRecordedNameExtent()
    {
        // lib.spy is unchanged between builds, so the warm build reaches its symbols through the
        // cache. `Widget` is escape-declared and Gadget is not: the escaped extent is the one whose
        // width differs from its spelling, so it is where a dropped field is visible.
        var lib = WriteFile("lib.spy",
            "class `Widget`:\n"                       // line 1: name at col 7, extent ends at 15
            + "    value: str\n"
            + "\n"
            + "    def __init__(self, value: str):\n"
            + "        self.value = value\n"
            + "\n"
            + "\n"
            + "class Gadget:\n"                       // line 8: name at col 7, extent ends at 13
            + "    tag: int\n"
            + "\n"
            + "    def __init__(self, tag: int):\n"
            + "        self.tag = tag\n");
        var main = WriteFile("main.spy",
            "from lib import `Widget`, Gadget\n"
            + "\n"
            + "\n"
            + "def main() -> None:\n"
            + "    box: `Widget` = `Widget`(\"escaped\")\n"
            + "    print(box.value)\n");

        var config = new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "test.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Test",
            SourceFiles = new List<string> { lib, main },
            Configuration = "Debug"
        };

        var cold = new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance)
            .CompileProject(config);
        cold.Success.Should().BeTrue(
            "cold build: " + string.Join("; ", cold.Diagnostics.GetErrors().Select(e => e.Message)));

        // Change main only, so lib comes back from the cache on the second build.
        File.WriteAllText(main,
            "from lib import `Widget`, Gadget\n"
            + "\n"
            + "\n"
            + "def main() -> None:\n"
            + "    box: `Widget` = `Widget`(\"escaped\")\n"
            + "    print(box.value, len(box.value))\n");

        var warm = new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance)
            .CompileProject(config);
        warm.Success.Should().BeTrue(
            "warm build: " + string.Join("; ", warm.Diagnostics.GetErrors().Select(e => e.Message)));

        var entry = LoadCachedEntry(config, lib);
        foreach (var s in entry.Symbols)
        {
            _output.WriteLine($"cached: {s.Name} kind={s.Kind} nameCol={s.NameDeclarationColumn} end={s.NameDeclarationColumnEnd}");
        }

        AssertRestoredExtent(entry, "Widget", nameColumn: 7, nameColumnEnd: 7 + "`Widget`".Length);
        AssertRestoredExtent(entry, "Gadget", nameColumn: 7, nameColumnEnd: 7 + "Gadget".Length);
    }

    private static void AssertRestoredExtent(
        FileCacheEntry entry, string name, int nameColumn, int nameColumnEnd)
    {
        var cached = entry.Symbols.FirstOrDefault(s => s.Name == name);
        cached.Should().NotBeNull($"the cache must hold an entry for {name}");

        var restored = SymbolSerializer.Deserialize(cached!, new Dictionary<string, Symbol>());

        restored.NameDeclarationColumn.Should().Be(nameColumn);
        restored.NameDeclarationColumnEnd.Should().Be(nameColumnEnd,
            $"the warm build's {name} must carry the extent the parser measured on the cold build; "
            + "a null here means the cache dropped it and every reader silently fell back to the "
            + "spelling-derived length");
    }

    /// <summary>
    /// Reads back what the production serializer wrote to <c>.sharpy-symbols</c>, through the
    /// production loader — a fresh <see cref="IncrementalCompilationCache"/> over the same project,
    /// which is exactly how a warm build reaches the cache.
    /// </summary>
    private static FileCacheEntry LoadCachedEntry(ProjectConfig config, string sourceFile)
    {
        var symbolCachePath = Path.Combine(
            config.ProjectDirectory, "obj", config.Configuration, ".sharpy-symbols");
        File.Exists(symbolCachePath).Should().BeTrue($"No symbol cache was written to {symbolCachePath}.");

        var reloaded = new IncrementalCompilationCache(config, NullLogger.Instance);
        reloaded.LoadAllCaches();

        var entry = reloaded.GetFileCache(sourceFile);
        entry.Should().NotBeNull(
            $"The symbol cache holds no valid entry for {Path.GetFileName(sourceFile)}; "
            + "the file was never cached, or its content changed after the build being asserted.");
        return entry!;
    }
}
