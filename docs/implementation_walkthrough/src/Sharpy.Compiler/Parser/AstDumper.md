# Walkthrough: AstDumper.cs

**Source File**: `src/Sharpy.Compiler/Parser/AstDumper.cs`

---

## Overview

`AstDumper` is a diagnostic utility class that converts Abstract Syntax Tree (AST) nodes into human-readable tree-formatted text. It's the compiler's debugging Swiss Army knife—when you need to see what the parser actually produced, you dump the AST. This is invaluable during parser development, debugging semantic analysis issues, or understanding how Sharpy code gets represented internally.

**Pipeline Position**: Used throughout the compiler, but primarily during:
- **Parser debugging**: Verify the parser creates correct AST structures
- **Semantic analysis debugging**: Inspect the tree before type checking
- **Test diagnostics**: Generate readable output for test assertions

**Design Philosophy**: The dumper uses a tree-drawing approach with box-drawing characters (`├─`, `└─`, `│`) to show parent-child relationships, making complex nested structures easy to understand at a glance.

---

## Class Structure

### Main Class: `AstDumper`

```csharp
public class AstDumper
{
    private readonly StringBuilder _output;
    private const string IndentUnit = "  ";
}
```

**State Management**:
- `_output`: Accumulates the formatted tree output (mutable state)
- `IndentUnit`: Constant 2-space indent for each nesting level

