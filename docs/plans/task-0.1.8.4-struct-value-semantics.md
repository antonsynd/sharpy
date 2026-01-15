# Implementation Plan: Task 0.1.8.4 - Document Struct Value Semantics

## Overview

**Task ID:** 0.1.8.4
**Type:** 📝 Documentation
**File to modify:** `docs/language_specification/structs.md`

## Current State Analysis

The existing `structs.md` (96 lines) already covers:
- Basic struct syntax and definition
- Field declaration rules
- Constructor requirements (implicit/explicit)
- Interface implementation with boxing warnings
- Basic "when to use structs" guidance

**What's missing (the task objective):**
- Dedicated section explaining value type semantics in detail
- Copy-on-assignment behavior with examples
- Pass-by-value semantics with examples
- Memory model explanation (inline storage, no heap for struct itself)
- Cross-reference to parameter modifiers (`in[T]`, `ref[T]`, `out[T]`) for avoiding copies
- Comparison with reference types (classes)

## Step-by-Step Implementation

### Step 1: Add "Value Semantics" Section

Insert a new section after the opening definition (after line 30) that comprehensively explains value type behavior.

**Content to add:**

```markdown
## Value Semantics

Structs are **value types** with distinct behavior from classes (reference types):

### Copy on Assignment

When a struct is assigned to a new variable, a complete copy is made:

```python
struct Point:
    x: int
    y: int

p1 = Point(10, 20)
p2 = p1              # Creates a COPY of p1

p2.x = 99            # Modifying p2 does NOT affect p1
print(p1.x)          # 10 (unchanged)
print(p2.x)          # 99
```

**Contrast with classes (reference types):**

```python
class PointRef:
    x: int
    y: int

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

r1 = PointRef(10, 20)
r2 = r1              # r2 references the SAME object as r1

r2.x = 99            # Modifying r2 ALSO modifies r1
print(r1.x)          # 99 (changed!)
print(r2.x)          # 99
```

### Pass by Value

Structs are copied when passed to functions:

```python
def try_modify(p: Point):
    p.x = 999        # Modifies the local copy only

point = Point(1, 2)
try_modify(point)
print(point.x)       # 1 (unchanged - function received a copy)
```

### Inline Storage

Struct instances are stored inline (directly in the variable's memory location):
- Local struct variables are stored on the stack
- Struct fields within a class are embedded directly in the class's heap allocation
- Arrays of structs store elements contiguously (no per-element heap allocations)

This provides memory efficiency and cache-friendly access patterns for small types.
```

### Step 2: Add "Avoiding Copies with Parameter Modifiers" Section

Add a section explaining how to pass large structs efficiently.

**Content to add:**

```markdown
## Avoiding Copies for Large Structs

For structs larger than ~16 bytes, copying can impact performance. Use parameter modifiers to pass by reference:

| Modifier | Use Case | Callee Can Modify |
|----------|----------|-------------------|
| `in[T]` | Read-only access, avoid copy | ❌ No |
| `ref[T]` | Read/write access | ✅ Yes |
| `out[T]` | Output parameter | ✅ Yes (must assign) |

```python
struct Matrix4x4:
    """64-byte matrix - too large to copy efficiently."""
    data: list[float]  # 16 floats

    def __init__(self):
        self.data = [0.0] * 16

def transform_readonly(matrix: in[Matrix4x4], point: Point) -> Point:
    """Efficient: matrix passed by reference, not copied."""
    # matrix.data[0] = 1.0  # ERROR: Cannot modify `in` parameter
    return Point(...)

def transform_inplace(matrix: ref[Matrix4x4]):
    """Modify the caller's matrix directly."""
    matrix.data[0] = 1.0   # OK: ref allows modification
```

See [Parameter Modifiers](parameter_modifiers.md) for complete documentation.
```

### Step 3: Enhance "When to Use Structs" Section

Expand the existing guidance with more detail on the value semantics implications.

**Update existing section (lines 89-92) to:**

```markdown
## When to Use Structs

**Prefer structs when:**
- The type is small (typically ≤ 16 bytes)
- Value semantics are desired (copies should be independent)
- The type represents a single value (Point, Color, DateTime)
- Immutability is intended (no need to share state)
- High-frequency allocation would cause GC pressure with classes

**Prefer classes when:**
- The type is large (copying would be expensive)
- Reference semantics are needed (shared state)
- Inheritance is required
- Identity matters (two instances should be distinguishable even if equal)

**Value Semantics Implications:**
- Struct equality compares field values (not identity)
- Structs in collections are independent copies
- Returning a struct from a method returns a copy
```

### Step 4: Add "See Also" Section

Add cross-references to related documentation.

**Content to add:**

```markdown
## See Also

- [Parameter Modifiers](parameter_modifiers.md) — `in`, `ref`, `out` for efficient struct passing
- [Classes](classes.md) — Reference types with different semantics
- [Type Hierarchy](type_hierarchy.md) — How structs fit in the type system
```

## Files to Modify

| File | Action |
|------|--------|
| `docs/language_specification/structs.md` | Add value semantics documentation |

## Tests to Verify

This is a documentation-only task. Verification:

1. **Manual review:** Ensure examples are syntactically correct Sharpy code
2. **Cross-reference check:** Verify linked documents exist:
   - `parameter_modifiers.md` ✓ (exists, 251 lines)
   - `classes.md` ✓ (exists, 71 lines)
   - `type_hierarchy.md` ✓ (exists, 31 lines)
3. **Consistency check:** Confirm terminology matches existing docs (e.g., "value types", "reference types")
4. **Build docs:** If documentation build process exists, run it to verify no broken links

## Potential Risks / Questions

### Risks

1. **Example accuracy:** Code examples must be valid Sharpy syntax. Need to verify:
   - Struct field mutation syntax is correct
   - `in[T]` / `ref[T]` syntax matches `parameter_modifiers.md`
   - List initialization syntax for structs

2. **Boxing discussion:** Current doc mentions boxing briefly. New content shouldn't contradict or duplicate. Keep boxing details in the existing "Structs and Interface Default Methods" section.

### Questions to Clarify

1. **Struct equality:** Does Sharpy generate value-based equality for structs automatically, or must users implement `__eq__`? (Affects "Value Semantics Implications" bullet point)

2. **Struct mutability:** Are struct fields mutable by default? The examples show mutation (`p2.x = 99`), but should we recommend immutable structs?

3. **Memory model accuracy:** The "inline storage" description assumes stack allocation for locals. Is this always true, or can the JIT move structs to heap? (May want to soften language to "typically stored on stack")

4. **Size threshold:** The "≤ 16 bytes" guidance comes from common C# practice. Is this the right threshold for Sharpy guidance?

## Summary

The implementation adds approximately 80-100 lines to `structs.md`, organized into:
- "Value Semantics" section with copy/assignment and pass-by-value examples
- "Avoiding Copies for Large Structs" section with parameter modifier guidance
- Enhanced "When to Use Structs" section with class comparison
- "See Also" section with cross-references

This addresses the task objective of documenting value type semantics for engineers/users while maintaining consistency with existing documentation style.
