# Walkthrough: Parser.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.cs`

---

## Overview

The `Parser.cs` file contains the **heart of the Sharpy compiler's syntax analysis phase**. It implements a **recursive descent parser** that transforms a flat stream of tokens (from the Lexer) into a hierarchical **Abstract Syntax Tree (AST)**. 

The parser is the second stage in the Sharpy compilation pipeline:
```
Source Code → Lexer (tokens) → Parser (AST) → Semantic Analyzer → Code Generator
```

**Key responsibilities:**
- Convert token streams into structured AST nodes
- Handle Python-like syntax including significant whitespace (indentation)
- Implement operator precedence for expressions
- Parse all Sharpy language constructs (functions, classes, control flow, expressions)
- Provide meaningful error messages with line/column information
- Support advanced features like comprehensions, f-strings, decorators, and type annotations

**Design philosophy:**
- **Immutable AST**: All AST nodes are C# records (immutable)
- **Error recovery**: Throws `ParserError` on syntax errors (no recovery - fail fast)
- **Position tracking**: Every AST node knows its source location (line/column start/end)
- **Precedence climbing**: Expression parsing uses precedence levels for correct operator handling

---

## Class Structure

### Main Class: `Parser`

```csharp
public class Parser
{
    private readonly List<Token> _tokens;      // Token stream from lexer
    private int _position;                     // Current position in token stream
    private readonly ICompilerLogger _logger;  // For diagnostic logging
}
```

**State:**
- `_tokens`: The complete list of tokens to parse (immutable after construction)
- `_position`: Current index into the token stream (moves forward as we parse)
- `_logger`: Optional logger for debugging and performance tracking

**Helper Properties:**
- `Current`: Token at current position (safe - returns EOF if past end)
- `Previous`: Token before current position (for error reporting)
- `Peek(offset)`: Look ahead at future tokens without consuming them
- `IsAtEnd`: Check if we've reached EOF token

---

## Key Methods and Parsing Flow

### 1. Entry Point: `ParseModule()`

```csharp
public Module ParseModule()
```

**Purpose**: Parse an entire Sharpy source file into a Module AST node.

**Algorithm**:
1. Skip leading newlines
2. Check for optional module-level docstring (first string literal)
3. Parse statements until EOF
4. Return a `Module` node containing all top-level statements

**Example**:
```python
"""This is a module docstring"""

def hello():
    print("Hello, world!")

class Greeter:
    pass
```

Produces:
```
Module
├── DocString: "This is a module docstring"
└── Body: [FunctionDef("hello"), ClassDef("Greeter")]
```

---

### 2. Statement Parsing

#### `ParseStatement()`

**Purpose**: Dispatch to the appropriate statement parser based on current token.

**Pattern**: Uses C# pattern matching on `TokenType`:
```csharp
return Current.Type switch
{
    TokenType.Def => ParseFunctionDef(),
    TokenType.Class => ParseClassDef(),
    TokenType.If => ParseIfStatement(),
    TokenType.For => ParseForStatement(),
    // ... etc
    _ => ParseSimpleStatement()  // Fallback for expressions/assignments
};
```

**Special case: Decorators** - If we see `@`, parse decorators first then the decorated definition.

#### `ParseSimpleStatement()`

**Purpose**: Handle statements that look like expressions initially.

**Challenge**: Sharpy allows multiple statement types that start the same way:
```python
x                    # Expression statement
x = 5                # Assignment
x: int = 5           # Variable declaration with type
x, y = 1, 2          # Tuple unpacking assignment
```

**Algorithm**:
1. Parse the first expression
2. **Check for comma** → Tuple unpacking
3. **Check for assignment operator** (`=`, `+=`, etc.) → Assignment
4. **Check for colon** (`:`) → Variable declaration (type annotation)
5. Otherwise → Expression statement

**Design insight**: This is why parsing Python-like syntax is tricky - you can't know what statement type you're parsing until you've read several tokens!

#### Function Definitions: `ParseFunctionDef()`

