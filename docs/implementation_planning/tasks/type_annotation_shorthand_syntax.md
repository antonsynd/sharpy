# Task List: Type Annotation Shorthand Syntax

**Feature:** Alternative type annotation syntax (syntactic sugar) for built-in collection types  
**Status:** Proposed  
**Created:** 2025-01-17  
**Related Specs:** `grammar.ebnf.txt`, `type_annotations.md`, `collection_types.md`

---

## Overview

Add Swift/TypeScript-inspired shorthand syntax for collection type annotations:

| Shorthand | Canonical | Notes |
|-----------|-----------|-------|
| `()` | `tuple[()]` | Empty/unit tuple |
| `(T)` | `tuple[T]` | Single-element tuple (trailing comma optional) |
| `(T,)` | `tuple[T]` | Single-element tuple (canonical form) |
| `(T, U)` | `tuple[T, U]` | Multi-element tuple |
| `(T, U,)` | `tuple[T, U]` | Trailing comma allowed |
| `(T) -> U` | function type | Unambiguous due to `->` |
| `[T]` | `list[T]` | List shorthand |
| `{T}` | `set[T]` | Set shorthand |
| `{K: V}` | `dict[K, V]` | Dict shorthand |
| `T[]` | array | .NET `T[]` (postfix) |

**Key Design Decisions:**
- Both syntaxes valid; parser normalizes to canonical AST representation
- In type context, `(T)` is unambiguously a single-element tuple (no grouping ambiguity)
- Trailing comma optional for all tuple arities (canonical: with comma for single-element)
- Nullable suffix `?` works with all forms: `[int]?`, `(str, int)?`, `{int}?`

---

# Phase TA.1: Language Specification Updates

**Goal**: Document the shorthand syntax formally in the language specification.

---

## Task TA.1.1: Update Grammar EBNF

📁 **Files**: `docs/language_specification/grammar.ebnf.txt`

**Changes Required:**

Update the `TYPES` section to include shorthand forms:

```ebnf
(* Current *)
primary_type    ::= qualified_name [ type_args ]
                  | tuple_type
                  | function_type
                  | 'auto'

(* Updated *)
primary_type    ::= qualified_name [ type_args ]
                  | tuple_type
                  | function_type
                  | list_type_shorthand
                  | set_type_shorthand
                  | dict_type_shorthand
                  | array_type_shorthand
                  | tuple_type_shorthand
                  | 'auto'

(* New shorthand productions *)
list_type_shorthand  ::= '[' type_expr ']'
set_type_shorthand   ::= '{' type_expr '}'
dict_type_shorthand  ::= '{' type_expr ':' type_expr '}'
array_type_shorthand ::= primary_type '[' ']'

(* Tuple shorthand - distinct from function_type by absence of '->' *)
tuple_type_shorthand ::= '(' ')'                                      (* empty tuple *)
                       | '(' type_expr ',' ')'                        (* single with comma *)
                       | '(' type_expr ')'                            (* single without comma *)
                       | '(' type_expr ',' type_list ')'              (* multi-element *)
```

**Add disambiguation notes:**

```ebnf
(*
AMBIGUITY: Tuple Shorthand vs Function Type
  (int)        -> tuple[int] (no arrow follows)
  (int) -> str -> function type (arrow follows)
Resolution: Look ahead for '->' to distinguish

AMBIGUITY: Set Shorthand vs Dict Shorthand  
  {int}        -> set[int]
  {str: int}   -> dict[str, int]
Resolution: Presence of ':' determines dict vs set (same as literals)

AMBIGUITY: Array Shorthand vs Empty Subscription
  int[]        -> array type (in type context)
  items[]      -> would be invalid subscription (in expression context)
Resolution: Only valid in type annotation context
*)
```

**Acceptance Criteria:**
- [ ] Grammar compiles (no ambiguities in EBNF)
- [ ] All shorthand forms documented with examples
- [ ] Disambiguation rules clearly specified
- [ ] Trailing comma rules documented for tuples

