# Sharpy C# Parser Implementation Plan

## Overview

This document provides a step-by-step plan for implementing a complete parser for Sharpy v0.5 in C#. The parser will transform the token stream from the lexer into an Abstract Syntax Tree (AST) suitable for code generation.

**Status**: Lexer is 100% complete with 203 passing tests. Parser.cs exists (~1,810 lines) with partial implementation. This plan focuses on verification, completion, and systematic testing.

**Architecture Reference**: The Rust implementation (`/rust/src/parser/parse.rs`, 3,454 lines) serves as the architectural reference for parsing logic and AST structure.

---

## High-Level Organization

### Phase 1: AST Structure Expansion and Alignment
Ensure the C# AST nodes comprehensively cover all v0.5 language features and align with the proven Rust implementation.

### Phase 2: Parser Core Infrastructure
Verify and enhance the parser's foundational mechanisms (token management, error handling, precedence climbing).

### Phase 3: Statement Parsing Implementation
Implement or verify parsing for all v0.5 statement types in priority order.

### Phase 4: Expression Parsing Implementation
Implement or verify parsing for all v0.5 expression types with proper precedence handling.

### Phase 5: Type Annotation Parsing
Ensure complete type syntax support including generics, nullability, and function types.

### Phase 6: Integration and Testing
Add comprehensive parser tests and validate end-to-end parsing of v0.5 programs.

---

## Phase 1: AST Structure Expansion and Alignment

**Goal**: Ensure C# AST nodes match all v0.5 requirements. Current state shows minimal Node.cs; need to verify Expression.cs, Statement.cs, and Types.cs match feature set.

### 1.1: Audit Existing AST Nodes

**Task**: Compare existing C# AST files against v0.5 language reference features.

**Files to Review**:
- `/src/Sharpy.Compiler/Parser/Ast/Node.cs` (base classes)
- `/src/Sharpy.Compiler/Parser/Ast/Statement.cs` (statement nodes)
- `/src/Sharpy.Compiler/Parser/Ast/Expression.cs` (expression nodes)
- `/src/Sharpy.Compiler/Parser/Ast/Types.cs` (type annotation nodes)

**Verification Checklist** (from language reference):

**Statements** ([v0.5]):
- ✓ Simple statements: expression, assignment, variable declaration, assert, pass, break, continue, return, raise
- ✓ Compound statements: if/elif/else, while, for, try/except/finally
- ✓ Definitions: function (def), class, struct, interface, enum
- ✓ Imports: import, from...import
- ✓ Decorators: @decorator syntax (simple identifiers only in v0.5)
- ✓ Access modifiers: public, private, protected, internal (applied via decorators in v0.5)

**Expressions** ([v0.5]):
- ✓ Literals: int, float, string, bool, None, ellipsis (...)
- ✓ Collections: list, dict, set, tuple
- ✓ String formatting: f-strings (f"Hello {name}")
- ✓ Primary: identifiers, member access (obj.member, obj?.member), index access, slicing
- ✓ Function calls: positional and keyword arguments
- ✓ Operators: arithmetic, comparison, logical, bitwise, null-coalescing (??)
- ✓ Comparison chains: a < b < c
- ✓ Conditional: value if test else other
- ✓ Lambda: lambda x, y: x + y
- ✓ Type operations: as (cast), is (type check)

**Type Annotations** ([v0.5]):
- ✓ Basic types: int, float, str, bool, etc.
- ✓ Generic types: list[T], dict[K, V], tuple[T1, T2, ...]
- ✓ Nullable types: T? syntax
- ✓ Function types: (T1, T2) -> R (if used in v0.5)
- ✓ Qualified types: module.ClassName

**Current Assessment**: Based on the file contents read, the C# AST appears to have good coverage. Statement.cs has 274 lines covering most statement types. Expression.cs appears comprehensive with literals, collections, operators, etc. Types.cs is minimal but covers the basics.

**Action Items**:
1. Verify all enum values match operator inventory (assignment ops, unary ops, binary ops, comparison ops)
2. Check for any missing v0.5 features flagged in language reference
3. Ensure source location tracking (LineStart, ColumnStart, LineEnd, ColumnEnd) is present on all nodes
4. Add XML documentation comments for all node types

### 1.2: Add Missing AST Nodes (if any)

**Task**: Based on audit in 1.1, add any missing node types.

