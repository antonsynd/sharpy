# Walkthrough: AstDumper.cs

**Source File**: `src/Sharpy.Compiler/Parser/AstDumper.cs`

---

## 1. Overview

`AstDumper` is a diagnostic utility that transforms Abstract Syntax Trees (AST) into human-readable tree-formatted strings. It's primarily used for debugging the parser and understanding the structure of parsed Sharpy code. When you need to visualize what the parser produced from source code, this is your go-to tool.

### Role in the Compiler Pipeline

```
Source (.spy) → Lexer → Parser (AST) → Semantic Analysis → RoslynEmitter → C#
                              ↓
                        AstDumper (debug visualization)
```

`AstDumper` is a **side-channel debugging tool**—it observes the AST but doesn't participate in the main compilation pipeline. It's invaluable for:
- **Upstream verification**: Confirming the Parser created the expected AST structure
- **Downstream debugging**: Understanding what the Semantic Analyzer and CodeGen will receive

**Key Use Cases:**
- Debugging parser issues (did it parse what you expected?)
- Understanding AST structure when implementing new language features
- Creating test assertions that verify specific AST shapes
- Educational purposes (showing newcomers how code becomes an AST)

**Not for:** Production code generation or semantic analysis—this is purely a visualization tool.

---

## 2. Class/Type Structure

### Main Class: `AstDumper`

```csharp
public class AstDumper
{
    private readonly StringBuilder _output;
    private const string IndentUnit = "  ";

    public AstDumper();
    public string Dump(Module module);
}
```

**Design Philosophy:**
- **Stateful but reusable**: Uses a `StringBuilder` instance field that gets cleared on each `Dump()` call
- **Two-space indentation**: The `IndentUnit` constant ensures consistent visual hierarchy
- **Tree-drawing characters**: Uses Unicode box-drawing characters (`├─`, `└─`, `│`) for visual clarity

### Internal Structure

The class organizes AST node handling into logical sections:

| Section | Node Types | Line Range |
|---------|------------|------------|
| Statements | `ExpressionStatement`, `Assignment`, `IfStatement`, `WhileStatement`, `ForStatement`, `TryStatement`, `FunctionDef`, `ClassDef`, etc. | 50-421 |
| Expressions | `IntegerLiteral`, `StringLiteral`, `BinaryOp`, `FunctionCall`, `ListComprehension`, etc. | 424-696 |
| Helpers | `DumpParameter`, `DumpTypeAnnotation`, `FormatType`, `EscapeString`, `DumpComprehensionClause` | 699-779 |

---

## 3. Key Functions/Methods

### 3.1 `Dump(Module module)` - Entry Point

**Purpose:** Converts an entire parsed module (file) into a formatted string representation.

```csharp
public string Dump(Module module)
{
    _output.Clear();
    _output.AppendLine($"Module @ L{module.LineStart}:C{module.ColumnStart}");
    // ... handles docstrings and body statements
    return _output.ToString();
}
```

**What it does:**
1. Clears any previous output (makes the dumper reusable)
2. Writes the module header with source location
3. Optionally includes the module's docstring
4. Iterates through all top-level statements in the module body
5. Calls `DumpNode()` for each statement with proper depth tracking

**Location Format:** The `@ L{line}:C{column}` pattern appears throughout—it helps you trace AST nodes back to source code positions for debugging.

**Example Output:**
```
Module @ L1:C1
  DocString: "This is a sample module"
  Body: [2 statement(s)]
  ├─ FunctionDef @ L3:C1
  │  Name: hello
  └─ ExpressionStatement @ L6:C1
```

---

### 3.2 `DumpNode(Node node, int depth, bool isLast)` - The Workhorse

**Purpose:** Recursively dumps any AST node with appropriate indentation and tree-drawing characters.

**Parameters:**
- `node`: The AST node to dump (can be Statement or Expression)
- `depth`: Current nesting level (multiplied by `IndentUnit` for spacing)
- `isLast`: Whether this is the last child in its parent's collection (affects tree drawing)

**Algorithm:**
1. Calculate indentation based on depth
2. Choose tree prefix: `└─` for last child, `├─` for others
3. Use pattern matching (switch statement) to handle each AST node type
4. Recursively call `DumpNode()` for child expressions/statements
5. Use helper methods for specialized nodes (parameters, type annotations, etc.)

