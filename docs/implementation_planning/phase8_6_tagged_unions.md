<!-- Verified by /verify-plan on 2026-02-28 -->
<!-- Verification result: PASS WITH CORRECTIONS -->

# Phase 8.6: Tagged Union Declarations — Implementation Plan

## Overview

Implement `union` declarations (algebraic data types / discriminated unions). Unions lower to an abstract base class with sealed nested case classes in C#.

**Spec files:** `docs/language_specification/tagged_unions_optional.md`, `tagged_unions_result.md`
**AST placeholders:** `Statement.Future.cs` (UnionDef, UnionCaseDef, UnionCaseField)
**Semantic placeholder:** `SemanticType.cs` (UnionType)
**Pattern placeholder:** `Pattern.cs` (UnionCasePattern — wiring deferred to Phase 8.7)

### Sharpy Syntax

```python
union Shape:
    case Circle(radius: float)
    case Rectangle(width: float, height: float)
    case Point()

# Construction
s: Shape = Shape.Circle(5.0)
r: Shape = Shape.Rectangle(3.0, 4.0)
p: Shape = Shape.Point()
```

### Generic Unions

```python
union Result[T, E]:
    case Ok(value: T)
    case Err(error: E)

result: Result[int, str] = Result.Ok(42)
```

### C# Lowering Target

```csharp
public abstract class Shape
{
    private Shape() { }

    public sealed class Circle : Shape
    {
        public double Radius { get; }
        public Circle(double radius) { Radius = radius; }
        public void Deconstruct(out double radius) { radius = Radius; }
    }

    public sealed class Rectangle : Shape
    {
        public double Width { get; }
        public double Height { get; }
        public Rectangle(double width, double height) { Width = width; Height = height; }
        public void Deconstruct(out double width, out double height)
        {
            width = Width;
            height = Height;
        }
    }

    public sealed class Point : Shape
    {
        public Point() { }
    }
}
```

**Key lowering decisions:**
- Abstract base class with **private constructor** (prevents external subclassing)
- Each case is a **public sealed nested class** inheriting from the base
- Cases with fields get: read-only auto-properties, public constructor, `Deconstruct` method
- Cases with zero fields get: public parameterless constructor (singleton optimization deferred)
- Generic type parameters on the union propagate to the base class and all case classes

---

## Scope

**In scope (this plan):**
- `union` keyword parsing (new `TokenType.Union`)
- `TypeKind.Union` in semantic model
- Name resolution: register union type + case types
- Type checking: validate union declarations
- Code generation: abstract base + sealed nested classes
- Union case construction via `UnionName.CaseName(args)`
- Generic unions with type parameters
- Integration tests (.spy + .expected)

**Out of scope (Phase 8.7):**
- `UnionCasePattern` in match statements (`case Ok(value):`)
- Short-form case constructors without type prefix (`Ok(42)` instead of `Result.Ok(42)`)

**Out of scope (Phase 8.8):**
- Exhaustiveness checking for unions in match statements

---

## Reference: Enum Pipeline (Template)

The enum implementation is the closest analog. Key dispatch points:

| Stage | File | Pattern |
|-------|------|---------|
| Lexer keyword | `Lexer.cs:82` | `{ "enum", TokenType.Enum }` |
| Parser dispatch | `Parser.cs:399` | `TokenType.Enum => ParseEnumDef()` |
| Name resolution | `NameResolver.cs:212` | `case EnumDef: ResolveEnumDeclaration()` |
| Type checking | `TypeChecker.cs:317` | `case EnumDef: CheckEnum()` |
| CodeGen dispatch | `RoslynEmitter.ModuleClass.cs:523` | `EnumDef enumDef => GenerateEnumDeclaration(enumDef)` |
| TypeKind | `Symbol.cs:315-321` | `enum TypeKind { Class, Struct, Interface, Enum }` |

---

## Tasks

### Task 1: Lexer + TypeKind + AST Readiness

**Goal:** Add `union` as a recognized keyword and `TypeKind.Union` to the semantic model.

