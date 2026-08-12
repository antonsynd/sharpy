using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Pins the scope-walk rule that <see cref="Scope.Update"/> replaces the binding being updated and
/// not a different declaration that merely shares its name (#1393).
///
/// <para>
/// A function's return type is resolved with the function's OWN scope pushed and its parameters
/// already registered, so for <c>def month(month: int)</c> the nearest <c>month</c> is the
/// PARAMETER. The name-keyed walk overwrote it with the function symbol, and every later read of
/// <c>month</c> in the body typed as <c>(int) -&gt; None</c> — a correct program refused with
/// SPY0220/SPY0222. The fixtures cover the user-visible face; this covers the mechanism, so a
/// future refactor of the walk cannot quietly restore the overwrite.
/// </para>
/// </summary>
public class ScopeUpdateShadowingTests
{
    private static FunctionSymbol Function(string name) => new()
    {
        Name = name,
        Kind = SymbolKind.Function,
        DeclarationLine = 1,
        DeclarationColumn = 1,
    };

    private static VariableSymbol Parameter(string name) => new()
    {
        Name = name,
        Kind = SymbolKind.Parameter,
        DeclarationLine = 1,
        DeclarationColumn = 1,
    };

    /// <summary>The defect: the update must skip the parameter and reach the function's own scope.</summary>
    [Fact]
    public void Update_ParameterShadowsItsOwnFunctionName_LeavesTheParameterBound()
    {
        var module = new Scope("module");
        var declared = Function("month");
        module.Define(declared);

        var body = new Scope("month", module);
        var parameter = Parameter("month");
        body.Define(parameter);

        var updated = declared with { ReturnType = SemanticType.Unknown };
        body.Update(declared, updated).Should().BeTrue("the function's own binding is reachable in the parent scope");

        body.Lookup("month", searchParent: false)
            .Should().BeSameAs(parameter, "a parameter must not be displaced by its own function's symbol");
        module.Lookup("month").Should().BeSameAs(updated, "the function's binding is what gets updated");
    }

    /// <summary>
    /// The ordinary case is unchanged: with no shadowing parameter, the nearest binding of the same
    /// kind is replaced, which is how resolved return types reach later references.
    /// </summary>
    [Fact]
    public void Update_NoShadowing_ReplacesTheFunctionBinding()
    {
        var module = new Scope("module");
        var declared = Function("helper");
        module.Define(declared);

        var body = new Scope("helper", module);
        body.Define(Parameter("value"));

        var updated = declared with { ReturnType = SemanticType.Unknown };
        body.Update(declared, updated).Should().BeTrue();
        module.Lookup("helper").Should().BeSameAs(updated);
    }

    /// <summary>
    /// The same overwrite one scope further out, and the reason matching on name plus
    /// <see cref="SymbolKind"/> is not enough: a method's symbol lives on <c>TypeSymbol.Methods</c>
    /// and is in no scope, so a kind match would send <c>class C: def month(self, month: int)</c>
    /// past its own parameter into the module scope and replace a module-level <c>def month</c>
    /// with the method. Measured — it turned <c>month(3)</c> into SPY0224 "expects 2 arguments".
    /// </summary>
    [Fact]
    public void Update_SymbolInNoScope_DoesNotDisplaceASameNamedFunction()
    {
        var module = new Scope("module");
        var moduleFunction = Function("month");
        module.Define(moduleFunction);

        var methodBody = new Scope("month", module);
        methodBody.Define(Parameter("month"));

        // A method symbol: same name, same kind, but a distinct instance bound nowhere in the chain.
        var method = Function("month");
        methodBody.Update(method, method with { ReturnType = SemanticType.Unknown })
            .Should().BeFalse("a symbol that is in no scope has no binding here to update");

        module.Lookup("month").Should().BeSameAs(moduleFunction);
    }

    /// <summary>Nothing to update anywhere in the chain reports failure rather than inventing a binding.</summary>
    [Fact]
    public void Update_NameAbsentFromChain_ReturnsFalse()
    {
        var module = new Scope("module");
        var body = new Scope("f", module);
        body.Define(Parameter("month"));

        var orphan = Function("month");
        body.Update(orphan, orphan with { ReturnType = SemanticType.Unknown }).Should().BeFalse();
        body.Lookup("month", searchParent: false).Should().BeOfType<VariableSymbol>();
    }
}
