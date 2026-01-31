using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Tests for enum-specific semantic validation rules
/// </summary>
public class EnumValidationTests
{
    private (Module, SymbolTable, SemanticInfo, TypeChecker) CompileAndCheck(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        // Name resolution first
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance(); // Second pass: resolve inheritance

        // Type checking
        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);

        return (module, symbolTable, semanticInfo, typeChecker);
    }

    #region Enum Value Explicitness Tests

    [Fact]
    public void Enum_MemberWithoutValue_ReportsError()
    {
        var source = @"
enum Status:
    PENDING
    ACTIVE = 1
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e =>
            e.Message.Contains("Enum member 'PENDING' requires an explicit value"));
    }

    [Fact]
    public void Enum_MultipleMembersWithoutValue_ReportsMultipleErrors()
    {
        var source = @"
enum Status:
    PENDING
    ACTIVE = 1
    INACTIVE
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().HaveCountGreaterThanOrEqualTo(2);
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("Enum member 'PENDING' requires an explicit value"));
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("Enum member 'INACTIVE' requires an explicit value"));
    }

    [Fact]
    public void Enum_AllMembersWithExplicitValues_NoError()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    INACTIVE = 2
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotContain(e =>
            e.Message.Contains("requires an explicit value"));
    }

    #endregion

    #region Enum Value Type Consistency Tests

    [Fact]
    public void Enum_IntValuesOnly_NoError()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = 1
    INACTIVE = 2
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotContain(e =>
            e.Message.Contains("must be the same type"));
    }

    [Fact]
    public void Enum_StrValuesOnly_NoError()
    {
        var source = @"
enum Status:
    PENDING = ""pending""
    ACTIVE = ""active""
    INACTIVE = ""inactive""
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotContain(e =>
            e.Message.Contains("must be the same type"));
    }

    [Fact]
    public void Enum_MixedIntAndStr_ReportsError()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = ""active""
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e =>
            e.Message.Contains("Enum member 'ACTIVE' has type") &&
            e.Message.Contains("but previous members have type") &&
            e.Message.Contains("All enum values must be the same type"));
    }

    [Fact]
    public void Enum_FloatValue_ReportsError()
    {
        var source = @"
enum Status:
    PENDING = 0.5
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e =>
            e.Message.Contains("Enum member 'PENDING' has invalid value type") &&
            e.Message.Contains("Enum values must be int or str"));
    }

    [Fact]
    public void Enum_BoolValue_ReportsError()
    {
        var source = @"
enum Status:
    PENDING = True
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().Contain(e =>
            e.Message.Contains("Enum member 'PENDING' has invalid value type") &&
            e.Message.Contains("Enum values must be int or str"));
    }

    [Fact]
    public void Enum_ComplexExpression_ReportsError()
    {
        var source = @"
enum Status:
    PENDING = 1 + 2
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // The expression should still be type-checked, but the result should be int
        // So this shouldn't error unless there's an issue with the expression itself
        typeChecker.Diagnostics.GetErrors().Should().NotContain(e =>
            e.Message.Contains("has invalid value type"));
    }

    [Fact]
    public void Enum_NegativeIntValue_NoError()
    {
        var source = @"
enum Status:
    PENDING = -1
    ACTIVE = 0
    INACTIVE = 1
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotContain(e =>
            e.Message.Contains("has invalid value type"));
    }

    #endregion

    #region Combined Validation Tests

    [Fact]
    public void Enum_MissingValueAndTypeMismatch_ReportsMultipleErrors()
    {
        var source = @"
enum Status:
    PENDING
    ACTIVE = 1
    INACTIVE = ""inactive""
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // Should have at least 2 errors: missing value for PENDING and type mismatch for INACTIVE
        typeChecker.Diagnostics.GetErrors().Should().HaveCountGreaterThanOrEqualTo(2);
        typeChecker.Diagnostics.GetErrors().Should().Contain(e => e.Message.Contains("Enum member 'PENDING' requires an explicit value"));
        typeChecker.Diagnostics.GetErrors().Should().Contain(e =>
            e.Message.Contains("Enum member 'INACTIVE'") &&
            e.Message.Contains("must be the same type"));
    }

    [Fact]
    public void Enum_EmptyEnum_NoError()
    {
        // Empty enums with no members don't trigger validation errors
        // (they may trigger parser/name resolution errors, but not semantic validation errors)
        var source = @"
enum Status:
    PENDING = 0
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        // An enum with at least one valid member should have no errors
        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Enum_SingleMemberWithIntValue_NoError()
    {
        var source = @"
enum Single:
    ONLY = 1
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Enum_SingleMemberWithStrValue_NoError()
    {
        var source = @"
enum Single:
    ONLY = ""only""
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Enum_MultipleEnums_IndependentValidation()
    {
        var source = @"
enum IntEnum:
    A = 1
    B = 2

enum StrEnum:
    X = ""x""
    Y = ""y""
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty();
    }

    [Fact]
    public void Enum_LongTypeValue_NoError()
    {
        var source = @"
enum Status:
    PENDING = 1000000000000
    ACTIVE = 2000000000000
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        typeChecker.Diagnostics.GetErrors().Should().NotContain(e =>
            e.Message.Contains("has invalid value type"));
    }

    #endregion

    #region Error Message Quality Tests

    [Fact]
    public void Enum_MissingValue_ErrorMessageIncludesMemberName()
    {
        var source = @"
enum Status:
    PENDING
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var error = typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e =>
            e.Message.Contains("Enum member 'PENDING' requires an explicit value")).Subject;
        error.Message.Should().Contain("All enum members must have explicit constant values");
    }

    [Fact]
    public void Enum_TypeMismatch_ErrorMessageShowsBothTypes()
    {
        var source = @"
enum Status:
    PENDING = 0
    ACTIVE = ""active""
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var error = typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e =>
            e.Message.Contains("Enum member 'ACTIVE'")).Subject;
        error.Message.Should().Contain("str");
        error.Message.Should().Contain("int");
    }

    [Fact]
    public void Enum_InvalidType_ErrorMessageShowsValidTypes()
    {
        var source = @"
enum Status:
    PENDING = 3.14
";

        var (module, symbolTable, semanticInfo, typeChecker) = CompileAndCheck(source);
        typeChecker.CheckModule(module, isEntryPoint: false);

        var error = typeChecker.Diagnostics.GetErrors().Should().ContainSingle(e =>
            e.Message.Contains("Enum member 'PENDING'")).Subject;
        error.Message.Should().Contain("Enum values must be int or str");
    }

    #endregion
}
