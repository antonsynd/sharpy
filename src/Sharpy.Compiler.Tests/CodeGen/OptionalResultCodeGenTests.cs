using System.IO;
using System.Reflection;
using Xunit;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests for code generation of Optional and Result types,
/// including Some/None()/Ok/Err constructor expressions.
///
/// Codegen strategy:
/// - T? (Optional) → C# Optional&lt;T&gt; (Sharpy.Core struct)
/// - T !E (Result) → C# Result&lt;T, E&gt; (Sharpy.Core struct)
/// - Some(v) → Optional&lt;T&gt;.Some(v)
/// - None() → Optional&lt;T&gt;.None
/// - Ok(v) → Result&lt;T, E&gt;.Ok(v)
/// - Err(e) → Result&lt;T, E&gt;.Err(e)
/// </summary>
public class OptionalResultCodeGenTests
{
    private string CompileToCSharp(string sharpySource, bool isEntryPoint = false)
    {
        var logger = NullLogger.Instance;
        var lexer = new Sharpy.Compiler.Lexer.Lexer(sharpySource, logger);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, logger);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger);
        typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: isEntryPoint);

        typeChecker.Diagnostics.GetErrors().Should().BeEmpty("Sharpy source should have no type errors");

        var context = new CodeGenContext(symbolTable, builtinRegistry)
        {
            IsEntryPoint = isEntryPoint,
            SemanticInfo = semanticInfo
        };
        var emitter = new RoslynEmitter(context);
        var compilationUnit = emitter.GenerateCompilationUnit(module);

        return compilationUnit.NormalizeWhitespace().ToFullString();
    }

    #region Type Mapping

    [Fact]
    public void TypeMapping_OptionalInt_GeneratesOptionalInt()
    {
        var code = @"
x: int? = None()
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>");
    }

    [Fact]
    public void TypeMapping_ResultType_GeneratesResultGeneric()
    {
        var code = @"
x: int !str = Ok(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>");
    }

    #endregion

    #region Constructor Generation - Optional

    [Fact]
    public void Constructor_Some_GeneratesOptionalSome()
    {
        var code = @"
x: int? = Some(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>");
        csharp.Should().Contain("Optional<int>.Some(42)");
    }

    [Fact]
    public void Constructor_None_GeneratesOptionalNone()
    {
        var code = @"
x: int? = None()
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>.None");
    }

    #endregion

    #region Constructor Generation - Result

    [Fact]
    public void Constructor_Ok_GeneratesResultOk()
    {
        var code = @"
x: int !str = Ok(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>.Ok(42)");
    }

    [Fact]
    public void Constructor_Err_GeneratesResultErr()
    {
        var code = @"
x: int !str = Err(""error"")
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>.Err(");
    }

    #endregion

    #region Function Return Types

    [Fact]
    public void Function_OptionalReturn_GeneratesOptionalReturnType()
    {
        var code = @"
def get_value() -> int?:
    return Some(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>");
        csharp.Should().Contain("GetValue()");
    }

    [Fact]
    public void Function_ResultReturn_GeneratesResultReturnType()
    {
        var code = @"
def parse(s: str) -> int !str:
    return Ok(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>");
        csharp.Should().Contain("Parse(");
    }

    [Fact]
    public void Function_OptionalReturn_SomeAndNone_GeneratesBoth()
    {
        var code = @"
def maybe_double(x: int) -> int?:
    if x > 0:
        return Some(x * 2)
    return None()
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>.Some(");
        csharp.Should().Contain("Optional<int>.None");
    }

    [Fact]
    public void Function_ResultReturn_OkAndErr_GeneratesBoth()
    {
        var code = @"
def parse(s: str) -> int !str:
    if s == """":
        return Err(""empty string"")
    return Ok(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Result<int, string>.Ok(42)");
        csharp.Should().Contain("Result<int, string>.Err(");
    }

    #endregion

    #region Default Parameters

    [Fact]
    public void DefaultParam_None_GeneratesDefault()
    {
        var code = @"
def foo(x: int? = None()) -> None:
    pass
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>");
        csharp.Should().Contain("= default");
    }

    #endregion

    #region C# Compilation Verification

    private bool CompileGeneratedCSharp(string generatedCSharp, out string errors)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(generatedCSharp);

        var systemRuntimePath = Path.Combine(
            Path.GetDirectoryName(typeof(object).Assembly.Location)!,
            "System.Runtime.dll");

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(SharpyCoreReference.Location),
            MetadataReference.CreateFromFile(systemRuntimePath),
        };

        try
        {
            var netstandardAssembly = Assembly.Load("netstandard");
            references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
        }
        catch
        {
            var netstandardPath = Path.Combine(
                Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                "netstandard.dll");
            if (File.Exists(netstandardPath))
            {
                references.Add(MetadataReference.CreateFromFile(netstandardPath));
            }
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = compilation.GetDiagnostics();
        var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

        errors = string.Join("\n", errorDiagnostics.Select(d => d.ToString()));
        return errorDiagnostics.Count == 0;
    }

    [Fact]
    public void Compile_OptionalVariable_ProducesValidCSharp()
    {
        var code = @"
x: int? = Some(42)
y: int? = None()
";
        var csharp = CompileToCSharp(code);
        var success = CompileGeneratedCSharp(csharp, out var errors);
        success.Should().BeTrue($"Generated C# should compile. Errors:\n{errors}\n\nGenerated:\n{csharp}");
    }

    [Fact]
    public void Compile_ResultVariable_ProducesValidCSharp()
    {
        var code = @"
x: int !str = Ok(42)
y: int !str = Err(""error"")
";
        var csharp = CompileToCSharp(code);
        var success = CompileGeneratedCSharp(csharp, out var errors);
        success.Should().BeTrue($"Generated C# should compile. Errors:\n{errors}\n\nGenerated:\n{csharp}");
    }

    [Fact]
    public void Compile_OptionalFunction_ProducesValidCSharp()
    {
        var code = @"
def get_value(flag: bool) -> int?:
    if flag:
        return Some(42)
    return None()
";
        var csharp = CompileToCSharp(code);
        var success = CompileGeneratedCSharp(csharp, out var errors);
        success.Should().BeTrue($"Generated C# should compile. Errors:\n{errors}\n\nGenerated:\n{csharp}");
    }

    [Fact]
    public void Compile_ResultFunction_ProducesValidCSharp()
    {
        var code = @"
def parse(s: str) -> int !str:
    if s == """":
        return Err(""empty string"")
    return Ok(42)
";
        var csharp = CompileToCSharp(code);
        var success = CompileGeneratedCSharp(csharp, out var errors);
        success.Should().BeTrue($"Generated C# should compile. Errors:\n{errors}\n\nGenerated:\n{csharp}");
    }

    [Fact]
    public void Compile_DefaultNone_ProducesValidCSharp()
    {
        var code = @"
def foo(x: int? = None()) -> None:
    pass
";
        var csharp = CompileToCSharp(code);
        var success = CompileGeneratedCSharp(csharp, out var errors);
        success.Should().BeTrue($"Generated C# should compile. Errors:\n{errors}\n\nGenerated:\n{csharp}");
    }

    #endregion

    #region Property Access (is_some, is_none, is_ok, is_err)

    [Fact]
    public void PropertyAccess_IsSome_EmitsPropertyNotMethodCall()
    {
        var code = @"
def test() -> bool:
    x: int? = Some(42)
    return x.is_some()
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("x.IsSome");
        csharp.Should().NotContain("x.IsSome()");
    }

    [Fact]
    public void PropertyAccess_IsOk_EmitsPropertyNotMethodCall()
    {
        var code = @"
def test() -> bool:
    x: int !str = Ok(42)
    return x.is_ok()
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("x.IsOk");
        csharp.Should().NotContain("x.IsOk()");
    }

    #endregion

    #region Null Coalescing

    [Fact]
    public void NullCoalesce_WithOptional_GeneratesUnwrapOr()
    {
        var code = @"
def test() -> int:
    x: int? = Some(42)
    return x ?? 0
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain(".UnwrapOr(0)");
    }

    [Fact]
    public void NullCoalesce_OptionalChain_GeneratesTernary()
    {
        var code = @"
def test() -> int?:
    x: int? = Some(42)
    y: int? = None()
    return x ?? y
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain(".IsSome ? x : y");
    }

    #endregion

    #region Is None / Is Not None

    [Fact]
    public void IsNone_WithOptional_EmitsIsNoneProperty()
    {
        var code = @"
def test() -> bool:
    x: int? = Some(42)
    return x is None
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("x.IsNone");
    }

    [Fact]
    public void IsNotNone_WithOptional_EmitsIsSomeProperty()
    {
        var code = @"
def test() -> bool:
    x: int? = Some(42)
    return x is not None
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("x.IsSome");
    }

    #endregion

    #region Maybe Expression

    [Fact]
    public void MaybeExpression_GeneratesOptionalFrom()
    {
        var code = @"
def test(s: str | None) -> str?:
    return maybe s
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("global::Sharpy.Optional.From(s)");
    }

    #endregion

    #region Type Narrowing

    [Fact]
    public void TypeNarrowing_AfterIsNotNone_EmitsUnwrap()
    {
        var code = @"
def main():
    x: int? = Some(42)
    if x is not None:
        print(x + 1)
";
        var csharp = CompileToCSharp(code, isEntryPoint: true);
        csharp.Should().Contain("x.Unwrap()");
    }

    [Fact]
    public void TypeNarrowing_CompoundAnd_NarrowsBothVariables()
    {
        var code = @"
def main():
    x: int? = Some(10)
    y: int? = Some(20)
    if x is not None and y is not None:
        print(x + y)
";
        var csharp = CompileToCSharp(code, isEntryPoint: true);
        csharp.Should().Contain("x.Unwrap()");
        csharp.Should().Contain("y.Unwrap()");
    }

    [Fact]
    public void TypeNarrowing_NestedIf_PreservesOuterNarrowing()
    {
        var code = @"
def main():
    x: int? = Some(42)
    if x is not None:
        print(x + 1)
        if x is not None:
            print(x + 2)
        print(x + 3)
";
        var csharp = CompileToCSharp(code, isEntryPoint: true);
        // All three uses of x should have .Unwrap()
        var unwrapCount = System.Text.RegularExpressions.Regex.Matches(csharp, @"x\.Unwrap\(\)").Count;
        unwrapCount.Should().BeGreaterThanOrEqualTo(3, "all uses of x in narrowed scope should emit .Unwrap()");
    }

    [Fact]
    public void TypeNarrowing_Elif_NarrowsInElifBody()
    {
        var code = @"
def test(x: int?, y: int?) -> int:
    if x is not None:
        return x
    elif y is not None:
        return y
    return 0
";
        var csharp = CompileToCSharp(code);
        // Both x and y should have .Unwrap() in their respective branches
        csharp.Should().Contain("x.Unwrap()");
        csharp.Should().Contain("y.Unwrap()");
    }

    [Fact]
    public void TypeNarrowing_IsNone_NarrowsInElseBody()
    {
        var code = @"
def main():
    x: int? = Some(42)
    if x is None:
        print(0)
    else:
        print(x + 1)
";
        var csharp = CompileToCSharp(code, isEntryPoint: true);
        csharp.Should().Contain("x.Unwrap()");
    }

    #endregion

    #region Type Coercion

    [Fact]
    public void TypeCoercion_ToOptionalValueType_EmitsIsPatternWithSome()
    {
        var code = @"
def test(obj: object) -> int?:
    return obj to int?
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<int>.Some(");
        csharp.Should().Contain("default");
        csharp.Should().NotContain("(Optional<int>)null");
    }

    [Fact]
    public void TypeCoercion_ToOptionalRefType_EmitsIsPatternWithSome()
    {
        var code = @"
def test(obj: object) -> str?:
    return obj to str?
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("Optional<string>.Some(");
        csharp.Should().Contain("default");
        csharp.Should().NotContain("as string");
    }

    #endregion

    #region Null Conditional on Optional

    [Fact]
    public void NullConditional_MemberAccess_EmitsTernaryNotDotAccess()
    {
        var code = @"
class Wrapper:
    value: int

    def __init__(self, v: int):
        self.value = v

def test() -> int:
    x: Wrapper? = Some(Wrapper(42))
    return x?.value ?? 0
";
        var csharp = CompileToCSharp(code);
        // Should use ternary pattern for ?. on Optional, not C# ?.
        csharp.Should().Contain(".IsSome");
        csharp.Should().Contain(".Unwrap()");
    }

    #endregion
}
