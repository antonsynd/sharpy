# Implementation Plan: Pipe Operator (`|>`)

**Task ID**: 0.1.1.3
**Status**: Partially Implemented (Lexer complete, Parser/CodeGen missing)

---

## Executive Summary

The pipe operator (`|>`) is **partially implemented**:
- **Lexer**: Complete - `TokenType.PipeForward` is defined and tokenization works
- **Parser**: NOT implemented - no `ParsePipe()` method or `PipeExpression` AST node
- **Code Generation**: NOT implemented - no `GeneratePipeExpression()` handler

---

## Current State Analysis

### What Already Exists

| Component | Status | Location |
|-----------|--------|----------|
| Token Type | `TokenType.PipeForward` | `src/Sharpy.Compiler/Lexer/Token.cs:133` |
| Lexer Recognition | `\|>` → `PipeForward` | `src/Sharpy.Compiler/Lexer/Lexer.cs:1621-1624` |
| Lexer Tests | 2 tests passing | `LexerTests.cs:649-656`, `Phase010ExitCriteriaTests.cs` |

### What's Missing

| Component | Required Action |
|-----------|-----------------|
| AST Node | Add `PipeExpression` record |
| Parser Method | Add `ParsePipe()` |
| Precedence Integration | Insert between `ParseComparison` and `ParseBitwiseOr` |
| Code Generator | Add `GeneratePipeExpression()` |
| Parser Tests | Add tests for parsing pipe expressions |
| Integration Tests | Add end-to-end tests |

---

## Step-by-Step Implementation Approach

### Step 1: Add AST Node (Expression.cs)

**File**: `src/Sharpy.Compiler/Parser/Ast/Expression.cs`
**Location**: After line 371 (after `Parenthesized` record, before `#endregion`)

```csharp
/// <summary>
/// Pipe forward expression (left |> right)
/// The left operand is passed as the first argument to the right operand.
/// Example: data |> transform() becomes transform(data)
/// </summary>
public record PipeExpression : Expression
{
    public Expression Left { get; init; } = null!;
    public Expression Right { get; init; } = null!;
}
```

### Step 2: Add Parser Method (Parser.cs)

**File**: `src/Sharpy.Compiler/Parser/Parser.cs`
**Location**: After `ParseBitwiseOr()` (after line 1589)

```csharp
private Expression ParsePipe()
{
    var left = ParseBitwiseOr();

    while (Current.Type == TokenType.PipeForward)
    {
        Advance();
        var right = ParseBitwiseOr();

        left = new PipeExpression
        {
            Left = left,
            Right = right,
            LineStart = left.LineStart,
            ColumnStart = left.ColumnStart,
            LineEnd = right.LineEnd,
            ColumnEnd = right.ColumnEnd
        };
    }

    return left;
}
```

### Step 3: Update Precedence Chain (Parser.cs)

**File**: `src/Sharpy.Compiler/Parser/Parser.cs`
**Location**: Line 1443 in `ParseComparison()`

**Change from**:
```csharp
private Expression ParseComparison()
{
    var left = ParseBitwiseOr();
```

**Change to**:
```csharp
private Expression ParseComparison()
{
    var left = ParsePipe();
```

### Step 4: Add Code Generator Handler (RoslynEmitter.cs)

**File**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

#### 4a. Add case to `GenerateExpression()` switch (around line 1654)

**Location**: After line 1654 (`ComparisonChain chain => GenerateComparisonChain(chain),`)

```csharp
PipeExpression pipe => GeneratePipeExpression(pipe),
```

#### 4b. Add `GeneratePipeExpression()` method (after `GenerateComparisonChain`)

