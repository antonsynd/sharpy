# Walkthrough: AstDumper.cs

**Source File**: `src/Sharpy.Compiler/Parser/AstDumper.cs`

---

## Overview

`AstDumper` is a diagnostic utility class that converts Abstract Syntax Tree (AST) nodes into human-readable tree-formatted text. It's the compiler's debugging Swiss Army knife—when you need to see what the parser actually produced, you dump the AST. This is invaluable during parser development, debugging semantic analysis issues, or understanding how Sharpy code gets represented internally.

**Pipeline Position**: Used throughout the compiler, but primarily during:
- **Parser debugging**: Verify the parser creates correct AST structures
- **Semantic analysis debugging**: Inspect the tree before type checking
- **Test diagnostics**: Generate readable output for test assertions
- **CLI emit command**: The `sharpyc emit ast` command uses this to show AST structure

**Design Philosophy**: The dumper uses a tree-drawing approach with Unicode box-drawing characters (`├─`, `└─`, `│`) to show parent-child relationships, making complex nested structures easy to understand at a glance. Every node includes source location information (`@ L{line}:C{column}`) for easy correlation with source code.

---

## Class Structure

### Main Class: `AstDumper`

```csharp
public class AstDumper
{
    private readonly StringBuilder _output;
    private const string IndentUnit = "  ";

    public AstDumper()
    {
        _output = new StringBuilder();
    }
}
```

**State Management**:
- `_output`: Accumulates the formatted tree output (mutable state)
- `IndentUnit`: Constant 2-space indent for each nesting level

**Key Design Decision**: Uses a single `StringBuilder` that gets cleared and reused on each `Dump()` call. This is more efficient than creating new string builders for each dump operation. The builder is instantiated once in the constructor and reused throughout the dumper's lifetime.

**Thread Safety**: Not thread-safe. Each thread should use its own `AstDumper` instance.

---

## Key Methods

### 1. `Dump(Module module)` - Entry Point

**Purpose**: The public API for dumping a complete module (the root AST node).

```csharp
public string Dump(Module module)
{
    _output.Clear();
    _output.AppendLine($"Module @ L{module.LineStart}:C{module.ColumnStart}");

    if (!string.IsNullOrEmpty(module.DocString))
    {
        _output.AppendLine($"{IndentUnit}DocString: \"{EscapeString(module.DocString)}\"");
    }

    _output.AppendLine($"{IndentUnit}Body: [{module.Body.Length} statement(s)]");

    for (int i = 0; i < module.Body.Length; i++)
    {
        DumpNode(module.Body[i], 2, i == module.Body.Length - 1);
    }

    return _output.ToString();
}
```

**How It Works**:
1. **Clears** previous output (allows reuse of same dumper instance)
2. **Prints** module header with source location (`L{line}:C{column}`)
3. **Shows** docstring if present (escaped for readability)
4. **Counts** statements in the module body (note: uses `ImmutableArray<T>.Length`)
5. **Recursively dumps** each statement with `isLast` tracking for tree formatting
6. **Returns** the complete formatted string

**Example Output**:
```
Module @ L1:C1
  DocString: "This is my module"
  Body: [3 statement(s)]
  ├─ FunctionDef @ L3:C1
  │  Name: add
  ├─ FunctionDef @ L7:C1
  │  Name: multiply
  └─ ExpressionStatement @ L11:C1
```

**Important Note**: The initial depth passed to `DumpNode` is `2`, not `1`, because the module body label already consumed one level of indentation.

---

### 2. `DumpNode(Node node, int depth, bool isLast)` - Core Recursive Dumper

**Purpose**: The workhorse method that handles dumping all AST node types. This is a 700+ line method with an exhaustive switch statement covering every node type in the Sharpy AST.

**Parameters**:
- `node`: The AST node to dump
- `depth`: Current nesting depth (controls indentation, multiplied by `IndentUnit.Length`)
- `isLast`: Whether this is the last child (affects tree drawing characters)

**Implementation Strategy**:
```csharp
var indent = new string(' ', depth * IndentUnit.Length);
var prefix = isLast ? "└─ " : "├─ ";           // Last child vs. middle child
var childPrefix = isLast ? "   " : "│  ";       // Indentation for sub-children
```

