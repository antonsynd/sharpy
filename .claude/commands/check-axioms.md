# Check Axiom Compliance

Verify that a design decision or implementation complies with Sharpy's three core axioms.

## Decision or Code

$ARGUMENTS

## The Three Axioms

1. **Axiom 1: .NET Runtime Compatibility**
   - Sharpy compiles to C# for the .NET CLR
   - Must produce valid, efficient .NET code
   - Must interoperate with .NET libraries

2. **Axiom 2: Python Surface Syntax**
   - Sharpy uses Python 3 syntax and idioms
   - `snake_case` identifiers, colons, indentation
   - Pythonic constructs where possible

3. **Axiom 3: Static & Null-Safe Typing**
   - Explicit type annotations
   - Non-nullable by default (`T` vs `T?`)
   - No dynamic dispatch or runtime type discovery
   - Compile-time type resolution

## Precedence Rule

> **When axioms conflict: Axiom 1 > Axiom 3 > Axiom 2**

- Axiom 1 wins: broken .NET interop defeats the project's purpose
- Axiom 3 usually aligns with Axiom 1 (both favor static typing)
- Axiom 2 can often be approximated even when exact Python semantics aren't possible

**Exception:** If conflict can be resolved at zero cost, satisfy all axioms.

## Analysis Template

```markdown
### Axiom 1 (.NET Compatibility)
- [ ] Compiles to valid C#
- [ ] Works with .NET type system
- [ ] Interops with .NET libraries

### Axiom 2 (Python Syntax)
- [ ] Uses Pythonic syntax
- [ ] Familiar to Python developers
- [ ] Matches Python behavior (if applicable)

### Axiom 3 (Type Safety)
- [ ] Statically typed
- [ ] Null-safe
- [ ] No runtime type discovery

### Conflicts
[Document any conflicts and resolution]
```

## Common Patterns

| Conflict | Resolution |
|----------|------------|
| Integer division (`//` semantics) | Use C# semantics; provide `math.floor_div()` |
| String indexing (code points vs UTF-16) | UTF-16 units; helper methods for code points |
| Global/nonlocal variables | Use C# scoping rules |
| Duck typing | Require explicit interfaces |
