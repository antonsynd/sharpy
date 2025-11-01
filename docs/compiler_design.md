# Sharpy Compiler Design

## Overview

The Sharpy compiler is a transpiler that converts Sharpy source code (.spy files) to C# code, which is then compiled by the C# compiler (Roslyn) to .NET assemblies. This document describes the compiler architecture, code generation strategies, and implementation details.

For language syntax, see [Language Reference](language_reference.md).
For type system semantics, see [Type System](type_system.md).

## Architecture

### Compilation Pipeline

```
┌─────────────┐
│ .spy files  │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Lexer     │  Tokenization
│  (Rust/C#)  │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Parser    │  AST Construction
│  (Rust/C#)  │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  Semantic   │  Multi-pass Analysis
│  Analyzer   │  - Declaration pass
│  (C#)       │  - Import resolution
│             │  - Type checking
└──────┬──────┘
       │
       ▼
┌─────────────┐
│  Code Gen   │  C# Code Generation
│  (Roslyn)   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ .cs files   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Roslyn    │  C# Compilation
│  Compiler   │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│ .dll/.exe   │  .NET Assembly
└─────────────┘
```

### Project Structure

```
sharpy/
├── src/Sharpy.Compiler/          # Compiler implementation (C#)
│   ├── Lexer/                    # Tokenization
│   │   ├── Token.cs
│   │   ├── Lexer.cs
│   │   └── LexerError.cs
│   ├── Parser/                   # AST construction
│   │   └── Ast/
│   │       ├── Node.cs
│   │       ├── Statement.cs
│   │       ├── Expression.cs
│   │       └── Types.cs
│   ├── Semantic/                 # Type checking & analysis
│   │   ├── Symbol.cs
│   │   ├── Scope.cs
│   │   ├── SymbolTable.cs
│   │   ├── BuiltinRegistry.cs
│   │   └── SemanticError.cs
│   └── CodeGen/                  # C# generation
│       ├── RoslynEmitter.cs
│       ├── NameMangler.cs
│       └── CodeGenContext.cs
├── src/Sharpy.Runtime/           # Runtime library (C#)
│   ├── Builtins/                 # Built-in functions & types
│   │   ├── Exports.cs
│   │   └── Exceptions.cs
│   └── Modules/                  # Standard library modules
└── rust/                         # Rust compiler (legacy/MVP)
    └── src/
```

## Lexical Analysis (Lexer)

The lexer converts source text into a stream of tokens.

### Token Types

```csharp
public enum TokenType
{
    // Literals
    Integer, Float, String, True, False, None,

    // Identifiers and keywords
    Identifier, Keyword,

    // Operators
    Plus, Minus, Star, Slash, DoubleSlash, Percent, DoubleStar,
    Equals, DoubleEquals, NotEquals,
    Less, Greater, LessEquals, GreaterEquals,
    And, Or, Not,
    Ampersand, Pipe, Caret, Tilde, LeftShift, RightShift,
    At, Arrow, Question, DoubleQuestion, QuestionDot,

    // Delimiters
    LeftParen, RightParen, LeftBracket, RightBracket,
    LeftBrace, RightBrace, Comma, Colon, Semicolon, Dot,
    Ellipsis, Newline, Indent, Dedent,

    // Special
    EndOfFile, Error
}
```

### Indentation Handling

Sharpy uses Python-style indentation. The lexer tracks indentation levels and emits `INDENT` and `DEDENT` tokens:

```python
def foo():        # Newline
    x = 1         # INDENT
    if True:      # Newline
        y = 2     # INDENT
    return x      # DEDENT
                  # DEDENT (back to module level)
```

### Literal Name Detection

The lexer detects backtick-surrounded identifiers and marks them as literal names:

```python
# Normal identifier (subject to case conversion)
add_something()

# Literal identifier (no case conversion)
`ExactMethodName`()
```

### Access Modifier Hints

The lexer detects underscore prefixes and stores them as naming hints:

```python
_protected_name   # Hint: protected
__private_name    # Hint: private
public_name       # Hint: public (default)
```

## Syntax Analysis (Parser)

The parser constructs an Abstract Syntax Tree (AST) from the token stream.

### AST Node Hierarchy

