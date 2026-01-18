# Walkthrough: Parser.Statements.cs

**Source File**: `src/Sharpy.Compiler/Parser/Parser.Statements.cs`

---

## Overview

`Parser.Statements.cs` is a partial class file containing the statement parsing logic for the Sharpy compiler's recursive descent parser. While expressions represent values and computations, statements represent actions and control flow in a program. This file handles parsing of:

- **Control flow**: `if/elif/else`, `while`, `for`, `try/except/finally`
- **Loop control**: `break`, `continue`
- **Function control**: `return`, `raise`
- **Module organization**: `import`, `from...import`
- **Utilities**: `assert`, `pass`
- **List/dict/set comprehensions** (via `ParseComprehensionClauses`)

**Role in Pipeline**: Receives token stream from Lexer → Constructs statement AST nodes → Passes to semantic analysis

This file is one of several partial class files that together form the complete `Parser` class. Each partial file focuses on a specific aspect of parsing (statements, expressions, definitions, types, etc.).

---

## Class/Type Structure

This file defines methods on the partial `Parser` class (namespace `Sharpy.Compiler.Parser`). The main parser state and core methods are in `Parser.cs`, while this file extends it with statement-specific parsing logic.

**Partial Class Pattern**: The `Parser` class is split across multiple files:
- `Parser.cs` - Core parser infrastructure, main parsing loop
- `Parser.Statements.cs` - **This file** - Control flow and statement parsing
- `Parser.Expressions.cs` - Expression parsing with operator precedence
- `Parser.Definitions.cs` - Function/class/decorator definitions
- `Parser.Types.cs` - Type annotation parsing
- `Parser.Primaries.cs` - Primary expressions (literals, identifiers, etc.)

All methods in this file return AST node types defined in `Sharpy.Compiler.Parser.Ast.Statement`.

---

## Key Functions/Methods

### Control Flow Statements

#### `ParseIfStatement()` (lines 13-77)

Parses Python-style if/elif/else chains with indentation-based blocks.

**Syntax Pattern**:
```python
if condition:
    body
elif condition2:
    body2
else:
    body3
```

**Key Implementation Details**:
- Expects `if`, expression, `:`, newline, `INDENT`, block, `DEDENT`
- Handles multiple `elif` clauses in a loop (lines 30-53)
- Optional `else` clause (lines 56-64)
- Each elif clause is stored as an `ElifClause` with its own test expression and body
- Tracks source location (line/column) for error reporting and debugging

**AST Output**: `IfStatement` with:
- `Test`: condition expression
- `ThenBody`: list of statements for if branch
- `ElifClauses`: list of `ElifClause` objects
- `ElseBody`: list of statements for else branch (empty if no else)

---

#### `ParseWhileStatement()` (lines 79-114)

Parses while loops with optional else clause (Python feature: runs if loop completes without `break`).

**Syntax Pattern**:
```python
while condition:
    body
else:
    # Runs if loop completes normally (no break)
    else_body
```

**Key Implementation Details**:
- Similar structure to `if` statement: expects `:`, newline, indent/dedent
- Optional `else` clause for "no-break" scenarios (lines 94-102)
- The else clause is a Python-specific feature that beginners often find surprising

**AST Output**: `WhileStatement` with `Test`, `Body`, `ElseBody`

---

#### `ParseForStatement()` (lines 116-158)

Parses for-in loops with tuple unpacking support and optional else clause.

**Syntax Pattern**:
```python
for x in iterable:
    body
for x, y in pairs:  # Tuple unpacking
    body
else:
    # Runs if loop completes normally (no break)
    else_body
```

**Key Implementation Details**:
- Uses `ParseForTarget()` to handle both simple identifiers and tuple unpacking
- Important: Must **not** consume `in` as a comparison operator (see comment line 124)
- Supports optional else clause like `while` (lines 137-145)
- The target can be a single identifier or a tuple of identifiers

**AST Output**: `ForStatement` with `Target`, `Iterator`, `Body`, `ElseBody`

**Design Decision**: The parser carefully separates target parsing from general expression parsing to avoid ambiguity with the `in` operator.

---

#### `ParseForTarget()` (lines 160-196)

