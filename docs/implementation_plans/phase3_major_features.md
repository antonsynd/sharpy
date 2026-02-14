# Phase 3: Major Feature Gaps -- Implementation Plan

This document provides step-by-step implementation plans for the six largest
unimplemented features in the Sharpy compiler. Each section is self-contained
and follows the canonical implementation order:

```
Lexer -> Parser -> Semantic -> Validation -> CodeGen -> Tests
```

**Before starting any feature**, read the General Guidance section at the end
of this document.

---

## Table of Contents

1. [Feature 1: Properties System](#feature-1-properties-system)
2. [Feature 2: `with` Statement (IDisposable)](#feature-2-with-statement-idisposable)
3. [Feature 3: `ref`/`out`/`in` Parameter Modifiers](#feature-3-refoutin-parameter-modifiers)
4. [Feature 4: Collection Type Wrappers](#feature-4-collection-type-wrappers)
5. [Feature 5: Spread Operator](#feature-5-spread-operator)
6. [Feature 6: Named Tuples](#feature-6-named-tuples)
7. [General Guidance](#general-guidance)

---

## Feature 1: Properties System

### Context and Motivation

Properties are the **single largest gap** in the Sharpy type system. Currently,
all class/struct fields are emitted as bare `public` C# fields. This prevents:

- Encapsulation (no read-only, write-only, or init-only semantics)
- Computed properties (must use methods instead)
- Interface property requirements (interfaces cannot declare property contracts)
- Virtual/abstract properties for polymorphism
- Mixed access modifiers (public getter, private setter)

The feature is described across three spec documents:
`properties.md`, `properties_function_style.md`, `properties_inheritance.md`.

### Spec Summary

**Auto-properties** -- compiler-generated backing field:

| Syntax | C# Equivalent |
|--------|---------------|
| `property name: T` | `T Name { get; set; }` |
| `property name: T = val` | `T Name { get; set; } = val` |
| `property get name: T` | `T Name { get; }` |
| `property set name: T` | `T Name { set; }` |
| `property init name: T` | `T Name { get; init; }` |

**Function-style properties** -- user-provided backing field or computed:

| Syntax | C# Equivalent |
|--------|---------------|
| `property get name(self) -> T:` | `T Name { get { ... } }` |
| `property set name(self, value: T):` | `T Name { set { ... } }` |

Key rules:
- Auto-property and function-style accessors for the same name cannot coexist
  (no shared backing field).
- Function-style has no `init` form.
- `@static` properties omit `self`, emit `static` modifier.
- Decorators: `@virtual`, `@abstract`, `@override`, `@final`, `@private`,
  `@protected`, `@internal`, `@static`.
- Interfaces can declare property requirements using auto-property syntax
  (no body = abstract) or function-style with `...` body.
- Covariant return types on override (C# 9.0+).
- Explicit interface implementation: `property get IFace.name(self) -> T:`.

### Architecture

All compiler phases need changes:

1. **Lexer** -- `property` token already exists (`TokenType.Property`), and
   `get`, `set`, `init` are identifiers (not reserved keywords). No lexer
   changes needed.
2. **Parser** -- New AST node `PropertyDef`. New parsing in `Parser.Definitions.cs`.
3. **Semantic (NameResolver)** -- Register `PropertySymbol` on `TypeSymbol.Properties`.
4. **Semantic (TypeChecker)** -- Type-check property bodies, validate accessor
   type consistency, check constructor-only assignment rules for `get`/`init`.
5. **Validation** -- Property-specific validators (name conflicts with methods,
   decorator validation, interface property satisfaction).
6. **CodeGen** -- Emit auto-properties and function-style properties via
   `SyntaxFactory` in `RoslynEmitter.ClassMembers.cs`.

### Incremental Implementation Plan

#### Sub-task 1A: AST Node and Parser for Auto-Properties

**Goal**: Parse `property [get|set|init]? name: T [= value]` inside class,
struct, and interface bodies. No semantic analysis or codegen yet.

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs`
  -- Add `PropertyDef` record.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Definitions.cs`
  -- Add `ParsePropertyDef()` method.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.cs`
  -- Wire `TokenType.Property` into `ParseStatement()` switch and
  `ParseDecoratedStatement()` switch.

**AST node design**:

```csharp
/// <summary>
/// Property definition (auto-property or function-style).
/// </summary>
public record PropertyDef : Statement
{
    public string Name { get; init; } = "";

    /// <summary>
    /// The accessor modifier: None (get+set), Get, Set, or Init.
    /// For function-style, this is always Get or Set.
    /// </summary>
    public PropertyAccessor Accessor { get; init; } = PropertyAccessor.None;

    /// <summary>
    /// Type annotation. For auto-properties, always present.
    /// For function-style getters, derived from return type.
    /// For function-style setters, derived from value parameter.
    /// </summary>
    public TypeAnnotation? Type { get; init; }

    /// <summary>
    /// Default value for auto-properties (e.g., property name: str = "Unknown").
    /// </summary>
    public Expression? DefaultValue { get; init; }

    /// <summary>
    /// True if this is a function-style property (has parameters and body).
    /// False for auto-properties.
    /// </summary>
    public bool IsFunctionStyle { get; init; }

    /// <summary>
    /// Parameters for function-style properties. Includes self for instance
    /// properties, value for setters. Empty for auto-properties.
    /// </summary>
    public ImmutableArray<Parameter> Parameters { get; init; }
        = ImmutableArray<Parameter>.Empty;

    /// <summary>
    /// Return type annotation for function-style getters.
    /// </summary>
    public TypeAnnotation? ReturnType { get; init; }

    /// <summary>
    /// Body for function-style properties.
    /// Empty for auto-properties.
    /// </summary>
    public ImmutableArray<Statement> Body { get; init; }
        = ImmutableArray<Statement>.Empty;

    /// <summary>
    /// Decorators applied to this property (@virtual, @abstract, etc.).
    /// </summary>
    public ImmutableArray<Decorator> Decorators { get; init; }
        = ImmutableArray<Decorator>.Empty;

    /// <summary>
    /// For explicit interface implementation: the interface name prefix
    /// (e.g., "ISecret" in "property get ISecret.name(self) -> T:").
    /// Null for normal properties.
    /// </summary>
    public string? ExplicitInterface { get; init; }

    // ... ValidateInvariants, GetChildNodes overrides ...
}

public enum PropertyAccessor
{
    None,  // property name: T  (get + set)
    Get,   // property get name: T  or  property get name(self) -> T:
    Set,   // property set name: T  or  property set name(self, value: T):
    Init   // property init name: T (auto-property only)
}
```

**Parser logic** (in `ParsePropertyDef()`):

1. Consume `TokenType.Property`.
2. Peek at next token. If it is an identifier matching `"get"`, `"set"`, or
   `"init"`, consume it and record the accessor modifier.
3. Read the property name (identifier). For explicit interface implementation,
   check for `Name.Name` pattern (dotted name before the property name).
4. If next token is `(` -- this is function-style. Parse parameter list, `->`,
   return type, `:`, and body.
5. If next token is `:` -- this is auto-property. Parse type annotation,
   optional `= value`.
6. Return `PropertyDef` with appropriate fields set.

**Important**: In `ParseStatement()`, add `TokenType.Property =>
ParsePropertyDef()` to the switch. In `ParseDecoratedStatement()`, add
`TokenType.Property => ParsePropertyDef()` alongside `Def`, `Class`, `Struct`.
Update the decorator attachment logic to handle `PropertyDef`.

**Testing**: Write parser unit tests that verify the AST shape for each
auto-property variant and function-style variant. Use `emit ast` CLI command
to inspect output.

**Commit message**: `feat: Add PropertyDef AST node and parser for property declarations`

#### Sub-task 1B: CodeGen for Auto-Properties

**Goal**: Emit C# auto-properties from `PropertyDef` AST nodes (auto-property
form only). Function-style is deferred.

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`
  -- Add `case PropertyDef:` in `GenerateClassMembers()` and
  `GenerateInterfaceMembers()`. Add `GenerateAutoProperty()` method.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`
  -- May need minor adjustments if struct codegen differs.

**CodeGen logic for `GenerateAutoProperty(PropertyDef prop)`**:

1. Map type via `_typeMapper.MapType(prop.Type)`.
2. Mangle name via `NameMangler.ToPascalCase(prop.Name)`.
3. Build accessor list based on `prop.Accessor`:
   - `None` -> `{ get; set; }`
   - `Get` -> `{ get; }`
   - `Set` -> `{ set; }`
   - `Init` -> `{ get; init; }`
4. Apply modifiers from decorators via `GenerateMethodModifiersFromDecorators()`.
5. If `prop.DefaultValue` is not null, add initializer via
   `EqualsValueClause(GenerateExpression(prop.DefaultValue))`.
6. Return `PropertyDeclarationSyntax`.

**For interfaces**: Auto-properties with no default value emit abstract property
accessors (semicolon-only). Auto-properties with a default value emit a
default implementation.

**Constructor integration**: In `GenerateConstructor()`, assignments like
`self.name = value` where `name` is a property should resolve to
`this.Name = value` using the same `fieldMapping` dictionary. Extend the
mapping to include property names.

**Decision guidance**: The existing `GenerateField()` method returns a
`FieldDeclarationSyntax`. Properties return `PropertyDeclarationSyntax`.
These are both `MemberDeclarationSyntax`, so they slot into the existing
`members` list naturally. Do NOT try to reuse `GenerateField()` -- properties
have fundamentally different syntax.

**Testing**: Create integration test fixtures:
- `properties/auto_property_basic.spy` + `.expected` -- read-write property
- `properties/auto_property_get_only.spy` + `.expected` -- get-only
- `properties/auto_property_init.spy` + `.expected` -- init-only
- `properties/auto_property_default.spy` + `.expected` -- with default value

**Commit message**: `feat: Emit C# auto-properties from PropertyDef AST nodes`

#### Sub-task 1C: Function-Style Properties and Mixed Access

**Goal**: Emit function-style properties with custom getter/setter bodies.
Support mixed access modifiers (public getter, private setter).

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs`
  -- Add `GenerateFunctionStyleProperty()`. Modify `GenerateClassMembers()`
  to group getter/setter `PropertyDef` nodes for the same name into a single
  C# property declaration.

**Grouping logic**: Multiple `PropertyDef` nodes with the same name
(e.g., one `Get` and one `Set`) must be combined into a single C# property
with both accessors. The algorithm:

1. In `GenerateClassMembers()`, collect all `PropertyDef` nodes.
2. Group by property name.
3. For each group:
   - If all are auto-properties, emit a single auto-property (validate only
     one modifier per name).
   - If any are function-style, emit a function-style property combining
     the getter body and setter body.
   - Validate that there is no mix of auto and function-style for the same name.
4. Each accessor can have its own access modifier from decorators.

**Mixed access modifier codegen**:
```csharp
// property get value(self) -> int:    (public by default)
// @private
// property set value(self, v: int):   (private)
//
// Emits:
public int Value {
    get { return _value; }
    private set { _value = value; }
}
```

In Roslyn SyntaxFactory, the accessor-level modifier goes on the
`AccessorDeclarationSyntax`, not on the `PropertyDeclarationSyntax`.

**Static properties**: If `@static` decorator is present, add
`SyntaxKind.StaticKeyword` to the property modifiers. Verify that the
function-style property does NOT have a `self` parameter (same pattern
as static methods).

**Testing**:
- `properties/function_style_getter.spy` + `.expected`
- `properties/function_style_getter_setter.spy` + `.expected`
- `properties/mixed_access.spy` + `.expected`
- `properties/static_property.spy` + `.expected`

**Commit message**: `feat: Emit function-style properties with mixed access modifiers`

#### Sub-task 1D: Semantic Analysis and Validation

**Goal**: Register properties in `PropertySymbol`, type-check property bodies,
validate accessor rules, support `@virtual`/`@abstract`/`@override`/`@final`.

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Symbol.cs`
  -- Extend `PropertySymbol` with `HasInit`, `IsStatic`, `IsVirtual`,
  `IsAbstract`, `IsOverride`, `IsFinal`, `ExplicitInterface` fields.
  Add `AccessLevel Internal` to the `AccessLevel` enum (currently missing;
  needed for `@internal` decorator).
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/NameResolver.cs`
  -- Add `ResolvePropertyDeclaration()` method, called from
  `ResolveClassDeclaration()`, `ResolveStructDeclaration()`,
  `ResolveInterfaceDeclaration()` when a `PropertyDef` is encountered.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs`
  (or the `.Definitions.cs` partial) -- Add property body type-checking
  (getter must return correct type, setter parameter must match property type).
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Validation/`
  -- Add property-related validation rules (or extend `DecoratorValidator`
  and `SignatureValidator`).
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`
  -- Add new diagnostic codes for property errors (e.g., `PropertyNameConflict`,
  `MixedAutoAndFunctionStyle`, `InvalidPropertyModifier`).

**NameResolver integration**: In `ResolveClassDeclaration()`, the loop over
`classDef.Body` currently handles `FunctionDef` and `VariableDeclaration`.
Add a `PropertyDef` case:

```csharp
else if (statement is PropertyDef prop)
{
    ResolvePropertyDeclaration(prop, typeSymbol);
}
```

`ResolvePropertyDeclaration()` creates a `PropertySymbol` and adds it to
`typeSymbol.Properties`. For function-style properties, register the property
body as a scope for type-checking.

**Inheritance validation**: When `@override` is present, look up the base
class's `Properties` list and verify:
- A property with the same name exists and is `virtual` or `abstract`.
- The type is compatible (covariant return for getters is allowed in C# 9.0+).
- The overriding accessor is not more restrictive than the base.

**Validation rules to add**:
- A class cannot have both a property and a method with the same name.
- A class cannot have both a property and a field with the same name.
- Auto-property and function-style accessors for the same name are not allowed.
- `property init` is only valid for auto-properties, not function-style.
- In interfaces, function-style properties must have `...` body.
- `@abstract` properties must have `...` body.
- `@final` cannot be combined with `@abstract` or `@virtual`.

**Testing**:
- Error test fixtures for invalid combinations
- `properties/abstract_override.spy` + `.expected`
- `properties/interface_property.spy` + `.expected`
- `properties/covariant_return.spy` + `.expected`

**Commit message**: `feat: Add semantic analysis and validation for property declarations`

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Grouping getter/setter into one C# property is complex | High | Medium | Start with single-accessor properties, add grouping in 1C |
| Constructor `self.x = v` assignment must work for properties | High | High | Extend the `fieldMapping` dict in `GenerateConstructor()` early |
| `init` accessor requires C# 9.0 features | Low | Medium | Sharpy.Core already targets C# 9.0; ensure generated code does too |
| Explicit interface implementation (`IFace.name`) | Medium | Low | Defer to a follow-up if complex; mark with TODO |
| Interactions with existing field codegen | High | High | Ensure the field/property distinction is clean in the first pass |

---

## Feature 2: `with` Statement (IDisposable)

### Context and Motivation

The `with` statement enables deterministic resource cleanup, critical for .NET
interop (file handles, database connections, network streams). Without it,
Sharpy cannot express `using` blocks, which are fundamental .NET patterns.

### Spec Summary

From `context_managers.md` and `dotnet_interop.md`:

```python
with open("file.txt", "r") as f:
    content = f.read()
# f.Dispose() called automatically

# Multiple resources
with open("in.txt") as input, open("out.txt", "w") as output:
    output.write(input.read())
```

C# mapping: `with expr as name:` -> `using (var name = expr) { ... }`

The object must implement `IDisposable`. The `Dispose()` method is called
when the `with` block exits (even on exceptions).

### Architecture

1. **Lexer** -- `with` and `as` tokens already exist (`TokenType.With`,
   `TokenType.As`). No changes.
2. **Parser** -- New AST node `WithStatement`. New `ParseWithStatement()`.
3. **Semantic** -- Type-check that the expression type implements `IDisposable`.
4. **Validation** -- None specific (covered by type checking).
5. **CodeGen** -- Emit `using` statement via `SyntaxFactory`.

### Incremental Implementation Plan

#### Sub-task 2A: AST Node and Parser

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs`
  -- Add `WithStatement` record and `WithItem` record.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Statements.cs`
  -- Add `ParseWithStatement()`.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.cs`
  -- Wire `TokenType.With => ParseWithStatement()` in `ParseStatement()`.

**AST node design**:

```csharp
/// <summary>
/// With statement (context manager / IDisposable)
/// </summary>
public record WithStatement : Statement
{
    public ImmutableArray<WithItem> Items { get; init; }
        = ImmutableArray<WithItem>.Empty;
    public ImmutableArray<Statement> Body { get; init; }
        = ImmutableArray<Statement>.Empty;
}

public record WithItem
{
    public Expression ContextExpression { get; init; } = null!;
    public string? Name { get; init; }  // The "as name" binding

    // Source location
    public int LineStart { get; init; }
    public int ColumnStart { get; init; }
    public int LineEnd { get; init; }
    public int ColumnEnd { get; init; }
    public Text.TextSpan? Span { get; init; }
}
```

**Parser logic** (`ParseWithStatement()`):

1. Consume `TokenType.With`.
2. Parse one or more `WithItem` separated by commas:
   - Parse expression.
   - If `TokenType.As`, consume and parse identifier name.
3. Consume `:`, newline, indent.
4. Parse block body.
5. Consume dedent.

This follows the exact same pattern as `ParseTryStatement()` and
`ParseForStatement()`.

**Testing**: Parser unit tests verifying AST shape for single and multiple
`with` items.

**Commit message**: `feat: Add WithStatement AST node and parser`

#### Sub-task 2B: Semantic Analysis and CodeGen

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs`
  (or `.Statements.cs` partial) -- Add `case WithStatement:` in statement
  type-checking. Verify expression type implements `IDisposable`.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs`
  -- Add `case WithStatement:` in `GenerateBodyStatement()`. Emit
  `UsingStatement` via `SyntaxFactory`.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`
  -- Add `NotDisposable` diagnostic code.

**CodeGen logic**:

Single item:
```csharp
// with expr as name:  ->  using (var name = expr) { ... }
UsingStatement(
    VariableDeclaration(
        IdentifierName("var"))
        .WithVariables(SingletonSeparatedList(
            VariableDeclarator(name)
                .WithInitializer(EqualsValueClause(exprSyntax)))),
    Block(bodyStatements))
```

Multiple items: Nest `using` statements. The outermost `using` wraps the
next, forming a nested chain:
```csharp
using (var input = Open("in.txt"))
using (var output = Open("out.txt", "w"))
{
    // body
}
```

Without `as` clause (no variable binding): Use `using` statement with
expression form (C# 8.0+):
```csharp
using (expr) { ... }
```

**Decision guidance**: For the IDisposable check, the simplest approach is
to skip the check in v1 and rely on C# compilation errors. This keeps the
initial implementation small. Add the semantic check as a follow-up, using
the same pattern as how other interface checks work (look up the type in
`SemanticInfo`, check if it has a `Dispose()` method or implements the
`IDisposable` interface).

**Testing**:
- `with_statement/basic.spy` + `.expected`
- `with_statement/multiple_items.spy` + `.expected`
- `with_statement/no_as_clause.spy` + `.expected`

**Commit message**: `feat: Emit using statements for with blocks`

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| IDisposable check requires CLR type discovery | Medium | Low | Defer check; rely on C# compiler errors initially |
| Multiple items nesting is fiddly | Low | Low | Generate iteratively from inside out |
| `with` without `as` may need special handling | Low | Low | C# supports `using (expr) { }` directly |

---

## Feature 3: `ref`/`out`/`in` Parameter Modifiers

### Context and Motivation

Parameter modifiers are essential for .NET interop. Many .NET APIs use `out`
parameters (e.g., `int.TryParse(string, out int)`), and `ref`/`in` are
needed for efficient value-type passing. Without these, Sharpy cannot call
or implement a large class of .NET methods.

### Spec Summary

From `parameter_modifiers.md`:

| Modifier | Sharpy Syntax | C# Syntax |
|----------|---------------|-----------|
| `ref[T]` | `def swap(a: ref[int], b: ref[int]):` | `void Swap(ref int a, ref int b)` |
| `out[T]` | `def try_parse(s: str, result: out[int]) -> bool:` | `bool TryParse(string s, out int result)` |
| `in[T]` | `def analyze(data: in[LargeStruct]) -> float:` | `float Analyze(in LargeStruct data)` |

Call-site syntax mirrors the declaration:
- `swap(ref x, ref y)` -> `Swap(ref x, ref y);`
- `try_parse("42", out value: int)` -> `TryParse("42", out int value);`
- `analyze(in large_data)` -> `Analyze(in largeData);` (or just `Analyze(largeData)`)

Inline `out` declaration: `out value: int` or `out value: auto`.

### Architecture

1. **Lexer** -- `ref` and `out` are not currently reserved keywords and
   are parsed as identifiers. `in` is already a reserved keyword
   (`TokenType.In`, used for `for x in items`). They will be recognized as
   special names in the type annotation context (`ref[T]`, `out[T]`,
   `in[T]`). The `ref` and `out` keywords at call sites also need to be
   recognized. Consider adding `TokenType.Ref` and `TokenType.Out` as
   reserved keywords, or handle them as contextual identifiers in the parser.
   `TokenType.In` already exists but may need special handling in type
   annotation and call-site contexts.
2. **Parser** -- Modify type annotation parsing to recognize `ref[T]`,
   `out[T]`, `in[T]` as modifier wrappers. Modify function call argument
   parsing to handle `ref expr`, `out expr`, `out name: type`.
3. **Semantic** -- Track parameter modifiers in `ParameterSymbol`. Validate
   constraints (no `ref` with defaults, no `ref` with `*args`, etc.).
4. **CodeGen** -- Emit `ref`/`out`/`in` modifiers on parameter declarations
   and at call sites.

### Incremental Implementation Plan

#### Sub-task 3A: Type Annotation and Parameter Parsing

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs`
  -- Add `ParameterModifier` enum to `Parameter` record:
  ```csharp
  public enum ParameterModifier { None, Ref, Out, In }
  ```
  Add `ParameterModifier Modifier { get; init; } = ParameterModifier.None;`
  to the `Parameter` record.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Types.cs`
  -- Modify `ParseTypeAnnotation()` to detect `ref[T]`, `out[T]`, `in[T]`
  patterns. When the type name is `ref`, `out`, or `in` and has exactly one
  type argument, set the modifier on the containing parameter and return
  the inner type.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Statements.cs`
  -- Modify `ParseParameters()` (defined in this file) to propagate the
  modifier from the type annotation to the `Parameter` record.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Expression.cs`
  -- Add `RefArgument` expression node for call-site `ref expr`, `out expr`,
  `in expr`:
  ```csharp
  public record RefArgument : Expression
  {
      public ParameterModifier Modifier { get; init; }
      public Expression Argument { get; init; } = null!;
      // For inline out declaration: out value: int
      public string? InlineName { get; init; }
      public TypeAnnotation? InlineType { get; init; }
  }
  ```
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Expressions.cs`
  or `Parser.Primaries.cs` -- Handle `ref`/`out`/`in` at call argument positions.

**Decision guidance -- keyword vs. contextual identifier**: The simplest
approach is to treat `ref`, `out`, `in` as **contextual keywords** -- they
are only special in type annotation context (`ref[T]`) and call argument
context (`ref expr`). This avoids breaking any existing code that might use
`ref` as a variable name. In the parser, check if the current identifier is
`"ref"`/`"out"`/`"in"` followed by `[` (type context) or followed by an
expression (call context). This is the pattern used by `"new"` in constraint
parsing already.

**Testing**: Parser tests verifying correct `Parameter.Modifier` values and
`RefArgument` AST nodes at call sites.

**Commit message**: `feat: Parse ref/out/in parameter modifiers and call-site arguments`

#### Sub-task 3B: Semantic Analysis and CodeGen

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Symbol.cs`
  -- Add `ParameterModifier` to `ParameterSymbol`.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs`
  -- Validate modifier constraints (no defaults, no `*args`, no lambdas).
  For `out` parameters, ensure all code paths assign before return.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs`
  -- In `GenerateParameter()`, add `ref`/`out`/`in` modifier token.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`
  -- In function call generation, when encountering `RefArgument`, emit the
  appropriate modifier keyword at the call site.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs`
  -- Add diagnostics: `RefWithDefault`, `RefWithVariadic`, `RefInLambda`.

**CodeGen for parameters**:

```csharp
// In GenerateParameter():
if (param.Modifier != ParameterModifier.None)
{
    var modifierKind = param.Modifier switch
    {
        ParameterModifier.Ref => SyntaxKind.RefKeyword,
        ParameterModifier.Out => SyntaxKind.OutKeyword,
        ParameterModifier.In => SyntaxKind.InKeyword,
        _ => throw new InvalidOperationException()
    };
    paramSyntax = paramSyntax.WithModifiers(
        TokenList(Token(modifierKind)));
}
```

**CodeGen for call-site arguments**:

```csharp
// When generating a FunctionCall argument that is RefArgument:
case RefArgument refArg:
    var argExpr = GenerateExpression(refArg.Argument);
    var refKind = refArg.Modifier switch
    {
        ParameterModifier.Ref => SyntaxKind.RefKeyword,
        ParameterModifier.Out => SyntaxKind.OutKeyword,
        ParameterModifier.In => SyntaxKind.InKeyword,
        _ => throw new InvalidOperationException()
    };
    return Argument(argExpr)
        .WithRefKindKeyword(Token(refKind));
```

For inline `out` declarations (`out value: int`), emit a
`DeclarationExpression`:

```csharp
Argument(
    DeclarationExpression(
        typeSyntax,
        SingleVariableDesignation(Identifier(mangledName))))
    .WithRefKindKeyword(Token(SyntaxKind.OutKeyword))
```

**Testing**:
- `ref_out_in/ref_swap.spy` + `.expected`
- `ref_out_in/out_try_parse.spy` + `.expected`
- `ref_out_in/in_readonly.spy` + `.expected`
- `ref_out_in/inline_out_declaration.spy` + `.expected`

**Commit message**: `feat: Emit ref/out/in parameter modifiers and call-site keywords`

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| `ref`/`out`/`in` clash with user variable names | Medium | Medium | Use contextual parsing (only special in type/call context) |
| Inline `out` declaration is a novel parse pattern | Medium | Medium | Model after how `VariableDeclaration` parsing works |
| `out` definite-assignment validation is complex | High | Low | Defer to C# compiler for validation initially |
| Interaction with function overloading | Low | Low | `ref[T]` and `out[T]` are distinct types for overloading |

---

## Feature 4: Collection Type Wrappers

### Context and Motivation

Currently, `list[T]` maps to `System.Collections.Generic.List<T>` and
`set[T]` maps to `HashSet<T>`. The Sharpy.Core library already has
`Sharpy.List<T>` and `Sharpy.Set<T>` classes that wrap the .NET types and
provide Pythonic APIs (negative indexing, `append()`, `remove()`, etc.).
`dict[K,V]` already maps to `Sharpy.Dict<K,V>`. The goal is to wire up
`list` and `set` the same way.

### Spec Summary

No separate spec document. The change is to update `TypeMapper` mappings
so that:

| Sharpy | Current C# | Target C# |
|--------|-----------|-----------|
| `list[T]` | `System.Collections.Generic.List<T>` | `Sharpy.List<T>` |
| `set[T]` | `System.Collections.Generic.HashSet<T>` | `Sharpy.Set<T>` |
| `dict[K,V]` | `Sharpy.Dict<K,V>` (already correct) | No change |

### Architecture

This is primarily a CodeGen change with some semantic follow-up.

1. **Lexer/Parser** -- No changes.
2. **Semantic** -- The `BuiltinRegistry` and type inference may need to be
   aware that `list[T]` methods use `Sharpy.List<T>` method names (which
   are already Pythonic: `append`, `extend`, etc.).
3. **CodeGen** -- Change two lines in `TypeMapper`.
4. **Tests** -- Many existing tests will produce different output (method
   names, constructor calls). This is the biggest risk.

### Incremental Implementation Plan

#### Sub-task 4A: Verify Sharpy.Core Compatibility

**Goal**: Before changing any mappings, verify that `Sharpy.List<T>` and
`Sharpy.Set<T>` implement all interfaces required by existing codegen patterns.

**Files to read**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Core/Partial.List/List.cs` --
  Constructor signatures.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Core/Partial.List/List.Interfaces.cs` --
  Interface implementations (`IList<T>`, `IReadOnlyList<T>`, `IEnumerable<T>`).
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Core/Partial.List/List.Methods.cs` --
  Available methods.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Core/Partial.Set/Set.cs`,
  `Set.Interfaces.cs`, `Set.Methods.cs` -- Same for Set.

**Checklist**:
- [x] `Sharpy.List<T>` implements `IList<T>` -- yes (from `List.cs` line 9)
- [x] `Sharpy.List<T>` implements `IReadOnlyList<T>` -- yes (line 10)
- [x] `Sharpy.List<T>` implements `IEnumerable<T>` -- yes (via `IList<T>`)
- [x] `Sharpy.List<T>` has `Add(T)` for collection initializers -- yes (line 42)
- [x] `Sharpy.Set<T>` implements `ISet<T>` -- yes (from `Set.cs` line 5)
- [x] `Sharpy.Set<T>` implements `IEnumerable<T>` -- yes (via `ISet<T>`)

**Key concern**: LINQ extension methods (`Where`, `Select`, `OrderBy`, etc.)
work on `IEnumerable<T>`, which both wrappers implement. `foreach` works
via `IEnumerable<T>`. Collection initializers work via `Add()`. These are
the three main codegen patterns.

**No code changes in this sub-task.** Document findings and proceed.

**Commit message**: N/A (research only)

#### Sub-task 4B: Change TypeMapper Mappings

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/TypeMapper.cs`
  -- Change two lines in the static constructor:
  ```csharp
  // Before:
  _builtinTypeMap["list"] = "System.Collections.Generic.List";
  _builtinTypeMap["set"] = "System.Collections.Generic.HashSet";

  // After:
  _builtinTypeMap["list"] = "Sharpy.List";
  _builtinTypeMap["set"] = "Sharpy.Set";
  ```

**Also check**: The `Discovery/TypeMapper.cs` (reverse mapping from CLR types
to Sharpy types) may need updating. Search for `System.Collections.Generic.List`
references in that file.

**Testing**: Run the full test suite. Many `.expected` files will need
updating because the generated C# will reference `Sharpy.List<T>` instead
of `System.Collections.Generic.List<T>`. Also, constructor calls like
`new System.Collections.Generic.List<int>()` become `new Sharpy.List<int>()`.

Use `UPDATE_SNAPSHOTS=true dotnet test --filter "FullyQualifiedName~FileBasedIntegrationTests"`
to regenerate `.expected.cs` snapshot files.

**Decision guidance**: This is a high-blast-radius change because many test
fixtures reference `System.Collections.Generic.List` in their expected output.
Consider:
1. Making this change on a dedicated branch.
2. Running the full test suite and batch-updating expected files.
3. Reviewing each changed `.expected` file to ensure correctness.

**Risk**: Some codegen patterns may emit method calls like `.Add()` (Pascal
case) that work on `System.Collections.Generic.List<T>` but might differ on
`Sharpy.List<T>`. Verify that the name mangler's output matches
`Sharpy.List<T>`'s method signatures.

**Commit message**: `feat: Map list and set types to Sharpy.Core wrappers`

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Many tests break from different type names in output | Certain | High | Batch update; review carefully |
| Some List/Set methods have different signatures | Medium | High | Audit Sharpy.Core method signatures against generated code |
| LINQ compatibility issues | Low | Medium | Both wrappers implement IEnumerable<T> |
| Constructor call syntax differences | Medium | Medium | Sharpy.List has same constructors (parameterless + IEnumerable) |

---

## Feature 5: Spread Operator

### Context and Motivation

Spread operators (`*expr` in lists/tuples/sets, `**expr` in dicts) enable
concise collection construction and function argument forwarding. This is
a core Python pattern that Sharpy currently lacks entirely.

### Spec Summary

From `spread_operator.md`:

| Context | Syntax | C# Lowering |
|---------|--------|-------------|
| List literal | `[*a, *b]` | `new List<T>(); .AddRange(a); .AddRange(b)` |
| Dict literal | `{**a, **b}` | `new Dict<K,V>(a); foreach(kvp in b) d[k]=v` |
| Set literal | `{*a, *b}` | `new Set<T>(); .UnionWith(a); .UnionWith(b)` |
| Tuple literal | `(*a, *b)` | Tuple concatenation helper |
| Function call | `f(*args)` | Expand to positional arguments |
| Function call | `f(**kwargs)` | Expand to keyword arguments |
| Unpacking | `first, *rest = items` | Destructuring with rest |

### Architecture

1. **Lexer** -- `*` and `**` tokens already exist (`TokenType.Star`,
   `TokenType.DoubleStar`). No changes.
2. **Parser** -- New AST node `SpreadExpression`. Modify collection literal
   parsing and function call argument parsing to recognize `*expr` / `**expr`.
3. **Semantic** -- Type-check that spread expressions are iterable (for `*`)
   or mappings (for `**`). Infer element types.
4. **CodeGen** -- Lower spread expressions in collection literals to
   `AddRange()`/loop patterns. Lower spread in function calls to argument
   expansion.

### Incremental Implementation Plan

#### Sub-task 5A: AST Node and Parser

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Expression.cs`
  -- Add `SpreadExpression`:
  ```csharp
  /// <summary>
  /// Spread expression (*expr or **expr)
  /// </summary>
  public record SpreadExpression : Expression
  {
      public Expression Operand { get; init; } = null!;
      /// <summary>
      /// True for **expr (dict spread), false for *expr (iterable spread).
      /// </summary>
      public bool IsDoubleSpread { get; init; }
  }
  ```
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Primaries.cs`
  -- Modify list literal, set literal, dict literal, and tuple literal parsing
  to check for `TokenType.Star` / `TokenType.DoubleStar` before parsing each
  element. If found, wrap the following expression in `SpreadExpression`.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Expressions.cs`
  -- Modify function call argument parsing to handle `*expr` and `**expr`
  in the arguments list.

**Parsing approach for list literals**: In the existing list literal parser,
before parsing each element expression, check:

```csharp
if (Current.Type == TokenType.Star)
{
    var starToken = Current;
    Advance();
    var operand = ParseExpression();
    elements.Add(new SpreadExpression
    {
        Operand = operand,
        IsDoubleSpread = false,
        // ... source location
    });
}
else
{
    elements.Add(ParseExpression());
}
```

Similarly for `**` in dict literals and `*` in set/tuple literals.

**Testing**: Parser unit tests for each collection type with spread elements
and function calls with spread arguments.

**Commit message**: `feat: Parse spread expressions in collection literals and function calls`

#### Sub-task 5B: CodeGen for Collection Spreading

**Goal**: Emit `AddRange()`/loop-based lowering for spread in list, dict,
set literals.

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`
  -- Modify list literal, dict literal, and set literal generation.

**Lowering strategy for list literals**:

When a `ListLiteral` contains any `SpreadExpression` elements, the codegen
changes from a simple collection initializer to a multi-statement pattern:

```python
combined = [1, 2, *middle, 99]
```

```csharp
var combined = new Sharpy.List<int>();
combined.Add(1);
combined.Add(2);
combined.AddRange(middle);
combined.Add(99);
```

This requires generating a temporary variable and multiple statements.
The challenge is that list literals can appear in expression context (not
just statement context). To handle this, use a block expression pattern or
hoist to a local variable.

**Decision guidance -- expression context**: When a spread-containing
collection literal appears in expression position (e.g., `return [*a, *b]`
or `foo([*a, *b])`), you need to either:

1. **Hoist to a local variable** before the containing statement (simpler,
   used by many compilers).
2. **Use an IIFE** (`((Func<List<int>>)(() => { ... }))()`) -- ugly but
   self-contained.

Recommend option 1. Add a mechanism to the emitter to "prepend" statements
before the current expression's containing statement. This pattern already
exists conceptually in the emitter for other lowering (e.g., comprehensions).

**Dict literal lowering**:
```python
merged = {**defaults, "key": val}
```
```csharp
var merged = new Sharpy.Dict<string, object>(defaults);
merged["key"] = val;
```

**Set literal lowering**:
```python
combined = {*set1, *set2, extra}
```
```csharp
var combined = new Sharpy.Set<int>();
combined.UnionWith(set1);
combined.UnionWith(set2);
combined.Add(extra);
```

**Testing**:
- `spread/list_spread.spy` + `.expected`
- `spread/dict_spread.spy` + `.expected`
- `spread/set_spread.spy` + `.expected`
- `spread/spread_in_expression.spy` + `.expected`

**Commit message**: `feat: Emit lowered code for spread in collection literals`

#### Sub-task 5C: Function Call Spreading (Defer)

**Goal**: Support `f(*args)` and `f(**kwargs)` at call sites.

This is significantly more complex because it requires compile-time knowledge
of the target function's parameter list to expand positional and keyword
arguments. **Recommend deferring** this to a later phase and focusing on
collection spreading first.

If implemented:
- `*args` where args has known length at compile time: expand to positional
  args via indexing (`args[0], args[1], ...`).
- `*args` where args has unknown length: requires runtime dispatch or
  params array.
- `**kwargs`: requires compile-time string-to-parameter mapping.

**Commit message**: `feat: Emit spread arguments in function calls`

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Expression-context hoisting is architecturally complex | High | High | Start with statement-context only; add hoisting incrementally |
| Type inference for spread element types | Medium | Medium | Defer type checking; rely on C# compiler |
| Function call spreading is very complex | High | High | Defer to a later phase |
| Performance of lowered code | Low | Low | Acceptable for v1; optimize later |

---

## Feature 6: Named Tuples

### Context and Motivation

Named tuples improve readability for multi-element tuples by providing field
names. They map directly to C# `ValueTuple` with named elements, which is
a zero-cost abstraction.

### Spec Summary

From `named_tuples.md`:

```python
type Point = tuple[x: float, y: float]
pos: Point = (x=1.0, y=2.0)
print(pos.x)  # 1.0
```

C# mapping:
```csharp
(double x, double y) pos = (x: 1.0, y: 2.0);
Console.WriteLine(pos.x);
```

Key rules:
- All fields must be named, or none (no partial naming).
- Field names are part of the type identity (different names = incompatible).
- Named tuples are immutable.
- Access by name (`pos.x`) and by position (`pos[0]`) both work.
- Named tuples work with pattern matching.
- Inline named tuple types allowed in function return types.

### Architecture

1. **Lexer** -- No changes. Named fields use `name:` syntax which is already
   parseable.
2. **Parser** -- Modify tuple type annotation parsing to capture field names.
   Modify tuple literal parsing for named construction (`(x=1.0, y=2.0)`).
3. **Semantic** -- Extend `TupleType` (in `SemanticType.cs`) with element
   names. Extend `TupleType` (in `Types.cs` AST) similarly.
4. **CodeGen** -- Emit named `ValueTuple` elements in type positions and
   named tuple construction.

### Incremental Implementation Plan

#### Sub-task 6A: Extend Type System and Parser

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs`
  -- Add `List<string?>? ElementNames` to the `TupleType` record. Null means
  unnamed (backward compatible). When present, must have same count as
  `ElementTypes`.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Types.cs`
  -- Add `ImmutableArray<string?> ElementNames` to the AST `TupleType` record.
  Default to empty (unnamed).
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Types.cs`
  -- Modify tuple type annotation parsing. When parsing `tuple[...]`,
  look for `name: type` pattern inside the brackets. If names are present,
  record them. Validate that either all or none are named.

**Parser logic for named tuple types**: Inside `ParseTypeAnnotation()`, when
parsing `tuple[...]` type arguments, check if each argument follows the
pattern `identifier : type`:

```
tuple[x: float, y: float]
```

The parser sees `x` (identifier) then `:` (colon) then `float` (type). This
is ambiguous with constraint syntax in type parameters. To disambiguate:
- In the `tuple[...]` context specifically, `name: type` means a named element.
- Track this via a flag or by checking that we are parsing inside a `tuple`
  type.

**Testing**: Parser tests for named tuple type annotations.

**Commit message**: `feat: Parse named tuple type annotations with element names`

#### Sub-task 6B: Named Tuple Construction and Access

**Files to modify**:

- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Expression.cs`
  -- The existing `TupleLiteral` has `Elements` but no element names. Add
  `ImmutableArray<string?> ElementNames` (default empty).
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Primaries.cs`
  -- Modify tuple literal parsing to handle `(x=1.0, y=2.0)` syntax. When
  the parser sees `identifier =` inside a parenthesized expression, treat
  it as a named tuple element. Be careful not to confuse with keyword
  arguments in function calls.
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs`
  -- When emitting tuple literals with names, use the `TupleExpression` syntax
  with named arguments:
  ```csharp
  TupleExpression(SeparatedList(new[] {
      Argument(expr).WithNameColon(NameColon("x")),
      Argument(expr).WithNameColon(NameColon("y"))
  }))
  ```
- `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/TypeMapper.cs`
  -- When mapping a `TupleType` with element names, emit named ValueTuple
  elements:
  ```csharp
  // (double x, double y) instead of (double, double)
  TupleType(SeparatedList(new[] {
      TupleElement(doubleType, Identifier("x")),
      TupleElement(doubleType, Identifier("y"))
  }))
  ```

**Decision guidance -- parsing ambiguity**: The syntax `(x=1.0, y=2.0)` looks
like keyword arguments. The key disambiguator is context:
- Inside a function call `f(x=1.0)` -- keyword argument.
- Assigned to a named-tuple-typed variable `p: Point = (x=1.0, y=2.0)` --
  named tuple.
- Standalone `(x=1.0, y=2.0)` -- could be either; resolve by looking at
  the target type context.

For the initial implementation, support named tuple construction only when
the target type is known (explicit type annotation). This avoids the
ambiguity entirely. Mark the general case as a TODO.

**Member access**: `pos.x` already parses as `MemberAccess`. In the
type checker / codegen, when the object type is a `TupleType` with element
names and the member matches an element name, generate the appropriate
field access. In C#, named tuple elements are accessed as fields directly
(e.g., `pos.x`), so the `MemberAccess` codegen should work as-is after
name mangling is handled correctly.

**Important**: C# named tuple element names are lowercase by convention
(matching the original Sharpy field names). Since Sharpy uses snake_case
and C# tuples preserve the exact names, do NOT mangle named tuple element
names to PascalCase. This is different from class properties/fields.

**Testing**:
- `named_tuples/basic.spy` + `.expected`
- `named_tuples/function_return.spy` + `.expected`
- `named_tuples/member_access.spy` + `.expected`

**Commit message**: `feat: Support named tuple construction and element access`

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Parsing ambiguity with keyword arguments | High | High | Support only typed context initially |
| TupleType equality must consider names | Medium | Medium | Update Equals/GetHashCode in SemanticType |
| C# ValueTuple name mangling differences | Medium | Medium | Do NOT PascalCase tuple element names |
| Pattern matching with named tuples | Low | Low | Defer to later phase |

---

## General Guidance

### Implementation Order

For every feature, implement components in this order:

```
Lexer -> Parser -> Semantic -> Validation -> CodeGen -> Tests
```

This ensures dependencies flow forward. You can commit after each phase
if the intermediate state compiles (even if the feature is not yet
end-to-end functional).

### Key Principles

1. **AST nodes are immutable records.** Annotations and computed data go in
   `SemanticInfo` (or `SemanticBinding`), not on the AST node itself. The
   exception is parser-level data like source locations and literal values.

2. **RoslynEmitter uses SyntaxFactory exclusively.** Never use string
   interpolation or template strings to generate C# code. Always construct
   the syntax tree using `SyntaxFactory` methods. Example:
   ```csharp
   // Correct:
   PropertyDeclaration(typeSyntax, "Name")
       .WithAccessorList(AccessorList(SingletonList(
           AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
               .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)))));

   // Incorrect:
   var code = $"public {type} Name {{ get; }}";
   ```

3. **Sharpy.Core targets C# 9.0 / netstandard2.1;netstandard2.0.** The generated code must
   also work within these constraints. The `init` accessor is available
   in C# 9.0. Record types are available. File-scoped namespaces and global
   usings are NOT available.

4. **When in doubt, look at existing patterns.** For properties, study how
   `GenerateField()`, `GenerateLenProperty()`, and `GenerateBoolProperty()`
   work in `RoslynEmitter.ClassMembers.cs`. For `with`, study how
   `ParseTryStatement()` works. For parameter modifiers, study how `Parameter`
   is parsed and how `GenerateParameter()` emits it.

5. **Prefer the smallest correct change.** Get the simplest case working
   end-to-end first (e.g., `property name: T` before function-style
   properties). Then iterate.

6. **Never modify `.expected` files to make tests pass.** Fix the
   implementation instead. The only exception is when the expected output
   *should* change (e.g., Feature 4 changes the type mappings).

7. **Verify Python behavior first** when implementing Python-like semantics.
   Run `python3 -c "..."` to confirm expected behavior before coding.

8. **Language spec is authoritative.** If the spec says one thing and the
   code does another, change the code. If the spec is ambiguous, ask before
   inventing behavior.

9. **Add diagnostic codes for new errors.** All error messages need a code
   from `DiagnosticCodes.cs`. Follow the existing code ranges:
   - `SPY0100-SPY0199` for parser errors
   - `SPY0200-SPY0399` for semantic errors
   - `SPY0400-SPY0499` for validation errors
   - `SPY0500-SPY0599` for codegen errors

10. **TODO/BUG/FIXME comments must have GitHub issues.** When deferring
    functionality, create an issue first and reference it:
    `// TODO(#123): Support spread in function call arguments`.

### Testing Strategy

For each sub-task:

1. **Unit tests** for the specific component (parser tests, type checker
   tests, codegen tests).
2. **Integration test fixtures** (`.spy` + `.expected` pairs) that verify
   end-to-end behavior. Place in
   `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler.Tests/Integration/TestFixtures/{feature_name}/`.
3. **Error test fixtures** (`.spy` + `.error` pairs) for invalid input
   that should produce specific error messages.
4. Run `dotnet test` to verify nothing regresses.
5. Run `dotnet format whitespace` before committing.

### File Reference

Key files referenced across multiple features:

| File | Purpose |
|------|---------|
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Statement.cs` | AST statement nodes |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Expression.cs` | AST expression nodes |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Ast/Types.cs` | Type annotation AST |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.cs` | Main parser (statement dispatch) |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Definitions.cs` | Class/struct/function parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Statements.cs` | Control flow statement parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Expressions.cs` | Expression parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Primaries.cs` | Literal and primary parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Parser/Parser.Types.cs` | Type annotation parsing |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/Symbol.cs` | Symbol hierarchy |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/SemanticType.cs` | Type hierarchy |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/NameResolver.cs` | Name resolution pass |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Type checking pass |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.ClassMembers.cs` | Class member codegen |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs` | Function/type codegen |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs` | Expression codegen |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/RoslynEmitter.Statements.cs` | Statement codegen |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/TypeMapper.cs` | Sharpy-to-C# type mapping |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/CodeGen/NameMangler.cs` | Name mangling rules |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Diagnostics/DiagnosticCodes.cs` | Error code catalog |
| `/Users/anton/Documents/github/sharpy/src/Sharpy.Compiler/Lexer/Token.cs` | Token type enum |

### Recommended Feature Order

Based on dependencies and blast radius:

1. **Feature 2: `with` statement** -- Smallest scope, no type system changes,
   high .NET interop value. Good warmup.
2. **Feature 1: Properties** -- Largest feature but well-specified. Start
   with sub-task 1A/1B (auto-properties) which is self-contained.
3. **Feature 3: `ref`/`out`/`in`** -- Important for .NET interop, moderate
   complexity.
4. **Feature 6: Named tuples** -- Mostly additive, low blast radius.
5. **Feature 5: Spread operator** -- Complex lowering, can be deferred.
6. **Feature 4: Collection wrappers** -- Simple code change but high test
   churn. Do this when test infrastructure is stable.
