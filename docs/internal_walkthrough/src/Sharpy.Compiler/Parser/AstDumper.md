# Walkthrough: AstDumper.cs

**Source File**: `src/Sharpy.Compiler/Parser/AstDumper.cs`

---

## 1. Overview

The **AstDumper** is a debugging and visualization utility for the Sharpy compiler. Its primary purpose is to convert Abstract Syntax Tree (AST) nodes into a human-readable, hierarchical text format that developers can easily understand.

**Why does this exist?**
- **Debugging**: When the parser produces an AST, developers need to verify it's correct
- **Testing**: Tests can compare expected AST structures against actual output
- **Documentation**: Provides a clear view of how Sharpy code maps to AST nodes
- **Learning**: Helps newcomers understand how the parser interprets code

**Key Role in the Project:**
This is a **developer tool**, not part of the compilation pipeline. It's used for:
- Writing and debugging parser tests
- Understanding complex AST structures
- Troubleshooting why code doesn't compile as expected
- Documenting AST node relationships

---

## 2. Class Structure

### AstDumper Class

```csharp
public class AstDumper
{
    private readonly StringBuilder _output;
    private const string IndentUnit = "  ";
}
```

**Design:**
- **Stateful**: Uses a `StringBuilder` to accumulate output
- **Single-use pattern**: Create instance → call `Dump()` → get result
- **Simple API**: One public method (`Dump`) with several private helper methods

**Why StringBuilder?**
String concatenation in loops is inefficient in C#. `StringBuilder` provides mutable, efficient string building for the potentially large output this class generates.

---

## 3. Key Methods

### 3.1. `Dump(Module module)` - Entry Point

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

**What it does:**
- Clears any previous output (enables reuse)
- Outputs the root `Module` node with its source location
- Shows the module's docstring if present
- Recursively dumps all statements in the module body

**Parameters:**
- `module`: The root AST node representing an entire Sharpy source file

**Returns:**
- A formatted string representation of the entire AST

**Location Information:**
- Every node shows `@ L{line}:C{column}` to trace back to source code
- Critical for debugging parser issues

---

### 3.2. `DumpNode(Node node, int depth, bool isLast)` - The Workhorse

This is the **core recursive method** that handles every AST node type. It's a massive switch statement (lines 42-689) covering all possible AST nodes.

```csharp
private void DumpNode(Node node, int depth, bool isLast)
{
    var indent = new string(' ', depth * IndentUnit.Length);
    var prefix = isLast ? "└─ " : "├─ ";
    var childPrefix = isLast ? "   " : "│  ";
    
    switch (node)
    {
        // ... handles 50+ AST node types
    }
}
```

**Parameters:**
- `node`: The AST node to dump
- `depth`: Indentation level (0 = root, increases for nested nodes)
- `isLast`: Whether this is the last child of its parent (affects tree drawing)

**Tree Drawing Characters:**
- `├─` (prefix): "Not last child" - shows more siblings follow
- `└─` (prefix): "Last child" - end of this branch
- `│  ` (childPrefix): "Continuation line" - shows parent has more children
- `   ` (childPrefix): "Empty continuation" - parent is done

**Example Output:**
```
Module @ L1:C1
  Body: [2 statement(s)]
  ├─ VariableDeclaration @ L1:C1
  │  Name: x
  │  Type:
  │     └─ int @ L1:C4
  └─ ExpressionStatement @ L2:C1
     └─ FunctionCall @ L2:C1
```

**Design Pattern:**
This uses the **Visitor Pattern** without formal visitor infrastructure. Each `case` in the switch acts as a specialized visitor for that node type.

---

### 3.3. Statement Handling Examples

#### Simple Statements (Pass, Break, Continue)

```csharp
case PassStatement:
    _output.AppendLine($"{indent}{prefix}PassStatement @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```

These are **leaf nodes** with no children, so they just output their name and location.

#### Complex Statements (If/Elif/Else)

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
    // ... elif and else handling
