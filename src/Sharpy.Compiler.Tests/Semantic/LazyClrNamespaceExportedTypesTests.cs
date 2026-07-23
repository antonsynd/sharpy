using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Regression coverage for #1106: lazily-constructed CLR-namespace <see cref="ModuleSymbol"/>s
/// must carry the same types-only <see cref="ModuleSymbol.ExportedTypes"/> lookup that
/// import-resolved modules get (#1092), so a value-position export cannot shadow a same-named
/// type in annotation position (SPY0202).
///
/// A genuine type/field name collision inside a live BCL namespace is not reliably
/// constructible (per the issue's own caveat), so these are behavioral-proxy assertions:
/// they verify <see cref="ModuleSymbol.ExportedTypes"/> is populated (non-empty, contains a
/// known BCL type) at the two lazy construction paths the analyzer actually exercises here —
/// the backtick inline namespace (<c>`System`</c>) and the .NET sub-namespace branch
/// (<c>`System`.Collections</c>) — and that its entries are the very same <see cref="TypeSymbol"/>
/// references stored in <see cref="ModuleSymbol.Exports"/>.
/// </summary>
public class LazyClrNamespaceExportedTypesTests
{
    private readonly ITestOutputHelper _output;

    public LazyClrNamespaceExportedTypesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>Collects every expression node in an analyzed module for type inspection.</summary>
    private sealed class ExpressionCollector : AstVisitor
    {
        public readonly List<Expression> Expressions = new();

        public override void VisitExpression(Expression node)
        {
            Expressions.Add(node);
            DefaultVisit(node);
        }
    }

    /// <summary>
    /// Analyzes a snippet that reaches both lazy CLR-namespace paths — the backtick inline
    /// namespace <c>`System`</c> (<c>TryResolveInlineClrNamespace</c>) and the .NET sub-namespace
    /// branch <c>`System`.Collections</c> (<c>TryResolveClrSubNamespaceOrType</c>) — and returns
    /// the constructed <see cref="ModuleSymbol"/>s keyed by their .NET namespace name.
    /// </summary>
    private Dictionary<string, ModuleSymbol> AnalyzeLazyModules(string source, out CompilationResult result)
    {
        var compiler = new Compiler(new CompilerOptions { OutputType = "library" });
        result = compiler.Analyze(source, "test.spy");
        result.Module.Should().NotBeNull();
        result.SemanticInfo.Should().NotBeNull();

        var collector = new ExpressionCollector();
        collector.Visit(result.Module!);

        var byNamespace = new Dictionary<string, ModuleSymbol>();
        foreach (var expr in collector.Expressions)
        {
            if (result.SemanticInfo!.GetExpressionType(expr) is ModuleType { Symbol: { NetNamespaceName: { } ns } ms })
                byNamespace[ns] = ms;
        }
        return byNamespace;
    }

    [Fact]
    public void BacktickInlineNamespace_ModuleSymbol_CarriesExportedTypes()
    {
        // `System` reaches TryResolveInlineClrNamespace, which builds a ModuleSymbol for the
        // System namespace. Before #1106 its ExportedTypes was left empty.
        var modules = AnalyzeLazyModules("def main():\n    x = `System`.Collections\n    print(x)\n", out _);

        modules.Should().ContainKey("System");
        var system = modules["System"];
        system.IsNetModule.Should().BeTrue();
        system.ExportedTypes.Should().NotBeEmpty("the inline `System` namespace ModuleSymbol must carry types-only exports (#1106)");
        system.ExportedTypes.Should().ContainKey("Array", "System is expected to expose the well-known System.Array type");

        // Exports and ExportedTypes must agree on a sampled entry (same reference), and
        // ExportedTypes must be a strict types-only subset of Exports (it excludes the
        // sub-namespace ModuleSymbol members that Exports also holds).
        system.ExportedTypes["Array"].Should().BeSameAs(system.Exports["Array"]);
        system.ExportedTypes.Count.Should().BeLessThan(system.Exports.Count,
            "ExportedTypes is types-only and must exclude non-type exports such as sub-namespace modules");
    }

    [Fact]
    public void ClrSubNamespaceBranch_ModuleSymbol_CarriesExportedTypes()
    {
        // `System`.Collections reaches the .NET sub-namespace branch of
        // TryResolveClrSubNamespaceOrType, which builds a ModuleSymbol for System.Collections.
        var modules = AnalyzeLazyModules("def main():\n    x = `System`.Collections\n    print(x)\n", out var result);

        result.Diagnostics.GetErrors().Should().NotContain(
            d => d.Code == "SPY0202",
            "annotation-position resolution through the sub-namespace must not degrade to SPY0202");

        modules.Should().ContainKey("System.Collections");
        var collections = modules["System.Collections"];
        collections.IsNetModule.Should().BeTrue();
        collections.ExportedTypes.Should().NotBeEmpty("the System.Collections sub-namespace ModuleSymbol must carry types-only exports (#1106)");
        collections.ExportedTypes.Should().ContainKey("ArrayList", "System.Collections is expected to expose the well-known ArrayList type");

        // Every ExportedTypes entry is the same reference as its Exports counterpart.
        foreach (var (name, type) in collections.ExportedTypes)
            collections.Exports[name].Should().BeSameAs(type,
                $"ExportedTypes['{name}'] must mirror the exact TypeSymbol stored in Exports");
    }
}
