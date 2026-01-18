# Walkthrough: Expression.cs

**Source File**: `src/Sharpy.Compiler/Parser/Ast/Expression.cs`

---

## 1. Overview

`Expression.cs` defines the complete Abstract Syntax Tree (AST) node hierarchy for **all expressions** in the Sharpy language. This file is the foundation of how the Sharpy compiler represents executable code that produces values—everything from simple literals like `42` to complex constructs like list comprehensions `[x * 2 for x in range(10) if x > 5]`.

**Role in the compiler pipeline:**
```
Source Code → Lexer → Parser → AST (Expression.cs) → Semantic Analysis → Code Generation → C#
                                    ↑
                            Expression.cs lives here
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

## 2. Class/Type Structure

### 2.1 Base Class

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

### 2.2 Expression Taxonomy

The file is organized into 6 major categories using `#region` directives:

```
Expression (abstract)
├── Literals
│   ├── IntegerLiteral      (42, 1_000, 42L)
│   ├── FloatLiteral        (3.14, 3.14f, 3.14m)
│   ├── StringLiteral       ("hello", r"C:\path", """multiline""")
│   ├── FStringLiteral      (f"Hello {name}")
│   ├── BooleanLiteral      (True, False)
│   ├── NoneLiteral         (None)
│   └── EllipsisLiteral     (...)
│
├── Collections
│   ├── ListLiteral         ([1, 2, 3])
│   ├── DictLiteral         ({"a": 1, "b": 2})
│   ├── SetLiteral          ({1, 2, 3})
│   └── TupleLiteral        ((1, 2, 3))
│
├── Comprehensions
│   ├── ListComprehension   ([x*2 for x in items if x > 0])
│   ├── SetComprehension    ({x*2 for x in items})
│   └── DictComprehension   ({k: v for k, v in items})
│
├── Primary Expressions
│   ├── Identifier          (variable_name)
│   ├── MemberAccess        (obj.field, obj?.field)
│   ├── IndexAccess         (arr[0])
│   ├── SliceAccess         (arr[1:3], arr[::2])
│   └── FunctionCall        (func(a, b, key=val))
│
├── Operators
│   ├── UnaryOp             (+x, -x, not x, ~x)
│   ├── BinaryOp            (a + b, a and b, a |> func, a ?? b)
│   └── ComparisonChain     (a < b < c)
│
└── Advanced Expressions
    ├── ConditionalExpression (x if cond else y)
    ├── LambdaExpression      (lambda x: x * 2)
    ├── TypeCast              (value as Type)
    ├── TypeCoercion          (value to Type)
    ├── TypeCheck             (value is Type)
    ├── Parenthesized         ((expr))
    ├── SuperExpression       (super())
    ├── WalrusExpression      (name := value)
    ├── TryExpression         (try expr, try[ValueError] expr)
    └── MaybeExpression       (maybe expr)
```

---

## 3. Key Functions/Methods

