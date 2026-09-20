using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
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
/// generator pipeline (#1042). The guard is BEHAVIORAL (#1887): it reflects every side-table field,
/// fabricates one entry per field on a source <c>SemanticInfo</c>, runs the real
/// <c>MergeFrom</c>, and asserts the entry reached the target. The former source-text scan
/// ("is the field name mentioned inside the MergeFrom body?") passed vacuously — a field mentioned
/// in a comment, or copied into the WRONG dictionary, or with the copy statement present but the
/// key silently dropped by a comparer mismatch, all read as "referenced". A round-trip cannot.
/// </para>
/// </summary>
public class SemanticInfoMergeConformanceTests
{
    /// <summary>
    /// Fields deliberately excluded from the "must be merged" check, each with a justification.
    /// Currently empty: every side-table (including the symbol-keyed <c>_symbolReferences</c>, which
    /// <c>MergeFrom</c> merges with a bag-union) must survive the per-file → project merge. The
    /// count is anchored to 0 so an entry added here is a visible edit.
    /// </summary>
    private static readonly HashSet<string> Allowlist = new(StringComparer.Ordinal)
    {
        // (intentionally empty — add a field here only with a written reason it must NOT merge)
    };

    /// <summary>
    /// The number of side-table fields on SemanticInfo at HEAD. Anchored so the reflective
    /// enumeration below cannot silently go vacuous (a refactor that renamed the collections out of
    /// the filter would drop the count, not pass a zero-field check).
    /// </summary>
    private const int MinimumSideTableFieldCount = 78;

    /// <summary>
    /// #1887: the real property — an entry recorded on one SemanticInfo survives <c>MergeFrom</c>
    /// into another — asserted by round-trip over EVERY reflected side-table field, not by a source
    /// scan. For each <c>ConcurrentDictionary&lt;TKey,TValue&gt;</c> field: fabricate a key (a
    /// concrete AST/symbol instance via <see cref="RuntimeHelpers.GetUninitializedObject"/>, a
    /// concrete subtype when the declared key type is an abstract record), add it to
    /// <c>source</c>, merge into an empty <c>target</c>, and assert the key is present. The
    /// symbol-keyed <c>_symbolReferences</c> (bag-union merge) is round-tripped too, asserting the
    /// bag grew by the fabricated reference.
    /// </summary>
    [Fact]
    public void MergeFrom_PreservesEveryEntry_Behaviorally()
    {
        Allowlist.Should().HaveCount(0, "the not-merged roster is empty — every side-table must merge");

        var fields = typeof(SemanticInfo)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(IsSideTableField)
            .Where(f => !Allowlist.Contains(f.Name))
            .ToList();

        fields.Should().HaveCountGreaterThanOrEqualTo(MinimumSideTableFieldCount,
            "the reflective enumeration must see every side-table (a shrunk count means the filter, "
            + "not the merge, changed — the guard would otherwise pass vacuously)");

        var dropped = new List<string>();
        foreach (var field in fields)
        {
            var key = FabricateEntryAndRoundTrip(field);
            if (!key)
                dropped.Add(field.Name);
        }

        dropped.Should().BeEmpty(
            "every node-keyed SemanticInfo side-table must be copied in MergeFrom or it is silently "
            + "dropped in the per-file → project merge that code generation, the generator pipeline, "
            + "validators, and the LSP read from (Critical Rule 2 / #1042). Fields whose fabricated "
            + "entry did NOT survive the merge:\n" + string.Join("\n", dropped));
    }

