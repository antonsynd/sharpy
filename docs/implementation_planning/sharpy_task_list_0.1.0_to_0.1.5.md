# Sharpy Compiler Task List: Phases 0.1.0 – 0.1.5

> **Purpose**: This document provides actionable tasks for implementing phases 0.1.0 through 0.1.5 of the Sharpy compiler. Each task includes acceptance criteria, verification steps, and notes about existing implementation that may need review.

> **Important**: There is existing work on the lexer, parser, and code generator. Before implementing anything, **audit the existing code** against the specification. Some tasks may be partially or fully complete; others may have inconsistencies that need fixing.

---

## Legend

| Symbol | Meaning |
|--------|---------|
| 🔍 | Audit existing code first |
| 🆕 | New implementation required |
| ⚠️ | Potential inconsistency to check |
| ✅ | Verification step |
| 📁 | File location |

---

## Phase 0.1.0: Lexer Foundation

**Goal**: Tokenize Sharpy source code with Python-style indentation handling.

### Task 0.1.0.1: Audit Existing Token Types

🔍 **Status Check**: The lexer has existing token types in `Token.cs`.

📁 **Files**: `src/Sharpy.Compiler/Lexer/Token.cs`

**Actions**:

1. [ ] Open `src/Sharpy.Compiler/Lexer/Token.cs` and compare `TokenType` enum against the spec
2. [ ] Create a checklist comparing spec keywords vs. implemented keywords:

   **Required Keywords (per spec)**:
   - [ ] `def`, `class`, `struct`, `interface`, `enum`
   - [ ] `if`, `elif`, `else`, `while`, `for`, `in`
   - [ ] `break`, `continue`, `return`, `pass`
   - [ ] `True`, `False`, `None`
   - [ ] `and`, `or`, `not`, `is`
   - [ ] `import`, `from`, `as`
   - [ ] `raise`, `try`, `except`, `finally`
   - [ ] `lambda`, `type`, `const`, `assert`, `auto`
   - [ ] `case`, `event`, `match`, `maybe`, `property`, `to`, `with`, `yield`
   - [ ] `async`, `await`, `del`

   **Soft Keywords (per spec)**:
   - [ ] `_`, `get`, `set`, `init`

3. [ ] Document any **missing** keywords
4. [ ] Document any **extra** keywords not in spec (may be intentional)

**Verification**:
- ✅ Run: `grep -E "TokenType\." src/Sharpy.Compiler/Lexer/Token.cs | wc -l` to count token types
- ✅ Compare count against spec's expected ~50+ token types

---

### Task 0.1.0.2: Audit/Implement Missing Keywords

🔍 **Status Check**: Some keywords may be missing from the lexer's keyword dictionary.

📁 **Files**: `src/Sharpy.Compiler/Lexer/Lexer.cs` (look for `Keywords` dictionary)

**Actions**:

1. [ ] Locate the `Keywords` dictionary in `Lexer.cs`
2. [ ] Cross-reference with Task 0.1.0.1's checklist
3. [ ] For each missing keyword:
   - [ ] Add `TokenType` entry in `Token.cs` if not present
   - [ ] Add mapping in `Keywords` dictionary in `Lexer.cs`

**Missing Keywords to Add (likely candidates based on spec)**:
```csharp
// Add to Token.cs if missing:
TokenType.Type,      // for 'type' keyword (type aliases)
TokenType.Case,      // for 'case' keyword (future pattern matching)
TokenType.Event,     // for 'event' keyword (future events)
TokenType.Match,     // for 'match' keyword (future pattern matching)
TokenType.Maybe,     // for 'maybe' keyword (if applicable)
TokenType.Property,  // for 'property' keyword (future properties)
TokenType.To,        // for 'to' keyword (type coercion: `value to int`)
TokenType.Yield,     // for 'yield' keyword (future generators)
TokenType.Del,       // for 'del' keyword (deletion)
```

**Verification**:
- ✅ Write test: Each new keyword tokenizes to correct `TokenType`
- ✅ Run: `dotnet test --filter "FullyQualifiedName~Lexer"` — all tests pass

---

### Task 0.1.0.3: Audit/Implement Operators

🔍 **Status Check**: Operators appear largely implemented but verify completeness.

📁 **Files**: `src/Sharpy.Compiler/Lexer/Lexer.cs`, `src/Sharpy.Compiler/Lexer/Token.cs`

**Actions**:

1. [ ] Verify these operators exist and tokenize correctly:

   **Arithmetic**:
   - [ ] `+` → `Plus`
   - [ ] `-` → `Minus`
   - [ ] `*` → `Star`
   - [ ] `/` → `Slash`
   - [ ] `//` → `DoubleSlash`
   - [ ] `%` → `Percent`
   - [ ] `**` → `DoubleStar`

   **Comparison**:
   - [ ] `==` → `Equal`
   - [ ] `!=` → `NotEqual`
   - [ ] `<` → `Less`
   - [ ] `<=` → `LessEqual`
   - [ ] `>` → `Greater`
   - [ ] `>=` → `GreaterEqual`

   **Assignment**:
   - [ ] `=` → `Assign`
   - [ ] `+=` → `PlusAssign`
   - [ ] `-=` → `MinusAssign`
   - [ ] `*=` → `StarAssign`
   - [ ] `/=` → `SlashAssign`
   - [ ] `//=` → `DoubleSlashAssign`
   - [ ] `%=` → `PercentAssign`

   **Special (Sharpy-specific)**:
   - [ ] `|>` → `PipeOperator` (function pipe)
   - [ ] `??` → `NullCoalesce`
   - [ ] `?.` → `NullConditional`