Helper method that parses for-loop targets, which can be identifiers or tuples but not full expressions.

**Valid Targets**:
```python
for x in ...           # Simple identifier
for x, y in ...        # Tuple
for x, y, z in ...     # Multi-element tuple
for x, in ...          # Single-element tuple with trailing comma
```

**Key Implementation Details**:
- Starts with `ParsePrimary()` to get first element
- Checks for comma to detect tuple unpacking (lines 173-193)
- Stops parsing when it encounters `in` keyword (line 180-181)
- Returns either a single `Expression` or a `TupleLiteral` expression

**Why Not Full Expression Parsing?**
To prevent consuming `in` as a binary operator. For example, `for x in y in z` should parse as `for (x) in (y in z)`, not `for (x in y) in (z)`.

---

#### `ParseComprehensionClauses()` (lines 202-254)

Parses the clauses used in list/dict/set comprehensions and generator expressions.

**Syntax Pattern**:
```python
[x for x in range(10) if x % 2 == 0 for y in range(x)]
     ^--- for clause --^ ^- if clause -^ ^--- for clause ---^
```

**Key Implementation Details**:
- Returns list of `ComprehensionClause` (abstract base class)
- Two clause types: `ForClause` and `IfClause`
- Loops until it encounters neither `for` nor `if` (lines 206-250)
- Uses `ParseLogicalOr()` instead of `ParseExpression()` to avoid consuming too much (lines 218, 236)
- Currently only supports single variable targets (no tuple unpacking in comprehensions - see comment line 200)

**Design Note**: Uses lower precedence parsing (`ParseLogicalOr`) to avoid accidentally consuming the next `for` or `if` keyword as part of the expression.

**Upstream Connection**: Called from expression parsing when encountering `[`, `{`, or `(` followed by expression and `for`.

---

#### `ParseTryStatement()` (lines 256-345)

Parses try/except/else/finally blocks with multiple exception handlers.

**Syntax Pattern**:
```python
try:
    risky_operation()
except ValueError as e:
    handle_value_error()
except (TypeError, KeyError):
    handle_type_or_key_error()
except:  # Bare except (catches all)
    handle_any_error()
else:
    # Runs if no exception raised
    success_path()
finally:
    # Always runs
    cleanup()
```

**Key Implementation Details**:
- Main try block (lines 261-266)
- Multiple except handlers in loop (lines 270-309)
- Each handler can have:
  - Type annotation for specific exception (line 282)
  - Optional `as name` binding (lines 284-288)
  - Or bare `except:` to catch all (line 280)
- Optional `else` clause runs if no exception occurred (lines 313-321)
- Optional `finally` clause always runs (lines 323-332)

**AST Output**: `TryStatement` with:
- `Body`: try block statements
- `Handlers`: list of `ExceptHandler` objects
- `ElseBody`: statements to run on success
- `FinallyBody`: cleanup statements

**Python Semantics**: The `else` clause is often misunderstood - it runs only if the try block completes without raising an exception.

---

### Jump Statements

#### `ParseReturnStatement()` (lines 347-368)

Parses return statements with optional value.

**Syntax Pattern**:
```python
return           # Return None
return value     # Return value
return x, y      # Return tuple
```

**Key Implementation Details**:
- Value is optional - checks for newline/dedent/EOF (line 355)
- If value exists, parses full expression (line 356)
- Uses `ExpectStatementEnd()` to enforce proper termination (line 358)

**AST Output**: `ReturnStatement` with optional `Value` expression

---

#### `ParseRaiseStatement()` (lines 370-403)

Parses raise statements for throwing exceptions.

**Syntax Pattern**:
```python
raise                           # Re-raise current exception
raise ValueError("bad input")   # Raise new exception
raise ValueError("msg") from e  # Exception chaining
```

**Key Implementation Details**:
- Exception expression is optional (bare `raise` re-raises current exception)
- Supports `from` clause for exception chaining (lines 385-389)
- The `from` clause sets the `__cause__` attribute in Python

**AST Output**: `RaiseStatement` with optional `Exception` and `Cause` expressions

**Python Feature**: Exception chaining (`raise ... from ...`) helps preserve stack traces when translating exceptions.