Since AST nodes are pure data structures (C# records), this file doesn't contain traditional methods. Instead, the "functions" are the record constructors and the implicit `with` expressions for creating modified copies.

### 3.1 Record Construction Patterns

**How nodes are created (in Parser.cs):**

```csharp
// Creating a binary operation node
var binaryOp = new BinaryOp
{
    Operator = BinaryOperator.Add,
    Left = leftExpression,
    Right = rightExpression,
    LineStart = leftExpression.LineStart,
    ColumnStart = leftExpression.ColumnStart,
    LineEnd = rightExpression.LineEnd,
    ColumnEnd = rightExpression.ColumnEnd
};
```

**Key parameters for each node type:**

| Node Type | Required Properties | Optional Properties |
|-----------|-------------------|-------------------|
| `IntegerLiteral` | `Value` (string) | `Suffix` (L, U, UL) |
| `StringLiteral` | `Value` (string) | `IsRaw` (bool) |
| `FStringLiteral` | `Parts` (list) | — |
| `BinaryOp` | `Operator`, `Left`, `Right` | — |
| `FunctionCall` | `Function` | `Arguments`, `KeywordArguments` |
| `SliceAccess` | `Object` | `Start`, `Stop`, `Step` |
| `TypeCast` | `Value`, `TargetType` | — |
| `TypeCoercion` | `Value`, `TargetType` | — |
| `TryExpression` | `Operand` | `ExceptionType` |
| `MaybeExpression` | `Operand` | — |
| `WalrusExpression` | `Target`, `Value` | — |

### 3.2 Consuming Expressions Downstream

**In Semantic Analysis (TypeChecker.cs):**
```csharp
SemanticType CheckExpression(Expression expr) => expr switch
{
    IntegerLiteral lit => ResolveIntegerType(lit),
    BinaryOp binOp => CheckBinaryOperation(binOp),
    FunctionCall call => CheckFunctionCall(call),
    TypeCoercion coercion => CheckTypeCoercion(coercion),
    TryExpression tryExpr => CheckTryExpression(tryExpr),
    MaybeExpression maybeExpr => CheckMaybeExpression(maybeExpr),
    WalrusExpression walrus => CheckWalrusExpression(walrus),
    // ... ~28 more cases
};
```

**In Code Generation (RoslynEmitter.cs):**
```csharp
ExpressionSyntax EmitExpression(Expression expr) => expr switch
{
    IntegerLiteral lit => SyntaxFactory.LiteralExpression(...),
    BinaryOp binOp => EmitBinaryOp(binOp),
    ComparisonChain chain => EmitComparisonChain(chain),
    TryExpression tryExpr => EmitTryExpression(tryExpr),
    // Maps each Sharpy expression to Roslyn C# syntax
};
```

---

## 4. Detailed Type Reference

### 4.1 Literals (`#region Literals`)

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

#### FloatLiteral
```csharp
public record FloatLiteral : Expression
{
    public string Value { get; init; } = "";
    public string? Suffix { get; init; }  // f, F, d, D, m, M
}
```

**Represents:** `3.14`, `3.14f`, `3.14m` (decimal)

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

**Represents:** Python-style f-strings: `f"Hello {name}, you have {count:.2f} items"`

**How it works:**
- The string is broken into alternating text and expression parts
- Each `FStringPart` is either text (`Text != null`) or an embedded expression (`Expression != null`)
- `FormatSpec` holds the optional format specification (e.g., `.2f`, `>10`)

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

### 4.2 Collections (`#region Collections`)

#### ListLiteral, SetLiteral, TupleLiteral
```csharp
public record ListLiteral : Expression
{
    public List<Expression> Elements { get; init; } = new();
}
// SetLiteral and TupleLiteral have identical structure
```

**Represents:** `[1, 2, 3]`, `{1, 2, 3}`, `(1, 2, 3)`

**Disambiguation:** `{}` is parsed as `DictLiteral` (empty dict), not `SetLiteral`. Use `set()` for empty sets.

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

---

### 4.3 Comprehensions (`#region Comprehensions`)

#### ListComprehension, SetComprehension, DictComprehension
```csharp
public record ListComprehension : Expression
{
    public Expression Element { get; init; } = null!;
    public List<ComprehensionClause> Clauses { get; init; } = new();
}

public record DictComprehension : Expression
{
    public Expression Key { get; init; } = null!;
    public Expression Value { get; init; } = null!;
    public List<ComprehensionClause> Clauses { get; init; } = new();
}

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

**Example breakdown:**
```python
[x * 2 for x in range(10) if x > 5]
# Element: BinaryOp(x * 2)
# Clauses: [
#   ForClause(Target=Identifier("x"), Iterator=FunctionCall(range, [10])),
#   IfClause(Condition=BinaryOp(x > 5))
# ]
```

---

### 4.4 Primary Expressions (`#region Primary Expressions`)

#### Identifier
```csharp
public record Identifier : Expression
{
    public string Name { get; init; } = "";
}
```

**Represents:** Variable and function names: `x`, `my_function`, `Person`

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
- `obj.property` → `IsNullConditional=false`
- `obj?.property` → `IsNullConditional=true` (short-circuits to `None` if object is null)

#### IndexAccess and SliceAccess
```csharp
public record IndexAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public Expression Index { get; init; } = null!;
}

public record SliceAccess : Expression
{
    public Expression Object { get; init; } = null!;
    public Expression? Start { get; init; }
    public Expression? Stop { get; init; }
    public Expression? Step { get; init; }
}
```

**Examples:**
- `list[0]` → `IndexAccess`
- `list[1:5]` → `SliceAccess(Start=1, Stop=5, Step=null)`
- `list[::-1]` → `SliceAccess(Start=null, Stop=null, Step=-1)` (reverse)

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
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
}
```

**Note:** `Function` is an `Expression`, allowing higher-order calls like `get_handler()(args)`.

---

### 4.5 Operators (`#region Operators`)

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
    // Arithmetic
    Add, Subtract, Multiply, Divide, FloorDivide, Modulo, Power,

    // Comparison
    Equal, NotEqual, LessThan, LessThanOrEqual, GreaterThan, GreaterThanOrEqual,

    // Logical
    And, Or,

    // Bitwise
    BitwiseAnd, BitwiseOr, BitwiseXor, LeftShift, RightShift,

    // Membership and Identity
    In, NotIn, Is, IsNot,

    // Null coalescing
    NullCoalesce,   // a ?? b

    // Pipe forward
    PipeForward     // a |> func
}
```

**Key operators:**
- **`NullCoalesce` (`??`)**: Returns left side if not None, otherwise right side
- **`PipeForward` (`|>`)**: Passes left operand as first argument to right function: `data |> filter(pred) |> map(transform)`

#### ComparisonChain
```csharp
public record ComparisonChain : Expression
{
    public List<Expression> Operands { get; init; } = new();
    public List<ComparisonOperator> Operators { get; init; } = new();
}

