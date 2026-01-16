# Implementation Plan: Task 0.1.10.CG3 - Update Module Code Generation

**Task ID:** 0.1.10.CG3
**Title:** Update Module Code Generation
**Date:** 2026-01-16
**Status:** Planning

## Objective

Generate a static class for each Sharpy module file containing its exports. Specifically, ensure module-level variables generate as **static fields** in the `Exports` class, not as local variables in `Main()`.

## Problem Analysis

### Current Behavior

Based on analysis of `RoslynEmitter.cs`:

1. **Module structure is correct**: Modules generate as `public static class Exports` (line 482-486)
2. **Functions work correctly**: Generate as `MemberDeclarationSyntax` (public static methods)
3. **Classes/structs/enums work correctly**: Generate as `MemberDeclarationSyntax`
4. **Module-level variables are BROKEN**:
   - `GenerateStatement()` (line 489-504) does NOT handle `VariableDeclaration`
   - Variables fall through to the `_ => null` case
   - They get added to `executableStatements` (line 445) and wrapped in `Main()` method

### Evidence

In `GenerateStatement()` (lines 489-504):
```csharp
return stmt switch
{
    FunctionDef funcDef => GenerateFunctionDeclaration(funcDef),
    ClassDef classDef => GenerateClassDeclaration(classDef),
    // ... other types ...
    _ => null  // VariableDeclaration falls through here!
};
```

In `GenerateModuleClass()` (lines 437-446):
```csharp
var member = GenerateStatement(stmt);
if (member is MemberDeclarationSyntax memberDecl)
{
    declarations.Add(memberDecl);  // Variables never reach here
}
else
{
    executableStatements.Add(stmt);  // Variables go here instead
}
```

### Expected Behavior

A module-level variable like:
```python
# config.spy
MAX_SIZE: int = 100
```

Should generate:
```csharp
public static class Exports
{
    public static int MaxSize = 100;  // Static field
}
```

Not:
```csharp
public static class Exports
{
    public static void Main()
    {
        int maxSize = 100;  // Wrong! Local variable
    }
}
```

## Implementation Approach

### Step 1: Create `GenerateModuleLevelVariable()` Method

Add a new method to generate module-level variables as static fields:

**Location:** After `GenerateVariableDeclaration()` (around line 2009)

```csharp
private FieldDeclarationSyntax GenerateModuleLevelVariable(VariableDeclaration varDecl)
{
    // Use PascalCase for module-level exports (following Sharpy naming convention)
    var varName = varDecl.IsConst
        ? NameMangler.ToConstantCase(varDecl.Name)
        : NameMangler.ToPascalCase(varDecl.Name);

    // Determine the type
    TypeSyntax typeSyntax;
    if (varDecl.Type != null && varDecl.Type.Name == "auto")
    {
        // Can't use 'var' for fields - need to infer type
        if (varDecl.InitialValue != null)
            typeSyntax = _typeMapper.InferTypeFromExpression(varDecl.InitialValue);
        else
            typeSyntax = IdentifierName("object"); // Fallback
    }
    else if (varDecl.Type == null && varDecl.IsConst && varDecl.InitialValue != null)
    {
        typeSyntax = _typeMapper.InferTypeFromExpression(varDecl.InitialValue);
    }
    else
    {
        typeSyntax = _typeMapper.MapType(varDecl.Type);
    }

    // Create variable declarator with optional initializer
    VariableDeclaratorSyntax declarator;
    if (varDecl.InitialValue != null)
    {
        var previousTargetType = _targetTypeContext;
        _targetTypeContext = varDecl.Type;
        try
        {
            var value = GenerateExpression(varDecl.InitialValue);
            declarator = VariableDeclarator(Identifier(varName))
                .WithInitializer(EqualsValueClause(value));
        }
        finally
        {
            _targetTypeContext = previousTargetType;
        }
    }
    else
    {
        declarator = VariableDeclarator(Identifier(varName));
    }

    var declaration = VariableDeclaration(typeSyntax)
        .WithVariables(SingletonSeparatedList(declarator));

    // Build modifiers: const fields vs regular static fields
    var modifiers = varDecl.IsConst
        ? TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.ConstKeyword))
        : TokenList(
            Token(SyntaxKind.PublicKeyword),
            Token(SyntaxKind.StaticKeyword));

    return FieldDeclaration(declaration)
        .WithModifiers(modifiers);
}
```

