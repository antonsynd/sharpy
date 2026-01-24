# Walkthrough: Parser.Definitions.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.Definitions.cs`

---

## Overview

This file is a partial class extension of the main `Parser` class, specifically responsible for parsing **type and function definitions** in Sharpy source code. While other Parser partial classes handle expressions, statements, and type annotations, this file focuses on the structural building blocks that define the shape of a Sharpy program.

**Key Responsibilities:**
- Parsing function definitions (`def`)
- Parsing class definitions (`class`)
- Parsing struct definitions (`struct`)
- Parsing interface definitions (`interface`)
- Parsing enum definitions (`enum`)
- Parsing type aliases (`type`)
- Parsing constant declarations (`const`)
- Parsing simple statements (assignments, variable declarations, expression statements)

This file sits at the **Syntactic Analysis** stage of the compiler pipeline:
```
Source (.spy) → Lexer (tokens) → [Parser: THIS FILE] → AST → Semantic Analysis → RoslynEmitter → C#
```

---

## Class/Type Structure

This file is part of the `Parser` partial class, which is split across multiple files:

```
Parser (partial class)
├── Parser.cs                  - Core infrastructure, token navigation, ParseModule()
├── Parser.Definitions.cs      - THIS FILE: type/function definitions
├── Parser.Expressions.cs      - Expression parsing
├── Parser.Statements.cs       - Control flow statements (if, while, for, etc.)
├── Parser.Types.cs            - Type annotation parsing
└── Parser.Primaries.cs        - Primary expressions (literals, identifiers, etc.)
```

**State maintained by the main Parser class** (from `Parser.cs:15-30`):
- `_tokens`: List of tokens from the lexer
- `_position`: Current position in token stream
- `_logger`: Compiler logger for diagnostics
- `Current`: Property to get current token
- `Previous`: Property to get previous token
- `Peek()`: Method to look ahead in token stream

---

## Key Functions/Methods

### 1. `ParseSimpleStatement()` (Lines 14-139)

**Purpose:** Dispatches parsing for statements that can be assignments, variable declarations, or expression statements. This is the "catch-all" for statements that don't start with a keyword.

**Flow:**
```
ParseExpression()
    ↓
Check for comma? → Tuple unpacking assignment
    ↓
Check for assignment operator (=, +=, etc.)? → Assignment
    ↓
Check for type annotation (:)? → Variable declaration
    ↓
Otherwise → Expression statement
```

**Key Implementation Details:**

- **Tuple Unpacking** (Lines 25-71): Handles `x, y = values` by detecting comma-separated expressions followed by assignment
  ```python
  # This gets parsed as a tuple unpacking assignment
  x, y, z = get_coordinates()
  ```

- **Assignment Operators** (Lines 74-92): Converts tokens like `+=`, `-=`, `**=` to AST `AssignmentOperator` enum values

- **Variable Declarations** (Lines 95-126): Parses type-annotated variables with optional initialization
  ```python
  # Both of these are parsed here
  x: int
  y: str = "hello"
  ```

- **Expression Statements** (Lines 130-138): Wraps standalone expressions (like function calls) in `ExpressionStatement` nodes

**Returns:** `Statement` (one of: `Assignment`, `VariableDeclaration`, or `ExpressionStatement`)

**Error Handling:** Throws `ParserError` if tuple expressions appear without assignment (line 70) or if type annotation target is not a simple identifier (line 98).

---

### 2. `ParseFunctionDef()` (Lines 160-264)

**Purpose:** Parses function definitions with full support for type parameters, parameters, return types, and bodies.

**Syntax Supported:**
```python
def simple_func(x: int) -> int:
    return x * 2

def generic_func[T](value: T) -> T:
    return value

def abstract_func(): ...  # Ellipsis shorthand for abstract methods
```

**Parsing Flow:**
1. Consume `def` keyword
2. Parse function name
3. **Optional:** Parse type parameters `[T, U]` (line 172-175)
4. Parse parameter list in parentheses
5. **Optional:** Parse return type after `->` (line 182-187)
6. Expect `:` colon
7. **Special case:** Handle inline ellipsis `def foo(): ...` (lines 192-231)
8. Otherwise, parse indented block with optional docstring

**Key Features:**
- **Type Parameters:** Supports generic functions like `def sort[T: Comparable](items: list[T])`
- **Docstrings:** First string literal in function body becomes `DocString` property
- **Abstract Methods:** Ellipsis literal creates a valid AST without requiring full implementation

