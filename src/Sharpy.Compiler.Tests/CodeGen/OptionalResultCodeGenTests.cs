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
/// including Some/Nothing/Ok/Err constructor expressions.
///
/// Current codegen strategy:
/// - T? (Optional) → C# T? (nullable) for backward compatibility
/// - T !E (Result) → C# Result&lt;T, E&gt; (Sharpy.Core struct)
/// - Some(v) → v (raw value, compatible with T?)
/// - Nothing → null (compatible with T?)
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

        typeChecker.Errors.Should().BeEmpty("Sharpy source should have no type errors");

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
    public void TypeMapping_OptionalInt_GeneratesNullableInt()
    {
        // T? currently maps to C# int? for backward compatibility
        var code = @"
x: int? = Nothing
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("int?");
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
    public void Constructor_Some_GeneratesRawValue()
    {
        // Some(42) → just 42 (compatible with T? codegen)
        var code = @"
x: int? = Some(42)
";
        var csharp = CompileToCSharp(code);
        // Should contain int? variable with value 42, not Optional<int>.Some(42)
        csharp.Should().Contain("int?");
        csharp.Should().NotContain("Optional<int>.Some");
    }

    [Fact]
    public void Constructor_Nothing_GeneratesNull()
    {
        // Nothing → null (compatible with T? codegen)
        var code = @"
x: int? = Nothing
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("null");
        csharp.Should().NotContain("Optional<int>.Nothing");
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
    public void Function_OptionalReturn_GeneratesNullableReturnType()
    {
        var code = @"
def get_value() -> int?:
    return Some(42)
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("int?");
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
    public void Function_OptionalReturn_SomeAndNothing_GeneratesBoth()
    {
        var code = @"
def maybe_double(x: int) -> int?:
    if x > 0:
        return Some(x * 2)
    return Nothing
";
        var csharp = CompileToCSharp(code);
        // Some → raw value, Nothing → null
        csharp.Should().Contain("null");
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
    public void DefaultParam_Nothing_GeneratesNull()
    {
        // Nothing as default parameter generates null for T? compatibility
        var code = @"
def foo(x: int? = Nothing) -> None:
    pass
";
        var csharp = CompileToCSharp(code);
        csharp.Should().Contain("int?");
        csharp.Should().Contain("= null");
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
            MetadataReference.CreateFromFile(typeof(Sharpy.Core.Exports).Assembly.Location),
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
y: int? = Nothing
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
    return Nothing
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
    public void Compile_DefaultNothing_ProducesValidCSharp()
    {
        var code = @"
def foo(x: int? = Nothing) -> None:
    pass
";
        var csharp = CompileToCSharp(code);
        var success = CompileGeneratedCSharp(csharp, out var errors);
        success.Should().BeTrue($"Generated C# should compile. Errors:\n{errors}\n\nGenerated:\n{csharp}");
    }

    #endregion
}