**Files to modify:**
1. `src/Sharpy.Compiler/Lexer/Token.cs` — Add `Union` to `TokenType` enum (near `Enum`)
2. `src/Sharpy.Compiler/Lexer/Lexer.cs` — Add `{ "union", TokenType.Union }` to keyword dictionary (line ~82) [CORRECTED: keyword dict starts at line 62, not 113]
3. `src/Sharpy.Compiler/Semantic/Symbol.cs` — Add `Union` to `TypeKind` enum (line ~321)

**Verification:**
- `/build` passes
- `/spy-emit-tokens` on a file containing `union` shows `TokenType.Union`

**Commit:** `feat: add union keyword token and TypeKind.Union`

---

### Task 2: Parser — ParseUnionDef()

**Goal:** Parse `union Name: case Case1(...) case Case2(...)` into `UnionDef` AST.

**Files to modify:**
1. `src/Sharpy.Compiler/Parser/Parser.cs` — Add `TokenType.Union => ParseUnionDef()` to the `ParseStatement()` switch (line ~399, near `TokenType.Enum`)
2. `src/Sharpy.Compiler/Parser/Parser.Definitions.cs` — Implement `ParseUnionDef()` method

**ParseUnionDef() implementation notes:**
- Follow the `ParseEnumDef()` pattern (lines 731-834 of Parser.Definitions.cs)
- Consume `TokenType.Union`, then identifier (name), then optional type parameters via `ParseTypeParameterList()` (already exists at line 630 — used by `ParseClassDef`, `ParseStructDef`, `ParseInterfaceDef`, `ParseTypeAlias`), then `TokenType.Colon`, then `TokenType.Newline` [CORRECTED: method is `ParseTypeParameterList()` not `ParseTypeParameters()`]
- Consume `TokenType.Indent`
- Optional docstring (same pattern as enums)
- Loop: expect `TokenType.Case`, then identifier (case name), then optional `(` field_list `)`, then newline
- Each field: `identifier : type_expr` (parse type with `ParseTypeAnnotation()`)
- Consume `TokenType.Dedent`
- Validate at least one case
- Return `UnionDef` record (already defined in `Statement.Future.cs`)
- Handle `pass` statement for empty body (emit error: unions must have at least one case)
- Handle generic type parameters: `union Result[T, E]:` — reuse `ParseTypeParameterList()` from class parsing (line 630 of Parser.Definitions.cs)

**Also handle in Parser.cs:**
- Add `TokenType.Union` to `IsCompoundStatement()` or equivalent check if one exists
- Add `TokenType.Union` to `Parser.Types.cs` line ~516 where `TokenType.Enum` appears in the "not a type expression" set [CORRECTED: line 516, not 526]
- Add `TokenType.Union` to the decorated definitions dispatch (~line 461 in Parser.cs) so `@final union Foo:` works

**Verification:**
- `/build` passes
- `/spy-emit-ast` on a union definition shows the correct `UnionDef` AST with cases and fields
- Create a temp `.spy` file:
  ```python
  union Shape:
      case Circle(radius: float)
      case Rectangle(width: float, height: float)
      case Point()
  ```

**Commit:** `feat: parse union declarations into UnionDef AST`

---

### Task 3: Semantic — Name Resolution

**Goal:** Register union types and their cases in the symbol table during Pass 1.

**Files to modify:**
1. `src/Sharpy.Compiler/Semantic/NameResolver.cs` — Add `case UnionDef:` dispatch (~line 212) and implement `ResolveUnionDeclaration()`

**ResolveUnionDeclaration() implementation:**
- Follow `ResolveEnumDeclaration()` pattern (lines 440-465)
- Check for duplicate name in symbol table
- Create `TypeSymbol` with `TypeKind = TypeKind.Union`
- Set `TypeParameters` from `unionDef.TypeParameters`
- Register in symbol table
- For each case: create a nested `TypeSymbol` with `TypeKind = TypeKind.Class` (cases are classes)
  - Set fields from `UnionCaseField` definitions
  - Store these case symbols somewhere accessible — options:
    - Add them as entries in the parent TypeSymbol's `Methods` or a new `UnionCases` property
    - OR register them in the symbol table as `UnionName.CaseName` qualified names
  - **Recommended approach:** Store case TypeSymbols in the parent union TypeSymbol. Add a `List<TypeSymbol> UnionCases` property to `TypeSymbol` in `Symbol.cs`. Each case TypeSymbol should have `BaseType` pointing to the union TypeSymbol.