---

#### `ParseBreakStatement()` & `ParseContinueStatement()` (lines 450-482)

Parse loop control flow statements.

**Key Implementation Details**:
- Simplest parsers in the file
- Both just expect the keyword and statement end
- No additional data needed beyond source location
- Semantic analysis later validates they're inside loops

**AST Output**: `BreakStatement` or `ContinueStatement` (no additional fields)

---

### Utility Statements

#### `ParseAssertStatement()` (lines 405-431)

Parses assertions with optional failure message.

**Syntax Pattern**:
```python
assert condition
assert x > 0, "x must be positive"
```

**Key Implementation Details**:
- Test expression is required (line 411)
- Optional message after comma (lines 413-418)
- Typically compiled to conditional that raises `AssertionError`

**AST Output**: `AssertStatement` with `Test` expression and optional `Message` expression

---

#### `ParsePassStatement()` (lines 433-448)

Parses the `pass` keyword (no-op statement).

**Usage Context**:
```python
if condition:
    pass  # TODO: implement later

def empty_function():
    pass  # Placeholder
```

**Key Implementation Details**:
- Simplest statement - just expects `pass` keyword and statement end
- Used as placeholder in blocks that would otherwise be empty
- Python requires at least one statement in every block

**AST Output**: `PassStatement` (no additional fields)

---

### Import Statements

#### `ParseImportStatement()` (lines 484-535)

Parses module import statements with optional aliasing.

**Syntax Pattern**:
```python
import os
import os.path
import json as j
import sys, os, pathlib  # Multiple imports
```

**Key Implementation Details**:
- Parses comma-separated list of imports (lines 493-523)
- Each import can have dotted name (via `ParseDottedName()`)
- Optional `as` alias for each import (lines 500-504)
- Loop continues until no more commas (lines 519-523)

**AST Output**: `ImportStatement` with list of `ImportAlias` objects, each containing:
- `Name`: dotted module name (e.g., "os.path")
- `AsName`: optional alias

**Downstream**: Module discovery system resolves these imports to .spy or .dll files.

---

#### `ParseFromImportStatement()` (lines 537-601)

Parses from-import statements, including star imports and relative imports.

**Syntax Pattern**:
```python
from os import path
from os.path import join, exists
from json import load as json_load
from . import helpers          # Relative import (current package)
from ..utils import helper     # Relative import (parent package)
from module import *           # Star import
```

**Key Implementation Details**:
- Module name parsed via `ParseModuleName()` which handles relative imports (lines 543)
- Star import handling: sets `ImportAll = true` (lines 549-553)
- Otherwise parses comma-separated list of names (lines 556-587)
- Each name can have `as` alias (lines 563-567)

**AST Output**: `FromImportStatement` with:
- `Module`: module name (potentially with leading dots for relative imports)
- `Names`: list of `ImportAlias` objects (empty for star imports)
- `ImportAll`: boolean flag for star imports

**Python Feature**: Star imports (`from module import *`) import all public names from a module.

---

### Helper Methods

#### `ParseDottedName()` (lines 603-614)

Parses dotted identifiers like `os.path.join`.

**Implementation**:
- Starts with identifier, then loops consuming dots and identifiers
- Joins parts with "." separator
- Used for absolute import paths

---

#### `ParseModuleName()` (lines 616-643)

Parses module names including relative imports with leading dots.

**Syntax Examples**:
```python
from .helpers import foo          # One dot: current package
from ..utils import bar           # Two dots: parent package
from ...config import settings    # Three dots: grandparent package
from . import module              # Just dot: current package
```

**Key Implementation Details**:
- Collects leading dots in `StringBuilder` (lines 619-624)
- After dots, optionally parses dotted name (lines 629-633)
- Valid to have just dots (e.g., `from . import x`) - line 635-639
- Throws error if neither dots nor identifier found (line 642)

**Design Decision**: Leading dots are preserved in the string to distinguish relative from absolute imports during module resolution.

---

#### `ParseBlock()` (lines 645-660)

Parses an indented block of statements.

**Usage Context**: Called after `:` in if/while/for/try/def/class

