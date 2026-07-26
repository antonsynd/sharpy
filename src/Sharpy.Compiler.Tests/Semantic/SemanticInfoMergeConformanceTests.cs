using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance test enforcing that every node-keyed side-table on <see cref="SemanticInfo"/> is
/// merged in <c>SemanticInfo.MergeFrom</c> (Critical Rule 2 / #1042).
///
/// <para>
/// Type checking runs per-file against a local <c>SemanticInfo</c>, which is then merged into the
/// shared project-level instance the emitter (and the generator sub-pipeline, validators, LSP)
/// read from. A side-table field that is not copied in <c>MergeFrom</c> is silently dropped in that
/// per-file → project merge — the failure mode that hid <c>_generatorBindings</c> from the source
/// generator pipeline (#1042). This test scans <c>SemanticInfo.cs</c> for every dictionary/set
/// field and fails if one is not referenced inside <c>MergeFrom</c>, making that omission
/// impossible to miss. Mirrors the source-scan style of <c>EmitterPurityConformanceTests</c>.
/// </para>
/// </summary>
public class SemanticInfoMergeConformanceTests
{
    /// <summary>
    /// Fields deliberately excluded from the "must be merged" check, each with a justification.
    /// Currently empty: every side-table (including the symbol-keyed <c>_symbolReferences</c>, which
    /// <c>MergeFrom</c> merges with a bag-union) must survive the per-file → project merge.
    /// </summary>
    private static readonly HashSet<string> Allowlist = new(StringComparer.Ordinal)
    {
        // (intentionally empty — add a field here only with a written reason it must NOT merge)
    };

    [Fact]
    public void EverySemanticInfoSideTable_IsReferencedInMergeFrom()
    {
        var source = File.ReadAllText(FindSemanticInfoSource());

        var fields = CollectSideTableFields(source);
        fields.Should().NotBeEmpty("SemanticInfo declares dictionary/set side-tables");

        var mergeFromBody = ExtractMethodBody(source, "public void MergeFrom(SemanticInfo other)");
        mergeFromBody.Should().NotBeNullOrEmpty("MergeFrom(SemanticInfo) should be present");

        var missing = fields
            .Where(f => !Allowlist.Contains(f))
            .Where(f => !Regex.IsMatch(mergeFromBody!, $@"\b{Regex.Escape(f)}\b"))
            .ToList();

        missing.Should().BeEmpty(
            "every node-keyed SemanticInfo side-table must be copied in MergeFrom or it is silently " +
            "dropped in the per-file → project merge that code generation, the generator pipeline, " +
            "validators, and the LSP read from (Critical Rule 2 / #1042). Add the missing field to " +
            "MergeFrom, or add it to the allowlist with a written reason it must not merge.\nMissing:\n" +
            string.Join("\n", missing));
    }

    /// <summary>
    /// Regression for #1042: a generator binding recorded on one SemanticInfo must survive
    /// <see cref="SemanticInfo.MergeFrom"/> into another. Before the fix, <c>_generatorBindings</c>
    /// was absent from <c>MergeFrom</c>, so bindings populated per-file never reached the merged
    /// project SemanticInfo the generator sub-pipeline enumerates — no source generator ran.
    /// </summary>
    [Fact]
    public void MergeFrom_CarriesGeneratorBindings()
    {
        var perFile = new SemanticInfo();
        var declaration = new PassStatement();
        var generatorType = new TypeSymbol { Name = "MyGenerator", Kind = SymbolKind.Type };
        var trigger = new Decorator { QualifiedParts = ImmutableArray.Create("my_generator") };
        perFile.AddGeneratorBinding(declaration, generatorType, trigger);

        var project = new SemanticInfo();
        project.MergeFrom(perFile);

        var merged = project.GetAllGeneratorBindings().ToList();
        merged.Should().ContainSingle("the generator binding must survive the per-file → project merge");
        merged[0].Bindings.Should().ContainSingle();
        merged[0].Bindings[0].GeneratorType.Name.Should().Be("MyGenerator");
    }

    /// <summary>
    /// A narrowed-read lowering recorded on one SemanticInfo must survive
    /// <see cref="SemanticInfo.MergeFrom"/> into another. Codegen reads narrowing accessors from the
    /// merged project-level SemanticInfo; if <c>_narrowedReadLowerings</c> were absent from
    /// <c>MergeFrom</c>, narrowed reads in imported modules would silently emit no accessor (#1081).
    /// </summary>
    [Fact]
    public void MergeFrom_CarriesNarrowedReadLowerings()
    {
        var perFile = new SemanticInfo();
        var readNode = new Identifier { Name = "x" };
        perFile.SetNarrowedReadLowering(readNode, new NarrowedReadLowering(NarrowedReadKind.UnwrapOptional));

        var project = new SemanticInfo();
        project.MergeFrom(perFile);

        var merged = project.GetNarrowedReadLowering(readNode);
        merged.Should().NotBeNull("the narrowed-read lowering must survive the per-file → project merge");
        merged!.Kind.Should().Be(NarrowedReadKind.UnwrapOptional);
    }

    /// <summary>
    /// An iterable-projection marker recorded on one SemanticInfo must survive
    /// <see cref="SemanticInfo.MergeFrom"/> into another. Codegen reads the DictKeys projection from the
    /// merged project-level SemanticInfo; if <c>_iterableProjections</c> were absent from
    /// <c>MergeFrom</c>, a bare dict passed to a builtin iterable position in an imported module would
    /// silently emit no <c>.Keys()</c> projection and mis-iterate as key/value pairs (#1154).
    /// </summary>
    [Fact]
    public void MergeFrom_CarriesIterableProjections()
    {
        var perFile = new SemanticInfo();
        var argNode = new Identifier { Name = "d" };
        perFile.SetIterableProjection(argNode, IterableProjectionKind.DictKeys);

        var project = new SemanticInfo();
        project.MergeFrom(perFile);

        var merged = project.GetIterableProjection(argNode);
        merged.Should().Be(IterableProjectionKind.DictKeys,
            "the iterable projection must survive the per-file → project merge");
    }

    /// <summary>
    /// Collects the names of private dictionary/set side-table fields declared in SemanticInfo.
    /// Matches <c>ConcurrentDictionary</c>/<c>Dictionary</c>/<c>HashSet</c> declarations (the field
    /// identifier follows the closing generic bracket on the same line).
    /// </summary>
    private static List<string> CollectSideTableFields(string source)
    {
        var pattern = new Regex(
            @"private\s+readonly\s+(?:ConcurrentDictionary|Dictionary|HashSet)<.+>\s+(_[A-Za-z0-9_]+)",
            RegexOptions.Compiled);
        return source
            .Split('\n')
            .Select(line => pattern.Match(line))
            .Where(m => m.Success)
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
    }

    /// <summary>Extracts a method body (text between its outermost braces) by brace matching.</summary>
    private static string? ExtractMethodBody(string source, string signature)
    {
        var sigIndex = source.IndexOf(signature, StringComparison.Ordinal);
        if (sigIndex < 0)
            return null;

        var open = source.IndexOf('{', sigIndex);
        if (open < 0)
            return null;

        int depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source.Substring(open + 1, i - open - 1);
            }
        }
        return null;
    }

    private static string FindSemanticInfoSource()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var path = Path.Combine(current, "src", "Sharpy.Compiler", "Semantic", "SemanticInfo.cs");
            if (File.Exists(path))
                return path;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Sharpy.Compiler", "Semantic", "SemanticInfo.cs"));
    }
}
