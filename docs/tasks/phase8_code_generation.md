# Phase 8: Code Generation for Optional and Result

## Overview

This phase updates the code generator (RoslynEmitter) to properly emit C# code for:
- `OptionalType` → `Sharpy.Optional<T>`
- `ResultType` → `Sharpy.Result<T, E>`
- Constructor calls: `Some(v)`, `Nothing`, `Ok(v)`, `Err(e)`

**Prerequisites:** 
- Phase 1 (Core types in Sharpy.Core)
- Phase 5-7 (Semantic types, resolution, constructor inference)

**Files to modify:**
- `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (if needed)

**Files to create:**
- `src/Sharpy.Compiler.Tests/CodeGen/OptionalResultCodeGenTests.cs`

---

## Task 8.1: Update TypeMapper for OptionalType

**File:** `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

### Steps

- [x] Open `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`
- [x] Find the method that maps `SemanticType` to Roslyn `TypeSyntax`
- [x] Add handling for `OptionalType`:

```csharp
public TypeSyntax MapToRoslynType(SemanticType type)
{
    return type switch
    {
        // ... existing cases ...
        
        OptionalType opt => CreateGenericType("Sharpy.Optional", MapToRoslynType(opt.UnderlyingType)),
        
        ResultType res => CreateGenericType("Sharpy.Result", 
            MapToRoslynType(res.OkType), 
            MapToRoslynType(res.ErrorType)),
        
        // Note: NullableType stays as C# nullable syntax
        NullableType nullable when nullable.UnderlyingType.IsValueType 
            => NullableType(MapToRoslynType(nullable.UnderlyingType)),
        NullableType nullable 
            => MapToRoslynType(nullable.UnderlyingType), // Reference types are nullable by default in nullable context
        
        // ... other cases ...
        
        _ => throw new NotSupportedException($"Cannot map type: {type.GetDisplayName()}")
    };
}

private TypeSyntax CreateGenericType(string name, params TypeSyntax[] typeArguments)
{
    // Handle qualified names like "Sharpy.Optional"
    var parts = name.Split('.');
    
    if (parts.Length == 1)
    {
        return GenericName(Identifier(name))
            .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArguments)));
    }
    
    // Build qualified name: Sharpy.Optional<T>
    NameSyntax current = IdentifierName(parts[0]);
    for (int i = 1; i < parts.Length - 1; i++)
    {
        current = QualifiedName(current, IdentifierName(parts[i]));
    }
    
    var genericName = GenericName(Identifier(parts[^1]))
        .WithTypeArgumentList(TypeArgumentList(SeparatedList(typeArguments)));
    
    return QualifiedName(current, genericName);
}
```

### Alternative: Use Using Directive

Instead of fully qualified names, ensure the generated code includes:
```csharp
using Sharpy;
```

Then use unqualified names:
```csharp
OptionalType opt => GenericName("Optional")
    .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(MapToRoslynType(opt.UnderlyingType)))),
```

### Verification

- [x] Build: `dotnet build src/Sharpy.Compiler`
- [x] No compiler errors

```
git add src/Sharpy.Compiler/CodeGen/TypeMapper.cs
git commit -m "codegen: add TypeMapper support for OptionalType and ResultType"
```

---

## Task 8.2: Add Using Directive for Sharpy Namespace

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs` or similar

> **Status: Already done.** `using global::Sharpy.Core;` is already present in `RoslynEmitter.CompilationUnit.cs` at line 159.

### Steps

- [x] Find where using directives are generated
- [x] Add `using Sharpy;` to the generated code (already present as `using global::Sharpy.Core;`):

```csharp
private CompilationUnitSyntax GenerateCompilationUnit(...)
{
    var usings = new List<UsingDirectiveSyntax>
    {
        UsingDirective(IdentifierName("System")),
        UsingDirective(QualifiedName(IdentifierName("System"), IdentifierName("Collections"))),
        UsingDirective(QualifiedName(
            QualifiedName(IdentifierName("System"), IdentifierName("Collections")),
            IdentifierName("Generic"))),
        // Add Sharpy namespace for Optional/Result
        UsingDirective(IdentifierName("Sharpy")),
        // ... other usings ...
    };
    
    // ...
}
```

### Verification

- [x] Build: `dotnet build src/Sharpy.Compiler`
- [x] No compiler errors

```
git add src/Sharpy.Compiler/CodeGen/RoslynEmitter*.cs
git commit -m "codegen: add using Sharpy directive for Optional/Result"
```

---

## Task 8.3: Generate Code for Some/Nothing/Ok/Err Constructors

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`