**Additional file modifications:**
2. `src/Sharpy.Compiler/Semantic/Symbol.cs` — Add `public List<TypeSymbol> UnionCases { get; init; } = new();` to `TypeSymbol` (near `Fields` and `Methods`)

**Verification:**
- `/build` passes
- A simple union definition compiles without "undefined" errors
- Duplicate union name produces `SPY0204` (DuplicateDefinition) [CORRECTED: SPY0200 is UndefinedVariable; DuplicateDefinition is SPY0204]

**Commit:** `feat: add union name resolution and case symbol registration`

---

### Task 4: Semantic — Type Checking

**Goal:** Type-check union declarations: validate cases, resolve field types, register UnionType.

**Files to modify:**
1. `src/Sharpy.Compiler/Semantic/TypeChecker.cs` — Add `case UnionDef:` dispatch (~line 317)
2. `src/Sharpy.Compiler/Semantic/TypeChecker.Definitions.cs` — Implement `CheckUnion(UnionDef unionDef)`

**CheckUnion() implementation:**
- Look up the TypeSymbol from the symbol table
- Create a `UnionType` semantic type and record it in `SemanticInfo`
- For each case:
  - Check for duplicate case names (new diagnostic: next available in semantic range is `SPY0365` — highest used is SPY0364) [CORRECTED: SPY0280 is already taken; use SPY0365+]
  - Resolve each field's type annotation via `_typeResolver.ResolveTypeAnnotation()` (method is on `TypeResolver`, not `TypeChecker` — see `TypeResolver.cs:34`) [CORRECTED: method lives in TypeResolver, accessed via `_typeResolver` field]
  - Store resolved field types on the case TypeSymbol
  - Create a `UserDefinedType` for each case and record in SemanticInfo
- For generic unions: resolve type parameters, ensure they're used in at least one case field

**New diagnostic codes** (add to `Diagnostics/DiagnosticCodes.cs`):
- `DuplicateUnionCase` — "Union case 'X' is already defined"
- `EmptyUnion` — "Union must have at least one case" (if not caught by parser)

**Verification:**
- `/build` passes
- Duplicate case names produce correct diagnostics
- Field type resolution works (e.g., `case Circle(radius: float)` resolves `float` correctly)

**Commit:** `feat: add union type checking and validation`

---

### Task 5: CodeGen — Union Declaration Lowering

**Goal:** Generate abstract base class + sealed nested case classes from UnionDef.

**Files to modify:**
1. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs` — Add dispatch: `UnionDef unionDef => GenerateUnionDeclaration(unionDef)` (~line 523, near `EnumDef`)
2. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs` — Implement `GenerateUnionDeclaration(UnionDef unionDef)`

**GenerateUnionDeclaration() implementation:**

```
Method: GenerateUnionDeclaration(UnionDef unionDef) -> MemberDeclarationSyntax
```

1. **Base class:**
   - Name: `NameMangler.Transform(unionDef.Name, NameContext.Type)`
   - Modifiers: `public abstract`
   - Private parameterless constructor: prevents external subclassing
   - If generic: add type parameters via `TypeParameterList()`

2. **For each case, generate a nested sealed class:**
   - Name: `NameMangler.Transform(caseDef.Name, NameContext.Type)`
   - Modifiers: `public sealed`
   - Base type: the union base class (with type arguments if generic)
   - For each field:
     - Read-only auto-property: `public T FieldName { get; }` using `NameMangler.ToPascalCase(field.Name)`
     - Map field type via `_typeMapper.MapSemanticType()` for resolved SemanticTypes, or `_typeMapper.MapType()` for TypeAnnotations [CORRECTED: primary method for SemanticType is `MapSemanticType()` at TypeMapper.cs:48]
   - Constructor: takes all fields as parameters, assigns to properties
   - `Deconstruct` method (only if case has fields): `public void Deconstruct(out T1 field1, out T2 field2, ...)`

3. **Assemble:** Add all case classes as members of the base class, return the base class declaration

**Helper methods to create (all in RoslynEmitter.TypeDeclarations.cs):**
- `GenerateUnionCaseClass(UnionCaseDef caseDef, string baseClassName, ...)` — generates one sealed case class
- `GenerateDeconstructMethod(UnionCaseDef caseDef)` — generates the Deconstruct method

