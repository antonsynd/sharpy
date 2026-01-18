# Walkthrough: Parser.Definitions.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.Definitions.cs`

---

## Overview

This file contains the definition-parsing portion of the Sharpy parser, implemented as a partial class of `Parser`. It handles the parsing of top-level constructs and complex statements including:

- **Type definitions**: Classes, structs, interfaces, enums
- **Function definitions**: Methods and standalone functions with generic type parameters
- **Variable declarations**: Both typed declarations and const declarations
- **Type aliases**: Including function type signatures
- **Assignments**: Simple and compound assignments, including tuple unpacking

This is a **critical component** in the compiler pipeline that converts tokens into high-level AST nodes representing the structure of Sharpy programs. While other Parser partial classes handle expressions, statements, and types, this file focuses on the "definition" layer—the building blocks that define the structure of classes, functions, and types.

### Role in Pipeline

```
Lexer (tokens) → Parser.Definitions → AST (ClassDef, FunctionDef, etc.) → Semantic Analysis
```

## Class/Type Structure

### Partial Class Design

This file is part of the `Parser` partial class split across multiple files:

- **Parser.cs**: Main parser structure, initialization, and coordination
- **Parser.Definitions.cs** (this file): Definition parsing (classes, functions, enums, etc.)
- **Parser.Statements.cs**: Statement parsing (if, while, for, return, etc.)
- **Parser.Expressions.cs**: Expression parsing with operator precedence
- **Parser.Types.cs**: Type annotation parsing
- **Parser.Primaries.cs**: Primary expressions (literals, identifiers, etc.)

This separation follows the **Single Responsibility Principle**, keeping related parsing logic together while maintaining a cohesive `Parser` class.

### AST Nodes Produced

This file creates the following AST node types (all from `Sharpy.Compiler.Parser.Ast` namespace):

```csharp
// Type definitions
ClassDef          // class MyClass[T](BaseClass): ...
StructDef         // struct Point(IComparable): ...
InterfaceDef      // interface IShape: ...
EnumDef           // enum Color: RED = 1 ...

// Function definitions
FunctionDef       // def foo[T](x: int) -> bool: ...

// Variables and constants
VariableDeclaration  // x: int = 5 or const PI = 3.14

// Type aliases
TypeAlias         // type Handler = (int, str) -> bool

// Statements
Assignment        // x = 10 or x, y = tuple
ExpressionStatement  // someFunction()
```

## Key Methods

### 1. `ParseSimpleStatement()` (Lines 13-132)

**Purpose**: Disambiguates and parses simple statements that can be assignments, variable declarations, or expression statements.

**Algorithm**:

1. **Parse initial expression** - Start by parsing an expression
2. **Check for tuple unpacking** - If comma follows, parse remaining tuple elements
3. **Determine statement type**:
   - **Assignment** (`=`, `+=`, etc.) → Create `Assignment` AST node
   - **Type annotation** (`:`) → Create `VariableDeclaration`
   - **Neither** → Wrap in `ExpressionStatement`

**Key Features**:

- **Tuple unpacking support**: `x, y = get_coords()` or `a, b, c = 1, 2, 3`
- **Compound assignments**: `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=`, `&=`, `|=`, `^=`, `<<=`, `>>=`, `??=`
- **Type inference**: Variables can be declared with or without initial values: `x: int` or `x: int = 5`

**Example Sharpy Code**:

```python
# Simple assignment
x = 10

# Compound assignment
x += 5

# Tuple unpacking
x, y = get_point()

# Variable declaration with type
name: str = "Alice"

# Variable declaration without initial value
count: int
```

**Error Handling**:

- Throws `ParserError` if tuple expression appears as standalone statement (line 68)
- Validates that type annotation target is a simple identifier (line 94-95)

---

### 2. `ParseFunctionDef()` (Lines 153-251)

**Purpose**: Parses function definitions including generic type parameters, parameters, return types, and body.

**Syntax Structure**:

```python
def function_name[TypeParams](params) -> ReturnType:
    """Optional docstring"""
    # function body
```

**Algorithm**:

1. **Expect `def` keyword** and function name
2. **Parse type parameters** (optional): `[T, U: IComparable]`
3. **Parse parameters**: `(x: int, y: str = "default")`
4. **Parse return type** (optional): `-> bool`
5. **Handle two body styles**:
   - **Inline ellipsis**: `def foo(): ...` (for stubs/interfaces)
   - **Full body**: Indented block with optional docstring

