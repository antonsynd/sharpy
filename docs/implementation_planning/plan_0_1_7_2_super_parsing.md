# Implementation Plan: Task 0.1.7.2 - `super()` Parsing and AST

## Overview

**Task ID:** 0.1.7.2
**Title:** Implement/Verify `super()` Parsing and AST
**Priority:** Critical
**Dependencies:** Task 0.1.7.1 (Verify Inheritance AST - completed)

## Objective

Parse `super()` calls and represent them in the AST with the correct grammar enforcement.

## Grammar Specification

```ebnf
super_call ::= 'super' '(' ')' '.' identifier '(' [ arguments ] ')'
```

**Valid forms:**
- `super().__init__(args)` - Constructor chaining
- `super().method_name(args)` - Method call
- `super().__eq__(other)` - Dunder method call

**Invalid forms (errors):**
- `super()` - Standalone (must have method call)
- `super().field` - Field access not allowed
- `super().method` - Method without call not allowed
- `x = super()` - Assignment not allowed

---

## Step-by-Step Implementation Approach

### Step 1: Add `Super` Token Type

**File:** `src/Sharpy.Compiler/Lexer/Token.cs`

Add `Super` to the `TokenType` enum in the Keywords section (around line 78):

```csharp
// Keywords - Other
Del,            // Delete statement
To,             // Type coercion operator
Maybe,          // Optional from nullable expressions
Super,          // super() for parent class access  <-- ADD THIS
```

### Step 2: Add `super` Keyword to Lexer

**File:** `src/Sharpy.Compiler/Lexer/Lexer.cs`

Add to the `Keywords` dictionary (around line 86):

```csharp
{ "maybe", TokenType.Maybe },
{ "super", TokenType.Super },  // <-- ADD THIS
```

### Step 3: Create `SuperCall` AST Node

**File:** `src/Sharpy.Compiler/Parser/Ast/Expression.cs`

Add after `MaybeExpression` (around line 416):

```csharp
/// <summary>
/// Super call expression (super().method(args))
/// Represents a call to a parent class method via super().
/// </summary>
public record SuperCall : Expression
{
    /// <summary>
    /// The method name being called (e.g., "__init__", "speak", "__eq__")
    /// </summary>
    public string MethodName { get; init; } = "";

    /// <summary>
    /// Positional arguments to the method call
    /// </summary>
    public List<Expression> Arguments { get; init; } = new();

    /// <summary>
    /// Keyword arguments to the method call
    /// </summary>
    public List<KeywordArgument> KeywordArguments { get; init; } = new();
}
```

### Step 4: Implement `ParseSuperCall()` Method

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`

Add new method in the Expression Parsing region (after `ParsePrimary`):

```csharp
/// <summary>
/// Parse a super() call expression.
/// Grammar: super_call ::= 'super' '(' ')' '.' identifier '(' [ arguments ] ')'
/// </summary>
private Expression ParseSuperCall()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;

    // Consume 'super'
    Advance();

    // Expect '('
    if (Current.Type != TokenType.LeftParen)
    {
        throw new ParserError("Expected '(' after 'super'", Current.Line, Current.Column);
    }
    Advance();

    // Expect ')' - super() takes no arguments
    if (Current.Type != TokenType.RightParen)
    {
        throw new ParserError("super() takes no arguments", Current.Line, Current.Column);
    }
    Advance();

    // Expect '.' - must access a method
    if (Current.Type != TokenType.Dot)
    {
        throw new ParserError("super() must be followed by a method call (e.g., super().method())", Current.Line, Current.Column);
    }
    Advance();

    // Parse method name
    if (Current.Type != TokenType.Identifier)
    {
        throw new ParserError("Expected method name after 'super().'", Current.Line, Current.Column);
    }
    var methodName = Current.Value;
    Advance();

    // Expect '(' - must call the method
    if (Current.Type != TokenType.LeftParen)
    {
        throw new ParserError($"super().{methodName} must be called as super().{methodName}()", Current.Line, Current.Column);
    }
    Advance();

    // Parse arguments (reuse existing argument parsing logic)
    var args = new List<Expression>();
    var kwargs = new List<KeywordArgument>();

    if (Current.Type != TokenType.RightParen)
    {
        ParseCallArguments(args, kwargs);
    }

    var endLine = Current.Line;
    var endColumn = Current.Column;

    // Expect ')'
    Expect(TokenType.RightParen);

    return new SuperCall
    {
        MethodName = methodName,
        Arguments = args,
        KeywordArguments = kwargs,
        LineStart = startLine,
        ColumnStart = startColumn,
        LineEnd = endLine,
        ColumnEnd = endColumn
    };
}
```

### Step 5: Integrate into `ParsePrimary()`

**File:** `src/Sharpy.Compiler/Parser/Parser.cs`

Add case in `ParsePrimary()` switch statement (before `default:` case at line 2482):

```csharp
case TokenType.Super:
    return ParseSuperCall();