### Step 2: Update `GenerateStatement()` to Handle Variables

**Location:** `GenerateStatement()` method (lines 489-504)

Add a case for `VariableDeclaration`:

```csharp
private SyntaxNode? GenerateStatement(Statement stmt)
{
    return stmt switch
    {
        FunctionDef funcDef => GenerateFunctionDeclaration(funcDef),
        ClassDef classDef => GenerateClassDeclaration(classDef),
        StructDef structDef => GenerateStructDeclaration(structDef),
        InterfaceDef interfaceDef => GenerateInterfaceDeclaration(interfaceDef),
        EnumDef enumDef => GenerateEnumDeclaration(enumDef),
        VariableDeclaration varDecl => GenerateModuleLevelVariable(varDecl),  // ADD THIS
        TypeAlias => null,
        ReturnStatement ret => GenerateReturn(ret),
        Assignment assign => GenerateAssignment(assign),
        _ => null
    };
}
```

### Step 3: Track Module-Level Variables in Symbol Table

**Location:** In `GenerateModuleClass()` after the const pre-scan (around line 402)

Add tracking for module-level variables so functions can reference them:

```csharp
// Also track non-const module-level variables for reference resolution
foreach (var stmt in statements)
{
    if (stmt is VariableDeclaration varDecl)
    {
        // Register in symbol tracking for use by functions
        _moduleConstVariables.Add(varDecl.Name); // Reuse this set or create a new one
    }
}
```

**Alternative:** Create a new `_moduleLevelVariables` HashSet if const/non-const need different treatment.

### Step 4: Update Variable Name Mangling for Module-Level Context

**Consideration:** The existing `GetMangledVariableName()` uses camelCase for local variables. Module-level exports should use PascalCase.

The new `GenerateModuleLevelVariable()` method handles this by using `NameMangler.ToPascalCase()` directly.

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add `GenerateModuleLevelVariable()`, update `GenerateStatement()` |

## Tests to Write/Verify

### New Unit Tests

Add to `RoslynEmitterModuleTests.cs`:

```csharp
[Fact]
public void GenerateCompilationUnit_ModuleLevelVariable_GeneratesStaticField()
{
    // Arrange
    var emitter = CreateEmitter();
    var module = new Module
    {
        Body = new List<Statement>
        {
            new VariableDeclaration
            {
                Name = "max_size",
                Type = new TypeAnnotation { Name = "int" },
                InitialValue = new IntegerLiteral { Value = 100 }
            }
        }
    };

    // Act
    var result = emitter.GenerateCompilationUnit(module);
    var code = result.ToFullString();

    // Assert
    Assert.Contains("public static int MaxSize = 100", code);
    Assert.DoesNotContain("void Main", code); // No Main needed for just a variable
}

[Fact]
public void GenerateCompilationUnit_ModuleLevelConst_GeneratesPublicConst()
{
    // Arrange
    var emitter = CreateEmitter();
    var module = new Module
    {
        Body = new List<Statement>
        {
            new VariableDeclaration
            {
                Name = "VERSION",
                Type = new TypeAnnotation { Name = "str" },
                InitialValue = new StringLiteral { Value = "1.0.0" },
                IsConst = true
            }
        }
    };

    // Act
    var result = emitter.GenerateCompilationUnit(module);
    var code = result.ToFullString();

    // Assert
    Assert.Contains("public const string VERSION = \"1.0.0\"", code);
}

[Fact]
public void GenerateCompilationUnit_ModuleLevelVariableAndFunction_BothInExports()
{
    // Arrange
    var emitter = CreateEmitter();
    var module = new Module
    {
        Body = new List<Statement>
        {
            new VariableDeclaration
            {
                Name = "counter",
                Type = new TypeAnnotation { Name = "int" },
                InitialValue = new IntegerLiteral { Value = 0 }
            },
            new FunctionDef
            {
                Name = "increment",
                Parameters = new List<Parameter>(),
                ReturnType = new TypeAnnotation { Name = "int" },
                Body = new List<Statement>
                {
                    new ReturnStatement
                    {
                        Value = new BinaryOperation
                        {
                            Left = new Identifier { Name = "counter" },
                            Operator = "+",
                            Right = new IntegerLiteral { Value = 1 }
                        }
                    }
                }
            }
        }
    };

    // Act
    var result = emitter.GenerateCompilationUnit(module);
    var code = result.ToFullString();

    // Assert
    Assert.Contains("public static int Counter = 0", code);
    Assert.Contains("public static int Increment()", code);
}

[Fact]
public void GenerateCompilationUnit_ModuleLevelVariableWithAutoType_InfersType()
{
    // Arrange
    var emitter = CreateEmitter();
    var module = new Module
    {
        Body = new List<Statement>
        {
            new VariableDeclaration
            {
                Name = "items",
                Type = new TypeAnnotation { Name = "auto" },
                InitialValue = new ListLiteral
                {
                    Elements = new List<Expression>
                    {
                        new IntegerLiteral { Value = 1 },
                        new IntegerLiteral { Value = 2 }
                    }
                }
            }
        }
    };

    // Act
    var result = emitter.GenerateCompilationUnit(module);
    var code = result.ToFullString();

    // Assert - Should infer List<int> type for the field
    Assert.Contains("public static", code);
    Assert.Contains("Items", code);
    // Field declaration should have inferred type, not 'var'
    Assert.DoesNotContain("var Items", code);
}
```