2. [ ] Check for `|>` (pipe operator) — this is Sharpy-specific and may be missing

**Verification**:
- ✅ Test file: `src/Sharpy.Compiler.Tests/Lexer/LexerTests.cs`
- ✅ Run operator tests: `dotnet test --filter "Tokenize_*Operator*"`

---

### Task 0.1.0.4: Verify Indentation Handling (INDENT/DEDENT)

🔍 **Status Check**: INDENT/DEDENT logic exists in `Lexer.cs`.

📁 **Files**: `src/Sharpy.Compiler/Lexer/Lexer.cs`

**Actions**:

1. [ ] Verify the lexer tracks indentation via `_indentStack`
2. [ ] Verify `INDENT` tokens are emitted when indentation increases
3. [ ] Verify `DEDENT` tokens are emitted when indentation decreases
4. [ ] Verify **4-space requirement** per indentation level (spec says "4-space required per tab")
5. [ ] Verify **tabs are disallowed** (spec: "no actual tabs")

**Test Cases to Verify**:
```python
# Test case 1: Basic indentation
def foo():
    if True:
        return 1
    return 0

# Expected tokens include:
# Def, Identifier("foo"), LeftParen, RightParen, Colon, Newline,
# INDENT, If, True, Colon, Newline,
# INDENT, Return, Integer(1), Newline,
# DEDENT, Return, Integer(0), Newline,
# DEDENT, EOF
```

**Verification**:
- ✅ Run: `dotnet test --filter "Indentation"`
- ✅ Manual test: Create a multi-level indented file and verify token stream

---

### Task 0.1.0.5: Verify Numeric Literals

🔍 **Status Check**: Numeric parsing exists in `Lexer.cs`.

📁 **Files**: `src/Sharpy.Compiler/Lexer/Lexer.cs` (look for `ReadNumber()`)

**Actions**:

1. [ ] Verify **integer formats**:
   - [ ] Decimal: `42`
   - [ ] Hexadecimal: `0x2A`, `0X2A`
   - [ ] Binary: `0b101010`, `0B101010`
   - [ ] Octal: `0o52`, `0O52`

2. [ ] Verify **float formats**:
   - [ ] Basic: `3.14`
   - [ ] Scientific: `1e10`, `2.5e-3`, `1E10`

3. [ ] Verify **type suffixes** (⚠️ may be missing):
   - [ ] Long: `42L`
   - [ ] Float: `3.14f`
   - [ ] Double: `3.14d`
   - [ ] Decimal: `100m`

**Verification**:
- ✅ Run: `dotnet test --filter "Number"` or `dotnet test --filter "Numeric"`
- ✅ Add tests for any missing suffix support

---

### Task 0.1.0.6: Verify String Literals

🔍 **Status Check**: String parsing exists with f-string support.

📁 **Files**: `src/Sharpy.Compiler/Lexer/Lexer.cs` (look for `ReadString()`, `ReadFString*()`, `ReadRawString()`)

**Actions**:

1. [ ] Verify **basic strings**:
   - [ ] Single quotes: `'hello'`
   - [ ] Double quotes: `"world"`

2. [ ] Verify **triple-quoted strings**:
   - [ ] `'''multiline'''`
   - [ ] `"""multiline"""`

3. [ ] Verify **raw strings**:
   - [ ] `r"path\to\file"`
   - [ ] `r'path\to\file'`

4. [ ] Verify **f-strings** (basic):
   - [ ] `f"Hello {name}"`
   - [ ] Correct token sequence: `FStringStart`, `FStringText`, `FStringExprStart`, expression tokens, `FStringExprEnd`, `FStringEnd`

**Verification**:
- ✅ Run: `dotnet test --filter "String"` or `dotnet test --filter "FString"`
- ✅ Verify f-string tests cover nested expressions

---

### Task 0.1.0.7: Verify Comments

🔍 **Status Check**: Comment handling should exist.

📁 **Files**: `src/Sharpy.Compiler/Lexer/Lexer.cs`

**Actions**:

1. [ ] Verify single-line comments are skipped: `# comment`
2. [ ] Verify triple-quoted strings as docstrings are handled (may be tokenized as strings initially)

**Verification**:
- ✅ Test: `"x = 42 # comment"` tokenizes to `Identifier, Assign, Integer, EOF` (no comment token visible)
- ✅ Run: `dotnet test --filter "Comment"`

---

### Task 0.1.0.8: Create Phase 0.1.0 Exit Criteria Test Suite

🆕 **New Implementation**: Create comprehensive test file for phase 0.1.0.

📁 **Files**: `src/Sharpy.Compiler.Tests/Lexer/Phase010ExitCriteriaTests.cs` (new file)

**Actions**:

1. [ ] Create test file with the following test cases:
   ```csharp
   [Fact]
   public void ExitCriteria_AllTokenTypesRecognized() { ... }
   
   [Fact]
   public void ExitCriteria_IndentDedentEmittedCorrectly() { ... }
   
   [Fact]
   public void ExitCriteria_NumericLiteralsWithSuffixes() { ... }
   
   [Fact]
   public void ExitCriteria_AllStringVariantsTokenized() { ... }
   
   [Fact]
   public void ExitCriteria_CommentsStripped() { ... }
   ```

2. [ ] Use the spec's test case as the primary integration test:
   ```python
   # Basic tokenization
   x = 42
   y = 3.14
   name = "Alice"
   
   # Indentation
   def foo():
       if True:
           return 1
       return 0
   ```

**Verification**:
- ✅ All tests in `Phase010ExitCriteriaTests.cs` pass
- ✅ Run: `dotnet test --filter "Phase010"`

---

## Phase 0.1.1: Parser Foundation

**Goal**: Parse Sharpy source into an Abstract Syntax Tree (AST).

### Task 0.1.1.1: Audit Existing AST Nodes

🔍 **Status Check**: AST nodes exist in `src/Sharpy.Compiler/Parser/Ast/`.

📁 **Files**: 
- `src/Sharpy.Compiler/Parser/Ast/Node.cs`
- `src/Sharpy.Compiler/Parser/Ast/Expression.cs`
- `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

**Actions**:

1. [ ] Verify these **core AST nodes** exist:
   - [ ] `Module` — root node containing statements
   - [ ] `ExpressionStatement` — standalone expressions
   - [ ] `Identifier` — variable/function names
   - [ ] `IntegerLiteral` — integer values
   - [ ] `FloatLiteral` — float values
   - [ ] `StringLiteral` — string values
   - [ ] `BooleanLiteral` — `True`/`False`
   - [ ] `NoneLiteral` — `None`
   - [ ] `BinaryOp` / `BinaryExpression` — `a + b`, `a == b`
   - [ ] `UnaryOp` / `UnaryExpression` — `-x`, `not x`
   - [ ] `PassStatement` — placeholder statement

2. [ ] Verify each node has **source location tracking**:
   ```csharp
   public int LineStart { get; init; }
   public int ColumnStart { get; init; }
   public int LineEnd { get; init; }
   public int ColumnEnd { get; init; }
   ```

**Verification**:
- ✅ Each node class inherits from `Node` or has location properties
- ✅ Run: `grep -r "LineStart" src/Sharpy.Compiler/Parser/Ast/`

---

### Task 0.1.1.2: Audit/Verify Operator Precedence

🔍 **Status Check**: Parser uses precedence climbing.

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`

**Actions**:

1. [ ] Map parser methods to spec precedence levels:

   | Precedence | Spec Operators | Parser Method (expected) |
   |------------|----------------|--------------------------|
   | Highest | Primary (literals, identifiers, parens, `.`, `?.`) | `ParsePrimary()`, `ParsePostfix()` |
   | | Unary: `-x`, `+x`, `~x`, `not x` | `ParseUnary()` |
   | | Power: `**` (right-associative) | `ParsePower()` |
   | | Multiplicative: `*`, `/`, `//`, `%` | `ParseMultiplicative()` |
   | | Additive: `+`, `-` | `ParseAdditive()` |
   | | Bitwise shifts: `<<`, `>>` | `ParseShift()` |
   | | Bitwise: `&`, `^`, `|` | `ParseBitwiseAnd()`, etc. |
   | | Pipe: `|>` | `ParsePipe()` (⚠️ may be missing) |
   | | Type coercion: `to` | `ParseTypeCast()` (⚠️ may be missing) |
   | | Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`, `in`, `is` | `ParseComparison()` |
   | | Logical: `not`, `and`, `or` | `ParseLogicalAnd()`, `ParseLogicalOr()` |
   | | Null coalesce: `??` | `ParseNullCoalesce()` |
   | Lowest | Conditional: `x if test else y` | `ParseConditionalExpression()` |

2. [ ] Verify **right-associativity** of `**`:
   - `2 ** 3 ** 2` should parse as `2 ** (3 ** 2)` = 512, not `(2 ** 3) ** 2` = 64

3. [ ] Check for **comparison chaining**:
   - `a < b < c` should parse as `(a < b) and (b < c)` with `b` evaluated once

**Verification**:
- ✅ Test: `"2 ** 3 ** 2"` parses with right-associative structure
- ✅ Test: `"1 + 2 * 3"` parses as `1 + (2 * 3)`
- ✅ Run: `dotnet test --filter "Precedence"`

---

### Task 0.1.1.3: Implement/Verify Pipe Operator (`|>`)

⚠️ **Potential Gap**: The `|>` operator may not be implemented.

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`

**Actions**:

1. [ ] Search for `|>` or `PipeOperator` in parser
2. [ ] If missing, implement:
   ```csharp
   // In precedence hierarchy, between Bitwise and Comparison
   private Expression ParsePipe()
   {
       var left = ParseBitwiseOr();
       while (Current.Type == TokenType.PipeOperator)
       {
           Advance();
           var right = ParseBitwiseOr();
           left = new PipeExpression
           {
               Left = left,
               Right = right,
               // ... location tracking
           };
       }
       return left;
   }
   ```

3. [ ] Add `PipeExpression` AST node if missing:
   ```csharp
   public record PipeExpression : Expression
   {
       public Expression Left { get; init; } = null!;
       public Expression Right { get; init; } = null!;  // Should be FunctionCall
   }
   ```