```python
def greet(name: str, age: int = 18) -> None:
    """Say hello to someone"""
    print(f"Hello {name}, you are {age} years old")
```

**Parsing steps**:
1. Expect `def` keyword
2. Parse function name (identifier)
3. Parse parameter list in parentheses
4. Parse optional return type annotation (`-> Type`)
5. Expect `:` and newline
6. Expect indent
7. Parse optional docstring (first string in body)
8. Parse function body (block of statements)
9. Expect dedent

**Returns**: `FunctionDef` AST node with parameters, return type, body, and docstring.

#### Class Definitions: `ParseClassDef()`

```python
class MyClass[T](BaseClass, Interface1):
    """A generic class"""
    pass
```

**Key features**:
- Type parameters: `[T, U]`
- Base classes/interfaces: `(Parent, Interface1, Interface2)`
- Body with indent/dedent
- Optional docstring

**Returns**: `ClassDef` AST node.

#### Control Flow: `ParseIfStatement()`, `ParseWhileStatement()`, `ParseForStatement()`

**If statement with elif/else**:
```python
if x > 0:
    print("positive")
elif x < 0:
    print("negative")
else:
    print("zero")
```

**Parsing strategy**:
- Parse main `if` clause
- Loop to collect all `elif` clauses (stored as list)
- Parse optional `else` clause
- Return `IfStatement` with test, body, elif clauses, and else body

**For statement with tuple unpacking**:
```python
for x, y in [(1, 2), (3, 4)]:
    print(x, y)
```

**Challenge**: Need to parse the target (`x, y`) without treating it as a full expression (to avoid consuming the `in` keyword).

**Solution**: `ParseForTarget()` - special method that parses identifiers and tuples, stopping before `in`.

#### Exception Handling: `ParseTryStatement()`

```python
try:
    risky_operation()
except ValueError as e:
    print(f"Error: {e}")
except Exception:
    print("Other error")
finally:
    cleanup()
```

**Parsing**:
- Parse `try` body
- Loop to collect multiple `except` handlers (each with optional exception type and variable name)
- Parse optional `finally` block
- Return `TryStatement` with all handlers

---

### 3. Expression Parsing (Precedence Climbing)

**The Challenge**: Expressions have complex precedence rules:
```python
x = 5 + 3 * 2 ** 4 or not y and z < 10
```

Should parse as:
```
         or
        /  \
      +     and
     / \    / \
    5  *   not <
      / \   |  / \
     3  **  y z  10
       / \
      2   4
```

**Solution**: **Precedence Climbing** - Each precedence level has its own parsing method.

#### Expression Precedence Hierarchy (Top to Bottom)

1. **`ParseExpression()`** → Entry point
2. **`ParseConditionalExpression()`** → Ternary `x if test else y`
3. **`ParseNullCoalesce()`** → Null coalescing `??`
4. **`ParseLogicalOr()`** → Boolean OR `or`
5. **`ParseLogicalAnd()`** → Boolean AND `and`
6. **`ParseLogicalNot()`** → Boolean NOT `not`
7. **`ParseComparison()`** → Comparisons `<`, `>`, `==`, `is`, `in`, etc.
8. **`ParseBitwiseOr()`** → Bitwise OR `|`
9. **`ParseBitwiseXor()`** → Bitwise XOR `^`
10. **`ParseBitwiseAnd()`** → Bitwise AND `&`
11. **`ParseShift()`** → Bit shifts `<<`, `>>`
12. **`ParseAdditive()`** → Addition/subtraction `+`, `-`
13. **`ParseMultiplicative()`** → Multiplication/division `*`, `/`, `//`, `%`
14. **`ParseUnary()`** → Unary operators `+`, `-`, `~`
15. **`ParsePower()`** → Exponentiation `**` (right-associative!)
16. **`ParsePostfix()`** → Member access, indexing, calls `.`, `[]`, `()`
17. **`ParsePrimary()`** → Literals, identifiers, parentheses

**Pattern**: Each method parses its level, then calls the next higher precedence level for operands.