```

**Key Points:**
- Shows the condition (`Test`)
- Shows the `then` body (list of statements)
- Handles optional `elif` clauses (can have multiple)
- Handles optional `else` body
- Each nested element increases depth by 1

**Why depth + 2?**
Depth increases by 2 for each level because:
- `depth + 1`: Reserved for labels like "Test:", "Body:", etc.
- `depth + 2`: Actual content under those labels

---

### 3.4. Expression Handling Examples

#### Literals

```csharp
case IntegerLiteral intLit:
    var intSuffix = intLit.Suffix != null ? $" ({intLit.Suffix})" : "";
    _output.AppendLine($"{indent}{prefix}IntegerLiteral: {intLit.Value}{intSuffix} @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```

Shows the value directly (e.g., `IntegerLiteral: 42 @ L1:C5`). Optional suffixes like `u64` are shown in parentheses.

#### Binary Operations

```csharp
case BinaryOp binaryOp:
    _output.AppendLine($"{indent}{prefix}BinaryOp: {binaryOp.Operator} @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Left:");
    DumpNode(binaryOp.Left, depth + 2, false);
    _output.AppendLine($"{indent}{childPrefix}Right:");
    DumpNode(binaryOp.Right, depth + 2, true);
    break;
```

**Structure:**
```
BinaryOp: + @ L1:C3
  Left:
    └─ IntegerLiteral: 1 @ L1:C1
  Right:
    └─ IntegerLiteral: 2 @ L1:C5
```

This clearly shows the operator and both operands.

#### F-Strings (Complex Example)

```csharp
case FStringLiteral fstrLit:
    _output.AppendLine($"{indent}{prefix}FStringLiteral @ L{node.LineStart}:C{node.ColumnStart}");
    _output.AppendLine($"{indent}{childPrefix}Parts: [{fstrLit.Parts.Count}]");
    for (int i = 0; i < fstrLit.Parts.Count; i++)
    {
        var part = fstrLit.Parts[i];
        // ... handle text vs expression parts
    }
```

F-strings are **composite nodes** containing both static text and embedded expressions. Each part is dumped separately to show the structure.

---

### 3.5. Helper Methods

#### `DumpParameter(Parameter param, int depth, bool isLast)`

```csharp
private void DumpParameter(Parameter param, int depth, bool isLast)
{
    var indent = new string(' ', depth * IndentUnit.Length);
    var prefix = isLast ? "└─ " : "├─ ";
    
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

**Purpose:** Specialized dumping for function/lambda parameters

**Output Example:**
```
Parameter: x @ L1:C10 : int
Parameter: y @ L1:C17 : str = 
  └─ StringLiteral: "default" @ L1:C23
```

Shows:
1. Parameter name and location
2. Type annotation (if present)
3. Default value (if present) as a nested node

---

#### `DumpTypeAnnotation(TypeAnnotation type, int depth, bool isLast)`

```csharp
private void DumpTypeAnnotation(TypeAnnotation type, int depth, bool isLast)
{
    var indent = new string(' ', depth * IndentUnit.Length);
    var prefix = isLast ? "└─ " : "├─ ";
    _output.AppendLine($"{indent}{prefix}{FormatType(type)} @ L{type.LineStart}:C{type.ColumnStart}");
}
```

**Purpose:** Display type annotations in a compact format

Uses `FormatType()` to show generics and nullability inline.

---

#### `FormatType(TypeAnnotation type)`

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

**Purpose:** Format types inline with generics and nullability

**Examples:**
- `int` → `"int"`
- `list[int]` → `"list[int]"`
- `dict[str, int]?` → `"dict[str, int]?"`
- `list[dict[str, list[int]]]` → `"list[dict[str, list[int]]]"` (recursive)

**Recursive:** Handles nested generic types through LINQ's `Select(FormatType)`

---

#### `EscapeString(string str)`

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

**Purpose:** Make string content readable in output

Converts special characters to their escape sequences so:
- Newlines appear as `\n` instead of actual line breaks
- Quotes don't break the output format
- Backslashes are properly escaped

**Example:**
```python
"Hello\nWorld"  →  "Hello\\nWorld"
```

---

#### `DumpComprehensionClause(ComprehensionClause clause, int depth, bool isLast)`

```csharp
private void DumpComprehensionClause(ComprehensionClause clause, int depth, bool isLast)
{
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
    }
}
```

**Purpose:** Handle list/set/dict comprehension clauses

**Example Comprehension:**
```python
[x * 2 for x in range(10) if x > 5]
```

**Would show:**
```
ListComprehension @ L1:C1
  Element:
    └─ BinaryOp: * @ L1:C2
       ...
  Clauses: [2]
  ├─ ForClause @ L1:C8
  │  Target:
  │     └─ Identifier: x @ L1:C12
  │  Iterator:
  │     └─ FunctionCall @ L1:C17
  └─ IfClause @ L1:C28
     Condition:
        └─ ComparisonChain @ L1:C31