    /// <summary>
    /// Populates one side-table field on a fresh source SemanticInfo, merges into a fresh empty
    /// target, and reports whether the fabricated key reached the target's copy of the field.
    /// </summary>
    private static bool FabricateEntryAndRoundTrip(FieldInfo field)
    {
        var source = new SemanticInfo();
        var target = new SemanticInfo();

        var keyType = field.FieldType.GetGenericArguments()[0];
        var valueType = field.FieldType.GetGenericArguments()[1];
        var key = FabricateInstance(keyType);

        // _symbolReferences (Symbol -> ConcurrentBag<SymbolReference>) is the one bag-union merge:
        // fabricate a bag holding one reference and assert the target's bag received it.
        if (field.Name == "_symbolReferences")
        {
            var reference = (SymbolReference)RuntimeHelpers.GetUninitializedObject(typeof(SymbolReference));
            var bag = new ConcurrentBag<SymbolReference> { reference };
            ((IDictionary)field.GetValue(source)!)[key] = bag;

            target.MergeFrom(source);

            var targetDict = (IDictionary)field.GetValue(target)!;
            if (!targetDict.Contains(key))
                return false;
            return ((ConcurrentBag<SymbolReference>)targetDict[key]!).Count == 1;
        }

        var value = FabricateValue(valueType);
        ((IDictionary)field.GetValue(source)!)[key] = value;

        target.MergeFrom(source);

        return ((IDictionary)field.GetValue(target)!).Contains(key);
    }

    /// <summary>A field is a side-table if it is a constructed generic that implements the
    /// non-generic <see cref="IDictionary"/> (every SemanticInfo side-table is a
    /// <c>ConcurrentDictionary</c>).</summary>
    private static bool IsSideTableField(FieldInfo field)
        => field.FieldType.IsGenericType
           && typeof(IDictionary).IsAssignableFrom(field.FieldType);

    /// <summary>
    /// Fabricates a usable dictionary KEY of the declared type: a string probe, a value-type
    /// default, or — for a reference type — an uninitialized instance of the type itself, or of its
    /// first concrete subtype when the declared type is abstract (the four AST base records
    /// <c>Expression</c>/<c>Node</c>/<c>Pattern</c>/<c>Statement</c> and <c>Symbol</c> are abstract,
    /// and <see cref="RuntimeHelpers.GetUninitializedObject"/> throws on an abstract type).
    /// </summary>
    private static object FabricateInstance(Type type)
    {
        if (type == typeof(string))
            return "probe";
        if (type.IsValueType)
            return Activator.CreateInstance(type)!;

        var concrete = type.IsAbstract ? FirstConcreteSubtype(type) : type;
        return RuntimeHelpers.GetUninitializedObject(concrete);
    }

    /// <summary>
    /// A dictionary VALUE only needs to be assignable to the value type for <c>TryAdd</c> to store
    /// it; the round-trip asserts on the key, not the value. A string maps to a probe, a value type
    /// to its default (boxed), a reference type to null.
    /// </summary>
    private static object? FabricateValue(Type type)
    {
        if (type == typeof(string))
            return "probe";
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private static Type FirstConcreteSubtype(Type abstractType)
        => abstractType.Assembly.GetTypes()
            .First(t => !t.IsAbstract && !t.IsInterface && !t.ContainsGenericParameters
                        && abstractType.IsAssignableFrom(t));

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

    /// <summary>
    /// A callable-reference lowering recorded on one SemanticInfo must survive
    /// <see cref="SemanticInfo.MergeFrom"/> into another. Codegen reads the callable-reference
    /// lowering from the merged project-level SemanticInfo; if <c>_callableReferenceLowerings</c>
    /// were absent from <c>MergeFrom</c>, a builtin function used as a value in an imported module
    /// would silently emit a bare method group instead of an eta-expanded lambda (#1638).
    /// </summary>
    [Fact]
    public void MergeFrom_CarriesCallableReferenceLowerings()
    {
        var perFile = new SemanticInfo();
        var reference = new Identifier { Name = "len" };
        perFile.SetCallableReferenceLowering(reference, new CallableReferenceLowering(
            "Sharpy.Builtins.Len",
            new[] { (SemanticType)BuiltinType.Str },
            BuiltinType.Int));

        var project = new SemanticInfo();
        project.MergeFrom(perFile);

        var merged = project.GetCallableReferenceLowering(reference);
        merged.Should().NotBeNull("the callable-reference lowering must survive the per-file → project merge");
        merged!.QualifiedName.Should().Be("Sharpy.Builtins.Len");
        merged.ReturnType.Should().Be(BuiltinType.Int);
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

}