**Example: `ParseAdditive()`**:
```csharp
private Expression ParseAdditive()
{
    var left = ParseMultiplicative();  // Higher precedence
    
    while (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus)
    {
        var op = Current.Type == TokenType.Plus ? BinaryOperator.Add : BinaryOperator.Subtract;
        Advance();
        var right = ParseMultiplicative();  // Parse right operand at higher precedence
        
        left = new BinaryOp { Operator = op, Left = left, Right = right };
    }
    
    return left;
}
```

**Key insight**: The `while` loop handles left-associativity (e.g., `5 - 3 - 2` = `(5 - 3) - 2`).

#### Special Case: Comparison Chains

Python allows comparison chaining:
```python
0 <= x < 10  # Equivalent to: 0 <= x and x < 10
```

**Parsing**: `ParseComparison()` collects all comparison operators and operands, then creates either:
- Single `BinaryOp` for simple comparisons (`x < 10`)
- `ComparisonChain` node for chains (`a < b < c`)

#### Special Case: Power Operator `**`

The power operator is **right-associative**:
```python
2 ** 3 ** 4  # = 2 ** (3 ** 4) = 2 ** 81
```

**Implementation**:
```csharp
private Expression ParsePower()
{
    var left = ParsePostfix();
    
    if (Current.Type == TokenType.DoubleStar)
    {
        Advance();
        var right = ParseUnary();  // ← Recursive call to LOWER precedence for right-associativity
        return new BinaryOp { Operator = BinaryOperator.Power, Left = left, Right = right };
    }
    
    return left;
}
```

**Note**: No `while` loop - only one `**` operator per level, and we recurse back to `ParseUnary()` for the right side.

---

### 4. Postfix Operations: `ParsePostfix()`

**Purpose**: Handle operations that come after a primary expression.

**Supports**:
- **Member access**: `obj.member`, `obj?.member` (null-conditional)
- **Indexing**: `list[0]`, `dict["key"]`
- **Slicing**: `list[1:5]`, `list[::2]`, `list[:-1]`
- **Function calls**: `func(arg1, arg2, kwarg=value)`
- **Type casts**: `value as TargetType`

**Algorithm**: Loop while we see postfix operators:
```csharp
while (true)
{
    if (Current.Type == TokenType.Dot || Current.Type == TokenType.NullConditional)
        // Parse member access
    else if (Current.Type == TokenType.LeftBracket)
        // Parse indexing or slicing
    else if (Current.Type == TokenType.LeftParen)
        // Parse function call
    else if (Current.Type == TokenType.As)
        // Parse type cast
    else
        break;  // No more postfix operations
}
```

**Example**:
```python
data.items()[0].upper()
```

Parsing flow:
1. `data` (primary)
2. `.items` (member access)
3. `()` (function call)
4. `[0]` (indexing)
5. `.upper` (member access)
6. `()` (function call)

#### Slicing vs Indexing: `ParseSliceOrIndex()`

**Challenge**: Determine if `[...]` is an index or slice.

```python
list[5]        # Index
list[1:10]     # Slice
list[1:10:2]   # Slice with step
list[:5]       # Slice from start
list[5:]       # Slice to end
list[:]        # Full slice (copy)
```

**Algorithm**:
- If we see a colon → It's a slice
- Otherwise → It's an index

**Returns**: Either `IndexAccess` or `SliceAccess` AST node (caller fills in the object).

---

### 5. Primary Expressions: `ParsePrimary()`

**Purpose**: Parse the "atoms" of expressions - literals and identifiers.

**Handles**:
- **Literals**: integers, floats, strings, booleans, None
- **Identifiers**: variable names
- **Parenthesized expressions**: `(expr)`
- **Tuples**: `()`, `(1,)`, `(1, 2, 3)`
- **Lists**: `[]`, `[1, 2, 3]`
- **Dicts**: `{}`, `{"key": "value"}`
- **Sets**: `{/}` (empty), `{1, 2, 3}`
- **Comprehensions**: `[x for x in range(10)]`, `{x: x**2 for x in range(10)}`
- **Lambdas**: `lambda x, y: x + y`
- **F-strings**: `f"Hello {name}"`