**Why the `isLast` parameter matters:**
```
├─ Statement 1        # isLast=false, draws vertical line connector
│  └─ Child
└─ Statement 2        # isLast=true, no vertical line after
   └─ Child
```

---

### 3.3 Node Type Handling (The Big Switch Statement)

The `DumpNode` method contains a massive switch statement with 40+ cases. Here's how it's organized:

#### **Statements** (Lines 50-421)

**Simple Statements:**
```csharp
case PassStatement:
    _output.AppendLine($"{indent}{prefix}PassStatement @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```
These have no children, so they just write their name and location.

**Statements with Single Child:**
```csharp
case ReturnStatement returnStmt:
    _output.AppendLine($"{indent}{prefix}ReturnStatement @ ...");
    if (returnStmt.Value != null)
    {
        _output.AppendLine($"{indent}{childPrefix}Value:");
        DumpNode(returnStmt.Value, depth + 1, true);
    }
    break;
```
Conditionally dump the child if present (returns can be empty).

**Statements with Multiple Children:**
```csharp
case IfStatement ifStmt:
    // Dumps test, then body, elif clauses, else body
    // Each section needs careful isLast tracking
```
Complex nodes like `IfStatement`, `TryStatement`, and `FunctionDef` require:
- Multiple sections with labels ("Test:", "ThenBody:", "ElseBody:")
- Iteration over collections with proper `isLast` flags
- Nested indentation calculations

#### **Expressions** (Lines 424-688)

**Literals** - Simple values:
```csharp
case IntegerLiteral intLit:
    var intSuffix = intLit.Suffix != null ? $" ({intLit.Suffix})" : "";
    _output.AppendLine($"{indent}{prefix}IntegerLiteral: {intLit.Value}{intSuffix} @ ...");
    break;
```
Display the literal value inline (e.g., "IntegerLiteral: 42").

**Container Literals** - Collections:
```csharp
case ListLiteral listLit:
    _output.AppendLine($"{indent}{prefix}ListLiteral @ ...");
    _output.AppendLine($"{indent}{childPrefix}Elements: [{listLit.Elements.Count}]");
    for (int i = 0; i < listLit.Elements.Count; i++)
    {
        DumpNode(listLit.Elements[i], depth + 2, i == listLit.Elements.Count - 1);
    }
    break;
```
Show count, then recursively dump each element.

**Operators:**
```csharp
case BinaryOp binaryOp:
    _output.AppendLine($"{indent}{prefix}BinaryOp: {binaryOp.Operator} @ ...");
    _output.AppendLine($"{indent}{childPrefix}Left:");
    DumpNode(binaryOp.Left, depth + 2, false);
    _output.AppendLine($"{indent}{childPrefix}Right:");
    DumpNode(binaryOp.Right, depth + 2, true);
    break;
```
Display operator symbol, then dump left and right operands.

---

### 3.4 Helper Methods

#### `DumpParameter(Parameter param, int depth, bool isLast)`

Specialized dumper for function parameters:

```csharp
private void DumpParameter(Parameter param, int depth, bool isLast)
{
    _output.Append($"{indent}{prefix}Parameter: {param.Name} @ ...");
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

**Smart formatting:** Puts simple info (name, type) on one line, but expands default values to child nodes.

**Example:**
```
Parameters: [2]
├─ Parameter: x @ L5:C10 : int
└─ Parameter: y @ L5:C17 : int =
   └─ IntegerLiteral: 42