public enum ComparisonOperator
{
    Equal, NotEqual, LessThan, LessThanOrEqual,
    GreaterThan, GreaterThanOrEqual, In, NotIn, Is, IsNot
}
```

**Represents:** Python's chained comparisons: `1 < x < 10`
- Semantically equivalent to `(1 < x) and (x < 10)` but `x` is evaluated only once

---

### 4.6 Advanced Expressions (`#region Advanced Expressions`)

#### ConditionalExpression
```csharp
public record ConditionalExpression : Expression
{
    public Expression Test { get; init; } = null!;
    public Expression ThenValue { get; init; } = null!;
    public Expression ElseValue { get; init; } = null!;
}
```

**Represents:** Python's ternary: `value if test else other`

#### LambdaExpression
```csharp
public record LambdaExpression : Expression
{
    public List<Parameter> Parameters { get; init; } = new();
    public Expression Body { get; init; } = null!;
}
```

**Represents:** `lambda x, y: x + y`
- `Parameter` is defined in `Statement.cs` and includes type annotations and default values

#### TypeCast, TypeCoercion, TypeCheck
```csharp
public record TypeCast : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation TargetType { get; init; } = null!;
}

public record TypeCoercion : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation TargetType { get; init; } = null!;
}

public record TypeCheck : Expression
{
    public Expression Value { get; init; } = null!;
    public TypeAnnotation CheckType { get; init; } = null!;
}
```

**Key distinction between `TypeCast` and `TypeCoercion`:**

| Feature | `TypeCast` (`as`) | `TypeCoercion` (`to`) |
|---------|-------------------|----------------------|
| Syntax | `value as Type` | `value to Type` |
| On failure (non-nullable) | Returns `None` | Throws `InvalidCastException` |
| On failure (nullable `T?`) | Returns `None` | Returns `None` |
| Use case | Safe/optional casts | Assertive conversions |

See `docs/language_specification/type_casting.md` for detailed semantics.

#### Parenthesized
```csharp
public record Parenthesized : Expression
{
    public Expression Expression { get; init; } = null!;
}
```

**Represents:** `(x + y) * z`
- Preserves AST structure exactly as written for better error messages and code formatting

#### SuperExpression
```csharp
public record SuperExpression : Expression;
```

**Represents:** `super()`

Provides access to the parent class. Can only be used in specific contexts:
- `__init__` methods to call `super().__init__(...)`
- Dunder methods to call `super().__any_dunder__(...)`
- `@override` methods to call `super().method(...)`