**Special Cases**:

- **Abstract/stub functions**: Support `...` (ellipsis) as placeholder body
- **Docstrings**: First statement can be a string literal
- **Generic functions**: Type parameters with constraints

**Example Sharpy Code**:

```python
# Simple function
def greet(name: str) -> str:
    return f"Hello, {name}!"

# Generic function with constraints
def max[T: IComparable](a: T, b: T) -> T:
    return a if a > b else b

# Stub function (interface/abstract)
def process_data(data: list[int]) -> None: ...
```

**AST Node Fields**:

```csharp
FunctionDef {
    Name,              // Function identifier
    TypeParameters,    // Generic type params [T, U]
    Parameters,        // Function parameters
    ReturnType,        // Optional return type annotation
    Body,              // List of statements
    DocString          // Optional documentation string
}
```

---

### 3. `ParseClassDef()` (Lines 253-317)

**Purpose**: Parses class definitions with inheritance, type parameters, and class body.

**Syntax Structure**:

```python
class ClassName[T, U](BaseClass, Interface1, Interface2):
    """Optional docstring"""
    # class body
```

**Algorithm**:

1. **Expect `class` keyword** and class name
2. **Parse type parameters** (optional): `[T: IComparable]`
3. **Parse base classes** (optional): `(ParentClass, IInterface)`
4. **Parse body**: Indented block with optional docstring at start

**Design Notes**:

- **Multiple inheritance**: Supports multiple base classes/interfaces
- **Generic classes**: Full support for constrained type parameters
- **Docstrings**: Extracted from first string literal in body

**Example Sharpy Code**:

```python
# Simple class
class Person:
    name: str
    age: int

    def __init__(self, name: str, age: int):
        self.name = name
        self.age = age

# Generic class with constraint
class Container[T: IComparable](IEnumerable[T]):
    """A generic container that stores comparable items"""
    items: list[T]

    def add(self, item: T) -> None:
        self.items.append(item)
```

---

### 4. `ParseStructDef()` (Lines 319-383)

**Purpose**: Parses struct (value type) definitions.

**Key Difference from Classes**:

- **Structs** can only implement **interfaces**, not inherit from base classes
- Represents value types in the compiled C# code
- Otherwise similar structure to `ParseClassDef()`

**Example Sharpy Code**:

```python
struct Point(IComparable[Point]):
    """Represents a 2D point"""
    x: float
    y: float

    def distance_from_origin(self) -> float:
        return (self.x ** 2 + self.y ** 2) ** 0.5
```

**Why Separate from Classes?**:

The distinction between `class` and `struct` affects code generation:
- **Classes** → C# reference types (`class`)
- **Structs** → C# value types (`struct`)

This semantic difference is preserved in the AST for downstream processing.

---

### 5. `ParseInterfaceDef()` (Lines 385-449)

**Purpose**: Parses interface definitions.

**Syntax Structure**:

```python
interface IShape[T](IComparable[T]):
    """Shape interface"""
    def get_area(self) -> float: ...
    def get_perimeter(self) -> float: ...
```

**Key Features**:

- Interfaces contain only **method signatures** (usually with `...` bodies)
- Can extend other interfaces via base interfaces list
- Supports generic type parameters

---

### 6. `ParseEnumDef()` (Lines 533-612)

**Purpose**: Parses enumeration definitions.

**Syntax Structure**:

```python
enum Color:
    """RGB color values"""
    RED = 1
    GREEN = 2
    BLUE = 3
```

**Algorithm**:

1. **Expect `enum` keyword** and name
2. **Parse optional docstring**
3. **Parse enum members**: Each can have optional explicit value
4. **Validate**: Enum must have at least one member (line 597-600)

**Special Handling**:

- **Explicit values**: `RED = 1` sets explicit integer value
- **Auto values**: `RED` without `=` gets auto-assigned value
- **Pass statements**: Allowed but skipped (for empty enum blocks during development)

**Example Sharpy Code**:

```python
enum HttpMethod:
    GET = 1
    POST = 2
    PUT = 3
    DELETE = 4

enum LogLevel:
    DEBUG
    INFO
    WARNING
    ERROR
```

---

### 7. `ParseTypeAlias()` (Lines 614-675)

**Purpose**: Parses type alias declarations, including function type signatures.

