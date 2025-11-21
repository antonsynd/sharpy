# Walkthrough: Expression.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Expression.cs`

---

## 1. Overview

`Expression.cs` defines the **Abstract Syntax Tree (AST) nodes for all expression types** in the Sharpy programming language. This file is at the heart of how Sharpy represents executable code during compilation.

**What it does:**
- Defines immutable record types for every kind of expression in Sharpy (literals, operators, function calls, comprehensions, etc.)
- Provides the structural representation of code after parsing but before semantic analysis
- Serves as the intermediate representation that flows through the compiler pipeline: **Parser → Semantic Analyzer → Code Generator**

**Role in the project:**
- Part of the **Parser/AST layer** in the compiler architecture
- Works alongside `Statement.cs` (statements/declarations) and `Types.cs` (type annotations)
- Used by the Parser to build the AST from tokens
- Used by the Semantic Analyzer for type checking and validation
- Used by the Code Generator to produce C# code

**Key insight:** All expression nodes inherit from the base `Expression` record, which itself inherits from `Node`. This gives every expression position tracking (line/column numbers) for error reporting.

---

## 2. Class/Type Structure

The file is organized into **6 major categories** of expressions, each in its own `#region`:

### 2.1 Base Types

```csharp
public abstract record Expression : Node;
```

- **`Expression`**: Abstract base class for all expression nodes
- Inherits from `Node` (defined in `Node.cs`), which provides source location tracking:
  - `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`
- All expression types in this file inherit from `Expression`

### 2.2 Literals (`#region Literals`)

Represent constant values in source code:

| Type | Example Sharpy Code | Purpose |
|------|---------------------|---------|
| `IntegerLiteral` | `42`, `1_000_000`, `42L` | Integer constants with optional suffixes (L, U, UL) |
| `FloatLiteral` | `3.14`, `3.14f`, `3.14m` | Floating-point constants with optional suffixes (f, d, m) |
| `StringLiteral` | `"hello"`, `'world'`, `r"C:\path"` | String literals, including raw strings |
| `FStringLiteral` | `f"Hello {name}"` | Formatted string literals (Python-style f-strings) |
| `BooleanLiteral` | `True`, `False` | Boolean constants |
| `NoneLiteral` | `None` | Null/None value |
| `EllipsisLiteral` | `...` | Ellipsis (used in type hints and slicing) |

**Key details:**
- `FStringLiteral` is special: it contains a list of `FStringPart` records
- Each `FStringPart` can be either text (`Text` property) or an embedded expression (`Expression` property)
- This representation makes it easy to generate interpolated strings in the code generator

### 2.3 Collections (`#region Collections`)

Represent collection literals:

| Type | Example Sharpy Code | Structure |
|------|---------------------|-----------|
| `ListLiteral` | `[1, 2, 3]` | `List<Expression> Elements` |
| `DictLiteral` | `{"a": 1, "b": 2}` | `List<DictEntry> Entries` |
| `SetLiteral` | `{1, 2, 3}` | `List<Expression> Elements` |
| `TupleLiteral` | `(1, 2, 3)` or `(1,)` | `List<Expression> Elements` |

**Key details:**
- `DictLiteral` uses a helper record `DictEntry` with `Key` and `Value` properties
- Empty braces `{}` would parse as an empty dict (not a set)
- Single-element tuple requires trailing comma: `(1,)`

### 2.4 Comprehensions (`#region Comprehensions`)

Represent Python-style comprehension syntax:

| Type | Example Sharpy Code |
|------|---------------------|
| `ListComprehension` | `[x * 2 for x in numbers if x > 0]` |
| `SetComprehension` | `{x * 2 for x in numbers if x > 0}` |
| `DictComprehension` | `{k: v * 2 for k, v in items if v > 0}` |

**Structure:**
- Each comprehension has an `Element` (or `Key`/`Value` for dict) expression
- Each has a `List<ComprehensionClause> Clauses` containing:
  - `ForClause`: The iteration part (`for x in iterable`)
  - `IfClause`: The filter part (`if condition`)

**Example breakdown:**
```python
[x * 2 for x in numbers if x > 0]
```
- `Element`: `BinaryOp` representing `x * 2`
- `Clauses[0]`: `ForClause` with `Target` = `Identifier("x")` and `Iterator` = `Identifier("numbers")`
- `Clauses[1]`: `IfClause` with `Condition` = `BinaryOp` representing `x > 0`

### 2.5 Primary Expressions (`#region Primary Expressions`)

Core building blocks of expressions:

| Type | Example Sharpy Code | Key Properties |
|------|---------------------|----------------|
| `Identifier` | `x`, `my_variable` | `Name` |
| `MemberAccess` | `obj.field`, `obj?.field` | `Object`, `Member`, `IsNullConditional` |
| `IndexAccess` | `list[0]`, `dict["key"]` | `Object`, `Index` |
| `SliceAccess` | `list[1:10:2]` | `Object`, `Start`, `Stop`, `Step` (all nullable) |
| `FunctionCall` | `func(1, 2, key=3)` | `Function`, `Arguments`, `KeywordArguments` |

**Key details:**
- `MemberAccess` supports null-conditional operator (`?.`) via `IsNullConditional` flag
- `SliceAccess` allows omitted parts: `list[:10]`, `list[5:]`, `list[::2]`
- `FunctionCall` uses a helper record `KeywordArgument` for named arguments
- `KeywordArgument` has its own location tracking (for precise error messages)

### 2.6 Operators (`#region Operators`)

Represent unary and binary operations:

#### Unary Operations
```csharp
public record UnaryOp : Expression
{
    public UnaryOperator Operator { get; init; }
    public Expression Operand { get; init; } = null!;
}

public enum UnaryOperator
{
    Plus,      // +x
    Minus,     // -x
    Not,       // not x
    BitwiseNot // ~x
}
```

#### Binary Operations
```csharp
public record BinaryOp : Expression
{
    public BinaryOperator Operator { get; init; }
    public Expression Left { get; init; } = null!;
    public Expression Right { get; init; } = null!;
}
```

