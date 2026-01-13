# Implementation Plan: R-0.1.3.2 - Make Const Type Annotation Optional

## Summary

The parser currently requires a type annotation for const declarations (`const X: int = 1`), but according to the spec, type annotation should be optional (`const X = "MyApp"`) with type inference from the initializer.

## Spec References

- **Grammar EBNF** (`docs/language_specification/grammar.ebnf.txt`, line 313):
  ```ebnf
  const_stmt ::= 'const' identifier [ ':' type_expr ] '=' expression
  ```
  The `[ ':' type_expr ]` indicates the type annotation is **optional**.

- **Statements Spec** (`docs/language_specification/statements.md`, lines 119-127):
  ```python
  const PI: float = 3.14159
  const APP_NAME = "MyApp"       # Type inferred as str
  ```

## Current Implementation

`src/Sharpy.Compiler/Parser/Parser.cs` lines 665-689:

```csharp
private VariableDeclaration ParseConstDeclaration()
{
    // ...
    Expect(TokenType.Const);
    var name = ExpectIdentifier();
    Expect(TokenType.Colon);          // ❌ BUG: Requires colon
    var type = ParseTypeAnnotation();  // ❌ BUG: Requires type
    Expect(TokenType.Assign);
    var value = ParseExpression();
    // ...
}
```

## Implementation Steps

### Step 1: Modify ParseConstDeclaration in Parser.cs

Change from requiring `: type` to making it optional:

```csharp
private VariableDeclaration ParseConstDeclaration()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;

    Expect(TokenType.Const);
    var name = ExpectIdentifier();

    // Type annotation is optional for const declarations
    TypeAnnotation? type = null;
    if (Current.Type == TokenType.Colon)
    {
        Advance();  // Skip ':'
        type = ParseTypeAnnotation();
    }

    Expect(TokenType.Assign);
    var value = ParseExpression();
    ExpectNewline();

    return new VariableDeclaration
    {
        Name = name,
        Type = type,  // May be null for type inference
        InitialValue = value,
        IsConst = true,
        LineStart = startLine,
        ColumnStart = startColumn,
        LineEnd = Current.Line,
        ColumnEnd = Current.Column
    };
}
```

### Step 2: Update AST Definition (if needed)

Check `VariableDeclaration` in `Statement.cs`:

```csharp
public record VariableDeclaration : Statement
{
    public string Name { get; init; } = "";
    public TypeAnnotation Type { get; init; } = null!;  // Already nullable via null!
    public Expression? InitialValue { get; init; }
    public bool IsConst { get; init; }
}
```

The `Type` property should be changed to `TypeAnnotation?` to properly express nullability:

```csharp
public TypeAnnotation? Type { get; init; }
```

### Step 3: Verify Downstream Handling

The downstream components already handle nullable `TypeAnnotation`:

1. **TypeResolver.ResolveTypeAnnotation** (line 25-28) - Already handles null:
   ```csharp
   public SemanticType ResolveTypeAnnotation(TypeAnnotation? annotation)
   {
       if (annotation == null)
           return SemanticType.Unknown;
   ```

2. **TypeChecker.CheckVariableDeclaration** (line 540-556) - Already handles type inference:
   ```csharp
   var declaredType = _typeResolver.ResolveTypeAnnotation(varDecl.Type);
   // ...
   if (declaredType is UnknownType)
   {
       declaredType = initType;  // Infers type from initializer
   }
   ```

3. **RoslynEmitter.GenerateVariableDeclaration** (line 1533-1539) - Needs update:
   ```csharp
   // Current: Assumes Type is not null
   if (varDecl.Type != null && varDecl.Type.Name == "auto")
   {
       typeSyntax = IdentifierName("var");
   }
   else
   {
       typeSyntax = _typeMapper.MapType(varDecl.Type);  // May fail if Type is null
   }
   ```

   Should be updated to handle null Type similar to `auto`:
   ```csharp
   if (varDecl.Type == null || varDecl.Type.Name == "auto")
   {
       typeSyntax = IdentifierName("var");
   }
   else
   {
       typeSyntax = _typeMapper.MapType(varDecl.Type);
   }
   ```

4. **GenerateField** in RoslynEmitter (line 1144-1146) - Already handles null:
   ```csharp
   TypeSyntax fieldType = varDecl.Type != null
       ? _typeMapper.MapType(varDecl.Type)
       : PredefinedType(Token(SyntaxKind.ObjectKeyword));
   ```

## Files to Modify

1. **`src/Sharpy.Compiler/Parser/Parser.cs`** (~line 665-689)
   - Make type annotation optional in `ParseConstDeclaration()`

2. **`src/Sharpy.Compiler/Parser/Ast/Statement.cs`** (line 51)
   - Change `Type` property to nullable: `public TypeAnnotation? Type { get; init; }`

3. **`src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`** (~line 1533)
   - Handle null `Type` in `GenerateVariableDeclaration()`

## Tests to Add/Verify

### Parser Tests (`src/Sharpy.Compiler.Tests/Parser/ParserTests.cs`)

Add new test case:
```csharp
[Fact]
public void ParseConstDeclarationWithoutTypeAnnotation()
{
    var module = Parse("const APP_NAME = \"MyApp\"");
    var constDecl = module.Body[0].Should().BeOfType<VariableDeclaration>().Subject;
    constDecl.Name.Should().Be("APP_NAME");
    constDecl.IsConst.Should().BeTrue();
    constDecl.Type.Should().BeNull();  // No type annotation
    constDecl.InitialValue.Should().BeOfType<StringLiteral>()
        .Which.Value.Should().Be("MyApp");
}
```

Verify existing test still passes:
```csharp
[Fact]
public void ParseConstDeclaration()  // Existing test
{
    var module = Parse("const MAX: int = 100");
    // ...
    constDecl.Type.Name.Should().Be("int");  // Type is present
}
```

### Integration Tests

Add tests for const inference in code generation:
```csharp
[Fact]
public void GenerateConstWithTypeInference()
{
    var source = "const MESSAGE = \"Hello\"";
    // Should generate: const string MESSAGE = "Hello";
}
```

### Negative Tests (Parser)

```csharp
[Fact]
public void ConstRequiresInitializer()
{
    var action = () => Parse("const X: int");
    action.Should().Throw<ParserError>();  // Missing '='
}
```

## Potential Risks

1. **Null Reference Exceptions**: If any code assumes `Type` is never null for `VariableDeclaration`, it will crash. Mitigated by checking all usages in semantic analysis and code gen.

2. **Test Breakage**: Existing tests that check `constDecl.Type.Name` will need null checks if `Type` can be null.

3. **Downstream Type Inference**: The semantic analyzer must correctly infer the type from the initializer when no type annotation is present. This is already implemented for `auto` keyword, so it should work.

## Implementation Order

1. Update `VariableDeclaration.Type` to be nullable
2. Update `ParseConstDeclaration()` to make type optional
3. Update `RoslynEmitter.GenerateVariableDeclaration()` to handle null Type
4. Add parser tests for const without type annotation
5. Run existing tests to ensure no regressions
6. Add integration tests if needed
