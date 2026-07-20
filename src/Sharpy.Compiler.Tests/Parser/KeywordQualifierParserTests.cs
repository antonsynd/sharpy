using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests for #1091: a keyword immediately followed by <c>.</c> is treated as an identifier
/// qualifier (e.g. <c>struct.pack(...)</c>) in statement, expression, and type-annotation position,
/// generalizing the former struct-only special case. Also guards the regression where the greedy
/// heuristic must NOT swallow relative imports (<c>from .mod import x</c>).
/// </summary>
public partial class ParserTests
{
    #region Keyword qualifiers (#1091)

    [Fact]
    public void KeywordQualifier_StatementLevel_ParsesAsExpressionStatement()
    {
        // `struct` is a keyword; `struct.calcsize(...)` as a bare statement must route to
        // expression parsing rather than ParseStructDef (which would throw SPY0101 on the dot).
        var module = Parse("struct.calcsize(\"i\")");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var call = exprStmt.Expression.Should().BeOfType<FunctionCall>().Subject;
        var member = call.Function.Should().BeOfType<MemberAccess>().Subject;
        member.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("struct");
        member.Member.Should().Be("calcsize");
    }

    [Fact]
    public void KeywordQualifier_ExpressionPosition_ParsesMemberAccess()
    {
        var module = Parse("y = struct.calcsize(\"i\")");
        var assign = module.Body[0].Should().BeOfType<Assignment>().Subject;
        var call = assign.Value.Should().BeOfType<FunctionCall>().Subject;
        var member = call.Function.Should().BeOfType<MemberAccess>().Subject;
        member.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("struct");
        member.Member.Should().Be("calcsize");
    }

    [Fact]
    public void KeywordQualifier_TypeAnnotation_ParsesDottedName()
    {
        var module = Parse("x: struct.Struct = make()");
        var varDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
        varDecl.Type.Name.Should().Be("struct.Struct");
    }

    [Fact]
    public void KeywordQualifier_ParameterAnnotation_ParsesDottedName()
    {
        var module = Parse("def f(e: struct.StructError) -> None:\n    pass");
        var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
        func.Parameters[0].Type!.Name.Should().Be("struct.StructError");
    }

    [Fact]
    public void KeywordQualifier_GeneralizesBeyondStruct()
    {
        // The rule is general: any keyword-then-dot is a qualifier, not just `struct`.
        var module = Parse("enum.Enum");
        var exprStmt = module.Body[0].Should().BeOfType<ExpressionStatement>().Subject;
        var member = exprStmt.Expression.Should().BeOfType<MemberAccess>().Subject;
        member.Object.Should().BeOfType<Identifier>().Which.Name.Should().Be("enum");
        member.Member.Should().Be("Enum");
    }

    [Fact]
    public void StructWithoutDot_StillParsesAsStructDefinition()
    {
        // Negative: `struct` NOT followed by `.` keeps its dedicated definition parse.
        var module = Parse("struct Point:\n    x: int\n    y: int");
        var structDef = module.Body[0].Should().BeOfType<StructDef>().Subject;
        structDef.Name.Should().Be("Point");
    }

    [Fact]
    public void RelativeImport_NotTreatedAsKeywordQualifier()
    {
        // Regression: `from .mod import x` has `from` (a keyword) followed by `.`, but it is a
        // relative import, not a `from.member` qualifier — it must still parse as an import.
        var module = Parse("from .impl import SomeClass");
        module.Body[0].Should().BeOfType<FromImportStatement>();
    }

    [Fact]
    public void RelativeImport_MultiLevel_StillParses()
    {
        var module = Parse("from ..pkg.sub import Thing");
        module.Body[0].Should().BeOfType<FromImportStatement>();
    }

    [Fact]
    public void KeywordNamedModule_StillImportable()
    {
        // A keyword may still name a module segment (`import struct`, `import match`) — the
        // exclusion only removes the import-statement delimiters, not general keywords.
        Parse("import struct").Body[0].Should().BeOfType<ImportStatement>();
        Parse("import match").Body[0].Should().BeOfType<ImportStatement>();
    }

    [Fact]
    public void FromWithMissingModule_StillReportsExpectedModuleName()
    {
        // Regression: `from import x` must NOT consume the `import` delimiter as the module name;
        // it stays an "Expected module name" error rather than "Expected Import, got Identifier".
        var error = ParseExpectingError("from import x");
        error.Should().Contain("Expected module name");
    }

    #endregion
}