---

## Task TA.1.2: Create Type Annotation Shorthand Specification

🆕 **New File**: `docs/language_specification/type_annotation_shorthand.md`

**Content outline:**

```markdown
# Type Annotation Shorthand Syntax

Sharpy provides shorthand syntax for common collection type annotations...

## List Shorthand
[T] is equivalent to list[T]

## Set Shorthand  
{T} is equivalent to set[T]

## Dict Shorthand
{K: V} is equivalent to dict[K, V]

## Tuple Shorthand
() is the empty/unit tuple
(T) or (T,) is a single-element tuple
(T, U) is a two-element tuple

## Array Shorthand
T[] is equivalent to System.Array of T

## Nullability
All shorthand forms support the ? suffix...

## Nesting
Shorthand forms can be nested...

## Formatting Conventions
- Single-element tuples: canonical form is (T,) with trailing comma
- Multi-element tuples: trailing comma optional, recommended for multi-line
```

**Acceptance Criteria:**
- [ ] All shorthand forms documented with examples
- [ ] Shows equivalence to canonical forms
- [ ] Includes nullable examples
- [ ] Includes nested examples
- [ ] Documents formatting conventions/preferences

---

## Task TA.1.3: Update Existing Type Annotation Documentation

📁 **Files**: `docs/language_specification/type_annotations.md`, `docs/language_specification/collection_types.md`

**Changes:**
- Add cross-references to the new shorthand specification
- Update examples to show both forms where appropriate
- Note that both syntaxes produce identical AST

**Acceptance Criteria:**
- [ ] Cross-references added
- [ ] No conflicting information between docs

---

# Phase TA.2: Parser Implementation

**Goal**: Implement parsing of shorthand type annotation syntax.

---

## Task TA.2.1: Extend Type Annotation Parsing

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`

**Implementation approach:**

Modify `ParseTypeAnnotation()` to handle shorthand forms. The key insight is that these are only valid in **type annotation context**, so we know we're parsing types, not expressions.

```csharp
private TypeAnnotation ParseTypeAnnotation()
{
    var startLine = Current.Line;
    var startColumn = Current.Column;
    
    // Check for shorthand forms first
    if (Check(TokenType.LeftBracket))
    {
        // [T] list shorthand
        return ParseListTypeShorthand(startLine, startColumn);
    }
    
    if (Check(TokenType.LeftBrace))
    {
        // {T} set or {K: V} dict shorthand
        return ParseSetOrDictTypeShorthand(startLine, startColumn);
    }
    
    if (Check(TokenType.LeftParen))
    {
        // () empty tuple, (T) single tuple, (T, U) tuple, or (T) -> U function
        return ParseTupleOrFunctionTypeShorthand(startLine, startColumn);
    }
    
    // Existing logic for identifier-based types...
    var name = ParseQualifiedName();
    
    // Check for array shorthand: T[]
    if (Check(TokenType.LeftBracket) && CheckNext(TokenType.RightBracket))
    {
        return ParseArrayTypeShorthand(name, startLine, startColumn);
    }
    
    // ... rest of existing logic
}
```

**Key parsing methods to add:**

1. `ParseListTypeShorthand()` - handles `[T]`
2. `ParseSetOrDictTypeShorthand()` - handles `{T}` vs `{K: V}`
3. `ParseTupleOrFunctionTypeShorthand()` - handles `()`, `(T)`, `(T,)`, `(T, U)`, `(T) -> U`
4. `ParseArrayTypeShorthand()` - handles `T[]`

**Critical: AST Normalization**

All shorthand forms must produce the **same AST** as their canonical equivalents:

```csharp
// [int] produces same AST as list[int]:
new TypeAnnotation 
{ 
    Name = "list", 
    TypeArguments = new List<TypeAnnotation> { new TypeAnnotation { Name = "int" } }
}

