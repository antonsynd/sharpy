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

**Packages are C# namespaces.** Each directory between the project's common source directory and a
source file becomes a segment of that file's C# namespace, and the file's module class is declared
in it. In a project whose root namespace is `Merge`:

```
src/
    main.spy              # namespace Merge     { static partial class Program   }
    pkg/
        __init__.spy      # namespace Merge.Pkg { static partial class PkgModule }
        lib.spy           # namespace Merge.Pkg { static partial class Lib       }
    lib/
        lib.spy           # namespace Merge.Lib { static partial class Lib       }
```

A regular module's class is named after its file (`pkg/lib.spy` → `Merge.Pkg.Lib`). A package's
`__init__.spy` emits its module class as `<Dir>Module` inside the package's own namespace
(`pkg/__init__.spy` → `Merge.Pkg.PkgModule`), so `import pkg; pkg.init_fn()` reaches
`Merge.Pkg.PkgModule.InitFn()`. A directory may share its name with a module inside it (`lib/lib.spy`,
`from lib.lib import lf` — a namespace and a class of one name nest legally), and directories may
repeat (`a/a/x.spy`). Directories are measured from the project's common source directory, and every
file is checked whether or not anything imports it. A library built this way exposes every package
module to a consumer that references it: `from pkg.lib import f` resolves against the built assembly.

Three package layouts emit one identifier twice in one namespace and are refused before analysis —
the first two with `SPY0526`, the third with `SPY0523`. Each compares *emitted* identifiers, so
spellings that mangle alike collide too (`my_pkg.spy` beside `myPkg/`):

```
src/
    pkg.spy               # error SPY0526: Module 'pkg.spy' and the package directory 'pkg'
    pkg/                  # beside it both emit the C# identifier 'Pkg' ... Rename the file or
        x.spy             # the directory.
```

A module file beside a same-named package directory, with or without `pkg/__init__.spy`. Python
imports only one of the two — the package when it has an `__init__`, the module when it does not
(`from pkg.x import f` is then `ModuleNotFoundError: ... 'pkg' is not a package`) — so the other is
unreachable; in C# the class `Pkg` and the namespace `Pkg` cannot share `Merge`.

```
src/
    pkg/
        __init__.spy      # def lib() -> int: ...
        lib.spy           # error SPY0526 (at the `def lib` in __init__.spy): 'lib' in the package's
                          # __init__.spy emits the C# identifier 'Lib', which its submodule 'lib.spy'
                          # also emits (python's `pkg.lib` would name both). Rename the declaration
                          # or the submodule.
```

A top-level name in a package's `__init__.spy` (function, variable, constant or type) whose emitted
identifier is one of that package's own submodules or subpackages: after `import pkg.lib`, python's
`pkg.lib` names the submodule, not the function. The rule compares emitted identifiers, so it also
refuses `X: int` in `pkg/__init__.spy` beside `pkg/x.spy`, which python tells apart; it is expected
to narrow to real C# clashes once module types sit beside the module class (#2086).

```
src/
    pkg/
        __init__.spy      # error SPY0523: The submodule 'pkg_module.spy' compiles to 'PkgModule',
        pkg_module.spy    # which conflicts with the module class of the package's __init__.spy.
                          # Rename the submodule.
```

A submodule or subpackage spelled like the package's own module class (`pkg/pkg_module.spy` or
`pkg/pkg_module/` beside `pkg/__init__.spy`): both would be `Merge.Pkg.PkgModule`. It is the same
module-class collision (`SPY0523`) a *function* spelled like the module class gets — `def pkg_module`
in `pkg/__init__.spy`, or `def thing` in `thing.spy`.

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
without its arity (CS0542). It is refused with `SPY0520` too; rename the class or the file. The
refusal is uniform: it applies even to a module that holds nothing but the generic class, whose
module class could otherwise simply *be* `Thing<T>`. A rule that depended on whether the module has
other members would flip to an error the moment one helper function is added. Emitting user types
beside the module class instead of nested in it would lift the refusal for every case at once
(#2039).

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