```

---

#### `DumpTypeAnnotation(TypeAnnotation type, int depth, bool isLast)`

Dumps type annotations (like `list[int]` or `dict[str, float?]`):

```csharp
private void DumpTypeAnnotation(TypeAnnotation type, int depth, bool isLast)
{
    var indent = new string(' ', depth * IndentUnit.Length);
    var prefix = isLast ? "└─ " : "├─ ";
    _output.AppendLine($"{indent}{prefix}{FormatType(type)} @ L{type.LineStart}:C{type.ColumnStart}");
}
```

Delegates to `FormatType()` for the actual formatting.

---

#### `FormatType(TypeAnnotation type)` - Recursive Type Formatter

**Purpose:** Converts type annotations into readable strings like `dict[str, list[int]?]`.

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

**Recursion:** Type arguments can themselves have type arguments (e.g., `list[dict[str, int]]`), so this calls itself via LINQ's `Select`.

**Nullable handling:** The `?` suffix is added last, after all type arguments.

---

#### `EscapeString(string str)` - String Sanitizer

**Purpose:** Makes string content safe for display in the dumped output.

```csharp
private string EscapeString(string str)
{
    return str
        .Replace("\\", "\\\\")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r")
        .Replace("\t", "\\t")
        .Replace("\"", "\\\"");
}
```

**Why needed:** Docstrings and string literals might contain newlines or quotes that would break the tree formatting.

**Order matters:** Backslash must be replaced first, otherwise you'd double-escape things.

---

#### `DumpComprehensionClause()` - List/Set/Dict Comprehension Support

Handles the `for` and `if` clauses in comprehensions:

```csharp
case ForClause forClause:
    _output.AppendLine($"{indent}{prefix}ForClause @ ...");
    _output.AppendLine($"{indent}{childPrefix}Target:");
    DumpNode(forClause.Target, depth + 2, false);
    _output.AppendLine($"{indent}{childPrefix}Iterator:");
    DumpNode(forClause.Iterator, depth + 2, true);
    break;
```

**Example Output:**
```
ListComprehension @ L10:C5
  Element:
  └─ BinaryOp: * @ L10:C6
  Clauses: [2]
  ├─ ForClause @ L10:C12
  │  Target:
  │  └─ Identifier: x
  │  Iterator:
  │  └─ FunctionCall @ L10:C17
  └─ IfClause @ L10:C25
     Condition:
     └─ ComparisonChain @ L10:C28
```

---

## 4. Dependencies

### Internal Dependencies

**Directly imports:**
- `Sharpy.Compiler.Parser.Ast` - All AST node types (Statement, Expression, Node, etc.)
- `System.Text` - For `StringBuilder`

**Indirectly depends on:**
- AST node definitions in `Parser/Ast/Node.cs`, `Statement.cs`, `Expression.cs`, `Types.cs`
- The parser that creates these AST nodes

### Connection to Upstream/Downstream Components

| Component | Relationship |
|-----------|--------------|
| **Parser** (upstream) | Creates the `Module` and AST nodes that AstDumper visualizes |
| **Semantic Analysis** (downstream) | Also consumes the same AST; AstDumper helps verify what they receive |
| **RoslynEmitter** (downstream) | Translates AST to C#; use AstDumper to debug unexpected codegen |

**Important:** `AstDumper` is read-only—it never modifies AST nodes, just reads their properties.

---

## 5. Patterns and Design Decisions

### 5.1 Visitor Pattern (Informal)

While not using the formal Visitor pattern with `Accept()` methods, `AstDumper` follows the same spirit:
- Single method (`DumpNode`) handles all node types
- Switch statement on node type (pattern matching in C#)
- Each case knows how to traverse that node's children

**Why not formal Visitor?**
- AST nodes are immutable records (by design)
- Don't want to pollute AST with dumping logic
- Simpler to maintain one dumper class than add methods to 50+ AST nodes

---

### 5.2 StringBuilder for Performance

```csharp
private readonly StringBuilder _output;
```

**Why StringBuilder?**
- String concatenation in loops is O(n²) due to immutability
- `StringBuilder` is O(n) for append operations
- Matters when dumping large ASTs with thousands of nodes

**Pattern:** Clear and reuse the same instance rather than creating new ones.

---

### 5.3 Tree-Drawing Character Choice

```csharp
var prefix = isLast ? "└─ " : "├─ ";
var childPrefix = isLast ? "   " : "│  ";
```

**Unicode box-drawing characters:**
- `├─` (branch connector)
- `└─` (last branch connector)
- `│` (vertical line continuation)

**Why these?** They create clear visual hierarchy without needing graphics. Works in any terminal.

**Alternative considered:** Plain ASCII (`|`, `+`, `-`) but less readable for deeply nested trees.

---

### 5.4 Location Tracking (`@ L{line}:C{col}`)

Every node includes its source position:
```csharp
_output.AppendLine($"{indent}{prefix}IntegerLiteral: {value} @ L{node.LineStart}:C{node.ColumnStart}");
```

**Critical for debugging:**
- Maps AST nodes back to source code
- Helps identify parser issues ("Why did this expression parse as X?")
- Useful for error reporting in later compiler phases

---

### 5.5 Depth-First Traversal

The dumper uses depth-first traversal (DFS):
```csharp
DumpNode(parent.Child1, depth + 1, false);
DumpNode(parent.Child2, depth + 1, true);
```

**Why DFS?**
- Natural recursive structure
- Produces readable nested output
- Matches how humans think about tree structures

**Contrast with BFS:** Would be harder to format with proper indentation.

---

### 5.6 Null Safety

Many optional fields are checked before dumping:
```csharp
if (funcDef.DocString != null)
{
    _output.AppendLine($"{indent}{childPrefix}DocString: ...");
}
```

**Why important:**
- Not all nodes have all fields (e.g., `return` with no value)
- Prevents `NullReferenceException`
- Makes output cleaner (don't show empty sections)

---

## 6. Debugging Tips

### Using AstDumper for Debugging

**Scenario 1: Parser producing unexpected AST**

```csharp
var parser = new Parser(sourceCode);
var module = parser.Parse();
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