**Verification**:
- ✅ Test: `"x |> f"` parses correctly
- ✅ Test: `"x |> f |> g"` chains left-to-right

---

### Task 0.1.1.4: Implement/Verify `to` Operator (Type Coercion)

⚠️ **Potential Gap**: The `to` keyword for type coercion may not be implemented.

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Parser/Ast/Expression.cs`

**Actions**:

1. [ ] Search for `to` keyword handling in parser
2. [ ] If missing, implement:
   - Grammar: `value to Type` or `value to Type?`
   - Example: `animal to Dog`, `value to int?`

3. [ ] This may need a `TypeCast` or `ToExpression` AST node
   - Check if `TypeCast` already exists and repurpose

**Verification**:
- ✅ Test: `"x to int"` parses correctly
- ✅ Test: `"obj to MyClass?"` parses with nullable target

---

### Task 0.1.1.5: Verify Type Annotation Parsing

🔍 **Status Check**: Type annotations should be parsed but not validated.

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Types.cs`, `src/Sharpy.Compiler/Parser/Parser.cs`

**Actions**:

1. [ ] Verify **simple types** parse: `int`, `int32`, `str`, `bool`, `float`, `float64`
2. [ ] Verify **nullable types** parse: `int32?`, `str?`
3. [ ] Verify **generic types** parse (structure only): `list[int32]`, `dict[str, int]`
4. [ ] Check that `TypeAnnotation` AST node captures:
   - Name (string)
   - IsNullable (bool)
   - TypeArguments (list of TypeAnnotation)

**Verification**:
- ✅ Test: `"x: int"` produces `TypeAnnotation { Name = "int" }`
- ✅ Test: `"x: list[int]"` produces nested type structure
- ✅ Run: `dotnet test --filter "Type"` in parser tests

---

### Task 0.1.1.6: Verify Pass Statement Parsing

🔍 **Status Check**: `PassStatement` should exist.

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`

**Actions**:

1. [ ] Verify `pass` keyword is handled in `ParseStatement()` or `ParseSimpleStatement()`
2. [ ] Verify it produces a `PassStatement` AST node

**Verification**:
- ✅ Test: `"pass"` parses to `PassStatement`
- ✅ Test: Indented `pass` works in function body

---

### Task 0.1.1.7: Create Phase 0.1.1 Exit Criteria Test Suite

🆕 **New Implementation**: Create comprehensive parser test file.

📁 **Files**: `src/Sharpy.Compiler.Tests/Parser/Phase011ExitCriteriaTests.cs` (new file)

**Actions**:

1. [ ] Create tests for spec's exit criteria:
   ```csharp
   [Fact]
   public void ExitCriteria_ExpressionPrecedenceCorrect()
   {
       // Test: a + b * c parses as a + (b * c)
   }
   
   [Fact]
   public void ExitCriteria_ParenthesesOverridePrecedence()
   {
       // Test: (a + b) * c parses correctly
   }
   
   [Fact]
   public void ExitCriteria_TypeAnnotationsParsedNotValidated()
   {
       // Test: x: SomeFakeType = 42 parses without error
   }
   
   [Fact]
   public void ExitCriteria_ModuleStructureCaptured()
   {
       // Test: Multiple statements in module
   }
   
   [Fact]
   public void ExitCriteria_ComparisonChainingParsed()
   {
       // Test: a < b < c
   }
   ```

**Verification**:
- ✅ All tests pass
- ✅ Run: `dotnet test --filter "Phase011"`

---

## Phase 0.1.2: Code Generation Bootstrap

**Goal**: Generate executable C# code via Roslyn for minimal programs.

### Task 0.1.2.1: Audit Existing Code Generation

🔍 **Status Check**: `RoslynEmitter` exists and can generate C# code.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Verify entry point generation:
   - Top-level statements should be wrapped in `Main()` method
   - Check for `GenerateMainMethod()` or similar

2. [ ] Verify module class structure:
   - Generated code should have a class wrapper (e.g., `Exports`)

3. [ ] Check compilation pipeline:
   - Lex → Parse → Emit Roslyn AST → Compile to IL → Output DLL/EXE

**Verification**:
- ✅ Run: `dotnet run --project src/Sharpy.Cli -- build samples/minimal.spy` (if exists)
- ✅ Check that output is a valid .NET assembly

---

### Task 0.1.2.2: Verify Type Mapping

🔍 **Status Check**: `TypeMapper` should handle primitive mappings.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`

**Actions**:

1. [ ] Verify these type mappings exist:

   | Sharpy | C# |
   |--------|-----|
   | `int8` | `sbyte` |
   | `int16` | `short` |
   | `int` or `int32` | `int` |
   | `int64` | `long` |
   | `uint8` | `byte` |
   | `uint16` | `ushort` |
   | `uint32` | `uint` |
   | `uint64` | `ulong` |
   | `float32` | `float` |
   | `float` or `float64` | `double` |
   | `decimal` | `decimal` |
   | `bool` | `bool` |
   | `str` | `string` |
   | `object` | `object` |
   | `array[T]` | `T[]` |
   | `None` | `void` (return) / `null` (value) |

2. [ ] Check that `int` maps to `int` (not `int32`) in C# output

