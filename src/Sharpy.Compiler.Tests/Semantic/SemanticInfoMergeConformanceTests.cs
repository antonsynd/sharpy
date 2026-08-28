using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Helpers;
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
/// impossible to miss. Mirrors the source-scan style of <c>EmitterBannedTokenScanTests</c>.
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
    /// An iterable-argument mark recorded on one SemanticInfo must survive
    /// <see cref="SemanticInfo.MergeFrom"/> into another. Codegen reads the projection from the
    /// merged project-level SemanticInfo; if <c>_iterableProjections</c> were absent from
    /// <c>MergeFrom</c>, a bare dict passed to a builtin iterable position in an imported module would
    /// silently emit no <c>.Keys()</c> projection and mis-iterate as key/value pairs (#1154), and a
    /// tuple there would lose its typed-array bridge (#1198).
    /// </summary>
    [Fact]
    public void MergeFrom_CarriesIterableProjections()
    {
        var perFile = new SemanticInfo();
        var argNode = new Identifier { Name = "d" };
        perFile.SetIterableProjection(argNode,
            new IterableArgumentProjection(IterableProjectionKind.DictKeys, SemanticType.Str));
        var tupleArgNode = new Identifier { Name = "t" };
        perFile.SetIterableProjection(tupleArgNode,
            new IterableArgumentProjection(IterableProjectionKind.TupleToArray, SemanticType.Int, 2));

        var project = new SemanticInfo();
        project.MergeFrom(perFile);

        var merged = project.GetIterableProjection(argNode);
        merged.Should().Be(new IterableArgumentProjection(IterableProjectionKind.DictKeys, SemanticType.Str),
            "the iterable projection must survive the per-file → project merge");
        project.GetIterableProjection(tupleArgNode).Should().Be(
            new IterableArgumentProjection(IterableProjectionKind.TupleToArray, SemanticType.Int, 2),
            "the tuple bridge's element type and arity must survive the merge too");
    }

    /// <summary>
    /// A GenericReference fact recorded on one SemanticInfo must survive
    /// <see cref="SemanticInfo.MergeFrom"/> into another. Codegen reads the generic-reference lowering
    /// from the merged project-level SemanticInfo; if <c>_genericReferences</c> were absent from
    /// <c>MergeFrom</c>, a generic reference (callee[T, ...]) in an imported module would silently lose
    /// its resolved kind/target/type-args and mis-lower (#1143).
    /// </summary>
    [Fact]
    public void MergeFrom_CarriesGenericReferences()
    {
        var perFile = new SemanticInfo();
        var indexNode = new IndexAccess
        {
            Object = new Identifier { Name = "identity" },
            Index = new Identifier { Name = "int" }
        };
        var target = new FunctionSymbol { Name = "identity", Kind = SymbolKind.Function };
        perFile.SetGenericReference(indexNode, new GenericReference
        {
            Kind = GenericReferenceKind.UserFunction,
            TargetSymbol = target,
            TypeArgs = new[] { (SemanticType)BuiltinType.Int },
            SelectedOverload = target,
        });

        var project = new SemanticInfo();
        project.MergeFrom(perFile);

        var merged = project.GetGenericReference(indexNode);
        merged.Should().NotBeNull("the generic-reference fact must survive the per-file → project merge");
        merged!.Kind.Should().Be(GenericReferenceKind.UserFunction);
        merged.TargetSymbol.Should().BeSameAs(target);
        merged.TypeArgs.Should().ContainSingle().Which.Should().Be(BuiltinType.Int);
    }

    /// <summary>
    /// A declaration→symbol binding recorded on one SemanticInfo must survive
    /// <see cref="SemanticInfo.MergeFrom"/> into another. The LSP reads a file's semantic model
    /// through <c>ProjectAnalysisResult.GetFileResult</c>, which falls back to the merged
    /// project-level instance; if <c>_declarationSymbols</c> were absent from <c>MergeFrom</c>,
    /// an unannotated declaration would resolve to no symbol there and silently show no inferred
    /// type (#1222).
    /// </summary>
    [Fact]
    public void MergeFrom_CarriesDeclarationSymbols()
    {
        var perFile = new SemanticInfo();
        var declaration = new VariableDeclaration { Name = "LIMIT", IsConst = true };
        var symbol = new VariableSymbol { Name = "LIMIT", Kind = SymbolKind.Variable, IsConstant = true };
        perFile.SetDeclarationSymbol(declaration, symbol);

        var project = new SemanticInfo();
        project.MergeFrom(perFile);

        project.GetDeclarationSymbol(declaration).Should().BeSameAs(symbol,
            "the declaration→symbol binding must survive the per-file → project merge");
    }

    [Fact]
    public void MergeFrom_CarriesDefinitelyAssignedBareLocals()
    {
        var perFile = new SemanticInfo();
        var decl = new VariableDeclaration { Name = "x", Type = new TypeAnnotation { Name = "int" } };
        perFile.RecordDefinitelyAssignedBareLocal(decl);

        var project = new SemanticInfo();
        project.MergeFrom(perFile);

        project.IsDefinitelyAssignedBareLocal(decl).Should().BeTrue(
            "the definitely-assigned bare-local fact must survive the per-file → project merge");
    }

    /// <summary>
    /// The same property through a real multi-file compile rather than two hand-built instances:
    /// the merged project SemanticInfo must answer for a declaration in a file that is not the
    /// entry point, and for one nothing references.
    /// </summary>
    [Fact]
    public void ProjectAnalysis_MergesDeclarationSymbolsFromEveryFile()
    {
        using var helper = new ProjectCompilationHelper();
        helper.WithRootNamespace("DeclarationSymbolMerge")
            .AddSourceFile("lib.spy", """
                def helper() -> int:
                    const UNREFERENCED = 7
                    total = 41
                    return total
                """)
            .AddSourceFile("main.spy", """
                from lib import helper

                def main() -> None:
                    print(helper())
                """)
            .CreateProjectFile();

        var config = ProjectFileParser.Load(
            Path.Combine(helper.ProjectDirectory, "DeclarationSymbolMerge.spyproj"));
        var analysis = new CompilerApi().AnalyzeProject(config);

        var libPath = helper.SourceFiles.Single(p => Path.GetFileName(p) == "lib.spy");
        var libAst = analysis.GetFileResult(libPath)?.Ast;
        libAst.Should().NotBeNull("the project analysis must have parsed lib.spy");

        var declaration = FindDeclaration(libAst!, "UNREFERENCED");
        declaration.Should().NotBeNull("lib.spy declares an unreferenced const");

        var merged = analysis.ProjectModel.SemanticInfo;
        merged.Should().NotBeNull("project analysis builds a merged SemanticInfo");

        var symbol = merged!.GetDeclarationSymbol(declaration!);
        symbol.Should().BeOfType<VariableSymbol>(
            "the merged project SemanticInfo must carry the binding for a declaration in a "
            + "non-entry file, even one nothing references")
            .Which.Type.Should().Be(BuiltinType.Int);
    }

    private static VariableDeclaration? FindDeclaration(Node root, string name)
    {
        if (root is VariableDeclaration declaration && declaration.Name == name)
            return declaration;

        foreach (var child in root.GetChildNodes())
        {
            var found = FindDeclaration(child, name);
            if (found != null)
                return found;
        }

        return null;
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