**Returns:** `FunctionDef` AST node with:
- `Name`, `TypeParameters`, `Parameters`, `ReturnType`
- `Body` (immutable array of statements)
- `DocString` (optional)
- Position/span information

**Connects to:**
- Upstream: `Parser.cs:102` dispatches here when seeing `TokenType.Def`
- Downstream: `ParseTypeParameterList()` for generics, `ParseParameters()` from Parameters parsing logic

---

### 3. `ParseClassDef()` (Lines 266-333)

**Purpose:** Parses class definitions including type parameters and base class/interface inheritance.

**Syntax Supported:**
```python
class MyClass:
    pass

class GenericClass[T](BaseClass, IComparable):
    """Class docstring"""
    x: int
```

**Key Differences from Functions:**
- **Base Classes** (lines 285-300): Parses comma-separated list in parentheses after class name
- **Type Parameters:** Same syntax as functions using square brackets `[T, U]`
- **Body:** Contains class members (fields, methods, nested types)

**Implementation Pattern:**
This method follows the same structural pattern as all other definition parsers:
1. Capture start position/token
2. Consume keyword and name
3. Parse optional modifiers (type params, bases)
4. Expect `:` and newline
5. Parse indented body with optional docstring
6. Capture end position and create AST node

**Returns:** `ClassDef` AST node

---

### 4. `ParseStructDef()` (Lines 335-402)

