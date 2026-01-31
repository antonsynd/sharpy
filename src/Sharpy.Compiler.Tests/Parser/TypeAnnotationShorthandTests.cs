using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Tests for type annotation shorthand syntax parsing.
/// Verifies that shorthand forms produce the same AST as canonical forms.
/// </summary>
public class TypeAnnotationShorthandTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = new List<LexerNs.Token>();
        while (true)
        {
            var token = lexer.NextToken();
            tokens.Add(token);
            if (token.Type == LexerNs.TokenType.Eof)
                break;
        }
        var parser = new ParserNs.Parser(tokens);
        return parser.ParseModule();
    }

    private static string ParseExpectingError(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeTrue("Expected parser to report an error for input: " + source);
        return string.Join("\n", parser.Diagnostics.GetErrors().Select(d => d.Message));
    }

    private static TypeAnnotation ParseTypeAnnotation(string typeSource)
    {
        var source = $"x: {typeSource}";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        return varDecl.Type!;
    }

    #region List Shorthand [T]

    [Fact]
    public void ParseListShorthand_SimpleType()
    {
        var annotation = ParseTypeAnnotation("[int]");
        annotation.Name.Should().Be("list");
        annotation.TypeArguments.Should().HaveCount(1);
        annotation.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseListShorthand_StringType()
    {
        var annotation = ParseTypeAnnotation("[str]");
        annotation.Name.Should().Be("list");
        annotation.TypeArguments[0].Name.Should().Be("str");
    }

    [Fact]
    public void ParseListShorthand_Nested()
    {
        var annotation = ParseTypeAnnotation("[[str]]");
        annotation.Name.Should().Be("list");
        annotation.TypeArguments.Should().HaveCount(1);
        annotation.TypeArguments[0].Name.Should().Be("list");
        annotation.TypeArguments[0].TypeArguments[0].Name.Should().Be("str");
    }

    [Fact]
    public void ParseListShorthand_DeeplyNested()
    {
        var annotation = ParseTypeAnnotation("[[[int]]]");
        annotation.Name.Should().Be("list");
        annotation.TypeArguments[0].Name.Should().Be("list");
        annotation.TypeArguments[0].TypeArguments[0].Name.Should().Be("list");
        annotation.TypeArguments[0].TypeArguments[0].TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseListShorthand_Nullable()
    {
        var annotation = ParseTypeAnnotation("[int]?");
        annotation.Name.Should().Be("list");
        annotation.IsOptional.Should().BeTrue();
        annotation.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseListShorthand_NullableElement()
    {
        var annotation = ParseTypeAnnotation("[int?]");
        annotation.Name.Should().Be("list");
        annotation.IsOptional.Should().BeFalse();
        annotation.TypeArguments[0].Name.Should().Be("int");
        annotation.TypeArguments[0].IsOptional.Should().BeTrue();
    }

    #endregion

    #region Set Shorthand {T}

    [Fact]
    public void ParseSetShorthand_SimpleType()
    {
        var annotation = ParseTypeAnnotation("{int}");
        annotation.Name.Should().Be("set");
        annotation.TypeArguments.Should().HaveCount(1);
        annotation.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseSetShorthand_StringType()
    {
        var annotation = ParseTypeAnnotation("{str}");
        annotation.Name.Should().Be("set");
        annotation.TypeArguments[0].Name.Should().Be("str");
    }

    [Fact]
    public void ParseSetShorthand_Nested()
    {
        // Set of sets
        var annotation = ParseTypeAnnotation("{{int}}");
        annotation.Name.Should().Be("set");
        annotation.TypeArguments[0].Name.Should().Be("set");
        annotation.TypeArguments[0].TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseSetShorthand_Nullable()
    {
        var annotation = ParseTypeAnnotation("{str}?");
        annotation.Name.Should().Be("set");
        annotation.IsOptional.Should().BeTrue();
        annotation.TypeArguments[0].Name.Should().Be("str");
    }

    [Fact]
    public void ParseSetShorthand_ContainingList()
    {
        // Set of lists
        var annotation = ParseTypeAnnotation("{[int]}");
        annotation.Name.Should().Be("set");
        annotation.TypeArguments[0].Name.Should().Be("list");
        annotation.TypeArguments[0].TypeArguments[0].Name.Should().Be("int");
    }

    #endregion

    #region Dict Shorthand {K: V}

    [Fact]
    public void ParseDictShorthand_SimpleTypes()
    {
        var annotation = ParseTypeAnnotation("{str: int}");
        annotation.Name.Should().Be("dict");
        annotation.TypeArguments.Should().HaveCount(2);
        annotation.TypeArguments[0].Name.Should().Be("str");
        annotation.TypeArguments[1].Name.Should().Be("int");
    }

    [Fact]
    public void ParseDictShorthand_IntKeys()
    {
        var annotation = ParseTypeAnnotation("{int: str}");
        annotation.Name.Should().Be("dict");
        annotation.TypeArguments[0].Name.Should().Be("int");
        annotation.TypeArguments[1].Name.Should().Be("str");
    }

    [Fact]
    public void ParseDictShorthand_Nullable()
    {
        var annotation = ParseTypeAnnotation("{str: int}?");
        annotation.Name.Should().Be("dict");
        annotation.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseDictShorthand_ListValue()
    {
        // Dict with list values: {str: [int]}
        var annotation = ParseTypeAnnotation("{str: [int]}");
        annotation.Name.Should().Be("dict");
        annotation.TypeArguments[0].Name.Should().Be("str");
        annotation.TypeArguments[1].Name.Should().Be("list");
        annotation.TypeArguments[1].TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseDictShorthand_SetValue()
    {
        // Dict with set values: {str: {int}}
        var annotation = ParseTypeAnnotation("{str: {int}}");
        annotation.Name.Should().Be("dict");
        annotation.TypeArguments[0].Name.Should().Be("str");
        annotation.TypeArguments[1].Name.Should().Be("set");
    }

    [Fact]
    public void ParseDictShorthand_NestedDict()
    {
        // Dict of dicts: {str: {str: int}}
        var annotation = ParseTypeAnnotation("{str: {str: int}}");
        annotation.Name.Should().Be("dict");
        annotation.TypeArguments[1].Name.Should().Be("dict");
        annotation.TypeArguments[1].TypeArguments[0].Name.Should().Be("str");
        annotation.TypeArguments[1].TypeArguments[1].Name.Should().Be("int");
    }

    #endregion

    #region Tuple Shorthand (T, U)

    [Fact]
    public void ParseTupleShorthand_Empty()
    {
        var annotation = ParseTypeAnnotation("()");
        annotation.Name.Should().Be("tuple");
        annotation.TypeArguments.Should().BeEmpty();
    }

    [Fact]
    public void ParseTupleShorthand_SingleElement_NoComma()
    {
        var annotation = ParseTypeAnnotation("(int)");
        annotation.Name.Should().Be("tuple");
        annotation.TypeArguments.Should().HaveCount(1);
        annotation.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseTupleShorthand_SingleElement_WithComma()
    {
        var annotation = ParseTypeAnnotation("(int,)");
        annotation.Name.Should().Be("tuple");
        annotation.TypeArguments.Should().HaveCount(1);
        annotation.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseTupleShorthand_TwoElements()
    {
        var annotation = ParseTypeAnnotation("(int, str)");
        annotation.Name.Should().Be("tuple");
        annotation.TypeArguments.Should().HaveCount(2);
        annotation.TypeArguments[0].Name.Should().Be("int");
        annotation.TypeArguments[1].Name.Should().Be("str");
    }

    [Fact]
    public void ParseTupleShorthand_ThreeElements()
    {
        var annotation = ParseTypeAnnotation("(int, str, bool)");
        annotation.Name.Should().Be("tuple");
        annotation.TypeArguments.Should().HaveCount(3);
        annotation.TypeArguments[0].Name.Should().Be("int");
        annotation.TypeArguments[1].Name.Should().Be("str");
        annotation.TypeArguments[2].Name.Should().Be("bool");
    }

    [Fact]
    public void ParseTupleShorthand_TrailingComma()
    {
        var annotation = ParseTypeAnnotation("(int, str,)");
        annotation.Name.Should().Be("tuple");
        annotation.TypeArguments.Should().HaveCount(2);
    }

    [Fact]
    public void ParseTupleShorthand_Nullable()
    {
        var annotation = ParseTypeAnnotation("(int, str)?");
        annotation.Name.Should().Be("tuple");
        annotation.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseTupleShorthand_NestedTuple()
    {
        var annotation = ParseTypeAnnotation("((int, str), bool)");
        annotation.Name.Should().Be("tuple");
        annotation.TypeArguments.Should().HaveCount(2);
        annotation.TypeArguments[0].Name.Should().Be("tuple");
        annotation.TypeArguments[0].TypeArguments.Should().HaveCount(2);
        annotation.TypeArguments[1].Name.Should().Be("bool");
    }

    [Fact]
    public void ParseTupleShorthand_WithListElement()
    {
        var annotation = ParseTypeAnnotation("([int], str)");
        annotation.Name.Should().Be("tuple");
        annotation.TypeArguments[0].Name.Should().Be("list");
        annotation.TypeArguments[1].Name.Should().Be("str");
    }

    #endregion

    #region Array Shorthand T[]

    [Fact]
    public void ParseArrayShorthand_SimpleType()
    {
        var annotation = ParseTypeAnnotation("int[]");
        annotation.Name.Should().Be("array");
        annotation.TypeArguments.Should().HaveCount(1);
        annotation.TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseArrayShorthand_StringType()
    {
        var annotation = ParseTypeAnnotation("str[]");
        annotation.Name.Should().Be("array");
        annotation.TypeArguments[0].Name.Should().Be("str");
    }

    [Fact]
    public void ParseArrayShorthand_MultiDimensional()
    {
        // int[][] = array of array of int
        var annotation = ParseTypeAnnotation("int[][]");
        annotation.Name.Should().Be("array");
        annotation.TypeArguments[0].Name.Should().Be("array");
        annotation.TypeArguments[0].TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseArrayShorthand_Nullable()
    {
        var annotation = ParseTypeAnnotation("int[]?");
        annotation.Name.Should().Be("array");
        annotation.IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseArrayShorthand_OfList()
    {
        // [int][] = array of list[int]
        var annotation = ParseTypeAnnotation("[int][]");
        annotation.Name.Should().Be("array");
        annotation.TypeArguments[0].Name.Should().Be("list");
        annotation.TypeArguments[0].TypeArguments[0].Name.Should().Be("int");
    }

    [Fact]
    public void ParseArrayShorthand_OfTuple()
    {
        // (int, str)[] = array of tuple
        var annotation = ParseTypeAnnotation("(int, str)[]");
        annotation.Name.Should().Be("array");
        annotation.TypeArguments[0].Name.Should().Be("tuple");
    }

    [Fact]
    public void ParseArrayShorthand_OfGenericType()
    {
        // list[int][] = array of list[int]
        var annotation = ParseTypeAnnotation("list[int][]");
        annotation.Name.Should().Be("array");
        annotation.TypeArguments[0].Name.Should().Be("list");
        annotation.TypeArguments[0].TypeArguments[0].Name.Should().Be("int");
    }

    #endregion

    #region Function Types (T) -> U

    [Fact]
    public void ParseFunctionType_SingleParam()
    {
        var annotation = ParseTypeAnnotation("(int) -> str");
        annotation.Name.Should().Be("function");
        annotation.TypeArguments.Should().HaveCount(2);
        annotation.TypeArguments[0].Name.Should().Be("int");  // param
        annotation.TypeArguments[1].Name.Should().Be("str");  // return
    }

    [Fact]
    public void ParseFunctionType_NoParams()
    {
        var annotation = ParseTypeAnnotation("() -> int");
        annotation.Name.Should().Be("function");
        annotation.TypeArguments.Should().HaveCount(1);
        annotation.TypeArguments[0].Name.Should().Be("int");  // return type only
    }

    [Fact]
    public void ParseFunctionType_MultipleParams()
    {
        var annotation = ParseTypeAnnotation("(int, str, bool) -> float");
        annotation.Name.Should().Be("function");
        annotation.TypeArguments.Should().HaveCount(4);
        annotation.TypeArguments[0].Name.Should().Be("int");
        annotation.TypeArguments[1].Name.Should().Be("str");
        annotation.TypeArguments[2].Name.Should().Be("bool");
        annotation.TypeArguments[3].Name.Should().Be("float");  // return
    }

    [Fact]
    public void ParseFunctionType_WithShorthandParamTypes()
    {
        var annotation = ParseTypeAnnotation("([int]) -> {str: int}");
        annotation.Name.Should().Be("function");
        annotation.TypeArguments[0].Name.Should().Be("list");
        annotation.TypeArguments[1].Name.Should().Be("dict");
    }

    [Fact]
    public void ParseFunctionType_Nullable()
    {
        var annotation = ParseTypeAnnotation("(int) -> str?");
        annotation.Name.Should().Be("function");
        annotation.TypeArguments[1].IsOptional.Should().BeTrue();
    }

    [Fact]
    public void ParseFunctionType_ReturningTuple()
    {
        var annotation = ParseTypeAnnotation("(int) -> (str, bool)");
        annotation.Name.Should().Be("function");
        annotation.TypeArguments[1].Name.Should().Be("tuple");
    }

    #endregion

    #region AST Equivalence Tests

    [Fact]
    public void ShorthandProducesSameAST_List()
    {
        var shorthand = ParseTypeAnnotation("[int]");
        var canonical = ParseTypeAnnotation("list[int]");

        shorthand.Name.Should().Be(canonical.Name);
        shorthand.TypeArguments.Should().HaveCount(canonical.TypeArguments.Length);
        shorthand.TypeArguments[0].Name.Should().Be(canonical.TypeArguments[0].Name);
    }

    [Fact]
    public void ShorthandProducesSameAST_Set()
    {
        var shorthand = ParseTypeAnnotation("{str}");
        var canonical = ParseTypeAnnotation("set[str]");

        shorthand.Name.Should().Be(canonical.Name);
        shorthand.TypeArguments[0].Name.Should().Be(canonical.TypeArguments[0].Name);
    }

    [Fact]
    public void ShorthandProducesSameAST_Dict()
    {
        var shorthand = ParseTypeAnnotation("{str: int}");
        var canonical = ParseTypeAnnotation("dict[str, int]");

        shorthand.Name.Should().Be(canonical.Name);
        shorthand.TypeArguments.Should().HaveCount(canonical.TypeArguments.Length);
        shorthand.TypeArguments[0].Name.Should().Be(canonical.TypeArguments[0].Name);
        shorthand.TypeArguments[1].Name.Should().Be(canonical.TypeArguments[1].Name);
    }

    [Fact]
    public void ShorthandProducesSameAST_Tuple()
    {
        var shorthand = ParseTypeAnnotation("(int, str)");
        var canonical = ParseTypeAnnotation("tuple[int, str]");

        shorthand.Name.Should().Be(canonical.Name);
        shorthand.TypeArguments.Should().HaveCount(canonical.TypeArguments.Length);
        shorthand.TypeArguments[0].Name.Should().Be(canonical.TypeArguments[0].Name);
        shorthand.TypeArguments[1].Name.Should().Be(canonical.TypeArguments[1].Name);
    }

    [Fact]
    public void ShorthandProducesSameAST_NestedList()
    {
        var shorthand = ParseTypeAnnotation("[[int]]");
        var canonical = ParseTypeAnnotation("list[list[int]]");

        shorthand.Name.Should().Be(canonical.Name);
        shorthand.TypeArguments[0].Name.Should().Be(canonical.TypeArguments[0].Name);
        shorthand.TypeArguments[0].TypeArguments[0].Name.Should().Be(
            canonical.TypeArguments[0].TypeArguments[0].Name);
    }

    [Fact]
    public void ShorthandProducesSameAST_ComplexNested()
    {
        // {str: [(int, bool)]} should equal dict[str, list[tuple[int, bool]]]
        var shorthand = ParseTypeAnnotation("{str: [(int, bool)]}");
        var canonical = ParseTypeAnnotation("dict[str, list[tuple[int, bool]]]");

        shorthand.Name.Should().Be(canonical.Name);
        shorthand.TypeArguments[0].Name.Should().Be(canonical.TypeArguments[0].Name);
        shorthand.TypeArguments[1].Name.Should().Be(canonical.TypeArguments[1].Name);
        shorthand.TypeArguments[1].TypeArguments[0].Name.Should().Be(
            canonical.TypeArguments[1].TypeArguments[0].Name);
    }

    #endregion

    #region Error Cases

    [Fact]
    public void ParseError_EmptyListShorthand()
    {
        var source = "x: []";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("element type");
    }

    [Fact]
    public void ParseError_EmptySetOrDict()
    {
        var source = "x: {}";
        var errors = ParseExpectingError(source);
        errors.Should().Contain("type");
    }

    #endregion

    #region Context Tests - Function Parameters and Return Types

    [Fact]
    public void ParseFunctionDef_WithShorthandParamTypes()
    {
        var source = @"
def process(items: [int], mapping: {str: int}) -> [str]:
    pass
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        func.Parameters[0].Type!.Name.Should().Be("list");
        func.Parameters[1].Type!.Name.Should().Be("dict");
        func.ReturnType!.Name.Should().Be("list");
    }

    [Fact]
    public void ParseFunctionDef_WithTupleReturn()
    {
        var source = @"
def get_pair() -> (int, str):
    pass
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        func.ReturnType!.Name.Should().Be("tuple");
        func.ReturnType!.TypeArguments.Should().HaveCount(2);
    }

    [Fact]
    public void ParseFunctionDef_WithFunctionTypeParam()
    {
        var source = @"
def apply(f: (int) -> str, x: int) -> str:
    pass
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        func.Parameters[0].Type!.Name.Should().Be("function");
    }

    #endregion

    #region Context Tests - Variable Declarations

    [Fact]
    public void ParseVariableDecl_WithListShorthand()
    {
        var source = "items: [int] = [1, 2, 3]";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;

        varDecl.Type!.Name.Should().Be("list");
    }

    [Fact]
    public void ParseVariableDecl_WithDictShorthand()
    {
        var source = "scores: {str: int} = {}";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;

        varDecl.Type!.Name.Should().Be("dict");
    }

    [Fact]
    public void ParseVariableDecl_WithNullableShorthand()
    {
        var source = "maybe_items: [int]? = None";
        var module = Parse(source);
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;

        varDecl.Type!.Name.Should().Be("list");
        varDecl.Type!.IsOptional.Should().BeTrue();
    }

    #endregion

    #region Context Tests - Class Fields

    [Fact]
    public void ParseClassDef_WithShorthandFieldTypes()
    {
        var source = @"
class Container:
    items: [str]
    lookup: {str: int}
    pair: (int, int)
";
        var module = Parse(source);
        var classDef = module.Body[0].Should().BeOfType<ClassDef>().Subject;

        // Fields are stored in the Body as VariableDeclaration statements
        var items = classDef.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        items.Type!.Name.Should().Be("list");

        var lookup = classDef.Body[1].Should().BeOfType<VariableDeclaration>().Subject;
        lookup.Type!.Name.Should().Be("dict");

        var pair = classDef.Body[2].Should().BeOfType<VariableDeclaration>().Subject;
        pair.Type!.Name.Should().Be("tuple");
    }

    #endregion

    #region Mixed Syntax Tests

    [Fact]
    public void ParseMixedSyntax_ShorthandAndCanonical()
    {
        var source = @"
def process(items: [int], lookup: dict[str, int]) -> set[str]:
    pass
";
        var module = Parse(source);
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;

        // First param uses shorthand
        func.Parameters[0].Type!.Name.Should().Be("list");
        // Second param uses canonical
        func.Parameters[1].Type!.Name.Should().Be("dict");
        // Return uses canonical
        func.ReturnType!.Name.Should().Be("set");
    }

    #endregion
}