#### F-String Parsing: `ParseFStringParts()`

**Challenge**: F-strings contain embedded expressions:
```python
f"Hello {name.upper()}, you are {age + 1} years old"
```

**Algorithm**:
1. Scan through the f-string value character by character
2. Collect literal text between `{` and `}`
3. When we hit `{`, extract the expression code
4. Create a mini-lexer and parser to parse the embedded expression
5. Return list of `FStringPart` (alternating text and expressions)

**Design insight**: We recursively create a new `Lexer` and `Parser` for each embedded expression! This is elegant but potentially expensive for complex f-strings.

#### Comprehension Parsing: Lists, Sets, Dicts

**Pattern**: All comprehensions have the same structure:
```python
[element for var in iterable if condition]
{element for var in iterable if condition}
{key: value for var in iterable if condition}
```

**Detection in `ParsePrimary()`**:
- For lists: After parsing first element in `[...]`, check if next token is `for`
- For sets: After parsing first element in `{...}`, check if next token is `for`
- For dicts: After parsing `key: value`, check if next token is `for`

**Delegation**: Calls `ParseComprehensionClauses()` which loops to parse all `for` and `if` clauses.

**Example**:
```python
[x**2 for x in range(10) if x % 2 == 0 for y in range(x)]
```

Parses to:
- Element: `x**2`
- Clauses:
  1. `ForClause`: target=`x`, iterator=`range(10)`
  2. `IfClause`: condition=`x % 2 == 0`
  3. `ForClause`: target=`y`, iterator=`range(x)`

---

### 6. Type Annotation Parsing: `ParseTypeAnnotation()`

**Purpose**: Parse Sharpy type annotations.

**Syntax**:
```python
x: int                    # Simple type
y: list[str]              # Generic type
z: dict[str, int]         # Multiple type arguments
w: MyClass?               # Nullable type
func() -> None            # Return type annotation
auto_val: auto = 42       # Type inference
```

**Algorithm**:
1. Parse type name (identifier, or special keywords `auto`/`None`)
2. If `[`, parse type arguments recursively
3. If `?`, mark as nullable
4. Return `TypeAnnotation` AST node

**Design note**: Type annotations are parsed but not validated here - that's the Semantic Analyzer's job.

---

### 7. Helper Methods

#### Token Consumption

```csharp
private void Advance()                        // Move to next token
private void Expect(TokenType type)           // Consume expected token or error
private string ExpectIdentifier()             // Consume identifier and return its value
private void ExpectNewline()                  // Consume newline (or EOF/dedent)
private void ExpectStatementEnd()             // Consume statement terminator
private void SkipNewlines()                   // Skip multiple newlines
```

**Pattern**: `Expect*` methods throw `ParserError` if the expected token is not found.

#### Type Checking Helpers

```csharp
private bool IsComparisonOperator(TokenType type)
private bool IsTypeName(string name)
```

**Purpose**: Classify tokens and names to make parsing decisions.

**`IsTypeName()` heuristic**:
- Built-in types: `int`, `str`, `list`, `dict`, etc.
- User-defined types: Starts with uppercase letter (convention)

This helps disambiguate `is` as a comparison vs type check:
```python
x is None          # Comparison
x is MyClass       # Type check (becomes TypeCheck AST node)
```

#### Operator Mapping

```csharp
private AssignmentOperator TokenTypeToAssignmentOperator(TokenType type)
private ComparisonOperator TokenTypeToComparisonOperator(TokenType type)
private BinaryOperator ComparisonOperatorToBinary(ComparisonOperator op)
```

**Purpose**: Convert lexer token types to AST operator enums.

**Why separate?** 
- Lexer knows about character sequences (`+=`, `==`, `<=`)
- AST knows about semantic operations (`PlusAssign`, `Equal`, `LessThanOrEqual`)
- Parser bridges the gap

---

## Dependencies

### Internal Dependencies

**Sharpy.Compiler.Lexer**:
- `Token`: Data structure from lexer
- `TokenType`: Enum of all token types
- `Lexer`: Used for parsing f-string embedded expressions