**Purpose:** Parses value-type struct definitions (similar to C# structs).

**Key Constraint:**
Structs can only implement interfaces, not inherit from classes (this is enforced at semantic analysis stage, but the field is named `BaseClasses` for consistency).

**Syntax:**
```python
struct Point(IEquatable):
    x: float
    y: float
```

**Nearly Identical to `ParseClassDef()`:** The implementation is almost a copy-paste, with only naming differences. This is intentional to keep each definition parser self-contained.

**Returns:** `StructDef` AST node

---

### 5. `ParseInterfaceDef()` (Lines 404-471)

**Purpose:** Parses interface definitions (contracts for implementation).

**Key Difference:**
- Field is named `BaseInterfaces` instead of `BaseClasses` (line 462)
- Interfaces can only extend other interfaces, never classes

**Syntax:**
```python
interface IComparable(IEquatable):
    def compare(other: Self) -> int: ...
```

**Returns:** `InterfaceDef` AST node

---

### 6. `ParseTypeParameterList()` (Lines 473-517)

**Purpose:** Parses generic type parameters with constraints.

**Syntax Examples:**
```python
# Simple type parameters
[T, U, V]

# With constraints
[T: IComparable, U: class, V: struct]

# Multiple constraints
[T: IComparable & IEquatable]
```

**Constraint Types Supported** (parsed by `ParseSingleConstraint()`):
- `class` - reference type constraint
- `struct` - value type constraint
- `new()` - parameterless constructor constraint
- Type constraints - interface or base type requirements

**Returns:** `List<TypeParameterDef>` where each parameter has:
- `Name`: The type parameter name
- `Constraints`: Immutable array of constraint clauses
- Position/span information

**Connects to:** Called by `ParseFunctionDef()`, `ParseClassDef()`, `ParseStructDef()`, `ParseInterfaceDef()`

---

### 7. `ParseEnumDef()` (Lines 566-651)

**Purpose:** Parses enumeration definitions.

**Syntax:**
```python
enum Color:
    Red = 1
    Green = 2
    Blue = 3

enum Status:
    Pending    # Auto-assigned value
    Active
    Complete
```

**Key Features:**
- **Optional Values:** Members can have explicit values or be auto-assigned (line 607-611)
- **Validation:** Enums must have at least one member (lines 635-638)
- **Pass Statements:** Empty enums can use `pass` as placeholder (lines 593-599)

**Parsing Loop:**
- Continues until `TokenType.Dedent` (end of indented block)
- Each iteration parses: name + optional `= value`
- Newlines are expected after each member

**Returns:** `EnumDef` AST node with members array

**Error Case:** Throws if enum has zero members after parsing

---

### 8. `ParseTypeAlias()` (Lines 653-717)

**Purpose:** Parses type alias declarations (creating named shortcuts for types).

**Two Forms Supported:**

**Regular Type Alias:**
```python
type StringList = list[str]
type IntPair = tuple[int, int]
```

**Function Type Alias:**
```python
type Comparator = (int, int) -> bool
type Transform = (str) -> int
```

**Disambiguation Logic** (lines 667-701):
- If next token after `=` is `(`, check if it's a function type
- Function types require `->` arrow after parameter list
- Regular types use standard type annotation parsing

**Returns:** `TypeAlias` AST node with either:
- `Type` field set (for regular aliases), OR
- `FunctionType` field set (for function types)

---

### 9. `ParseConstDeclaration()` (Lines 719-755)

**Purpose:** Parses compile-time constant declarations.

**Syntax:**
```python
const PI: float = 3.14159
const APP_NAME = "Sharpy"  # Type inferred
```

**Key Points:**
- Type annotation is **optional** (lines 731-736)
- Initial value is **required** (line 738)
- Creates a `VariableDeclaration` with `IsConst = true` (line 748)

**Contrast with Regular Variables:**
Regular `x: int = 5` is parsed in `ParseSimpleStatement()`, while `const` declarations are parsed here and dispatched from `Parser.cs:119`.

**Returns:** `VariableDeclaration` AST node with `IsConst = true`

---

### 10. `TokenTypeToAssignmentOperator()` (Lines 141-158)

**Purpose:** Converts lexer token types to AST assignment operator enum values.

**Mapping:**
```
TokenType.Assign          → AssignmentOperator.Assign (=)
TokenType.PlusAssign      → AssignmentOperator.PlusAssign (+=)
TokenType.DoubleStarAssign → AssignmentOperator.PowerAssign (**=)
...and 11 more operators
```

**Usage:** Called by `ParseSimpleStatement()` when constructing `Assignment` nodes

**Error Handling:** Throws if passed a non-assignment token type (should never happen if parser is correct)

---

### 11. Helper Methods: Constraints Parsing (Lines 519-564)

**`ParseConstraints()`** - Parses ampersand-separated constraint clauses:
```python
[T: IComparable & IEquatable & new()]
```

**`ParseSingleConstraint()`** - Parses one constraint:
- `class` keyword → `ClassConstraint` (line 539-543)
- `struct` keyword → `StructConstraint` (line 546-550)
- `new()` → `NewConstraint` (line 553-559)
- Type annotation → `TypeConstraint` (line 562-563)

These directly mirror C# generic constraint syntax.

---

## Dependencies

### Internal Sharpy Dependencies

**From `Sharpy.Compiler.Lexer`:**
- `Token`: Token data structure (type, value, position)
- `TokenType`: Enum of all token types (keywords, operators, literals)

**From `Sharpy.Compiler.Logging`:**
- `ICompilerLogger`: Diagnostic logging interface

**From `Sharpy.Compiler.Parser.Ast`:**
- `Statement`: Base class for all statements
- `Expression`: Base class for all expressions
- `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`: Definition AST nodes
- `Assignment`, `VariableDeclaration`, `ExpressionStatement`: Statement types
- `TypeAnnotation`, `TypeParameterDef`, `ConstraintClause`: Type system nodes
- `TupleLiteral`, `Identifier`, `EllipsisLiteral`: Expression nodes

### Methods Called From Other Partial Classes

**Not defined in this file** (must be in other Parser partials):
- `ParseExpression()` - Expression parsing entry point
- `ParseTypeAnnotation()` - Type annotation parsing
- `ParseParameters()` - Function parameter list parsing
- `ParseBlock()` - Indented block of statements
- `Expect()`, `ExpectIdentifier()`, `ExpectNewline()`, `ExpectStatementEnd()` - Token consumption
- `Advance()`, `SkipNewlines()` - Token navigation
- `GetSpanFromToken()`, `GetSpanFromTokens()`, `CombineSpans()` - Span tracking

---

## Patterns and Design Decisions

### 1. **Recursive Descent Parsing**

Each parsing method is responsible for one grammar production rule. The method structure directly mirrors the language grammar:

```
FunctionDef → 'def' Identifier TypeParams? '(' Parameters ')' ('->' Type)? ':' Block
```

becomes:

```csharp
private FunctionDef ParseFunctionDef() {
    Expect(TokenType.Def);
    var name = ExpectIdentifier();
    if (Current.Type == TokenType.LeftBracket)
        typeParams = ParseTypeParameterList();
    // ... and so on
}
```

### 2. **Immutable AST Nodes**

All AST nodes use `ImmutableArray<T>` for collections (lines 44, 203, 254, etc.). This ensures:
- Thread safety for parallel semantic analysis
- No accidental mutations during compilation
- Clear ownership semantics

### 3. **Position Tracking Pattern**

Every definition parser follows this pattern:
```csharp
var startLine = Current.Line;
var startColumn = Current.Column;
var startToken = Current;

// ... parsing logic ...

var endToken = Previous;

return new AstNode {
    // ... properties ...
    LineStart = startLine,
    ColumnStart = startColumn,
    LineEnd = Current.Line,
    ColumnEnd = Current.Column,
    Span = GetSpanFromTokens(startToken, endToken)
};
```

This enables:
- Precise error reporting
- IDE features (go-to-definition, find references)
- Source map generation

### 4. **Docstring Convention**

All definition parsers check for a string literal as the first statement in the body (e.g., lines 238-244 for functions). This is Python's docstring convention, carried over to Sharpy.

### 5. **Error Recovery Strategy**

The parser uses **panic mode** error recovery:
- Throws `ParserError` immediately on unexpected tokens
- No attempt to continue parsing after errors in this file
- Higher-level code can catch and recover if needed

This is a simple strategy appropriate for a compiler (versus an IDE language server which needs better recovery).

### 6. **Ellipsis as Abstract Method Marker**

The special handling for `def foo(): ...` (lines 192-231) is a Python convention that Sharpy adopts. The ellipsis becomes an `EllipsisLiteral` expression in the AST, which semantic analysis can recognize as "abstract method, no implementation yet."

### 7. **Consistent AST Node Structure**

Notice that `ClassDef`, `StructDef`, and `InterfaceDef` have nearly identical implementations (lines 266-471). This is intentional:
- Makes parser easier to understand
- Each definition type is self-contained
- Changes to one don't accidentally affect others
- Code duplication is acceptable when it improves clarity

---

## Debugging Tips

### 1. **Use the `emit ast` Command**

To see what AST this file generates:
```bash
dotnet run --project src/Sharpy.Cli -- emit ast yourfile.spy
```

This shows the complete AST structure, helping you verify parsing correctness.

### 2. **Check Token Stream First**

If parsing fails, verify the lexer is producing correct tokens:
```bash
dotnet run --project src/Sharpy.Cli -- emit tokens yourfile.spy
```

Many "parser errors" are actually lexer issues.

### 3. **Common Parser Error Patterns**

**"Unexpected token"** errors usually mean:
- Missing token consumption (`Advance()` call)
- Wrong token type in `Expect()` call
- Lexer didn't recognize a keyword

**Position off-by-one** errors:
- Check that `endToken = Previous` happens at the right time
- Verify `Advance()` is called before or after position capture

### 4. **Breakpoint Locations**

Set breakpoints at:
- Start of definition parsers (lines 160, 266, 335, 404, 566, 653, 719)
- `ParseSimpleStatement()` entry (line 14) to trace statement dispatching
- Error throws (lines 70, 98, 637) to catch malformed input

### 5. **Trace Token Position**

Add logging:
```csharp
_logger.LogDebug($"Parsing function, current token: {Current.Type} '{Current.Value}' at {Current.Line}:{Current.Column}");
```

The `_logger` field is available in all partial class methods.

### 6. **Testing Individual Definition Types**

Create minimal test cases:
```python
# test_function.spy
def foo(): pass

# test_class.spy
class Bar: pass

# test_enum.spy
enum Baz:
    A
```

Run file-based tests to isolate issues.

---

## Contribution Guidelines

### What Kinds of Changes Might Be Made to This File

#### 1. **Adding New Definition Types**

If Sharpy adds new top-level constructs (e.g., `trait`, `extension`, `namespace`):

```csharp
private TraitDef ParseTraitDef()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;
    var startToken = Current;

    Expect(TokenType.Trait);  // Requires adding TokenType.Trait to Lexer
    var name = ExpectIdentifier();

    // Follow the pattern of ParseInterfaceDef()...
}
```

**Also update:** `Parser.cs:ParseStatement()` to dispatch to the new method.

#### 2. **Extending Type Parameter Constraints**

To support new constraint types (e.g., `unmanaged`, `notnull`):

Modify `ParseSingleConstraint()` (lines 536-564):
```csharp
if (Current.Type == TokenType.Identifier && Current.Value == "unmanaged")
{
    Advance();
    return new UnmanagedConstraint();
}
```

**Also update:** AST definitions in `Statement.cs` to add new constraint classes.

#### 3. **Improving Error Messages**

Replace generic `ParserError` with context-specific messages:
```csharp
// Before:
throw new ParserError("Invalid type annotation target", Current.Line, Current.Column);

// After:
throw new ParserError(
    $"Type annotations can only be applied to identifiers, not {expr.GetType().Name}",
    Current.Line, Current.Column);
```

#### 4. **Supporting Python-style Decorators with Arguments**

Currently decorators don't support arguments. To add:
```python
@deprecated(message="Use new_func instead")
def old_func(): pass
```

You'd need to:
- Parse argument list after decorator name
- Update `Decorator` AST node to include arguments
- Modify `ParseDecoratedStatement()` in `Parser.cs`

#### 5. **Handling Multi-line Type Annotations**

Currently type annotations must fit on one line. To support:
```python
def complex_func(
    arg1: dict[
        str,
        list[int]
    ]
) -> tuple[int, int]:
```

You'd need to modify newline handling in parameter and type parsing logic.

### Code Style Guidelines for Changes

1. **Follow the existing position tracking pattern** - Always capture `startToken`, `endToken`, and populate `LineStart/End`, `ColumnStart/End`, `Span`

2. **Use immutable collections** - Prefer `ImmutableArray<T>` for AST node collections

3. **Keep definition parsers self-contained** - Don't factor out "common code" between class/struct/interface parsers; duplication is fine

4. **Add tests** - Create file-based test fixtures in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/` for any new syntax

5. **Update documentation** - If adding new definition types, update language specification in `docs/language_specification/`

6. **Don't modify expected values to make tests pass** - This is a critical rule from `CLAUDE.md`. If a test fails, fix the implementation, not the test expectation

---

## Cross-References

### Related Parser Partial Class Files

- **[Parser.cs](Parser.md)** - Main parser class with `ParseModule()`, `ParseStatement()`, token navigation utilities
- **[Parser.Expressions.cs](Parser.Expressions.md)** - Expression parsing (`ParseExpression()` called from this file)
- **[Parser.Statements.cs](Parser.Statements.md)** - Control flow statements (if/while/for/try/etc.)
- **[Parser.Types.cs](Parser.Types.md)** - Type annotation parsing (`ParseTypeAnnotation()` called from this file)
- **[Parser.Primaries.cs](Parser.Primaries.md)** - Primary expressions and literals

### Related AST Files

- **[Statement.cs](Ast/Statement.md)** - Defines `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`, `TypeAlias`, `VariableDeclaration`
- **[Expression.cs](Ast/Expression.md)** - Defines `Identifier`, `TupleLiteral`, `EllipsisLiteral`
- **[Types.cs](Ast/Types.md)** - Defines `TypeAnnotation`, `TypeParameterDef`, `ConstraintClause` types

### Lexer Integration

- **[Lexer.md](../Lexer/Lexer.md)** - Produces tokens consumed by this parser

### Downstream Consumers

- **Semantic Analysis** - Consumes the AST produced by this parser
- **[RoslynEmitter.md](../CodeGen/RoslynEmitter.md)** - Eventually emits C# code from these AST nodes

---

## Summary

`Parser.Definitions.cs` is the **structural backbone** of the Sharpy parser. While other parser files handle the procedural aspects (expressions, statements, control flow), this file defines how to parse the **declarative elements** that give shape to a Sharpy program.

**Key Takeaways:**
- All definition parsers follow a consistent pattern: capture start, parse components, capture end, build AST node
- Immutable AST ensures safe downstream processing
- Position tracking enables excellent error reporting and IDE features
- The parser is a straightforward recursive descent implementation
- Error handling is simple (panic mode) - appropriate for a compiler

**Mental Model:**
Think of this file as the "nouns" parser - it handles classes, functions, enums, types. The other parser files handle the "verbs" (statements) and "values" (expressions).

When reading Sharpy code, the parser orchestrator (`Parser.cs`) looks at each top-level keyword and dispatches to the appropriate method in this file to build the structural definition nodes that form the skeleton of the program's AST.