**The Tree Drawing Logic**:
- Last child uses `└─` (corner) and children get spaces `   ` (no vertical line)
- Middle children use `├─` (branch) and children get `│  ` (vertical line continues)
- This creates the characteristic tree structure that makes hierarchy immediately visible

**Node Type Handling**: Uses a massive `switch` statement with ~50 cases covering:

**Statements** (Lines 50-429):
- Control flow: `IfStatement` (with elif/else), `WhileStatement`, `ForStatement`, `TryStatement` (with except/else/finally)
- Declarations: `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
- Simple statements: `PassStatement`, `BreakStatement`, `ContinueStatement`, `ReturnStatement`
- Exception handling: `RaiseStatement`, `AssertStatement`
- Variable handling: `VariableDeclaration`, `Assignment`
- Imports: `ImportStatement`, `FromImportStatement`

**Expressions** (Lines 431-700):
- Literals: `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `FStringLiteral`, `BooleanLiteral`, `NoneLiteral`, `EllipsisLiteral`
- Collections: `ListLiteral`, `DictLiteral`, `SetLiteral`, `TupleLiteral`
- Comprehensions: `ListComprehension`, `SetComprehension`, `DictComprehension`
- Access: `Identifier`, `MemberAccess` (with null-conditional support), `IndexAccess`, `SliceAccess`
- Operations: `UnaryOp`, `BinaryOp`, `ComparisonChain`
- Advanced: `FunctionCall` (with keyword arguments), `LambdaExpression`, `ConditionalExpression`
- Type operations: `TypeCast`, `TypeCoercion`, `TypeCheck`
- Grouping: `Parenthesized`

**Default Case** (Lines 701-703):
```csharp
default:
    _output.AppendLine($"{indent}{prefix}{node.GetType().Name} @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```
If a new AST node type is added but not explicitly handled, it falls through to this case and shows just the type name and location. This prevents crashes but signals missing implementation.

---

### 3. Statement Dumping Patterns

The dumper uses consistent patterns based on statement complexity:

#### Leaf Statements (No Children)

**Example**: `PassStatement`, `BreakStatement`, `ContinueStatement` (Lines 90-99)

```csharp
case PassStatement:
    _output.AppendLine($"{indent}{prefix}PassStatement @ L{node.LineStart}:C{node.ColumnStart}");
    break;

case BreakStatement:
    _output.AppendLine($"{indent}{prefix}BreakStatement @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```

**Pattern**: Single-line output showing node type and location. No recursion needed.

#### Statements with Expression Children

**Example**: `ReturnStatement` (Lines 81-88)

```csharp
case ReturnStatement returnStmt:
    _output.AppendLine($"{indent}{prefix}ReturnStatement @ L{node.LineStart}:C{node.ColumnStart}");
    if (returnStmt.Value != null)
    {
        _output.AppendLine($"{indent}{childPrefix}Value:");
        DumpNode(returnStmt.Value, depth + 1, true);
    }
    break;
```