**Key Implementation Details**:
- Expects to be positioned inside `INDENT...DEDENT` tokens
- Skips newlines before and after each statement (lines 651, 656)
- Continues until `DEDENT` or EOF (line 649)
- Returns list of statements (possibly empty for `pass`)

**Upstream Connection**: Called by all compound statements (if/while/for/try/def/class)

---

#### `ParseParameters()` (lines 662-733)

Parses function parameter lists including variadic parameters (*args).

**Syntax Pattern**:
```python
def foo():                           # No parameters
def bar(x):                          # Single parameter
def baz(x: int, y: str = "hi"):     # Typed with default
def qux(x, y, *args):               # Variadic (star parameter)
def trailing(a, b,):                # Trailing comma allowed
```

**Key Implementation Details**:
- Returns empty list if immediately at `)` (lines 667-668)
- Detects variadic parameters with star prefix (lines 676-684)
- Only one variadic parameter allowed (line 679-680)
- Parameter structure (lines 686-717):
  - Name (required)
  - Optional type annotation after `:` (lines 690-694)
  - Optional default value after `=` (lines 696-702)
  - Variadic flag
- Variadic parameters validated:
  - Cannot have default value (lines 698-699)
  - Must be last parameter (lines 721-722)
- Trailing comma support (lines 724-726)

**AST Output**: List of `Parameter` objects with `Name`, `Type`, `DefaultValue`, `IsVariadic`

**Python Semantics**:
- Parameters with defaults must come after those without
- Variadic parameter (`*args`) collects remaining positional arguments into a tuple

---

## Dependencies

### Internal Dependencies
This file relies heavily on:

1. **Base Parser infrastructure** (`Parser.cs`):
   - `Current`, `Peek()`, `Advance()` - Token stream navigation
   - `Expect()`, `ExpectIdentifier()` - Token consumption with error handling
   - `ExpectNewline()`, `ExpectStatementEnd()`, `SkipNewlines()` - Whitespace handling
   - `IsAtEnd` - EOF detection

2. **Expression parsing** (`Parser.Expressions.cs`):
   - `ParseExpression()` - Full expression parsing
   - `ParseLogicalOr()` - Lower precedence expression parsing (for comprehensions)
   - `ParsePrimary()` - Primary expressions (identifiers, literals)

3. **Type parsing** (`Parser.Types.cs`):
   - `ParseTypeAnnotation()` - For parameter types and exception types

4. **Lexer** (`Sharpy.Compiler.Lexer`):
   - `Token`, `TokenType` - Token representation
   - Expects properly tokenized input with INDENT/DEDENT tokens for Python-style blocks

5. **AST nodes** (`Sharpy.Compiler.Parser.Ast`):
   - `Statement` - Base class and all specific statement types
   - `Expression` - For test conditions, values, etc.
   - `ComprehensionClause`, `ElifClause`, `ExceptHandler`, `Parameter`, `ImportAlias` - Supporting structures

### Upstream Component
**Lexer** - Must provide:
- Proper INDENT/DEDENT tokens for block structure
- Keyword tokens (if, while, for, try, etc.)
- Accurate line/column information for error reporting

### Downstream Components
**Semantic Analysis** - Receives AST and must:
- Validate break/continue appear inside loops
- Validate return appears inside functions
- Resolve import statements to actual modules
- Type-check expressions in statements

---

## Patterns and Design Decisions

### 1. **Recursive Descent Parsing**
Each statement type has a dedicated method that:
- Expects specific token patterns
- Recursively calls other parsing methods for sub-components
- Returns strongly-typed AST nodes
- Tracks source locations for error reporting

### 2. **Indentation-Based Blocks**
Python's significant whitespace is handled via INDENT/DEDENT tokens:
- Lexer produces these tokens based on indentation changes
- Parser expects them around blocks: `Expect(TokenType.Indent)` ... `Expect(TokenType.Dedent)`
- Makes block structure explicit in token stream

### 3. **Source Location Tracking**
Every AST node includes:
- `LineStart`, `ColumnStart` - Where construct begins
- `LineEnd`, `ColumnEnd` - Where construct ends
- Enables precise error messages and IDE features (go-to-definition, hover, etc.)

