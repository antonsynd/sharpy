# Walkthrough: Parser.Expressions.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.Expressions.cs`

---

## 1. Overview

This is a **partial class** of the main `Parser` class, containing all expression parsing methods. It implements Sharpy's operator precedence hierarchy using a technique called **precedence climbing** (also known as **recursive descent with precedence**).

**Key responsibilities:**
- Parse all expression types (literals, operators, function calls, etc.)
- Enforce correct operator precedence (from walrus `:=` to postfix operators)
- Support operator chaining (e.g., `a < b < c`)
- Handle special expressions: `try`, `maybe`, conditional expressions
- Create appropriate AST nodes for each expression type

**Pipeline position:**
```
Lexer → [TOKENS] → Parser.Expressions → [Expression AST Nodes] → Semantic Analysis
```

This file works hand-in-hand with other Parser partial classes:
- `Parser.Primaries.cs` - Handles literals, identifiers, collection syntax
- `Parser.Statements.cs` - Handles statements (assignments, loops, etc.)
- `Parser.Types.cs` - Handles type annotations
- `Parser.Definitions.cs` - Handles function/class definitions

---

## 2. Class Structure

### This is a Partial Class

```csharp
public partial class Parser
{
    private Expression ParseExpression() => ParseWalrusExpression();
    // ... all expression parsing methods
}
```

The `Parser` class is split across multiple files for maintainability. The main `Parser.cs` file contains:
- Token stream navigation (`Current`, `Peek()`, `Advance()`)
- Error handling (`Expect()`, `ExpectIdentifierOrKeyword()`)
- Module parsing entry point

**See also:**
- [Parser.md](Parser.md) - Main Parser class documentation
- [Parser.Primaries.md](Parser.Primaries.md) - Primary expression parsing
- [Parser.Statements.md](Parser.Statements.md) - Statement parsing

---

## 3. Key Concepts

### 3.1 Precedence Climbing

The parser implements operator precedence through a **chain of method calls**, where each method handles one precedence level:

```
ParseExpression()                    ← Entry point
  └─> ParseWalrusExpression()        ← Lowest precedence (:=)
      └─> ParseTryMaybeExpression()  ← try/maybe
          └─> ParseConditionalExpression()  ← x if c else y
              └─> ParseNullCoalesce()       ← ??
                  └─> ParseLogicalOr()      ← or
                      └─> ParseLogicalAnd() ← and
                          └─> ParseLogicalNot() ← not
                              └─> ParseComparison() ← ==, <, >, in, is
                                  └─> ParsePipe() ← |>
                                      └─> ParseBitwiseOr() ← |
                                          └─> ParseBitwiseXor() ← ^
                                              └─> ParseBitwiseAnd() ← &
                                                  └─> ParseShift() ← <<, >>
                                                      └─> ParseAdditive() ← +, -
                                                          └─> ParseMultiplicative() ← *, /, //, %
                                                              └─> ParseUnary() ← +x, -x, ~x
                                                                  └─> ParsePower() ← **
                                                                      └─> ParsePostfix() ← ., [], ()
                                                                          └─> ParsePrimary() ← literals, identifiers
```

**How it works:**
1. Each method parses its left operand by calling the **next higher precedence** method
2. Then it loops, consuming operators at its precedence level
3. For each operator, it parses the right operand at the **same** level (for left-associative) or **higher** level (for right-associative)
4. It builds a `BinaryOp` AST node and continues the loop

**Example** - Parsing `2 + 3 * 4`:
```
ParseExpression()
  ParseWalrusExpression()  (no := operator)
    ParseTryMaybeExpression()  (no try/maybe)
      ParseConditionalExpression()  (no if/else)
        ParseNullCoalesce()  (no ??)
          ...continuing down...
            ParseAdditive()
              left = ParseMultiplicative()
                left = ParseUnary()
                  ParsePower()
                    ParsePostfix()
                      ParsePrimary() → IntegerLiteral(2)
                return IntegerLiteral(2)

              See '+' token → Advance()
              right = ParseMultiplicative()
                left = ParseUnary()
                  ParsePower()
                    ParsePostfix()
                      ParsePrimary() → IntegerLiteral(3)

                See '*' token → Advance()
                right = ParseUnary()
                  ParsePower()
                    ParsePostfix()
                      ParsePrimary() → IntegerLiteral(4)

                return BinaryOp(Multiply, 3, 4)

              return BinaryOp(Add, 2, BinaryOp(Multiply, 3, 4))
```