**Pattern**: 
1. Print statement header
2. Check for optional expression (`Value` can be null for `return` without a value)
3. Label the child ("Value:")
4. Recursively dump with `depth + 1` and `isLast: true` (since it's the only/last child)

#### Complex Control Flow Statements

**Example**: `IfStatement` with elif/else support (Lines 122-157)

```csharp
case IfStatement ifStmt:
    _output.AppendLine($"{indent}{prefix}IfStatement @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Test:");
    DumpNode(ifStmt.Test, depth + 2, false);
    _output.AppendLine($"{indent}{childPrefix}ThenBody: [{ifStmt.ThenBody.Length} statement(s)]");
    for (int i = 0; i < ifStmt.ThenBody.Length; i++)
    {
        DumpNode(ifStmt.ThenBody[i], depth + 2, i == ifStmt.ThenBody.Length - 1);
    }
    if (ifStmt.ElifClauses.Length > 0)
    {
        _output.AppendLine($"{indent}{childPrefix}ElifClauses: [{ifStmt.ElifClauses.Length}]");
        // ... dump each elif clause
    }
    if (ifStmt.ElseBody.Length > 0)
    {
        _output.AppendLine($"{indent}{childPrefix}ElseBody: [{ifStmt.ElseBody.Length} statement(s)]");
        // ... dump else body
    }
    break;
```

**Pattern**:
1. Print statement header
2. Dump test condition (not last, so `isLast: false`)
3. Show body count, then iterate and dump each statement
4. Handle optional sections (elif, else) conditionally
5. Careful `isLast` tracking: depends on whether more sections follow

**Key Insight**: Notice `depth + 2` when dumping children—this increases indentation by one level. Using `depth + 2` instead of `depth + 1` accounts for the label line ("Test:", "ThenBody:") consuming visual space.

#### Exception Handling: `TryStatement` (Lines 183-231)

The most complex statement in the dumper, handling try/except/else/finally:

```csharp
case TryStatement tryStmt:
    _output.AppendLine($"{indent}{prefix}TryStatement @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Body: [{tryStmt.Body.Length} statement(s)]");
    // ... dump body
    if (tryStmt.Handlers.Length > 0)
    {
        _output.AppendLine($"{indent}{childPrefix}Handlers: [{tryStmt.Handlers.Length}]");
        for (int i = 0; i < tryStmt.Handlers.Length; i++)
        {
            var handler = tryStmt.Handlers[i];
            // Complex isLast calculation: last if no else/finally follow
            var handlerPrefix = i == tryStmt.Handlers.Length - 1 
                && tryStmt.ElseBody.Length == 0 
                && tryStmt.FinallyBody.Length == 0 ? "└─ " : "├─ ";
            // ... dump exception type, name, handler body
        }
    }
    // ... handle else body and finally body
```

**Complexity**: The `isLast` calculation must look ahead to see if there are more sections (else, finally) to determine the correct tree-drawing character.

---

### 4. Expression Dumping Patterns

#### Simple Literals

**Example**: `IntegerLiteral` with optional suffix (Lines 432-435)

```csharp
case IntegerLiteral intLit:
    var intSuffix = intLit.Suffix != null ? $" ({intLit.Suffix})" : "";
    _output.AppendLine($"{indent}{prefix}IntegerLiteral: {intLit.Value}{intSuffix} @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```

**What It Shows**: Integer values can have type suffixes (like `42L` for long, `42u` for unsigned). The dumper shows both the value and optional suffix in parentheses.

**Example Output**:
- `42` → `IntegerLiteral: 42 @ L1:C1`
- `42L` → `IntegerLiteral: 42 (L) @ L1:C1`
- `0xFF` → `IntegerLiteral: 255 @ L1:C1` (value is already parsed)

#### String Literals with Metadata

**Example**: `StringLiteral` with raw string support (Lines 442-445)

```csharp
case StringLiteral strLit:
    var strPrefix = strLit.IsRaw ? "r" : "";
    _output.AppendLine($"{indent}{prefix}StringLiteral: {strPrefix}\"{EscapeString(strLit.Value)}\" @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```

**Pattern**: Shows whether it's a raw string (`r"..."`) and always escapes special characters for readability.

#### Complex Structure: F-String Interpolation (Lines 447-465)

F-strings (formatted string literals like `f"Hello {name}"`) are broken into parts:

```csharp
case FStringLiteral fstrLit:
    _output.AppendLine($"{indent}{prefix}FStringLiteral @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Parts: [{fstrLit.Parts.Length}]");
    for (int i = 0; i < fstrLit.Parts.Length; i++)
    {
        var part = fstrLit.Parts[i];
        var partIndent = new string(' ', (depth + 2) * IndentUnit.Length);
        var partPrefix = i == fstrLit.Parts.Length - 1 ? "└─ " : "├─ ";
        if (part.Text != null)
        {
            _output.AppendLine($"{partIndent}{partPrefix}Text: \"{EscapeString(part.Text)}\"");
        }
        else if (part.Expression != null)
        {
            _output.AppendLine($"{partIndent}{partPrefix}Expression:");
            DumpNode(part.Expression, depth + 3, true);
        }
    }
    break;
```

**Key Design**: F-strings aren't immediately evaluated—they're represented as a sequence of literal text and expression parts. Each part is either static text or an embedded expression that gets recursively dumped.

**Example**: `f"x = {x + 1}"` becomes:
```
FStringLiteral @ L1:C1
  Parts: [3]
  ├─ Text: "x = "
  ├─ Expression:
  │    └─ BinaryOp: Add @ L1:C10
  └─ Text: ""
```

#### Binary Operations with Operator Display (Lines 630-636)

```csharp
case BinaryOp binaryOp:
    _output.AppendLine($"{indent}{prefix}BinaryOp: {binaryOp.Operator} @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Left:");
    DumpNode(binaryOp.Left, depth + 2, false);
    _output.AppendLine($"{indent}{childPrefix}Right:");
    DumpNode(binaryOp.Right, depth + 2, true);
    break;
```

**Pattern**: Shows the operator type (Add, Subtract, Multiply, etc.) in the header, then dumps left and right operands. The right operand is always last.

#### Member Access with Null-Conditional Support (Lines 561-567)

```csharp
case MemberAccess memberAccess:
    var nullCond = memberAccess.IsNullConditional ? "?." : ".";
    _output.AppendLine($"{indent}{prefix}MemberAccess ({nullCond}) @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Object:");
    DumpNode(memberAccess.Object, depth + 2, false);
    _output.AppendLine($"{indent}{childPrefix}Member: {memberAccess.Member}");
    break;
```

**Feature Highlight**: Sharpy supports null-conditional operators (`?.`). The dumper shows whether the access is null-conditional in the header: `MemberAccess (.)` vs `MemberAccess (?.)`.

#### Function Calls with Keyword Arguments (Lines 598-622)

```csharp
case FunctionCall funcCall:
    _output.AppendLine($"{indent}{prefix}FunctionCall @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Function:");
    DumpNode(funcCall.Function, depth + 2, false);
    if (funcCall.Arguments.Length > 0)
    {
        _output.AppendLine($"{indent}{childPrefix}Arguments: [{funcCall.Arguments.Length}]");
        for (int i = 0; i < funcCall.Arguments.Length; i++)
        {
            DumpNode(funcCall.Arguments[i], depth + 2, 
                i == funcCall.Arguments.Length - 1 && funcCall.KeywordArguments.Length == 0);
        }
    }
    if (funcCall.KeywordArguments.Length > 0)
    {
        _output.AppendLine($"{indent}{childPrefix}KeywordArguments: [{funcCall.KeywordArguments.Length}]");
        for (int i = 0; i < funcCall.KeywordArguments.Length; i++)
        {
            var kwarg = funcCall.KeywordArguments[i];
            _output.AppendLine($"{kwIndent}{kwPrefix}{kwarg.Name} @ L{kwarg.LineStart}:C{kwarg.ColumnStart}:");
            DumpNode(kwarg.Value, depth + 3, true);
        }
    }
    break;
```

**Complexity**: The `isLast` tracking for positional arguments must check if keyword arguments follow. Keyword arguments are shown with their parameter names.

---

### 5. Helper Methods

#### `DumpParameter(Parameter param, int depth, bool isLast)` - Parameter Formatting (Lines 707-727)

**Purpose**: Specialized dumper for function/lambda parameters that tries to fit on one line when possible.

```csharp
private void DumpParameter(Parameter param, int depth, bool isLast)
{
    var indent = new string(' ', depth * IndentUnit.Length);
    var prefix = isLast ? "└─ " : "├─ ";
    var childPrefix = isLast ? "   " : "│  ";

    _output.Append($"{indent}{prefix}Parameter: {param.Name} @ L{param.LineStart}:C{param.ColumnStart}");
    if (param.Type != null)
    {
        _output.Append($" : {FormatType(param.Type)}");
    }
    if (param.DefaultValue != null)
    {
        _output.AppendLine(" =");
        DumpNode(param.DefaultValue, depth + 1, true);
    }
    else
    {
        _output.AppendLine();
    }
}
```

**Compact Formatting Strategy**:
- Uses `Append` (not `AppendLine`) to build the parameter line incrementally
- Type annotation appears inline: `Parameter: x @ L5:C10 : int`
- If there's a default value, adds ` =` and dumps the value on the next line
- If no default, just ends the line

**Example Outputs**:
```
Parameter: x @ L5:C10 : int
Parameter: y @ L5:C20 : str = 
  └─ StringLiteral: "default" @ L5:C25
Parameter: z @ L5:C35
```

#### `DumpTypeAnnotation(TypeAnnotation type, int depth, bool isLast)` - Type Display (Lines 729-734)

**Purpose**: Dumps type annotations with full generic and nullability information.

```csharp
private void DumpTypeAnnotation(TypeAnnotation type, int depth, bool isLast)
{
    var indent = new string(' ', depth * IndentUnit.Length);
    var prefix = isLast ? "└─ " : "├─ ";
    _output.AppendLine($"{indent}{prefix}{FormatType(type)} @ L{type.LineStart}:C{type.ColumnStart}");
}
```

**Simple but essential**: Delegates to `FormatType` for the actual formatting, then adds location information.

#### `FormatType(TypeAnnotation type)` - Recursive Type String Builder (Lines 736-749)

**Purpose**: Converts type annotations into readable strings with full generic nesting and nullability.

```csharp
private string FormatType(TypeAnnotation type)
{
    var result = type.Name;
    if (type.TypeArguments.Length > 0)
    {
        var args = string.Join(", ", type.TypeArguments.Select(FormatType));
        result += $"[{args}]";
    }
    if (type.IsNullable)
    {
        result += "?";
    }
    return result;
}
```

**Recursive Generics**: The recursive call to `FormatType` via LINQ's `Select` handles arbitrarily nested generic types.

**Example Outputs**:
- `int` → `"int"`
- `int?` → `"int?"`
- `List[str]` → `"List[str]"`
- `Dict[str, int?]` → `"Dict[str, int?]"`
- `Dict[str, List[int?]?]?` → `"Dict[str, List[int?]?]?"`

**Design Note**: Uses Python-style square brackets `[...]` for generics, not C# angle brackets `<...>`. This matches Sharpy's surface syntax.

#### `EscapeString(string str)` - String Sanitization (Lines 751-759)

**Purpose**: Makes string content safe for display by escaping special characters.

```csharp
private string EscapeString(string str)
{
    return str
        .Replace("\\", "\\\\")   // Backslash must be first!
        .Replace("\n", "\\n")    // Newlines
        .Replace("\r", "\\r")    // Carriage returns
        .Replace("\t", "\\t")    // Tabs
        .Replace("\"", "\\\"");  // Quotes
}
```

**Critical Detail**: Backslash is replaced **first** to avoid double-escaping. If we replaced `\n` → `\\n` first, then replaced `\\` → `\\\\`, we'd end up with `\\\\n` (incorrect).

**Why It Matters**: Dumping a string literal with actual newlines would break the tree formatting. Multi-line strings appear as `"First line\nSecond line"`, keeping the dump on one line.

**Example**:
- Input: `Hello\nWorld\t!`
- Output: `"Hello\\nWorld\\t!"`

#### `DumpComprehensionClause(ComprehensionClause clause, int depth, bool isLast)` - Comprehension Support (Lines 761-787)

**Purpose**: Handles the for-clauses and if-clauses in list/set/dict comprehensions.

```csharp
private void DumpComprehensionClause(ComprehensionClause clause, int depth, bool isLast)
{
    var indent = new string(' ', depth * IndentUnit.Length);
    var prefix = isLast ? "└─ " : "├─ ";
    var childPrefix = isLast ? "   " : "│  ";

    switch (clause)
    {
        case ForClause forClause:
            _output.AppendLine($"{indent}{prefix}ForClause @ L{clause.LineStart}:C{clause.ColumnStart}");
            _output.AppendLine($"{indent}{childPrefix}Target:");
            DumpNode(forClause.Target, depth + 2, false);
            _output.AppendLine($"{indent}{childPrefix}Iterator:");
            DumpNode(forClause.Iterator, depth + 2, true);
            break;

        case IfClause ifClause:
            _output.AppendLine($"{indent}{prefix}IfClause @ L{clause.LineStart}:C{clause.ColumnStart}");
            _output.AppendLine($"{indent}{childPrefix}Condition:");
            DumpNode(ifClause.Condition, depth + 2, true);
            break;

        default:
            _output.AppendLine($"{indent}{prefix}{clause.GetType().Name} @ L{clause.LineStart}:C{clause.ColumnStart}");
            break;
    }
}
```

**Used By**: `ListComprehension`, `SetComprehension`, `DictComprehension` cases (lines 522-555).

**Example**: For `[x * 2 for x in range(10) if x % 2 == 0]`:
```
ListComprehension @ L1:C1
  Element:
    └─ BinaryOp: Multiply
  Clauses: [2]
  ├─ ForClause @ L1:C12
  │  Target:
  │    └─ Identifier: x
  │  Iterator:
  │    └─ FunctionCall @ L1:C17
  └─ IfClause @ L1:C28
     Condition:
       └─ ComparisonChain
```

---

## Dependencies

### Internal Dependencies

**Primary Dependency**: `Sharpy.Compiler.Parser.Ast`
- **All AST node types**: `Module`, `Node`, `Statement`, `Expression`, `TypeAnnotation`, `Parameter`, etc.
- **Record types**: The dumper uses pattern matching on these immutable record types
- **Location tracking**: Every node implements `ILocatable` with `LineStart`, `ColumnStart`, `LineEnd`, `ColumnEnd`

**What the dumper needs to know**:
- The complete hierarchy of AST nodes (50+ node types)
- Which nodes have children and how to access them
- Which properties are nullable/optional
- The structure of composite types like `FStringLiteral.Parts`, `TryStatement.Handlers`, etc.

### External Dependencies

- `System.Text.StringBuilder` - For efficient string building
- `System.Linq` - Used in `FormatType` for recursive generic type formatting

### No Dependencies On

**Important**: `AstDumper` does **not** depend on:
- Semantic analysis (no type checking, no symbol resolution)
- Code generation
- The lexer (gets location info from AST nodes, not tokens)

This makes it a pure AST visualization tool that works immediately after parsing, before any semantic analysis.

---

## Patterns and Design Decisions

### 1. **Switch Statement Pattern Matching**

The dumper uses C# 8's pattern matching in a switch statement to handle polymorphic AST nodes:

```csharp
switch (node)
{
    case ExpressionStatement exprStmt:  // Pattern match + variable binding
        // Use exprStmt with full type information
        break;
    case Assignment assignment:
        // Use assignment with full type information
        break;
    // ... 50+ more cases
    default:
        // Fallback for unhandled node types
        _output.AppendLine($"{indent}{prefix}{node.GetType().Name} @ L{node.LineStart}:C{node.ColumnStart}");
        break;
}
```

**Why This Pattern**:
- Avoids messy `if-elseif` chains with type checks
- Provides compile-time type safety (each case has correctly-typed variable)
- Exhaustiveness: missing cases go to `default`, preventing crashes
- Clean and readable: each case is self-contained

**Alternative Not Used**: Visitor pattern (see #5 below for why)

### 2. **Location Annotations Throughout**

Every node dumps its source location: `@ L{line}:C{column}`

**Why**: When debugging parser issues, you need to know *exactly* where in the source file each AST node came from. This makes it trivial to correlate AST structure with source code.

**Example Use Case**: Parser creates wrong AST for `if x > 0:`. The dumper shows:
```
IfStatement @ L5:C1
  Test:
    └─ ComparisonChain @ L5:C4
```
You immediately know the `if` starts at line 5, column 1, and the test starts at column 4.

### 3. **Count-Then-Dump Pattern**

```csharp
_output.AppendLine($"{indent}{childPrefix}Body: [{funcDef.Body.Length} statement(s)]");
for (int i = 0; i < funcDef.Body.Length; i++)
{
    DumpNode(funcDef.Body[i], depth + 2, i == funcDef.Body.Length - 1);
}
```

**Why**: Showing counts (`[3 statement(s)]`) gives immediate context about the size of collections before diving into details. You can quickly see "this function has 10 statements" without counting tree branches.

**Consistency**: Used for all collections: function bodies, parameters, elif clauses, comprehension clauses, etc.

### 4. **The `isLast` Tracking Algorithm**

This boolean is threaded through every dump call to control tree drawing:

```csharp
for (int i = 0; i < items.Length; i++)
{
    DumpNode(items[i], depth, i == items.Length - 1);  // Last item gets true
}
```

**Why**: The tree box-drawing characters must be different for the last child (`└─`) vs. middle children (`├─`), otherwise the tree looks broken.

**Complexity**: In some cases (e.g., `TryStatement`), the `isLast` calculation must look ahead:
```csharp
var handlerPrefix = i == tryStmt.Handlers.Length - 1 
    && tryStmt.ElseBody.Length == 0 
    && tryStmt.FinallyBody.Length == 0 ? "└─ " : "├─ ";
```
This checks if there are more sections (else, finally) to determine if the last handler is truly last.

### 5. **No Visitor Pattern**

**Interesting Decision**: This dumper does *not* use the visitor pattern, despite AST traversal being a classic visitor use case.

**Why Not Visitor**:
- **Boilerplate**: Would require adding `Accept` methods to all 50+ AST node classes
- **Not needed**: C# 8+ pattern matching in switch statements is just as clean
- **Maintainability**: All dumping logic in one 788-line file vs. scattered across node classes
- **IDE support**: Jump-to-definition, find-references work cleanly with switch cases
- **Single Responsibility**: AST nodes don't need to know about dumping (separation of concerns)

**Tradeoff**: Adding new node types requires updating the switch statement here. But that's visible compile-time work, not a hidden runtime bug.

### 6. **Immutable AST, Mutable Output**

**Pattern**: The AST is immutable (record types, init-only properties), but the dumper uses mutable `StringBuilder`.

**Why**: 
- AST immutability ensures thread-safety and prevents accidental modification during traversal
- StringBuilder mutability is appropriate here—we're building a linear string incrementally
- Best of both worlds: safe data structures, efficient string building

### 7. **ImmutableArray Usage**

All collections in AST nodes use `ImmutableArray<T>`, not `List<T>`:

```csharp
public ImmutableArray<Statement> Body { get; init; }
```

**Impact on Dumper**:
- Uses `.Length` not `.Count`
- No risk of collection modification during traversal
- Slightly more efficient than `List<T>` for read-only scenarios

### 8. **Depth-Based Indentation Algorithm**

```csharp
var indent = new string(' ', depth * IndentUnit.Length);
```

**How It Works**:
- Each depth level = 2 spaces (`IndentUnit.Length = 2`)
- Depth 0: no indent
- Depth 1: 2 spaces
- Depth 2: 4 spaces
- etc.

**Why Simple String Allocation**: Creating a new string of spaces is actually efficient for small depths (< 20). For very deep nesting, could optimize with a cached indent string array, but this is a debugging tool—clarity over micro-optimization.

### 9. **Escaping Strategy for Strings**

**Order Matters**:
```csharp
.Replace("\\", "\\\\")   // MUST be first!
.Replace("\n", "\\n")
```

**Why**: If `\n` were replaced first, we'd get:
1. `\n` → `\\n`
2. `\\n` → `\\\\n` (wrong!)

By doing backslash first, we correctly handle escape sequences.

### 10. **Compact vs. Verbose Formatting**

Some nodes try to be compact (parameters on one line), others are always verbose (statements with bodies):

**Compact** (Parameters):
```
Parameter: x @ L5:C10 : int
```

**Verbose** (If statement):
```
IfStatement @ L5:C1
  Test:
    └─ ...
  ThenBody: [...]
```

**Rationale**: Parameters are typically simple (name + type + maybe default), so one line is readable. Statements have complex nested structure requiring hierarchical display.

---

## Debugging Tips

### When AST Dumps Look Wrong

**Problem**: Tree structure seems off—lines don't connect properly.

**Check**:
1. **Incorrect `isLast` calculation**: Look for off-by-one errors in loop conditions
2. **Depth not incrementing**: Ensure `depth + 1` or `depth + 2` when calling `DumpNode` for children
3. **Prefix/childPrefix swapped**: Make sure you're using `childPrefix` for labels, not `prefix`

**Example Bug**:
```csharp
// WRONG: Should be depth + 2, not depth + 1
DumpNode(ifStmt.Test, depth + 1, false);
```

### Missing AST Node Types

**Problem**: Getting `[NodeType] @ L5:C10` with no details.

**Cause**: The node type hits the `default` case (line 701-703) because there's no specific handler.

**Fix**: Add a new case for that node type in the switch statement.

### String Escaping Issues

**Problem**: Dumped strings break formatting or look garbled.

**Check**: `EscapeString()` might be missing an escape sequence. The current implementation handles the common cases, but if Sharpy adds new string features (like raw strings with special semantics), you might need to update escaping.

---

## Contribution Guidelines

### Adding Support for New AST Nodes

When adding a new AST node type (e.g., `YieldStatement`), you must:

1. **Add a case in `DumpNode`** (around line 48):
   ```csharp
   case YieldStatement yieldStmt:
       _output.AppendLine($"{indent}{prefix}YieldStatement @ L{node.LineStart}:C{node.ColumnStart}");
       if (yieldStmt.Value != null)
       {
           _output.AppendLine($"{indent}{childPrefix}Value:");
           DumpNode(yieldStmt.Value, depth + 1, true);
       }
       break;
   ```

2. **Follow the patterns**:
   - Always show location (`@ L{line}:C{column}`)
   - Dump children recursively with proper depth
   - Track `isLast` correctly for tree drawing

3. **Test it**: Write a Sharpy code snippet using the new construct, dump the AST, and verify the output is readable.

### Improving Formatting

**Current Limitations**:
- Very deep nesting (10+ levels) becomes hard to read
- Long lines wrap awkwardly in narrow terminals
- No color output (could use ANSI codes to highlight node types)

**Enhancement Ideas**:
- Add optional maximum width parameter with intelligent wrapping
- Colorize node types (blue for statements, green for expressions, etc.)
- Add a "compact" mode that elides deeply nested nodes

### Performance Considerations

**Current Performance**: `StringBuilder` appending is O(1) amortized, recursive tree walk is O(n) where n is node count. This is fine for dumping single files.

**Don't Prematurely Optimize**: AST dumping is a *debugging tool*, not production code. Clarity trumps speed. If you're dumping enormous ASTs (100k+ nodes) and it's slow, that's a sign you should be dumping smaller subtrees, not optimizing the dumper.

---

## Real-World Usage Examples

### Example 1: Debugging Parser Changes

You're modifying the parser to support a new syntax. Dump the AST before and after:

```csharp
var module = parser.ParseModule(sourceCode);
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

Compare the output to ensure the parser creates the expected structure.

### Example 2: Writing Parser Tests

Use the dumper in unit tests to assert AST structure:

```csharp
[Test]
public void TestIfStatement()
{
    var source = "if x > 0:\n    print(x)\n";
    var module = parser.ParseModule(source);
    var dumped = new AstDumper().Dump(module);

    Assert.That(dumped, Does.Contain("IfStatement"));
    Assert.That(dumped, Does.Contain("Test:"));
    Assert.That(dumped, Does.Contain("ThenBody: [1 statement(s)]"));
}
```

### Example 3: Understanding Comprehension Desugaring

List comprehensions are syntactic sugar. Dump them to see how they're represented:

```python
# Sharpy code: [x * 2 for x in range(10) if x % 2 == 0]
```

The dumper will show:
```
ListComprehension @ L1:C1
  Element:
    └─ BinaryOp: Multiply @ L1:C2
       Left:
         └─ Identifier: x @ L1:C2
       Right:
         └─ IntegerLiteral: 2 @ L1:C6
  Clauses: [2]
    ├─ ForClause @ L1:C12
    │  Target:
    │    └─ Identifier: x @ L1:C12
    │  Iterator:
    │    └─ FunctionCall @ L1:C17
    │       Function:
    │         └─ Identifier: range @ L1:C17
    └─ IfClause @ L1:C28
       Condition:
         └─ ComparisonChain @ L1:C31
```

This shows the comprehension isn't immediately desugared—it's kept as a `ListComprehension` node with separate `ForClause` and `IfClause` children.

---

## Cross-References

### Related Files

- **[`Ast/Expression.md`](Ast/Expression.md)**: Definitions of all expression node types that this dumper formats
- **[`Ast/Statement.md`](Ast/Statement.md)**: Definitions of all statement node types
- **[`Parser.md`](Parser.md)**: The parser creates the AST nodes that this dumper visualizes

### Upstream Components

- **Lexer**: Provides tokens with line/column information that gets embedded in AST nodes
- **Parser**: Creates the AST tree structure

### Downstream Usage

- **Test suites**: Integration tests use dumpers to verify parser correctness
- **Developer debugging**: Anyone working on the parser, semantic analyzer, or code generator uses this
- **Documentation generation**: Could be extended to auto-generate AST structure docs

---

## Summary

`AstDumper` is deceptively simple—just 788 lines of straightforward code—but it's an essential compiler development tool. Its tree-drawing approach makes complex nested structures immediately comprehensible, and having source locations on every node makes debugging parser issues trivial.

**Key Takeaway**: When you change the parser or add new AST nodes, update the dumper *first*. Having good diagnostic tooling makes the rest of compiler development much smoother.