**Sharpy.Compiler.Parser.Ast**:
- `Node`, `Statement`, `Expression`: Base AST classes
- All specific node types (60+ different AST node types)
- Operators enums: `BinaryOperator`, `UnaryOperator`, `ComparisonOperator`, `AssignmentOperator`

**Sharpy.Compiler.Logging**:
- `ICompilerLogger`: For diagnostic output
- `NullLogger`: Default no-op logger

### External Dependencies

- `System.Text.StringBuilder`: For f-string parsing text accumulation
- .NET Core collections: `List<T>`, `Dictionary<K,V>`

---

## Patterns and Design Decisions

### 1. Immutable AST with C# Records

**All AST nodes are `record` types**:
```csharp
public record BinaryOp : Expression
{
    public BinaryOperator Operator { get; init; }
    public Expression Left { get; init; }
    public Expression Right { get; init; }
    public int LineStart { get; init; }
    // ... position info
}
```

**Benefits**:
- **Immutability**: Once created, AST nodes can't be modified (safer, easier to reason about)
- **Structural equality**: Two nodes with same values are equal
- **Pattern matching**: `with` expressions for creating modified copies
- **Thread-safety**: Can be shared across threads without locks

**Tradeoff**: More memory allocations, but modern GC handles this well.

### 2. Recursive Descent Parsing

**Why recursive descent?**
- ✅ Simple to understand and implement
- ✅ Easy to extend with new syntax
- ✅ Natural mapping from grammar rules to code
- ✅ Good error messages (know exactly where you are)
- ❌ Can't handle left-recursive grammars (but Sharpy doesn't need it)
- ❌ No error recovery (first error stops parsing)

**Each grammar rule becomes a method**:
```
Grammar:    if_stmt := 'if' expression ':' suite ('elif' expression ':' suite)* ('else' ':' suite)?
Code:       IfStatement ParseIfStatement() { ... }
```

### 3. Precedence Climbing for Expressions

**Why not a table-driven parser?**
- Precedence climbing is **simple and fast**
- Code is **explicit and readable** (each level is a method)
- Easy to **customize** (e.g., right-associative power operator)
- Performance is excellent (no table lookups, all static dispatch)

**Alternative**: Operator-precedence parser with tables (more flexible but more complex).

### 4. Position Tracking on Every Node

**Every AST node records its source location**:
```csharp
public int LineStart { get; init; }
public int ColumnStart { get; init; }
public int LineEnd { get; init; }
public int ColumnEnd { get; init; }
```

**Why?**
- **Error messages**: "Error on line 42, column 15"
- **IDE features**: Jump to definition, find references, refactoring
- **Debugging**: Show source context when debugging compiler
- **Code generation**: Preserve line information for debugging generated code

**Tradeoff**: Extra memory per node (~16 bytes), but worth it for developer experience.

### 5. No Error Recovery

**Current design**: First syntax error throws `ParserError` and stops.

**Why?**
- **Simpler code**: No complex error recovery logic
- **Faster for valid code**: No overhead checking for error states
- **Modern compilers run fast**: Users re-run quickly, so seeing one error at a time is okay

**Future improvement**: Could add error recovery to show multiple errors in one run (better developer experience).

### 6. Logging for Performance Tracking

```csharp
var startTime = System.Diagnostics.Stopwatch.StartNew();
// ... parse module ...
_logger.LogInfo($"Module parsing completed in {startTime.ElapsedMilliseconds}ms, {statements.Count} statements");
```

**Why?**
- Track parsing performance (important for large files)
- Debug slow parses
- Measure impact of optimizations

**Note**: Uses optional logger (defaults to `NullLogger` if not provided).

---

## Debugging Tips

### 1. Understanding Parse Errors

**Common error**: `Expected X, got Y`

**How to debug**:
1. Look at the line and column in the error
2. Check what the parser was expecting based on the context
3. Verify the lexer is producing correct tokens (use `--emit-tokens`)

**Example**:
```
Error: Expected :, got Newline at line 5, column 20
```