**Pre-scan registration** (in `RoslynEmitter.ModuleClass.cs`, near enum pre-scan at lines 108-127):
- Register union TypeSymbol in the codegen context (same pattern as enum pre-scan)

**Verification:**
- `/build` passes
- `/spy-emit-csharp` on a union definition shows correct abstract base + sealed nested classes
- Generated C# compiles (verified by running the full pipeline)

**Commit:** `feat: generate C# abstract base + sealed case classes for unions`

---

### Task 6: Union Case Construction

**Goal:** Enable `Shape.Circle(5.0)` construction syntax — member access on union type resolves to case constructor.

**Context:** `Shape.Circle(5.0)` is parsed as `CallExpression(MemberAccess(Shape, Circle), [5.0])`. The type checker needs to resolve `Shape.Circle` as a reference to the nested case type, and the call as a constructor invocation.

**Files to modify:**
1. `src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.Access.cs` — Handle member access on union types to resolve case types
2. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.Access.cs` — Generate `new Shape.Circle(5.0)` for union case construction
3. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` — May need adjustments in call expression generation

**Type checker changes:**
- In the member access checking logic, when the object type is a `UnionType` (or the symbol is a `TypeSymbol` with `TypeKind.Union`):
  - Look up the member name in `typeSymbol.UnionCases`
  - If found, return a `UserDefinedType` for the case class
  - This allows the subsequent `CallExpression` check to treat it as a constructor call

**CodeGen changes:**
- When generating a call expression where the callee resolves to a union case type:
  - Emit `new UnionName.CaseName(args)` — a constructor invocation on the nested class
  - Use `ObjectCreationExpression()` with `QualifiedName(unionName, caseName)`

**Verification:**
- `/build` passes
- Integration test: construct union cases and print type info
- Create test fixture `src/Sharpy.Compiler.Tests/Integration/TestFixtures/unions/union_basic.spy`:
  ```python
  union Shape:
      case Circle(radius: float)
      case Rectangle(width: float, height: float)
      case Point()

  def main():
      c: Shape = Shape.Circle(5.0)
      r: Shape = Shape.Rectangle(3.0, 4.0)
      p: Shape = Shape.Point()
      print(isinstance(c, Shape))
      print(isinstance(r, Shape))
      print(isinstance(p, Shape))
  ```
  Expected output:
  ```
  True
  True
  True
  ```
  [CORRECTED: `type(c).__name__` is not a tested pattern in Sharpy; `isinstance()` is the established test pattern (see `TestFixtures/type_system/isinstance_*.spy`)]

**Commit:** `feat: support union case construction via UnionName.CaseName(args)`

---

### Task 7: Generic Unions

**Goal:** Handle type parameters on union declarations end-to-end.

**Files to modify:**
1. Parser — already handled in Task 2 (ParseTypeParameters reuse)
2. `src/Sharpy.Compiler/Semantic/TypeResolver.cs` — Resolve generic type parameters on union types (follow class generic pattern)
3. `src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs` — Emit type parameter lists on base class and propagate to case classes

**Implementation notes:**
- Generic unions like `union Result[T, E]:` should emit `public abstract class Result<T, E>`
- Case classes inherit with type args: `public sealed class Ok : Result<T, E>`
- Field types can reference type parameters: `public T Value { get; }`
- Construction: `Result.Ok[int, str](42)` or `Result[int, str].Ok(42)` — follow whatever pattern classes use for generic construction

**Verification:**
- `/build` passes
- Integration test with generic union:
  ```python
  union Result[T, E]:
      case Ok(value: T)
      case Err(error: E)

  def main():
      ok: Result[int, str] = Result.Ok(42)
      err: Result[int, str] = Result.Err("oops")
      print(type(ok).__name__)
      print(type(err).__name__)
  ```

**Commit:** `feat: support generic type parameters on union declarations`

---

### Task 8: Error Tests + Edge Cases

**Goal:** Add error diagnostic tests and handle edge cases.

**Test fixtures to create** (in `src/Sharpy.Compiler.Tests/Integration/TestFixtures/unions/`):