```
Node (abstract base)
├── Statement
│   ├── FunctionDef
│   ├── ClassDef
│   ├── StructDef
│   ├── InterfaceDef
│   ├── PropertyDef
│   ├── EventDef
│   ├── MemberDef
│   ├── ImportStmt
│   ├── FromImportStmt
│   ├── IfStmt
│   ├── WhileStmt
│   ├── ForStmt
│   ├── MatchStmt
│   ├── TryStmt
│   ├── WithStmt
│   ├── AssignStmt
│   ├── ReturnStmt
│   ├── RaiseStmt
│   ├── PassStmt
│   ├── BreakStmt
│   ├── ContinueStmt
│   └── ExpressionStmt
├── Expression
│   ├── BinaryOp
│   ├── UnaryOp
│   ├── CallExpr
│   ├── AttributeAccess
│   ├── IndexAccess
│   ├── SliceExpr
│   ├── LambdaExpr
│   ├── ListExpr
│   ├── DictExpr
│   ├── SetExpr
│   ├── TupleExpr
│   ├── Identifier
│   ├── Literal
│   └── ConditionalExpr
└── TypeAnnotation
    ├── SimpleType
    ├── GenericType
    ├── OptionalType
    ├── QualifiedType
    └── UnionType
```

### Example AST

**Sharpy source**:
```python
def greet(name: str) -> str:
    return f"Hello, {name}!"
```

**AST structure**:
```
FunctionDef
├── name: "greet"
├── access_modifier: public
├── parameters:
│   └── Parameter
│       ├── name: "name"
│       └── type: SimpleType("str")
├── return_type: SimpleType("str")
└── body:
    └── ReturnStmt
        └── expression: FStringExpr
            ├── parts: ["Hello, ", name, "!"]
```

## Semantic Analysis

The semantic analyzer performs type checking, symbol resolution, and semantic validation using a multi-pass approach.

### Multi-Pass Architecture

#### Pass 1: Declaration Pass

Collects all symbol declarations without analyzing bodies:

```python
# Pass 1 sees these declarations:
def foo(): ...
class Bar: ...
x: int = ...

# But doesn't analyze:
# - Function bodies
# - Class member details
# - Expression types
```

**Purpose**: Enable forward references and resolve circular dependencies.

#### Pass 2: Import Resolution

Resolves import statements and establishes module dependencies:

```python
import math
from collections import defaultdict

# Resolves:
# - Module paths
# - Imported symbols
# - Cross-module dependencies
```

#### Pass 3: Type Checking

Performs comprehensive type analysis:

- Type inference for variables and expressions
- Function call validation (argument types, count)
- Attribute access validation
- Operator overload resolution
- Generic type instantiation

### Symbol Table

The symbol table maintains a hierarchical scope structure:

```csharp
public class SymbolTable
{
    private Scope _currentScope;
    private Stack<Scope> _scopeStack;

    public void EnterScope(ScopeType type) { ... }
    public void ExitScope() { ... }

    public void DefineSymbol(Symbol symbol) { ... }
    public Symbol? LookupSymbol(string name) { ... }
    public Symbol? LookupInCurrentScope(string name) { ... }
}

public class Scope
{
    public ScopeType Type { get; }  // Module, Class, Function, Block
    public Scope? Parent { get; }
    public Dictionary<string, Symbol> Symbols { get; }

    public Symbol? Resolve(string name) { ... }
}

public class Symbol
{
    public string Name { get; }
    public SymbolType Type { get; }  // Variable, Function, Class, etc.
    public TypeInfo? TypeInfo { get; }
    public AccessModifier Access { get; }
}
```

### Type Inference

The semantic analyzer infers types using these strategies:

**Literal inference**:
```python
x = 42          # int
y = 3.14        # float (double)
z = "hello"     # str
```

**Expression inference**:
```python
a: int = 5
b: int = 10
c = a + b       # int (from operand types)

x: float = 3.14
y = x * 2       # float (int widened to float)
```

**Function return inference**:
```python
def get_value() -> int:
    return 42

result = get_value()  # int
```

**Generic inference**:
```python
def identity[T](value: T) -> T:
    return value

x = identity(42)        # T = int
y = identity("hello")   # T = str
```

### Attribute Resolution