Likely cause: Missing colon after function definition or if statement.

### 2. Visualizing the AST

**Use `AstDumper.cs`** (in same directory):
```csharp
var dumper = new AstDumper();
var astString = dumper.Dump(module);
Console.WriteLine(astString);
```

**Output**:
```
Module
├── FunctionDef: greet
│   ├── Parameter: name (type: str)
│   ├── Body
│   │   └── ExpressionStatement
│   │       └── FunctionCall
│   │           └── print
```

**When to use**: When AST structure doesn't match expectations.

### 3. Token Stream Inspection

**Before debugging parser, check tokens**:
```bash
sharpyc --emit-tokens myfile.spy
```

**Common issues**:
- Unexpected indent/dedent tokens
- F-string not tokenizing correctly
- Keywords vs identifiers confusion

### 4. Precedence Issues

**Symptom**: Expression parses incorrectly:
```python
x = 5 + 3 * 2  # Should be 5 + (3 * 2), not (5 + 3) * 2
```

**Debug**:
1. Check which `Parse*()` method handles each operator
2. Verify the precedence hierarchy (lower in call stack = higher precedence)
3. Add logging to see the order of operations

### 5. Infinite Loops

**Symptom**: Parser hangs forever.

**Cause**: Usually a `while` loop that doesn't consume tokens:
```csharp
while (Current.Type == TokenType.Something)
{
    // BUG: Forgot to call Advance()!
    ParseSomething();
}
```

**Fix**: Make sure every loop iteration consumes at least one token.

### 6. Stack Overflow

**Symptom**: `StackOverflowException` on deeply nested code.

**Cause**: Recursive descent parsing creates deep call stacks for deeply nested expressions.

**Example**:
```python
((((((((((x))))))))))  # 10 levels of nesting
```

**Current status**: Not a problem for reasonable code. If it becomes an issue, could switch to iterative parsing for some constructs.

---

## Contribution Guidelines

### What Changes Are Appropriate for This File?

#### ✅ Good Contributions

1. **Adding new syntax features**
   - Add parsing methods for new statements/expressions
   - Follow existing patterns (recursive descent, precedence climbing)
   - Add comprehensive tests in `Sharpy.Compiler.Tests/Parser/`

2. **Improving error messages**
   - Make errors more specific and actionable
   - Add context to error messages (what was being parsed)

3. **Performance optimizations**
   - Reduce allocations (careful with this - readability first!)
   - Cache commonly used values
   - Optimize hot paths (measure first!)

4. **Bug fixes**
   - Fix incorrect parsing of valid code
   - Fix crashes on invalid code
   - Add regression tests

5. **Better position tracking**
   - More accurate line/column information
   - Better handling of multi-line constructs

#### ❌ Changes to Avoid

1. **Don't change AST node structure** without coordinating with Semantic Analyzer and Code Generator
2. **Don't add error recovery** without a design discussion (significant complexity)
3. **Don't change precedence** without updating all affected tests
4. **Don't remove position tracking** (needed for error messages and IDE features)

### How to Add a New Statement Type

**Example**: Adding a `match` statement (hypothetical).

**Steps**:

1. **Add token type to Lexer**:
   ```csharp
   // In Lexer/Token.cs
   public enum TokenType
   {
       // ...
       Match,  // Add this
   }
   ```

2. **Add AST node**:
   ```csharp
   // In Parser/Ast/Statement.cs
   public record MatchStatement : Statement
   {
       public Expression Subject { get; init; }
       public List<MatchCase> Cases { get; init; }
   }
   
   public record MatchCase
   {
       public Pattern Pattern { get; init; }
       public List<Statement> Body { get; init; }
   }
   ```

