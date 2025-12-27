# Walkthrough: Expression.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Expression.cs`

---

## Overview

`Expression.cs` defines the complete Abstract Syntax Tree (AST) node hierarchy for **all expressions** in the Sharpy language. This file is the foundation of how the Sharpy compiler represents executable code that produces values—everything from simple literals like `42` to complex constructs like list comprehensions `[x * 2 for x in range(10) if x > 5]`.

**Role in the compiler pipeline:**
```
Source Code → Lexer → Parser → AST (Expression.cs) → Semantic Analysis → Code Generation → C#
```

The parser (`Parser.cs`) reads tokens from the lexer and constructs these expression nodes, which are then:
- Analyzed by the semantic analyzer (`TypeChecker`, `TypeResolver`) to determine types and validate correctness
- Converted to C# code by the code generator (`RoslynEmitter`)

**Key Design Philosophy:**
- Expressions are **immutable** C# records (following functional programming principles)
- Each expression type is a distinct record inheriting from the base `Expression` class
- Location information (line/column) is inherited from `Node` for precise error reporting
- No behavior/methods on AST nodes—they're pure data structures analyzed by separate visitor/processor classes

---

## Class Structure

### Base Class

```csharp
public abstract record Expression : Node;
```

All expression nodes inherit from `Expression`, which itself inherits from `Node`. The `Node` base class (defined in `Node.cs`) provides source location tracking:

```csharp
public abstract record Node
{
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**Why this matters:** Every expression knows exactly where it came from in the source code, enabling precise error messages like "Error at line 5, column 12: Cannot add 'str' and 'int'".

---

## Expression Categories

The file is organized into 7 major categories using `#region` directives:

### 1. Literals (`#region Literals`)

Represent constant values directly written in source code.

#### IntegerLiteral
```csharp
public record IntegerLiteral : Expression
{
    public string Value { get; init; } = "";
    public string? Suffix { get; init; }  // L, U, UL, etc.
}
```

**Represents:** `42`, `1_000_000`, `42L`, `0xFF`
- `Value`: The numeric string (preserves underscores and format from source)
- `Suffix`: Type suffix for .NET interop (e.g., `L` for long, `U` for unsigned)

**Example usage:**
```python
x = 1_000_000  # IntegerLiteral(Value="1_000_000", Suffix=null)
y = 42L        # IntegerLiteral(Value="42", Suffix="L")
```

#### FloatLiteral
```csharp
public record FloatLiteral : Expression
{
    public string Value { get; init; } = "";
    public string? Suffix { get; init; }  // f, F, d, D, m, M
}
```

**Represents:** `3.14`, `3.14f`, `3.14m` (decimal)
- Suffix determines .NET type: `f` = float, `d` = double, `m` = decimal

#### StringLiteral
```csharp
public record StringLiteral : Expression
{
    public string Value { get; init; } = "";
    public bool IsRaw { get; init; }
}
```

