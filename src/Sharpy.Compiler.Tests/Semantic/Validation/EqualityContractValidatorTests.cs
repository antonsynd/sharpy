using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Tests for EqualityContractValidator:
/// - SPY0454: warns when __eq__ exists without object overload
/// - SPY0455: errors when __eq__(object) exists without __hash__
/// - SPY0456: errors when __hash__ exists without __eq__(object)
/// </summary>
public class EqualityContractValidatorTests
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

        // Run name resolution
        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    [Fact]
    public void EqWithObjectOverload_NoWarning()
    {
        var code = @"
class Foo:
    def __eq__(self, other: object) -> bool:
        return True

    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void EqWithTypedOverloadOnly_WarnsAboutMissingObjectOverload()
    {
        var code = @"
class Foo:
    def __eq__(self, other: Foo) -> bool:
        return True
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Single(warnings);
        Assert.Contains("__eq__", warnings[0].Message);
        Assert.Contains("__eq__(self, other: object)", warnings[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.EqWithoutObjectOverload, warnings[0].Code);
    }

    [Fact]
    public void EqWithBothTypedAndObjectOverloads_NoWarning()
    {
        var code = @"
class Foo:
    def __eq__(self, other: Foo) -> bool:
        return True

    def __eq__(self, other: object) -> bool:
        return False

    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void NoEqDefined_NoWarning()
    {
        var code = @"
class Foo:
    x: int

    def __init__(self, x: int):
        self.x = x
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void StructWithTypedEqOnly_WarnsAboutMissingObjectOverload()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def __eq__(self, other: Point) -> bool:
        return self.x == other.x and self.y == other.y
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        var warnings = context.Diagnostics.GetWarnings();
        Assert.Single(warnings);
        Assert.Contains("Point", warnings[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.EqWithoutObjectOverload, warnings[0].Code);
    }

    [Fact]
    public void StructWithObjectEq_NoWarning()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def __eq__(self, other: object) -> bool:
        return False

    def __hash__(self) -> int:
        return self.x * 31 + self.y
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void EqObjectWithoutHash_ErrorsSPY0455()
    {
        var code = @"
class Foo:
    def __eq__(self, other: object) -> bool:
        return True
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("__eq__(self, other: object)", errors[0].Message);
        Assert.Contains("__hash__", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.EqObjectWithoutHash, errors[0].Code);
    }

    [Fact]
    public void HashWithoutEqObject_ErrorsSPY0456()
    {
        var code = @"
class Foo:
    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Contains("__hash__", errors[0].Message);
        Assert.Contains("__eq__(self, other: object)", errors[0].Message);
        Assert.Equal(DiagnosticCodes.Validation.HashWithoutEqObject, errors[0].Code);
    }

    [Fact]
    public void HashWithTypedEqOnly_ErrorsSPY0456()
    {
        // __eq__(Foo) exists but not __eq__(object), so __hash__ triggers SPY0456
        var code = @"
class Foo:
    def __eq__(self, other: Foo) -> bool:
        return True

    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.Validation.HashWithoutEqObject, errors[0].Code);
    }

    [Fact]
    public void OnlyHashNoEq_ErrorsSPY0456()
    {
        var code = @"
class Foo:
    x: int

    def __init__(self, x: int):
        self.x = x

    def __hash__(self) -> int:
        return self.x
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.Validation.HashWithoutEqObject, errors[0].Code);
    }

    [Fact]
    public void BothEqObjectAndHash_NoError()
    {
        var code = @"
class Foo:
    def __eq__(self, other: object) -> bool:
        return True

    def __hash__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
        Assert.Empty(context.Diagnostics.GetWarnings());
    }

    [Fact]
    public void StructEqObjectWithoutHash_ErrorsSPY0455()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def __eq__(self, other: object) -> bool:
        return False
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        var errors = context.Diagnostics.GetErrors();
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.Validation.EqObjectWithoutHash, errors[0].Code);
    }

    /// <summary>
    /// A duplicate class owns no symbol of its own (NameResolver refuses it with SPY0204), so the
    /// inherited-half rules have no ancestry to check against and are not applied — before the fix
    /// the null symbol read as "nothing inherited" and the duplicate drew a false SPY0455 although
    /// its base supplies <c>__hash__</c> (#2063). <c>Plain</c> is the positive control: the same
    /// validator, on the same module, still reports a real SPY0455.
    /// </summary>
    [Fact]
    public void DuplicateClass_OwnsNoSymbol_NoEqualityContractCascade()
    {
        var code = @"
class Base:
    def __hash__(self) -> int:
        return 1

    def __eq__(self, other: object) -> bool:
        return True

class Plain:
    def __eq__(self, other: object) -> bool:
        return True

class Foo(Base):
    pass

class Foo(Base):
    def __eq__(self, other: object) -> bool:
        return True
";
        var (module, context) = Parse(code);

        var validator = new EqualityContractValidator();
        validator.Validate(module, context);

        var error = Assert.Single(context.Diagnostics.GetErrors());
        Assert.Equal(DiagnosticCodes.Validation.EqObjectWithoutHash, error.Code);
        Assert.Contains("'Plain'", error.Message);
    }
}
