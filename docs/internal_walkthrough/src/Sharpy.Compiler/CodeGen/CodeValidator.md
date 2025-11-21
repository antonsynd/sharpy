# Walkthrough: CodeValidator.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/CodeValidator.cs`

---

## Overview

`CodeValidator` is a quality assurance component in the Sharpy compiler's code generation pipeline. Its primary responsibility is to **validate generated C# code before it's passed to the Roslyn compiler**. Think of it as a "sanity check" layer that catches common code generation bugs early, providing clear error messages that help compiler developers debug issues in the code generation phase.

### Role in the Compilation Pipeline

```
Sharpy Source Code
    ↓
Lexer (Tokenization)
    ↓
Parser (AST Generation)
    ↓
Semantic Analyzer (Type Checking)
    ↓
Code Generator (RoslynEmitter) → Generates C# Syntax Tree
    ↓
CodeValidator ← YOU ARE HERE (Validates the generated C# code)
    ↓
Roslyn Compiler (C# → IL)
    ↓
.NET Assembly
```

**Why validate generated code?**
- Catch code generation bugs early with specific error messages
- Avoid cryptic Roslyn compilation errors
- Ensure generated C# follows expected patterns
- Help developers debug the RoslynEmitter more easily

---

## Class Structure

### Main Class: `CodeValidator`

```csharp
public class CodeValidator
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();
    
    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;
    
    public bool Validate(SyntaxTree syntaxTree) { ... }
    // ... validation methods
}
```

**Key Design Decisions:**

1. **Mutable internal state, immutable external interface**: The validator accumulates errors and warnings in private lists but exposes them as read-only collections. This allows multiple validation passes while protecting external code from modifying the results.

2. **Stateful validation**: Each call to `Validate()` clears previous errors/warnings and starts fresh. This means you should create a new validator instance for each independent validation if you need to keep results separate.

3. **Boolean return value**: `Validate()` returns `true` if there are no errors (warnings are acceptable), making it easy to use in conditional logic.

---

## Key Methods

### 1. `Validate(SyntaxTree syntaxTree)`