**Represents:** `"hello"`, `'world'`, `r"C:\path"`, `"""multi-line"""`
- `IsRaw`: If true, escape sequences are not processed (like Python's raw strings)

#### FStringLiteral
```csharp
public record FStringLiteral : Expression
{
    public List<FStringPart> Parts { get; init; } = new();
}

public record FStringPart
{
    public string? Text { get; init; }
    public Expression? Expression { get; init; }
    public string? FormatSpec { get; init; }
}
```

**Represents:** Python-style f-strings with embedded expressions: `f"Hello {name}, you have {count:.2f} items"`

**How it works:**
- The string is broken into alternating text and expression parts
- Each `FStringPart` is either text (`Text != null`) or an embedded expression (`Expression != null`)
- `FormatSpec` holds the optional format specification (e.g., `.2f`, `>10`)

**Example:**
```python
f"Score: {score:.2f}"
# Parts: [
#   FStringPart(Text="Score: ", Expression=null, FormatSpec=null),
#   FStringPart(Text=null, Expression=Identifier("score"), FormatSpec=".2f")
# ]
```

#### BooleanLiteral, NoneLiteral, EllipsisLiteral
```csharp
public record BooleanLiteral : Expression { public bool Value { get; init; } }
public record NoneLiteral : Expression;
public record EllipsisLiteral : Expression;
```

- `BooleanLiteral`: `True` or `False`
- `NoneLiteral`: `None` (Python's null)
- `EllipsisLiteral`: `...` (used in slicing and type hints)

---

### 2. Collections (`#region Collections`)

Python's built-in collection literals.

#### ListLiteral
```csharp
public record ListLiteral : Expression
{
    public List<Expression> Elements { get; init; } = new();
}
```

**Represents:** `[1, 2, 3]`, `["a", "b", "c"]`, `[]`
- Simply a list of element expressions
- Elements can be any expression type (literals, variables, function calls, etc.)

#### DictLiteral
```csharp
public record DictLiteral : Expression
{
    public List<DictEntry> Entries { get; init; } = new();
}

public record DictEntry
{
    public Expression Key { get; init; } = null!;
    public Expression Value { get; init; } = null!;
}
```

**Represents:** `{"name": "Alice", "age": 30}`, `{}`
- Each entry is a key-value pair
- Both keys and values are arbitrary expressions

#### SetLiteral
```csharp
public record SetLiteral : Expression
{
    public List<Expression> Elements { get; init; } = new();
}
```

**Represents:** `{1, 2, 3}`, `{"apple", "banana"}`

**Ambiguity note:** `{}` is parsed as `DictLiteral` (empty dict), not `SetLiteral`. Use `set()` for empty sets.

#### TupleLiteral
```csharp
public record TupleLiteral : Expression
{
    public List<Expression> Elements { get; init; } = new();
}
```

**Represents:** `(1, 2, 3)`, `(1,)` (single-element tuple)
- Tuples are immutable sequences
- Parser distinguishes tuples from parenthesized expressions by comma presence

---

### 3. Comprehensions (`#region Comprehensions`)

Python's powerful collection-building syntax using for/if clauses.

#### ListComprehension
```csharp
public record ListComprehension : Expression
{
    public Expression Element { get; init; } = null!;
    public List<ComprehensionClause> Clauses { get; init; } = new();
}
```

**Represents:** `[x * 2 for x in range(10) if x > 5]`

**Structure:**
- `Element`: The expression to evaluate for each item (`x * 2`)
- `Clauses`: Sequence of `ForClause` and `IfClause` nodes

**Example breakdown:**
```python
[x * 2 for x in range(10) if x > 5]
# Element: BinaryOp(x * 2)
# Clauses: [
#   ForClause(Target=Identifier("x"), Iterator=FunctionCall(Identifier("range"), [10])),
#   IfClause(Condition=BinaryOp(x > 5))
# ]
```

#### SetComprehension, DictComprehension
Similar structure, but:
- `SetComprehension`: Creates sets `{x * 2 for x in range(10)}`
- `DictComprehension`: Has both `Key` and `Value` expressions `{x: x*2 for x in range(10)}`

#### ComprehensionClause (Abstract Base)
```csharp
public abstract record ComprehensionClause : Node;

public record ForClause : ComprehensionClause
{
    public Expression Target { get; init; } = null!;
    public Expression Iterator { get; init; } = null!;
}

public record IfClause : ComprehensionClause
{
    public Expression Condition { get; init; } = null!;
}
```

**Design note:** Clauses can appear in any order and quantity:
- `[x for x in items for y in x]` (nested loops)
- `[x for x in items if x > 0 if x < 10]` (multiple filters)

---

### 4. Primary Expressions (`#region Primary Expressions`)

The building blocks for accessing data and calling functions.

#### Identifier
```csharp
public record Identifier : Expression
{
    public string Name { get; init; } = "";
}
```

**Represents:** Variable names and function names: `x`, `my_function`, `Person`
- The semantic analyzer resolves what this name refers to (variable, function, class, etc.)

#### MemberAccess
```csharp
public record MemberAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public string Member { get; init; } = "";
    public bool IsNullConditional { get; init; }
}
```

**Represents:** 
- `obj.property` → `MemberAccess(Object=Identifier("obj"), Member="property", IsNullConditional=false)`
- `obj?.property` → `MemberAccess(Object=..., Member="property", IsNullConditional=true)`

**Null-conditional operator (`?.`)**: If `obj` is `None`, the entire expression evaluates to `None` instead of throwing.

**Chaining:** Member accesses can nest:
```python
person.address.street
# MemberAccess(
#   Object=MemberAccess(Object=Identifier("person"), Member="address"),
#   Member="street"
# )
```

#### IndexAccess
```csharp
public record IndexAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public Expression Index { get; init; } = null!;
}
```

**Represents:** `list[0]`, `dict["key"]`, `matrix[i][j]`
- `Object`: The collection being indexed
- `Index`: The index expression (integer, string, or any hashable type for dicts)

**Python semantics:** Supports negative indices: `list[-1]` (last element)

#### SliceAccess
```csharp
public record SliceAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public Expression? Start { get; init; }
    public Expression? Stop { get; init; }
    public Expression? Step { get; init; }
}
```

**Represents:** Python slice notation: `list[start:stop:step]`

**Examples:**
- `list[1:5]` → `SliceAccess(Object=..., Start=1, Stop=5, Step=null)`
- `list[::2]` → `SliceAccess(Object=..., Start=null, Stop=null, Step=2)` (every 2nd element)
- `list[::-1]` → `SliceAccess(Object=..., Start=null, Stop=null, Step=-1)` (reverse)

**All three are optional:** `list[:]` copies the entire list.

#### FunctionCall
```csharp
public record FunctionCall : Expression
{
    public Expression Function { get; init; } = null!;
    public List<Expression> Arguments { get; init; } = new();
    public List<KeywordArgument> KeywordArguments { get; init; } = new();
}

public record KeywordArgument
{
    public string Name { get; init; } = "";
    public Expression Value { get; init; } = null!;
    // ... location info
}
```

**Represents:** `print("hello")`, `max(a, b)`, `create_user(name="Alice", age=30)`

**Python calling conventions:**
- **Positional arguments:** `func(1, 2, 3)` → stored in `Arguments`
- **Keyword arguments:** `func(x=1, y=2)` → stored in `KeywordArguments`
- **Mixed:** `func(1, 2, x=3, y=4)` → positional must come before keyword

**Note:** `Function` is an `Expression`, allowing higher-order functions:
```python
get_function()(args)  # Function = FunctionCall(get_function)
```

---

### 5. Operators (`#region Operators`)

Binary and unary operations with explicit operator enums.

#### UnaryOp
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

**Represents:** `-5`, `not is_valid`, `~flags`

**Evaluation order:** The operand is evaluated first, then the operator is applied.

#### BinaryOp
```csharp
public record BinaryOp : Expression
{
    public BinaryOperator Operator { get; init; }
    public Expression Left { get; init; } = null!;
    public Expression Right { get; init; } = null!;
}

public enum BinaryOperator
{
    // Arithmetic: Add, Subtract, Multiply, Divide, FloorDivide, Modulo, Power
    // Comparison: Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual
    // Logical: And, Or
    // Bitwise: BitwiseAnd, BitwiseOr, BitwiseXor, LeftShift, RightShift
    // Membership: In, NotIn
    // Identity: Is, IsNot
    // Null coalescing: NullCoalesce
}
```

**Represents:** `a + b`, `x < y`, `a and b`, `key in dict`, `value is None`, `x ?? 0`

**Key operators:**
- **Arithmetic:** Standard math operations
- **Comparison:** Return boolean values
- **Logical:** `and`/`or` with short-circuit evaluation
- **Membership:** `in`/`not in` for testing collection membership
- **Identity:** `is`/`is not` for reference equality (especially for `None` checks)
- **Null coalescing (`??`):** Returns left side if not None, otherwise right side

**Design decision:** The `NullCoalesce` operator is .NET-inspired, not standard Python (Python uses `or` idiom).

#### ComparisonChain
```csharp
public record ComparisonChain : Expression
{
    public List<Expression> Operands { get; init; } = new();
    public List<ComparisonOperator> Operators { get; init; } = new();
}
```

**Represents:** Python's chained comparisons: `a < b < c`, `1 <= x <= 10`

**How it works:**
```python
1 < x < 10
# Operands: [1, x, 10]
# Operators: [LessThan, LessThan]
# Semantically equivalent to: (1 < x) and (x < 10)
```

**Important:** Each operand is evaluated only once (unlike the expanded form).

---

### 6. Advanced Expressions (`#region Advanced Expressions`)

Complex control flow and type-related expressions.

#### ConditionalExpression
```csharp
public record ConditionalExpression : Expression
{
    public Expression Test { get; init; } = null!;
    public Expression ThenValue { get; init; } = null!;
    public Expression ElseValue { get; init; } = null!;
}
```

**Represents:** Python's ternary operator: `value if test else other`

**Example:**
```python
result = "positive" if x > 0 else "non-positive"
# Test: BinaryOp(x > 0)
# ThenValue: StringLiteral("positive")
# ElseValue: StringLiteral("non-positive")
```

**Syntax note:** Unlike C's `test ? then : else`, Python uses `then if test else other`.

#### LambdaExpression
```csharp
public record LambdaExpression : Expression
{
    public List<Parameter> Parameters { get; init; } = new();
    public Expression Body { get; init; } = null!;
}
```

**Represents:** Anonymous functions: `lambda x, y: x + y`

**Limitation:** Lambda body must be a single expression (no statements).

**Usage:**
```python
sorted(items, key=lambda x: x.name)
# Parameters: [Parameter(Name="x")]
# Body: MemberAccess(Object=Identifier("x"), Member="name")
```

**Note:** `Parameter` is defined in `Statement.cs` and includes type annotations and default values.

#### TypeCast
```csharp
public record TypeCast : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation TargetType { get; init; } = null!;
}
```

**Represents:** Explicit type conversion: `value as int`

**Semantics:** 
- Returns `None` if cast fails (safe cast)
- Does NOT throw exception (unlike C#'s cast operator)
- Similar to C#'s `as` operator for reference types

#### TypeCheck
```csharp
public record TypeCheck : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation CheckType { get; init; } = null!;
}
```

**Represents:** Type testing: `isinstance(value, int)`, `value is MyClass`

**Type narrowing:** The semantic analyzer uses this for flow-sensitive typing:
```python
if isinstance(x, str):
    # Inside this block, x is narrowed from 'object' to 'str'
    print(x.upper())  # OK: str has upper() method
```

#### Parenthesized
```csharp
public record Parenthesized : Expression
{
    public Expression Expression { get; init; } = null!;
}
```

**Represents:** Grouping parentheses: `(x + y) * z`

**Why separate node?** Preserves the AST structure exactly as written for:
- Better error messages
- Code formatting/pretty-printing
- Maintaining programmer intent

---

## Dependencies and Integration

### Upstream Dependencies
- **`Node.cs`**: Base class providing source location tracking
- **`Types.cs`**: `TypeAnnotation` used in `TypeCast`, `TypeCheck`, `LambdaExpression`
- **`Statement.cs`**: `Parameter` used in `LambdaExpression`

### Downstream Consumers
1. **Parser (`Parser.cs`)**: Creates these expression nodes from tokens
2. **Semantic Analyzer:**
   - `NameResolver`: Binds `Identifier` nodes to their declarations
   - `TypeResolver`: Determines the type of each expression
   - `TypeChecker`: Validates type correctness and performs type narrowing
3. **Code Generator (`RoslynEmitter`)**: Converts expressions to C# Roslyn syntax trees
4. **AST Visitors**: Any code that walks the AST (e.g., `AstDumper` for debugging)

### Type System Integration

Expressions don't carry type information directly. Instead, the `SemanticInfo` class maintains a mapping:
```csharp
// In semantic analysis
SemanticInfo semanticInfo = new();
semanticInfo.SetType(expression, resolvedType);

// Later retrieval
var type = semanticInfo.GetType(expression);
```

**Why separate?** Keeps AST nodes immutable and allows multiple semantic analyses (e.g., for IDE features).

---

## Patterns and Design Decisions

### 1. **Immutability via Records**
```csharp
public record BinaryOp : Expression { ... }
```

C# records provide:
- Structural equality by value
- Immutability (init-only properties)
- Copy-with syntax: `expr with { Operator = BinaryOperator.Add }`

**Benefit:** Safe to share AST nodes across threads; no defensive copying needed.

### 2. **Explicit Operator Enums**
Rather than storing operators as strings (`"+"`, `"-"`), Sharpy uses typed enums:
```csharp
public enum BinaryOperator { Add, Subtract, ... }
```

**Benefits:**
- Type safety (no typos)
- Easy pattern matching
- IDE autocomplete
- Efficient switch statements in code generation

### 3. **Null-Aware Properties**
```csharp
public Expression? Start { get; init; }  // Nullable for optional slice bounds
public string? Suffix { get; init; }     // Nullable for optional suffixes
```

Sharpy uses C# nullable reference types to encode optionality at the type level.

### 4. **Composite Pattern**
Expressions form a tree structure where:
- Leaf nodes: Literals, Identifiers
- Internal nodes: BinaryOp, FunctionCall, MemberAccess

This enables recursive traversal:
```csharp
void Visit(Expression expr)
{
    switch (expr)
    {
        case BinaryOp binOp:
            Visit(binOp.Left);
            Visit(binOp.Right);
            break;
        case FunctionCall call:
            Visit(call.Function);
            foreach (var arg in call.Arguments)
                Visit(arg);
            break;
        // ... more cases
    }
}
```

### 5. **Location Tracking**
Every expression knows its source position:
```csharp
throw new CompilerError(
    $"Cannot add {leftType} and {rightType}",
    expr.LineStart, expr.ColumnStart
);
```

**User experience:** Precise error messages like:
```
Error at line 42, column 15: Type mismatch
    result = "hello" + 5
                      ^
```

---

## Debugging Tips

### 1. **Visualizing the AST**
Use `AstDumper` to see the structure:
```csharp
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(expression));
```

Example output:
```
BinaryOp (Add)
├─ Left: IntegerLiteral (42)
└─ Right: IntegerLiteral (8)
```

### 2. **Breakpoint Locations**
When debugging parser issues:
- Set breakpoints in `Parser.cs` methods like `ParseExpression()`, `ParsePrimary()`
- Watch the `_currentToken` field to see what the parser is consuming
- Check that the created AST node matches expectations

When debugging semantic issues:
- Set breakpoints in `TypeChecker.cs`'s `Visit(Expression)` methods
- Inspect the `semanticInfo.GetType(expr)` to see resolved types
- Check `_narrowedTypes` dictionary for type narrowing state

### 3. **Common Mistakes**

**Forgetting to set location info:**
```csharp
// BAD: No location info
return new BinaryOp { Operator = op, Left = left, Right = right };

// GOOD: Copy location from token
return new BinaryOp 
{ 
    Operator = op, 
    Left = left, 
    Right = right,
    LineStart = startToken.LineStart,
    ColumnStart = startToken.ColumnStart,
    LineEnd = endToken.LineEnd,
    ColumnEnd = endToken.ColumnEnd
};
```

**Confusing expression types:**
- `{}` is `DictLiteral` (empty dict), NOT `SetLiteral`
- `(x)` is `Parenthesized`, but `(x,)` is `TupleLiteral`
- `value is Type` can be `TypeCheck` OR `BinaryOp` with `Is` operator (check context)

### 4. **Testing Expressions**
Write parser tests for each expression type:
```csharp
[Fact]
public void TestParseBinaryOp()
{
    var parser = new Parser("2 + 3");
    var module = parser.Parse();
    var expr = ((ExpressionStatement)module.Body[0]).Expression;
    
    Assert.IsType<BinaryOp>(expr);
    var binOp = (BinaryOp)expr;
    Assert.Equal(BinaryOperator.Add, binOp.Operator);
    Assert.IsType<IntegerLiteral>(binOp.Left);
    Assert.IsType<IntegerLiteral>(binOp.Right);
}
```

---

## Contribution Guidelines

### Adding a New Expression Type

**Example:** Adding support for the walrus operator `:=` (assignment expression)

1. **Define the AST node:**
```csharp
/// <summary>
/// Assignment expression (walrus operator) - (x := value)
/// </summary>
public record AssignmentExpression : Expression
{
    public Identifier Target { get; init; } = null!;
    public Expression Value { get; init; } = null!;
}
```

2. **Update the parser:**
- Add token recognition in `Lexer.cs`: `TokenType.ColonEqual`
- Add parsing logic in `Parser.cs`: `ParseAssignmentExpression()`
- Determine operator precedence (walrus is low precedence)

3. **Add semantic analysis:**
- In `TypeChecker.cs`, add visitor method:
```csharp
protected override void Visit(AssignmentExpression assignExpr)
{
    Visit(assignExpr.Value);
    var valueType = semanticInfo.GetType(assignExpr.Value);
    
    // Register the target variable
    symbolTable.Define(assignExpr.Target.Name, valueType);
    
    // The expression itself evaluates to the assigned value
    semanticInfo.SetType(assignExpr, valueType);
}
```

4. **Update code generation:**
- In `RoslynEmitter.cs`, add:
```csharp
protected override SyntaxNode VisitAssignmentExpression(AssignmentExpression node)
{
    // C# doesn't have walrus operator, emit as variable declaration + value
    var value = Visit(node.Value);
    var assignment = SyntaxFactory.AssignmentExpression(
        SyntaxKind.SimpleAssignmentExpression,
        SyntaxFactory.IdentifierName(node.Target.Name),
        value
    );
    return assignment;
}
```

5. **Add tests:**
```csharp
// In Sharpy.Compiler.Tests/Parser/ExpressionTests.cs
[Fact]
public void TestParseWalrusOperator()
{
    var parser = new Parser("if (x := get_value()) > 0:");
    // ... assertions
}

// In Sharpy.Compiler.Tests/Integration/WalrusOperatorTests.cs
[Fact]
public void TestWalrusOperatorExecution()
{
    var source = @"
def process():
    if (x := compute()) is not None:
        return x * 2
    return 0
";
    var result = CompileAndExecute(source);
    Assert.Equal("42\n", result.StandardOutput);
}
```

### Modifying Existing Expression Types

**Before modifying:**
- Check if the change affects semantic analysis or code generation
- Search for all uses: `grep -r "TypeCheck" src/`
- Run existing tests: `dotnet test --filter "FullyQualifiedName~TypeCheck"`

**After modifying:**
- Update related documentation in `docs/specs/`
- Ensure all tests pass
- Update integration tests if behavior changed

### Expression Design Checklist

When adding or modifying expression types:

- [ ] Does it have proper location tracking (inherited from `Node`)?
- [ ] Are all properties init-only (immutable)?
- [ ] Does it have XML documentation comments?
- [ ] Does the parser create it correctly?
- [ ] Does semantic analysis handle it?
- [ ] Does code generation emit correct C#?
- [ ] Are there unit tests for parsing?
- [ ] Are there integration tests for execution?
- [ ] Does it match Python semantics (verify with `python3 -c "..."`)?

### Common Extension Points

**Adding operator support:**
1. Add to `BinaryOperator` or `UnaryOperator` enum
2. Update parser's operator precedence table
3. Add semantic rules in `TypeChecker`
4. Map to C# operator in `RoslynEmitter`

**Adding collection syntax:**
1. Create new record type (e.g., `MapLiteral`)
2. Add parsing in `ParsePrimary()` or `ParseCollectionLiteral()`
3. Ensure semantic analyzer knows the result type
4. Generate appropriate C# initialization code

**Adding comprehension support:**
1. Extend `ComprehensionClause` with new clause type
2. Update parser's comprehension parsing
3. Handle in code generation (often becomes LINQ or loop)

---

## Quick Reference

### Expression Type Cheat Sheet

| Sharpy Syntax | AST Node | Example |
|---------------|----------|---------|
| `42` | `IntegerLiteral` | `IntegerLiteral(Value="42")` |
| `3.14` | `FloatLiteral` | `FloatLiteral(Value="3.14")` |
| `"hello"` | `StringLiteral` | `StringLiteral(Value="hello")` |
| `f"{x}"` | `FStringLiteral` | `FStringLiteral(Parts=[...])` |
| `True` | `BooleanLiteral` | `BooleanLiteral(Value=true)` |
| `None` | `NoneLiteral` | `NoneLiteral()` |
| `[1, 2]` | `ListLiteral` | `ListLiteral(Elements=[...])` |
| `{"a": 1}` | `DictLiteral` | `DictLiteral(Entries=[...])` |
| `{1, 2}` | `SetLiteral` | `SetLiteral(Elements=[...])` |
| `(1, 2)` | `TupleLiteral` | `TupleLiteral(Elements=[...])` |
| `[x for x in items]` | `ListComprehension` | `ListComprehension(Element=..., Clauses=[...])` |
| `x` | `Identifier` | `Identifier(Name="x")` |
| `obj.attr` | `MemberAccess` | `MemberAccess(Object=..., Member="attr")` |
| `list[0]` | `IndexAccess` | `IndexAccess(Object=..., Index=...)` |
| `list[1:5]` | `SliceAccess` | `SliceAccess(Object=..., Start=..., Stop=...)` |
| `func(a, b)` | `FunctionCall` | `FunctionCall(Function=..., Arguments=[...])` |
| `-x` | `UnaryOp` | `UnaryOp(Operator=Minus, Operand=...)` |
| `a + b` | `BinaryOp` | `BinaryOp(Operator=Add, Left=..., Right=...)` |
| `a < b < c` | `ComparisonChain` | `ComparisonChain(Operands=[...], Operators=[...])` |
| `x if test else y` | `ConditionalExpression` | `ConditionalExpression(Test=..., ThenValue=..., ElseValue=...)` |
| `lambda x: x*2` | `LambdaExpression` | `LambdaExpression(Parameters=[...], Body=...)` |
| `value as int` | `TypeCast` | `TypeCast(Value=..., TargetType=...)` |
| `isinstance(x, str)` | `TypeCheck` | `TypeCheck(Value=..., CheckType=...)` |

### Related Files

- **`Node.cs`**: Base class with location tracking
- **`Statement.cs`**: Statement AST nodes, `Parameter` definition
- **`Types.cs`**: Type annotation nodes
- **`Parser.cs`**: Creates expression nodes from tokens
- **`Lexer.cs`**: Tokenizes source code
- **`TypeChecker.cs`**: Validates and infers expression types
- **`RoslynEmitter.cs`**: Converts expressions to C# code

---

## Further Reading

- **Architecture doc**: `docs/architecture/semantic-analyzer-architecture.md` - How semantic analysis uses these nodes
- **Type system spec**: `docs/specs/type_system.md` - Type rules for expressions
- **Parser design**: `docs/architecture/parser-design.md` - How expressions are parsed
- **Samples**: `snippets/*.spy` - Example Sharpy programs to see expressions in action

---

**Welcome to the Sharpy compiler team!** This file is the heart of representing executable code. Understanding these expression types will help you work on any part of the compiler pipeline. When in doubt, look at how Python does it (`python3 -c "..."`), check the tests, and don't hesitate to add debug output with `AstDumper`.