Compare the output to what you expected. Look for:
- Wrong node types (parsed as `BinaryOp` instead of `FunctionCall`)
- Missing children (where did the else clause go?)
- Incorrect source locations (off-by-one errors)

---

**Scenario 2: Adding a new AST node type**

After adding `MyNewStatement` to the AST:

1. Add a case to the switch in `DumpNode`:
```csharp
case MyNewStatement myNew:
    _output.AppendLine($"{indent}{prefix}MyNewStatement @ L{node.LineStart}:C{node.ColumnStart}");
    // Dump child nodes
    break;
```

2. Test it:
```csharp
var ast = ParseCode("my_new_syntax here");
var output = new AstDumper().Dump(ast);
Assert.Contains("MyNewStatement", output);
```

---

**Scenario 3: Debugging tree drawing issues**

If the tree looks malformed:
- Check `isLast` parameter—are you calculating it correctly?
- Verify depth increments (`depth + 1` for children, `depth + 2` for sub-sections)
- Ensure `childPrefix` usage matches `prefix` (both use `isLast`)

**Example bug:**
```csharp
// Wrong: always passing true
DumpNode(children[i], depth + 1, true);

// Correct: check if it's the last element
DumpNode(children[i], depth + 1, i == children.Count - 1);
```

---

### Common Issues and Solutions

**Problem:** Output shows `Node @ L0:C0` (wrong location)

**Cause:** Parser not setting location info on nodes

**Solution:** Check parser's node construction—ensure `LineStart`/`ColumnStart` are set from tokens.

---

**Problem:** Tree looks flat (no indentation)

**Cause:** Not incrementing `depth` parameter

**Solution:**
```csharp
DumpNode(child, depth + 1, isLast);  // Correct
DumpNode(child, depth, isLast);      // Wrong - stays at same level
```

---

**Problem:** Unicode characters display as `?` or `□`

**Cause:** Terminal encoding not set to UTF-8

**Solution:**
```csharp
Console.OutputEncoding = System.Text.Encoding.UTF8;
```

---

**Problem:** New AST node falls through to default case

**Cause:** Missing case in the switch statement

**Solution:** Search for `default:` in `DumpNode` and add your specific case above it:
```csharp
case YourNewNode yourNode:
    // ... handle it
    break;

default:
    _output.AppendLine($"{indent}{prefix}{node.GetType().Name} @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```

---

## 7. Contribution Guidelines

### Adding Support for New AST Nodes

When adding a new statement or expression type to the AST:

1. **Add a case to the switch in `DumpNode()`**
   - Place it in the appropriate section (Statements vs. Expressions)
   - Follow alphabetical order within each section for maintainability

2. **Follow the existing patterns:**
   ```csharp
   case YourNewNode yourNode:
       _output.AppendLine($"{indent}{prefix}YourNewNode @ L{node.LineStart}:C{node.ColumnStart}");

       // For simple properties:
       _output.AppendLine($"{indent}{childPrefix}PropertyName: {yourNode.Property}");

       // For child nodes:
       _output.AppendLine($"{indent}{childPrefix}ChildName:");
       DumpNode(yourNode.Child, depth + 2, isLast);

       break;
   ```

3. **Test with real code:**
   - Write a snippet that uses your new syntax
   - Parse it and dump the AST
   - Verify the output is readable and complete

---

### Improving Readability

**Consider these enhancements:**