// (int, str) produces same AST as tuple[int, str]:
new TupleType
{
    ElementTypes = new List<TypeAnnotation> 
    { 
        new TypeAnnotation { Name = "int" },
        new TypeAnnotation { Name = "str" }
    }
}
```

**Acceptance Criteria:**
- [ ] All shorthand forms parse correctly
- [ ] Shorthand produces identical AST to canonical form
- [ ] Nullable suffix works: `[int]?`, `{str}?`, `(int, str)?`
- [ ] Nested shorthand works: `[[int]]`, `{[int]}`, `[(int, str)]`
- [ ] Function types still work: `(int) -> str`, `([int]) -> {str: int}`
- [ ] Error messages are clear for malformed shorthand

---

## Task TA.2.2: Handle Edge Cases and Ambiguities

📁 **Files**: `src/Sharpy.Compiler/Parser/Parser.cs`

**Edge cases to handle:**

1. **Empty containers in type position:**
   ```python
   x: []      # ERROR: element type required
   x: {}      # ERROR: Could be empty set or dict - require explicit type
   x: ()      # OK: empty tuple / unit type
   ```

2. **Distinguish tuple from function type:**
   ```python
   x: (int)        # tuple[int] - no arrow
   x: (int) -> str # function type - has arrow
   ```
   Implementation: After parsing `(type_list)`, look ahead for `->`

3. **Array shorthand only on simple types:**
   ```python
   x: int[]           # OK
   x: list[int][]     # OK (array of lists)
   x: (int, str)[]    # OK (array of tuples)
   x: [int][]         # OK (array of lists via shorthand)
   ```

4. **Nested braces:**
   ```python
   x: {{int}}         # set[set[int]]
   x: {{str: int}}    # set[dict[str, int]] - outer is set, inner is dict
   x: {str: {int}}    # dict[str, set[int]]
   ```

**Acceptance Criteria:**
- [ ] Empty `[]` and `{}` produce clear error messages
- [ ] Tuple vs function type disambiguation works
- [ ] All nesting combinations parse correctly
- [ ] Postfix `[]` binds correctly with precedence

---

# Phase TA.3: Parser Tests

**Goal**: Comprehensive test coverage for shorthand syntax parsing.

---

## Task TA.3.1: Unit Tests for Shorthand Parsing

📁 **Files**: `src/Sharpy.Compiler.Tests/Parser/TypeAnnotationShorthandTests.cs`

🆕 **New test file** with regions:

```csharp
namespace Sharpy.Compiler.Tests.Parser;

public class TypeAnnotationShorthandTests : ParserTestBase
{
    #region List Shorthand [T]
    
    [Fact]
    public void ParseListShorthand_SimpleType()
    {
        // x: [int]
        var annotation = ParseTypeAnnotation("[int]");
        annotation.Name.Should().Be("list");
        annotation.TypeArguments.Should().HaveCount(1);
        annotation.TypeArguments[0].Name.Should().Be("int");
    }
    
    [Fact]
    public void ParseListShorthand_Nested()
    {
        // x: [[str]]
        var annotation = ParseTypeAnnotation("[[str]]");
        annotation.Name.Should().Be("list");
        annotation.TypeArguments[0].Name.Should().Be("list");
        annotation.TypeArguments[0].TypeArguments[0].Name.Should().Be("str");
    }
    
    [Fact]
    public void ParseListShorthand_Nullable()
    {
        // x: [int]?
        var annotation = ParseTypeAnnotation("[int]?");
        annotation.Name.Should().Be("list");
        annotation.IsNullable.Should().BeTrue();
    }
    
    #endregion
    
    #region Set Shorthand {T}
    
    [Fact]
    public void ParseSetShorthand_SimpleType()
    {
        // x: {int}
        var annotation = ParseTypeAnnotation("{int}");
        annotation.Name.Should().Be("set");
        annotation.TypeArguments.Should().HaveCount(1);
    }
    