3. **Add parser method**:
   ```csharp
   // In Parser.cs
   private MatchStatement ParseMatchStatement()
   {
       var startLine = Current.Line;
       var startColumn = Current.Column;
       
       Expect(TokenType.Match);
       var subject = ParseExpression();
       Expect(TokenType.Colon);
       ExpectNewline();
       Expect(TokenType.Indent);
       
       var cases = new List<MatchCase>();
       while (Current.Type == TokenType.Case)
       {
           cases.Add(ParseMatchCase());
       }
       
       Expect(TokenType.Dedent);
       
       return new MatchStatement
       {
           Subject = subject,
           Cases = cases,
           LineStart = startLine,
           ColumnStart = startColumn,
           LineEnd = Current.Line,
           ColumnEnd = Current.Column
       };
   }
   ```

4. **Add to dispatcher**:
   ```csharp
   private Statement ParseStatement()
   {
       return Current.Type switch
       {
           // ...
           TokenType.Match => ParseMatchStatement(),  // Add this
           // ...
       };
   }
   ```

5. **Add tests**:
   ```csharp
   // In Sharpy.Compiler.Tests/Parser/StatementTests.cs
   [Fact]
   public void TestMatchStatement()
   {
       var source = """
           match x:
               case 1:
                   print("one")
               case 2:
                   print("two")
           """;
       
       var parser = new Parser(Lex(source));
       var module = parser.ParseModule();
       
       var matchStmt = Assert.IsType<MatchStatement>(module.Body[0]);
       Assert.Equal(2, matchStmt.Cases.Count);
   }
   ```

6. **Update Semantic Analyzer and Code Generator** to handle the new node type.

### How to Add a New Expression Type

**Similar to statements**, but add to expression parsing chain:

1. Add AST node (in `Parser/Ast/Expression.cs`)
2. Add parsing logic (in appropriate precedence level method)
3. Add tests
4. Update subsequent compiler stages

### Testing Guidelines

**Every parser change needs tests**:
- **Positive tests**: Valid syntax parses correctly
- **Negative tests**: Invalid syntax produces meaningful errors
- **Edge cases**: Empty constructs, trailing commas, nested structures
- **Position tests**: Verify line/column information is accurate

**Example test structure**:
```csharp
[Fact]
public void TestFeatureName_ValidInput()
{
    var source = "...";
    var result = Parse(source);
    Assert.NotNull(result);
    // ... detailed assertions on AST structure
}

[Fact]
public void TestFeatureName_InvalidInput_ThrowsParserError()
{
    var source = "...";
    Assert.Throws<ParserError>(() => Parse(source));
}
```

### Code Style

**Follow existing conventions**:
- Methods named `Parse*()` for parsing logic
- Use `Expect*()` for token consumption with errors
- Use pattern matching for dispatching
- Keep methods focused (one grammar rule per method)
- Add XML doc comments for public APIs
- Use meaningful variable names (`startLine`, `parameters`, `body`)

### Performance Considerations

**Parser performance is usually good**, but if you're optimizing:
- Profile first (use BenchmarkDotNet)
- Focus on hot paths (expression parsing, postfix operations)
- Avoid allocations in tight loops
- Consider caching (but measure overhead)
- Don't sacrifice readability for micro-optimizations

**Current bottlenecks**:
- F-string parsing (creates mini-lexers/parsers)
- Deep expression nesting (recursive calls)
- Large list/dict literals (many allocations)

---

## Summary

The `Parser.cs` file is a **clean, well-structured recursive descent parser** that transforms Sharpy tokens into an immutable AST. It handles complex Python-like syntax with operator precedence, significant whitespace, and advanced features like comprehensions and f-strings.

**Key takeaways for newcomers**:
1. **Follow the flow**: `ParseModule()` → `ParseStatement()` → `ParseExpression()` → `ParsePrimary()`
2. **Understand precedence**: Expression parsing is a chain of methods, each handling one precedence level
3. **Immutable AST**: All nodes are records - create new ones, don't modify existing
4. **Position tracking**: Every node knows where it came from in source code
5. **Error handling**: Fail fast with clear error messages
6. **Test everything**: Parser tests ensure correctness and prevent regressions

**Resources**:
- AST definitions: `Parser/Ast/*.cs`
- Parser tests: `Sharpy.Compiler.Tests/Parser/`
- AST visualization: Use `AstDumper.cs`
- Language reference: `docs/specs/language_reference.md`