### 4. **Partial Class Organization**
Large parser split into focused files:
- Easier to navigate and maintain
- Clear separation of concerns
- Each partial file groups related functionality

### 5. **Predictive Parsing with Lookahead**
Uses `Current.Type` to decide which parsing method to call:
```csharp
if (Current.Type == TokenType.If)
    return ParseIfStatement();
else if (Current.Type == TokenType.While)
    return ParseWhileStatement();
```
This is characteristic of LL(1) recursive descent parsers.

### 6. **Error Recovery Through Exceptions**
When encountering unexpected tokens, parser throws `ParserError`:
- Includes line/column information
- Halts parsing (no error recovery currently)
- Clear error messages for users

### 7. **Python Compatibility**
Sharpy aims for Python syntax compatibility:
- If/elif/else chains
- While/for with else clauses
- Try/except/else/finally
- Relative imports with leading dots
- Variadic parameters (*args)

---

## Debugging Tips

### Common Issues and Solutions

#### 1. **Unexpected INDENT/DEDENT Errors**
**Symptom**: Parser fails expecting INDENT or DEDENT

**Debug Approach**:
- Check lexer output - are INDENT/DEDENT tokens being generated correctly?
- Look for mixed tabs/spaces in source file
- Verify indentation is consistent throughout block
- Use `Peek()` to inspect token stream around error location

#### 2. **"Expected X but found Y" Errors**
**Symptom**: `Expect()` throws because wrong token type

**Debug Approach**:
- Add logging before `Expect()` calls to see actual vs expected tokens
- Check if previous parsing consumed too many tokens
- Verify lexer is producing correct token types for keywords

#### 3. **Loop Else Clauses Not Working**
**Symptom**: Else clause after while/for not parsing correctly

**Debug Approach**:
- Verify `DEDENT` token appears before `else` keyword
- Check that else block is properly indented
- Trace through `ParseWhileStatement()` or `ParseForStatement()` to see where it stops

#### 4. **Import Statement Parsing Issues**
**Symptom**: Relative imports or complex imports fail

**Debug Approach**:
- Trace through `ParseModuleName()` for relative imports
- Check if dots are being consumed correctly
- For star imports, verify `ImportAll` flag is set
- Log the final import alias list to see what was parsed

#### 5. **For Target Ambiguity**
**Symptom**: For loop target parsed incorrectly

**Debug Approach**:
- Step through `ParseForTarget()`
- Check if `in` keyword is being consumed as part of target expression
- Verify tuple detection logic (comma handling)

### Useful Debugging Techniques

1. **Token Stream Inspection**:
   ```csharp
   // Add before problematic parsing
   Console.WriteLine($"Current: {Current.Type} '{Current.Value}' at {Current.Line}:{Current.Column}");
   Console.WriteLine($"Next: {Peek(1).Type} '{Peek(1).Value}'");
   ```

2. **AST Dumping**:
   - Use `AstDumper` class (see `Parser/AstDumper.cs`) to print parsed AST
   - Helps verify structure matches expectations

3. **Breakpoint Locations**:
   - Start of each `Parse*Statement()` method
   - Inside `Expect()` calls that are failing
   - In comprehension clause loops to see iteration

4. **Compare with Python**:
   - Parse same code with Python's `ast.parse()`
   - Compare AST structure to Sharpy's
   - Identify divergences

---

## Contribution Guidelines

### When to Modify This File

1. **Adding New Statement Types**:
   - Add new `Parse*Statement()` method
   - Add corresponding AST node in `Ast/Statement.cs`
   - Update main parsing dispatch in `Parser.cs`
   - Add test cases

2. **Extending Existing Statements**:
   - Example: Adding `async for` support
   - Modify relevant `Parse*Statement()` method
   - Update AST node definition if needed
   - Ensure backwards compatibility

3. **Improving Error Messages**:
   - Add more context to `ParserError` throws
   - Check for common mistakes and provide hints

4. **Supporting New Python Features**:
   - Example: Structural pattern matching (match/case)
   - Add new parsing methods
   - Define new AST nodes
   - Update documentation

### Code Style Guidelines