    #endregion
    
    #region Dict Shorthand {K: V}
    
    [Fact]
    public void ParseDictShorthand_SimpleTypes()
    {
        // x: {str: int}
        var annotation = ParseTypeAnnotation("{str: int}");
        annotation.Name.Should().Be("dict");
        annotation.TypeArguments.Should().HaveCount(2);
        annotation.TypeArguments[0].Name.Should().Be("str");
        annotation.TypeArguments[1].Name.Should().Be("int");
    }
    
    #endregion
    
    #region Tuple Shorthand (T, U)
    
    [Fact]
    public void ParseTupleShorthand_Empty()
    {
        // x: ()
        var tuple = ParseTupleType("()");
        tuple.ElementTypes.Should().BeEmpty();
    }
    
    [Fact]
    public void ParseTupleShorthand_SingleElement_NoComma()
    {
        // x: (int)
        var tuple = ParseTupleType("(int)");
        tuple.ElementTypes.Should().HaveCount(1);
        tuple.ElementTypes[0].Name.Should().Be("int");
    }
    
    [Fact]
    public void ParseTupleShorthand_SingleElement_WithComma()
    {
        // x: (int,)
        var tuple = ParseTupleType("(int,)");
        tuple.ElementTypes.Should().HaveCount(1);
    }
    
    [Fact]
    public void ParseTupleShorthand_MultiElement()
    {
        // x: (int, str, bool)
        var tuple = ParseTupleType("(int, str, bool)");
        tuple.ElementTypes.Should().HaveCount(3);
    }
    
    [Fact]
    public void ParseTupleShorthand_TrailingComma()
    {
        // x: (int, str,)
        var tuple = ParseTupleType("(int, str,)");
        tuple.ElementTypes.Should().HaveCount(2);
    }
    
    #endregion
    
    #region Array Shorthand T[]
    
    [Fact]
    public void ParseArrayShorthand_SimpleType()
    {
        // x: int[]
        var annotation = ParseTypeAnnotation("int[]");
        // Verify it's an array type (implementation detail TBD)
    }
    
    [Fact]
    public void ParseArrayShorthand_OfList()
    {
        // x: [int][]
        var annotation = ParseTypeAnnotation("[int][]");
        // Array of list[int]
    }
    
    #endregion
    
    #region Function Types (ensure no regression)
    
    [Fact]
    public void ParseFunctionType_SingleParam()
    {
        // x: (int) -> str
        var funcType = ParseFunctionType("(int) -> str");
        funcType.ParameterTypes.Should().HaveCount(1);
        funcType.ReturnType.Name.Should().Be("str");
    }
    
    [Fact]
    public void ParseFunctionType_WithShorthandTypes()
    {
        // x: ([int]) -> {str: bool}
        var funcType = ParseFunctionType("([int]) -> {str: bool}");
        funcType.ParameterTypes[0].Name.Should().Be("list");
        funcType.ReturnType.Name.Should().Be("dict");
    }
    
    #endregion
    
    #region AST Equivalence
    
    [Fact]
    public void ShorthandProducesSameAST_List()
    {
        var shorthand = ParseTypeAnnotation("[int]");
        var canonical = ParseTypeAnnotation("list[int]");
        
        shorthand.Name.Should().Be(canonical.Name);
        shorthand.TypeArguments.Should().BeEquivalentTo(canonical.TypeArguments);
    }
    
    [Fact]
    public void ShorthandProducesSameAST_Dict()
    {
        var shorthand = ParseTypeAnnotation("{str: int}");
        var canonical = ParseTypeAnnotation("dict[str, int]");
        
        shorthand.Name.Should().Be(canonical.Name);
        shorthand.TypeArguments.Should().BeEquivalentTo(canonical.TypeArguments);
    }
    
    // ... more equivalence tests
    
    #endregion
    
    #region Error Cases
    
