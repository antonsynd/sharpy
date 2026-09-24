# Module System

## Package Structure

Packages are directories containing an optional `__init__.spy` file:

```
project/
    utils/
        __init__.spy      # Optional, can be empty
        helpers.spy
        math/
            __init__.spy
            vectors.spy
```

The `__init__.spy` file can re-export symbols for convenient imports:

<!-- spec-sweep: fragment -->
```python
# utils/__init__.spy
from utils.helpers import format_string, parse_input
from utils.math.vectors import Vector2, Vector3
```

**A package directory may not spell the same identifier as a module file inside it.** Each
directory above a source file becomes a C# wrapper class, and the file becomes the module class
nested in it — so `lib/lib.spy` would nest module class `Lib` in wrapper `Lib`, which C# forbids
(CS0542). The project refuses such a layout by name (`SPY0526`: "Package directory 'lib' and
module 'lib.spy' both emit the C# identifier 'Lib'... Rename the file or the directory"). The rule
compares the *emitted* identifiers, so it also catches spellings that mangle alike
(`my_lib/myLib.spy`) and nested packages (`a/b/b.spy`); directories are measured from the project's
common source directory, and a file is checked whether or not anything imports it. A package
whose directory differs from its modules (`pkg/lib.spy`), or that has only an `__init__.spy`,
builds normally. (A later change emitting packages as namespaces is expected to relax this.)

## Name Qualification in Generated C#

Every reference the compiler emits to a **module-level member** (a module function, variable, or
constant — imported or defined in the same module) and to a **same-file type** is fully
`global::`-qualified through its module class. Nothing is bound by an ambient `using static` or
`using <alias> = ...` directive; those directives are not emitted.

```python
def greet(name: str) -> str:
    return "Hello, " + name

def main() -> None:
    # `greet` is a module-level member, so the call is emitted as
    # global::<ModuleClass>.Greet("world") — never a bare `Greet(...)`.
    print(greet("world"))
```

Because every such reference is `global::`-rooted rather than resolved by name lookup, a
module-level name is free to equal the root namespace, a namespace segment, or the module class
itself without a C# name collision. For example, a project whose root namespace is `Poison` may
define `def poison()` in `lib.poison` and call it from another module — the call is emitted
`global::Poison.Lib.Poison()`, so it does not collide with the `Poison` namespace (which, before
universal qualification, produced a `CS0118` "is a namespace but is used like a type" error). A
local top-level definition that shadows an imported name of the same spelling is left unqualified,
so the local binding still wins.

A `struct`, `interface`, `enum`, `union`, or `delegate` whose name equals the file-derived module
class name is still an error (`SPY0520`): only a `class` can absorb the module's members and serve as
the module class — and only a NON-generic one. A generic class named like its file (`class
Thing[T]` in `thing.spy`) is `Thing<T>` in C#: it cannot merge into the module class `Thing` (which
has no type parameters), and it cannot sit inside it either, because C# compares a nested type's name
without its arity (CS0542). It is refused with `SPY0520` too; rename the class or the file.

## Circular Import Handling

### Current Behavior

All circular imports are rejected at compile time with diagnostic `SPY0302`. When `ModuleLoader` detects that a module being loaded is already on the import chain (tracked via `_importChain` Stack), it emits an error with the full cycle path and returns `null`, preventing the module from loading.

<!-- spec-sweep: fragment -->
```python
# file: parent.spy
from child import Child  # ERROR: SPY0302 circular import detected

class Parent:
    children: list[Child]

# file: child.spy
from parent import Parent  # ERROR: SPY0302 circular import detected

class Child:
    parent: Parent?
```

The workaround today is to restructure code to break the cycle, typically by extracting shared types into a third module that both sides import.

### RFC: Allow Circular Imports for Type-Annotation-Only References

**Status:** RFC -- implementation deferred to a dedicated issue.