1. `union_duplicate_case.spy` + `.error` — duplicate case name error
2. `union_empty.spy` + `.error` — union with no cases (parser error)
3. `union_field_types.spy` + `.expected` — various field types (int, str, list, custom classes)
4. `union_single_case.spy` + `.expected` — union with exactly one case
5. `union_no_fields.spy` + `.expected` — union where all cases have no fields
6. `union_mixed_fields.spy` + `.expected` — mix of cases with and without fields

**Edge cases to verify:**
- Union with only one case (valid, should compile)
- Case with no fields: `case None()` — parameterless constructor
- Union name conflicts with existing class/enum name — duplicate definition error
- Field name conflicts within a case — handled by C# compiler (our codegen just needs to be correct)
- Reserved C# keywords as field names — `NameMangler` should handle via `CSharpKeywords` escaping

**Commit:** `test: add error and edge case tests for union declarations`

---

### Task 9: Grammar + Spec Updates

**Goal:** Update the grammar and spec files to reflect the implementation.

**Files to modify:**
1. `docs/language_specification/grammar.ebnf.txt` — Add `union_def` production to `compound_stmt`, define `union_body` grammar rule
2. `docs/language_specification/tagged_unions_optional.md` — Update implementation status
3. `docs/language_specification/tagged_unions_result.md` — Update implementation status
4. `docs/implementation_planning/phases2.md` — Mark 8.6 as complete

**Grammar additions:**
```ebnf
compound_stmt   ::= ... | union_def

union_def       ::= 'union' identifier [ type_params ] ':' NEWLINE union_body

union_body      ::= INDENT union_case { union_case } DEDENT

union_case      ::= 'case' identifier [ '(' case_fields ')' ] NEWLINE
case_fields     ::= case_field { ',' case_field } [ ',' ]
case_field      ::= identifier ':' type_expr
```

**Note:** The existing grammar embeds `tagged_union_case` within `enum_body` (grammar lines 548-562). This plan uses a separate `union` keyword (matching the `UnionDef` AST record in `Statement.Future.cs` and the spec files `tagged_unions_optional.md`/`tagged_unions_result.md`). The grammar's `enum_body` `tagged_union_case` rule should be removed or deprecated when `union_def` is added. [CORRECTED: added grammar alignment note]

**Commit:** `docs: update grammar and spec for union declarations`

---

## Task Dependencies

```
Task 1 (Lexer/TypeKind)
  └→ Task 2 (Parser)
       └→ Task 3 (Name Resolution)
            └→ Task 4 (Type Checking)
                 └→ Task 5 (CodeGen)
                      └→ Task 6 (Construction)
                           └→ Task 7 (Generics)
                                └→ Task 8 (Error Tests)
                                     └→ Task 9 (Docs)
```

All tasks are sequential — each builds on the previous. Tasks 1-5 form the core pipeline. Tasks 6-7 add construction and generics. Tasks 8-9 are polish.

## Verification Checklist

After all tasks, the following should work end-to-end:

- [ ] `union Shape: case Circle(radius: float)` parses to `UnionDef` AST
- [ ] Union types are registered in the symbol table with `TypeKind.Union`
- [ ] Case types are nested under the union TypeSymbol
- [ ] Type checking validates cases and resolves field types
- [ ] CodeGen emits abstract base class + sealed nested case classes
- [ ] `Shape.Circle(5.0)` constructs a case instance
- [ ] Generic unions work: `union Result[T, E]:`
- [ ] Deconstruct methods are emitted for cases with fields
- [ ] Duplicate case names produce diagnostics
- [ ] All existing tests still pass (no regressions)

---

## Verification Summary

**Result:** PASS WITH CORRECTIONS
**Verified on:** 2026-02-28
**Plan file:** `docs/implementation_planning/phase8_6_tagged_unions.md`

### Corrections Made

