using System.Linq;
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

public class PropertyValidatorTests
{
    private (Module module, SemanticContext context) Parse(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var semanticBinding = new SemanticBinding();
        var nameResolver = new NameResolver(symbolTable, semanticBinding: semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        // Run type checking to populate type symbols and properties
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    // ===========================
    // SPY0405: Property/Field name conflict
    // ===========================

    [Fact]
    public void PropertyConflictsWithField_ReportsError()
    {
        var code = @"
class Foo:
    name: str

    property get name(self) -> str:
        return ""hello""
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.PropertyFieldNameConflict);
    }

    // ===========================
    // SPY0406: Property/Method name conflict
    // ===========================

    [Fact]
    public void PropertyConflictsWithMethod_ReportsError()
    {
        var code = @"
class Foo:
    property get name(self) -> str:
        return ""hello""

    def name(self) -> str:
        return ""world""
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.PropertyMethodNameConflict);
    }

    // ===========================
    // SPY0407: Mixed auto/function-style
    // ===========================

    [Fact]
    public void MixedAutoAndFunctionStyle_IsAllowed()
    {
        // Mixed auto and function-style properties are now allowed
        // (auto property provides backing field, function-style provides custom accessors)
        var code = @"
class Foo:
    property name: str

    property get name(self) -> str:
        return ""hello""
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        var errors = context.Diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Validation.MixedAutoAndFunctionStyleProperty)
            .ToList();
        Assert.Empty(errors);
    }

    // ===========================
    // SPY0408: Init on function-style
    // ===========================

    [Fact]
    public void InitOnFunctionStyleProperty_ReportsError()
    {
        var code = @"
class Foo:
    _name: str

    def __init__(self):
        self._name = """"

    property init name(self, value: str):
        self._name = value
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.InitOnlyFunctionStyleProperty);
    }

    // ===========================
    // SPY0409: Abstract property must have ellipsis body
    // ===========================

    [Fact]
    public void AbstractPropertyWithBody_ReportsError()
    {
        var code = @"
@abstract
class Foo:
    @abstract
    property get name(self) -> str:
        return ""hello""
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.AbstractPropertyMustHaveEllipsisBody);
    }

    [Fact]
    public void AbstractPropertyWithEllipsis_NoError()
    {
        var code = @"
@abstract
class Foo:
    @abstract
    property get name(self) -> str: ...
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        var abstractErrors = context.Diagnostics.GetErrors()
            .Where(e => e.Code == DiagnosticCodes.Validation.AbstractPropertyMustHaveEllipsisBody)
            .ToList();
        Assert.Empty(abstractErrors);
    }

    // ===========================
    // SPY0410: Final with abstract or virtual
    // ===========================

    [Fact]
    public void FinalWithAbstract_ReportsError()
    {
        var code = @"
@abstract
class Foo:
    @final
    @abstract
    property get name(self) -> str: ...
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.FinalWithAbstractOrVirtual);
    }

    [Fact]
    public void FinalWithVirtual_ReportsError()
    {
        var code = @"
class Foo:
    @final
    @virtual
    property get name(self) -> str:
        return ""hello""
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.FinalWithAbstractOrVirtual);
    }

    // ===========================
    // SPY0411: Invalid property override
    // ===========================

    [Fact]
    public void OverridePropertyNoBase_ReportsError()
    {
        var code = @"
class Base:
    pass

class Derived(Base):
    @override
    property get name(self) -> str:
        return ""hello""
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.Validation.InvalidPropertyOverride);
    }

    // ===========================
    // Valid property — no errors
    // ===========================

    [Fact]
    public void ValidFunctionStyleProperty_NoError()
    {
        var code = @"
class Foo:
    _name: str

    def __init__(self):
        self._name = ""test""

    property get name(self) -> str:
        return self._name
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        var propErrors = context.Diagnostics.GetErrors()
            .Where(e => e.Code != null && e.Code.StartsWith("SPY04"))
            .ToList();
        Assert.Empty(propErrors);
    }

    [Fact]
    public void ValidAutoProperty_NoError()
    {
        var code = @"
class Foo:
    property name: str
";
        var (module, context) = Parse(code);

        var validator = new PropertyValidator();
        validator.Validate(module, context);

        var propErrors = context.Diagnostics.GetErrors()
            .Where(e => e.Code != null && e.Code.StartsWith("SPY04"))
            .ToList();
        Assert.Empty(propErrors);
    }
}
