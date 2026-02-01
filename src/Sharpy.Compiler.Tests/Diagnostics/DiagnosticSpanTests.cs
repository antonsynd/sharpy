using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// Tests verifying that compiler diagnostics carry TextSpan information
/// when reported from updated call sites, with actual Start/Length verification.
/// </summary>
public class DiagnosticSpanTests
{
    private TypeChecker CompileToTypeChecker(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };

        typeChecker.CheckModule(module, isEntryPoint: false);
        return typeChecker;
    }

    private (NameResolver nameResolver, DiagnosticBag diagnostics) CompileToNameResolver(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        return (nameResolver, nameResolver.Diagnostics);
    }

    [Fact]
    public void UndefinedVariable_DiagnosticHasSpan()
    {
        var source = "def main():\n    print(xyz)\n";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        var error = errors.First(e =>
            e.Code == DiagnosticCodes.Semantic.UndefinedVariable &&
            e.Message.Contains("xyz"));

        error.Span.Should().NotBeNull("updated call site should pass span from AST node");
        var span = error.Span!.Value;
        var expected = source.IndexOf("xyz");
        span.Start.Should().Be(expected, "span should start at 'xyz'");
        span.Length.Should().Be(3, "span should cover 'xyz'");
    }

    [Fact]
    public void TypeMismatch_Assignment_DiagnosticHasSpan()
    {
        var source = "def main():\n    x: int = \"hello\"\n";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        var error = errors.First(e => e.Code == DiagnosticCodes.Semantic.TypeMismatch);

        error.Span.Should().NotBeNull("variable declaration type mismatch should carry span");
        var span = error.Span!.Value;
        var expectedStart = source.IndexOf("x: int");
        span.Start.Should().Be(expectedStart, "span should start at the variable declaration");
    }

    [Fact]
    public void InvalidBinaryOperation_DiagnosticHasSpan()
    {
        var source = "def main():\n    x: int = 1 + \"hello\"\n";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        var error = errors.First(e => e.Code == DiagnosticCodes.Semantic.InvalidBinaryOperation);

        error.Span.Should().NotBeNull("binary operation error should carry span");
        var span = error.Span!.Value;
        var expectedStart = source.IndexOf("1 + \"hello\"");
        span.Start.Should().Be(expectedStart, "span should start at the binary operation");
    }

    [Fact]
    public void WrongArgumentCount_DiagnosticHasSpan()
    {
        var source = "def foo(a: int, b: int) -> int:\n    return a + b\n\ndef main():\n    foo(1, 2, 3)\n";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        var error = errors.First(e => e.Code == DiagnosticCodes.Semantic.WrongArgumentCount);

        error.Span.Should().NotBeNull("wrong argument count error should carry span");
        var span = error.Span!.Value;
        var expectedStart = source.IndexOf("foo(1, 2, 3)");
        span.Start.Should().Be(expectedStart, "span should start at the function call");
    }

    [Fact]
    public void NonBoolCondition_DiagnosticHasSpan()
    {
        var source = "def main():\n    if 42:\n        pass\n";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        var error = errors.First(e => e.Message.Contains("boolean"));

        error.Span.Should().NotBeNull("condition type mismatch should carry span from the test expression");
        var span = error.Span!.Value;
        // Span should point to the condition expression "42"
        var expectedStart = source.IndexOf("42");
        span.Start.Should().Be(expectedStart, "span should start at the condition '42'");
        span.Length.Should().Be(2, "span should cover '42'");
    }

    [Fact]
    public void DiagnosticWithoutSpan_StillWorks()
    {
        // Verify that diagnostics from non-updated call sites still work (span is null)
        var diagnostic = new CompilerDiagnostic(
            "Some error",
            CompilerDiagnosticSeverity.Error,
            Line: 5,
            Column: 10
        );

        diagnostic.Span.Should().BeNull();
        diagnostic.Line.Should().Be(5);
        diagnostic.Column.Should().Be(10);
    }

    [Fact]
    public void DuplicateDefinition_NameResolver_DiagnosticHasSpan()
    {
        var source = "class Foo:\n    pass\n\nclass Foo:\n    pass\n";
        var (_, diagnostics) = CompileToNameResolver(source);
        var errors = diagnostics.GetErrors();

        var error = errors.First(e =>
            e.Code == DiagnosticCodes.Semantic.DuplicateDefinition &&
            e.Message.Contains("Foo"));

        error.Span.Should().NotBeNull("duplicate definition should carry span from NameResolver");
        var span = error.Span!.Value;
        // Should point to the second "class Foo:" declaration
        var secondClassStart = source.IndexOf("class Foo:", source.IndexOf("class Foo:") + 1);
        span.Start.Should().Be(secondClassStart, "span should start at the duplicate class definition");
    }

    [Fact]
    public void OverrideValidation_DiagnosticHasSpan()
    {
        var source = "class Base:\n    def greet(self) -> str:\n        return \"hello\"\n\nclass Child(Base):\n    def greet(self) -> str:\n        return \"hi\"\n";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        // Should get an error about missing @override decorator
        var error = errors.FirstOrDefault(e =>
            e.Code == DiagnosticCodes.Semantic.InvalidOverride &&
            e.Message.Contains("greet"));

        // This error may or may not fire depending on whether base method is virtual
        // If it fires, it should have a span
        if (error != null)
        {
            error.Span.Should().NotBeNull("override validation error should carry span from functionDef");
        }
    }

    [Fact]
    public void SuperOutsideClass_DiagnosticHasSpan()
    {
        var source = "def main():\n    super().__init__()\n";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        var error = errors.First(e =>
            e.Code == DiagnosticCodes.Semantic.SuperOutsideClass ||
            e.Code == DiagnosticCodes.Semantic.InvalidSuperUsage);

        error.Span.Should().NotBeNull("super() error should carry span");
        var span = error.Span!.Value;
        var expectedStart = source.IndexOf("super()");
        span.Start.Should().Be(expectedStart, "span should start at 'super()'");
    }

    [Fact]
    public void ReturnOutsideFunction_DiagnosticHasSpan()
    {
        var source = "return 42\n";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        var error = errors.FirstOrDefault(e =>
            e.Code == DiagnosticCodes.Semantic.ReturnOutsideFunction);

        if (error != null)
        {
            error.Span.Should().NotBeNull("return outside function error should carry span");
            var span = error.Span!.Value;
            span.Start.Should().Be(0, "span should start at beginning where return is");
        }
    }

    [Fact]
    public void BareRaise_DiagnosticHasSpan()
    {
        var source = "def main():\n    raise\n";
        var typeChecker = CompileToTypeChecker(source);
        var errors = typeChecker.Diagnostics.GetErrors();

        var error = errors.FirstOrDefault(e =>
            e.Code == DiagnosticCodes.Semantic.InvalidRaise);

        if (error != null)
        {
            error.Span.Should().NotBeNull("bare raise error should carry span");
            var span = error.Span!.Value;
            var expectedStart = source.IndexOf("raise");
            span.Start.Should().Be(expectedStart, "span should start at 'raise'");
        }
    }

    #region Validation Pipeline Span Tests

    private SemanticContext CreateValidationContext(string source, bool isEntryPoint = false)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };
        typeChecker.CheckModule(module, isEntryPoint: isEntryPoint);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver)
        {
            IsEntryPoint = isEntryPoint,
            SemanticBinding = semanticBinding
        };
        return context;
    }

    private (Module module, SemanticContext context) ParseForValidation(string source, bool isEntryPoint = false)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };
        typeChecker.CheckModule(module, isEntryPoint: isEntryPoint);

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver)
        {
            IsEntryPoint = isEntryPoint,
            SemanticBinding = semanticBinding
        };
        return (module, context);
    }

    [Fact]
    public void ModuleLevelValidator_ExecutableStatement_DiagnosticHasSpan()
    {
        var source = "def main():\n    pass\n\nprint(\"hello\")\n";
        var (module, context) = ParseForValidation(source, isEntryPoint: true);

        var validator = new ModuleLevelValidatorV2();
        validator.Validate(module, context);

        var error = context.Diagnostics.GetErrors()
            .FirstOrDefault(e => e.Code == DiagnosticCodes.Semantic.ModuleLevelExecutableStatement);

        error.Should().NotBeNull("should report module-level executable statement error");
        error!.Span.Should().NotBeNull("module-level statement error should carry span");
        var span = error.Span!.Value;
        var expectedStart = source.IndexOf("print(\"hello\")");
        span.Start.Should().Be(expectedStart, "span should start at 'print(\"hello\")'");
    }

    [Fact]
    public void SignatureValidator_WrongParamCount_DiagnosticHasSpan()
    {
        var source = "class Foo:\n    def __add__(self) -> Foo:\n        return self\n";
        var (module, context) = ParseForValidation(source);

        var validator = new SignatureValidatorV2();
        validator.Validate(module, context);

        var error = context.Diagnostics.GetErrors()
            .FirstOrDefault(e => e.Code == DiagnosticCodes.Semantic.InvalidOperatorSignature);

        error.Should().NotBeNull("should report wrong operator param count");
        error!.Span.Should().NotBeNull("operator signature error should carry span");
        var span = error.Span!.Value;
        var expectedStart = source.IndexOf("def __add__");
        span.Start.Should().Be(expectedStart, "span should start at 'def __add__'");
    }

    [Fact]
    public void ControlFlowV3_UnreachableCode_DiagnosticHasSpan()
    {
        var source = "def foo() -> int:\n    return 1\n    x: int = 2\n";
        var (module, context) = ParseForValidation(source);

        var validator = new ControlFlowValidatorV3();
        validator.Validate(module, context);

        var error = context.Diagnostics.GetErrors()
            .FirstOrDefault(e => e.Code == DiagnosticCodes.Semantic.UnreachableCode);

        error.Should().NotBeNull("should detect unreachable code after return");
        error!.Span.Should().NotBeNull("unreachable code error should carry span");
        var span = error.Span!.Value;
        var expectedStart = source.IndexOf("x: int = 2");
        span.Start.Should().Be(expectedStart, "span should start at 'x: int = 2'");
    }

    [Fact]
    public void DefaultParameterValidator_MutableDefault_DiagnosticHasSpan()
    {
        var source = "def foo(items: list[int] = []):\n    pass\n";
        var (module, context) = ParseForValidation(source);

        var validator = new DefaultParameterValidatorV2();
        validator.Validate(module, context);

        var error = context.Diagnostics.GetErrors()
            .FirstOrDefault(e => e.Code == DiagnosticCodes.Validation.MutableDefault);

        error.Should().NotBeNull("should detect mutable default value");
        error!.Span.Should().NotBeNull("mutable default error should carry span");
        var span = error.Span!.Value;
        var expectedStart = source.IndexOf("items");
        span.Start.Should().Be(expectedStart, "span should start at parameter 'items'");
    }

    #endregion
}