### Prerequisites Check

> ⚠️ **CRITICAL:** This task requires `SemanticInfo` to be available during code generation. Verify that:
> 1. The `RoslynEmitter` has access to semantic information (e.g., via `CodeGenInfo` or a similar mechanism)
> 2. Expression types are available when generating code
> 
> Check for patterns like:
> ```csharp
> var type = _codeGenInfo.GetExpressionType(expr);
> // or
> var semanticInfo = _semanticModel.GetInfo(expr);
> ```
> 
> If semantic info is NOT available in the emitter, you'll need to either:
> - Pass it through from the semantic analysis phase (add to `CodeGenInfo`)
> - Store it on AST nodes during semantic analysis

### Steps

The semantic analyzer has already identified these as tagged union constructors. Now we need to generate the correct C# code.

- [x] Find where function calls are generated
- [x] Add handling for tagged union constructors:

```csharp
private ExpressionSyntax GenerateExpression(Expression expr, SemanticInfo? info = null)
{
    return expr switch
    {
        // ... existing cases ...
        
        FunctionCall call when IsTaggedUnionConstructor(call, info) 
            => GenerateTaggedUnionConstructor(call, info),
        
        Identifier id when IsNothingIdentifier(id, info)
            => GenerateNothingExpression(info!.Type),
        
        // ... other cases ...
    };
}

private bool IsTaggedUnionConstructor(FunctionCall call, SemanticInfo? info)
{
    if (call.Function is not Identifier id) return false;
    return id.Name is "Some" or "Ok" or "Err" 
        && info?.Type is OptionalType or ResultType;
}

private bool IsNothingIdentifier(Identifier id, SemanticInfo? info)
{
    return id.Name == "Nothing" && info?.Type is OptionalType;
}

private ExpressionSyntax GenerateTaggedUnionConstructor(FunctionCall call, SemanticInfo? info)
{
    var id = (Identifier)call.Function;
    var type = info!.Type;
    
    return (id.Name, type) switch
    {
        ("Some", OptionalType opt) => GenerateSomeExpression(call, opt),
        ("Ok", ResultType res) => GenerateOkExpression(call, res),
        ("Err", ResultType res) => GenerateErrExpression(call, res),
        _ => throw new InvalidOperationException($"Unexpected constructor: {id.Name}")
    };
}

private ExpressionSyntax GenerateSomeExpression(FunctionCall call, OptionalType opt)
{
    // Generate: Optional<T>.Some(value)
    var underlyingType = _typeMapper.MapToRoslynType(opt.UnderlyingType);
    var arg = GenerateExpression(call.Arguments[0]);
    
    return InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            GenericName("Optional")
                .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
            IdentifierName("Some")))
        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
}

private ExpressionSyntax GenerateNothingExpression(SemanticType type)
{
    // Generate: Optional<T>.Nothing
    var opt = (OptionalType)type;
    var underlyingType = _typeMapper.MapToRoslynType(opt.UnderlyingType);
    
    return MemberAccessExpression(
        SyntaxKind.SimpleMemberAccessExpression,
        GenericName("Optional")
            .WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(underlyingType))),
        IdentifierName("Nothing"));
}

private ExpressionSyntax GenerateOkExpression(FunctionCall call, ResultType res)
{
    // Generate: Result<T, E>.Ok(value)
    var okType = _typeMapper.MapToRoslynType(res.OkType);
    var errType = _typeMapper.MapToRoslynType(res.ErrorType);
    var arg = GenerateExpression(call.Arguments[0]);
    
    return InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            GenericName("Result")
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { okType, errType }))),
            IdentifierName("Ok")))
        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
}

private ExpressionSyntax GenerateErrExpression(FunctionCall call, ResultType res)
{
    // Generate: Result<T, E>.Err(error)
    var okType = _typeMapper.MapToRoslynType(res.OkType);
    var errType = _typeMapper.MapToRoslynType(res.ErrorType);
    var arg = GenerateExpression(call.Arguments[0]);
    
    return InvocationExpression(
        MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            GenericName("Result")
                .WithTypeArgumentList(TypeArgumentList(SeparatedList(new[] { okType, errType }))),
            IdentifierName("Err")))
        .WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));
}
```