### Existing Tests to Verify

Run these to ensure no regressions:
- `RoslynEmitterModuleTests.cs` - All existing tests
- `RoslynEmitterStatementTests.cs` - Local variable tests still work
- `RoslynEmitterIntegrationTests.cs` - Full compilation works

## Potential Risks and Questions

### Risks

1. **Name collision between module-level and local variables**
   - Module uses PascalCase (`MaxSize`), locals use camelCase (`maxSize`)
   - Low risk due to different casing

2. **Reference resolution inside functions**
   - Functions need to resolve module-level variables correctly
   - May need to update `GetMangledVariableName()` to check module-level scope

3. **Const vs non-const handling**
   - C# `const` fields have restrictions (only compile-time constants)
   - `readonly static` may be needed for runtime-initialized values

4. **Auto type inference for fields**
   - C# fields can't use `var`
   - Must infer concrete type from initializer
   - Fails if no initializer provided with `auto` type

### Questions to Resolve

1. **Should module-level variables be `public static` or `public static readonly`?**
   - Current design: `public static` for mutable, `public const` for const
   - Consider: `readonly` for immutable-by-default?

2. **What about variables without initializers?**
   - `x: int` (no initializer) → `public static int X;` (default value)
   - Is this the expected behavior?

3. **How should functions reference module-level variables?**
   - Direct reference: `Counter + 1` inside function body
   - Current implementation uses `GetMangledVariableName()` which expects camelCase
   - May need adjustment for PascalCase module-level references

## Verification Steps

1. **Unit tests pass:** Run `dotnet test` on CodeGen tests
2. **Generated code compiles:** Validate with Roslyn
3. **Integration test:** Create a multi-file project with imports
4. **Manual verification:** Generate code for:
   ```python
   # config.spy
   MAX_SIZE: int = 100
   VERSION: str = "1.0.0"

   def get_max() -> int:
       return MAX_SIZE
   ```

   Should produce:
   ```csharp
   public static class Exports
   {
       public static int MaxSize = 100;
       public const string VERSION = "1.0.0";

       public static int GetMax()
       {
           return MaxSize;  // Or VERSION depending on const handling
       }
   }
   ```

## Conclusion

The implementation requires two main changes:
1. Add `GenerateModuleLevelVariable()` method that returns `FieldDeclarationSyntax`
2. Update `GenerateStatement()` to route `VariableDeclaration` to this new method

This is a focused change (~50 lines of new code) with clear test coverage requirements.