| # | Claim | Correction |
|---|-------|-----------|
| 1 | `Lexer.cs:113` for keyword dict | **Line 82**, not 113. Keyword dictionary starts at line 62. |
| 2 | `Parser.cs:397` for enum dispatch | **Line 399**. Minor line shift. |
| 3 | `ParseTypeParameters()` method | **Does not exist.** Correct method is `ParseTypeParameterList()` at `Parser.Definitions.cs:630`. |
| 4 | `SPY0200` = DuplicateDefinition | **SPY0200 is UndefinedVariable.** DuplicateDefinition is `SPY0204`. |
| 5 | `SPY0280` suggested for new diag | **SPY0280 is already taken.** Next available in semantic range is `SPY0365` (highest used: SPY0364). |
| 6 | `ResolveTypeAnnotation()` on TypeChecker | **Method is on `TypeResolver`** (TypeResolver.cs:34), accessed via `_typeResolver` field in TypeChecker. |
| 7 | `_typeMapper.MapType()` for SemanticType | **Primary method is `MapSemanticType()`** (TypeMapper.cs:48). `MapType()` exists but takes `TypeAnnotation`. |
| 8 | Test uses `type(c).__name__` | **Not a tested pattern in Sharpy.** Replaced with `isinstance()` which is the established test pattern. |
| 9 | `Parser.Types.cs` line ~526 | **Line 516**, not 526. |
| 10 | Grammar `union_body` had redundant `NEWLINE INDENT` | Fixed to `INDENT ... DEDENT` (NEWLINE is consumed by `union_def` before `union_body`). |

### Warnings

- **Grammar discrepancy:** The existing grammar puts `tagged_union_case` inside `enum_body` (lines 548-562), but this plan uses a separate `union` keyword (matching the `UnionDef` AST in `Statement.Future.cs` and spec files). The grammar's `enum_body` rule should be updated when `union_def` is added. This is a deliberate design choice, not an error.
- **`UnionCaseField.Name` is non-nullable (`string`)** with default `""`. Positional fields are not yet supported; Sharpy requires named fields per spec.
- **C# 9.0 compliance verified** — `{ get; }` auto-properties, `Deconstruct` with `out` parameters, and abstract+sealed pattern are all valid C# 9.0. Generated code will compile against `netstandard2.1`.
- **No existing `Deconstruct` generation in codebase** — This will be new codegen. No conflicts expected.

### Verified Claims (Correct)

| Dimension | Items Checked | Result |
|-----------|--------------|--------|
| **File paths** | All 12 source files referenced | All exist |
| **AST records** | UnionDef, UnionCaseDef, UnionCaseField, UnionCasePattern | All confirmed in Statement.Future.cs and Pattern.cs with exact property names |
| **SemanticType** | UnionType with Name, Symbol, CaseTypes | Confirmed at SemanticType.cs:770-790 |
| **Enum pipeline pattern** | Lexer → Parser → NameResolver → TypeChecker → RoslynEmitter dispatch points | All 5 dispatch points confirmed at stated lines |
| **TypeKind enum** | `{ Class, Struct, Interface, Enum }` at Symbol.cs:315-321 | Confirmed; no existing `Union` value |
| **ParseEnumDef span** | Lines 731-834 of Parser.Definitions.cs | Confirmed |
| **ResolveEnumDeclaration span** | Lines 440-465 of NameResolver.cs | Confirmed |
| **Enum pre-scan** | Lines 109-127 of RoslynEmitter.ModuleClass.cs | Confirmed |
| **NameMangler** | `Transform()` with `NameContext.Type`, `ToPascalCase()` | Both confirmed in NameMangler.cs |
| **C# 9.0 lowering** | Abstract class + sealed nested + Deconstruct | All valid C# 9.0 |

### Missing Steps

- **Task 2 should also handle decorated unions**: The parser should support `@final` or other decorators before `union` keyword, similar to how `ParseClassDef` handles decorated definitions. The `Parser.cs` dispatch for `TokenType.Union` should also be added in the decorated definitions section (~line 461).
- **Task 3 should consider `MaterializeInheritance()`**: After name resolution, union case types with BaseType pointing to the union type may need to participate in inheritance materialization (see `NameResolver.ResolveInheritance()`).
- **Task 5 should generate `ToString()` override**: Consider generating `ToString()` on case classes for debugging (e.g., `Circle(radius=5.0)`). Not critical but improves usability.

### Unchecked Claims

- **Task 6 member access resolution**: Did not trace the exact code path in `TypeChecker.Expressions.Access.cs` for how member access on type symbols works. The approach (looking up union cases via member name) is architecturally sound but the exact integration point needs investigation during implementation.
- **Task 7 generic construction syntax**: Did not verify how generic class construction (`ClassName[T](args)`) is currently parsed/emitted. The implementer should check existing generic class instantiation patterns before implementing generic union construction.