```

### Step 6: Helper Method for Argument Parsing (if needed)

If there's no existing helper for parsing call arguments, extract the logic from `ParsePostfix()` into a reusable method:

```csharp
/// <summary>
/// Parse function call arguments (positional and keyword)
/// </summary>
private void ParseCallArguments(List<Expression> args, List<KeywordArgument> kwargs)
{
    do
    {
        // Check for keyword argument
        if (Current.Type == TokenType.Identifier && Peek().Type == TokenType.Assign)
        {
            var kwStartLine = Current.Line;
            var kwStartColumn = Current.Column;
            var name = Current.Value;
            Advance(); // name
            Advance(); // =
            var value = ParseExpression();
            kwargs.Add(new KeywordArgument
            {
                Name = name,
                Value = value,
                LineStart = kwStartLine,
                ColumnStart = kwStartColumn,
                LineEnd = value.LineEnd,
                ColumnEnd = value.ColumnEnd
            });
        }
        else
        {
            args.Add(ParseExpression());
        }

        if (Current.Type == TokenType.Comma)
            Advance();
        else
            break;
    } while (true);
}
```

---

## Key Files to Modify

| File | Change | Lines |
|------|--------|-------|
| `src/Sharpy.Compiler/Lexer/Token.cs` | Add `Super` enum value | ~78 |
| `src/Sharpy.Compiler/Lexer/Lexer.cs` | Add `"super"` keyword mapping | ~86 |
| `src/Sharpy.Compiler/Parser/Ast/Expression.cs` | Add `SuperCall` record | ~417 |
| `src/Sharpy.Compiler/Parser/Parser.cs` | Add `ParseSuperCall()` method | New method |
| `src/Sharpy.Compiler/Parser/Parser.cs` | Add `Super` case in `ParsePrimary()` | ~2482 |

---

## Tests to Verify

### Unit Tests (Parser Level)

**File:** `src/Sharpy.Compiler.Tests/Parser/SuperCallParsingTests.cs`

```csharp
[Fact]
public void ParseSuperInit_ValidSyntax()
{
    // super().__init__(name)
    var source = "super().__init__(name)";
    var expr = ParseExpression(source);
    var superCall = Assert.IsType<SuperCall>(expr);
    Assert.Equal("__init__", superCall.MethodName);
    Assert.Single(superCall.Arguments);
}

[Fact]
public void ParseSuperMethod_ValidSyntax()
{
    // super().speak()
    var source = "super().speak()";
    var expr = ParseExpression(source);
    var superCall = Assert.IsType<SuperCall>(expr);
    Assert.Equal("speak", superCall.MethodName);
    Assert.Empty(superCall.Arguments);
}

[Fact]
public void ParseSuperDunder_ValidSyntax()
{
    // super().__eq__(other)
    var source = "super().__eq__(other)";
    var expr = ParseExpression(source);
    var superCall = Assert.IsType<SuperCall>(expr);
    Assert.Equal("__eq__", superCall.MethodName);
    Assert.Single(superCall.Arguments);
}

[Fact]
public void ParseSuperWithKeywordArgs()
{
    // super().__init__(name="test", age=25)
    var source = "super().__init__(name=\"test\", age=25)";
    var expr = ParseExpression(source);
    var superCall = Assert.IsType<SuperCall>(expr);
    Assert.Equal("__init__", superCall.MethodName);
    Assert.Equal(2, superCall.KeywordArguments.Count);
}

