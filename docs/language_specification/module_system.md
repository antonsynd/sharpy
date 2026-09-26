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

**Every module is a C# namespace.** A source file's namespace is the project's root namespace,
then each directory between the project's common source directory and the file, then the file's
own stem. A package's `__init__.spy` is the package itself, so its namespace stops at the directory.
Inside that namespace the module emits:

- its **members class** `<Stem>Module` (for `__init__.spy`, `<Dir>Module`), which holds the module's
  functions, variables and constants (and, in the entry module, `Main`);
- every top-level **type** — class, struct, interface, enum, union, delegate — declared *beside*
  the members class, never inside it;
- in test builds, the test class `<Stem>ModuleTests` and one `<Name>Fixture` class per
  `@test.fixture`.

In a project whose root namespace is `Merge`:

```text
src/
    main.spy              # namespace Merge.Main  { static partial class MainModule { Main() } }
    thing.spy             # namespace Merge.Thing { static partial class ThingModule { Helper() }
                          #                         class Thing<T> { ... } }
    pkg/
        __init__.spy      # namespace Merge.Pkg     { static partial class PkgModule { InitFn() } }
        lib.spy           # namespace Merge.Pkg.Lib { static partial class LibModule { F() } }
```

So `import pkg; pkg.init_fn()` reaches `Merge.Pkg.PkgModule.InitFn()`, `from pkg.lib import f`
reaches `Merge.Pkg.Lib.LibModule.F()`, and a type `Foo` declared in `pkg/lib.spy` is
`Merge.Pkg.Lib.Foo`. A single-file compile has no root namespace: `thing.spy` is `namespace Thing`.
The layout is the same in every mode — `run`, `project`, and single-file library builds.

Because types sit beside the members class, a type may share its module's name: `class Thing[T]`
(or a `struct`, `enum`, `union`, … named `Thing`) in `thing.spy` is simply `Merge.Thing.Thing`, and
a function and a type whose emitted names coincide (`def foo_bar` and `class FooBar`) live in two
scopes and coexist. Two modules may each declare a type of one name — `a.Foo` and `b.Foo` are
`Merge.A.Foo` and `Merge.B.Foo`, two types, as in python.

A non-entry module's members class carries `[SharpyModule("<dotted.name>")]` and each of its
types carries `[SharpyModuleType("<dotted.name>", "<PythonName>")]`, which is how a project that
references the built assembly discovers them (`from pkg.lib import Foo`). The entry module's types
are stamped with python's name for it, `__main__`, and its members class carries no
`[SharpyModule]`. A directory may share its name with a module inside it (`lib/lib.spy` is
`Merge.Lib.Lib`), and directories may repeat (`a/a/x.spy`).

The names a module emits into its namespace are reserved there:

```text
thing.spy
    def thing_module() -> int: ...   # error SPY0523: 'thing_module' compiles to 'ThingModule',
                                     # which is this module's members class ... Rename it.
    class ThingModule: ...           # error SPY0523 (a type spelled like the members class)
    class GreetingFixture: ...       # error SPY0522 beside `@test.fixture def greeting`
                                     # (and a type spelled `ThingModuleTests` when the module
                                     # declares tests)
```

A function, variable or constant spelled like the members class would be a member named like its
enclosing class (CS0542); a type spelled like it, like the test class, or like a fixture class would
be a second declaration of that name in the module namespace. A function named like its *file*
(`def thing` in `thing.spy`) is no collision: its class is `ThingModule`.

A module whose namespace is also the full name of a .NET type it uses is refused with `SPY0615`:
C# resolves the name to the namespace declared in the compiled source, so the type could never be
reached. In a project with root namespace `App` that references an assembly declaring the class
`App.Foo`, the module `foo.spy` doing `from app import Foo` is refused; rename the file (its stem is
the namespace's last segment).

An executable names its one entry point explicitly — the entry module's members class
(`Merge.Main.MainModule`) — so a non-entry module's function that happens to emit as `Main`
(`def Main`, or ``def `Main` ``, whose backticks keep the literal spelling) is an ordinary static
method, never a second C# entry point. A non-entry `def main` is still emitted as `MainFunc`.

Three package layouts emit one identifier twice in one namespace, or are unreachable in python,
and are refused before analysis — the first two with `SPY0526`, the third with `SPY0523`. Each
compares *emitted* identifiers, so spellings that mangle alike collide too (`my_pkg.spy` beside
`myPkg/`):

```text
src/
    pkg.spy               # error SPY0526: Module 'pkg.spy' and the package directory 'pkg'
    pkg/                  # beside it both emit the C# identifier 'Pkg' ... Rename the file or
        x.spy             # the directory.
```

A module file beside a same-named package directory, with or without `pkg/__init__.spy`. Python
imports only one of the two — the package when it has an `__init__`, the module when it does not
(`from pkg.x import f` is then `ModuleNotFoundError: ... 'pkg' is not a package`) — so the other is
unreachable. The comparison is by the module's own namespace segment (`main.spy` beside `program/`
are two names).

```text
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
refuses `X: int` in `pkg/__init__.spy` beside `pkg/x.spy`, which python tells apart. Now that an
`__init__`'s functions, variables and constants live in `PkgModule`, only an `__init__` *type* can
clash with a child's namespace in C#; narrowing the rule to that is #2086.

```text
src/
    pkg/
        __init__.spy      # error SPY0523: The submodule 'pkg_module.spy' compiles to 'PkgModule',
        pkg_module.spy    # which conflicts with the module class of the package's __init__.spy.
                          # Rename the submodule.
```

A submodule or subpackage spelled like the package's own members class (`pkg/pkg_module.spy` or
`pkg/pkg_module/` beside `pkg/__init__.spy`): the namespace `Merge.Pkg.PkgModule` and the class
`Merge.Pkg.PkgModule` cannot coexist. It is the same members-class collision (`SPY0523`) a
*function* spelled like it gets — `def pkg_module` in `pkg/__init__.spy`.

## Name Qualification in Generated C#

Every reference the compiler emits to a **module-level member** (a module function, variable, or
constant — imported or defined in the same module) is fully `global::`-qualified through its
module's members class, and every reference to a **type** declared by a module is `global::`-qualified
through its module's namespace. Nothing is bound by an ambient `using static` or
`using <alias> = ...` directive; those directives are not emitted.

```python
def greet(name: str) -> str:
    return "Hello, " + name

def main() -> None:
    # `greet` is a module-level member, so the call is emitted as
    # global::<Namespace>.<Stem>Module.Greet("world") — never a bare `Greet(...)`.
    print(greet("world"))
```

Because every such reference is `global::`-rooted rather than resolved by name lookup, a
module-level name is free to equal the root namespace, a namespace segment, or a type without a C#
name collision. For example, a project whose root namespace is `Poison` may define `def poison()` in
`lib.spy` and call it from another module — the call is emitted
`global::Poison.Lib.LibModule.Poison()`, so it does not collide with the `Poison` namespace (which,
before universal qualification, produced a `CS0118` "is a namespace but is used like a type" error).
The same holds inside a type body: a method of `class C` that reads a module variable its own field
shadows (python resolves the bare name to the module) is emitted `global::<Root>.Lib.LibModule.V` —
a bare `Lib.V` there would name the namespace. A local top-level definition that shadows an imported
name of the same spelling is left unqualified, so the local binding still wins.

`SPY0520` (a type named like its file) is retired: with types beside the members class, no module
class exists for a type to collide with.

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