The `BinaryOperator` enum covers:
- **Arithmetic**: `Add`, `Subtract`, `Multiply`, `Divide`, `FloorDivide`, `Modulo`, `Power`
- **Comparison**: `Equal`, `NotEqual`, `LessThan`, `LessThanOrEqual`, `GreaterThan`, `GreaterThanOrEqual`
- **Logical**: `And`, `Or`
- **Bitwise**: `BitwiseAnd`, `BitwiseOr`, `BitwiseXor`, `LeftShift`, `RightShift`
- **Membership/Identity**: `In`, `NotIn`, `Is`, `IsNot`
- **Null coalescing**: `NullCoalesce` (C#-style `??` operator)

#### Comparison Chains
```csharp
public record ComparisonChain : Expression
{
    public List<Expression> Operands { get; init; } = new();
    public List<ComparisonOperator> Operators { get; init; } = new();
}
```

**What is this?** Python allows chained comparisons like `a < b < c`, which is semantically equivalent to `(a < b) and (b < c)` but evaluates `b` only once.

**Example:**
```python
x < y <= z
```
- `Operands`: `[x, y, z]`
- `Operators`: `[LessThan, LessThanOrEqual]`

### 2.7 Advanced Expressions (`#region Advanced Expressions`)

Special-purpose expressions:

| Type | Example Sharpy Code | Purpose |
|------|---------------------|---------|
| `ConditionalExpression` | `value if test else other` | Ternary conditional (Python's inline if) |
| `LambdaExpression` | `lambda x, y: x + y` | Anonymous functions |
| `TypeCast` | `value as int` | Explicit type casting |
| `TypeCheck` | `value is int` | Runtime type checking |
| `Parenthesized` | `(expression)` | Explicit grouping/precedence |

**Key details:**
- `ConditionalExpression` uses `ThenValue` and `ElseValue` (not `IfTrue`/`IfFalse`) to match Python's syntax order
- `LambdaExpression` uses `Parameter` records (defined in `Statement.cs`) and has a single `Expression` body (not a statement block)
- `TypeCast` and `TypeCheck` reference `TypeAnnotation` (defined in `Types.cs`)
- `Parenthesized` preserves explicit grouping for accurate code generation

---

## 3. Key Design Patterns and Decisions

### 3.1 Immutable Records
All expression nodes are **C# records** with `init`-only properties:

```csharp
public record IntegerLiteral : Expression
{
    public string Value { get; init; } = "";
    public string? Suffix { get; init; }
}
```

**Why?**
- **Thread-safety**: Multiple compiler phases can access the AST concurrently
- **Predictability**: No accidental mutations during compilation
- **Pattern matching**: Records provide excellent support for C# pattern matching (used heavily in code generation)
- **Equality semantics**: Records have built-in structural equality (useful for testing and optimization)

### 3.2 Nullable Reference Types
The file uses nullable reference types (`?`) extensively:

```csharp
public string? Suffix { get; init; }  // Optional
public Expression Operand { get; init; } = null!;  // Required (null-forgiving)
```

**Pattern:**
- **Optional properties**: Use `?` suffix (e.g., `Suffix`, `DefaultValue`)
- **Required properties**: Use `= null!` to indicate "will be set during construction"
- The parser is responsible for ensuring required properties are set

### 3.3 Hierarchy Through Inheritance
All expressions inherit from `Expression`, which inherits from `Node`:

```
Node (abstract)
  └─ Expression (abstract)
       ├─ IntegerLiteral
       ├─ BinaryOp
       ├─ FunctionCall
       └─ ... (all other expressions)
```

**Benefits:**
- **Polymorphism**: Code can work with `Expression` without knowing the specific type
- **Visitor pattern**: Easy to traverse and transform ASTs
- **Source tracking**: All expressions automatically have location information from `Node`

### 3.4 Helper Records
Several helper records support the main expression types:

- `FStringPart`: Components of f-strings
- `DictEntry`: Key-value pairs in dict literals
- `KeywordArgument`: Named arguments in function calls
- `ComprehensionClause` (abstract), `ForClause`, `IfClause`: Comprehension syntax

These helpers:
- Keep expression records focused and clean
- Can have their own location tracking (like `KeywordArgument`)
- Are reusable across different expression types

### 3.5 Enumerations for Operators
Instead of creating separate classes for each operator type, the design uses enums:

```csharp
public record BinaryOp : Expression
{
    public BinaryOperator Operator { get; init; }
    // ...
}

public enum BinaryOperator { Add, Subtract, ... }
```

**Why?**
- **Conciseness**: Single record type for all binary operations
- **Easy pattern matching**: `switch` on `Operator` enum
- **Performance**: Enums are efficient for comparison
- **Extensibility**: Adding new operators is straightforward

---

## 4. Dependencies

### 4.1 Within AST Module
- **`Node.cs`**: Provides base `Node` class with location tracking
- **`Types.cs`**: Provides `TypeAnnotation` (used in `TypeCast`, `TypeCheck`, `Parameter`)
- **`Statement.cs`**: Provides `Parameter` (used in `LambdaExpression`)

### 4.2 Used By
- **`Parser.cs`**: Constructs these AST nodes during parsing
- **`Semantic/SemanticAnalyzer.cs`**: Traverses and validates expressions
- **`Semantic/TypeChecker.cs`**: Infers and checks types
- **`CodeGen/RoslynEmitter.cs`**: Generates C# code from expressions
- **`AstDumper.cs`**: Visualizes AST for debugging

### 4.3 External Dependencies
- **.NET BCL**: Uses `System.Collections.Generic.List<T>`
- **No Sharpy.Core dependency**: AST is pure compiler infrastructure

---

## 5. How Expressions Flow Through the Compiler

### Pipeline Overview

```
Source Code
    ↓
[Lexer] → Tokens
    ↓
[Parser] → AST (Expression/Statement nodes)
    ↓
[Semantic Analyzer] → Validated + Type-annotated AST
    ↓
[Code Generator] → C# Code (via Roslyn)
    ↓
[C# Compiler] → .NET Assembly
```

### Expression Lifecycle

1. **Parser creates the expression:**
   ```csharp
   // Parser.cs
   var expr = new BinaryOp
   {
       Operator = BinaryOperator.Add,
       Left = new IntegerLiteral { Value = "1" },
       Right = new IntegerLiteral { Value = "2" },
       LineStart = 1, ColumnStart = 0, // ...
   };
   ```

2. **Semantic Analyzer validates it:**
   ```csharp
   // SemanticAnalyzer.cs
   void Visit(BinaryOp node)
   {
       var leftType = Visit(node.Left);   // Infer type of left operand
       var rightType = Visit(node.Right); // Infer type of right operand
       ValidateOperator(node.Operator, leftType, rightType);
   }
   ```

3. **Code Generator emits C# code:**
   ```csharp
   // RoslynEmitter.cs
   string Visit(BinaryOp node)
   {
       var left = Visit(node.Left);
       var right = Visit(node.Right);
       return $"{left} + {right}";  // For Add operator
   }
   ```

---

## 6. Common Patterns and Usage

### 6.1 Pattern Matching on Expressions

Code that processes expressions typically uses pattern matching:

```csharp
string ProcessExpression(Expression expr) => expr switch
{
    IntegerLiteral lit => $"int literal: {lit.Value}",
    BinaryOp op => $"binary op: {op.Operator}",
    FunctionCall call => $"function call with {call.Arguments.Count} args",
    Identifier id => $"identifier: {id.Name}",
    _ => "unknown expression"
};
```

### 6.2 Recursive Tree Traversal

Most compiler phases use recursive traversal:

```csharp
void Visit(Expression expr)
{
    switch (expr)
    {
        case BinaryOp op:
            Visit(op.Left);
            Visit(op.Right);
            break;
        case FunctionCall call:
            Visit(call.Function);
            foreach (var arg in call.Arguments)
                Visit(arg);
            break;
        // ... handle other types
    }
}
```

### 6.3 Building Expressions Programmatically

Tests and compiler phases often build AST nodes:

```csharp
// Create: x + y * 2
var expr = new BinaryOp
{
    Operator = BinaryOperator.Add,
    Left = new Identifier { Name = "x" },
    Right = new BinaryOp
    {
        Operator = BinaryOperator.Multiply,
        Left = new Identifier { Name = "y" },
        Right = new IntegerLiteral { Value = "2" }
    }
};
```

---

## 7. Debugging Tips

### 7.1 Use AstDumper for Visualization

The compiler includes `AstDumper.cs` for debugging:

```csharp
var dumper = new AstDumper();
var astString = dumper.Dump(expression);
Console.WriteLine(astString);
```

This produces human-readable tree representations of expressions.

### 7.2 Check Source Locations

All expressions have location information:

```csharp
void ReportError(Expression expr, string message)
{
    Console.WriteLine($"Error at {expr.LineStart}:{expr.ColumnStart}: {message}");
}
```

This is invaluable for error messages and debugging parser issues.

### 7.3 Watch for Null Reference Errors

Properties marked `= null!` **must** be set by the parser:

```csharp
public Expression Left { get; init; } = null!;
```

If you get `NullReferenceException`, check:
- Parser is setting all required properties
- Helper methods aren't returning null
- Defensive null checks in semantic analysis

### 7.4 Understand Operator Precedence

When debugging incorrect AST structure, remember parser precedence:
- `2 + 3 * 4` should parse as `2 + (3 * 4)`, not `(2 + 3) * 4`
- The AST structure should reflect this:
  ```
  BinaryOp(+)
    ├─ IntegerLiteral(2)
    └─ BinaryOp(*)
         ├─ IntegerLiteral(3)
         └─ IntegerLiteral(4)
  ```

### 7.5 Test with Small Examples

When adding new expression types or fixing bugs:
1. Write a minimal Sharpy source example
2. Run the parser to generate AST
3. Use `AstDumper` to visualize
4. Compare with expected structure

---

## 8. Contribution Guidelines

### 8.1 Adding a New Expression Type

**When to add:** You're implementing a new language feature that requires a new expression form.

**Steps:**
1. **Define the record** in the appropriate `#region`:
   ```csharp
   /// <summary>
   /// Matrix multiplication expression (a @ b)
   /// </summary>
   public record MatrixMultiply : Expression
   {
       public Expression Left { get; init; } = null!;
       public Expression Right { get; init; } = null!;
   }
   ```

2. **Update the Parser** (`Parser.cs`):
   - Add parsing logic to recognize and construct the new node
   - Handle operator precedence if applicable

3. **Update Semantic Analyzer**:
   - Add type checking rules
   - Add validation logic

4. **Update Code Generator** (`RoslynEmitter.cs`):
   - Add C# code generation for the new expression type

5. **Add Tests**:
   - Lexer tests (if new tokens)
   - Parser tests (verify AST structure)
   - Semantic tests (type checking)
   - Code generation tests
   - Integration tests (end-to-end)

6. **Update Documentation**:
   - Language reference in `docs/specs/`
   - User manual in `docs/manual/`

### 8.2 Modifying Existing Expressions

**Guidelines:**
- **Preserve backward compatibility** when possible
- **Update all compiler phases**: Parser, Semantic, CodeGen
- **Run all tests** to catch regressions
- **Add new tests** for the modified behavior

**Example:** Adding support for negative step in slicing:
1. Verify `SliceAccess` can handle it (it can - `Step` is nullable `Expression`)
2. Update parser to accept negative steps
3. Update semantic analyzer to validate step != 0
4. Update code generator to emit correct C# slice code
5. Add tests for negative step slicing

### 8.3 Best Practices

**DO:**
- ✅ Use descriptive XML comments for new expression types
- ✅ Follow the existing naming conventions (`PascalCase` for types, `camelCase` for properties)
- ✅ Group related expressions in appropriate `#region` blocks
- ✅ Use nullable types (`?`) for optional properties
- ✅ Use `= null!` for required properties (the parser will set them)
- ✅ Add location tracking if creating helper records
- ✅ Write tests that verify AST structure, not just successful parsing

**DON'T:**
- ❌ Add mutable properties (use `init`-only setters)
- ❌ Add logic to AST nodes (they're data structures, not behavior)
- ❌ Create deep inheritance hierarchies (keep it simple)
- ❌ Break immutability guarantees
- ❌ Skip tests when adding new expression types

### 8.4 Testing New Expressions

**Parser tests** (in `Sharpy.Compiler.Tests/Parser/`):
```csharp
[Fact]
public void TestParseNewExpression()
{
    var source = "a @ b";  // Your new syntax
    var parser = new Parser(source);
    var module = parser.Parse();
    
    var expr = Assert.IsType<ExpressionStatement>(module.Body[0]);
    var matMul = Assert.IsType<MatrixMultiply>(expr.Expression);
    Assert.IsType<Identifier>(matMul.Left);
    Assert.IsType<Identifier>(matMul.Right);
}
```

**Semantic tests** (in `Sharpy.Compiler.Tests/Semantic/`):
```csharp
[Fact]
public void TestTypeCheckMatrixMultiply()
{
    var source = """
        a: Matrix = Matrix()
        b: Matrix = Matrix()
        c = a @ b
        """;
    // Verify type inference and validation
}
```

**Integration tests** (in `Sharpy.Compiler.Tests/Integration/`):
```csharp
[Fact]
public void CompileMatrixMultiply()
{
    var source = "result = matrix1 @ matrix2";
    var assembly = Compile(source);
    Assert.NotNull(assembly);
}
```

### 8.5 Common Modifications

**Scenarios you might encounter:**

1. **Adding operator support:**
   - Add to appropriate `Operator` enum
   - No new expression type needed (reuse `BinaryOp`/`UnaryOp`)
   - Update parser operator table
   - Update code generator operator mapping

2. **Supporting new literal formats:**
   - Add properties to existing literal types (e.g., new suffix)
   - Or create new literal type if semantically different
   - Update lexer to recognize the format
   - Update parser to construct the node

3. **Extending comprehensions:**
   - Add new clause types to `ComprehensionClause` hierarchy
   - Update parser comprehension logic
   - Handle in semantic analysis and code generation

4. **Adding Python compatibility:**
   - Research Python's AST structure for the feature
   - Mirror the structure where reasonable
   - Document differences in code comments

---

## 9. Relationship to Python AST

Sharpy's expression AST is **heavily inspired by Python's AST module**, with adaptations for .NET and static typing:

### Similarities to Python:
- Comprehension structure (for/if clauses)
- Comparison chains (`a < b < c`)
- F-string representation (parts with text/expressions)
- Conditional expressions (ternary)
- Distinction between expression and statement

### Differences from Python:
- **Static typing**: Type annotations are first-class (not comments)
- **Null safety**: Explicit null-conditional operators (`?.`)
- **Null coalescing**: C#-style `??` operator
- **Immutability**: Records vs. Python's mutable AST nodes
- **.NET integration**: Type casting and type checking operators

### Why this matters:
- **Python developers** will find the structure familiar
- **Language design**: Following Python conventions reduces surprises
- **Tooling**: Similar structure means similar analysis techniques

---

## 10. Further Reading

### Related Files to Explore:
- **`Node.cs`**: Base AST node with location tracking
- **`Statement.cs`**: Statement and declaration AST nodes
- **`Types.cs`**: Type annotation structures
- **`Parser.cs`**: How these expressions are constructed from tokens
- **`RoslynEmitter.cs`**: How expressions become C# code

### Documentation:
- **Language Reference**: `docs/specs/language_reference.md`
- **Parser Architecture**: `docs/architecture/parser.md`
- **Compiler Guide**: `.github/instructions/Sharpy.Compiler/HOW_TO_CONTRIBUTE.instructions.md`

### Example Usage:
- **Parser Tests**: `src/Sharpy.Compiler.Tests/Parser/ExpressionTests.cs`
- **Code Generation Tests**: `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterTests.cs`
- **Samples**: `samples/` and `snippets/` directories

---

## Summary

`Expression.cs` is the **foundation of how Sharpy represents executable code** during compilation. Understanding this file is crucial for:
- **Implementing new language features**
- **Debugging parser or semantic analyzer issues**
- **Understanding code generation**
- **Writing compiler tests**

The design emphasizes **immutability, clarity, and Pythonic structure** while integrating seamlessly with .NET's type system. Every expression you write in Sharpy becomes one of these AST nodes before being transformed into executable .NET code.

**Key takeaway:** These are data structures, not behavior. They represent "what the code says," not "what the code does." The behavior comes from the semantic analyzer and code generator that process these nodes.