```

---

## 4. Dependencies

### Internal Dependencies

**From `Sharpy.Compiler.Parser.Ast`:**
- `Module`: Root AST node
- `Node`: Base class for all AST nodes
- All statement types: `IfStatement`, `WhileStatement`, `FunctionDef`, etc.
- All expression types: `BinaryOp`, `IntegerLiteral`, `FunctionCall`, etc.
- Supporting types: `Parameter`, `TypeAnnotation`, `ComprehensionClause`, etc.

**From .NET Framework:**
- `System.Text.StringBuilder`: For efficient string building
- `System.Linq`: For recursive type formatting

### No External Dependencies

This class is **self-contained** - it doesn't call into:
- The Lexer (works on already-parsed AST)
- The Semantic Analyzer (just dumps structure, doesn't validate)
- The Code Generator (pure visualization)

---

## 5. Patterns and Design Decisions

### 5.1. Visitor Pattern (Informal)

The giant `switch` statement in `DumpNode()` is an **informal Visitor Pattern**. 

**Why not use a formal visitor?**
- Simpler to maintain for this single use case
- All dumping logic in one file
- No need to modify AST nodes (they don't need `Accept()` methods)
- Easy to add new node types (just add a case)

**Trade-off:**
- ✅ Simpler, more readable
- ❌ Not extensible (can't add new "visitors" without modifying this class)
- ✅ But we only need one dumper, so this is fine

### 5.2. Tree Drawing

The tree visualization uses **box-drawing characters**:
```
├─  (not last)
└─  (last)
│   (continuation)
```

This makes the tree structure **instantly recognizable** and shows parent-child relationships clearly.

**Algorithm:**
```csharp
var prefix = isLast ? "└─ " : "├─ ";
var childPrefix = isLast ? "   " : "│  ";
```

- `isLast`: Determines if this node is the last child
- Children inherit `childPrefix` to maintain vertical lines

### 5.3. Depth Management

Depth increases by **2** for nested content:
```csharp
DumpNode(ifStmt.Test, depth + 2, false);
```

**Why?**
- `depth`: Current indentation
- `depth + 1`: Labels ("Test:", "Body:", etc.)
- `depth + 2`: Actual content

This creates visual separation between labels and content.

### 5.4. Location Tracking

Every node shows `@ L{line}:C{column}`:

```csharp
$"{indent}{prefix}IfStatement @ L{node.LineStart}:C{node.ColumnStart}"
```

**Purpose:**
- Trace AST nodes back to source code
- Debug parser issues (wrong line? wrong column? wrong node?)
- Essential for error reporting later in the pipeline

### 5.5. Compact vs. Expanded Formatting

**Compact** (single line):
```csharp
_output.AppendLine($"{indent}{prefix}IntegerLiteral: {intLit.Value} @ L1:C5");
```

**Expanded** (multiple lines):
```csharp
_output.AppendLine($"{indent}{prefix}BinaryOp: {binaryOp.Operator}");
_output.AppendLine($"{indent}{childPrefix}Left:");
DumpNode(binaryOp.Left, depth + 2, false);
```

**Rule:** 
- Leaf nodes (no children) → compact
- Composite nodes → expanded with labeled children

---

## 6. Debugging Tips

### 6.1. Reading Dumper Output

When you see output like this:
```
Module @ L1:C1
  Body: [1 statement(s)]
  └─ ExpressionStatement @ L3:C5
     └─ BinaryOp: + @ L3:C7
        Left:
          └─ IntegerLiteral: 1 @ L3:C5
        Right:
          └─ IntegerLiteral: 2 @ L3:C9