The result: `2 + (3 * 4)` - multiplication binds tighter because `ParseMultiplicative()` is called recursively before returning to `ParseAdditive()`.

### 3.2 Left vs. Right Associativity

**Left-associative** operators (most operators):
```csharp
while (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus)
{
    var op = ...;
    Advance();
    var right = ParseMultiplicative();  // Same level
    left = new BinaryOp { Operator = op, Left = left, Right = right };
}
```
Loop structure creates left-to-right chaining: `a + b + c` → `(a + b) + c`

**Right-associative** operators (power `**`, walrus `:=`, conditionals):
```csharp
if (Current.Type == TokenType.DoubleStar)
{
    Advance();
    var right = ParseUnary();  // Recursive call to higher level
    return new BinaryOp { Operator = Power, Left = left, Right = right };
}
```
Recursion creates right-to-left chaining: `a ** b ** c` → `a ** (b ** c)`

### 3.3 Comparison Chaining

Python-style comparison chains (`a < b < c`) are handled specially:

```python
# Instead of left-to-right: (a < b) < c (which would be bool < int)
# We want: (a < b) and (b < c)
```

The `ParseComparison()` method (lines 233-320):
1. Collects **all** comparison operators and operands in lists
2. If there's only one operator → creates a simple `BinaryOp`
3. If there are multiple → creates a `ComparisonChain` node

```csharp
var operators = new List<ComparisonOperator>();
var operands = new List<Expression> { left };

while (IsComparisonOperator(Current.Type))
{
    operators.Add(...);
    operands.Add(ParsePipe());
}

if (operators.Count > 1)
    return new ComparisonChain { Operands = operands, Operators = operators };
```

The code generator later transforms this into AND-chained comparisons.

---

## 4. Key Methods

### 4.1 Entry Point

#### `ParseExpression()` (line 13)

```csharp
private Expression ParseExpression() => ParseWalrusExpression();
```

The main entry point for parsing any expression. Simply delegates to the lowest-precedence operator (walrus).

**Called by:**
- Statement parsers (assignment, return, etc.)
- Other expression parsers (function arguments, list elements, etc.)

---

### 4.2 Lowest Precedence Operators

#### `ParseWalrusExpression()` (lines 15-40)

Handles the walrus operator `:=` (assignment expression).

**Syntax:** `name := value`

**Example:**
```python
if (match := pattern.match(text)):
    print(match.group(1))
```

**Algorithm:**
1. Look ahead for `identifier :=` pattern
2. If found:
   - Capture identifier name
   - Recursively parse the value (right-associative)
   - Return `WalrusExpression` node
3. Otherwise, delegate to next precedence level

**Important:** Right-associative, so `a := b := 5` parses as `a := (b := 5)`.

---

#### `ParseTryMaybeExpression()` (lines 42-97)

Handles `try` and `maybe` prefix expressions for error/null handling.

**Syntax:**
- `try expr` or `try[ExceptionType] expr`
- `maybe expr`

**Examples:**
```python
# Wrap potentially-throwing expression in Result[T, E]
result = try int(user_input)              # Result[int, Exception]
result = try[ValueError] int(user_input)  # Result[int, ValueError]

# Wrap nullable expression in Optional[T]
value = maybe dict.get("key")  # Optional[T]
```

**Algorithm:**
1. Check for `try` keyword:
   - Check for optional `[ExceptionType]` bracket syntax
   - Parse the operand expression
   - Return `TryExpression` node
2. Check for `maybe` keyword:
   - Parse the operand expression
   - Return `MaybeExpression` node