**Key Design Decision**: Uses a single `StringBuilder` that gets cleared and reused on each `Dump()` call. This is more efficient than creating new string builders for each dump operation.

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

    _output.AppendLine($"{IndentUnit}Body: [{module.Body.Count} statement(s)]");

    for (int i = 0; i < module.Body.Count; i++)
    {
        DumpNode(module.Body[i], 2, i == module.Body.Count - 1);
    }

    return _output.ToString();
}
```

**How It Works**:
1. **Clears** previous output (allows reuse of same dumper instance)
2. **Prints** module header with source location (`L{line}:C{column}`)
3. **Shows** docstring if present (escaped for readability)
4. **Counts** statements in the module body
5. **Recursively dumps** each statement with `isLast` tracking for tree formatting

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

---

### 2. `DumpNode(Node node, int depth, bool isLast)` - Core Recursive Dumper

**Purpose**: The workhorse method that handles dumping all AST node types.

**Parameters**:
- `node`: The AST node to dump
- `depth`: Current nesting depth (controls indentation)
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

**Node Type Handling**: Uses a massive `switch` statement with ~50 cases covering:
- **Statements**: `Assignment`, `VariableDeclaration`, `IfStatement`, `ForStatement`, `TryStatement`, etc.
- **Expressions**: `IntegerLiteral`, `FunctionCall`, `BinaryOp`, `ListComprehension`, etc.
- **Declarations**: `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef`
- **Import statements**: `ImportStatement`, `FromImportStatement`

---

### 3. Statement Dumping Examples

#### Simple Statement: `PassStatement` (Line 90-92)

```csharp
case PassStatement:
    _output.AppendLine($"{indent}{prefix}PassStatement @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```

**Pattern**: Leaf nodes just print their type and location—no children to dump.

#### Complex Statement: `IfStatement` (Lines 122-157)

```csharp
case IfStatement ifStmt:
    _output.AppendLine($"{indent}{prefix}IfStatement @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Test:");
    DumpNode(ifStmt.Test, depth + 2, false);
    _output.AppendLine($"{indent}{childPrefix}ThenBody: [{ifStmt.ThenBody.Count} statement(s)]");
    for (int i = 0; i < ifStmt.ThenBody.Count; i++)
    {
        DumpNode(ifStmt.ThenBody[i], depth + 2, i == ifStmt.ThenBody.Count - 1);
    }
    // ... handles elif clauses and else body
```

**Pattern**:
1. Print statement header
2. Recursively dump the test condition
3. List body statements count
4. Dump each body statement (tracking `isLast` for proper tree drawing)
5. Handle optional parts (elif, else)

**Key Insight**: Notice `depth + 2` when dumping children—this increases indentation by one level.

---

### 4. Expression Dumping Examples

#### Literals with Metadata: `IntegerLiteral` (Lines 432-435)

```csharp
case IntegerLiteral intLit:
    var intSuffix = intLit.Suffix != null ? $" ({intLit.Suffix})" : "";
    _output.AppendLine($"{indent}{prefix}IntegerLiteral: {intLit.Value}{intSuffix} @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```

**What It Shows**: Integer values can have type suffixes (like `42L` for long, `42u` for unsigned). The dumper shows both the value and suffix.

#### String Interpolation: `FStringLiteral` (Lines 447-465)

```csharp
case FStringLiteral fstrLit:
    _output.AppendLine($"{indent}{prefix}FStringLiteral @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Parts: [{fstrLit.Parts.Count}]");
    for (int i = 0; i < fstrLit.Parts.Count; i++)
    {
        var part = fstrLit.Parts[i];
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
```

**Key Design**: F-strings (like `f"Hello {name}"`) are broken into parts—literal text or embedded expressions. Each part gets dumped separately, showing the interpolation structure.

---

### 5. `DumpParameter(Parameter param, int depth, bool isLast)` - Parameter Dumping

**Purpose**: Specialized dumper for function/lambda parameters.

```csharp
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
```

**Compact Formatting**: Parameters try to fit on one line when possible:
- `Parameter: x @ L5:C10 : int = 42`

But if the default value is complex, it's dumped recursively on subsequent lines.

---

### 6. `FormatType(TypeAnnotation type)` - Type Formatting Helper

**Purpose**: Converts type annotations into readable strings.

```csharp
private string FormatType(TypeAnnotation type)
{
    var result = type.Name;
    if (type.TypeArguments.Count > 0)
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

**Recursive Generics**: Handles nested generic types like `List[Dict[str, int?]]?`

**Example Outputs**:
- `int` → `"int"`
- `List[str]` → `"List[str]"`
- `Dict[str, int?]?` → `"Dict[str, int?]?"`

---

### 7. `EscapeString(string str)` - String Sanitization

**Purpose**: Makes string content safe for display by escaping special characters.

```csharp
return str
    .Replace("\\", "\\\\")   // Backslash must be first!
    .Replace("\n", "\\n")    // Newlines
    .Replace("\r", "\\r")    // Carriage returns
    .Replace("\t", "\\t")    // Tabs
    .Replace("\"", "\\\"");  // Quotes
```

**Critical Detail**: Backslash is replaced first to avoid double-escaping (e.g., `\n` shouldn't become `\\n` then `\\\\n`).

**Why It Matters**: Dumping a string literal with actual newlines would break the tree formatting. This ensures multi-line strings appear as `"First line\nSecond line"`.

---

## Dependencies

### Internal Dependencies

**Primary**: `Sharpy.Compiler.Parser.Ast`
- All AST node types (`Module`, `Statement`, `Expression`, etc.)
- This is the entire AST vocabulary the dumper must understand

**See Also**:
- [`Ast/Expression.md`](Ast/Expression.md) - Expression node definitions
- [`Ast/Statement.md`](Ast/Statement.md) - Statement node definitions

---

## Patterns and Design Decisions

### 1. **Switch Expression Pattern Matching**

The dumper uses C# 8's pattern matching in a switch expression to handle polymorphic AST nodes:

```csharp
switch (node)
{
    case ExpressionStatement exprStmt:  // Pattern match + variable binding
        // Use exprStmt with full type information
        break;
    case Assignment assignment:
        // Use assignment with full type information
        break;
}
```

**Why**: Avoids messy `if-elseif` chains and gives compile-time exhaustiveness checking warnings.

### 2. **Location Annotations Throughout**

Every node dumps its source location: `@ L{line}:C{column}`

**Why**: When debugging parser issues, you need to know *exactly* where in the source file each AST node came from. This makes it trivial to correlate AST structure with source code.

### 3. **Count-Then-Dump Pattern**

```csharp
_output.AppendLine($"{indent}{childPrefix}Body: [{funcDef.Body.Count} statement(s)]");
for (int i = 0; i < funcDef.Body.Count; i++)
{
    DumpNode(funcDef.Body[i], depth + 2, i == funcDef.Body.Count - 1);
}
```

**Why**: Showing counts (`[3 statement(s)]`) gives immediate context about the size of collections before diving into details.

### 4. **The `isLast` Tracking**

This boolean is threaded through every dump call to control tree drawing:

```csharp
for (int i = 0; i < items.Count; i++)
{
    DumpNode(items[i], depth, i == items.Count - 1);  // Last item gets true
}
```

**Why**: The tree box-drawing characters must be different for the last child (`└─`) vs. middle children (`├─`), otherwise the tree looks broken.

### 5. **No Visitor Pattern**

**Interesting Decision**: This dumper does *not* use the visitor pattern, despite AST traversal being a classic visitor use case.

**Why**:
- Visitor pattern adds boilerplate (accept methods in all node classes)
- Switch expressions are just as clean in C# 8+
- Keeps all dumping logic in one file (easier to maintain)
- Better IDE support (jump to definition works cleanly)

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