```

**How to read it:**
1. **Module** starts at line 1, column 1
2. It has **1 statement** in its body
3. That statement is an **ExpressionStatement** at line 3, column 5
4. The expression is a **BinaryOp** (addition) at line 3, column 7
5. Left operand: literal `1` at L3:C5
6. Right operand: literal `2` at L3:C9

**Pro tip:** Compare the locations with your source code to verify the parser got it right.

### 6.2. Common Issues

**Problem: Wrong tree structure**
- **Symptom:** Nodes appear in wrong order or nesting
- **Debug:** Check the parser's `Parse*` methods
- **Example:** If `BinaryOp` left/right are swapped, the parser likely built the AST incorrectly

**Problem: Missing nodes**
- **Symptom:** Expected node doesn't appear in dump
- **Debug:** Check if the parser actually creates that node
- **Example:** If a function parameter is missing, the parser might have skipped it

**Problem: Location information is wrong**
- **Symptom:** `@ L{line}:C{column}` doesn't match source
- **Debug:** Check how the parser sets `LineStart`/`ColumnStart`
- **Common cause:** Parser doesn't capture token position correctly

### 6.3. Adding Dump Support for New AST Nodes

When you add a new AST node type, you **must** add a case to `DumpNode()`:

```csharp
case YourNewNode yourNode:
    _output.AppendLine($"{indent}{prefix}YourNewNode @ L{node.LineStart}:C{node.ColumnStart}");
    // Dump properties
    if (yourNode.SomeChild != null)
    {
        _output.AppendLine($"{indent}{childPrefix}SomeChild:");
        DumpNode(yourNode.SomeChild, depth + 2, true);
    }
    break;
```

**If you forget:**
You'll hit the default case:
```csharp
default:
    _output.AppendLine($"{indent}{prefix}{node.GetType().Name} @ L{node.LineStart}:C{node.ColumnStart}");
    break;
```

This shows the type name but **no details** - a clear signal you need to add a proper case.

### 6.4. Using in Tests

**Typical test pattern:**

```csharp
[Fact]
public void TestIfStatement()
{
    var source = @"
if x > 0:
    print('positive')
";
    var parser = new Parser(source);
    var module = parser.Parse();
    
    // Dump for debugging
    var dumper = new AstDumper();
    var dump = dumper.Dump(module);
    Console.WriteLine(dump);  // See the structure
    
    // Now assert on the structure
    Assert.Single(module.Body);
    var ifStmt = Assert.IsType<IfStatement>(module.Body[0]);
    // ...
}
```

**When test fails:**
1. Look at the dumped tree
2. Compare to what you expected
3. Identify the mismatch
4. Fix the parser or the test

---

## 7. Contribution Guidelines

### 7.1. When to Modify This File

**Add support for new AST nodes:**
- If you add a new statement or expression type
- Add a `case` in `DumpNode()` to handle it
- Follow existing patterns for similar nodes

**Improve formatting:**
- If the current output is hard to read for certain nodes
- Consider more compact or more expanded formats
- Keep consistency with existing style

**Add specialized helpers:**
- If a new category of nodes needs special handling (like parameters or comprehensions)
- Add a private helper method (e.g., `DumpYourThing()`)

### 7.2. Style Guidelines

**Follow these patterns:**

1. **Always show location:**
   ```csharp
   @ L{node.LineStart}:C{node.ColumnStart}
   ```

2. **Show counts for collections:**
   ```csharp
   Body: [{body.Count} statement(s)]
   ```

3. **Label nested content:**
   ```csharp
   _output.AppendLine($"{indent}{childPrefix}Test:");
   DumpNode(test, depth + 2, ...);
   ```

4. **Use proper tree characters:**
   - `├─` for non-last children
   - `└─` for last children
   - `│  ` for continuation
   - `   ` for end of branch

5. **Escape strings:**
   Always use `EscapeString()` for string content:
   ```csharp
   $"DocString: \"{EscapeString(docString)}\""
   ```

### 7.3. Testing Your Changes

After modifying the dumper:

1. **Run existing tests:**
   ```bash
   dotnet test src/Sharpy.Compiler.Tests/Parser/
   ```

2. **Manually test with sample code:**
   ```csharp
   var source = "your test code";
   var parser = new Parser(source);
   var module = parser.Parse();
   var dumper = new AstDumper();
   Console.WriteLine(dumper.Dump(module));
   ```

3. **Verify readability:**
   - Is the structure clear?
   - Are tree lines aligned?
   - Is location information accurate?

### 7.4. Common Mistakes to Avoid

**❌ Forgetting `isLast` parameter:**
```csharp
// Wrong - all children marked as "not last"
for (int i = 0; i < items.Count; i++)
{
    DumpNode(items[i], depth + 2, false);  // ❌ Wrong
}