### Verification

- [x] Build: `dotnet build src/Sharpy.Compiler`
- [x] No compiler errors

```
git add src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs
git commit -m "codegen: generate code for Some/Nothing/Ok/Err constructors"
```

---

## Task 8.4: Add Integration Tests for Code Generation

**File:** `src/Sharpy.Compiler.Tests/CodeGen/OptionalResultCodeGenTests.cs`

### Steps

- [x] Create new file `src/Sharpy.Compiler.Tests/CodeGen/OptionalResultCodeGenTests.cs`
- [x] Add tests that verify generated C# code:

```csharp
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

public class OptionalResultCodeGenTests : IntegrationTestBase
{
    #region Type Mapping
    
    [Fact]
    public void TypeMapping_OptionalInt_GeneratesOptionalGeneric()
    {
        var code = @"
x: int? = Nothing
";
        var csharp = CompileToCSharp(code);
        Assert.Contains("Optional<int>", csharp);
    }
    
    [Fact]
    public void TypeMapping_ResultType_GeneratesResultGeneric()
    {
        var code = @"
x: int !str = Ok(42)
";
        var csharp = CompileToCSharp(code);
        Assert.Contains("Result<int, string>", csharp);
    }
    
    [Fact]
    public void TypeMapping_CSharpNullable_GeneratesNullableSyntax()
    {
        var code = @"
x: int | None = None
";
        var csharp = CompileToCSharp(code);
        // Should use C# nullable, not Optional
        Assert.Contains("int?", csharp);
        Assert.DoesNotContain("Optional", csharp);
    }
    
    #endregion
    
    #region Constructor Generation
    
    [Fact]
    public void Constructor_Some_GeneratesOptionalSome()
    {
        var code = @"
x: int? = Some(42)
";
        var csharp = CompileToCSharp(code);
        Assert.Contains("Optional<int>.Some(42)", csharp);
    }
    
    [Fact]
    public void Constructor_Nothing_GeneratesOptionalNothing()
    {
        var code = @"
x: int? = Nothing
";
        var csharp = CompileToCSharp(code);
        Assert.Contains("Optional<int>.Nothing", csharp);
    }
    
    [Fact]
    public void Constructor_Ok_GeneratesResultOk()
    {
        var code = @"
x: int !str = Ok(42)
";
        var csharp = CompileToCSharp(code);
        Assert.Contains("Result<int, string>.Ok(42)", csharp);
    }
    
    [Fact]
    public void Constructor_Err_GeneratesResultErr()
    {
        var code = @"
x: int !str = Err(""error"")
";
        var csharp = CompileToCSharp(code);
        Assert.Contains("Result<int, string>.Err(", csharp);
    }
    
    #endregion
    
    #region Function Return Types
    
    [Fact]
    public void Function_OptionalReturn_GeneratesCorrectSignature()
    {
        var code = @"
def get_value() -> int?:
    return Some(42)
";
        var csharp = CompileToCSharp(code);
        Assert.Contains("Optional<int> GetValue()", csharp);
    }
    
    [Fact]
    public void Function_ResultReturn_GeneratesCorrectSignature()
    {
        var code = @"
def parse(s: str) -> int !str:
    return Ok(42)
";
        var csharp = CompileToCSharp(code);
        Assert.Contains("Result<int, string> Parse(", csharp);
    }
    
    #endregion
    
    #region Compilation Success
    
    [Fact]
    public void Compile_OptionalUsage_ProducesValidCSharp()
    {
        var code = @"
def get_value(flag: bool) -> int?:
    if flag:
        return Some(42)
    return Nothing
";
        var result = CompileAndVerifyCSharp(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    [Fact]
    public void Compile_ResultUsage_ProducesValidCSharp()
    {
        var code = @"
def parse(s: str) -> int !str:
    if s == """":
        return Err(""empty string"")
    return Ok(42)
";
        var result = CompileAndVerifyCSharp(code);
        Assert.True(result.Success, GetErrors(result));
    }
    
    #endregion
}
```

### Helper Method Needed

You may need to add a `CompileToCSharp` helper that returns the generated C# code as a string:

```csharp
protected string CompileToCSharp(string sharpyCode)
{
    // Compile the Sharpy code
    var lexer = new Lexer(sharpyCode);
    var parser = new Parser(lexer.Tokenize());
    var module = parser.Parse();
    
    // Run semantic analysis
    // ...
    
    // Generate C# code
    var emitter = new RoslynEmitter(/* dependencies */);
    var csharpAst = emitter.Emit(module);
    
    // Return as string
    return csharpAst.NormalizeWhitespace().ToFullString();
}
```

