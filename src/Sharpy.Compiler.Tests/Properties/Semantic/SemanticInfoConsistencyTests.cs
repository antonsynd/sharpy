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
[Collection("HeavyCompilation")]
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

        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)));

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "consistency_test.spy");

            if (!result.Success || result.SemanticInfo == null || result.Module == null)
                return;

            var unresolved = new List<string>();
            WalkExpressions(result.Module, expr =>
            {
                var type = result.SemanticInfo.GetExpressionType(expr);
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

        _output.WriteLine($"SemanticInfo consistency: {errors.Count} programs with unresolved types");
        Assert.Empty(errors);
    }

    [Fact]
    public void AllVariableReferences_ResolveToSymbols()
    {
        var errors = new List<string>();

        var gen = SemanticFilter.WellTypedProgram(
            Gen.OneOfConst("int", "str", "bool").SelectMany(type =>
                GenTyped.TypedProgram(TypeEnv.Default, type, fuel: 2)));

        gen.Sample(module =>
        {
            var source = Sharpy.Compiler.Pretty.Unparser.Unparse(module);
            var compiler = new Sharpy.Compiler.Compiler();
            var result = compiler.Analyze(source, "consistency_test.spy");

            if (!result.Success || result.SemanticInfo == null || result.Module == null)
                return;

            var unresolved = new List<string>();
            WalkExpressions(result.Module, expr =>
            {
                if (expr is Identifier { Name: var name } id
                    && name != "print" && name != "len" && name != "abs"
                    && name != "range" && name != "None"
                    && name != "True" && name != "False")
                {
                    var symbol = result.SemanticInfo.GetIdentifierSymbol(id);
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

        _output.WriteLine($"Symbol resolution consistency: {errors.Count} programs with unresolved references");
        Assert.Empty(errors);
    }

    private static void WalkExpressions(Node node, Action<Expression> visitor)
    {
        if (node is Expression expr)
            visitor(expr);

        foreach (var child in node.GetChildNodes())
            WalkExpressions(child, visitor);
    }
}
