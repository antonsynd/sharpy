using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Lsp.Refactoring;
using Xunit;

namespace Sharpy.Lsp.Tests.Refactoring;

public class SharplySourceGeneratorTests
{
    #region FormatTypeAnnotation

    [Fact]
    public void FormatTypeAnnotation_BuiltinInt_ReturnsInt()
    {
        var result = SharplySourceGenerator.FormatTypeAnnotation((BuiltinType)SemanticType.Int);
        result.Should().Be("int");
    }

    [Fact]
    public void FormatTypeAnnotation_BuiltinStr_ReturnsStr()
    {
        var result = SharplySourceGenerator.FormatTypeAnnotation((BuiltinType)SemanticType.Str);
        result.Should().Be("str");
    }

    [Fact]
    public void FormatTypeAnnotation_BuiltinBool_ReturnsBool()
    {
        var result = SharplySourceGenerator.FormatTypeAnnotation((BuiltinType)SemanticType.Bool);
        result.Should().Be("bool");
    }

    [Fact]
    public void FormatTypeAnnotation_BuiltinFloat_ReturnsFloat()
    {
        var result = SharplySourceGenerator.FormatTypeAnnotation((BuiltinType)SemanticType.Float);
        result.Should().Be("float");
    }

    [Fact]
    public void FormatTypeAnnotation_VoidType_ReturnsNone()
    {
        var result = SharplySourceGenerator.FormatTypeAnnotation(new VoidType());
        result.Should().Be("None");
    }

    [Fact]
    public void FormatTypeAnnotation_OptionalType_ReturnsTypeWithQuestionMark()
    {
        var optionalInt = new OptionalType { UnderlyingType = (BuiltinType)SemanticType.Int };
        var result = SharplySourceGenerator.FormatTypeAnnotation(optionalInt);
        result.Should().Be("int?");
    }

    [Fact]
    public void FormatTypeAnnotation_NullableType_ReturnsTypeWithQuestionMark()
    {
        var nullableStr = new NullableType { UnderlyingType = (BuiltinType)SemanticType.Str };
        var result = SharplySourceGenerator.FormatTypeAnnotation(nullableStr);
        result.Should().Be("str?");
    }

    [Fact]
    public void FormatTypeAnnotation_GenericType_FormatsCorrectly()
    {
        var listOfInt = new GenericType
        {
            Name = "list",
            TypeArguments = new System.Collections.Generic.List<SemanticType>
            {
                (BuiltinType)SemanticType.Int
            }
        };
        var result = SharplySourceGenerator.FormatTypeAnnotation(listOfInt);
        result.Should().Be("list[int]");
    }

    [Fact]
    public void FormatTypeAnnotation_GenericTypeMultipleArgs_FormatsCorrectly()
    {
        var dictType = new GenericType
        {
            Name = "dict",
            TypeArguments = new System.Collections.Generic.List<SemanticType>
            {
                (BuiltinType)SemanticType.Str,
                (BuiltinType)SemanticType.Int
            }
        };
        var result = SharplySourceGenerator.FormatTypeAnnotation(dictType);
        result.Should().Be("dict[str, int]");
    }

    [Fact]
    public void FormatTypeAnnotation_TupleType_FormatsCorrectly()
    {
        var tupleType = new TupleType
        {
            ElementTypes = new System.Collections.Generic.List<SemanticType>
            {
                (BuiltinType)SemanticType.Int,
                (BuiltinType)SemanticType.Str
            }
        };
        var result = SharplySourceGenerator.FormatTypeAnnotation(tupleType);
        result.Should().Be("tuple[int, str]");
    }

    [Fact]
    public void FormatTypeAnnotation_FunctionType_FormatsAsCallable()
    {
        var funcType = new FunctionType
        {
            ParameterTypes = new System.Collections.Generic.List<SemanticType>
            {
                (BuiltinType)SemanticType.Int,
                (BuiltinType)SemanticType.Str
            },
            ReturnType = (BuiltinType)SemanticType.Bool
        };
        var result = SharplySourceGenerator.FormatTypeAnnotation(funcType);
        result.Should().Be("Callable[[int, str], bool]");
    }

    [Fact]
    public void FormatTypeAnnotation_UserDefinedType_ReturnsName()
    {
        var udt = new UserDefinedType { Name = "MyClass" };
        var result = SharplySourceGenerator.FormatTypeAnnotation(udt);
        result.Should().Be("MyClass");
    }

    [Fact]
    public void FormatTypeAnnotation_TypeParameterType_ReturnsName()
    {
        var tpt = new TypeParameterType { Name = "T" };
        var result = SharplySourceGenerator.FormatTypeAnnotation(tpt);
        result.Should().Be("T");
    }

    #endregion

    #region FormatFunctionDef

    [Fact]
    public void FormatFunctionDef_NoParams_ProducesCorrectStub()
    {
        var parameters = System.Array.Empty<(string Name, SemanticType Type)>();
        var result = SharplySourceGenerator.FormatFunctionDef("do_something", parameters, null, 0);

        result.Should().Contain("def do_something():");
        result.Should().Contain("pass");
        // Should NOT contain return type annotation when returnType is null
        result.Should().NotContain("->");
    }