3. Otherwise, delegate to conditional expression parsing

**Design note:** These expressions capture everything up to (but not including) conditional expressions, giving them very low precedence. See `ParseConditionalOperand()`.

---

#### `ParseConditionalOperand()` (lines 104-109)

```csharp
private Expression ParseConditionalOperand()
{
    return ParseNullCoalesce();
}
```

Helper method that defines what `try`/`maybe` expressions should capture. They bind to everything except conditional expressions (`x if c else y`).

**Why this exists:**
```python
# try captures null coalesce and everything higher
result = try foo() ?? default  # try (foo() ?? default)

# try does NOT capture conditionals
result = try foo() if cond else bar()  # (try foo()) if cond else bar()
```

---

#### `ParseConditionalExpression()` (lines 111-136)

Handles Python-style ternary expressions: `value if condition else other`.

**Example:**
```python
x = "positive" if num > 0 else "negative"
```

**Algorithm:**
1. Parse the "then" value first (this is Python's unusual order)
2. If we see `if` keyword:
   - Parse the condition
   - Expect `else` keyword
   - Recursively parse the "else" value (right-associative)
   - Return `ConditionalExpression` with: `Test`, `ThenValue`, `ElseValue`
3. Otherwise, return the expression as-is

**Right-associative chaining:**
```python
a if x else b if y else c
# Parses as: a if x else (b if y else c)
```

---

### 4.3 Null Coalescing

#### `ParseNullCoalesce()` (lines 138-160)

Handles the `??` null coalescing operator.

**Syntax:** `a ?? b` returns `a` if not null, otherwise `b`.

**Example:**
```python
name = user.name ?? "Anonymous"
```

**Left-associative loop:**
```csharp
while (Current.Type == TokenType.NullCoalesce)
{
    Advance();
    var right = ParseLogicalOr();
    left = new BinaryOp { Operator = NullCoalesce, Left = left, Right = right };
}
```

---

### 4.4 Logical Operators

#### `ParseLogicalOr()`, `ParseLogicalAnd()`, `ParseLogicalNot()` (lines 162-231)

Handle boolean logic operators with correct precedence.

**Precedence:** `not` > `and` > `or`

**Examples:**
```python
a or b and c        # a or (b and c)
not a and b         # (not a) and b
a and b or c and d  # (a and b) or (c and d)
```

**Implementation pattern:**
- `ParseLogicalOr()` - Left-associative loop for `or`
- `ParseLogicalAnd()` - Left-associative loop for `and`
- `ParseLogicalNot()` - Prefix operator (checks for `not`, then recurses)

**Unary `not` handling:**
```csharp
if (Current.Type == TokenType.Not)
{
    Advance();
    var operand = ParseLogicalNot();  // Right-associative recursion
    return new UnaryOp { Operator = Not, Operand = operand };
}
```

---

### 4.5 Comparison Operators

#### `ParseComparison()` (lines 233-320)

Handles all comparison operators: `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, `not in`, `is`, `is not`.

**Special cases:**

1. **Type checking:** `value is TypeName`
   ```csharp
   if (Current.Type == TokenType.Is && Peek(1).Type == TokenType.Identifier)
   {
       if (IsTypeName(nextTokenValue))
           return new TypeCheck { Value = left, CheckType = typeAnnotation };
   }
   ```

2. **Multi-token operators:** `is not` and `not in`
   ```csharp
   if (op == TokenType.Is && Current.Type == TokenType.Not)
   {
       Advance();
       operators.Add(ComparisonOperator.IsNot);
   }
   ```

3. **Comparison chains:** `a < b < c`
   ```csharp
   while (IsComparisonOperator(Current.Type))
   {
       operators.Add(...);
       operands.Add(ParsePipe());
   }

   if (operators.Count > 1)
       return new ComparisonChain { Operands = operands, Operators = operators };
   ```

**Helper methods:**
- `IsComparisonOperator()` - Checks if token is a comparison operator
- `TokenTypeToComparisonOperator()` - Converts token to enum
- `ComparisonOperatorToBinary()` - Converts for single comparisons

---

### 4.6 Pipe Operator

#### `ParsePipe()` (lines 359-381)

Handles the pipe-forward operator `|>` for functional-style data flow.

**Example:**
```python
result = data |> filter(is_positive) |> map(square) |> sum
```

**Left-associative loop** - pipes chain left-to-right:
```python
a |> f |> g  # (a |> f) |> g
```

This allows natural data transformation pipelines.

---

### 4.7 Bitwise Operators

#### `ParseBitwiseOr()`, `ParseBitwiseXor()`, `ParseBitwiseAnd()` (lines 383-453)

Handle bitwise operators with correct precedence.

**Precedence:** `&` > `^` > `|`

**Examples:**
```python
a | b ^ c & d  # a | (b ^ (c & d))
```

All are left-associative with standard loop pattern.

---

#### `ParseShift()` (lines 455-478)

Handles left shift `<<` and right shift `>>` operators.

**Example:**
```python
x << 2   # Multiply by 4
y >> 1   # Divide by 2
```

**Both operators handled in same method:**
```csharp
while (Current.Type == TokenType.LeftShift || Current.Type == TokenType.RightShift)
{
    var op = Current.Type == TokenType.LeftShift ? BinaryOperator.LeftShift : BinaryOperator.RightShift;
    // ...
}
```

---

### 4.8 Arithmetic Operators

#### `ParseAdditive()` (lines 480-503)

Handles `+` and `-` operators.

**Standard left-associative pattern:**
```csharp
while (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus)
{
    var op = Current.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
    Advance();
    var right = ParseMultiplicative();
    left = new BinaryOp { Operator = op, Left = left, Right = right };
}
```

---

#### `ParseMultiplicative()` (lines 505-536)

Handles `*`, `/`, `//` (floor division), and `%` (modulo).

**All four operators handled together:**
```csharp
while (Current.Type == TokenType.Star || Current.Type == TokenType.Slash ||
       Current.Type == TokenType.DoubleSlash || Current.Type == TokenType.Percent)
{
    var op = Current.Type switch
    {
        TokenType.Star => BinaryOperator.Multiply,
        TokenType.Slash => BinaryOperator.Divide,
        TokenType.DoubleSlash => BinaryOperator.FloorDivide,
        TokenType.Percent => BinaryOperator.Modulo,
        _ => throw new ParserError(...)
    };
    // ...
}
```

---

### 4.9 Unary Operators

#### `ParseUnary()` (lines 538-566)

Handles unary prefix operators: `+x`, `-x`, `~x` (bitwise not).

**Right-associative recursion:**
```csharp
if (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus || Current.Type == TokenType.Tilde)
{
    var op = Current.Type switch
    {
        TokenType.Plus => UnaryOperator.Plus,
        TokenType.Minus => UnaryOperator.Minus,
        TokenType.Tilde => UnaryOperator.BitwiseNot,
        _ => throw new ParserError(...)
    };
    Advance();
    var operand = ParseUnary();  // Recursive - allows stacking: --x, +-x
    return new UnaryOp { Operator = op, Operand = operand };
}
```

**Allows stacking:**
```python
--x   # Parses as: -(-(x))
+-5   # Parses as: +(-(5))
```

---

### 4.10 Power Operator

#### `ParsePower()` (lines 568-590)

Handles exponentiation `**` operator.

**Right-associative:**
```csharp
if (Current.Type == TokenType.DoubleStar)
{
    Advance();
    var right = ParseUnary();  // Right-associative: parse higher precedence
    return new BinaryOp { Operator = Power, Left = left, Right = right };
}
```

**Example:**
```python
2 ** 3 ** 2  # 2 ** (3 ** 2) = 2 ** 9 = 512
```

Unlike `ParseAdditive()` which loops, this uses a single `if` statement and recursion to achieve right-associativity.

---

### 4.11 Postfix Operators

#### `ParsePostfix()` (lines 592-735)

Handles the highest-precedence operators that appear **after** a primary expression:
- `.` - Member access: `obj.member`
- `?.` - Null-conditional access: `obj?.member`
- `[]` - Indexing/slicing: `list[0]`, `list[1:5]`
- `()` - Function calls: `func(args)`
- `as` - Type cast: `value as Type`
- `to` - Type coercion: `value to Type`

**Infinite loop pattern** (continues until no more postfix operators):
```csharp
while (true)
{
    if (Current.Type == TokenType.Dot || Current.Type == TokenType.NullConditional)
    {
        // Handle member access
    }
    else if (Current.Type == TokenType.LeftBracket)
    {
        // Handle indexing/slicing
    }
    else if (Current.Type == TokenType.LeftParen)
    {
        // Handle function call
    }
    else if (Current.Type == TokenType.As)
    {
        // Handle type cast
    }
    else if (Current.Type == TokenType.To)
    {
        // Handle type coercion
    }
    else
    {
        break;  // No more postfix operators
    }
}
```

**Member Access** (lines 598-615):
```csharp
if (Current.Type == TokenType.Dot || Current.Type == TokenType.NullConditional)
{
    var isNullConditional = Current.Type == TokenType.NullConditional;
    Advance();
    var member = ExpectIdentifierOrKeyword();

    expr = new MemberAccess
    {
        Object = expr,
        Member = member,
        IsNullConditional = isNullConditional,
        // Location info...
    };
}
```

**Allows chaining:**
```python
obj.foo.bar?.baz()  # (((obj.foo).bar)?.baz)()
```

**Indexing/Slicing** (lines 616-626):
```csharp
else if (Current.Type == TokenType.LeftBracket)
{
    Advance();
    var index = ParseSliceOrIndex();  // Helper method
    Expect(TokenType.RightBracket);

    if (index is IndexAccess ia)
        expr = ia with { Object = expr, LineStart = expr.LineStart, ColumnStart = expr.ColumnStart };
    else if (index is SliceAccess sa)
        expr = sa with { Object = expr, LineStart = expr.LineStart, ColumnStart = expr.ColumnStart };
}
```

Uses C# 9 `with` expressions to update record properties.

**Function Calls** (lines 627-694):

The most complex postfix operation - handles both positional and keyword arguments.

```csharp
else if (Current.Type == TokenType.LeftParen)
{
    Advance();
    var args = new List<Expression>();
    var kwargs = new List<KeywordArgument>();
    var seenKeywordArg = false;

    if (Current.Type != TokenType.RightParen)
    {
        do
        {
            // Check for keyword argument: name=value
            if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.Assign)
            {
                seenKeywordArg = true;
                var name = Current.Value;
                Advance();  // Skip name
                Advance();  // Skip =
                var value = ParseExpression();
                kwargs.Add(new KeywordArgument { Name = name, Value = value });
            }
            else
            {
                if (seenKeywordArg)
                    throw new ParserError("Positional argument cannot follow keyword argument", ...);
                args.Add(ParseExpression());
            }

            // Handle comma separation and trailing commas
            if (Current.Type == TokenType.Comma)
            {
                Advance();
                if (Current.Type == TokenType.RightParen)  // Trailing comma: foo(1, 2,)
                    break;
            }
            else
                break;
        } while (true);
    }

    Expect(TokenType.RightParen);
    expr = new FunctionCall { Function = expr, Arguments = args, KeywordArguments = kwargs };
}
```

**Important rules:**
1. Positional arguments must come before keyword arguments
2. Trailing commas are allowed: `foo(1, 2, 3,)`
3. Each argument is a full expression (recursively parsed)

**Type Cast** (`as`) (lines 695-710):
```python
x as float  # Soft cast - returns None if cast fails
```

**Type Coercion** (`to`) (lines 711-727):
```python
x to int    # Hard cast - throws InvalidCastException if fails
x to int?   # Soft coercion - returns None if fails
```

---

### 4.12 Slice and Index Parsing

#### `ParseSliceOrIndex()` (lines 737-826)

Complex helper that disambiguates between:
1. Simple index: `list[5]`
2. Slice: `list[1:10]`, `list[:5]`, `list[::2]`
3. Multiple type arguments for generics: `Dict[str, int]`

**Algorithm:**

```csharp
Expression? start = null, stop = null, step = null;
var isSlice = false;

// Parse first expression (unless we start with ':')
if (Current.Type != TokenType.Colon)
    start = ParseExpression();

// Check for multiple type arguments: Dict[int, str, bool]
if (Current.Type == TokenType.Comma)
{
    var elements = new List<Expression> { start! };
    while (Current.Type == TokenType.Comma)
    {
        Advance();
        if (Current.Type == TokenType.RightBracket)  // Trailing comma
            break;
        elements.Add(ParseExpression());
    }

    // Return TupleLiteral wrapped in IndexAccess for generic type args
    return new IndexAccess { Index = new TupleLiteral { Elements = elements } };
}

// Check for slice syntax: ':'
if (Current.Type == TokenType.Colon)
{
    isSlice = true;
    Advance();

    // Parse stop (if present)
    if (Current.Type != TokenType.Colon && Current.Type != TokenType.RightBracket)
        stop = ParseExpression();

    // Parse step (if second ':' present)
    if (Current.Type == TokenType.Colon)
    {
        Advance();
        if (Current.Type != TokenType.RightBracket)
            step = ParseExpression();
    }
}

if (isSlice)
    return new SliceAccess { Start = start, Stop = stop, Step = step };
else
    return new IndexAccess { Index = start! };
```

**Examples:**
```python
list[5]           # IndexAccess(5)
list[1:10]        # SliceAccess(1, 10, None)
list[:5]          # SliceAccess(None, 5, None)
list[::2]         # SliceAccess(None, None, 2)
list[1:10:2]      # SliceAccess(1, 10, 2)
Dict[str, int]    # IndexAccess(TupleLiteral([str, int]))
```

---

## 5. Dependencies

### Internal Dependencies

**From `Sharpy.Compiler.Lexer`:**
- `Token` - Token data structure with `Type`, `Value`, `Line`, `Column`
- `TokenType` - Enum of all token types (keywords, operators, literals)

**From `Sharpy.Compiler.Parser.Ast`:**
- `Expression` - Base class for all expression AST nodes
- `BinaryOp`, `UnaryOp` - Operator expressions
- `WalrusExpression`, `TryExpression`, `MaybeExpression` - Special expressions
- `ConditionalExpression` - Ternary expressions
- `ComparisonChain` - Multi-comparison chains
- `MemberAccess`, `IndexAccess`, `SliceAccess` - Postfix operations
- `FunctionCall`, `KeywordArgument` - Function call syntax
- `TypeCast`, `TypeCoercion`, `TypeCheck` - Type operations
- `TupleLiteral` - Used for multi-type-argument generics

**From `Sharpy.Compiler.Logging`:**
- `ICompilerLogger` - For diagnostics (accessed via parent `Parser` class)

### External Dependencies

**From Main Parser Class (`Parser.cs`):**
- `Current`, `Previous`, `Peek()` - Token navigation
- `Advance()` - Consume current token
- `Expect()` - Consume and verify token type
- `ExpectIdentifierOrKeyword()` - Flexible identifier parsing
- `IsTypeName()` - Check if identifier is a type name
- `ParseTypeAnnotation()` - Parse type syntax (delegated to `Parser.Types.cs`)
- `ParsePrimary()` - Parse primary expressions (delegated to `Parser.Primaries.cs`)

---

## 6. Patterns and Design Decisions

### 6.1 Precedence Climbing Pattern

Instead of a single monolithic expression parser, precedence is encoded in the **call stack depth**:
- Lowest precedence = earliest function call
- Highest precedence = deepest function call

**Benefits:**
- Natural encoding of precedence rules
- Easy to understand and modify
- No precedence table needed
- Compiler optimizations work well with deep call stacks

### 6.2 Loop vs. Recursion for Associativity

**Left-associative** (most operators):
```csharp
while (Current.Type == ...)
{
    left = new BinaryOp { Left = left, Right = ParseHigherPrecedence() };
}
```
Loop builds left-to-right chain without deep recursion.

**Right-associative** (power, walrus, conditionals):
```csharp
if (Current.Type == ...)
{
    return new BinaryOp { Left = left, Right = ParseSamePrecedence() };
}
```
Recursion builds right-to-left chain naturally.

### 6.3 Location Tracking

Every AST node captures source location:
```csharp
return new BinaryOp
{
    Operator = op,
    Left = left,
    Right = right,
    LineStart = left.LineStart,      // Start of left operand
    ColumnStart = left.ColumnStart,
    LineEnd = right.LineEnd,         // End of right operand
    ColumnEnd = right.ColumnEnd
};
```

**Why:** Enables precise error messages in later compiler phases.

### 6.4 Multi-Token Operators

Some operators span multiple tokens: `is not`, `not in`.

**Handled by lookahead:**
```csharp
if (op == TokenType.Is && Current.Type == TokenType.Not)
{
    Advance();  // Consume 'not'
    operators.Add(ComparisonOperator.IsNot);
}
```

### 6.5 Comparison Chains as Special Syntax

Rather than treating `a < b < c` as `(a < b) < c`, we capture the entire chain:

```csharp
var operators = new List<ComparisonOperator>();
var operands = new List<Expression> { left };

while (IsComparisonOperator(Current.Type))
{
    operators.Add(...);
    operands.Add(ParsePipe());
}

if (operators.Count > 1)
    return new ComparisonChain { Operands = operands, Operators = operators };
```

**Why:** Matches Python semantics where `a < b < c` means `a < b AND b < c`, and `b` is only evaluated once.

### 6.6 Null-Conditional Chaining

The `?.` operator is handled the same as `.` but with a flag:

```csharp
var isNullConditional = Current.Type == TokenType.NullConditional;
```

**Later phases** use this flag to generate appropriate null-checking code.

### 6.7 Postfix Operator Greedy Parsing

The `ParsePostfix()` method uses an **infinite loop** that breaks when no more postfix operators are found. This allows unlimited chaining:

```python
obj.method()[0].field?.nested(arg1, arg2)[5:10] as List[int]
```

All parsed in one `ParsePostfix()` call.

---

## 7. Debugging Tips

### 7.1 Tracing Precedence

If an expression parses incorrectly, trace the call stack:

1. Start at `ParseExpression()`
2. Follow the chain down to see which method handles the problematic operator
3. Check if it's calling the correct precedence level for its operands

**Example bug:** If `a + b * c` parses as `(a + b) * c`:
- Check `ParseAdditive()` - it should call `ParseMultiplicative()` for the right operand
- If it calls `ParseAdditive()` instead, multiplication won't bind tighter

### 7.2 Location Info Issues

If error messages point to wrong locations:
- Check that `LineStart`/`ColumnStart` use the **left** operand's start
- Check that `LineEnd`/`ColumnEnd` use the **right** operand's end
- For prefix operators, use the operator's location as start
- For postfix operators, use the operator's location as end

### 7.3 Associativity Problems

If `a op b op c` groups incorrectly:
- **Left-associative should loop:** `while (Current.Type == ...)`
- **Right-associative should use recursion/single if:** `if (Current.Type == ...)`

### 7.4 Precedence Issues

If `a op1 b op2 c` groups incorrectly:
- Higher precedence should be **deeper** in the call chain
- Check the method calling order matches the precedence table

### 7.5 Using the AST Dumper

The Parser namespace includes an `AstDumper` class for debugging:

```csharp
var module = parser.ParseModule();
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

This shows the AST structure to verify parsing is correct.

---

## 8. Contribution Guidelines

### 8.1 Adding a New Operator

**Steps:**

1. **Add token type** in `Lexer/TokenType.cs`:
   ```csharp
   MyNewOperator,
   ```

2. **Add to lexer** in `Lexer/Lexer.cs`:
   ```csharp
   case "~>": return new Token(TokenType.MyNewOperator, "~>", line, column);
   ```

3. **Add to BinaryOperator enum** in `Parser/Ast/Expression.cs`:
   ```csharp
   public enum BinaryOperator
   {
       // ...
       MyNewOperator,
   }
   ```

4. **Add parsing method** in this file at the correct precedence level:
   ```csharp
   private Expression ParseMyOperatorLevel()
   {
       var left = ParseNextHigherPrecedence();

       while (Current.Type == TokenType.MyNewOperator)
       {
           Advance();
           var right = ParseNextHigherPrecedence();  // Or same level for left-assoc
           left = new BinaryOp { Operator = MyNewOperator, Left = left, Right = right };
       }

       return left;
   }
   ```

5. **Wire it into the precedence chain** by having the next-lower precedence method call it.

6. **Add code generation** in `CodeGen/RoslynEmitter.Expressions.cs`.

7. **Add tests** in `Sharpy.Compiler.Tests/Parser/`.

### 8.2 Modifying Precedence

To change where an operator sits in the precedence hierarchy:

1. Move the parsing logic to a different method
2. Update the call chain to reflect the new precedence
3. Update `docs/language_specification/operator_precedence.md`
4. Check all tests still pass

### 8.3 Adding Special Expression Types

For new expression types (like `try`, `maybe`, `walrus`):

1. Define AST node in `Parser/Ast/Expression.cs`
2. Add parsing method in this file
3. Add to the precedence chain at appropriate level
4. Implement code generation in `CodeGen/RoslynEmitter.Expressions.cs`
5. Add semantic analysis if needed
6. Add comprehensive tests

### 8.4 Code Style

Follow existing patterns:
- One operator precedence level per method
- Method names: `Parse<OperatorName>()`
- Use `while` loops for left-associative
- Use `if` + recursion for right-associative
- Always track source locations
- Comment unusual precedence decisions

### 8.5 Testing

Test files are in `Sharpy.Compiler.Tests/Parser/`:
- Add tests for new operators
- Test precedence: `a op1 b op2 c`
- Test associativity: `a op b op c`
- Test edge cases: empty operands, trailing commas, etc.

---

## 9. Cross-References

### Related Parser Files
- **[Parser.md](Parser.md)** - Main Parser class (token navigation, entry points)
- **[Parser.Primaries.md](Parser.Primaries.md)** - Primary expressions (literals, identifiers, collections)
- **[Parser.Statements.md](Parser.Statements.md)** - Statement parsing
- **[Parser.Types.md](Parser.Types.md)** - Type annotation parsing
- **[Parser.Definitions.md](Parser.Definitions.md)** - Function and class definitions

### Related AST Files
- **[Expression.md](../Parser/Ast/Expression.md)** - AST node definitions
- **[Statement.md](../Parser/Ast/Statement.md)** - Statement AST nodes

### Downstream Components
- **[RoslynEmitter.Expressions.md](../CodeGen/RoslynEmitter.Expressions.md)** - Converts expression AST to C# code

### Language Specifications
- **`docs/language_specification/operator_precedence.md`** - Formal precedence rules
- **`docs/language_specification/expressions.md`** - Expression syntax reference

---

## 10. Summary

`Parser.Expressions.cs` is the core of Sharpy's expression parsing, implementing a clean precedence-climbing algorithm that mirrors the language's operator precedence hierarchy. Each method handles one precedence level, making the code easy to understand and maintain.

**Key takeaways:**
- **Precedence = call depth** - Lower precedence operators call higher precedence
- **Loop = left-associative** - `while` loops build left-to-right chains
- **Recursion = right-associative** - Recursive calls build right-to-left chains
- **Special cases** - Comparison chains, postfix operators, multi-token operators handled explicitly
- **Location tracking** - Every node captures precise source location for error reporting

When working with this file, always verify:
1. Precedence matches the specification
2. Associativity is correct (loop vs. recursion)
3. Location info is accurate
4. AST nodes are correctly constructed

For questions or issues, refer to the [main Parser documentation](Parser.md) or the [language specification](../../language_specification/).