**Potential Gaps to Check**:
- Augmented assignment for all operators (+=, -=, *=, /=, //=, %=, **=, &=, |=, ^=, <<=, >>=)
- Comparison chains as distinct from binary operators
- F-string parts representation (mix of text and expressions)
- Ellipsis literal (...)
- Null-conditional member access (obj?.member)
- Backtick literal names (`` `keyword` ``) - may need identifier variant

**Expected Outcome**: Complete set of C# record types for all v0.5 AST nodes.

### 1.3: Verify Rust-to-C# Mapping

**Task**: Cross-reference Rust AST node definitions with C# implementation.

**Rust Reference**: `/rust/src/ast/node.rs` (889 lines)

**Key Rust Structures to Map**:
- `NodeSource` → C# `Node` base class properties (LineStart/End, ColumnStart/End)
- `UnaryOp_` enum → C# `UnaryOperator` enum
- `BinaryOp_` enum → C# `BinaryOperator` enum
- `CompOp` enum → C# `ComparisonOperator` enum
- `BoolOp_` enum → part of C# `BinaryOperator` (And, Or)
- `ConstantValue` enum → C# literal nodes (IntegerLiteral, FloatLiteral, StringLiteral, BooleanLiteral, NoneLiteral, EllipsisLiteral)

**v0.5-Specific Filtering**: Ignore v1.0+ features in Rust (async/await, with statement, del, match/case, comprehensions, walrus operator, generators).

**Expected Outcome**: Document any structural differences and ensure C# can represent all v0.5 constructs.

---

## Phase 2: Parser Core Infrastructure

**Goal**: Verify and enhance the foundational parser mechanisms.

### 2.1: Token Stream Management

**File**: `/src/Sharpy.Compiler/Parser/Parser.cs`

**Verify/Implement**:
- ✓ `_tokens` list and `_current` position tracking
- ✓ `Peek()` - look at current token without consuming
- ✓ `PeekAhead(int n)` - look ahead n tokens
- ✓ `Advance()` - consume current token and return it
- ✓ `Match(TokenType type)` - check if current token matches type
- ✓ `Match(params TokenType[] types)` - check multiple types
- ✓ `Consume(TokenType type, string errorMessage)` - consume expected token or throw
- ✓ `IsAtEnd()` - check if at end of token stream
- ✓ Automatic NEWLINE and COMMENT skipping (where appropriate)

**Special Handling**:
- INDENT/DEDENT tokens for block structure
- Bracket depth tracking (inherited from lexer context, but parser needs to handle)
- EOF token handling

**Testing Focus**: Create unit tests for token stream utilities.

### 2.2: Error Handling and Recovery

**Verify/Implement**:
- ✓ `ParseException` or equivalent for syntax errors
- ✓ Error messages with line/column information
- ✓ Synchronization points for error recovery (optional in v0.5, can defer)
- ✓ Meaningful error messages for common mistakes

**Error Message Examples**:
```
Expected ':' after if condition at line 5, column 20
Unexpected token 'else' at line 10, column 5
Missing closing bracket ']' at line 8, column 15
Invalid function definition at line 3, column 1
```

**Action**: Review existing error handling in Parser.cs and ensure consistency.

### 2.3: Operator Precedence Table

**Task**: Verify operator precedence table matches v0.5 specification.

**Reference**: `docs/language_reference.md` section "Operator Precedence [v0.5]"

**Precedence Levels** (highest to lowest):
1. Primary: `()`, `[]`, `.`, `?.`, function calls
2. Exponentiation: `**`
3. Unary: `+x`, `-x`, `not x`, `~x`
4. Multiplication: `*`, `/`, `//`, `%`
5. Addition: `+`, `-`
6. Shifts: `<<`, `>>`
7. Bitwise AND: `&`
8. Bitwise XOR: `^`
9. Bitwise OR: `|`
10. Comparisons: `<`, `<=`, `>`, `>=`, `==`, `!=`, `is`, `is not`, `in`, `not in`
11. Logical NOT: `not`
12. Logical AND: `and`
13. Logical OR: `or`
14. Null coalescing: `??`
15. Conditional: `if else`
16. Lambda: `lambda`

**Implementation Strategy**: Precedence climbing (Pratt parsing) for binary/unary operators.

**Action**: Verify `ParseExpression(int precedence)` method implements this correctly.

### 2.4: Indentation-Based Block Parsing

**Task**: Ensure proper handling of INDENT/DEDENT tokens for block structure.

**Pattern**:
```python
if condition:    # Expect ':' then NEWLINE then INDENT
    statement1   # Parse statements until DEDENT
    statement2
                 # DEDENT marks end of block
next_statement
```

**Verify/Implement**:
- ✓ `ParseBlock()` or `ParseSuite()` method
- ✓ Consumes INDENT token at start
- ✓ Parses statements until DEDENT
- ✓ Consumes DEDENT token at end
- ✓ Handles empty blocks (pass statement requirement)

**Edge Cases**:
- Single-line suites: `if x: return 42` (no INDENT/DEDENT)
- Nested blocks
- EOF before DEDENT (error)

---

## Phase 3: Statement Parsing Implementation

**Goal**: Implement parsing for all v0.5 statement types in dependency order.

### 3.1: Simple Statements

**Priority**: High (foundational for all code)

#### 3.1.1: Pass Statement
```python
pass
```
**Implementation**: `ParsePassStatement()` - simplest statement, just consume `pass` keyword.

#### 3.1.2: Break and Continue
```python
break
continue
```
**Implementation**: `ParseBreakStatement()`, `ParseContinueStatement()` - consume keyword only.

#### 3.1.3: Return Statement
```python
return
return value
return x, y  # tuple return
```
**Implementation**: `ParseReturnStatement()` - optional expression after `return`.

#### 3.1.4: Assert Statement
```python
assert condition
assert condition, "error message"
```
**Implementation**: `ParseAssertStatement()` - condition expression, optional message.

#### 3.1.5: Raise Statement
```python
raise
raise Exception("error")
raise Exception("error") from cause
```
**Implementation**: `ParseRaiseStatement()` - optional exception, optional `from` cause.

#### 3.1.6: Expression Statement
```python
print("hello")
x + 5  # legal but useless
```
**Implementation**: Any expression can be a statement. Parse expression, wrap in `ExpressionStatement` node.

#### 3.1.7: Assignment Statement
```python
x = 5
x, y = 1, 2
x += 10
x.attr = value
list[0] = value
```
**Implementation**: `ParseAssignment()` - handle simple, augmented, and multiple assignment.

**Complexity**:
- Distinguish from comparison (`x == 5` vs `x = 5`)
- Handle targets: identifier, member access, index access, tuple unpacking
- Augmented assignment operators: `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=`, `&=`, `|=`, `^=`, `<<=`, `>>=`

#### 3.1.8: Variable Declaration
```python
x: int
x: int = 5
x: list[str] = []
const MAX: int = 100
```
**Implementation**: `ParseVariableDeclaration()` - identifier, colon, type, optional initializer, optional `const`.

**Testing**: Create tests for each simple statement type.

### 3.2: Compound Statements

**Priority**: High (control flow)

#### 3.2.1: If Statement
```python
if condition:
    statement
elif other_condition:
    statement
else:
    statement
```
**Implementation**: `ParseIfStatement()`

**Steps**:
1. Consume `if`, parse test expression
2. Consume `:`, parse block (then body)
3. Handle zero or more `elif` clauses (list of ElifClause)
4. Handle optional `else` clause (else body)

#### 3.2.2: While Statement
```python
while condition:
    statement
```
**Implementation**: `ParseWhileStatement()`

**Steps**:
1. Consume `while`, parse test expression
2. Consume `:`, parse block (body)

#### 3.2.3: For Statement
```python
for item in iterable:
    statement
for x, y in pairs:
    statement
```
**Implementation**: `ParseForStatement()`

**Steps**:
1. Consume `for`, parse target (identifier or tuple)
2. Consume `in`, parse iterator expression
3. Consume `:`, parse block (body)

**Note**: v0.5 does not have async for.

#### 3.2.4: Try Statement
```python
try:
    statement
except Exception:
    statement
except ValueError as e:
    statement
finally:
    statement
```
**Implementation**: `ParseTryStatement()`

**Steps**:
1. Consume `try`, consume `:`, parse block (try body)
2. Parse zero or more `except` handlers (ExceptHandler list)
   - Optional exception type
   - Optional `as` alias
   - Required block
3. Parse optional `finally` clause (finally body)

**Validation**: At least one except or finally required.

### 3.3: Import Statements

**Priority**: High (module system)

#### 3.3.1: Import Statement
```python
import module
import module as alias
import module1, module2
import module.submodule as alias
```
**Implementation**: `ParseImportStatement()`

**Steps**:
1. Consume `import`
2. Parse comma-separated list of module names with optional `as` aliases
3. Create `ImportStatement` with list of `ImportAlias` records

#### 3.3.2: From-Import Statement
```python
from module import name
from module import name1, name2
from module import name as alias
from module import *
```
**Implementation**: `ParseFromImportStatement()`

**Steps**:
1. Consume `from`, parse module name
2. Consume `import`
3. Check for `*` (import all)
4. Otherwise parse comma-separated list of names with optional `as` aliases
5. Create `FromImportStatement`

### 3.4: Definition Statements

**Priority**: Critical (define types and functions)

#### 3.4.1: Function Definition
```python
def function_name(param1: type1, param2: type2 = default) -> return_type:
    """docstring"""
    statement

@decorator
def decorated_func():
    pass
```
**Implementation**: `ParseFunctionDef()`

**Steps**:
1. Parse optional decorators (if `@` present)
2. Consume `def`, parse identifier (function name)
3. Parse parameter list: `(param1: type, param2: type = default, ...)`
   - Each parameter: name, optional type annotation, optional default value
4. Parse optional return type: `-> type`
5. Consume `:`, parse docstring (if first statement is string literal)
6. Parse block (function body)
7. Create `FunctionDef` node

**Parameter Parsing Details**:
- Required parameters first, then optional (with defaults)
- Type annotations optional but recommended
- Default values must be compile-time constants in v0.5

#### 3.4.2: Class Definition
```python
class ClassName:
    """docstring"""
    field: int

    def method(self):
        pass

class Derived(BaseClass):
    pass

class Generic[T](BaseClass):
    pass

@decorator
class DecoratedClass:
    pass
```
**Implementation**: `ParseClassDef()`

**Steps**:
1. Parse optional decorators
2. Consume `class`, parse identifier (class name)
3. Parse optional type parameters: `[T]` or `[T, U]`
4. Parse optional base classes: `(Base1, Base2, ...)`
5. Consume `:`, parse docstring
6. Parse block (class body)
7. Create `ClassDef` node

**Note**: v0.5 classes are reference types (heap-allocated).

#### 3.4.3: Struct Definition
```python
struct Point:
    x: float
    y: float

struct Vector[T](IEquatable[Vector[T]]):
    values: list[T]
```
**Implementation**: `ParseStructDef()`

**Similar to class but**:
- Keyword is `struct`
- Value type semantics (stack-allocated)
- Can only inherit from interfaces, not classes
- Create `StructDef` node

#### 3.4.4: Interface Definition
```python
interface IDrawable:
    def draw(self) -> None:
        ...

interface IGeneric[T]:
    def process(self, item: T) -> T:
        ...

interface IDerived(IBase1, IBase2):
    pass
```
**Implementation**: `ParseInterfaceDef()`

**Steps**:
1. Consume `interface`, parse identifier
2. Parse optional type parameters: `[T]`
3. Parse optional base interfaces: `(IBase1, IBase2, ...)`
4. Consume `:`, parse docstring
5. Parse block (interface body - method signatures)
6. Create `InterfaceDef` node

**Note**: Interface methods can have `...` (ellipsis) as body.

#### 3.4.5: Enum Definition
```python
enum Color:
    RED
    GREEN
    BLUE

enum Status:
    PENDING = 0
    ACTIVE = 1
    DONE = 2
```
**Implementation**: `ParseEnumDef()`

**Steps**:
1. Consume `enum`, parse identifier
2. Consume `:`, parse docstring
3. Parse block of enum members (identifiers with optional `= value`)
4. Create `EnumDef` node

**Note**: v0.5 only supports simple enums (integer-backed). Tagged unions deferred to v1.0+.

### 3.5: Decorator Parsing

**Priority**: Medium (enhances definitions)

```python
@decorator
def function():
    pass

@decorator1
@decorator2
class MyClass:
    pass
```
**Implementation**: `ParseDecorators()`

**Steps**:
1. Check for `@` token
2. Parse identifier (decorator name)
3. Consume NEWLINE
4. Repeat until no more `@` tokens
5. Return list of `Decorator` records

**v0.5 Limitation**: Only simple identifier decorators (no dotted names, no arguments).

**Decorators Apply To**: Functions, classes, structs (not interfaces or enums in v0.5).

### 3.6: Statement Dispatcher

**Implementation**: `ParseStatement()` - main entry point

**Logic**:
```csharp
Token current = Peek();
return current.Type switch {
    TokenType.Pass => ParsePassStatement(),
    TokenType.Break => ParseBreakStatement(),
    TokenType.Continue => ParseContinueStatement(),
    TokenType.Return => ParseReturnStatement(),
    TokenType.Assert => ParseAssertStatement(),
    TokenType.Raise => ParseRaiseStatement(),
    TokenType.If => ParseIfStatement(),
    TokenType.While => ParseWhileStatement(),
    TokenType.For => ParseForStatement(),
    TokenType.Try => ParseTryStatement(),
    TokenType.Import => ParseImportStatement(),
    TokenType.From => ParseFromImportStatement(),
    TokenType.At => ParseDecoratedDefinition(),
    TokenType.Def => ParseFunctionDef(),
    TokenType.Class => ParseClassDef(),
    TokenType.Struct => ParseStructDef(),
    TokenType.Interface => ParseInterfaceDef(),
    TokenType.Enum => ParseEnumDef(),
    TokenType.Const => ParseConstDeclaration(),
    TokenType.Identifier when PeekAhead(1).Type == TokenType.Colon => ParseVariableDeclaration(),
    _ => ParseExpressionOrAssignment()
};
```

**Ambiguity Resolution**:
- `x: int` → variable declaration (identifier followed by colon)
- `x = 5` → assignment (identifier followed by `=`)
- `x += 5` → augmented assignment
- `x + 5` → expression statement
- `x == 5` → expression statement (comparison)

---

## Phase 4: Expression Parsing Implementation

**Goal**: Implement parsing for all v0.5 expression types with correct precedence.

### 4.1: Primary Expressions

**Priority**: Critical (building blocks)

#### 4.1.1: Literals

**Integer Literal**:
```python
42
1_000_000
42L
```
**Implementation**: `ParseIntegerLiteral()` - extract value and suffix from token.

**Float Literal**:
```python
3.14
3.14f
0.5m
```
**Implementation**: `ParseFloatLiteral()` - extract value and suffix.

**String Literal**:
```python
"hello"
'world'
r"C:\path"
"""multi-line"""
```
**Implementation**: `ParseStringLiteral()` - handle raw strings, triple quotes.

**F-String Literal**:
```python
f"Hello {name}"
f"Value: {x + 5}"
```
**Implementation**: `ParseFStringLiteral()` - parse mixed text and expression parts.

**Boolean Literal**:
```python
True
False
```
**Implementation**: `ParseBooleanLiteral()` - create node with boolean value.

**None Literal**:
```python
None
```
**Implementation**: `ParseNoneLiteral()` - create NoneLiteral node.

**Ellipsis Literal**:
```python
...
```
**Implementation**: `ParseEllipsisLiteral()` - create EllipsisLiteral node (used in interfaces, type hints).

#### 4.1.2: Identifier

```python
variable_name
_private
ClassName
```
**Implementation**: `ParseIdentifier()` - create Identifier node with name.

**Backtick Literal Names**:
```python
`class`  # Use keyword as identifier
`from`
```
**Implementation**: Handle TokenType.Backtick, extract enclosed name.

#### 4.1.3: Parenthesized Expression

```python
(expression)
(x + y)
```
**Implementation**:
1. Consume `(`
2. Parse inner expression
3. Consume `)`
4. Wrap in `Parenthesized` node or return inner expression directly (optimization)

### 4.2: Collection Literals

#### 4.2.1: List Literal
```python
[]
[1, 2, 3]
[x, y + 5, func()]
```
**Implementation**: `ParseListLiteral()`

**Steps**:
1. Consume `[`
2. Parse comma-separated expressions (zero or more)
3. Consume `]`
4. Create `ListLiteral` with elements list

#### 4.2.2: Dictionary Literal
```python
{}  # empty dict
{"key": value}
{"a": 1, "b": 2}
{x: y for complex keys}
```
**Implementation**: `ParseDictLiteral()`

**Steps**:
1. Consume `{`
2. If next is `}`, return empty dict
3. Parse key `:` value pairs, comma-separated
4. Consume `}`
5. Create `DictLiteral` with entries list

**Ambiguity**: `{}` is empty dict, not empty set.

#### 4.2.3: Set Literal
```python
{1, 2, 3}
{x, y, z}
```
**Implementation**: `ParseSetLiteral()`

**Disambiguation**: If first element has no `:`, it's a set. Otherwise it's a dict.

#### 4.2.4: Tuple Literal
```python
()  # empty tuple
(1,)  # single-element tuple (trailing comma required)
(1, 2, 3)
1, 2, 3  # implicit tuple
```
**Implementation**: `ParseTupleLiteral()`

**Ambiguity Rules**:
- `()` → empty tuple
- `(x,)` → single-element tuple (trailing comma distinguishes from parenthesized expression)
- `(x, y)` → tuple
- `x, y` in expression context → implicit tuple

**Challenge**: Distinguish tuple from parenthesized expression in certain contexts.

### 4.3: Postfix Expressions

#### 4.3.1: Member Access
```python
obj.member
obj.method()
obj?.nullable_member  # null-conditional
```
**Implementation**: `ParseMemberAccess()`

**Steps**:
1. Parse primary expression (object)
2. While `.` or `?.` token:
   - Consume operator
   - Parse identifier (member name)
   - Create `MemberAccess` node (IsNullConditional flag for `?.`)
   - Update current expression
3. Return final expression

#### 4.3.2: Index Access
```python
list[0]
dict["key"]
matrix[i][j]
```
**Implementation**: `ParseIndexAccess()`

**Steps**:
1. Parse primary expression (object)
2. While `[` token:
   - Consume `[`
   - Check for slice notation (`:` present)
   - If slice: parse slice (start:stop:step)
   - Otherwise: parse index expression
   - Consume `]`
   - Create `IndexAccess` or `SliceAccess` node
   - Update current expression
3. Return final expression

#### 4.3.3: Slicing
```python
list[:]
list[1:]
list[:10]
list[1:10]
list[::2]
list[1:10:2]
```
**Implementation**: `ParseSlice()` (called from index access)

**Steps**:
1. Parse optional start expression
2. Consume `:`
3. Parse optional stop expression
4. If another `:`, parse optional step expression
5. Create `SliceAccess` node

#### 4.3.4: Function Call
```python
func()
func(arg1, arg2)
func(arg1, keyword=value)
obj.method(arg)
```
**Implementation**: `ParseFunctionCall()`

**Steps**:
1. Parse primary expression (function)
2. While `(` token:
   - Consume `(`
   - Parse argument list:
     - Positional arguments (expressions)
     - Keyword arguments (name `=` expression)
   - Consume `)`
   - Create `FunctionCall` node
   - Update current expression
3. Return final expression

**Validation**: Positional arguments must come before keyword arguments.

### 4.4: Unary Operators

```python
+x
-x
not x
~x
```
**Implementation**: `ParseUnaryExpression()`

**Operators**: `+`, `-`, `not`, `~`

**Steps**:
1. Check for unary operator token
2. Consume operator
3. Recursively parse operand (with unary precedence)
4. Create `UnaryOp` node

### 4.5: Binary Operators

**Implementation**: Precedence climbing algorithm in `ParseExpression(int minPrecedence)`

**Operator Categories**:
1. **Arithmetic**: `+`, `-`, `*`, `/`, `//`, `%`, `**`
2. **Bitwise**: `&`, `|`, `^`, `<<`, `>>`
3. **Logical**: `and`, `or`
4. **Null coalescing**: `??`

**Algorithm**:
```csharp
Expression ParseExpression(int minPrecedence = 0) {
    Expression left = ParseUnaryOrPrimary();

    while (IsBinaryOperator(Peek()) && GetPrecedence(Peek()) >= minPrecedence) {
        Token op = Advance();
        int precedence = GetPrecedence(op);
        Expression right = ParseExpression(precedence + 1);
        left = new BinaryOp {
            Operator = MapTokenToBinaryOperator(op),
            Left = left,
            Right = right
        };
    }

    return left;
}
```

### 4.6: Comparison Operators and Chains

```python
x == y
x != y
x < y <= z  # comparison chain
a in [1, 2, 3]
x is None
```
**Implementation**: `ParseComparisonExpression()`

**Operators**: `<`, `<=`, `>`, `>=`, `==`, `!=`, `is`, `is not`, `in`, `not in`

**Challenge**: Comparison chaining (`a < b < c`) is special syntax in Python/Sharpy.

**Steps**:
1. Parse left operand
2. Collect comparison operators and operands: `[left, op1, operand2, op2, operand3, ...]`
3. If single comparison: create `BinaryOp`
4. If chain: create `ComparisonChain` with operands list and operators list

### 4.7: Conditional Expression (Ternary)

```python
value if condition else other_value
x if x > 0 else 0
```
**Implementation**: `ParseConditionalExpression()`

**Precedence**: Very low (above lambda, below or)

**Steps**:
1. Parse left expression (then value)
2. If `if` keyword:
   - Consume `if`
   - Parse test expression
   - Consume `else`
   - Parse else expression
   - Create `ConditionalExpression` node
3. Otherwise return left expression

### 4.8: Lambda Expression

```python
lambda x: x + 1
lambda x, y: x * y
lambda: 42
```
**Implementation**: `ParseLambdaExpression()`

**Precedence**: Lowest

**Steps**:
1. Consume `lambda`
2. Parse parameter list (comma-separated identifiers with optional types)
3. Consume `:`
4. Parse body expression (single expression, not statements)
5. Create `LambdaExpression` node

**Note**: Lambda body is always a single expression in v0.5.

### 4.9: Type Operations

#### 4.9.1: Type Cast
```python
value as TargetType
obj as IDrawable
```
**Implementation**: Parse `as` as binary operator with special handling.

**Steps**:
1. Parse left expression (value)
2. If `as` keyword:
   - Consume `as`
   - Parse type annotation
   - Create `TypeCast` node

#### 4.9.2: Type Check
```python
value is Type
obj is IDrawable
```
**Implementation**: Distinguish `is` for identity vs `is` for type check.

**Disambiguation**:
- `x is None` → identity comparison (BinaryOp with IsOperator)
- `x is MyClass` → type check (TypeCheck node)
- Heuristic: if right side starts with uppercase or is qualified, it's a type check

### 4.10: Expression Dispatcher

**Implementation**: `ParseExpression()` - main entry point using precedence climbing.

**Algorithm Overview**:
1. Check for primary expressions (literals, identifiers, collections)
2. Check for prefix operators (unary)
3. Enter precedence climbing loop for binary/postfix operators
4. Handle special cases (conditional, lambda) at appropriate precedence levels

---

## Phase 5: Type Annotation Parsing

**Goal**: Support all v0.5 type syntax.

### 5.1: Basic Type Names

```python
int
float
str
bool
MyClass
```
**Implementation**: `ParseTypeAnnotation()`

**Steps**:
1. Parse identifier (type name)
2. Create `TypeAnnotation` with name

### 5.2: Qualified Type Names

```python
module.ClassName
System.Collections.Generic.List
```
**Implementation**: Parse dotted identifier chain.

**Steps**:
1. Parse identifier
2. While `.` token:
   - Consume `.`
   - Parse identifier
   - Append to qualified name
3. Create `TypeAnnotation` with full qualified name

### 5.3: Generic Types

```python
list[int]
dict[str, int]
Optional[MyClass]
tuple[int, str, float]
```
**Implementation**: `ParseGenericType()`

**Steps**:
1. Parse base type name
2. If `[` token:
   - Consume `[`
   - Parse comma-separated type arguments (recursively)
   - Consume `]`
   - Store in TypeArguments list
3. Create `TypeAnnotation` with type arguments

### 5.4: Nullable Types

```python
int?
MyClass?
list[str]?
```
**Implementation**: Handle `?` suffix.

**Steps**:
1. Parse base type annotation
2. If `?` token:
   - Consume `?`
   - Set IsNullable flag
3. Return `TypeAnnotation`

**Note**: Nullable is post-fix, applied after generic arguments: `list[int]?`, not `list?[int]`.

### 5.5: Function Types (if used in v0.5)

```python
(int, str) -> bool
() -> None
```
**Implementation**: `ParseFunctionType()` (if needed)

**Steps**:
1. Consume `(`
2. Parse comma-separated parameter types
3. Consume `)`
4. Consume `->`
5. Parse return type
6. Create `FunctionType` record

**Usage**: Type annotations for callable parameters, variables holding functions.

### 5.6: Tuple Types

```python
tuple[int, str]
tuple[int, int, int]
```
**Implementation**: Handle as generic type with special semantics.

**Note**: `tuple[T, U]` is heterogeneous (fixed types), vs `tuple[T, ...]` for homogeneous (v1.0+).

---

## Phase 6: Integration and Testing

**Goal**: Validate parser with comprehensive tests and end-to-end parsing.

### 6.1: Parser Unit Tests

**File**: `/src/Sharpy.Compiler.Tests/Parser/ParserTests.cs`

**Test Structure**:
```csharp
[Fact]
public void ParseSimpleAssignment() {
    var tokens = Lex("x = 5");
    var parser = new Parser(tokens);
    var module = parser.ParseModule();

    var assignment = module.Body[0].Should().BeOfType<Assignment>();
    assignment.Target.Should().BeOfType<Identifier>().Which.Name.Should().Be("x");
    assignment.Value.Should().BeOfType<IntegerLiteral>().Which.Value.Should().Be("5");
}
```

**Test Categories**:

#### 6.1.1: Statement Tests (~50 tests)
- Simple statements: pass, break, continue, return, assert, raise
- Assignment: simple, augmented, multiple targets
- Variable declaration: with/without type, with/without initializer, const
- If statement: simple, with elif, with else, nested
- While loop: simple, nested
- For loop: simple, with tuple unpacking
- Try-except-finally: various combinations
- Import statements: simple, with alias, multiple, from-import, import all
- Function definition: simple, with params, with types, with defaults, with decorators
- Class definition: simple, with bases, with type params, with decorators
- Struct definition: simple, with interfaces
- Interface definition: simple, with inheritance, with generics
- Enum definition: simple, with explicit values

#### 6.1.2: Expression Tests (~80 tests)
- Literals: int, float, string, f-string, bool, None, ellipsis
- Collections: list, dict, set, tuple (empty, single, multiple)
- Identifiers: simple, backtick names
- Member access: simple, chained, null-conditional
- Index access: simple, chained
- Slicing: all forms (start:stop:step)
- Function calls: no args, positional args, keyword args, chained
- Unary operators: +, -, not, ~
- Binary operators: arithmetic, bitwise, logical
- Comparison: simple, chains
- Conditional expression: simple, nested
- Lambda: no params, with params, with types
- Type operations: cast, type check
- Null coalescing: simple, chained

#### 6.1.3: Type Annotation Tests (~30 tests)
- Basic types: int, str, etc.
- Qualified types: module.Class
- Generic types: list[T], dict[K, V], nested generics
- Nullable types: T?, generic nullable
- Function types: (T) -> R
- Tuple types: tuple[T, U]

#### 6.1.4: Precedence and Disambiguation Tests (~20 tests)
- Operator precedence: verify correct tree structure
- Tuple vs parenthesized expression
- Dict vs set literal
- Assignment vs comparison
- Type annotation vs generic call
- Comparison chains

#### 6.1.5: Error Handling Tests (~30 tests)
- Missing colons
- Missing closing brackets
- Unexpected tokens
- Invalid indentation
- Empty blocks without pass
- Invalid assignment targets

**Total**: ~210 parser tests

### 6.2: Integration Tests

**File**: `/src/Sharpy.Compiler.Tests/Integration/EndToEndParsingTests.cs`

**Test Approach**: Parse complete v0.5 programs and verify AST structure.

**Sample Programs**:

#### 6.2.1: Simple Program
```python
# Simple arithmetic program
def add(x: int, y: int) -> int:
    return x + y

result: int = add(5, 10)
print(result)
```

#### 6.2.2: Class with Methods
```python
class Counter:
    """A simple counter class."""

    count: int

    def __init__(self):
        self.count = 0

    def increment(self) -> None:
        self.count += 1

    def get_value(self) -> int:
        return self.count
```

#### 6.2.3: Interface and Struct
```python
interface IShape:
    def area(self) -> float:
        ...

struct Rectangle(IShape):
    width: float
    height: float

    def area(self) -> float:
        return self.width * self.height
```

#### 6.2.4: Enum and Control Flow
```python
enum Status:
    PENDING
    ACTIVE
    DONE

def process(status: Status) -> str:
    if status is Status.PENDING:
        return "waiting"
    elif status is Status.ACTIVE:
        return "processing"
    else:
        return "completed"
```

#### 6.2.5: Exception Handling
```python
def safe_divide(a: float, b: float) -> float:
    try:
        return a / b
    except ZeroDivisionError as e:
        print(f"Error: {e}")
        return 0.0
    finally:
        print("Division attempted")
```

#### 6.2.6: Generics and Collections
```python
class Container[T]:
    items: list[T]

    def __init__(self):
        self.items = []

    def add(self, item: T) -> None:
        self.items.append(item)

    def get_all(self) -> list[T]:
        return self.items
```

### 6.3: AST Verification

**Implementation**: Helper methods to inspect AST structure in tests.

```csharp
// Extension methods for test assertions
public static class AstAssertions {
    public static T ShouldBeStatement<T>(this Node node) where T : Statement;
    public static T ShouldBeExpression<T>(this Node node) where T : Expression;

    public static void ShouldHaveLocation(this Node node, int lineStart, int colStart);
    public static void ShouldContainStatements(this List<Statement> body, int count);
}
```

### 6.4: Error Message Quality

**Review**: Ensure error messages are clear and helpful.

**Examples**:
```
Good: "Expected ':' after if condition at line 5, column 20"
Bad:  "Syntax error"

Good: "Function definition requires at least one statement or 'pass' at line 10"
Bad:  "Parse error in function"

Good: "Positional argument cannot follow keyword argument at line 15, column 25"
Bad:  "Invalid arguments"
```

### 6.5: Performance Testing

**Goal**: Ensure parser can handle moderately large files efficiently.

**Test Cases**:
- Parse 1,000-line Sharpy file
- Parse deeply nested expressions (50+ levels)
- Parse class with 100+ methods

**Baseline**: Should complete in < 100ms for 1,000 lines.

### 6.6: Documentation

**Update**:
- `docs/transpiler-architecture.md` - Add parser architecture details
- XML comments on all public parser methods
- README examples showing parser usage

---

## Implementation Checklist

### Phase 1: AST Structure
- [ ] 1.1: Audit existing AST nodes against v0.5 spec
- [ ] 1.2: Add any missing node types
- [ ] 1.3: Verify Rust-to-C# mapping and document differences

### Phase 2: Parser Core
- [ ] 2.1: Verify token stream management utilities
- [ ] 2.2: Review and enhance error handling
- [ ] 2.3: Verify operator precedence table matches v0.5
- [ ] 2.4: Verify indentation-based block parsing

### Phase 3: Statement Parsing
- [ ] 3.1.1-3.1.8: Implement/verify all simple statements
- [ ] 3.2.1-3.2.4: Implement/verify all compound statements
- [ ] 3.3.1-3.3.2: Implement/verify import statements
- [ ] 3.4.1-3.4.5: Implement/verify all definition statements
- [ ] 3.5: Implement/verify decorator parsing
- [ ] 3.6: Implement/verify statement dispatcher

### Phase 4: Expression Parsing
- [ ] 4.1: Implement/verify primary expressions and literals
- [ ] 4.2: Implement/verify collection literals
- [ ] 4.3: Implement/verify postfix expressions
- [ ] 4.4: Implement/verify unary operators
- [ ] 4.5: Implement/verify binary operators with precedence
- [ ] 4.6: Implement/verify comparison operators and chains
- [ ] 4.7: Implement/verify conditional expressions
- [ ] 4.8: Implement/verify lambda expressions
- [ ] 4.9: Implement/verify type operations
- [ ] 4.10: Verify expression dispatcher and precedence climbing

### Phase 5: Type Annotations
- [ ] 5.1: Verify basic type names
- [ ] 5.2: Verify qualified type names
- [ ] 5.3: Verify generic types
- [ ] 5.4: Verify nullable types
- [ ] 5.5: Verify function types (if used)
- [ ] 5.6: Verify tuple types

### Phase 6: Testing
- [ ] 6.1.1: Add 50 statement tests
- [ ] 6.1.2: Add 80 expression tests
- [ ] 6.1.3: Add 30 type annotation tests
- [ ] 6.1.4: Add 20 precedence/disambiguation tests
- [ ] 6.1.5: Add 30 error handling tests
- [ ] 6.2: Add 6+ integration tests with complete programs
- [ ] 6.3: Implement AST verification helpers
- [ ] 6.4: Review and improve error messages
- [ ] 6.5: Add performance tests
- [ ] 6.6: Update documentation

---

## Success Criteria

**Parser v0.5 is complete when**:
1. ✅ All v0.5 statement types can be parsed
2. ✅ All v0.5 expression types can be parsed with correct precedence
3. ✅ All v0.5 type annotations can be parsed
4. ✅ 210+ parser unit tests pass
5. ✅ 6+ integration tests with full programs pass
6. ✅ Error messages are clear and include line/column information
7. ✅ Parser handles edge cases (empty collections, single-element tuples, etc.)
8. ✅ AST nodes include complete source location information
9. ✅ Parser performance is acceptable (< 100ms for 1,000 lines)
10. ✅ Documentation is complete and accurate

---

## Notes and Considerations

### Deferred to v1.0+
- Properties (get/set)
- Type aliases (`type` keyword)
- Walrus operator (`:=`)
- List/dict/set comprehensions
- Match/case statements
- With statements (context managers)
- Del statement
- Advanced integer/float literals (handled in lexer, but not emphasized)

### Deferred to v1.5+
- Async/await
- Yield/generators
- Defer statement

### C# Implementation Advantages
- Records for immutable AST nodes
- Pattern matching for node dispatch
- Nullable reference types for optional AST properties
- LINQ for AST traversal/transformation

### Rust Reference Value
- `/rust/src/parser/parse.rs` - 3,454 lines of parsing logic
- `/rust/src/ast/node.rs` - 889 lines of node definitions
- Provides architectural guidance and edge case handling
- Use for cross-reference when implementing complex features

### Testing Philosophy
- Test each feature in isolation first
- Then test combinations and interactions
- Include both positive (valid) and negative (error) tests
- Prioritize clarity of test names and structure
- Use FluentAssertions for readable assertions

---

## Appendix: Quick Reference

### v0.5 Keywords (27 total)
and, as, assert, auto, break, class, const, continue, def, elif, else, enum, except, False, finally, for, from, if, import, in, interface, is, lambda, None, not, or, pass, raise, return, struct, True, try, while

### v0.5 Operators
**Arithmetic**: `+`, `-`, `*`, `/`, `//`, `%`, `**`
**Comparison**: `<`, `<=`, `>`, `>=`, `==`, `!=`
**Logical**: `and`, `or`, `not`
**Bitwise**: `&`, `|`, `^`, `~`, `<<`, `>>`
**Identity/Membership**: `is`, `is not`, `in`, `not in`
**Special**: `?.` (null-conditional), `??` (null-coalescing), `as` (cast)
**Assignment**: `=`, `+=`, `-=`, `*=`, `/=`, `//=`, `%=`, `**=`, `&=`, `|=`, `^=`, `<<=`, `>>=`

### Statement Types (17 categories)
Simple: pass, break, continue, return, assert, raise, expression, assignment, variable declaration
Compound: if/elif/else, while, for, try/except/finally
Definitions: def, class, struct, interface, enum
Imports: import, from...import

### Expression Types (15 categories)
Literals: int, float, string, f-string, bool, None, ellipsis
Collections: list, dict, set, tuple
Operations: unary, binary, comparison, logical, bitwise, null-coalescing
Advanced: conditional, lambda, type cast, type check, member access, index, slice, function call

### Type Annotations
Basic: int, float, str, bool, etc.
Generic: list[T], dict[K, V]
Nullable: T?
Qualified: module.Class
Function: (T1, T2) -> R
Tuple: tuple[T1, T2]
