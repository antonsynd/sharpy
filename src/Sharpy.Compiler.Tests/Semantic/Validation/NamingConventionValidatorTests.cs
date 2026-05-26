using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

[Collection("Sequential")]
public class NamingConventionValidatorTests
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

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    private List<CompilerDiagnostic> GetNamingWarnings(SemanticContext context)
    {
        return context.Diagnostics.GetWarnings()
            .Where(w => w.Code == DiagnosticCodes.Validation.NamingConventionWarning)
            .ToList();
    }

    private void ValidateCode(string code, out List<CompilerDiagnostic> warnings)
    {
        var (module, context) = Parse(code);
        var validator = new NamingConventionValidator();
        validator.Validate(module, context);
        warnings = GetNamingWarnings(context);
    }

    #region Variable Declaration Tests

    [Fact]
    public void Variable_ConsecutiveUnderscores_Warns()
    {
        ValidateCode("foo__bar: int = 1", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("foo__bar", warnings[0].Message);
        Assert.Contains("consecutive underscores", warnings[0].Message);
    }

    [Fact]
    public void Variable_SingleUnderscores_NoWarning()
    {
        ValidateCode("foo_bar: int = 1", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Variable_NoUnderscores_NoWarning()
    {
        ValidateCode("count: int = 1", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Variable_PrivatePrefix_NoWarning()
    {
        ValidateCode("_private_var: int = 1", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Variable_DoublePrivatePrefix_NoWarning()
    {
        ValidateCode("__private_var: int = 1", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Variable_DoublePrivatePrefixWithConsecutive_Warns()
    {
        ValidateCode("__foo__bar: int = 1", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("__foo__bar", warnings[0].Message);
    }

    #endregion

    #region Function Definition Tests

    [Fact]
    public void Function_ConsecutiveUnderscores_Warns()
    {
        var code = @"
def my__func():
    pass
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("my__func", warnings[0].Message);
    }

    [Fact]
    public void Function_SingleUnderscores_NoWarning()
    {
        var code = @"
def my_func():
    pass
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Function_Dunder_NoWarning()
    {
        var code = @"
class Foo:
    def __init__(self):
        pass
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Function_Dunder_Str_NoWarning()
    {
        var code = @"
class Foo:
    def __str__(self) -> str:
        pass
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Function_Dunder_CustomMethod_NoWarning()
    {
        var code = @"
class Foo:
    def __custom_method__(self):
        pass
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Parameter Tests

    [Fact]
    public void Parameter_ConsecutiveUnderscores_Warns()
    {
        var code = @"
def foo(my__param: int):
    pass
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("my__param", warnings[0].Message);
    }

    [Fact]
    public void Parameter_SingleUnderscores_NoWarning()
    {
        var code = @"
def foo(my_param: int):
    pass
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Class/Struct/Interface Tests

    [Fact]
    public void Class_ConsecutiveUnderscores_Warns()
    {
        var code = @"
class My__Class:
    pass
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("My__Class", warnings[0].Message);
    }

    [Fact]
    public void Struct_ConsecutiveUnderscores_Warns()
    {
        var code = @"
struct My__Struct:
    x: int
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("My__Struct", warnings[0].Message);
    }

    [Fact]
    public void Interface_ConsecutiveUnderscores_Warns()
    {
        var code = @"
interface My__Interface:
    def do_thing(self):
        pass
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("My__Interface", warnings[0].Message);
    }

    [Fact]
    public void Class_NormalName_NoWarning()
    {
        var code = @"
class MyClass:
    pass
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Enum Tests

    [Fact]
    public void Enum_ConsecutiveUnderscoresInName_Warns()
    {
        var code = @"
enum My__Enum:
    VALUE_A
    VALUE_B
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("My__Enum", warnings[0].Message);
    }

    [Fact]
    public void Enum_ConsecutiveUnderscoresInMember_Warns()
    {
        var code = @"
enum Color:
    DARK__BLUE
    RED
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("DARK__BLUE", warnings[0].Message);
    }

    [Fact]
    public void Enum_NormalMembers_NoWarning()
    {
        var code = @"
enum Color:
    RED
    DARK_BLUE
    LIGHT_GREEN
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Backtick Escaping Tests

    [Fact]
    public void BacktickEscaped_ConsecutiveUnderscores_NoWarning()
    {
        // The lexer strips backticks but sets IsNameBacktickEscaped on the AST node,
        // so the validator can suppress the warning for backtick-escaped identifiers.
        ValidateCode("`foo__bar`: int = 1", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void BacktickEscaped_Function_ConsecutiveUnderscores_NoWarning()
    {
        var code = @"
def `foo__bar`():
    pass
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void BacktickEscaped_Variable_ConsecutiveUnderscores_NoWarning()
    {
        ValidateCode("`foo__bar`: int = 1", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void NonBacktick_Variable_ConsecutiveUnderscores_StillWarns()
    {
        ValidateCode("foo__bar: int = 1", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("foo__bar", warnings[0].Message);
    }

    #endregion

    #region Local Variable Tests

    [Fact]
    public void LocalVariable_ConsecutiveUnderscores_Warns()
    {
        var code = @"
def foo():
    local__var: int = 1
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("local__var", warnings[0].Message);
    }

    [Fact]
    public void LocalVariable_Normal_NoWarning()
    {
        var code = @"
def foo():
    local_var: int = 1
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Class Field Tests

    [Fact]
    public void ClassField_ConsecutiveUnderscores_Warns()
    {
        var code = @"
class Foo:
    my__field: int
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("my__field", warnings[0].Message);
    }

    #endregion

    #region For Loop Target Tests

    [Fact]
    public void ForLoopTarget_ConsecutiveUnderscores_Warns()
    {
        var code = @"
def foo():
    for loop__var in range(10):
        pass
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("loop__var", warnings[0].Message);
    }

    [Fact]
    public void ForLoopTarget_Normal_NoWarning()
    {
        var code = @"
def foo():
    for loop_var in range(10):
        pass
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Except Handler Variable Tests

    [Fact]
    public void ExceptHandler_ConsecutiveUnderscores_Warns()
    {
        var code = @"
def foo():
    try:
        pass
    except Exception as err__var:
        pass
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("err__var", warnings[0].Message);
    }

    [Fact]
    public void ExceptHandler_Normal_NoWarning()
    {
        var code = @"
def foo():
    try:
        pass
    except Exception as err:
        pass
";
        ValidateCode(code, out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region ElseBody Tests

    [Fact]
    public void ForElseBody_ConsecutiveUnderscores_Warns()
    {
        var code = @"
def foo():
    for i in range(10):
        pass
    else:
        else__var: int = 1
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("else__var", warnings[0].Message);
    }

    [Fact]
    public void WhileElseBody_ConsecutiveUnderscores_Warns()
    {
        var code = @"
def foo():
    while True:
        pass
    else:
        else__var: int = 1
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("else__var", warnings[0].Message);
    }

    [Fact]
    public void TryElseBody_ConsecutiveUnderscores_Warns()
    {
        var code = @"
def foo():
    try:
        pass
    except Exception as e:
        pass
    else:
        else__var: int = 1
";
        ValidateCode(code, out var warnings);
        Assert.Single(warnings);
        Assert.Contains("else__var", warnings[0].Message);
    }

    #endregion

    #region Multiple Warnings Tests

    [Fact]
    public void MultipleViolations_WarnsForEach()
    {
        var code = @"
foo__bar: int = 1
baz__qux: str = ""hello""
";
        ValidateCode(code, out var warnings);
        Assert.Equal(2, warnings.Count);
    }

    #endregion

    #region Diagnostic Code Tests

    [Fact]
    public void Warning_HasCorrectDiagnosticCode()
    {
        ValidateCode("foo__bar: int = 1", out var warnings);
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.Validation.NamingConventionWarning, warnings[0].Code);
    }

    #endregion

    #region Convention: Method/Function Tests

    [Fact]
    public void Function_SnakeCase_NoWarning()
    {
        ValidateCode("def my_function():\n    pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Function_SingleWordLower_NoWarning()
    {
        ValidateCode("def run():\n    pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Function_PascalCase_Warns()
    {
        ValidateCode("def MyFunction():\n    pass\n", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("snake_case", warnings[0].Message);
        Assert.Contains("my_function", warnings[0].Message);
    }

    [Fact]
    public void Function_CamelCase_Warns()
    {
        ValidateCode("def myFunction():\n    pass\n", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("snake_case", warnings[0].Message);
    }

    #endregion

    #region Convention: Class/Struct/Interface Tests

    [Fact]
    public void Class_PascalCase_NoWarning()
    {
        ValidateCode("class MyClass:\n    pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Class_SingleWordUpper_NoWarning()
    {
        ValidateCode("class HTTP:\n    pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Class_SnakeCase_Warns()
    {
        ValidateCode("class my_class:\n    pass\n", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("PascalCase", warnings[0].Message);
        Assert.Contains("MyClass", warnings[0].Message);
    }

    [Fact]
    public void Struct_LowerCase_Warns()
    {
        ValidateCode("struct point:\n    x: int\n", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("PascalCase", warnings[0].Message);
    }

    [Fact]
    public void Interface_PascalCase_NoWarning()
    {
        ValidateCode("interface IShape:\n    def area(self) -> float:\n        pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Convention: Enum Tests

    [Fact]
    public void EnumType_PascalCase_NoWarning()
    {
        ValidateCode("enum Color:\n    RED = 1\n    GREEN = 2\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void EnumType_SnakeCase_Warns()
    {
        ValidateCode("enum my_color:\n    RED = 1\n", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("PascalCase", warnings[0].Message);
    }

    [Fact]
    public void EnumValue_ScreamingSnakeCase_NoWarning()
    {
        ValidateCode("enum Color:\n    DARK_RED = 1\n    LIGHT_BLUE = 2\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void EnumValue_LowerCase_Warns()
    {
        ValidateCode("enum Color:\n    red = 1\n    green = 2\n", out var warnings);
        Assert.Equal(2, warnings.Count);
        Assert.Contains("SCREAMING_SNAKE_CASE", warnings[0].Message);
        Assert.Contains("enum values", warnings[0].Message);
    }

    #endregion

    #region Convention: Variable Tests

    [Fact]
    public void Variable_SnakeCase_NoWarning()
    {
        ValidateCode("my_var: int = 1", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Variable_PascalCase_Warns()
    {
        ValidateCode("MyVar: int = 1", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("snake_case", warnings[0].Message);
        Assert.Contains("my_var", warnings[0].Message);
    }

    [Fact]
    public void LocalVariable_PascalCase_Warns()
    {
        ValidateCode("def foo():\n    LocalVar: int = 1\n    print(LocalVar)\n", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("snake_case", warnings[0].Message);
    }

    [Fact]
    public void PrivatePrefixedVariable_SnakeCaseBody_NoWarning()
    {
        ValidateCode("_my_var: int = 1", out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Convention: Constant Tests

    [Fact]
    public void Constant_ScreamingSnakeCase_NoWarning()
    {
        ValidateCode("const MAX_SIZE: int = 1", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Constant_SingleWordUpper_NoWarning()
    {
        ValidateCode("const PI: int = 3", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Constant_SnakeCase_Warns()
    {
        ValidateCode("const max_size: int = 1", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("SCREAMING_SNAKE_CASE", warnings[0].Message);
        Assert.Contains("MAX_SIZE", warnings[0].Message);
    }

    #endregion

    #region Convention: Parameter Tests

    [Fact]
    public void Parameter_SnakeCase_NoWarning()
    {
        ValidateCode("def foo(my_param: int):\n    pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Parameter_PascalCase_Warns()
    {
        ValidateCode("def foo(MyParam: int):\n    pass\n", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("snake_case", warnings[0].Message);
        Assert.Contains("parameters", warnings[0].Message);
    }

    [Fact]
    public void Parameter_Self_NoWarning()
    {
        ValidateCode("class Foo:\n    def bar(self):\n        pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Parameter_Cls_NoWarning()
    {
        ValidateCode("class Foo:\n    @classmethod\n    def bar(cls):\n        pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Convention: Exemption Tests

    [Fact]
    public void Convention_DunderMethod_NoWarning()
    {
        ValidateCode("class Foo:\n    def __init__(self):\n        pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Convention_BacktickEscapedFunction_NoWarning()
    {
        ValidateCode("def `MyFunction`():\n    pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Convention_BacktickEscapedClass_NoWarning()
    {
        ValidateCode("class `my_class`:\n    pass\n", out var warnings);
        Assert.Empty(warnings);
    }

    #endregion

    #region Convention: Combined Warning Tests

    [Fact]
    public void ConsecutiveUnderscoresAndWrongConvention_EmitsSingleWarning()
    {
        // foo__Bar has consecutive underscores AND is not valid snake_case. Both the
        // consecutive-underscore check and the convention check run, but since both emit
        // SPY0453 at the same identifier location they are deduplicated to a single
        // diagnostic. The consecutive-underscore check runs first, so its message wins.
        ValidateCode("def foo__Bar():\n    pass\n", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("foo__Bar", warnings[0].Message);
        Assert.Contains("consecutive underscores", warnings[0].Message);
    }

    [Fact]
    public void WrongConventionWithoutConsecutiveUnderscores_EmitsConventionWarning()
    {
        // PascalCase method without consecutive underscores → only the convention warning.
        ValidateCode("def FooBar():\n    pass\n", out var warnings);
        Assert.Single(warnings);
        Assert.Contains("snake_case", warnings[0].Message);
        Assert.Contains("foo_bar", warnings[0].Message);
    }

    #endregion
}
