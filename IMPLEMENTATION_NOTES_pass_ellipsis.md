# Implementation Notes: `pass` and Ellipsis (`...`) Code Generation

## Summary

Implemented proper code generation for `pass` statements and ellipsis (`...`) literals according to the Sharpy language specification.

## Changes Made

### 1. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

#### Abstract Method Handling (Lines 1026-1045)
- **Modified `GenerateClassMethod`** to check for `@abstractmethod` or `@abstract` decorators
- Abstract methods now generate **no body** (semicolon only) in C#, as required by C# syntax
- Concrete methods continue to generate normal method bodies

**Before:**
```csharp
// All methods got a body, even abstract ones
var body = Block(func.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
var method = MethodDeclaration(returnType, mangledName)
    .WithBody(body);
```

**After:**
```csharp
// Check if this is an abstract method
bool isAbstract = func.Decorators.Any(d => 
    d.Name == "abstractmethod" || d.Name == "abstract");

// Generate method declaration
var method = MethodDeclaration(returnType, mangledName)
    .WithModifiers(modifiers)
    .WithParameterList(ParameterList(SeparatedList(parameters)));

// Abstract methods must not have a body in C#
if (isAbstract)
{
    method = method.WithSemicolonToken(Token(SyntaxKind.SemicolonToken));
}
else
{
    // Generate method body for concrete methods
    var body = Block(func.Body.Select(GenerateBodyStatement).OfType<StatementSyntax>());
    method = method.WithBody(body);
}
```

#### Ellipsis Literal Documentation (Lines 1847-1855)
- **Updated `GenerateEllipsisLiteral`** comment to clarify behavior
- Ellipsis in concrete methods generates `throw new NotImplementedException()`
- Ellipsis in abstract/interface methods is ignored (no body generated)

### 2. `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterDefinitionTests.cs`

Added comprehensive tests:

#### `GenerateMethod_WithAbstractMethodDecorator_GeneratesAbstractMethodWithoutBody`
- Tests that abstract methods with ellipsis generate no body
- Verifies no `NotImplementedException` is thrown
- Ensures proper `public abstract ReturnType MethodName();` syntax

#### `GenerateMethod_ConcreteMethodWithEllipsis_ThrowsNotImplementedException`
- Tests that concrete methods with ellipsis throw `NotImplementedException`
- Verifies the exception is properly generated in method body

### 3. Test Snippet: `snippets/test_abstract_ellipsis.spy`

Created comprehensive test file demonstrating:
- Abstract class with abstract methods using ellipsis
- Concrete implementation of abstract methods
- Concrete methods with ellipsis (should throw `NotImplementedException`)
- Methods with `pass` statement
- Interface methods with ellipsis

## Specification Compliance

### `pass` Statement
From `docs/language_specification/pass_statement.md`:
- ✅ Generates empty statement or empty body
- ✅ Used as placeholder for required blocks

### Ellipsis Literal (`...`)
From `docs/language_specification/ellipsis_literal.md`:
- ✅ In **abstract methods**: Generates nothing (abstract method)
- ✅ In **interface methods**: Generates nothing (abstract method)
- ✅ In **concrete methods**: Generates `throw new NotImplementedException()`

From `docs/language_specification/interfaces.md`:
- ✅ `...` (ellipsis) → abstract, no implementation
- ✅ `pass` → empty body, valid only for `-> None` methods as a default implementation

## Code Generation Examples

### Abstract Method with Ellipsis
**Sharpy:**
```python
@abstract
class Shape:
    @abstractmethod
    def area(self) -> float:
        ...
```

**Generated C#:**
```csharp
public abstract class Shape
{
    public abstract float Area();
}
```

### Concrete Method with Ellipsis
**Sharpy:**
```python
class Todo:
    def not_implemented(self) -> int:
        ...
```

**Generated C#:**
```csharp
public class Todo
{
    public int NotImplemented()
    {
        throw new System.NotImplementedException();
    }
}
```

### Interface Method with Ellipsis
**Sharpy:**
```python
interface IDrawable:
    def draw(self) -> None:
        ...
```

**Generated C#:**
```csharp
public interface IDrawable
{
    void Draw();
}
```

### Method with Pass
**Sharpy:**
```python
def empty_method() -> None:
    pass
```

**Generated C#:**
```csharp
public static void EmptyMethod()
{
    ;
}
```

## Testing

### Unit Tests
Run the new tests:
```bash
dotnet test --filter "FullyQualifiedName~GenerateMethod_WithAbstractMethodDecorator"
dotnet test --filter "FullyQualifiedName~GenerateMethod_ConcreteMethodWithEllipsis"
```

### Integration Tests
Compile the test snippet:
```bash
dotnet run --project src/Sharpy.Cli -- build snippets/test_abstract_ellipsis.spy
```

### All CodeGen Tests
```bash
dotnet test --filter "FullyQualifiedName~CodeGen"
```

## Design Decisions

1. **Abstract Method Detection**: Check decorators for `abstractmethod` or `abstract` to determine if method is abstract
2. **No Context Tracking**: Don't track whether we're in an abstract/interface context; rely on decorator presence
3. **Roslyn Syntax**: Use `.WithSemicolonToken()` for abstract methods vs `.WithBody()` for concrete methods
4. **Consistent Behavior**: Interface methods already correctly generated without body (line 1197)

## Related Files

- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` - Main implementation
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs` - `PassStatement` AST node
- `src/Sharpy.Compiler/Parser/Ast/Expression.cs` - `EllipsisLiteral` AST node
- `docs/language_specification/pass_statement.md` - Specification
- `docs/language_specification/ellipsis_literal.md` - Specification
- `docs/language_specification/function_definition.md` - Context for usage

## Status

✅ **COMPLETE** - Implementation matches specification:
- `pass` generates empty statement
- `...` in abstract/interface methods generates no body
- `...` in concrete methods generates `throw new NotImplementedException()`