**Example:**
```python
class Dog(Animal):
    def __init__(self, name: str):
        super().__init__(name)  # Call parent constructor

    @override
    def speak(self) -> str:
        return super().speak() + " Woof!"  # Extend parent behavior
```

#### WalrusExpression
```csharp
public record WalrusExpression : Expression
{
    public string Target { get; init; } = "";
    public Expression Value { get; init; } = null!;
}
```

**Represents:** `name := value`

Assigns value to name and returns the value, enabling assignment within expressions.

**Example:**
```python
# Capture value in conditional
if (match := pattern.search(text)) is not None:
    print(f"Found at {match.start()}")

# Reuse computed value in comprehension
results = [y for x in data if (y := transform(x)) is not None]
```

**Important:** Variables assigned with `:=` inside comprehensions are **local to the comprehension** (unlike Python 3.8+). See `docs/language_specification/walrus_operator.md`.

#### TryExpression
```csharp
public record TryExpression : Expression
{
    public Expression Operand { get; init; } = null!;
    public TypeAnnotation? ExceptionType { get; init; }
}
```

**Represents:** `try expr` or `try[ExceptionType] expr`

Wraps an expression in `Result[T, E]` where `E` is the exception type. If the expression raises an exception of type `E`, the result holds its `Err` case.

**Examples:**
```python
# Generic exception handling
x = try int("some string")  # x: Result[int, Exception]

# Specific exception type
y = try[ValueError] int("bad")  # y: Result[int, ValueError]

# Safe type coercion
z = try my_dog to Cat  # z: Result[Cat, InvalidCastException]
```

**Precedence:** Very low precedence—captures entire following expression. See `docs/language_specification/try_expressions.md`.

#### MaybeExpression
```csharp
public record MaybeExpression : Expression
{
    public Expression Operand { get; init; } = null!;
}
```

**Represents:** `maybe expr`

Wraps a nullable expression in `Optional[T]`. If the expression is `None`, the result holds its `Nothing` case.

**Example:**
```python
d: dict[str, int] = {"y": 5}
x = maybe d.get("x")  # x: Optional[int]
```

**Type requirement:** The expression **must** return a nullable type (`T?`), otherwise it's a type-checking error.

**Precedence:** Very low precedence—captures entire following expression. See `docs/language_specification/maybe_expressions.md`.

---

## 5. Dependencies

### 5.1 Internal Dependencies

| File | Types Used |
|------|------------|
| `Node.cs` | `Node` base class (location tracking) |
| `Types.cs` | `TypeAnnotation` (for `TypeCast`, `TypeCoercion`, `TypeCheck`, `TryExpression`) |
| `Statement.cs` | `Parameter` (for `LambdaExpression`) |

### 5.2 Downstream Consumers

| Consumer | How It Uses Expressions |
|----------|------------------------|
| **Parser** (`Parser.cs`) | Creates all expression nodes from tokens |
| **NameResolver** (`Semantic/NameResolver.cs`) | Resolves `Identifier` nodes to declarations |
| **TypeChecker** (`Semantic/TypeChecker.cs`) | Validates types, infers expression types |
| **RoslynEmitter** (`CodeGen/RoslynEmitter.cs`) | Converts to C# Roslyn syntax trees |
| **AstDumper** (`Parser/AstDumper.cs`) | Debug visualization of AST |

### 5.3 Type System Integration

Expressions don't carry type information directly. The `SemanticInfo` class maintains a separate mapping:
```csharp
// In semantic analysis
semanticInfo.SetType(expression, resolvedType);

// Later retrieval
var type = semanticInfo.GetType(expression);
```

**Why separate?** Keeps AST nodes immutable and allows multiple semantic analyses (e.g., for IDE features).

---

## 6. Patterns and Design Decisions

### 6.1 Immutability via Records
```csharp
public record BinaryOp : Expression { ... }
```

C# records provide:
- Structural equality by value
- Immutability (init-only properties)
- Copy-with syntax: `expr with { Operator = BinaryOperator.Add }`

### 6.2 Explicit Operator Enums

Rather than storing operators as strings, Sharpy uses typed enums:
```csharp
public enum BinaryOperator { Add, Subtract, ... }
```

**Benefits:** Type safety, IDE autocomplete, efficient switch statements in code generation.

