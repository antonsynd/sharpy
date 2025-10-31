# Documentation Reorganization

The Sharpy documentation has been reorganized into three focused documents for better clarity and maintainability.

## New Structure

### [Language Reference](language_reference.md)
**Audience**: Sharpy developers writing code

**Content**:
- Complete syntax guide
- Keywords and operators
- Type annotations
- Control flow
- Functions, classes, structs, protocols
- Module system and imports
- Properties and decorators
- Async programming
- Examples and usage patterns

**Use when**: Learning Sharpy syntax, looking up language features, writing Sharpy code

---

### [Type System](type_system.md)
**Audience**: Advanced users, library authors, contributors

**Content**:
- Type hierarchy and built-in types
- Sharpy.Object base class
- Nullable vs Optional semantics
- Protocols and structural typing
- Generic types with constraints and variance
- Type inference rules
- Module implementation details
- Classes, structs, and inheritance
- Tuples and properties

**Use when**: Understanding type semantics, designing libraries, working with protocols, debugging type issues

---

### [Compiler Design](compiler_design.md)
**Audience**: Compiler developers, maintainers

**Content**:
- Compilation pipeline architecture
- Lexer, parser, semantic analyzer, code generator
- AST structure
- Multi-pass semantic analysis
- Name mangling rules
- C# code generation strategies
- Operator synthesis
- Runtime implementation
- Optimization strategies

**Use when**: Contributing to the compiler, understanding code generation, debugging compiler issues

## Old Documents

The original documents have been superseded:

- **`specification.md`** → Split into `language_reference.md` and `compiler_design.md`
- **`object_model.md`** → Reorganized into `type_system.md` with cross-references

The old files will remain for reference but should not be updated.

## Cross-References

Each document includes "See Also" sections with links to related content:

```markdown
<!-- In language_reference.md -->
See [Type System - Protocols](type_system.md#protocols-and-interfaces)
for type checking rules.

<!-- In type_system.md -->
For syntax details, see [Language Reference - Classes](language_reference.md#classes).

<!-- In compiler_design.md -->
For type system semantics, see [Type System](type_system.md).
```

## Migration Guide for Contributors

### Adding new syntax
1. Add syntax description to `language_reference.md`
2. Add type semantics to `type_system.md` if needed
3. Document code generation in `compiler_design.md`

### Documenting a new type
1. Add user-facing info to `type_system.md` (Built-in Types section)
2. Add usage examples to `language_reference.md`
3. Document implementation in `compiler_design.md` (Runtime Implementation section)

### Compiler features
1. Architecture/design → `compiler_design.md`
2. User-visible behavior → `language_reference.md` or `type_system.md`
3. Always cross-reference between docs

## Quick Reference

| I want to... | Read... |
|--------------|---------|
| Learn Sharpy syntax | [Language Reference](language_reference.md) |
| Understand how types work | [Type System](type_system.md) |
| Understand protocols | [Type System - Protocols](type_system.md#protocols-and-interfaces) |
| See import syntax | [Language Reference - Modules](language_reference.md#modules-and-imports) |
| Understand module implementation | [Type System - Modules](type_system.md#modules) |
| See how code is generated | [Compiler Design](compiler_design.md) |
| Contribute to the compiler | [Compiler Design](compiler_design.md) |
| Design a library | [Type System](type_system.md) |