[Fact]
public void ParseStandaloneSuper_Error()
{
    // super() - error: must have method call
    var source = "super()";
    Assert.Throws<ParserError>(() => ParseExpression(source));
}

[Fact]
public void ParseSuperFieldAccess_Error()
{
    // super().field - error: must call method
    // Note: Parser can't distinguish field vs method without call
    // This will error because no '(' after identifier
    var source = "super().field";
    Assert.Throws<ParserError>(() => ParseExpression(source));
}

[Fact]
public void ParseSuperMethodNoCall_Error()
{
    // super().method - error: must call
    var source = "super().method";
    Assert.Throws<ParserError>(() => ParseExpression(source));
}

[Fact]
public void ParseSuperWithArgs_Error()
{
    // super(cls) - error: super() takes no arguments
    var source = "super(cls).__init__()";
    Assert.Throws<ParserError>(() => ParseExpression(source));
}
```

### Integration Tests (in method context)

```csharp
[Fact]
public void SuperInInit_ParsesCorrectly()
{
    var source = @"
class Dog(Animal):
    def __init__(self, name: str, breed: str):
        super().__init__(name)
        self.breed = breed
";
    var ast = Parse(source);
    // Verify SuperCall is in the __init__ body
}

[Fact]
public void SuperInOverride_ParsesCorrectly()
{
    var source = @"
class Dog(Animal):
    @override
    def speak(self) -> str:
        return super().speak() + ' Woof!'
";
    var ast = Parse(source);
    // Verify SuperCall is in the return expression
}
```

---

## Potential Risks and Questions

### Risk 1: Argument Parsing Reuse
**Issue:** The existing `ParsePostfix()` method has inline argument parsing logic. We need to either:
- Extract it into a shared helper method
- Duplicate the logic in `ParseSuperCall()`

**Recommendation:** Extract to `ParseCallArguments()` helper for code reuse.

### Risk 2: Error Message Clarity
**Issue:** Different invalid forms need different error messages.

**Mitigations:**
- `super()` alone → "super() must be followed by a method call"
- `super().field` or `super().method` → "super().X must be called as super().X()"
- `super(arg)` → "super() takes no arguments"

### Risk 3: Postfix Chaining Prevention
**Issue:** After parsing `super().method()`, the result goes back through `ParsePostfix()` which could allow `super().method().super()`.

**Mitigation:** This is a semantic error, not a parse error. The AST structure makes `SuperCall` a terminal expression. Chaining like `super().super()` would require `super()` to return something with a `super` method, which semantic analysis will catch.

### Question 1: Super as Identifier
**Q:** What if someone has a variable named `super`?

**A:** Once `super` is a keyword, it's reserved. Users cannot have variables named `super`. This matches Python's behavior where `super` is a builtin that can technically be shadowed but is strongly discouraged.

### Question 2: Keyword Arguments Order
**Q:** Should we enforce positional before keyword args?

**A:** Yes, this matches Python semantics. The parser should handle this - keyword args after positional is allowed, but positional after keyword is not.

---

## Design Decision: Why a Dedicated AST Node?

**Alternative:** Parse `super()` as `FunctionCall` with `Identifier("super")`, then `.method()` as `MemberAccess` + another `FunctionCall`.

**Chosen Approach:** Dedicated `SuperCall` AST node.

**Rationale:**
1. **Grammar Enforcement:** The spec requires `super()` to always be followed by `.method()`. A dedicated node enforces this at parse time.
2. **Semantic Analysis:** Easier to validate context rules (must be in `__init__`, `@override`, or dunder method).
3. **Code Generation:** Direct mapping to C# `base.Method()` without reconstructing the pattern from multiple AST nodes.
4. **Error Messages:** Better error messages ("super() must be followed by method call" vs generic "unexpected token").

---

## Implementation Order

1. **Lexer changes** (Token.cs, Lexer.cs) - 5 minutes
2. **AST node** (Expression.cs) - 5 minutes
3. **Parser method** (Parser.cs - `ParseSuperCall()`) - 20 minutes
4. **Parser integration** (Parser.cs - `ParsePrimary()`) - 5 minutes
5. **Unit tests** - 30 minutes
6. **Integration verification** - 15 minutes

**Total Estimated Time:** 1.5-2 hours