```csharp
private ExpressionSyntax GeneratePipeExpression(PipeExpression pipe)
{
    // Semantics: left |> right
    // - If right is a function call f(a, b), result is f(left, a, b)
    // - If right is an identifier f, result is f(left)
    // - If right is a member access obj.method, result is obj.method(left)

    var leftExpr = GenerateExpression(pipe.Left);

    switch (pipe.Right)
    {
        case FunctionCall funcCall:
            // right(a, b, c) becomes right(left, a, b, c)
            var funcExpr = GenerateExpression(funcCall.Function);
            var args = new List<ArgumentSyntax> { Argument(leftExpr) };
            args.AddRange(funcCall.Arguments.Select(a => Argument(GenerateExpression(a))));
            return InvocationExpression(funcExpr)
                .WithArgumentList(ArgumentList(SeparatedList(args)));

        case Identifier id:
            // right becomes right(left)
            var name = _context.IsBuiltinFunction(id.Name)
                ? $"global::Sharpy.Core.Exports.{NameMangler.ToPascalCase(id.Name)}"
                : NameMangler.ToPascalCase(id.Name);
            return InvocationExpression(ParseName(name))
                .AddArgumentListArguments(Argument(leftExpr));

        case MemberAccess memberAccess:
            // obj.method becomes obj.method(left)
            var memberExpr = GenerateExpression(pipe.Right);
            return InvocationExpression(memberExpr)
                .AddArgumentListArguments(Argument(leftExpr));

        default:
            // For other expressions, assume they evaluate to a callable and invoke with left
            var callable = GenerateExpression(pipe.Right);
            return InvocationExpression(callable)
                .AddArgumentListArguments(Argument(leftExpr));
    }
}
```

---

## Key Files to Modify

| File | Changes |
|------|---------|
| `src/Sharpy.Compiler/Parser/Ast/Expression.cs` | Add `PipeExpression` record |
| `src/Sharpy.Compiler/Parser/Parser.cs` | Add `ParsePipe()`, update `ParseComparison()` |
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Add `GeneratePipeExpression()`, update switch |

---

## Tests to Verify

### Parser Tests to Add

**File**: `src/Sharpy.Compiler.Tests/Parser/ParserTests.cs` (or create new `PipeOperatorTests.cs`)

```csharp
[Fact]
public void Parse_SimplePipeExpression_CreatesPipeExpression()
{
    var source = "x |> f()";
    var ast = Parse(source);
    var expr = GetExpression(ast);

    expr.Should().BeOfType<PipeExpression>();
    var pipe = (PipeExpression)expr;
    pipe.Left.Should().BeOfType<Identifier>();
    pipe.Right.Should().BeOfType<FunctionCall>();
}

[Fact]
public void Parse_ChainedPipeExpression_IsLeftAssociative()
{
    // x |> f() |> g() should parse as ((x |> f()) |> g())
    var source = "x |> f() |> g()";
    var ast = Parse(source);
    var expr = GetExpression(ast);

    expr.Should().BeOfType<PipeExpression>();
    var outer = (PipeExpression)expr;
    outer.Left.Should().BeOfType<PipeExpression>();
    outer.Right.Should().BeOfType<FunctionCall>();
}

[Fact]
public void Parse_PipePrecedence_LowerThanBitwiseOr()
{
    // a | b |> f() should parse as (a | b) |> f()
    var source = "a | b |> f()";
    var ast = Parse(source);
    var expr = GetExpression(ast);

    expr.Should().BeOfType<PipeExpression>();
    var pipe = (PipeExpression)expr;
    pipe.Left.Should().BeOfType<BinaryOp>()
        .Which.Operator.Should().Be(BinaryOperator.BitwiseOr);
}

[Fact]
public void Parse_PipePrecedence_HigherThanComparison()
{
    // x |> f() == 5 should parse as (x |> f()) == 5
    var source = "x |> f() == 5";
    var ast = Parse(source);
    var expr = GetExpression(ast);

    // Should be a comparison with pipe on left
    expr.Should().Match<Expression>(e =>
        e is BinaryOp { Operator: BinaryOperator.Equal } ||
        e is ComparisonChain);
}

[Fact]
public void Parse_PipeWithIdentifier_ParsesCorrectly()
{
    var source = "data |> transform";
    var ast = Parse(source);
    var expr = GetExpression(ast);

    expr.Should().BeOfType<PipeExpression>();
    var pipe = (PipeExpression)expr;
    pipe.Right.Should().BeOfType<Identifier>();
}
```

