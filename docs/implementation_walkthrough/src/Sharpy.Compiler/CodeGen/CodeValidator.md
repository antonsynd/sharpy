# Walkthrough: CodeValidator.cs

**Source File**: `src/Sharpy.Compiler/CodeGen/CodeValidator.cs`

---

## Overview

`CodeValidator` is a **quality assurance checkpoint** in the Sharpy compiler's code generation pipeline. After the `RoslynEmitter` generates C# code from the Sharpy AST, but before we hand that code to the .NET compiler, `CodeValidator` performs a lightweight sanity check to catch common structural issues and ensure the generated C# code is well-formed.

Think of it as a "pre-flight check" — it validates that the emitted C# syntax tree is structurally sound and doesn't contain obvious errors that would cause the compilation to fail later. This helps provide better error messages and catches potential bugs in the code generation phase early.

### Role in the Compilation Pipeline

```
Sharpy Source (.spy)
    ↓
Lexer → Parser → AST
    ↓
Semantic Analysis (Type Checking, Name Resolution)
    ↓
RoslynEmitter (AST → Roslyn C# Syntax Tree)
    ↓
CodeValidator ← YOU ARE HERE
    ↓
C# Compiler (Roslyn)
    ↓
.NET IL Assembly
```

The validator sits right after code generation and acts as a quality gate. If validation fails, the compiler can provide clearer error messages about what went wrong during code generation, rather than letting confusing C# compilation errors bubble up.

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
}
```

The class is designed as a **stateful validator** that accumulates errors and warnings during validation:

- **`_errors`**: Critical issues that prevent successful compilation (e.g., abstract methods with bodies)
- **`_warnings`**: Non-critical issues that might indicate code generation bugs (e.g., duplicate member names, var without initializer)

The validator is **reusable** — you can call `Validate()` multiple times on the same instance. Each call clears the previous errors/warnings and starts fresh.

---

## Key Methods

### 1. `Validate(SyntaxTree syntaxTree)` - Entry Point

```csharp
public bool Validate(SyntaxTree syntaxTree)
{
    _errors.Clear();
    _warnings.Clear();

    // Check for syntax errors from Roslyn
    var diagnostics = syntaxTree.GetDiagnostics();
    foreach (var diagnostic in diagnostics)
    {
        if (diagnostic.Severity == DiagnosticSeverity.Error)
            _errors.Add($"Syntax error at {diagnostic.Location.GetLineSpan()}: {diagnostic.GetMessage()}");
        else if (diagnostic.Severity == DiagnosticSeverity.Warning)
            _warnings.Add($"Syntax warning at {diagnostic.Location.GetLineSpan()}: {diagnostic.GetMessage()}");
    }

    // Custom structural validation
    var root = syntaxTree.GetRoot();
    ValidateNode(root);

    return _errors.Count == 0;
}
```

**What it does:**
1. **Clears previous state** - Ensures each validation is independent
2. **Collects Roslyn diagnostics** - Picks up syntax errors that Roslyn's parser detected (malformed C# code)
3. **Performs custom validation** - Walks the syntax tree looking for Sharpy-specific issues
4. **Returns validation result** - `true` means no errors (warnings are okay), `false` means compilation should stop

**Key insight:** This method does two levels of validation:
- **Roslyn-level**: Basic C# syntax errors (missing parens, braces, etc.)
- **Sharpy-level**: Domain-specific rules about how Sharpy's code generation should behave

---

### 2. `ValidateNode(SyntaxNode node)` - Recursive Tree Walker

```csharp
private void ValidateNode(SyntaxNode node)
{
    // Check specific node types
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

**What it does:**
- Uses **pattern matching** to identify different syntax node types
- Delegates to specialized validation methods based on node type
- **Recursively descends** the entire syntax tree to validate every node

**Design pattern:** This is a classic **Visitor pattern** implementation, walking the entire tree and performing type-specific operations on each node.

**Important:** The recursive descent ensures every node in the tree is validated, even deeply nested ones (e.g., methods inside nested classes).

---

### 3. `ValidateClassDeclaration(ClassDeclarationSyntax classDecl)` - Class Validation

```csharp
private void ValidateClassDeclaration(ClassDeclarationSyntax classDecl)
{
    // Check for empty class name
    if (string.IsNullOrWhiteSpace(classDecl.Identifier.Text))
    {
        _errors.Add("Class declaration has empty name");
    }

    // Check for duplicate non-method members
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

**What it validates:**
1. **Empty class names** - Should never happen if code generation is working correctly
2. **Duplicate non-method members** - Fields, properties, etc. with the same name

**Why only non-method members?**
In C#, method overloading is perfectly valid:
```csharp
public void DoSomething() { }
public void DoSomething(int x) { }  // Valid overload
```

But duplicate fields or properties are always errors:
```csharp
public int Value;
public int Value;  // Invalid - duplicate field
```

**Note:** Duplicate members are added as **warnings**, not errors, because Roslyn will catch them anyway. The warning helps Sharpy developers debug code generation issues.

---

### 4. `ValidateMethodDeclaration(MethodDeclarationSyntax methodDecl)` - Method Validation

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

**What it validates:**

1. **Empty method names** - Catches potential code generation bugs where method names aren't properly set

2. **Abstract methods with bodies** - Classic C# error:
   ```csharp
   public abstract void Process()  // Abstract
   {
       Console.WriteLine("This is invalid!");  // Cannot have body
   }
   ```

3. **Non-abstract methods without bodies** - Regular methods must have either:
   - A block body: `{ ... }`
   - An expression body: `=> expression`
   
   **Exceptions:**
   - Interface methods (they're declarations only)
   - Partial methods (the implementation might be in another partial class)

**Important detail:** The validator checks for **both `Body` and `ExpressionBody`** because C# supports two syntaxes:
```csharp
public int GetValue() { return 42; }      // Block body
public int GetValue() => 42;               // Expression body
```

---

### 5. `ValidateVariableDeclaration(VariableDeclarationSyntax varDecl)` - Variable Validation

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

**What it validates:**
Using `var` without an initializer in C#:
```csharp
var x;  // Invalid - compiler can't infer type without initializer
```

**Why is this a warning?**
This is actually a C# syntax error that Roslyn will catch, but the custom warning provides **Sharpy-specific context** about where in the code generation pipeline this problem originated.

**Edge case:** Multiple variable declarations:
```csharp
var x = 1, y;  // y has no initializer - warning
```

The loop handles this by checking each variable declarator individually.

---

### 6. `GetMemberName(MemberDeclarationSyntax member)` - Helper Method

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

**What it does:**
Extracts the name from different types of class members using C# pattern matching.

**Why is FieldDeclarationSyntax more complex?**
Fields in C# can declare multiple variables at once:
```csharp
public int x, y, z;  // One FieldDeclarationSyntax with 3 variables
```

The code takes the **first variable's name** as the representative name for duplicate checking. This is a simplification — in practice, duplicate field names would be caught at the syntax level by Roslyn anyway.

---

## Dependencies

### External Dependencies

1. **Microsoft.CodeAnalysis.CSharp** - Roslyn C# compiler APIs
   - `SyntaxTree` - Represents the parsed C# code
   - `SyntaxNode` - Base class for all syntax nodes
   - Various specific syntax types: `ClassDeclarationSyntax`, `MethodDeclarationSyntax`, etc.

2. **System.Linq** - Used for collection operations (GroupBy, Where, Select)

### Internal Dependencies

**None!** This is a key design feature. `CodeValidator` is **completely independent** of:
- The Sharpy AST (`Parser/Ast/*`)
- The semantic analysis phase (`Semantic/*`)
- The code generation context (`CodeGenContext`)

This isolation means the validator can be:
- **Tested independently** without setting up the entire compiler pipeline
- **Reused** in other contexts where you need to validate generated C# code
- **Maintained separately** from the rest of the code generation logic

---

## Patterns and Design Decisions

### 1. **Separation of Concerns**

The validator doesn't know anything about Sharpy — it only validates C# syntax trees. This is intentional:
- **Single Responsibility**: Only concerned with C# code correctness
- **Testability**: Can test with hand-written C# code, no Sharpy needed
- **Maintainability**: Changes to Sharpy semantics don't affect validation

### 2. **Accumulator Pattern**

Errors and warnings are accumulated in lists rather than failing fast:
```csharp
// Collects ALL errors, not just the first one
foreach (var duplicate in duplicates)
{
    _warnings.Add($"Class {classDecl.Identifier.Text} has duplicate member: {duplicate}");
}
```

**Benefit:** Users see all problems at once, not one at a time. This speeds up debugging.

### 3. **Visitor Pattern**

`ValidateNode()` implements a tree visitor that:
- Walks the entire syntax tree recursively
- Dispatches to specialized validation methods based on node type
- Handles the tree structure generically

This is a **textbook implementation** of the Visitor pattern from the Gang of Four.

### 4. **Clear Separation: Errors vs. Warnings**

The validator distinguishes between:
- **Errors**: Things that will definitely break compilation (abstract methods with bodies)
- **Warnings**: Things that might indicate bugs but won't necessarily break (duplicate members)

The `Validate()` method returns `true` only if there are **no errors** (warnings are acceptable).

### 5. **State Management**

The validator maintains state (`_errors`, `_warnings`) but **clears it** at the start of each validation:
```csharp
public bool Validate(SyntaxTree syntaxTree)
{
    _errors.Clear();
    _warnings.Clear();
    // ...
}
```

This makes the validator **reusable** — the same instance can validate multiple syntax trees without interference.

### 6. **Defensive Programming**

Notice the null checks and optional handling:
```csharp
private string? GetMemberName(MemberDeclarationSyntax member)
{
    return member switch
    {
        // ...
        FieldDeclarationSyntax field => field.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        _ => null  // Unknown member type - return null gracefully
    };
}
```

The code **never throws** on unexpected input — it returns null or skips validation, letting Roslyn catch the issue later.

---

## Debugging Tips

### When to Suspect CodeValidator Issues

1. **False positives**: The validator reports errors, but the C# code is actually valid
   - Check if the validation rules are too strict
   - Look at the specific validation method flagging the issue

2. **False negatives**: Invalid C# code passes validation
   - The validator is intentionally lightweight — it doesn't catch everything
   - Roslyn will catch most issues in the next phase

3. **Misleading error messages**: The error text doesn't match the actual problem
   - Look at the string formatting in the error/warning messages
   - Check if the location info (`diagnostic.Location.GetLineSpan()`) is helpful

### Debugging Techniques

**Add temporary logging:**
```csharp
private void ValidateNode(SyntaxNode node)
{
    Console.WriteLine($"Validating {node.GetType().Name} at {node.GetLocation()}");
    // ... rest of method
}
```

**Dump the syntax tree:**
```csharp
var syntaxTree = /* ... */;
Console.WriteLine(syntaxTree.GetRoot().ToFullString());  // Shows the C# code
```

**Check specific node types:**
```csharp
// In a debugger or test, inspect what node types are present
var allNodeTypes = syntaxTree.GetRoot()
    .DescendantNodes()
    .Select(n => n.GetType().Name)
    .Distinct()
    .ToList();
```

**Test in isolation:**
The validator can be tested with just a C# string:
```csharp
var code = @"public class Test { public void Method() { } }";
var tree = CSharpSyntaxTree.ParseText(code);
var validator = new CodeValidator();
var result = validator.Validate(tree);
```

### Common Issues

**Issue**: "Why isn't my validation rule being applied?"

**Possible causes:**
1. The node type you're checking isn't in the `switch` statement in `ValidateNode()`
2. The recursive descent isn't reaching your node (check parent-child relationships)
3. The validation logic has a bug (add logging to verify it's executing)

**Issue**: "Validation reports errors but C# compiles fine"

**Possible causes:**
1. Validation rules are outdated (C# evolved, validator didn't)
2. Edge cases not handled correctly (e.g., expression-bodied members)

**Issue**: "Validation passes but C# compilation fails"

**Expected behavior:** The validator is **intentionally incomplete**. It only checks for:
- Common code generation bugs (empty names, abstract methods with bodies)
- Structural issues (duplicate members)

It doesn't validate:
- Type correctness (that's Roslyn's job)
- Name resolution (also Roslyn's job)
- Complex semantic rules (too expensive to duplicate)

---

## Contribution Guidelines

### When to Add New Validation Rules

Add a new validation rule when:
1. **Code generation bug pattern emerges** - You keep generating invalid C# in the same way
2. **Better error messages are needed** - Roslyn's error is confusing, you can provide context
3. **Common mistakes in code generation** - Things that waste debugging time

**Don't add validation for:**
- Things Roslyn already validates well
- Complex semantic rules (too expensive, duplicate effort)
- Sharpy-specific semantics (those should be in `TypeChecker`)

### How to Add a New Validation Rule

**Example:** Let's add validation for properties without getters or setters:

```csharp
// Add to the switch in ValidateNode()
case PropertyDeclarationSyntax propertyDecl:
    ValidatePropertyDeclaration(propertyDecl);
    break;

// Add new validation method
private void ValidatePropertyDeclaration(PropertyDeclarationSyntax propertyDecl)
{
    // Check for property with no accessors
    if (propertyDecl.AccessorList == null && propertyDecl.ExpressionBody == null)
    {
        _errors.Add($"Property {propertyDecl.Identifier.Text} must have accessors or expression body");
    }
    
    // Check for auto-property with conflicting accessors
    var accessors = propertyDecl.AccessorList?.Accessors ?? default;
    var hasGet = accessors.Any(a => a.IsKind(SyntaxKind.GetAccessorDeclaration));
    var hasSet = accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
    
    if (!hasGet && !hasSet && propertyDecl.ExpressionBody == null)
    {
        _warnings.Add($"Property {propertyDecl.Identifier.Text} has no get or set accessor");
    }
}
```

**Always add corresponding tests:**
```csharp
[Fact]
public void Validate_PropertyWithNoAccessors_AddsError()
{
    var code = @"
public class Test
{
    public int Value  // Invalid - no getter/setter
}";
    var tree = CSharpSyntaxTree.ParseText(code);
    var validator = new CodeValidator();
    
    var result = validator.Validate(tree);
    
    result.Should().BeFalse();
    validator.Errors.Should().Contain(e => e.Contains("must have accessors"));
}
```

### Code Style Guidelines

1. **Keep validation methods focused** - One concern per method
2. **Use descriptive error messages** - Include context (method name, class name)
3. **Prefer warnings over errors** - Only use errors for things that will definitely break
4. **Handle nulls gracefully** - Use null-conditional operators (`?.`)
5. **Document complex logic** - Add comments explaining why a rule exists

### Testing Guidelines

**Every validation rule must have:**
1. **Positive test** - Valid code passes validation
2. **Negative test** - Invalid code fails validation
3. **Edge case tests** - Empty inputs, null cases, etc.

**Test structure:**
```csharp
[Fact]
public void Validate_<Scenario>_<ExpectedOutcome>()
{
    // Arrange - Create test C# code
    var code = @"...";
    var tree = CSharpSyntaxTree.ParseText(code);
    var validator = new CodeValidator();
    
    // Act
    var result = validator.Validate(tree);
    
    // Assert
    result.Should().Be<expected>;
    validator.Errors.Should().<assertion>;
}
```

### Performance Considerations

The validator walks **every node** in the syntax tree, so:
- Keep validation methods fast (no expensive operations)
- Don't perform redundant checks (Roslyn will catch them anyway)
- Avoid allocations in hot paths (the recursive descent)

**Current performance characteristics:**
- Time complexity: O(n) where n = number of nodes in syntax tree
- Space complexity: O(d + e + w) where d = tree depth, e = error count, w = warning count
- For typical Sharpy programs: < 1ms validation time

---

## Related Files

### Testing
- **`src/Sharpy.Compiler.Tests/CodeGen/CodeValidatorTests.cs`** - Comprehensive test suite with examples of all validation rules

### Code Generation Pipeline
- **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`** - Generates the C# syntax trees that CodeValidator validates
- **`src/Sharpy.Compiler/CodeGen/TypeMapper.cs`** - Maps Sharpy types to C# types
- **`src/Sharpy.Compiler/CodeGen/NameMangler.cs`** - Converts Python-style names to C# conventions

### Error Handling
- **`src/Sharpy.Compiler/CodeGen/CodeGenException.cs`** - Exception type for code generation errors (not used by validator)

---

## Future Enhancements

Potential improvements to `CodeValidator`:

1. **Async/await validation** - Check that async methods return Task/Task<T>
2. **Nullable reference type checks** - Warn about potential null reference issues
3. **Attribute validation** - Check for malformed attributes
4. **Expression validation** - Validate lambda expressions, LINQ queries
5. **Accessibility validation** - Check for inconsistent accessibility modifiers
6. **Configuration** - Allow enabling/disabling specific validation rules

**Note:** Before adding complexity, consider whether Roslyn already handles it. The goal is to provide **early, Sharpy-specific** feedback, not to replicate the entire C# compiler.

---

## Summary

`CodeValidator` is a **lightweight quality gate** that validates generated C# syntax trees before compilation. It:

- ✅ Catches common code generation bugs early
- ✅ Provides better error messages than raw Roslyn errors
- ✅ Is independent of Sharpy semantics (pure C# validation)
- ✅ Is fully tested and easy to extend
- ✅ Fails fast with clear error messages

**Key takeaway**: Think of `CodeValidator` as a **sanity check**, not a full C# semantic analyzer. It's there to help Sharpy developers debug code generation issues, not to replace the Roslyn compiler.

When in doubt, ask: "Would this validation help me debug a code generation bug faster?" If yes, add it. If Roslyn already gives a clear error message, skip it.