// Correct - last child marked appropriately
for (int i = 0; i < items.Count; i++)
{
    DumpNode(items[i], depth + 2, i == items.Count - 1);  // ✅ Correct
}
```

**❌ Wrong depth calculation:**
```csharp
// Wrong - depth should increase
DumpNode(child, depth, true);  // ❌ Same depth

// Correct
DumpNode(child, depth + 2, true);  // ✅ Nested deeper
```

**❌ Not escaping strings:**
```csharp
// Wrong - newlines will break formatting
$"DocString: \"{docString}\""  // ❌ No escaping

// Correct
$"DocString: \"{EscapeString(docString)}\""  // ✅ Escaped
```

### 7.5. Performance Considerations

**This is a debug tool**, not performance-critical. However:

- ✅ Using `StringBuilder` (efficient string building)
- ✅ Single-pass traversal (no redundant walks)
- ⚠️ For huge ASTs (10,000+ nodes), output may be large
- ⚠️ String escaping allocates - acceptable for debug purposes

**Don't optimize unless:**
- You're dumping massive ASTs in tests
- Test suite is slow due to dumping
- Then consider: lazy dumping, partial dumps, or caching

---

## 8. Real-World Usage Examples

### 8.1. Debugging a Parser Bug

**Scenario:** Parser crashes on `if` statement with `elif`

**Steps:**
```csharp
var source = @"
if x > 0:
    print('positive')
elif x < 0:
    print('negative')
";

try {
    var parser = new Parser(source);
    var module = parser.Parse();
    
    var dumper = new AstDumper();
    Console.WriteLine(dumper.Dump(module));
} catch (Exception ex) {
    Console.WriteLine($"Parser failed: {ex.Message}");
}
```

**Look for:**
- Are `IfStatement` and `ElifClauses` present?
- Are the test conditions correct?
- Are bodies properly populated?

### 8.2. Verifying Complex Expressions

**Scenario:** Ensure operator precedence is correct

**Code:**
```python
x = 1 + 2 * 3
```

**Expected AST:**
```
Assignment
  Target: x
  Value:
    BinaryOp: +
      Left: IntegerLiteral: 1
      Right:
        BinaryOp: *
          Left: IntegerLiteral: 2
          Right: IntegerLiteral: 3
```

If multiplication isn't nested under addition, precedence is wrong!

### 8.3. Understanding F-String Parsing

**Code:**
```python
message = f"Hello, {name}! You are {age} years old."
```

**Dumped:**
```
FStringLiteral @ L1:C11
  Parts: [4]
  ├─ Text: "Hello, "
  ├─ Expression:
  │  └─ Identifier: name
  ├─ Text: "! You are "
  ├─ Expression:
  │  └─ Identifier: age
  └─ Text: " years old."
```

Shows exactly how the f-string was parsed into alternating text/expression parts.

---

## 9. Summary

**AstDumper** is a simple but essential utility for working with the Sharpy compiler's AST. It transforms complex tree structures into readable, debuggable text that shows:

- **What** nodes exist (type and properties)
- **Where** they came from (line/column in source)
- **How** they're nested (parent-child relationships)

**Key takeaways for newcomers:**

1. **It's a debug tool** - use it liberally when writing tests or debugging
2. **It's comprehensive** - handles all 50+ AST node types
3. **It's simple** - one switch statement, recursive tree walking
4. **It's readable** - tree-drawing characters make structure obvious
5. **It's maintainable** - adding new nodes is just one new `case`

When in doubt about what the parser produced, **dump it and look**!
