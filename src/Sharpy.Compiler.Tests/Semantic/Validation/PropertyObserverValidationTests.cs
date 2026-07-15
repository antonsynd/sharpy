using System.Linq;
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Validation and type-checking tests for property observers (#416): SPY0490 (invalid target),
/// SPY0491 (duplicate observer), and observer-body typing (self + parameter resolution).
/// </summary>
public class PropertyObserverValidationTests
{
    private (Module module, SemanticContext context, TypeChecker typeChecker) Parse(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();
        Assert.False(parser.Diagnostics.HasErrors,
            "test source should parse cleanly: "
            + string.Join("\n", parser.Diagnostics.GetErrors().Select(d => d.Message)));

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var semanticBinding = new SemanticBinding();
        var nameResolver = new NameResolver(symbolTable, semanticBinding: semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context, typeChecker);
    }

    private static void Validate(Module module, SemanticContext context)
        => new PropertyValidator().Validate(module, context);

    [Fact]
    public void SettableAutoProperty_WithObservers_NoObserverError()
    {
        var code =
            "class Character:\n" +
            "    property health: int = 100\n" +
            "        before_set(new_value):\n" +
            "            assert new_value >= 0\n" +
            "        after_set(old_value):\n" +
            "            print(old_value)\n";
        var (module, context, _) = Parse(code);
        Validate(module, context);

        Assert.DoesNotContain(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.PropertyObserverInvalidTarget
                 || e.Code == DiagnosticCodes.Validation.DuplicatePropertyObserver);
    }

    [Fact]
    public void ReadonlyProperty_WithObservers_ReportsInvalidTarget()
    {
        var code =
            "class C:\n" +
            "    @readonly\n" +
            "    property health: int\n" +
            "        before_set(new_value):\n" +
            "            print(new_value)\n";
        var (module, context, _) = Parse(code);
        Validate(module, context);

        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.PropertyObserverInvalidTarget);
    }

    [Fact]
    public void GetOnlyAutoProperty_WithObservers_ReportsInvalidTarget()
    {
        var code =
            "class C:\n" +
            "    property get health: int\n" +
            "        before_set(new_value):\n" +
            "            print(new_value)\n";
        var (module, context, _) = Parse(code);
        Validate(module, context);

        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.PropertyObserverInvalidTarget);
    }

    [Fact]
    public void InitOnlyAutoProperty_WithObservers_ReportsInvalidTarget()
    {
        var code =
            "class C:\n" +
            "    property init health: int\n" +
            "        before_set(new_value):\n" +
            "            print(new_value)\n";
        var (module, context, _) = Parse(code);
        Validate(module, context);

        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.PropertyObserverInvalidTarget);
    }

    [Fact]
    public void OverrideProperty_WithObservers_ReportsInvalidTarget()
    {
        var code =
            "class C:\n" +
            "    @override\n" +
            "    property health: int\n" +
            "        before_set(new_value):\n" +
            "            print(new_value)\n";
        var (module, context, _) = Parse(code);
        Validate(module, context);

        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.PropertyObserverInvalidTarget);
    }

    [Fact]
    public void InterfaceProperty_WithObservers_ReportsInvalidTarget()
    {
        var code =
            "interface IThing:\n" +
            "    property health: int\n" +
            "        before_set(new_value):\n" +
            "            print(new_value)\n";
        var (module, context, _) = Parse(code);
        Validate(module, context);

        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.PropertyObserverInvalidTarget);
    }

    [Fact]
    public void DuplicateBeforeSet_ReportsDuplicateObserver()
    {
        var code =
            "class C:\n" +
            "    property health: int\n" +
            "        before_set(a):\n" +
            "            print(a)\n" +
            "        before_set(b):\n" +
            "            print(b)\n";
        var (module, context, _) = Parse(code);
        Validate(module, context);

        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.DuplicatePropertyObserver);
    }

    [Fact]
    public void ObserverParameter_IsTypedAsPropertyType()
    {
        // A str-typed annotation initialized from the int-typed observer parameter must be a
        // type error — proving the parameter is bound to the property type (int).
        var code =
            "class C:\n" +
            "    property health: int\n" +
            "        before_set(new_value):\n" +
            "            bad: str = new_value\n";
        var (_, _, typeChecker) = Parse(code);

        Assert.Contains(typeChecker.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Semantic.TypeMismatch);
    }

    [Fact]
    public void ObserverBody_ResolvesSelfAndParameter_NoUndefinedError()
    {
        var code =
            "class C:\n" +
            "    property health: int = 0\n" +
            "        after_set(old_value):\n" +
            "            delta: int = self.health - old_value\n";
        var (_, _, typeChecker) = Parse(code);

        Assert.DoesNotContain(typeChecker.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Semantic.UndefinedVariable);
    }
}