    [Fact]
    public void FormatFunctionDef_WithParams_ProducesCorrectStub()
    {
        var parameters = new (string Name, SemanticType Type)[]
        {
            ("x", (BuiltinType)SemanticType.Int),
            ("name", (BuiltinType)SemanticType.Str)
        };
        var result = SharplySourceGenerator.FormatFunctionDef("greet", parameters, null, 0);

        result.Should().Contain("def greet(x: int, name: str):");
        result.Should().Contain("pass");
    }

    [Fact]
    public void FormatFunctionDef_WithReturnType_IncludesArrow()
    {
        var parameters = System.Array.Empty<(string Name, SemanticType Type)>();
        var result = SharplySourceGenerator.FormatFunctionDef(
            "get_value", parameters, (BuiltinType)SemanticType.Int, 0);

        result.Should().Contain("def get_value() -> int:");
        result.Should().Contain("pass");
    }

    [Fact]
    public void FormatFunctionDef_WithVoidReturn_OmitsArrow()
    {
        var parameters = System.Array.Empty<(string Name, SemanticType Type)>();
        var result = SharplySourceGenerator.FormatFunctionDef(
            "do_thing", parameters, new VoidType(), 0);

        result.Should().NotContain("->");
    }

    [Fact]
    public void FormatFunctionDef_WithIndent_IndentsCorrectly()
    {
        var parameters = System.Array.Empty<(string Name, SemanticType Type)>();
        var result = SharplySourceGenerator.FormatFunctionDef("method", parameters, null, 1);

        result.Should().StartWith("    def method():");
        // Body should be indented 2 levels (8 spaces)
        result.Should().Contain("        pass");
    }

    [Fact]
    public void FormatFunctionDef_WithCustomBody_UsesProvidedBody()
    {
        var parameters = System.Array.Empty<(string Name, SemanticType Type)>();
        var result = SharplySourceGenerator.FormatFunctionDef(
            "custom", parameters, null, 0, body: "return 42");

        result.Should().Contain("return 42");
        result.Should().NotContain("pass");
    }

    #endregion

    #region FormatPropertyDef

    [Fact]
    public void FormatPropertyDef_GetterOnly_ProducesCorrectStub()
    {
        var result = SharplySourceGenerator.FormatPropertyDef(
            "value", (BuiltinType)SemanticType.Int, hasGetter: true, hasSetter: false, indentLevel: 1);

        result.Should().Contain("@property");
        result.Should().Contain("def value(self) -> int:");
        result.Should().Contain("raise NotImplementedError()");
        result.Should().NotContain("@value.setter");
    }

    [Fact]
    public void FormatPropertyDef_SetterOnly_ProducesCorrectStub()
    {
        var result = SharplySourceGenerator.FormatPropertyDef(
            "value", (BuiltinType)SemanticType.Int, hasGetter: false, hasSetter: true, indentLevel: 1);

        result.Should().NotContain("@property");
        result.Should().Contain("@value.setter");
        result.Should().Contain("def value(self, value: int):");
    }

    [Fact]
    public void FormatPropertyDef_GetterAndSetter_ProducesBoth()
    {
        var result = SharplySourceGenerator.FormatPropertyDef(
            "name", (BuiltinType)SemanticType.Str, hasGetter: true, hasSetter: true, indentLevel: 1);

        result.Should().Contain("@property");
        result.Should().Contain("def name(self) -> str:");
        result.Should().Contain("@name.setter");
        result.Should().Contain("def name(self, value: str):");
    }

    #endregion

    #region GetIndentation

    [Fact]
    public void GetIndentation_IndentedLine_ReturnsWhitespace()
    {
        var source = "def main():\n    print('hello')";
        var result = SharplySourceGenerator.GetIndentation(source, 1);
        result.Should().Be("    ");
    }

    [Fact]
    public void GetIndentation_NoIndent_ReturnsEmpty()
    {
        var source = "x = 1\ny = 2";
        var result = SharplySourceGenerator.GetIndentation(source, 0);
        result.Should().Be("");
    }

    [Fact]
    public void GetIndentation_OutOfRange_ReturnsEmpty()
    {
        var source = "x = 1";
        var result = SharplySourceGenerator.GetIndentation(source, 5);
        result.Should().Be("");
    }

    [Fact]
    public void GetIndentation_TabIndented_ReturnsTabs()
    {
        var source = "def main():\n\tprint('hello')";
        var result = SharplySourceGenerator.GetIndentation(source, 1);
        result.Should().Be("\t");
    }

    #endregion

    #region GetIndentUnit

    [Fact]
    public void GetIndentUnit_FourSpaces_ReturnsFourSpaces()
    {
        var source = "def main():\n    pass";
        var result = SharplySourceGenerator.GetIndentUnit(source);
        result.Should().Be("    ");
    }

    [Fact]
    public void GetIndentUnit_TwoSpaces_ReturnsTwoSpaces()
    {
        var source = "def main():\n  pass";
        var result = SharplySourceGenerator.GetIndentUnit(source);
        result.Should().Be("  ");
    }

    [Fact]
    public void GetIndentUnit_TabIndent_ReturnsTab()
    {
        var source = "def main():\n\tpass";
        var result = SharplySourceGenerator.GetIndentUnit(source);
        result.Should().Be("\t");
    }

    [Fact]
    public void GetIndentUnit_NoIndentation_DefaultsFourSpaces()
    {
        var source = "x = 1";
        var result = SharplySourceGenerator.GetIndentUnit(source);
        result.Should().Be("    ");
    }

    #endregion
}