### Code Generation Tests to Add

**File**: `src/Sharpy.Compiler.Tests/CodeGen/RoslynEmitterExpressionTests.cs`

```csharp
[Fact]
public void Emit_PipeWithFunctionCall_InsertsAsFirstArgument()
{
    var source = "x |> f(a, b)";
    var code = EmitExpression(source);

    // Should generate: F(x, a, b)
    code.Should().Contain("F(");
}

[Fact]
public void Emit_PipeWithIdentifier_CreatesInvocation()
{
    var source = "data |> transform";
    var code = EmitExpression(source);

    // Should generate: Transform(data)
    code.Should().Contain("Transform(");
}

[Fact]
public void Emit_ChainedPipe_GeneratesNestedCalls()
{
    var source = "[1, 2, 3] |> filter(x => x > 1) |> map(x => x * 2)";
    var code = EmitExpression(source);

    // Should generate nested calls
    code.Should().Contain("Map(Filter(");
}
```

### Integration Tests

**File**: `src/Sharpy.Compiler.Tests/Integration/PipeOperatorIntegrationTests.cs`

```csharp
[Fact]
public void Execute_SimplePipe_ReturnsCorrectResult()
{
    var source = @"
def double(x):
    return x * 2

result = 5 |> double()
print(result)
";
    var output = Execute(source);
    output.Should().Be("10");
}

[Fact]
public void Execute_ChainedPipes_ReturnsCorrectResult()
{
    var source = @"
result = [1, 2, 3, 4, 5] |> filter(lambda x: x > 2) |> map(lambda x: x * 2) |> list()
print(result)
";
    var output = Execute(source);
    output.Should().Contain("[6, 8, 10]");
}
```

---

## Precedence Verification

The final precedence chain should be:

```
ParseComparison()      [==, <, >, in, is, etc.]
    ↓ calls
ParsePipe()            [|>] NEW
    ↓ calls
ParseBitwiseOr()       [|]
    ↓ calls
ParseBitwiseXor()      [^]
    ...
```

This ensures:
- `a + b |> f()` → `(a + b) |> f()`
- `a |> f() == 5` → `(a |> f()) == 5`
- `a | b |> f()` → `(a | b) |> f()`
- `a |> b |> c` → `((a |> b) |> c)` (left-associative)

---

## Potential Risks and Questions

### Risks

1. **Type Safety**: The pipe operator assumes the right side is callable. Without semantic analysis, invalid pipes (e.g., `5 |> 10`) won't error until runtime.

2. **Member Access Handling**: `obj |> method()` vs `obj |> Type.method()` may need different handling.

3. **Lambda Edge Cases**: `x |> (lambda y: y * 2)` - ensure lambdas work correctly as pipe targets.

4. **Partial Application**: Does `x |> f(_, b)` syntax need support for placeholder-based partial application?

### Questions for Clarification

1. **Semantic Validation**: Should the compiler validate that the right operand is callable at compile time, or defer to runtime?

2. **Method References**: Should `data |> str.upper` work (method reference without parentheses)?

3. **Async Support**: Should `await data |> async_transform()` have special handling?

4. **Error Messages**: What error message for `5 |> 10`? "Right operand of pipe must be callable"?

---

## Checklist

- [ ] Add `PipeExpression` AST node to Expression.cs
- [ ] Add `ParsePipe()` method to Parser.cs
- [ ] Update `ParseComparison()` to call `ParsePipe()`
- [ ] Add case to `GenerateExpression()` switch
- [ ] Implement `GeneratePipeExpression()` method
- [ ] Add parser unit tests
- [ ] Add code generation unit tests
- [ ] Add integration tests
- [ ] Verify all existing tests still pass
- [ ] Update language documentation (if applicable)