**Two Forms**:

1. **Simple type alias**: `type UserId = int`
2. **Function type alias**: `type Handler = (int, str) -> bool`

**Algorithm**:

1. **Expect `type` keyword** and alias name
2. **Check if function type** (starts with `(`)
   - Parse parameter types: `(int, str, bool)`
   - Parse return type: `-> ReturnType`
3. **Otherwise**: Parse regular type annotation

**Example Sharpy Code**:

```python
# Simple type aliases
type UserId = int
type Point2D = tuple[float, float]
type OptionalString = str | None

# Function type aliases
type Predicate = (int) -> bool
type BinaryOp = (int, int) -> int
type EventHandler = (object, EventArgs) -> None
```

**AST Structure**:

```csharp
TypeAlias {
    Name,           // Alias name
    Type,           // Regular type (if not function type)
    FunctionType    // Function signature (if function type)
}
```

---

### 8. `ParseConstDeclaration()` (Lines 677-710)

**Purpose**: Parses constant declarations at module or class level.

**Syntax**:

```python
const PI = 3.14159
const MAX_SIZE: int = 1000
```

**Features**:

- **Type inference**: Type annotation is optional
- **Required initialization**: Constants must be initialized with a value
- **Compile-time values**: Used for module-level constants

**Difference from Variable Declaration**:

- Sets `IsConst = true` in `VariableDeclaration` AST node
- Semantic analyzer enforces that value is a compile-time constant
- Cannot be reassigned after initialization

---

### 9. `ParseTypeParameterList()` (Lines 451-484)

**Purpose**: Parses generic type parameter lists with constraints.

**Syntax**:

```python
[T]                          # Simple type parameter
[T, U]                       # Multiple parameters
[T: IComparable]             # Single constraint
[T: IComparable & class]     # Multiple constraints
```

**Algorithm**:

1. **Expect `[`**
2. **For each type parameter**:
   - Parse parameter name
   - Parse optional constraints (after `:`)
3. **Expect `]`**

**Used By**:

- `ParseFunctionDef()` - Generic functions
- `ParseClassDef()` - Generic classes
- `ParseStructDef()` - Generic structs
- `ParseInterfaceDef()` - Generic interfaces

---

### 10. `ParseConstraints()` and `ParseSingleConstraint()` (Lines 486-531)

**Purpose**: Parses type parameter constraints.

**Constraint Types**:

```csharp
ClassConstraint       // class (reference type)
StructConstraint      // struct (value type)
NewConstraint         // new() (default constructor)
TypeConstraint        // IComparable (interface/base type)
```

**Syntax Examples**:

```python
[T: class]                    # Must be reference type
[T: struct]                   # Must be value type
[T: new()]                    # Must have parameterless constructor
[T: IComparable]              # Must implement interface
[T: IComparable & class]      # Multiple constraints with &
```

**Parsing Logic**:

- **`&` operator**: Combines multiple constraints
- **Order matters**: Constraints are stored as a list
- **Keyword vs Type**: Distinguishes `class`/`struct` keywords from type names

---

### 11. `TokenTypeToAssignmentOperator()` (Lines 134-151)

**Purpose**: Maps token types to AST assignment operator enums.

**Supported Operators**:

```csharp
=     → Assign
+=    → PlusAssign
-=    → MinusAssign
*=    → StarAssign
/=    → SlashAssign
//=   → DoubleSlashAssign (integer division)
%=    → PercentAssign
**=   → PowerAssign
&=    → AndAssign (bitwise)
|=    → OrAssign (bitwise)
^=    → XorAssign (bitwise)
<<=   → LeftShiftAssign
>>=   → RightShiftAssign
??=   → NullCoalesceAssign
```

**Design Pattern**: Uses C# switch expression for clean mapping.

## Dependencies

### Internal Sharpy Dependencies

```csharp
using Sharpy.Compiler.Lexer;        // Token, TokenType
using Sharpy.Compiler.Logging;      // ParserError (error reporting)
using Sharpy.Compiler.Parser.Ast;   // All AST node types
```

### Methods from Other Parser Partials

This file calls methods defined in other partial class files:

**From Parser.cs** (main file):
- `Advance()` - Move to next token
- `Peek(offset)` - Look ahead/behind in token stream
- `Expect(TokenType)` - Consume expected token or throw error
- `ExpectIdentifier()` - Parse and return identifier name
- `ExpectNewline()` - Expect newline token
- `SkipNewlines()` - Skip optional newlines
- `ExpectStatementEnd()` - Ensure statement ends properly