### 6.3 The `= null!` Pattern

Throughout the file you'll see:
```csharp
public Expression Left { get; init; } = null!;
```

This tells the compiler "trust me, this will be initialized." The parser always sets these via object initializers.

### 6.4 Operator Precedence Encoding

The AST structure **implicitly encodes precedence** through nesting. For `1 + 2 * 3`:
```
BinaryOp (Add)
├── Left: IntegerLiteral(1)
└── Right: BinaryOp (Multiply)
           ├── Left: IntegerLiteral(2)
           └── Right: IntegerLiteral(3)
```

The parser handles precedence during construction. See `docs/language_specification/operator_precedence.md` for the full precedence table.

### 6.5 Helper Records Outside Node Hierarchy

Several helper records don't inherit from `Node`:
- `FStringPart` - component of f-strings
- `DictEntry` - key-value pair in dictionaries
- `KeywordArgument` - named argument in function calls (but does track its own location)
- `ComprehensionClause` - base for `ForClause` and `IfClause` (inherits from `Node`)

---

## 7. Debugging Tips

### 7.1 Visualizing the AST

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

### 7.2 Common Issues

**Issue: "Null reference on expression property"**
- **Cause:** Parser created node without setting required property
- **Debug:** Check parser code that creates this node type

**Issue: "Unexpected expression type in semantic analysis"**
- **Cause:** Missing case in pattern match
- **Fix:** Search for `switch (expr)` and add the missing case

**Issue: "Wrong operator precedence"**
- **Debug:** Dump the AST and verify tree structure
- **Cause:** Parser's precedence climbing has wrong precedence level

**Issue: Confusing `TypeCast` vs `TypeCoercion`:**
- `as` → `TypeCast` (safe, returns None on failure)
- `to` → `TypeCoercion` (assertive, throws on failure)

**Issue: `TryExpression` or `MaybeExpression` capturing too much:**
- **Cause:** These have very low precedence and capture entire expressions
- **Fix:** Use parentheses to limit scope: `(try foo()) + bar()`

### 7.3 Useful Search Commands

```bash
# Find where a specific expression type is created:
grep -r "new BinaryOp" src/Sharpy.Compiler/Parser/

# Find where an expression type is handled:
grep -r "case BinaryOp" src/Sharpy.Compiler/

# Find all expression pattern matching:
grep -r "switch.*Expression" src/Sharpy.Compiler/

# Find uses of new expression types:
grep -r "TryExpression\|MaybeExpression\|WalrusExpression" src/
```

---

## 8. Contribution Guidelines

### 8.1 Adding a New Expression Type

1. **Define the AST node** in the appropriate `#region`:
   ```csharp
   public record AwaitExpression : Expression
   {
       public Expression Awaitable { get; init; } = null!;
   }
   ```

2. **Update Parser** (`Parser.cs`):
   - Add parsing logic at appropriate precedence level
   - Create node with all properties and locations

3. **Update Type Checker** (`Semantic/TypeChecker.cs`):
   - Add case to expression type checking
   - Implement type inference rules

4. **Update Code Generator** (`CodeGen/RoslynEmitter.cs`):
   - Add case to emit as C# code

5. **Update AST Dumper** (`Parser/AstDumper.cs`):
   - Add formatting for debug output

6. **Add Tests**:
   - Parser test: verify AST structure
   - Semantic test: verify type checking
   - CodeGen test: verify generated C#
   - Integration test: end-to-end compilation

7. **Update Documentation**:
   - Add language specification document if it's a new feature
   - Update this walkthrough

### 8.2 Adding a New Operator

1. Add to `BinaryOperator` or `UnaryOperator` enum
2. Add lexer token (if new syntax)
3. Update `ParseBinaryExpression()` with precedence
4. Handle in `TypeChecker` for operand type validation
5. Handle in `RoslynEmitter` for C# code generation
6. Update `docs/language_specification/operator_precedence.md`

### 8.3 What NOT to Do

- **Don't add semantic info to nodes:**
  ```csharp
  // BAD:
  public Symbol? ResolvedSymbol { get; init; }  // Use SemanticInfo instead
  ```