    [Fact]
    public void ParseError_EmptyListShorthand()
    {
        // x: []
        var ex = Assert.Throws<ParseException>(() => ParseTypeAnnotation("[]"));
        ex.Message.Should().Contain("element type");
    }
    
    [Fact]
    public void ParseError_EmptySetOrDict()
    {
        // x: {}
        var ex = Assert.Throws<ParseException>(() => ParseTypeAnnotation("{}"));
        ex.Message.Should().Contain("type");
    }
    
    #endregion
}
```

**Acceptance Criteria:**
- [ ] All shorthand forms have positive tests
- [ ] AST equivalence verified for all forms
- [ ] Error cases tested with clear messages
- [ ] Nullable combinations tested
- [ ] Nesting combinations tested
- [ ] Function type distinction tested

---

## Task TA.3.2: Integration Tests for Shorthand in Context

📁 **Files**: `src/Sharpy.Compiler.Tests/Parser/ParserTests.cs` (add to existing)

Add tests that use shorthand syntax in realistic contexts:

```csharp
#region Type Annotation Shorthand in Context

[Fact]
public void ParseFunctionDef_WithShorthandTypes()
{
    var source = @"
def process(items: [int], mapping: {str: int}) -> (bool, str):
    pass
";
    var module = Parse(source);
    var func = module.Body[0].Should().BeOfType<FunctionDef>().Subject;
    
    func.Parameters[0].TypeAnnotation.Name.Should().Be("list");
    func.Parameters[1].TypeAnnotation.Name.Should().Be("dict");
    func.ReturnType.Should().BeOfType<TupleType>();
}

[Fact]
public void ParseClassDef_WithShorthandFieldTypes()
{
    var source = @"
class Container:
    items: [str]
    lookup: {str: int}
    pair: (int, int)
";
    var module = Parse(source);
    // ... assertions
}

[Fact]
public void ParseVariableDecl_WithShorthandType()
{
    var source = "x: [int] = [1, 2, 3]";
    var module = Parse(source);
    // ... assertions
}

#endregion
```

**Acceptance Criteria:**
- [ ] Shorthand works in function parameters
- [ ] Shorthand works in function return types
- [ ] Shorthand works in class fields
- [ ] Shorthand works in variable declarations
- [ ] Shorthand works in generic constraints (if applicable)

---

# Phase TA.4: File-Based Integration Test Variations

**Goal**: Create variations of existing integration tests using shorthand syntax.

---

## Task TA.4.1: Create Shorthand Syntax Test Fixture Directory

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/type_shorthand/`

Create a new test fixture directory for shorthand-specific tests:

```
TestFixtures/
├── type_shorthand/
│   ├── list_shorthand.spy
│   ├── list_shorthand.expected
│   ├── dict_shorthand.spy
│   ├── dict_shorthand.expected
│   ├── set_shorthand.spy
│   ├── set_shorthand.expected
│   ├── tuple_shorthand.spy
│   ├── tuple_shorthand.expected
│   ├── mixed_shorthand.spy
│   ├── mixed_shorthand.expected
│   ├── nested_shorthand.spy
│   └── nested_shorthand.expected
```

**Example test files:**

`list_shorthand.spy`:
```python
def sum_list(items: [int]) -> int:
    total: int = 0
    for item in items:
        total = total + item
    return total

def main() -> None:
    numbers: [int] = [1, 2, 3, 4, 5]
    print(sum_list(numbers))
```

`list_shorthand.expected`:
```
15
```

**Acceptance Criteria:**
- [ ] At least one test per shorthand form
- [ ] Tests exercise runtime behavior, not just parsing
- [ ] All tests pass with expected output

---

## Task TA.4.2: Create Shorthand Variations of Existing Tests

📁 **Files**: Various files in `TestFixtures/` subdirectories

For key existing tests, create `*_shorthand.spy` variations that use shorthand syntax but produce identical output.

**Mapping of existing tests to shorthand variations:**