**Verification**:
- ✅ Write unit test: `TypeMapper.Map("int32")` returns `int` predefined type
- ✅ Run: `dotnet test --filter "TypeMapper"`

---

### Task 0.1.2.3: Verify Roslyn AST Generation Basics

🔍 **Status Check**: Roslyn syntax factory usage.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Verify these Roslyn syntax types are used:
   - [ ] `CompilationUnitSyntax` for file
   - [ ] `ClassDeclarationSyntax` for module wrapper
   - [ ] `MethodDeclarationSyntax` for `Main()`
   - [ ] `LiteralExpressionSyntax` for constants
   - [ ] `BinaryExpressionSyntax` for operators

2. [ ] Verify **no string templating** — all generation via `SyntaxFactory`

**Verification**:
- ✅ Code review: No `$"public class ..."` string interpolation for C# generation
- ✅ All C# generation uses `SyntaxFactory.*` methods

---

### Task 0.1.2.4: Implement `pass` Statement Code Generation

🔍 **Status Check**: `pass` should compile to empty statement or comment.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Find `PassStatement` handling in emitter
2. [ ] Verify it generates either:
   - Empty block: `{ }`
   - Empty statement: `;`
   - Comment: `// pass`

**Verification**:
- ✅ Test: `"pass"` compiles and runs without error
- ✅ Generated C# is valid

---

### Task 0.1.2.5: Create Minimal Program Test

🆕 **New Implementation**: End-to-end compilation test.

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase012IntegrationTests.cs` (new file)

**Actions**:

1. [ ] Create test that compiles and runs:
   ```python
   pass
   ```
   Expected: Compiles to empty `Main()` that runs without error

2. [ ] Create test for expression evaluation:
   ```python
   42 + 8
   ```
   Expected: Compiles (expression statement, result discarded)

3. [ ] Create test for binary operators:
   ```python
   x = 1 + 2
   y = 3 * 4
   z = x + y
   ```

**Verification**:
- ✅ All tests compile to valid .NET assembly
- ✅ Run: `dotnet test --filter "Phase012"`

---

### Task 0.1.2.6: Document Phase 0.1.2 Exit Criteria

🆕 **New Implementation**: Exit criteria validation.

**Exit Criteria per spec**:
- [ ] `pass` compiles to empty `Main()` and runs
- [ ] Primitive literals compile correctly
- [ ] Binary expressions generate correct C# operators
- [ ] Output is a valid .NET assembly

**Verification**:
- ✅ All integration tests pass
- ✅ Can run compiled assembly: `dotnet <output.dll>`

---

## Phase 0.1.3: Variables & Expressions

**Goal**: Variable declarations, assignments, and full expression support.

### Task 0.1.3.1: Audit Variable Declaration AST

🔍 **Status Check**: Variable declaration AST should exist.

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

**Actions**:

1. [ ] Verify `VariableDeclaration` or `Assignment` AST node exists
2. [ ] Check it captures:
   - Name (string)
   - Type annotation (optional)
   - Initial value (Expression)

3. [ ] Verify parser handles:
   ```python
   x: int = 42        # Typed declaration
   y = 3.14           # Inferred type
   z: auto = expr()   # Explicit inference
   ```

**Verification**:
- ✅ Test: `"x: int = 42"` parses correctly
- ✅ Test: `"y = 3.14"` parses with no type annotation

---

### Task 0.1.3.2: Implement/Verify `auto` Keyword Handling

⚠️ **Potential Gap**: `auto` keyword for explicit type inference.

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/Semantic/TypeResolver.cs`

**Actions**:

1. [ ] Parser: `x: auto = expr` should parse with special `auto` type annotation
2. [ ] Semantic: `auto` should trigger type inference from right-hand side
3. [ ] Code gen: `auto` should produce `var` in C#

**Verification**:
- ✅ Test: `"x: auto = 42"` generates `var x = 42;` in C#
- ✅ Test: `auto` allows shadowing with different type

---

### Task 0.1.3.3: Implement/Verify Augmented Assignment Operators

🔍 **Status Check**: Augmented assignments should be supported.

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Verify parser handles: `+=`, `-=`, `*=`, `/=`, `//=`, `%=`
2. [ ] Verify code generation for each:
   - `x += 1` → `x += 1;` (direct mapping)
   - `x //= 2` → `x = x / 2;` (floor division needs special handling in codegen)

**Note**: `//=` is floor division assignment — may need special code generation.

**Verification**:
- ✅ Test: `"x += 5"` compiles correctly
- ✅ Test: `"x //= 2"` compiles with floor division semantics

---

### Task 0.1.3.4: Implement `const` Declarations

⚠️ **Potential Gap**: `const` keyword for immutable values.

📁 **Files**: Multiple locations

**Actions**:

1. [ ] **Lexer**: Verify `const` keyword tokenizes to `TokenType.Const`
2. [ ] **Parser**: Parse `const PI: float = 3.14159`
   - May need `ConstDeclaration` AST node or flag on `VariableDeclaration`
3. [ ] **Semantic**: Track const-ness in symbol table
4. [ ] **Semantic**: Error on reassignment: `PI = 3` should fail
5. [ ] **CodeGen**: Emit as `const` in C# if compile-time constant, otherwise `readonly`

