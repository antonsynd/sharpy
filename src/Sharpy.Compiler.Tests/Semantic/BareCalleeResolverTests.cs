using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// plan-777361 D5 (A4, M4): a bare callee resolves by the scope walk a bare read uses and dispatches
/// on the symbol it bound, whatever an outer binding of the name is. Under an OVERLOADED module
/// function <c>x</c>, every inner binding of <c>x</c> — a loop variable, a parameter, a nested
/// <c>def</c> — is the callee: the checker never selects among the module's overloads for it. Before
/// D5 the overload routes read <c>SymbolTable.LookupFunctionOverloads</c>, which walks the chain's
/// overload lists past every inner binding: the zero-argument binders were dispatched to the module's
/// <c>x()</c> and the two-argument ones refused (no module overload takes two arguments).
/// </summary>
public class BareCalleeResolverTests
{
    private const string Source = @"def x() -> int:
    return 12

def x(a: int) -> int:
    return a + 100

def one() -> int:
    return 1

def add(a: int, b: int) -> int:
    return a + b

def via_for() -> None:
    for x in [one]:
        print(x())

def via_parameter(x: () -> int) -> None:
    print(x())

def via_nested_def() -> None:
    def x() -> int:
        return 1
    print(x())

def via_two_argument_parameter(x: (int, int) -> int) -> None:
    print(x(2, 3))

def via_two_argument_nested_def() -> None:
    def x(a: int, b: int) -> int:
        return a * b
    print(x(2, 3))

def main() -> None:
    print(x())
    print(x(5))
";

    private static (Module Module, SemanticInfo Info, DiagnosticBag Diagnostics) Analyze(string source)
    {
        var tokens = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance).TokenizeAll();
        var module = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance).ParseModule();

        var symbolTable = new SymbolTable(new BuiltinRegistry());
        var semanticInfo = new SemanticInfo();
        new NameResolver(symbolTable, NullLogger.Instance).ResolveDeclarations(module);
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module, isEntryPoint: false);
        return (module, semanticInfo, typeChecker.Diagnostics);
    }

    /// <summary>The calls of <c>x</c> inside each top-level function other than <c>x</c>'s own
    /// overloads, by the function's name.</summary>
    private static Dictionary<string, List<FunctionCall>> CallsOfXByFunction(Module module)
        => module.Body.OfType<FunctionDef>().Where(f => f.Name != "x").ToDictionary(
            f => f.Name,
            f => AstDescendants.SelfAndDescendants(f).OfType<FunctionCall>()
                .Where(c => c.Function is Identifier { Name: "x" }).ToList());

    [Fact]
    public void InnerBindings_UnderAnOverloadedModuleFunction_AreTheCallee()
    {
        var (module, info, diagnostics) = Analyze(Source);

        diagnostics.GetErrors().Select(d => $"{d.Code}: {d.Message}").Should().BeEmpty(
            "every call of `x` reaches a binding that accepts its arguments");

        var moduleOverloadLines = module.Body.OfType<FunctionDef>()
            .Where(f => f.Name == "x").Select(f => f.LineStart).ToHashSet();
        moduleOverloadLines.Should().HaveCount(2);

        var calls = CallsOfXByFunction(module);
        foreach (var binder in new[] { "via_for", "via_parameter", "via_nested_def", "via_two_argument_parameter", "via_two_argument_nested_def" })
        {
            calls[binder].Should().ContainSingle($"{binder} calls `x` once");
            var target = info.GetCallTarget(calls[binder][0]);
            if (target != null)
            {
                moduleOverloadLines.Should().NotContain(target.DeclarationLine ?? -1,
                    $"{binder}'s `x` is its own binding, not one of the module's overloads");
            }
        }

        // The positive control: the module's own calls select among its overloads.
        calls["main"].Should().HaveCount(2);
        foreach (var call in calls["main"])
        {
            var target = info.GetCallTarget(call);
            target.Should().NotBeNull("main's `x` is the module's overload set, resolved per call");
            moduleOverloadLines.Should().Contain(target!.DeclarationLine ?? -1);
        }
    }
}