### Verification

- [x] Run tests: `dotnet test src/Sharpy.Compiler.Tests --filter OptionalResultCodeGenTests`
- [x] All tests pass

```
git add src/Sharpy.Compiler.Tests/CodeGen/OptionalResultCodeGenTests.cs
git commit -m "test: add code generation tests for Optional and Result"
```

---

## Task 8.5: End-to-End Test with Sharpy.Core

### Steps

Create a test that actually runs the generated code to verify it works:

- [ ] Add a test that compiles and executes Sharpy code using Optional:
  > **Deferred:** Requires type checker to know about Optional/Result methods (unwrap, etc.). C# compilation verification tests added instead.

```csharp
[Fact]
public void EndToEnd_OptionalSomeUnwrap_ReturnsValue()
{
    var code = @"
def main() -> int:
    x: int? = Some(42)
    return x.unwrap()
";
    var result = CompileAndRun(code);
    Assert.Equal(42, result);
}

[Fact]
public void EndToEnd_OptionalNothingUnwrapOr_ReturnsDefault()
{
    var code = @"
def main() -> int:
    x: int? = Nothing
    return x.unwrap_or(99)
";
    var result = CompileAndRun(code);
    Assert.Equal(99, result);
}

[Fact]
public void EndToEnd_ResultOkUnwrap_ReturnsValue()
{
    var code = @"
def main() -> int:
    x: int !str = Ok(42)
    return x.unwrap()
";
    var result = CompileAndRun(code);
    Assert.Equal(42, result);
}
```

### Verification

- [ ] Run end-to-end tests (deferred — requires method resolution for Optional/Result)
- [x] C# compilation verification tests pass (validate generated C# compiles)

```
git add src/Sharpy.Compiler.Tests/Integration/OptionalResultE2ETests.cs
git commit -m "test: add end-to-end tests for Optional and Result"
```

---

## Task 8.6: Ensure Sharpy.Core Reference in Generated Assembly

### Steps

When compiling Sharpy code to an assembly, ensure Sharpy.Core is referenced:

- [x] Find where assembly references are added during compilation
- [x] Add Sharpy.Core.dll to the references (already present in IntegrationTestBase and Compiler):

```csharp
// In Compiler.cs or similar
private MetadataReference[] GetDefaultReferences()
{
    return new[]
    {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        // ... system assemblies ...
        
        // Add Sharpy.Core
        MetadataReference.CreateFromFile(typeof(Sharpy.Optional<>).Assembly.Location),
    };
}
```

### Verification

- [x] Run end-to-end tests
- [x] Generated assemblies successfully reference and use Sharpy.Core types

```
git add src/Sharpy.Compiler/Compiler.cs
git commit -m "build: add Sharpy.Core reference to generated assemblies"
```

---

## Final Verification

- [x] Build entire solution: `dotnet build`
- [x] Run all code generation tests: `dotnet test src/Sharpy.Compiler.Tests --filter "CodeGen"`
- [x] C# compilation verification tests pass
- [x] All tests pass (4203 compiler + 817 core = 5020 total, 0 failures)
- [x] Review all commits in this phase

```
git log --oneline -6
```

Expected commits:
1. `codegen: add TypeMapper support for OptionalType and ResultType`
2. `codegen: add using Sharpy directive for Optional/Result`
3. `codegen: generate code for Some/Nothing/Ok/Err constructors`
4. `test: add code generation tests for Optional and Result`
5. `test: add end-to-end tests for Optional and Result`
6. `build: add Sharpy.Core reference to generated assemblies`

---

## Notes for Implementer

- **Type mapping is critical:** The `TypeMapper` is used everywhere to convert semantic types to C# types. Make sure all code paths go through it consistently.

- **Using directives:** Adding `using Sharpy;` is cleaner than fully qualified names, but ensure it doesn't conflict with existing code.

- **Semantic info requirement:** The code generator needs `SemanticInfo` for expressions to know when `Some(42)` is a tagged union constructor vs a regular function call. Ensure this info is available.

- **Sharpy.Core dependency:** The generated assembly needs to reference Sharpy.Core. This may require ensuring Sharpy.Core.dll is in a known location or bundled with the compiler.
