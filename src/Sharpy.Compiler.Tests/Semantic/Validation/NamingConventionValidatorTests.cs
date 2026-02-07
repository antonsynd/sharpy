using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
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
    public void BacktickEscaped_ConsecutiveUnderscores_WarnsAfterLexerStripping()
    {
        // The lexer strips backticks from literal names, so by the time the AST is built,
        // `foo__bar` becomes just "foo__bar" — indistinguishable from a regular name.
        // The validator cannot suppress the warning at this pipeline stage.
        ValidateCode("`foo__bar`: int = 1", out var warnings);
        Assert.Single(warnings);
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
}