**1. Add color coding (for terminal output):**
```csharp
// Could use ANSI color codes
_output.AppendLine($"{indent}{prefix}\u001b[32m{node.GetType().Name}\u001b[0m @ ...");
```

**2. Add filtering options:**
```csharp
public string Dump(Module module, Func<Node, bool> filter)
{
    // Only dump nodes matching filter
}
```

**3. Collapsible sections:**
```csharp
public string Dump(Module module, int maxDepth)
{
    // Stop expanding after maxDepth levels
}
```

---

### Performance Optimization

If dumping large ASTs becomes slow:

**1. Pool StringBuilder instances:**
```csharp
private static readonly ObjectPool<StringBuilder> _pool = ...;
```

**2. Cache indentation strings:**
```csharp
private readonly Dictionary<int, string> _indentCache = new();
```

**3. Reduce allocations in `FormatType`:**
```csharp
// Current: allocates array for Join
var args = string.Join(", ", type.TypeArguments.Select(FormatType));

// Better: manual loop with StringBuilder
foreach (var arg in type.TypeArguments) { ... }
```

---

### Testing Considerations

While `AstDumper` is primarily a debugging tool, consider adding tests for:

**1. Completeness:**
```csharp
[Fact]
public void DumpHandlesAllNodeTypes()
{
    // Ensure no node type falls through to default case
}
```

**2. Format stability:**
```csharp
[Fact]
public void DumpFormatIsStable()
{
    var ast = ParseSampleCode();
    var output1 = new AstDumper().Dump(ast);
    var output2 = new AstDumper().Dump(ast);
    Assert.Equal(output1, output2);
}
```

**3. Location accuracy:**
```csharp
[Fact]
public void DumpIncludesSourceLocations()
{
    var output = Dump(module);
    Assert.Matches(@"@ L\d+:C\d+", output);
}
```

---

## 8. Quick Reference

### Summary

`AstDumper` is a straightforward but essential tool in the Sharpy compiler:

**What it does well:**
- Provides clear visual representation of AST structure
- Includes source location info for debugging
- Handles all AST node types comprehensively (40+ cases)
- Uses tree-drawing characters for readability

**Limitations to be aware of:**
- Output can be very large for big files
- No filtering or collapsing of sections
- Formatting can break with very deep nesting (> 20 levels)
- No syntax highlighting or color coding

**Best used for:**
- Parser development and debugging
- Understanding how code structures parse
- Creating test fixtures for expected AST shapes
- Teaching/learning compiler internals

### Common Commands

```bash
# Build compiler:
dotnet build

# Test parser:
dotnet test --filter "FullyQualifiedName~Parser"

# Compile a .spy file:
dotnet run --project src/Sharpy.Cli -- build file.spy
```

### Quick Usage

```csharp
// Dump AST for debugging (add to code):
var dumper = new AstDumper();
Console.WriteLine(dumper.Dump(module));
```

### Node Type Coverage

The switch statement handles these categories:

| Category | Example Nodes |
|----------|---------------|
| Simple Statements | `PassStatement`, `BreakStatement`, `ContinueStatement` |
| Value Statements | `ReturnStatement`, `RaiseStatement`, `AssertStatement` |
| Control Flow | `IfStatement`, `WhileStatement`, `ForStatement`, `TryStatement` |
| Declarations | `FunctionDef`, `ClassDef`, `StructDef`, `InterfaceDef`, `EnumDef` |
| Variables | `VariableDeclaration`, `Assignment` |
| Imports | `ImportStatement`, `FromImportStatement` |
| Literals | `IntegerLiteral`, `FloatLiteral`, `StringLiteral`, `BooleanLiteral`, etc. |
| Collections | `ListLiteral`, `DictLiteral`, `SetLiteral`, `TupleLiteral` |
| Comprehensions | `ListComprehension`, `SetComprehension`, `DictComprehension` |
| Operators | `UnaryOp`, `BinaryOp`, `ComparisonChain` |
| Access | `Identifier`, `MemberAccess`, `IndexAccess`, `SliceAccess`, `FunctionCall` |
| Type Operations | `TypeCast`, `TypeCoercion`, `TypeCheck` |
| Misc | `LambdaExpression`, `ConditionalExpression`, `Parenthesized`, `FStringLiteral` |

---

When in doubt about what the parser produced, dump the AST first—it'll save you hours of guessing!
