# Tagged Unions Implementation — Task Index

This directory contains task lists for implementing core sum types (Optional/Result) in Sharpy. These are **not** user-defined tagged unions (which are deferred to v0.2.x), but rather compiler-blessed primitives with special syntax support.

## Design Decision

**Optional and Result are core language primitives, not user-defined unions.**

| | Optional / Result | User-Defined Unions |
|---|---|---|
| **What they are** | Compiler-blessed core types | User-defined ADTs |
| **Defined with** | Hardcoded in Sharpy.Core | `union Foo:` syntax (v0.2.x) |
| **Implementation** | Struct (zero allocation) | Abstract class + sealed cases |
| **Special syntax** | `T?`, `T !E`, `maybe`, `try` | None |

See `nullability_and_results_redesign.md` for the full design rationale.

---

## Phase Overview

| Phase | Task File | Description | Dependencies |
|-------|-----------|-------------|--------------|
| 1 | `phase1_core_types_optional_result.md` | Create `Optional<T>` and `Result<T, E>` in Sharpy.Core | None |
| 2 | `phase2_lexer_bang_token.md` | Add `Bang` (`!`) token to lexer | None |
| 3 | `phase3_ast_type_annotation.md` | Update `TypeAnnotation` AST for `T?`, `T \| None`, `T !E` | None |
| 4 | `phase4_parser_type_annotations.md` | Parse `T?`, `T \| None`, `T !E` syntax | 2, 3 |
| 5 | `phase5_semantic_types.md` | Add `OptionalType` and `ResultType` semantic types | 1, 3 |
| 6 | `phase6_type_resolution.md` | Resolve type annotations to semantic types | 3, 4, 5 |
| 7 | `phase7_constructor_inference.md` | Recognize `Some/Nothing/Ok/Err` constructors | 1, 5, 6 |
| 8 | `phase8_code_generation.md` | Generate C# code for Optional/Result | 1, 5, 6, 7 |
| 9 | `phase9_maybe_expression.md` | Implement `maybe` expression | 1, 5, 6, 8 |

---

## Dependency Graph

```
Phase 1 (Core Types) ──────────────────┐
                                       │
Phase 2 (Lexer) ─────────┐             │
                         │             │
Phase 3 (AST) ───────────┼─────────────┤
                         │             │
                         ▼             │
Phase 4 (Parser) ────────┼─────────────┤
                         │             │
                         │             ▼
                         │      Phase 5 (Semantic Types)
                         │             │
                         ▼             ▼
                    Phase 6 (Type Resolution)
                              │
                              ▼
                    Phase 7 (Constructor Inference)
                              │
                              ▼
                    Phase 8 (Code Generation)
                              │
                              ▼
                    Phase 9 (Maybe Expression)
```

---

## Recommended Execution Order

### Parallel Track A: Core Types
1. **Phase 1** — Create `Optional<T>` and `Result<T, E>` structs

### Parallel Track B: Syntax Support
1. **Phase 2** — Add `Bang` token
2. **Phase 3** — Update AST
3. **Phase 4** — Update parser (requires Phase 2, 3)

### Sequential: Semantic & CodeGen
After tracks A and B complete:
1. **Phase 5** — Add semantic types (requires Phase 1, 3)
2. **Phase 6** — Type resolution (requires Phase 3, 4, 5)
3. **Phase 7** — Constructor inference (requires Phase 1, 5, 6)
4. **Phase 8** — Code generation (requires Phase 1, 5, 6, 7)
5. **Phase 9** — Maybe expression (requires Phase 1, 5, 6, 8)

---

## What's NOT Included

These tasks explicitly **exclude**:

1. **User-defined `union` declarations** — Deferred to v0.2.x
2. **`match` statements/expressions** — Deferred to v0.2.x
3. **Pattern matching on Optional/Result** — Requires `match`
4. **`try` expression implementation** — Already exists (just needs updating for new types)

---

## Verification Checkpoints

After each phase, verify:

1. **Build succeeds:** `dotnet build`
2. **Tests pass:** `dotnet test`
3. **No regressions:** Existing functionality still works

### Full Integration Test

After Phase 9, this code should compile and run:

```python
# Test Optional
def get_name(id: int) -> str?:
    if id == 0:
        return Nothing
    return Some(f"User {id}")

# Test Result  
def parse_int(s: str) -> int !str:
    if s == "":
        return Err("empty string")
    return Ok(42)  # Simplified

# Test maybe
def from_dotnet(raw: str | None) -> str?:
    return maybe raw

# Test all together
def main() -> int:
    name = get_name(1)
    result = parse_int("42")
    safe = from_dotnet("hello")
    
    if name.is_some() and result.is_ok() and safe.is_some():
        return 0
    return 1
```

---

## Documentation Updates

Each phase includes documentation updates. Key files to verify after completion:

- `docs/language_specification/tagged_unions.md` — Core vs user-defined distinction
- `docs/language_specification/tagged_unions_optional.md` — Updated implementation notes
- `docs/language_specification/tagged_unions_result.md` — Updated implementation notes
- `docs/language_specification/type_annotation_shorthand.md` — New syntax reference
- `docs/language_specification/maybe_expressions.md` — Implementation notes