The analyzer resolves attribute access with proper method typing:

```python
text: str = "hello"
upper_method = text.upper    # Type: () -> str
result = text.upper()        # Type: str

numbers: list[int] = [1, 2, 3]
append_method = numbers.append  # Type: (int) -> None
```

Built-in method signatures are registered in `BuiltinRegistry`:

```csharp
public class BuiltinRegistry
{
    public void RegisterBuiltinType(string typeName, TypeInfo typeInfo) { ... }
    public void RegisterMethod(string typeName, string methodName, MethodSignature sig) { ... }

    public TypeInfo? GetBuiltinType(string name) { ... }
    public MethodSignature? GetMethod(TypeInfo type, string methodName) { ... }
}
```

## Code Generation

The code generator uses Roslyn's Syntax API to emit C# code.

### Name Mangling

Sharpy identifiers are transformed to follow C# naming conventions:

```csharp
public class NameMangler
{
    public static string MangleModuleName(string name)
    {
        // snake_case -> PascalCase
        return ToPascalCase(name);
    }

    public static string MangleFunctionName(string name)
    {
        // snake_case -> PascalCase
        return ToPascalCase(name);
    }

    public static string MangleParameterName(string name)
    {
        // snake_case -> camelCase
        return ToCamelCase(name);
    }

    public static string MangleInterfaceName(string name)
    {
        // PascalCase -> IPascalCase
        return "I" + name;
    }
}
```

**Examples**:

| Sharpy | C# | Rule |
|--------|-----|------|
| `add_numbers` | `AddNumbers` | Function |
| `user_name` | `userName` | Parameter |
| `my_module` | `MyModule` | Module |
| `Drawable` | `IDrawable` | Interface |
| `MAX_SIZE` | `MAX_SIZE` | Constant (unchanged) |

### Module Code Generation

**Sharpy module (`math.spy`)**:
```python
"""Math utilities."""

PI: float = 3.14159

def square(x: int) -> int:
    return x * x
```

**Generated C#**:
```csharp
namespace Sharpy.Modules;

/// <summary>Math utilities.</summary>
public static class Math
{
    // Module metadata
    public const string __name__ = "math";
    public const string __file__ = "/path/to/math.spy";
    public static readonly string __doc__ = "Math utilities.";

    // Module constants
    public const double PI = 3.14159;

    // Module functions
    /// <summary>Square a number.</summary>
    public static int Square(int x)
    {
        return x * x;
    }
}
```

### Class Code Generation

**Sharpy class**:
```python
class Point:
    """A 2D point."""

    def __init__(self, x: float, y: float):
        self.x = x
        self.y = y

    def distance(self) -> float:
        """Distance from origin."""
        return (self.x ** 2 + self.y ** 2) ** 0.5

    def __str__(self) -> str:
        return f"Point({self.x}, {self.y})"

    def __add__(self, other: Point) -> Point:
        return Point(self.x + other.x, self.y + other.y)
```

**Generated C#**:
```csharp
namespace Sharpy.UserTypes;

/// <summary>A 2D point.</summary>
public class Point : Sharpy.Object
{
    public double x { get; set; }
    public double y { get; set; }

    public Point(double x, double y)
    {
        this.x = x;
        this.y = y;
    }

    /// <summary>Distance from origin.</summary>
    public double Distance()
    {
        return Math.Pow(Math.Pow(this.x, 2) + Math.Pow(this.y, 2), 0.5);
    }

    public override string __str__()
    {
        return $"Point({this.x}, {this.y})";
    }

    // Dunder method
    public virtual Point __add__(Point other)
    {
        return new Point(this.x + other.x, this.y + other.y);
    }

    // Synthesized static operator
    public static Point operator +(Point left, Point right)
    {
        return left.__add__(right);
    }
}
```

### Operator Synthesis

Dunder methods automatically generate static operators:

| Dunder Method | Synthesized Operator |
|--------------|---------------------|
| `__add__(self, other)` | `operator +(T, T)` |
| `__sub__(self, other)` | `operator -(T, T)` |
| `__mul__(self, other)` | `operator *(T, T)` |
| `__div__(self, other)` | `operator /(T, T)` |
| `__eq__(self, other)` | `operator ==(T, T)`, `operator !=(T, T)` |
| `__lt__(self, other)` | `operator <(T, T)` |
| `__le__(self, other)` | `operator <=(T, T)` |
| `__gt__(self, other)` | `operator >(T, T)` |
| `__ge__(self, other)` | `operator >=(T, T)` |
| `__neg__(self)` | `operator -(T)` (unary) |
| `__invert__(self)` | `operator ~(T)` |
| `__bool__(self)` | `operator true(T)`, `operator false(T)` |

**Implementation**:
```csharp
public class OperatorSynthesizer
{
    public SyntaxNode SynthesizeOperator(MethodDeclaration dunderMethod)
    {
        var operatorToken = MapDunderToOperator(dunderMethod.Name);

        return OperatorDeclaration(operatorToken)
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword),
                                    Token(SyntaxKind.StaticKeyword)))
            .WithParameterList(dunderMethod.ParameterList)
            .WithReturnType(dunderMethod.ReturnType)
            .WithBody(Block(
                ReturnStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            IdentifierName("left"),
                            IdentifierName(dunderMethod.Name))))));
    }
}
```

### Property Code Generation

**Auto property**:
```python
class Person:
    property name: str = "Unknown"
```

**Generated C#**:
```csharp
public class Person : Sharpy.Object
{
    public string Name { get; set; } = "Unknown";
}
```

**Explicit property**:
```python
class Temperature:
    __celsius: float = 0.0

    property fahrenheit(self) -> float:
        return self.__celsius * 9/5 + 32

    property fahrenheit(self, value: float):
        self.__celsius = (value - 32) * 5/9
```

**Generated C#**:
```csharp
public class Temperature : Sharpy.Object
{
    private double __celsius = 0.0;

    public double Fahrenheit
    {
        get => this.__celsius * 9.0 / 5.0 + 32.0;
        set => this.__celsius = (value - 32.0) * 5.0 / 9.0;
    }
}
```

### Interface Code Generation

**Sharpy interface**:
```python
interface Drawable:
    """Interface for drawable objects."""

    def draw(self) -> None:
        """Draw the object."""
        ...

    def get_bounds(self) -> tuple[float, float, float, float]:
        """Get bounding box."""
        ...
```

**Generated C# interface**:
```csharp
namespace Sharpy.Interfaces;

/// <summary>Interface for drawable objects.</summary>
public interface IDrawable
{
    /// <summary>Draw the object.</summary>
    void Draw();

    /// <summary>Get bounding box.</summary>
    Tuple<double, double, double, double> GetBounds();
}
```

### Generic Code Generation

**Generic class**:
```python
class Box[T]:
    def __init__(self, value: T):
        self._value = value

    def get(self) -> T:
        return self._value
```

**Generated C#**:
```csharp
public class Box<T> : Sharpy.Object
{
    private T _value;

    public Box(T value)
    {
        this._value = value;
    }

    public T Get()
    {
        return this._value;
    }
}
```

**Generic with constraints**:
```python
def find_max[T: Comparable](items: list[T]) -> T:
    max_item = items[0]
    for item in items:
        if max_item < item:
            max_item = item
    return max_item
```

**Generated C#**:
```csharp
public static T FindMax<T>(List<T> items) where T : IComparable<T>
{
    T max_item = items[0];
    foreach (var item in items)
    {
        if (max_item.CompareTo(item) < 0)
            max_item = item;
    }
    return max_item;
}
```

### Async Code Generation

**Async function**:
```python
async def fetch_data(url: str) -> str:
    await asyncio.sleep(1.0)
    return f"Data from {url}"
```

**Generated C#**:
```csharp
public static async Task<string> FetchData(string url)
{
    await Task.Delay(TimeSpan.FromSeconds(1.0));
    return $"Data from {url}";
}
```

**Async iteration**:
```python
async def count_up(n: int):
    for i in range(n):
        await asyncio.sleep(0.1)
        yield i
```

**Generated C#**:
```csharp
public static async IAsyncEnumerable<int> CountUp(int n)
{
    for (int i = 0; i < n; i++)
    {
        await Task.Delay(TimeSpan.FromSeconds(0.1));
        yield return i;
    }
}
```

## Runtime Implementation

The Sharpy runtime library (`Sharpy.Runtime.dll`) provides built-in types and functions.

