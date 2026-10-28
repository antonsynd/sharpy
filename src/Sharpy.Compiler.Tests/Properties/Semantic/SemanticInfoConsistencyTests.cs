using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Tests.Properties.Generators.Typed;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.Semantic;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
[Trait("Speed", "Slow")]
[Collection("PropertySerial")]
public class SemanticInfoConsistencyTests
{
    private readonly ITestOutputHelper _output;

    public SemanticInfoConsistencyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AllExpressions_HaveResolvedTypes()
    {
        var errors = new List<string>();

        var sink = new RejectionSink("SemanticInfoConsistency.AllExpressions_HaveResolvedTypes");
        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOf("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)), sink);

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var (semanticInfo, analysed) = AnalyseAdmitted(source);

            var unresolved = new List<string>();
            WalkExpressions(analysed, expr =>
            {
                var type = semanticInfo.GetExpressionType(expr);
                if (type == null || type is UnknownType)
                {
                    unresolved.Add($"{expr.GetType().Name} at line {expr.LineStart}");
                }
            });

            if (unresolved.Count > 0)
            {
                lock (errors)
                    errors.Add(
                        $"Unresolved expressions ({unresolved.Count}):\n" +
                        $"  {string.Join(", ", unresolved.Take(5))}\n" +
                        $"Source:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());

        _output.WriteLine($"SemanticInfo consistency: {errors.Count} programs with unresolved types");
        Assert.Empty(errors);
    }

    [Fact]
    public void AllVariableReferences_ResolveToSymbols()
    {
        var errors = new List<string>();

        var sink = new RejectionSink("SemanticInfoConsistency.AllVariableReferences_ResolveToSymbols");
        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOf("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)), sink);

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var (semanticInfo, analysed) = AnalyseAdmitted(source);

            var unresolved = new List<string>();
            WalkExpressions(analysed, expr =>
            {
                if (expr is Identifier { Name: var name } id
                    && name != "print" && name != "len" && name != "abs"
                    && name != "range" && name != "None"
                    && name != "True" && name != "False")
                {
                    var symbol = semanticInfo.GetIdentifierSymbol(id);
                    if (symbol == null)
                    {
                        unresolved.Add($"'{name}' at line {id.LineStart}");
                    }
                }
            });

            if (unresolved.Count > 0)
            {
                lock (errors)
                    errors.Add(
                        $"Unresolved identifiers ({unresolved.Count}):\n" +
                        $"  {string.Join(", ", unresolved.Take(5))}\n" +
                        $"Source:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 50);
        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());

        _output.WriteLine($"Symbol resolution consistency: {errors.Count} programs with unresolved references");
        Assert.Empty(errors);
    }

    [Fact]
    public void AllExpressions_HaveResolvedTypes_WithCollections()
    {
        var sink = new RejectionSink("SemanticInfoConsistency.AllExpressions_HaveResolvedTypes_WithCollections");
        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOf("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.WithCollections, type, fuel: 2)), sink);
        AssertAllExpressionsResolved(gen, sink, iter: 30);
    }

    [Fact]
    public void AllExpressions_HaveResolvedTypes_WithOptionals()
    {
        var sink = new RejectionSink("SemanticInfoConsistency.AllExpressions_HaveResolvedTypes_WithOptionals");
        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOf("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.WithOptionals, type, fuel: 2)), sink);
        AssertAllExpressionsResolved(gen, sink, iter: 30);
    }

    [Fact]
    public void AllExpressions_HaveResolvedTypes_WithClasses()
    {
        var sink = new RejectionSink("SemanticInfoConsistency.AllExpressions_HaveResolvedTypes_WithClasses");
        var gen = SemanticFilter.WellTypedProgram(
            GenClasses.ModuleWithClasses(TypeEnv.Default, fuel: 2), sink);
        AssertAllExpressionsResolved(gen, sink, iter: 20);
    }

    [Fact]
    public void AllExpressions_HaveResolvedTypes_WithFunctions()
    {
        var sink = new RejectionSink("SemanticInfoConsistency.AllExpressions_HaveResolvedTypes_WithFunctions");
        var gen = SemanticFilter.WellTypedProgram(
            GenFunctions.ModuleWithFunctions(TypeEnv.Default, "int", fuel: 2), sink);
        AssertAllExpressionsResolved(gen, sink, iter: 20);
    }

    [Fact]
    public void ExpressionTypes_AreConsistentWithNodeKind()
    {
        var errors = new List<string>();

        var sink = new RejectionSink("SemanticInfoConsistency.ExpressionTypes_AreConsistentWithNodeKind");
        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOf("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.WithCollections, type, fuel: 2)), sink);

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var (semanticInfo, analysed) = AnalyseAdmitted(source);

            var mismatches = new List<string>();
            WalkExpressions(analysed, expr =>
            {
                var type = semanticInfo.GetExpressionType(expr);
                if (type == null || type is UnknownType)
                    return;

                var mismatch = expr switch
                {
                    IntegerLiteral when type is not BuiltinType { Name: "int32" or "int64" }
                        => $"IntegerLiteral has type {type} (expected int32 or int64)",
                    StringLiteral when type is not BuiltinType { Name: "str" }
                        => $"StringLiteral has type {type} (expected str)",
                    BooleanLiteral when type is not BuiltinType { Name: "bool" }
                        => $"BooleanLiteral has type {type} (expected bool)",
                    NoneLiteral when type is not (VoidType or NullableType or OptionalType)
                        => $"NoneLiteral has type {type} (expected VoidType, NullableType, or OptionalType)",
                    ListLiteral when type is not GenericType { Name: "list" }
                        => $"ListLiteral has type {type} (expected list[T])",
                    _ => null
                };

                if (mismatch != null)
                    mismatches.Add($"{mismatch} at line {expr.LineStart}");
            });

            if (mismatches.Count > 0)
            {
                lock (errors)
                    errors.Add(
                        $"Type/node mismatches ({mismatches.Count}):\n" +
                        $"  {string.Join("\n  ", mismatches.Take(5))}\n" +
                        $"Source:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: 30);
        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());

        _output.WriteLine($"Type consistency: {errors.Count} programs with type/node mismatches");
        Assert.Empty(errors);
    }

    private void AssertAllExpressionsResolved(Gen<Module> gen, RejectionSink sink, int iter)
    {
        var errors = new List<string>();

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var (semanticInfo, analysed) = AnalyseAdmitted(source);

            var unresolved = new List<string>();
            WalkExpressions(analysed, expr =>
            {
                // SuperExpression, NoneLiteral, and the "super"/"self" identifiers
                // are not assigned expression types in SemanticInfo by design
                // (super() is resolved at call site, None's type depends on
                // assignment context, self is implicit).
                if (expr is SuperExpression or NoneLiteral)
                    return;
                // Special names (super, self) and identifiers referencing types
                // (e.g., constructor calls like DerivedEntity(...)) are not
                // assigned expression types in SemanticInfo.
                if (expr is Identifier { Name: "super" or "self" })
                    return;
                if (expr is Identifier ident
                    && semanticInfo.GetIdentifierSymbol(ident) is TypeSymbol)
                    return;

                var type = semanticInfo.GetExpressionType(expr);
                if (type == null || type is UnknownType)
                {
                    unresolved.Add($"{expr.GetType().Name} at line {expr.LineStart}");
                }
            });

            if (unresolved.Count > 0)
            {
                lock (errors)
                    errors.Add(
                        $"Unresolved expressions ({unresolved.Count}):\n" +
                        $"  {string.Join(", ", unresolved.Take(5))}\n" +
                        $"Source:\n{source}");
            }
        }, print: m => Sharpy.Compiler.Pretty.Unparser.Unparse(m), iter: iter);
        _output.WriteLine(sink.Summary);
        Assert.True(sink.Unexplained.Count == 0, sink.Report());

        _output.WriteLine($"SemanticInfo consistency: {errors.Count} programs with unresolved types");
        Assert.Empty(errors);
    }

    /// <summary>Re-analyses a program the filter ADMITTED: a refusal, or a clean result without semantic
    /// info or a module, is a red — before #2288 such a sample returned silently.</summary>
    private static (SemanticInfo SemanticInfo, Module Module) AnalyseAdmitted(string source)
    {
        // A refusal throws with its codes (PropertyAnalysis.AnalyseClean) and fails the sample.
        var result = PropertyAnalysis.AnalyseClean(source, "an admitted program's re-analysis");
        if (result.SemanticInfo == null || result.Module == null)
            throw new Exception($"An admitted program analysed clean without semantic info or a module:\n{source}");
        return (result.SemanticInfo, result.Module);
    }

    private static void WalkExpressions(Node node, Action<Expression> visitor)
    {
        if (node is Expression expr)
            visitor(expr);

        foreach (var child in node.GetChildNodes())
            WalkExpressions(child, visitor);
    }
}