**From Parser.Expressions.cs**:
- `ParseExpression()` - Parse any expression

**From Parser.Statements.cs**:
- `ParseBlock()` - Parse indented block of statements

**From Parser.Types.cs**:
- `ParseTypeAnnotation()` - Parse type annotations like `int`, `list[str]`
- `ParseParameters()` - Parse function parameter list

### Properties Used

```csharp
Current      // Current token being examined
IsAtEnd      // Whether we've reached end of token stream
```

## Patterns and Design Decisions

### 1. **Recursive Descent Parsing**

All parsing methods follow the **recursive descent** pattern:
- Each non-terminal in the grammar has a corresponding method
- Methods consume tokens and recursively call other parsing methods
- Clean mapping from grammar rules to code

**Example**:

```csharp
// Grammar: ClassDef ::= 'class' IDENTIFIER TypeParams? BaseClasses? ':' NEWLINE Block
// Method directly mirrors this structure
private ClassDef ParseClassDef()
{
    Expect(TokenType.Class);              // 'class'
    var name = ExpectIdentifier();        // IDENTIFIER
    if (Current.Type == LeftBracket)      // TypeParams?
        typeParams = ParseTypeParameterList();
    if (Current.Type == LeftParen)        // BaseClasses?
        baseClasses = ParseBaseClasses();
    Expect(TokenType.Colon);              // ':'
    ExpectNewline();                      // NEWLINE
    var body = ParseBlock();              // Block
    return new ClassDef { ... };
}
```

### 2. **Position Tracking**

Every AST node tracks source location for error reporting:

```csharp
var startLine = Current.Line;
var startColumn = Current.Column;
// ... parse node ...
return new SomeNode {
    // ... fields ...
    LineStart = startLine,
    ColumnStart = startColumn,
    LineEnd = Current.Line,
    ColumnEnd = Current.Column
};
```

This enables high-quality error messages that point to exact source locations.

### 3. **Docstring Extraction**

Common pattern for extracting docstrings:

```csharp
string? docString = null;
Expect(TokenType.Indent);

// Check for docstring as first element
if (Current.Type == TokenType.String)
{
    docString = Current.Value;
    Advance();
    SkipNewlines();
}

var body = ParseBlock();
```

Docstrings are always:
- Optional
- First statement in a block
- String literals
- Stored separately from body statements

### 4. **Optional vs Required Elements**

Clear distinction using conditional parsing:

```csharp
// Optional type parameters
if (Current.Type == TokenType.LeftBracket)
    typeParams = ParseTypeParameterList();

// Required colon
Expect(TokenType.Colon);
```

- **Optional**: Check token type with `if`
- **Required**: Use `Expect()` which throws if token doesn't match

### 5. **Validation at Parse Time**

Some semantic checks happen during parsing:

```csharp
// Enum must have at least one member
if (members.Count == 0)
    throw new ParserError($"Enum '{name}' must have at least one member", ...);
```

**Why?** Catches obvious errors early with better context than semantic analysis phase.

### 6. **Inline Stub Syntax**

Special handling for abstract/stub methods:

```csharp
// Support: def foo(): ...
if (Current.Type == TokenType.Ellipsis)
{
    // Create function with ellipsis expression as body
    // Skip indent/dedent since body is inline
}
```

This allows interface definitions and stubs without multi-line blocks.

## Debugging Tips

### 1. **Token Stream Inspection**

When debugging parser issues:

```csharp
// Add this before parsing to see current token
Console.WriteLine($"Current: {Current.Type} = '{Current.Value}' at {Current.Line}:{Current.Column}");
```

### 2. **Common Error Patterns**

**Indentation errors**: Most common cause is incorrect indent/dedent token handling
- Check that lexer is emitting indent/dedent correctly
- Verify `ExpectNewline()` and `SkipNewlines()` usage

**Type annotation parsing**: If types aren't parsing correctly
- Check `Parser.Types.cs` for type annotation logic
- Verify bracket matching for generics `list[int]`

**Expression disambiguation**: If assignments/declarations fail
- Problem is likely in `ParseSimpleStatement()` line 13-132
- Add logging to see which branch is taken

### 3. **AST Validation**