### Built-in Functions

```csharp
namespace Sharpy;

public static class Builtins
{
    public static void Print(params object?[] args)
    {
        Console.WriteLine(string.Join(" ", args));
    }

    public static int Len<T>(IEnumerable<T> collection)
    {
        return collection.Count();
    }

    public static T Min<T>(params T[] values) where T : IComparable<T>
    {
        return values.Min();
    }

    public static T Max<T>(params T[] values) where T : IComparable<T>
    {
        return values.Max();
    }

    public static IEnumerable<int> Range(int stop)
    {
        return Enumerable.Range(0, stop);
    }

    public static IEnumerable<int> Range(int start, int stop)
    {
        return Enumerable.Range(start, stop - start);
    }

    public static IEnumerable<int> Range(int start, int stop, int step)
    {
        for (int i = start; step > 0 ? i < stop : i > stop; i += step)
            yield return i;
    }
}
```

### Exception Types

```csharp
namespace Sharpy;

public class Exception : System.Exception
{
    public Exception() : base() { }
    public Exception(string message) : base(message) { }
    public Exception(string message, Exception inner) : base(message, inner) { }
}

public class ValueError : Exception
{
    public ValueError() : base() { }
    public ValueError(string message) : base(message) { }
}

public class TypeError : Exception
{
    public TypeError() : base() { }
    public TypeError(string message) : base(message) { }
}

public class IndexError : Exception
{
    public IndexError() : base() { }
    public IndexError(string message) : base(message) { }
}

public class KeyError : Exception
{
    public KeyError() : base() { }
    public KeyError(string message) : base(message) { }
}
```

## Optimization Strategies

### Zero-Cost Abstractions

Sharpy aims for zero-cost abstractions where Python semantics don't incur runtime overhead:

1. **Inline small methods**: Properties and simple methods are inlined by the JIT
2. **Struct value types**: Small immutable types use structs for stack allocation
3. **Generic specialization**: .NET JIT specializes generics per value type
4. **Static dispatch**: Virtual calls avoided when type is statically known

### Dunder Method Caching

Static operators delegate to dunder methods but are inlined by the JIT:

```csharp
// This compiles to the same IL as direct operator use
public static Point operator +(Point left, Point right)
{
    return left.__add__(right);  // Inlined by JIT
}
```

### Interface Dispatch

Interface method calls use direct interface dispatch (no reflection):

```csharp
public static void Render(IDrawable drawable)
{
    drawable.Draw();  // Direct virtual dispatch
}
```

## Error Handling

### Compile-Time Errors

The compiler reports errors with location information:

```
error[E0001]: undefined symbol 'foo'
  --> example.spy:5:10
   |
 5 |     x = foo()
   |          ^^^ undefined function
   |
```

### Error Categories

- **Lexer errors**: Invalid tokens, unterminated strings
- **Parser errors**: Syntax errors, unexpected tokens
- **Semantic errors**: Type mismatches, undefined symbols, invalid operations
- **Code generation errors**: Unsupported features, internal compiler errors

## Implementation Status

### Fully Implemented

- ✅ Lexer with indentation handling
- ✅ Parser with complete AST
- ✅ Multi-pass semantic analyzer
- ✅ Type inference
- ✅ Attribute resolution for built-in types
- ✅ Access modifier decorators
- ✅ Module system
- ✅ Class/struct/interface definitions
- ✅ Property syntax (auto and explicit)
- ✅ Generic types with constraints
- ✅ Operator synthesis from dunder methods

### Partially Implemented

- ⚠️ Code generation (structure exists, needs Rust → C# port)
- ⚠️ Runtime library (starter code, needs full builtins)
- ⚠️ Async/await (syntax recognized, codegen incomplete)
- ⚠️ Match statements (parsing complete, codegen incomplete)
- ⚠️ Events (syntax recognized, semantic analysis incomplete)

### Not Yet Implemented

- ❌ Decorators (beyond access modifiers)
- ❌ Try expressions
- ❌ Call pipelining
- ❌ Full stdlib modules
- ❌ Optimization passes

## See Also

- [Language Reference](language_reference.md) - Syntax and usage
- [Type System](type_system.md) - Type semantics and interfaces