**Verification**:
- ✅ Test: `const X: int = 42` compiles
- ✅ Test: Reassigning const produces error
- ✅ Run: `dotnet test --filter "Const"`

---

### Task 0.1.3.5: Implement Semantic Analysis (Phase 1)

🆕 **New Implementation**: Basic semantic checks.

📁 **Files**: `src/Sharpy.Compiler/Semantic/` (multiple files)

**Actions**:

1. [ ] **Symbol Table**: Verify `SymbolTable` tracks declared variables
2. [ ] **Name Resolution**: Verify `NameResolver` binds identifiers to declarations
3. [ ] **Type Checking**: Basic assignment type checking:
   ```python
   x: int = "hello"  # Error: str not assignable to int
   ```
4. [ ] **Undefined Variable Error**:
   ```python
   y = x  # Error if x not declared
   ```

**Verification**:
- ✅ Test: Undefined variable produces clear error message
- ✅ Test: Type mismatch produces clear error message
- ✅ Run: `dotnet test --filter "Semantic"`

---

### Task 0.1.3.6: Create Phase 0.1.3 Integration Tests

🆕 **New Implementation**: Comprehensive variable tests.

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase013IntegrationTests.cs`

**Actions**:

1. [ ] Test spec example:
   ```python
   x: int = 10
   y: int = 20
   z = x + y
   z += 5
   
   const MAX: int = 100
   # MAX = 50  # Should error
   ```

2. [ ] Test type inference:
   ```python
   x = 42        # Inferred as int32
   y = 3.14      # Inferred as float64
   ```

3. [ ] Test error cases:
   - Undefined variable
   - Const reassignment
   - Type mismatch

**Verification**:
- ✅ All tests pass
- ✅ Run: `dotnet test --filter "Phase013"`

---

## Phase 0.1.4: Control Flow

**Goal**: Conditional and loop statements.

### Task 0.1.4.1: Audit/Verify If Statement

🔍 **Status Check**: If statement likely exists.

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`, `src/Sharpy.Compiler/Parser/Parser.cs`

**Actions**:

1. [ ] Verify `IfStmt` or `IfStatement` AST node exists with:
   - Condition (Expression)
   - ThenBody (List<Statement>)
   - ElseBody (List<Statement> or null)
   - ElifClauses (List of condition+body pairs)

2. [ ] Verify parser handles:
   ```python
   if condition:
       statement
   elif other:
       statement
   else:
       statement
   ```

3. [ ] Verify code generation produces C# `if`/`else if`/`else`

**Verification**:
- ✅ Test: Basic if statement compiles and runs
- ✅ Test: if/elif/else chain works correctly

---

### Task 0.1.4.2: Audit/Verify While Loop

🔍 **Status Check**: While loop likely exists.

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

**Actions**:

1. [ ] Verify `WhileStmt` AST node exists with:
   - Condition (Expression)
   - Body (List<Statement>)

2. [ ] Verify code generation produces C# `while` loop

**Verification**:
- ✅ Test: While loop compiles and runs
- ✅ Test: Loop terminates correctly on condition change

---

### Task 0.1.4.3: Implement/Verify For Loop with `range()`

⚠️ **Potential Gap**: `range()` function handling.

📁 **Files**: Multiple locations

**Actions**:

1. [ ] Verify `ForStmt` AST node exists with:
   - Variable (string or pattern)
   - Iterable (Expression)
   - Body (List<Statement>)

2. [ ] Implement/verify `range()` handling:
   - `range(10)` → `0..9`
   - `range(0, 10)` → `0..9`
   - `range(0, 10, 2)` → `0, 2, 4, 6, 8`

3. [ ] Code generation options:
   - Transform to C# `for (int i = 0; i < n; i++)`
   - Or use `Enumerable.Range()` with `foreach`

**Verification**:
- ✅ Test: `for i in range(10)` compiles and iterates 10 times
- ✅ Test: `for i in range(0, 10, 2)` iterates with step 2

---

### Task 0.1.4.4: Implement/Verify Break and Continue

🔍 **Status Check**: Break/continue should exist.

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`, `src/Sharpy.Compiler/Semantic/`

**Actions**:

1. [ ] Verify `BreakStatement` and `ContinueStatement` AST nodes exist
2. [ ] Verify semantic analysis validates:
   - Break/continue only inside loops
   - Error: `break` at module level

3. [ ] Verify code generation produces C# `break`/`continue`

**Verification**:
- ✅ Test: Break exits loop correctly
- ✅ Test: Continue skips iteration
- ✅ Test: Break outside loop produces error

---

### Task 0.1.4.5: Implement Control Flow Validation

🆕 **New Implementation**: Semantic checks for control flow.

📁 **Files**: `src/Sharpy.Compiler/Semantic/ControlFlowAnalyzer.cs` (may need creation)

**Actions**:

1. [ ] Track "in loop" context during semantic analysis
2. [ ] Error on `break`/`continue` outside loop
3. [ ] (Optional) Basic unreachable code detection:
   ```python
   while True:
       break
       x = 42  # Unreachable
   ```

**Verification**:
- ✅ Test: `break` at top level produces error
- ✅ Test: `continue` outside loop produces error

---

### Task 0.1.4.6: Create Phase 0.1.4 Integration Tests

🆕 **New Implementation**: Control flow tests.

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase014IntegrationTests.cs`

