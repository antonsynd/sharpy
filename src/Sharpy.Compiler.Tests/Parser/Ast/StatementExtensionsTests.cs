using Sharpy.Compiler.Parser.Ast;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser.Ast;

/// <summary>
/// Guards <see cref="StatementExtensions.TryGetNestedDeclaration"/> — the single classifier over the
/// seven type-declaring statement kinds (#1729, R-I). One program per kind, parsed through the real
/// lexer/parser rather than hand-built AST nodes, so a future grammar change that renames or reshapes
/// a declaration node's members is caught here rather than downstream at a resolver/emitter call site.
/// </summary>
public class StatementExtensionsTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    [Theory]
    [InlineData("class Foo:\n    pass\n", NestedDeclarationKind.Class, "Foo")]
    [InlineData("struct Foo:\n    x: int\n", NestedDeclarationKind.Struct, "Foo")]
    [InlineData("interface Foo:\n    def m(self) -> int:\n        ...\n", NestedDeclarationKind.Interface, "Foo")]
    [InlineData("enum Foo:\n    A = 1\n", NestedDeclarationKind.Enum, "Foo")]
    [InlineData("union Foo:\n    case A\n", NestedDeclarationKind.Union, "Foo")]
    [InlineData("delegate Foo() -> None\n", NestedDeclarationKind.Delegate, "Foo")]
    [InlineData("type Foo = int\n", NestedDeclarationKind.Alias, "Foo")]
    public void TryGetNestedDeclaration_ReturnsTrue_ForEachOfTheSevenKinds(
        string source, NestedDeclarationKind expectedKind, string expectedName)
    {
        var module = Parse(source);
        var statement = module.Body[0];

        var result = statement.TryGetNestedDeclaration(out var declaration);

        Assert.True(result);
        Assert.Equal(expectedKind, declaration.Kind);
        Assert.Equal(expectedName, declaration.Name);
    }

    [Fact]
    public void TryGetNestedDeclaration_ClassBody_IsCarriedThrough()
    {
        var module = Parse("class Foo:\n    x: int = 1\n");
        var classDef = Assert.IsType<ClassDef>(module.Body[0]);

        Assert.True(classDef.TryGetNestedDeclaration(out var declaration));

        Assert.Equal(classDef.Body, declaration.Body);
    }

    [Fact]
    public void TryGetNestedDeclaration_Enum_HasEmptyBody()
    {
        // An EnumDef declares Members, not statements, and cannot contain a nested declaration —
        // NestedTypeIndex.TypeDeclarationOf already treats it as a leaf; the classifier must agree.
        var module = Parse("enum Foo:\n    A = 1\n");

        Assert.True(module.Body[0].TryGetNestedDeclaration(out var declaration));

        Assert.Empty(declaration.Body);
    }

    [Fact]
    public void TryGetNestedDeclaration_Delegate_HasEmptyBodyAndDecorators()
    {
        // DelegateDef carries a signature (Parameters/ReturnType), not a statement body, and has no
        // Decorators property at all.
        var module = Parse("delegate Foo() -> None\n");

        Assert.True(module.Body[0].TryGetNestedDeclaration(out var declaration));

        Assert.Empty(declaration.Body);
        Assert.Empty(declaration.Decorators);
    }

    [Fact]
    public void TryGetNestedDeclaration_Alias_HasEmptyBodyAndDecorators()
    {
        // TypeAlias carries a target type (Type/FunctionType), not a statement body, and has no
        // Decorators property at all.
        var module = Parse("type Foo = int\n");

        Assert.True(module.Body[0].TryGetNestedDeclaration(out var declaration));

        Assert.Empty(declaration.Body);
        Assert.Empty(declaration.Decorators);
    }

    [Fact]
    public void TryGetNestedDeclaration_ReturnsFalse_ForNonDeclarationStatement()
    {
        var module = Parse("x = 1\n");

        var result = module.Body[0].TryGetNestedDeclaration(out var declaration);

        Assert.False(result);
        Assert.Null(declaration);
    }
}