**Motivation:** Mutually-referential types are common in domain modeling (e.g., `Parent`/`Child`, `Order`/`LineItem`, `Node`/`Edge`). Requiring a third "types-only" module to break the cycle is boilerplate that does not exist in the target .NET platform, where circular type references within an assembly are resolved naturally by the CLR.

**Proposed behavior:** Circular imports are allowed when every imported symbol from the cycle is used ONLY in type annotation positions -- not at runtime. Specifically, a "type-annotation-only" usage means the symbol appears exclusively in:

- Field type annotations (e.g., `other: ClassB`)
- Parameter type annotations (e.g., `def f(self, b: ClassB) -> None`)
- Return type annotations (e.g., `-> ClassB`)
- Generic type arguments in annotations (e.g., `list[ClassB]`)
- Variable declaration type annotations (e.g., `x: ClassB = ...`)

The following usages are NOT type-annotation-only and would still require a non-circular import:

- Base class references (e.g., `class Foo(ClassB)`)
- Constructor calls (e.g., `ClassB()`)
- Static method or attribute access (e.g., `ClassB.create()`)
- `isinstance` checks (e.g., `isinstance(x, ClassB)`)
- Any expression-position usage of the imported name

**Example of proposed behavior:**

<!-- spec-sweep: fragment -->
```python
# file: parent.spy
from child import Child  # OK - Child used only in type annotations

class Parent:
    children: list[Child]  # Type annotation - resolved in type resolution phase

    def add_child(self, c: Child) -> None:  # Type annotation
        self.children.append(c)

# file: child.spy
from parent import Parent  # OK - Parent used only in type annotations

class Child:
    parent: Parent?  # Type annotation - resolved in type resolution phase
```

**Implementation touchpoints:**

| Component | File(s) | Change |
|-----------|---------|--------|
| Circular import detection | `ModuleLoader._importChain`, `IsModuleInChain()` | Defer rejection; record the cycle instead of immediately emitting SPY0302 |
| Import resolution | `ImportResolver.ResolveImports()` | Track which imported symbols come from a deferred-cycle module |
| Usage classification | New analysis pass or TypeChecker extension | Walk AST to classify each use of a deferred-cycle symbol as type-annotation-only vs. runtime |
| Error emission | `ModuleLoader`, `ImportResolver` | Emit SPY0302 only for cycles where at least one symbol has a runtime usage |

**Approach sketch -- two-pass import system:**

1. **Pass 1 (type stub collection):** When a circular import is detected, instead of emitting an error, create a "stub" `ModuleInfo` containing only type declarations (class/struct/interface/enum names and their type parameters). Register these stubs in the symbol table so that type annotations can resolve against them. Mark the import as "deferred."

2. **Pass 2 (full resolution):** After all modules have completed Pass 1, revisit deferred imports and attempt full resolution. At this point, all type names are known, so type annotations resolve correctly. If a deferred-cycle symbol is used in a non-annotation position, emit SPY0302 with a message clarifying that circular imports are only permitted for type annotations.

This approach aligns with the existing semantic pipeline: stub collection maps naturally onto `NameResolver.ResolveDeclarations()` (Pass 1), and full resolution maps onto `TypeResolver.ResolveTypes()` (Pass 2) and `TypeChecker.CheckModule()` (Pass 3).

**Design considerations:**

- **Base class cycles remain forbidden.** A class cannot extend a base from a circular import because inheritance resolution (`NameResolver.ResolveInheritance()`) requires the full base type, not just a stub.
- **Incremental compilation.** The `IncrementalCompilationCache` already tracks file dependencies. Deferred-cycle imports would add bidirectional edges to the dependency graph, causing both files to recompile when either changes. This is correct behavior.
- **Error quality.** When a cycle is rejected because a symbol has runtime usage, the error message should identify which symbol and which usage site caused the rejection, not just show the cycle path.
- **No new syntax required.** Unlike Python's `from __future__ import annotations` or `TYPE_CHECKING` guard, Sharpy can detect annotation-only usage statically at compile time because all type information is resolved ahead of time.