1. **Naming Conventions**:
   - Methods: `Parse*` prefix for all parsing methods
   - Parameters: camelCase
   - Local variables: camelCase
   - AST properties: PascalCase

2. **Error Handling**:
   - Use `Expect()` for required tokens
   - Use `ExpectIdentifier()` for identifier tokens
   - Throw `ParserError` with clear messages
   - Always include line/column information

3. **Source Location**:
   - Capture `startLine`/`startColumn` at method entry
   - Set `LineEnd`/`ColumnEnd` before returning AST node
   - Use `Peek(-1)` to get last consumed token for end position

4. **Comments**:
   - Add comments for non-obvious logic
   - Document Python features that may be unfamiliar (e.g., loop else clauses)
   - Include syntax examples in comments

### Testing Checklist

When modifying this file:

- [ ] Add unit tests for new statement types
- [ ] Test edge cases (empty blocks, trailing commas, etc.)
- [ ] Verify error messages are helpful
- [ ] Check source location tracking is accurate
- [ ] Test integration with expression/type parsing
- [ ] Verify AST nodes serialize/deserialize correctly
- [ ] Test with real-world Python code samples

### Common Modification Patterns

#### Adding a New Simple Statement
```csharp
private MyNewStatement ParseMyNewStatement()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;

    Expect(TokenType.MyKeyword);
    var someExpr = ParseExpression();
    ExpectStatementEnd();

    return new MyNewStatement
    {
        SomeField = someExpr,
        LineStart = startLine,
        ColumnStart = startColumn,
        LineEnd = Current.Line,
        ColumnEnd = Current.Column
    };
}
```

#### Adding a New Compound Statement
```csharp
private MyCompoundStatement ParseMyCompoundStatement()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;

    Expect(TokenType.MyKeyword);
    var condition = ParseExpression();
    Expect(TokenType.Colon);
    ExpectNewline();
    Expect(TokenType.Indent);
    var body = ParseBlock();
    Expect(TokenType.Dedent);

    return new MyCompoundStatement
    {
        Condition = condition,
        Body = body,
        LineStart = startLine,
        ColumnStart = startColumn,
        LineEnd = Current.Line,
        ColumnEnd = Current.Column
    };
}
```

---

## Cross-References

This file is part of the `Parser` partial class. Related files:

### Related Parser Partials
- **[Parser.cs](./Parser.md)** - Core parser infrastructure, main parsing loop, statement dispatch
- **[Parser.Expressions.cs](./Parser.Expressions.md)** - Expression parsing with operator precedence
- **[Parser.Definitions.cs](./Parser.Definitions.md)** - Function, class, and decorator definitions
- **[Parser.Types.cs](./Parser.Types.md)** - Type annotation parsing (`ParseTypeAnnotation()`)
- **Parser.Primaries.cs** - Primary expressions (literals, identifiers, etc.) used by `ParseForTarget()`

### AST Definitions
- **[Statement.cs](./Ast/Statement.md)** - All statement AST node types returned by methods in this file

### Upstream Components
- **[Lexer.cs](../Lexer/Lexer.md)** - Tokenizes source code, produces INDENT/DEDENT tokens
- **[Token.cs](../Lexer/Token.md)** - Token type definitions

### Downstream Components
- **[CodeGenContext.cs](../CodeGen/CodeGenContext.md)** - Semantic analysis and type checking
- **[RoslynEmitter.Statements.md](../CodeGen/RoslynEmitter.Statements.md)** - Converts statement AST to C# code

---

## Summary

`Parser.Statements.cs` is a crucial component of the Sharpy compiler's parser, responsible for transforming token streams into statement AST nodes. It demonstrates classic recursive descent parsing techniques while maintaining Python syntax compatibility.

**Key Takeaways for New Engineers**:
1. Each statement type has a dedicated parsing method
2. Parser expects INDENT/DEDENT tokens for block structure
3. Source location tracking is critical for error reporting
4. Comprehension clauses are shared by list/dict/set comprehensions
5. Python features like loop else clauses and exception chaining are supported
6. The parser is predictive (LL(1)-style) using single token lookahead

Understanding this file is essential for:
- Adding new Python statement types
- Debugging parsing errors
- Improving error messages
- Extending Sharpy's language features