**Purpose**: The main entry point for validation. Takes a Roslyn `SyntaxTree` (generated C# code) and validates it for common issues.

```csharp
public bool Validate(SyntaxTree syntaxTree)
{
    _errors.Clear();
    _warnings.Clear();

    // Check for syntax errors
    var diagnostics = syntaxTree.GetDiagnostics();
    foreach (var diagnostic in diagnostics)
    {
        if (diagnostic.Severity == DiagnosticSeverity.Error)
            _errors.Add($"Syntax error at {diagnostic.Location.GetLineSpan()}: {diagnostic.GetMessage()}");
        else if (diagnostic.Severity == DiagnosticSeverity.Warning)
            _warnings.Add($"Syntax warning at {diagnostic.Location.GetLineSpan()}: {diagnostic.GetMessage()}");
    }

    // Validate structure
    var root = syntaxTree.GetRoot();
    ValidateNode(root);

    return _errors.Count == 0;
}
```

**How it works:**

1. **Clears previous state**: Ensures each validation is independent
2. **Leverages Roslyn diagnostics**: Gets syntax errors directly from Roslyn's parser
3. **Structural validation**: Recursively walks the syntax tree to check for logical issues
4. **Returns success/failure**: `true` if no errors (warnings OK), `false` if any errors

**When to use:**
```csharp
var validator = new CodeValidator();
if (validator.Validate(generatedSyntaxTree))
{
    // Safe to proceed with compilation
    CompileWithRoslyn(generatedSyntaxTree);
}
else
{
    // Code generation bug detected!
    foreach (var error in validator.Errors)
        Console.WriteLine($"ERROR: {error}");
}
```

---

### 2. `ValidateNode(SyntaxNode node)`

**Purpose**: Recursive visitor that traverses the entire syntax tree and dispatches to specialized validators based on node type.

```csharp
private void ValidateNode(SyntaxNode node)
{
    // Check for common issues in specific node types
    switch (node)
    {
        case ClassDeclarationSyntax classDecl:
            ValidateClassDeclaration(classDecl);
            break;
        case MethodDeclarationSyntax methodDecl:
            ValidateMethodDeclaration(methodDecl);
            break;
        case VariableDeclarationSyntax varDecl:
            ValidateVariableDeclaration(varDecl);
            break;
    }

    // Recursively validate children
    foreach (var child in node.ChildNodes())
    {
        ValidateNode(child);
    }
}
```

**Design Pattern**: This is a **manual visitor pattern** implementation. Rather than using Roslyn's built-in `CSharpSyntaxWalker`, it uses pattern matching and recursion for simplicity.

**Why this approach?**
- **Selective validation**: Only validates node types that commonly have issues
- **Easy to extend**: Add new `case` branches to validate additional node types
- **Depth-first traversal**: Validates entire tree systematically

**Extensibility point**: To add validation for a new node type (e.g., `PropertyDeclarationSyntax`):

```csharp
case PropertyDeclarationSyntax propDecl:
    ValidatePropertyDeclaration(propDecl);
    break;
```

---

### 3. `ValidateClassDeclaration(ClassDeclarationSyntax classDecl)`

**Purpose**: Validates class-level constraints.

```csharp
private void ValidateClassDeclaration(ClassDeclarationSyntax classDecl)
{
    // Check for empty class name
    if (string.IsNullOrWhiteSpace(classDecl.Identifier.Text))
    {
        _errors.Add("Class declaration has empty name");
    }

    // Check for duplicate non-method members (fields, properties)
    var nonMethodMembers = classDecl.Members
        .Where(m => m is not MethodDeclarationSyntax)
        .Select(m => GetMemberName(m))
        .Where(name => name != null)
        .ToList();

    var duplicates = nonMethodMembers
        .GroupBy(name => name)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key)
        .ToList();

    foreach (var duplicate in duplicates)
    {
        _warnings.Add($"Class {classDecl.Identifier.Text} has duplicate member: {duplicate}");
    }
}
```

**What it catches:**

1. **Empty class names**: Would cause Roslyn to fail with a cryptic error
2. **Duplicate non-method members**: In C#, you can have multiple methods with the same name (overloading), but fields/properties must be unique

**Important note**: Methods are explicitly excluded from duplicate checking because C# supports method overloading:

```csharp
// Valid in C# (and thus not flagged as an error)
public void DoSomething(int x) { }
public void DoSomething(string s) { }
```

**Why warnings for duplicates?** Duplicates might indicate a code generation bug in the RoslynEmitter, but they're not always errors (Roslyn will catch truly invalid duplicates later). The warning helps developers notice potential issues.

---

### 4. `ValidateMethodDeclaration(MethodDeclarationSyntax methodDecl)`

**Purpose**: Validates method-level constraints, particularly around abstract methods and method bodies.

```csharp
private void ValidateMethodDeclaration(MethodDeclarationSyntax methodDecl)
{
    // Check for empty method name
    if (string.IsNullOrWhiteSpace(methodDecl.Identifier.Text))
    {
        _errors.Add("Method declaration has empty name");
    }

    // Check for abstract method with body
    if (methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)) &&
        methodDecl.Body != null)
    {
        _errors.Add($"Abstract method {methodDecl.Identifier.Text} cannot have a body");
    }

    // Check for non-abstract method without body or expression
    if (!methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.AbstractKeyword)) &&
        methodDecl.Body == null &&
        methodDecl.ExpressionBody == null)
    {
        // Allow interface methods and partial methods to have no body
        var parent = methodDecl.Parent;
        if (parent is not InterfaceDeclarationSyntax &&
            !methodDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            _errors.Add($"Non-abstract method {methodDecl.Identifier.Text} must have a body");
        }
    }
}
```

**What it catches:**

1. **Empty method names**: Basic validation that would cause compilation failure
2. **Abstract methods with bodies**: Illegal in C# - abstract methods cannot have implementations
3. **Missing method bodies**: Non-abstract methods must have either a block body `{ }` or expression body `=> expr`

**Special cases handled:**

- **Interface methods**: Can have no body (interface method signatures)
- **Partial methods**: Can have no body (declaration without implementation)
- **Expression-bodied methods**: `public int GetValue() => 42;` is valid

**Example of caught error:**

```csharp
// Generated C# (BUG in code generator)
public abstract void DoSomething()
{
    Console.WriteLine("Oops, abstract with body!");
}

// CodeValidator would report:
// ERROR: Abstract method DoSomething cannot have a body
```

---

### 5. `ValidateVariableDeclaration(VariableDeclarationSyntax varDecl)`

**Purpose**: Validates variable declarations, particularly around the use of `var`.

```csharp
private void ValidateVariableDeclaration(VariableDeclarationSyntax varDecl)
{
    // Check for var without initializer
    if (varDecl.Type.IsVar)
    {
        foreach (var variable in varDecl.Variables.Where(v => v.Initializer == null))
        {
            _warnings.Add($"Variable {variable.Identifier.Text} uses 'var' without initializer");
        }
    }
}
```

**What it catches:**

- **`var` without initializer**: In C#, `var x;` is illegal because the compiler cannot infer the type

**Why a warning instead of an error?** This check is somewhat defensive. In practice, Roslyn will catch this as a hard error, but the warning helps identify where the code generator might be producing questionable code.

**Example:**

```csharp
// Invalid C# (would be caught)
var x;  // ERROR: Cannot infer type

// Valid C# (not flagged)
var x = 42;  // Type inferred as int
```

---

### 6. `GetMemberName(MemberDeclarationSyntax member)`

**Purpose**: Helper method to extract the name from different member types.

```csharp
private string? GetMemberName(MemberDeclarationSyntax member)
{
    return member switch
    {
        MethodDeclarationSyntax method => method.Identifier.Text,
        PropertyDeclarationSyntax property => property.Identifier.Text,
        FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        _ => null
    };
}
```

**Why needed?** Different member types store their names differently in Roslyn's syntax tree:

- **Methods/Properties**: Direct `Identifier` property
- **Fields**: Name is in `Declaration.Variables` (a field declaration can declare multiple variables)
- **Other members**: Return `null` (not supported/not relevant)

**Pattern matching**: Uses C# switch expressions for clean, type-safe dispatching.

**Null handling**: Returns `null` for unsupported members, which is filtered out in `ValidateClassDeclaration`.

---

## Dependencies

### External Dependencies (NuGet Packages)

1. **Microsoft.CodeAnalysis.CSharp** (Roslyn)
   - `SyntaxTree`, `SyntaxNode`, `Diagnostic`
   - `ClassDeclarationSyntax`, `MethodDeclarationSyntax`, etc.
   - Provides all the syntax tree infrastructure

### Internal Dependencies (Sharpy.Compiler)

- **None directly**: `CodeValidator` is intentionally isolated and doesn't depend on other Sharpy compiler components
- **Usage**: Used by `RoslynEmitter` (or potentially other code generators) to validate their output

**Dependency Direction:**

```
RoslynEmitter (code generator)
    ↓ (generates)
SyntaxTree (C# code)
    ↓ (validates)
CodeValidator ← This class
```

---

## Patterns and Design Decisions

### 1. **Fail-Fast Error Collection**

Rather than throwing on the first error, the validator collects all errors and warnings. This provides comprehensive feedback in a single pass.

```csharp
// Collects multiple issues
_errors.Add("Issue 1");
_errors.Add("Issue 2");
_warnings.Add("Potential issue 3");

// Returns summary
return _errors.Count == 0;  // false if any errors
```

**Benefits:**
- Developers see all issues at once (better DX)
- More efficient (one tree traversal)
- Warnings don't block compilation

### 2. **Visitor Pattern (Manual Implementation)**

Uses explicit pattern matching instead of inheriting from `CSharpSyntaxWalker`.

**Advantages:**
- Simpler to understand for maintainers
- Only validates nodes that need checking
- Easy to extend with new validations

**Trade-offs:**
- Slightly more verbose than built-in walker
- Must manually implement recursion

### 3. **Separation of Concerns**

The validator focuses purely on structural validation of C# code. It does NOT:
- Generate code (RoslynEmitter's job)
- Type check Sharpy semantics (SemanticAnalyzer's job)
- Compile to IL (Roslyn's job)

### 4. **Defensive Programming**

Many checks are defensive (catching issues Roslyn would catch anyway). This is intentional:
- Provides clearer error messages during development
- Catches code generation bugs earlier in the pipeline
- Makes debugging the compiler easier

---

## Debugging Tips

### When Validation Fails

1. **Check the error messages first**: They include location information and specific issues
   ```csharp
   foreach (var error in validator.Errors)
       Console.WriteLine(error);
   ```

2. **Inspect the generated C# code**: Use a debugger to view the `SyntaxTree.ToString()` output
   ```csharp
   var generatedCode = syntaxTree.GetRoot().NormalizeWhitespace().ToFullString();
   Console.WriteLine(generatedCode);
   ```

3. **Isolate the failing node**: Set a breakpoint in the specific `Validate*` method that's adding the error

### Common Issues and Causes

| Error Message | Likely Cause | Fix Location |
|--------------|--------------|--------------|
| "Class declaration has empty name" | Bug in class name generation | Check `RoslynEmitter.Visit(ClassDef)` |
| "Abstract method X cannot have a body" | Incorrectly marking method as abstract | Check abstract modifier logic in emitter |
| "Non-abstract method X must have a body" | Missing body generation | Check method body generation in emitter |
| "Variable X uses 'var' without initializer" | Missing initializer in var declaration | Check variable declaration generation |

### Adding Debugging Output

To trace validation, add console output in `ValidateNode`:

```csharp
private void ValidateNode(SyntaxNode node)
{
    Console.WriteLine($"Validating {node.Kind()}: {node.ToString().Substring(0, Math.Min(50, node.ToString().Length))}");
    
    // ... existing validation ...
}
```

### Testing Your Changes

The validator has comprehensive tests in `src/Sharpy.Compiler.Tests/CodeGen/CodeValidatorTests.cs`. When modifying the validator:

1. Run existing tests: `dotnet test --filter "CodeValidatorTests"`
2. Add new test cases for new validation rules
3. Verify with real code generation: Compile sample Sharpy programs and check validation

---

## Contribution Guidelines

### When to Add New Validations

Add new validation rules when:

1. **Code generation bugs are hard to diagnose**: If RoslynEmitter produces invalid C# with cryptic error messages
2. **Common patterns need checking**: If a particular mistake keeps occurring in code generation
3. **Early detection helps**: If catching an issue here provides better feedback than Roslyn

**Don't add validations for:**
- Issues Roslyn already reports clearly
- Complex semantic checks (that's SemanticAnalyzer's job)
- Performance-critical paths (validation should be lightweight)

### How to Add a New Validation

**Example: Adding validation for duplicate method names in structs**

1. **Add the validation method:**

```csharp
private void ValidateStructDeclaration(StructDeclarationSyntax structDecl)
{
    // Check for duplicate method names (structs can't have overloads in our subset)
    var methodNames = structDecl.Members
        .OfType<MethodDeclarationSyntax>()
        .Select(m => m.Identifier.Text)
        .ToList();
        
    var duplicates = methodNames
        .GroupBy(name => name)
        .Where(g => g.Count() > 1)
        .Select(g => g.Key);
        
    foreach (var dup in duplicates)
    {
        _errors.Add($"Struct {structDecl.Identifier.Text} has duplicate method: {dup}");
    }
}
```

2. **Wire it into the visitor:**

```csharp
private void ValidateNode(SyntaxNode node)
{
    switch (node)
    {
        // ... existing cases ...
        case StructDeclarationSyntax structDecl:
            ValidateStructDeclaration(structDecl);
            break;
    }
    
    // ... recursion ...
}
```

3. **Add tests:**

```csharp
[Fact]
public void Validate_StructWithDuplicateMethod_ReportsError()
{
    var code = @"
public struct Point
{
    public void Move() { }
    public void Move() { }  // Duplicate!
}";
    var syntaxTree = CSharpSyntaxTree.ParseText(code);
    var validator = new CodeValidator();
    
    var result = validator.Validate(syntaxTree);
    
    Assert.False(result);
    Assert.Contains(validator.Errors, e => e.Contains("duplicate method"));
}
```

### Code Style Guidelines

- **Keep it simple**: Validation logic should be straightforward and readable
- **Use LINQ carefully**: Prefer clarity over cleverness
- **Error messages**: Include context (member name, location if possible)
- **Warnings vs Errors**: 
  - **Errors**: Definite problems that will cause compilation failure
  - **Warnings**: Suspicious patterns that might indicate bugs

### Testing Requirements

Every new validation rule MUST have tests:

1. **Positive test**: Valid code passes validation
2. **Negative test**: Invalid code fails with expected error
3. **Edge cases**: Empty inputs, null handling, etc.

### Performance Considerations

The validator is called once per generated syntax tree. Keep it lightweight:

- Avoid allocating large collections unnecessarily
- Don't perform expensive operations in loops
- Recursive traversal is O(n) in tree size - this is acceptable

---

## Example Usage Scenario

Here's how `CodeValidator` fits into the compilation pipeline:

```csharp
// In RoslynEmitter.cs (simplified)
public CompilationResult Emit(Module sharpyModule)
{
    // Generate C# syntax tree from Sharpy AST
    var csharpSyntaxTree = GenerateCSharpCode(sharpyModule);
    
    // Validate the generated code
    var validator = new CodeValidator();
    if (!validator.Validate(csharpSyntaxTree))
    {
        // Code generation bug detected!
        var errorReport = string.Join("\n", validator.Errors);
        throw new CodeGenException($"Generated invalid C# code:\n{errorReport}");
    }
    
    // If validation passes, compile with Roslyn
    var compilation = CSharpCompilation.Create(
        "GeneratedAssembly",
        new[] { csharpSyntaxTree },
        references,
        options
    );
    
    return compilation.Emit(outputStream);
}
```

**Key insight**: The validator acts as a **safety net between code generation and compilation**, catching bugs in the Sharpy compiler itself.

---

## Summary

`CodeValidator` is a focused, defensive component that:

- ✅ Validates generated C# code structure
- ✅ Catches common code generation bugs early
- ✅ Provides clear error messages for debugging
- ✅ Follows the visitor pattern for extensibility
- ✅ Maintains clean separation from other compiler phases

**For newcomers**: Think of this as a "spell checker" for the code generator. It doesn't generate code or understand Sharpy semantics - it just makes sure the generated C# makes sense before we try to compile it.

**Next steps**: To understand how code is generated, see `RoslynEmitter.md`. To understand what happens after validation, explore the Roslyn compilation in `AssemblyCompiler.cs`.