After parsing, dump the AST to verify structure:

```csharp
var dumper = new AstDumper();
dumper.Visit(parsedNode);
Console.WriteLine(dumper.GetResult());
```

See `AstDumper.cs` for AST visualization tool.

### 4. **Position Tracking Issues**

If error messages point to wrong locations:

```csharp
// Double-check position capture
var startLine = Current.Line;     // BEFORE consuming tokens
var startColumn = Current.Column; // BEFORE consuming tokens
// ... parse ...
var endLine = Peek(-1).Line;      // AFTER consuming (look back)
```

### 5. **Testing Individual Methods**

You can test parsing methods in isolation:

```csharp
var tokens = new Lexer("def foo(): pass").Tokenize();
var parser = new Parser(tokens, errorLogger);
var funcDef = parser.ParseFunctionDef();
Assert.Equal("foo", funcDef.Name);
```

Look at `test/Sharpy.Compiler.Tests/Parser/` for examples.

## Contribution Guidelines

### When to Modify This File

You should edit `Parser.Definitions.cs` when:

1. **Adding new definition types**: New top-level constructs (e.g., `trait`, `protocol`)
2. **Extending existing definitions**: Adding syntax to classes, functions, etc.
3. **New type parameter features**: Enhanced constraint syntax
4. **Assignment operator changes**: New compound assignment operators

### When NOT to Modify This File

These belong in other parser files:

- **Expression parsing** → `Parser.Expressions.cs`
- **Statement parsing** → `Parser.Statements.cs`
- **Type annotation syntax** → `Parser.Types.cs`
- **Primary expressions** → `Parser.Primaries.cs`
- **Core parser infrastructure** → `Parser.cs`

### Code Style Guidelines

**1. Match Existing Patterns**

Follow the established structure:

```csharp
private SomeNode ParseSomething()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;

    Expect(TokenType.Something);
    // ... parsing logic ...

    return new SomeNode {
        // ... fields ...
        LineStart = startLine,
        ColumnStart = startColumn,
        LineEnd = Current.Line,
        ColumnEnd = Current.Column
    };
}
```

**2. Add Comments for Complex Logic**

Example from tuple unpacking (lines 22-24):

```csharp
// Check for tuple unpacking: x, y = ...
// If we see a comma after the expression, it might be a tuple target
```

**3. Validate Input When Reasonable**

```csharp
if (members.Count == 0)
    throw new ParserError($"Enum '{name}' must have at least one member", ...);
```

**4. Keep AST Node Creation Clear**

Use object initializer syntax:

```csharp
return new ClassDef
{
    Name = name,
    TypeParameters = typeParams,
    BaseClasses = baseClasses,
    Body = body,
    DocString = docString,
    LineStart = startLine,
    ColumnStart = startColumn,
    LineEnd = Current.Line,
    ColumnEnd = Current.Column
};
```

### Testing Requirements

When adding/modifying parsing logic:

1. **Add unit tests**: `test/Sharpy.Compiler.Tests/Parser/DefinitionTests.cs`
2. **Test error cases**: Invalid syntax should produce good error messages
3. **Test edge cases**: Empty lists, optional elements, etc.
4. **Integration tests**: Add test `.spy` files to `examples/` directory

### Documentation Updates

When adding new syntax:

1. Update language documentation
2. Add examples to this walkthrough
3. Update grammar specification if one exists
4. Add to syntax highlighting/IDE support

## Cross-References

### Related Parser Files

This file is part of the `Parser` partial class. See also:

- **[Parser.md](./Parser.md)** - Main parser structure and core methods
- **Parser.Statements.md** - Control flow and statement parsing
- **Parser.Expressions.md** - Expression parsing with operator precedence
- **Parser.Types.md** - Type annotation parsing
- **Parser.Primaries.md** - Literal and primary expression parsing

### AST Node Definitions

For detailed AST node structure:

- **[Statement.md](./Ast/Statement.md)** - Statement AST nodes including definitions
- **[Expression.md](./Ast/Expression.md)** - Expression AST nodes

### Upstream/Downstream

- **Upstream**: [Lexer.md](../Lexer/Lexer.md) - Token generation
- **Downstream**: Semantic Analysis - Type checking and validation (not yet documented)

### Related Components

- **[ParserError.md](./ParserError.md)** - Error reporting
- **[AstDumper.md](./AstDumper.md)** - AST visualization tool