- **Don't add behavior to nodes:**
  ```csharp
  // BAD:
  public object Evaluate() { ... }  // Nodes are pure data
  ```

- **Don't use mutable properties:**
  ```csharp
  // BAD:
  public List<Expression> Elements { get; set; }  // Use 'init'
  ```

---

## 9. Cross-References

### Related AST Files
| File | Purpose |
|------|---------|
| [`Node.cs`](Node.md) | Base `Node` class with location tracking |
| [`Statement.cs`](Statement.md) | Statement nodes + `Parameter` record |
| [`Types.cs`](Types.md) | `TypeAnnotation` for type-related expressions |

### Implementation Files
| File | Purpose |
|------|---------|
| [`Parser.cs`](../Parser.md) | Creates expression nodes |
| [`TypeChecker.cs`](../../Semantic/TypeChecker.md) | Type-checks expressions |
| [`RoslynEmitter.cs`](../../CodeGen/RoslynEmitter.md) | Generates C# from expressions |
| [`AstDumper.cs`](../AstDumper.md) | Debug visualization |

### Language Specification
- [`expressions.md`](../../../../language_specification/expressions.md) - Expression syntax and semantics
- [`operator_precedence.md`](../../../../language_specification/operator_precedence.md) - Complete precedence table
- [`try_expressions.md`](../../../../language_specification/try_expressions.md) - Try expression semantics
- [`maybe_expressions.md`](../../../../language_specification/maybe_expressions.md) - Maybe expression semantics
- [`walrus_operator.md`](../../../../language_specification/walrus_operator.md) - Walrus operator (`:=`) semantics
- [`type_casting.md`](../../../../language_specification/type_casting.md) - Type cast vs coercion
- [`comprehensions.md`](../../../../language_specification/comprehensions.md) - List/set/dict comprehensions
- [`fstrings.md`](../../../../language_specification/fstrings.md) - F-string formatting

---

## 10. Quick Reference

### Expression Type Cheat Sheet

| Sharpy Syntax | AST Node |
|---------------|----------|
| `42`, `1_000L` | `IntegerLiteral` |
| `3.14`, `3.14f` | `FloatLiteral` |
| `"hello"`, `r"raw"` | `StringLiteral` |
| `f"{x}"` | `FStringLiteral` |
| `True`, `False` | `BooleanLiteral` |
| `None` | `NoneLiteral` |
| `...` | `EllipsisLiteral` |
| `[1, 2, 3]` | `ListLiteral` |
| `{"a": 1}` | `DictLiteral` |
| `{1, 2, 3}` | `SetLiteral` |
| `(1, 2)` | `TupleLiteral` |
| `[x for x in items]` | `ListComprehension` |
| `x`, `my_var` | `Identifier` |
| `obj.attr`, `obj?.attr` | `MemberAccess` |
| `list[0]` | `IndexAccess` |
| `list[1:5]` | `SliceAccess` |
| `func(a, b=1)` | `FunctionCall` |
| `-x`, `not x` | `UnaryOp` |
| `a + b`, `a |> f` | `BinaryOp` |
| `a ?? b` | `BinaryOp(NullCoalesce)` |
| `a < b < c` | `ComparisonChain` |
| `x if cond else y` | `ConditionalExpression` |
| `lambda x: x*2` | `LambdaExpression` |
| `value as Type` | `TypeCast` |
| `value to Type` | `TypeCoercion` |
| `value is Type` | `TypeCheck` |
| `(expr)` | `Parenthesized` |
| `super()` | `SuperExpression` |
| `name := value` | `WalrusExpression` |
| `try expr` | `TryExpression` |
| `try[ValueError] expr` | `TryExpression` |
| `maybe expr` | `MaybeExpression` |

### Learning Path

1. **Start here** - Understand the expression taxonomy
2. **Read `Node.cs` walkthrough** - Understand base node concepts
3. **Explore `Parser.cs`** - See how `ParseExpression()` builds these nodes
4. **Study `TypeChecker.cs`** - See how expressions are validated
5. **Review `RoslynEmitter.cs`** - See how expressions become C# code
6. **Run tests** - `dotnet test --filter "Expression"` to see examples