| Existing Test | Shorthand Variation |
|--------------|---------------------|
| `functions/with_list_param.spy` | `functions/with_list_param_shorthand.spy` |
| `functions/returns_dict.spy` | `functions/returns_dict_shorthand.spy` |
| `type_system/generic_collections.spy` | `type_system/generic_collections_shorthand.spy` |
| `type_system/tuple_types.spy` | `type_system/tuple_types_shorthand.spy` |

**Example transformation:**

Original `functions/with_list_param.spy`:
```python
def process(items: list[int]) -> list[str]:
    result: list[str] = []
    for item in items:
        result.append(str(item))
    return result
```

Shorthand variation `functions/with_list_param_shorthand.spy`:
```python
def process(items: [int]) -> [str]:
    result: [str] = []
    for item in items:
        result.append(str(item))
    return result
```

Both should have the **same `.expected` file** (or a shared symlink).

**Acceptance Criteria:**
- [ ] At least 5 existing tests have shorthand variations
- [ ] Shorthand variations produce identical output
- [ ] Coverage includes: list, dict, set, tuple shorthand
- [ ] At least one test with nested shorthand types

---

## Task TA.4.3: Add Error Test Cases for Invalid Shorthand

📁 **Files**: `src/Sharpy.Compiler.Tests/Integration/TestFixtures/errors/`

Add error test cases for invalid shorthand usage:

```
errors/
├── empty_list_shorthand.spy
├── empty_list_shorthand.error
├── empty_set_shorthand.spy
├── empty_set_shorthand.error
├── ambiguous_brace_shorthand.spy
└── ambiguous_brace_shorthand.error
```

`empty_list_shorthand.spy`:
```python
x: [] = []
```

`empty_list_shorthand.error`:
```
element type required
```

**Acceptance Criteria:**
- [ ] Empty `[]` error tested
- [ ] Empty `{}` error tested  
- [ ] Clear, helpful error messages verified

---

# Phase TA.5: Documentation and Tooling

**Goal**: Update tooling and documentation to support shorthand syntax.

---

## Task TA.5.1: Update Syntax Highlighting (if applicable)

📁 **Files**: Any syntax highlighting definitions (VS Code extension, etc.)

**Changes:**
- Ensure `[]`, `{}`, `()` in type contexts are highlighted as types
- Ensure `T[]` array syntax is highlighted correctly

**Acceptance Criteria:**
- [ ] Shorthand syntax is visually distinct as type annotation
- [ ] No false positives (expression `[]` shouldn't be highlighted as type)

---

## Task TA.5.2: Document Formatter Preferences

📁 **Files**: `docs/tooling/formatter.md` (or equivalent)

Document the canonical forms and formatter behavior:

```markdown
## Type Annotation Formatting

### Single-Element Tuples
Canonical form uses trailing comma:
- Input: `(int)` 
- Formatted: `(int,)`

### Shorthand vs Canonical
The formatter preserves the style used:
- `[int]` stays as `[int]`
- `list[int]` stays as `list[int]`

To enforce one style, use linter rules (future feature).
```

**Acceptance Criteria:**
- [ ] Formatting conventions documented
- [ ] Trailing comma preference documented

---

# Summary

| Phase | Tasks | Priority | Complexity |
|-------|-------|----------|------------|
| TA.1 | Specification | High | Low |
| TA.2 | Parser Implementation | High | Medium |
| TA.3 | Parser Tests | High | Medium |
| TA.4 | Integration Tests | Medium | Low |
| TA.5 | Documentation/Tooling | Low | Low |

**Dependencies:**
- TA.2 depends on TA.1 (spec must be finalized first)
- TA.3 depends on TA.2 (tests require implementation)
- TA.4 depends on TA.2 (integration tests need working parser)
- TA.5 can proceed in parallel after TA.1

**Estimated Total Effort:** Medium (parser changes are localized; no semantic or codegen changes needed since AST is normalized)