**Actions**:

1. [ ] Test factorial example from spec:
   ```python
   n: int = 5
   result: int = 1
   while n > 1:
       result *= n
       n -= 1
   # result should be 120
   ```

2. [ ] Test FizzBuzz-style logic:
   ```python
   for i in range(1, 16):
       if i % 15 == 0:
           pass  # "FizzBuzz"
       elif i % 3 == 0:
           pass  # "Fizz"
       elif i % 5 == 0:
           pass  # "Buzz"
       else:
           pass  # i
   ```

3. [ ] Test break/continue:
   ```python
   count: int = 0
   for i in range(100):
       if i == 10:
           break
       if i % 2 == 0:
           continue
       count += 1
   # count should be 5 (odd numbers 1, 3, 5, 7, 9)
   ```

**Verification**:
- ✅ All tests pass
- ✅ Run: `dotnet test --filter "Phase014"`

---

## Phase 0.1.5: Functions

**Goal**: Function definitions with parameters and return values.

### Task 0.1.5.1: Audit/Verify Function Definition AST

🔍 **Status Check**: `FunctionDef` likely exists.

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

**Actions**:

1. [ ] Verify `FunctionDef` AST node exists with:
   - Name (string)
   - Parameters (List<Parameter>)
   - ReturnType (TypeAnnotation or null)
   - Body (List<Statement>)
   - Decorators (List<Expression>) — may not be implemented yet

2. [ ] Verify `Parameter` node has:
   - Name (string)
   - Type (TypeAnnotation)
   - DefaultValue (Expression or null)

**Verification**:
- ✅ AST structure matches spec requirements
- ✅ Run: `grep -A 20 "record FunctionDef" src/Sharpy.Compiler/Parser/Ast/Statement.cs`

---

### Task 0.1.5.2: Verify Function Parameter Parsing

🔍 **Status Check**: Parameter parsing should work.

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`

**Actions**:

1. [ ] Verify positional parameters:
   ```python
   def add(x: int, y: int) -> int:
       return x + y
   ```

2. [ ] Verify default parameters:
   ```python
   def greet(name: str, greeting: str = "Hello") -> str:
       return f"{greeting}, {name}!"
   ```

3. [ ] Verify parser enforces: defaults must be compile-time constants
   - Allowed: literals, `None`, enum values, `const` references
   - Error: `def foo(items: list[int] = [])` — mutable default

**Verification**:
- ✅ Test: Positional parameters parse correctly
- ✅ Test: Default parameters parse correctly
- ✅ Test: Mutable default produces error (if enforced at parse time)

---

### Task 0.1.5.3: Implement Default Parameter Validation

🆕 **New Implementation**: Semantic validation for defaults.

📁 **Files**: `src/Sharpy.Compiler/Semantic/DefaultParameterValidator.cs` (may need creation)

**Actions**:

1. [ ] During semantic analysis, validate default values:
   - Must be compile-time constant expressions
   - Error on mutable defaults: `[]`, `{}`, `set()`

2. [ ] `None` only allowed for nullable parameter types:
   ```python
   def foo(x: int = None)   # Error: int is not nullable
   def bar(x: int? = None)  # OK
   ```

**Verification**:
- ✅ Test: `def f(x: list[int] = [])` produces error
- ✅ Test: `def f(x: int? = None)` is valid

---

### Task 0.1.5.4: Implement Keyword Arguments

⚠️ **Potential Gap**: Keyword argument handling at call sites.

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`, `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] **Parser**: Handle keyword arguments in function calls:
   ```python
   result = greet(name="Alice", greeting="Hi")
   result = greet("Bob", greeting="Hey")  # Mixed positional and keyword
   ```

2. [ ] Verify `FunctionCall` AST node supports named arguments:
   ```csharp
   public record Argument
   {
       public string? Name { get; init; }  // null for positional
       public Expression Value { get; init; }
   }
   ```

3. [ ] **Semantic**: Resolve keyword args to parameter positions
4. [ ] **CodeGen**: Generate C# named arguments: `Greet(name: "Alice", greeting: "Hi")`

**Verification**:
- ✅ Test: Keyword-only call works
- ✅ Test: Mixed positional and keyword works
- ✅ Test: Unknown keyword produces error

---

### Task 0.1.5.5: Verify Return Statement

🔍 **Status Check**: Return statement should exist.

📁 **Files**: `src/Sharpy.Compiler/Parser/Ast/Statement.cs`

**Actions**:

1. [ ] Verify `ReturnStatement` AST node exists with:
   - Value (Expression or null for void returns)

2. [ ] Verify `-> None` functions may omit return or use `return` without value

**Verification**:
- ✅ Test: `return x + y` works
- ✅ Test: `return` (no value) works in `-> None` functions

---

### Task 0.1.5.6: Implement Return Type Validation

🆕 **New Implementation**: All paths must return compatible type.

📁 **Files**: `src/Sharpy.Compiler/Semantic/ReturnTypeChecker.cs` (may need creation)

**Actions**:

1. [ ] Track return type of function being analyzed
2. [ ] Verify all code paths return compatible type:
   ```python
   def maybe_double(x: int) -> int:
       if x > 0:
           return x * 2
       # Error: missing return on else path
   ```

3. [ ] `-> None` functions should:
   - Allow omitting return entirely
   - Allow `return` without value
   - Error on `return value`

**Verification**:
- ✅ Test: Missing return path produces error
- ✅ Test: Wrong return type produces error
- ✅ Test: `-> None` with `return 42` produces error

---

### Task 0.1.5.7: Implement Function Code Generation

🔍 **Status Check**: Function generation likely exists.

📁 **Files**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`

