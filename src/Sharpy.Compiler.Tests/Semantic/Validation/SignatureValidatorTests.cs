using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Tests for SignatureValidator, which validates operator and protocol dunder signatures.
/// </summary>
public class SignatureValidatorTests
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

    #region Operator Signature - Unary Operators

    [Fact]
    public void UnaryOperator_ValidSignature_NoError()
    {
        var code = @"
class Number:
    def __neg__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void UnaryOperator_TooManyParams_ReportsError()
    {
        var code = @"
class Number:
    def __neg__(self, other: int) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must have exactly 1 parameter"));
    }

    [Fact]
    public void UnaryOperator_VoidReturn_ReportsError()
    {
        var code = @"
class Number:
    def __neg__(self) -> None:
        pass
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return a non-void type"));
    }

    #endregion

    #region Operator Signature - Binary Arithmetic

    [Fact]
    public void BinaryArithmetic_ValidSignature_NoError()
    {
        var code = @"
class Number:
    def __add__(self, other: int) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void BinaryArithmetic_TooFewParams_ReportsError()
    {
        var code = @"
class Number:
    def __add__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must have exactly 2 parameters"));
    }

    [Fact]
    public void BinaryArithmetic_VoidReturn_ReportsError()
    {
        var code = @"
class Number:
    def __add__(self, other: int) -> None:
        pass
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return a non-void type"));
    }

    #endregion

    #region Operator Signature - Comparison Operators

    [Fact]
    public void ComparisonOperator_ValidSignature_NoError()
    {
        var code = @"
class Number:
    def __eq__(self, other: object) -> bool:
        return True
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void ComparisonOperator_NonBoolReturn_ReportsError()
    {
        var code = @"
class Number:
    def __lt__(self, other: int) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return 'bool'"));
    }

    [Fact]
    public void ComparisonOperator_WrongParamCount_ReportsError()
    {
        var code = @"
class Number:
    def __ge__(self) -> bool:
        return True
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must have exactly 2 parameters"));
    }

    #endregion

    #region Protocol Signature - Single Param Methods

    [Fact]
    public void Protocol_Len_ValidSignature_NoError()
    {
        var code = @"
class Container:
    def __len__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Protocol_Len_TooManyParams_ReportsError()
    {
        var code = @"
class Container:
    def __len__(self, other: int) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must have exactly 1 parameter"));
    }

    [Fact]
    public void Protocol_Len_WrongReturnType_ReportsError()
    {
        var code = @"
class Container:
    def __len__(self) -> str:
        return ""0""
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return 'int'"));
    }

    [Fact]
    public void Protocol_Str_ValidSignature_NoError()
    {
        var code = @"
class MyClass:
    def __str__(self) -> str:
        return """"
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Protocol_Bool_ValidSignature_NoError()
    {
        var code = @"
class MyClass:
    def __bool__(self) -> bool:
        return True
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Protocol Signature - Two Param Methods

    [Fact]
    public void Protocol_Contains_ValidSignature_NoError()
    {
        var code = @"
class Container:
    def __contains__(self, item: int) -> bool:
        return False
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Protocol_Contains_TooFewParams_ReportsError()
    {
        var code = @"
class Container:
    def __contains__(self) -> bool:
        return False
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must have exactly 2 parameters"));
    }

    [Fact]
    public void Protocol_GetItem_ValidSignature_NoError()
    {
        var code = @"
class Container:
    def __getitem__(self, index: int) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Protocol Signature - Three Param Methods

    [Fact]
    public void Protocol_SetItem_ValidSignature_NoError()
    {
        var code = @"
class Container:
    def __setitem__(self, index: int, value: int) -> None:
        pass
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Protocol_SetItem_TooFewParams_ReportsError()
    {
        var code = @"
class Container:
    def __setitem__(self, index: int) -> None:
        pass
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must have exactly 3 parameters"));
    }

    #endregion

    #region Protocol Signature - Init (Variable Params)

    [Fact]
    public void Protocol_Init_ValidSignature_NoError()
    {
        var code = @"
class MyClass:
    def __init__(self) -> None:
        pass
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Protocol_Init_WithParams_NoError()
    {
        var code = @"
class MyClass:
    def __init__(self, x: int, y: int) -> None:
        pass
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Protocol_Init_WrongReturnType_ReportsError()
    {
        var code = @"
class MyClass:
    def __init__(self) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must return 'None'"));
    }

    #endregion

    #region Self Parameter Validation

    [Fact]
    public void Protocol_FirstParamNotSelf_ReportsError()
    {
        var code = @"
class MyClass:
    def __len__(this) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("'self'") && e.Message.Contains("'this'"));
    }

    #endregion

    #region Struct Validation

    [Fact]
    public void Struct_ValidOperator_NoError()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def __add__(self, other: Point) -> Point:
        return Point(0, 0)
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Struct_InvalidOperator_ReportsError()
    {
        var code = @"
struct Point:
    x: int
    y: int

    def __add__(self) -> Point:
        return Point(0, 0)
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("must have exactly 2 parameters"));
    }

    #endregion

    #region No Return Type Annotation

    [Fact]
    public void Operator_NoReturnTypeAnnotation_NoError()
    {
        var code = @"
class Number:
    def __add__(self, other: int):
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        // Only validates parameter count when no return type annotation
        Assert.False(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Protocol_NoReturnTypeAnnotation_NoError()
    {
        var code = @"
class Container:
    def __len__(self):
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        // Only validates parameter count when no return type annotation
        Assert.False(context.Diagnostics.HasErrors);
    }

    #endregion

    #region Validator Properties

    [Fact]
    public void Name_ReturnsSignatureValidator()
    {
        var validator = new SignatureValidator();
        Assert.Equal("SignatureValidator", validator.Name);
    }

    [Fact]
    public void Order_Returns150()
    {
        var validator = new SignatureValidator();
        Assert.Equal(150, validator.Order);
    }

    #endregion

    #region Error Message Quality

    [Fact]
    public void Error_IncludesTypeName()
    {
        var code = @"
class MyCustomClass:
    def __len__(self, extra: int) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("MyCustomClass"));
    }

    [Fact]
    public void Error_IncludesInterfaceReference()
    {
        var code = @"
class Container:
    def __len__(self, extra: int) -> int:
        return 0
";
        var (module, context) = Parse(code);

        var validator = new SignatureValidator();
        validator.Validate(module, context);

        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(),
            e => e.Message.Contains("ISized"));
    }

    #endregion
}
