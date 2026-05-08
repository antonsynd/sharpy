using System.Collections.Concurrent;
using CsCheck;
using Sharpy.Compiler.Parser.Ast;
using Xunit;

namespace Sharpy.Compiler.Tests.Properties.Generators;

[Trait("Category", "Property")]
public class ShrinkerTests
{
    [Fact]
    public void ShrinkableListLiteral_ProducesVariation()
    {
        var ctx = GenContext.Default with { Fuel = 3 };
        var sizes = new ConcurrentBag<int>();

        Shrinkers.ShrinkableListLiteral(ctx).Sample(expr =>
        {
            if (expr is ListLiteral list)
                sizes.Add(list.Elements.Length);
        }, iter: 200);

        Assert.True(sizes.Distinct().Count() > 1,
            $"ShrinkableListLiteral produced only one size: [{string.Join(", ", sizes.Distinct())}]");
    }

    [Fact]
    public void ShrinkableFunctionDef_ProducesVariation()
    {
        var ctx = GenContext.Default with { Fuel = 4 };
        var sizes = new ConcurrentBag<int>();

        Shrinkers.ShrinkableFunctionDef(ctx).Sample(stmt =>
        {
            if (stmt is FunctionDef fn)
                sizes.Add(fn.Body.Length);
        }, iter: 200);

        Assert.True(sizes.Distinct().Count() > 1,
            $"ShrinkableFunctionDef produced only one body size: [{string.Join(", ", sizes.Distinct())}]");
    }

    [Fact]
    public void ShrinkableModule_ProducesVariation()
    {
        var ctx = GenContext.Default with { Fuel = 3 };
        var sizes = new ConcurrentBag<int>();

        Shrinkers.ShrinkableModule(ctx).Sample(module =>
        {
            sizes.Add(module.Body.Length);
        }, iter: 200);

        Assert.True(sizes.Distinct().Count() > 1,
            $"ShrinkableModule produced only one body size: [{string.Join(", ", sizes.Distinct())}]");
    }

    [Fact]
    public void ShrinkableConditional_CanReduceToThenValue()
    {
        var ctx = GenContext.Default with { Fuel = 2 };
        int shrunkCount = 0;

        Shrinkers.ShrinkableConditional(ctx).Sample(expr =>
        {
            if (expr is not ConditionalExpression)
                Interlocked.Increment(ref shrunkCount);
        }, iter: 200);

        Assert.True(shrunkCount > 0,
            "ShrinkableConditional never reduced to ThenValue");
    }
}