**Actions**:

1. [ ] Verify functions generate as C# static methods (for module-level functions)
2. [ ] Verify name mangling: `hello_world` → `HelloWorld` (PascalCase)
3. [ ] Verify parameter generation with types
4. [ ] Verify default parameter values in C# signature

**Example transformation**:
```python
def add(a: int, b: int = 1) -> int:
    return a * b
```
Should generate:
```csharp
public static int Add(int a, int b = 1)
{
    return a * b;
}
```

**Verification**:
- ✅ Test: Generated C# compiles
- ✅ Test: Default values work in generated code

---

### Task 0.1.5.8: (Optional) Function Overloading

⚠️ **Marked Optional in Spec**: Same name, different signatures.

📁 **Files**: `src/Sharpy.Compiler/Semantic/OverloadResolver.cs` (would need creation)

**Actions**:

1. [ ] Decide: Implement for 0.1.5 or defer to later?
2. [ ] If implementing:
   - Allow multiple functions with same name, different signatures
   - Semantic analysis: Build overload sets
   - Call resolution: Match by argument types

**Note**: Python doesn't have overloading, so this is a Sharpy-specific feature. May be simpler to defer.

**Verification**:
- ✅ If implemented: Test overload resolution
- ✅ If deferred: Document in phase notes

---

### Task 0.1.5.9: Create Phase 0.1.5 Integration Tests

🆕 **New Implementation**: Function tests.

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/Phase015IntegrationTests.cs`

**Actions**:

1. [ ] Test spec examples:
   ```python
   def add(a: int, b: int) -> int:
       return a + b
   
   def multiply(a: int, b: int = 1) -> int:
       return a * b
   
   x = add(2, 3)           # 5
   y = multiply(4)          # 4
   z = multiply(4, b=5)     # 20
   ```

2. [ ] Test recursive function:
   ```python
   def factorial(n: int) -> int:
       if n <= 1:
           return 1
       return n * factorial(n - 1)
   
   result = factorial(5)  # 120
   ```

3. [ ] Test void function:
   ```python
   def greet(name: str) -> None:
       pass  # print deferred
   
   greet("World")
   ```

**Verification**:
- ✅ All tests pass
- ✅ Run: `dotnet test --filter "Phase015"`

---

## Summary Checklist

### Phase 0.1.0: Lexer Foundation
- [ ] All token types implemented and tested
- [ ] INDENT/DEDENT working correctly
- [ ] Numeric literals with suffixes
- [ ] All string variants (basic, triple, raw, f-string)
- [ ] Comments stripped

### Phase 0.1.1: Parser Foundation
- [ ] All required AST nodes exist
- [ ] Operator precedence correct
- [ ] Pipe operator (`|>`) implemented
- [ ] `to` operator implemented
- [ ] Type annotations parse correctly
- [ ] Pass statement works

### Phase 0.1.2: Code Generation Bootstrap
- [ ] Entry point generation works
- [ ] Type mapping complete
- [ ] `pass` compiles to valid C#
- [ ] Binary expressions generate correctly
- [ ] Output is valid .NET assembly

### Phase 0.1.3: Variables & Expressions
- [ ] Variable declarations with types
- [ ] Type inference working
- [ ] `auto` keyword working
- [ ] Augmented assignment operators
- [ ] `const` declarations
- [ ] Basic semantic analysis (symbol table, undefined vars, type checking)

### Phase 0.1.4: Control Flow
- [ ] If/elif/else working
- [ ] While loop working
- [ ] For loop with `range()` working
- [ ] Break/continue working
- [ ] Break/continue validation (only in loops)

### Phase 0.1.5: Functions
- [ ] Function definitions compile
- [ ] Parameters type-checked
- [ ] Default values working
- [ ] Keyword arguments working
- [ ] Return type validation
- [ ] Recursive functions work

---

## Appendix: Useful Commands

```bash
# Run all lexer tests
dotnet test --filter "FullyQualifiedName~Lexer"

# Run all parser tests
dotnet test --filter "FullyQualifiedName~Parser"

# Run all semantic tests
dotnet test --filter "FullyQualifiedName~Semantic"

# Run all codegen tests
dotnet test --filter "FullyQualifiedName~CodeGen"

# Run specific phase tests
dotnet test --filter "Phase010"
dotnet test --filter "Phase011"
dotnet test --filter "Phase012"
dotnet test --filter "Phase013"
dotnet test --filter "Phase014"
dotnet test --filter "Phase015"

# Build and run a test file
dotnet run --project src/Sharpy.Cli -- build test.spy

# Emit tokens for debugging
dotnet run --project src/Sharpy.Cli -- emit tokens test.spy

# Emit AST for debugging
dotnet run --project src/Sharpy.Cli -- emit ast test.spy

# Emit generated C# for debugging
dotnet run --project src/Sharpy.Cli -- emit csharp test.spy
```
