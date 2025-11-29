## Plan: Refactor Hard‑Coded Type/Operator Semantics

This plan focuses on systematically identifying all hard‑coded Sharpy/.NET/C# type and operator knowledge in `Sharpy.Compiler`, then replacing ad‑hoc logic with: (1) explicit, exhaustively tested registries (for closed sets like primitive numeric types), and (2) reflection/metadata‑driven discovery with caching (for framework/library types, methods, and operators). It leans on existing discovery and caching infrastructure (e.g. `Discovery/OverloadIndex*`, `CachedModuleDiscovery`, `BuiltinRegistry`, `TypeMapper`) and aims to centralize all "semantic knowledge" behind a few well‑defined services so the parser/semantic analyzer/codegen no longer scatter hard‑coded decisions.

This is an architectural refactor, not a behavior change project: the end state should preserve current user‑visible semantics (modulo clearly documented bug fixes), while making it significantly easier to add new Sharpy/Core features and .NET interop without touching many call sites.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Status](#status)
3. [High-Level Steps (Architectural)](#high-level-steps-architectural)
4. [V0.5 Implementation Task List](#v05-implementation-task-list)
5. [Post-V0.5 Roadmap](#post-v05-roadmap)
6. [Design Decisions](#design-decisions)
7. [Appendix: Existing Hard-Coded Locations](#appendix-existing-hard-coded-locations)

---

## Architecture Overview

### Current State (Post-Operator Refactor)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Sharpy.Compiler                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  Semantic/                                                                   │
│  ├── OperatorValidator ✅        (centralized operator resolution)          │
│  ├── OperatorSignatureValidator ✅ (dunder signature validation)            │
│  ├── BuiltinRegistry             (types + functions, needs consolidation)  │
│  ├── TypeChecker                 (has hard-coded protocol checks)          │
│  ├── TypeResolver                (has hard-coded container logic)          │
│  └── NameResolver                (populates OperatorMethods, needs extend) │
│                                                                              │
│  CodeGen/                                                                    │
│  ├── RoslynEmitter               (hard-coded dunder→.NET mappings)         │
│  ├── TypeMapper                  (hard-coded type mappings)                │
│  └── NameMangler                 (dunder name mappings)                    │
│                                                                              │
│  Discovery/                                                                  │
│  ├── CachedModuleDiscovery       (reflection-based function discovery)     │
│  ├── TypeMapper                  (duplicate! needs consolidation)          │
│  └── OverloadIndex*              (overload resolution infrastructure)      │
└─────────────────────────────────────────────────────────────────────────────┘
                                    ↓
┌─────────────────────────────────────────────────────────────────────────────┐
│                             Sharpy.Core                                      │
├─────────────────────────────────────────────────────────────────────────────┤
│  Protocol Interfaces/Classes (already defined, need compiler integration):  │
│  ├── IIterable<T>, Iterator<T>   → __iter__, __next__                       │
│  ├── ISized                       → __len__                                 │
│  ├── IContainer<T>                → __contains__                            │
│  ├── ISequence<T>, IMutableSequence<T> → __getitem__, __setitem__          │
│  ├── IStrConvertible, IRepresentable → __str__, __repr__                   │
│  ├── IHashable, IEquatable<T>     → __hash__, __eq__                        │
│  ├── IBoolConvertible             → __bool__                                │
│  └── (40+ additional protocol interfaces)                                   │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Target State

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Sharpy.Compiler                                    │
├─────────────────────────────────────────────────────────────────────────────┤
│  Semantic/                                                                   │
│  ├── Validators/                                                            │
│  │   ├── OperatorValidator ✅     (binary/unary/augmented operators)        │
│  │   ├── ProtocolValidator 🆕     (container/iteration/conversion dunders) │
│  │   ├── OperatorSignatureValidator ✅ (operator dunder signatures)        │
│  │   └── ProtocolSignatureValidator 🆕 (protocol dunder signatures)        │
│  │                                                                          │
│  ├── Registries/                                                            │
│  │   ├── PrimitiveCatalog 🆕      (numeric types, conversions, promotions) │
│  │   ├── ProtocolRegistry 🆕      (dunder→interface mappings)              │
│  │   └── BuiltinRegistry ✅       (consolidated types + functions)         │
│  │                                                                          │
│  ├── Caching/                                                               │
│  │   └── ClrMemberCache 🆕        (unified CLR reflection cache)           │
│  │                                                                          │
│  └── TypeChecker                  (delegates to validators, no hard-coding)│
│                                                                              │
│  CodeGen/                                                                    │
│  └── (uses registries, no hard-coded mappings)                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Status

### Completed ✅

- **Operator Validation** (see [reflection_based_operator_validation.md](reflection_based_operator_validation.md)):
  - `OperatorValidator` - centralized binary/unary/augmented operator resolution
  - `OperatorSignatureValidator` - dunder signature validation at name resolution time
  - `TypeSymbol.OperatorMethods` - caching of validated operator methods
  - `_clrOperatorCache` - CLR operator discovery via reflection
  - `TypeChecker` integration for all operator expressions

### In Progress 🔄

- **Primitive Catalog** - consolidating scattered primitive type checks
- **Protocol Registry** - mapping non-operator dunders to Sharpy.Core interfaces

### Not Started ⏳

- **Protocol Signature Validator** - signature validation for non-operator dunders
- **Protocol Validator** - TypeChecker integration for protocol conformance
- **RoslynEmitter consolidation** - removing hard-coded dunder mappings in codegen
- **Type Mapper consolidation** - merging duplicate type mapping logic
- **CLR Member Cache extraction** - reusable reflection caching service

---

## High-Level Steps (Architectural)

1. **Inventory and classify hard‑coded semantics**
  - **EXCLUDE** operator-related hard-coding (already refactored into `OperatorValidator` and `OperatorSignatureValidator`).
  - Scan `Semantic/` (`BuiltinRegistry`, `SemanticType`, `TypeResolver`, `TypeChecker`, `NameResolver`, `ModuleRegistry`, `AccessValidator`, `ControlFlowValidator`) and `CodeGen/TypeMapper` for explicit type/method names and special‑case branches related to:
    - Primitive types and their conversions
    - Container types (list, dict, set, tuple) and their protocols
    - Non-operator dunder methods (`__init__`, `__str__`, `__repr__`, `__hash__`, `__len__`, `__iter__`, `__next__`, `__call__`, `__getitem__`, `__setitem__`, etc.)
    - Built-in functions (`len`, `print`, `range`, `enumerate`, etc.)
  - Do the same for `Discovery/TypeMapper`, `OverloadIndex*`, any reflection‑based helpers, and any direct `System.*` `Type`/`MethodInfo` references in other folders that encode semantic knowledge.
  - Classify every hard‑coded piece as: (A) closed finite set (e.g. integer types, primitive conversions), (B) Sharpy language intrinsic (e.g. `len`, `print`, non-operator dunder protocol names), or (C) framework/library dependent (e.g. `System.Collections.Generic.Dictionary<,>` or LINQ support).

2. **Introduce centralized semantic description services**
   - **COMPLETED**: Operator validation is now centralized in `OperatorValidator` with:
     - Dunder → operator mapping (`BinaryOperatorToDunder`, `UnaryOperatorToDunder`)
     - CLR operator mapping (via `_clrOperatorCache`)
     - Overload resolution (`ResolveBestOverload`)
   - **REMAINING**: Define a single "builtin description" service (e.g. `BuiltinTypeModel`/`IntrinsicRegistry`) for:
     - Sharpy primitives and containers (currently scattered in `BuiltinRegistry`, `TypeMapper`)
     - Non-operator dunder protocols (`__init__`, `__str__`, `__repr__`, `__hash__`, `__iter__`, `__len__`, etc.)
     - Container/collection semantics (indexing, iteration, membership)
   - **ALIGN**: Ensure new services follow the pattern established by `OperatorValidator`:
     - Reflection-based discovery with caching
     - Separation between signature validation and semantic resolution
     - Integration points with `TypeChecker` via delegation

3. **Replace partial hard‑coded sets with exhaustive registries**
  - For .NET primitive and numeric types, define exhaustive tables in a dedicated module (e.g. `Semantic/PrimitiveCatalog`), owned by the semantic layer but independent of any particular `SemanticType` representation:
     - e.g. all integer types (`sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `nint`, `nuint`), floats (`float`, `double`, `decimal` if supported), `bool`, `char`, etc.
   - Encode: Sharpy surface name, underlying .NET `Type`, numeric kind (integer/float), size, signedness, and promotion/implicit conversion rules.
  - Add unit tests in `Sharpy.Compiler.Tests` (likely under a new `Semantic/PrimitiveCatalogTests` namespace) verifying:
     - The registry is exhaustive for supported primitives.
     - Conversions and promotions match a reference table.
   - Refactor any scattered primitive checks (e.g. “if type is int or long…”) to query the registry.

4. **Consolidate and formalize dunder/operator mapping**
   - **COMPLETED**: Central operator handling exists in `OperatorValidator`:
     - All arithmetic, bitwise, comparison, unary operators mapped to dunders
     - In-place operator support (`__iadd__`, etc.) with fallback to base operators
     - Equality complement synthesis (`__eq__` ↔ `__ne__`)
     - Power operator (`**` → `__pow__`)
   - **REMAINING**: Extend to non-operator dunders and protocols:
     - Container protocols: `__getitem__`, `__setitem__`, `__delitem__`, `__contains__`, `__len__`
     - Iteration protocols: `__iter__`, `__next__`
     - String/representation: `__str__`, `__repr__`
     - Hashing/equality for collections: `__hash__` (coordinate with `__eq__`)
     - Callable: `__call__`
   - **PATTERN**: Follow `OperatorSignatureValidator` approach:
     - Whitelist of recognized protocol dunders
     - Signature validation rules per protocol
     - Caching in `TypeSymbol` (may need additional dictionaries beyond `OperatorMethods`)

5. **Align new work with operator validation architecture**
   - **STUDY**: Review `OperatorValidator` and `OperatorSignatureValidator` as reference implementations
   - **PRINCIPLES** to carry forward:
     - **Separation of concerns**: Signature validation (name resolution time) vs semantic validation (type checking time)
     - **Caching strategy**: Pre-validated symbols in `TypeSymbol` metadata, runtime CLR members in validator caches
     - **Error quality**: Descriptive messages mentioning Sharpy syntax, .NET concepts, and actionable fixes
     - **Delegation pattern**: `TypeChecker` calls focused validators, doesn't own semantic rules
     - **Test coverage**: Dedicated validator tests separate from integration tests
   - **NAMING**: Use consistent terminology:
     - "Validator" for semantic validation services (e.g., `OperatorValidator`, `ProtocolValidator`)
     - "Registry" for static metadata (e.g., `PrimitiveCatalog`, `ProtocolRegistry`)
     - "Cache" for reflection-derived runtime data (e.g., `_clrOperatorCache`)

6. **Unify and extend reflection‑based type/method discovery**
   - **COMPLETED**: `OperatorValidator` implements reflection-based CLR operator discovery:
     - `_clrOperatorCache` for CLR operator methods (`op_Addition`, etc.)
     - Caching by CLR `Type`
     - Integration with Sharpy semantic types
   - **EXTEND**: Generalize this pattern for non-operator methods:
     - Collection methods (`Add`, `Remove`, `Contains` for .NET collections)
     - Enumerable/iterator methods (`GetEnumerator`, `MoveNext`, `Current`)
     - Common .NET patterns (property getters/setters, indexers, events)
   - **ARCHITECTURE**: Consider extracting a general `ClrMemberCache` or `RuntimeTypeMetadataProvider`:
     - Subsumes `_clrOperatorCache` logic from `OperatorValidator`
     - Provides unified interface for discovering any CLR member kind
     - Single responsibility: .NET type → member metadata with caching
     - Used by `OperatorValidator`, future `ProtocolValidator`, and codegen

7. **Strengthen and reuse caching infrastructure**
   - **PATTERN ESTABLISHED**: `OperatorValidator._clrOperatorCache` demonstrates:
     - Dictionary-based caching keyed by CLR `Type`
     - Lazy population on first use
     - Per-compilation lifetime (field on validator instance)
   - **EXTEND**: Apply same pattern to:
     - Non-operator CLR methods and properties
     - Primitive type metadata (conversions, promotions)
     - Protocol conformance (which types support which dunders)
   - **INTEGRATION**: Consider whether existing `Discovery/Caching` infrastructure should:
     - Host these caches (centralized, cross-compilation)
     - Or remain separate (per-compilation in validators, simpler)
   - **DECISION NEEDED**: Determine cache granularity and lifetime strategy

8. **Refactor type analysis and checking to use the new services**
   - **COMPLETED**: `TypeChecker` now delegates operator semantics to `OperatorValidator`:
     - `CheckBinaryOp` → `ValidateBinaryOp`
     - `CheckUnaryOp` → `ValidateUnaryOp`
     - `CheckAssignment` (augmented) → `ValidateAugmentedAssignment`
     - Comparison chains validated via `OperatorValidator`
   - **REMAINING**: Apply same delegation pattern to:
     - Container operations (indexing, slicing, membership)
     - Iteration and comprehension semantics
     - Callable invocation (when target is user-defined `__call__`)
     - String/representation conversion
     - Hash computation and equality for collections
   - **PATTERN**: For each area:
     - Create a focused validator (e.g., `ProtocolValidator`, `ContainerValidator`)
     - Move hard‑coded logic from `TypeChecker` into the validator
     - Add comprehensive tests before and after migration
     - Keep `TypeChecker` focused on AST traversal and coordination

8. **Make “closed sets” explicit and verified**
   - For all semantically closed domains, define centralized enums/records and test suites:
     - Primitive numeric types, built‑in collection kinds, built‑in literals, comparison operation kinds, unary operators, etc.
   - Add tests that:
     - Assert no enum value is unused in grammar or semantic code.
     - Assert every relevant grammar token is represented in registries.
   - Use these enums in pattern matching instead of string/`int`/`char` literals.

9. **Gradually tighten and deprecate ad‑hoc paths**
   - Mark existing ad‑hoc helper functions as internal implementation details to be removed once coverage is complete.
   - As each area is ported to the centralized registries and metadata provider, delete redundant hard‑coded branches and add regression tests in `Sharpy.Compiler.Tests` (semantic, codegen, and integration) to lock the new behavior down.
   - Introduce diagnostic logging (behind `ICompilerLogger`) when falling back to legacy paths, to help detect missed refactors; later, treat these as errors.

10. **Plan migration and validation strategy**
   - **COMPLETED SLICE**: Operator validation (arithmetic, bitwise, comparison, unary, augmented assignment)
     - Full test coverage in `OperatorValidatorTests`, `OperatorSignatureValidatorTests`
     - Integration confirmed in `TypeCheckerTests`
     - CLR interop validated for numeric operators
   - **NEXT SLICES** (in priority order):
     1. **Primitive catalog**: Exhaustive registry of numeric/primitive types with conversion rules
     2. **Container protocols**: Indexing, membership, length, iteration dunders
     3. **String/representation protocols**: `__str__`, `__repr__`, format integration
     4. **Callable protocol**: `__call__` validation and invocation
     5. **Hash/equality protocols**: Coordinate `__hash__` with `__eq__` for collection safety
   - **PER-SLICE CHECKLIST**:
     - Define validator/registry interface
     - Implement with reflection + caching where needed
     - Add comprehensive unit tests
     - Integrate into `TypeChecker` or `NameResolver`
     - Remove old hard‑coded paths
     - Run full test suite for regression
     - Update this document with status

### Further Considerations

1. **VALIDATED**: The operator validation work confirms that reflection-based discovery with caching scales well for .NET interop; apply this pattern to all framework type interactions.

2. **VALIDATED**: Separating signature validation (at name resolution) from semantic validation (at type checking) prevents invalid symbols from entering the symbol table and produces better error messages; maintain this separation for all protocols.

3. **PARTIALLY COMPLETE**: Scope of protocol support:
   - The Python protocols that Sharpy should support (iterator, context manager, descriptor, etc.)?
   - The .NET patterns that should be first-class (IDisposable, IEnumerable<T>, async/await)?
   - These decisions are documented in the table below to guide registry/validator implementation priorities.

   **Protocol/Pattern Support Decision Matrix:**

   | Protocol/Pattern | Category | Dunder Methods / .NET Interface | Support Now? |
   |-----------------|----------|--------------------------------|--------------|
   | **Python Protocols** | | | |
   | Iterator | Container | `__iter__`, `__next__` | Yes, these should be implemented in terms of Sharpy's native `IIterable` and `IIterator` interfaces, which inherit from .NET's `IEnumerable` and `IEnumerator` interfaces. |
   | Container/Sequence | Container | `__len__`, `__getitem__`, `__setitem__`, `__delitem__`, `__contains__` | Yes, there should be Sharpy interfaces for these, which inherit from appropriate .NET ones if they exist. |
   | Context Manager | Resource | `__enter__`, `__exit__` | Yes, but not a v0.5 feature. Requires some special code emission and does not strictly overlap with `IDisposable` in .NET. |
   | Callable | Function-like | `__call__` | No, C#/.NET doesn't support (non-dynamic) callable objects. It could be done later if we emit CIL directly to dispatch this correctly. |
   | Descriptor | Meta-programming | `__get__`, `__set__`, `__delete__` | Never |
   | String Representation | Display | `__str__`, `__repr__` | Yes. `__str__` maps to .NET's `ToString()`. `__repr__` is a separate method (`__Repr__()`) for debug representation - there is no direct .NET equivalent, so Sharpy generates a distinct method. Both inherit from `Sharpy.Core.Object` base class. |
   | Hashing/Equality | Collection | `__hash__`, `__eq__` | Yes, natively through inheritance from the `Sharpy.Core.Object` base class, and also via overriding of the .NET native `GetHashCode()` and `Equals()` instance methods (and related static operators). |
   | Numeric Conversion | Type Conversion | `__int__`, `__float__`, `__complex__`, `__bool__` | `__bool__` for v0.5, implemented as static conversion operator. The others for later. |
   | Rich Comparison | Comparison | `__lt__`, `__le__`, `__gt__`, `__ge__`, `__eq__`, `__ne__` | Yes, via Sharpy comparison interfaces. These are handled by `OperatorSignatureValidator` (not `ProtocolSignatureValidator`) since they map to operators. `__eq__` and `__ne__` also integrate with `Sharpy.Core.Object` and static operator synthesis. |
   | Attribute Access | Meta-programming | `__getattr__`, `__setattr__`, `__delattr__`, `__getattribute__` | Never. |
   | Class Instantiation | Meta-programming | `__new__`, `__init__`, `__init_subclass__` | Only `__init__` methods which map to constructor methods. |
   | Async Iterator | Async | `__aiter__`, `__anext__` | Not in v0.5. |
   | Async Context Manager | Async | `__aenter__`, `__aexit__` | Not in v0.5. |
   | Async Callable | Async | `__await__` | Not in v0.5. |
   | Buffer Protocol | Low-level | `__buffer__`, `__release_buffer__` | Not in v0.5. |
   | **Python-Specific Operators** | | | |
   | Matrix Multiplication | Operator | `__matmul__`, `__rmatmul__`, `__imatmul__` | Not in v0.5. |
   | Floor Division | Operator | `__floordiv__`, `__rfloordiv__`, `__ifloordiv__` | Not in v0.5. |
   | String Formatting | Display | `__format__` | Not in v0.5. |
   | **Collection Protocols** | | | |
   | Reversible | Container | `__reversed__` | Part of Sharpy's `IReversible` interface. |
   | Sized | Container | `__len__` | Part of Sharpy's `ISized` interface and used in top-level `len()` function. |
   | Collection | Container | `__len__`, `__iter__`, `__contains__` | Part of Sharpy's collection interfaces, like `ISized` and `IIterable`. |
   | Mapping | Container | `__getitem__`, `__setitem__`, `__delitem__`, `__iter__`, `__len__` | Part of Sharpy's collection interfaces. |
   | Mutable Mapping | Container | extends Mapping + `__delitem__`, `pop`, `popitem`, `clear`, `update`, `setdefault` | Yes. |
   | Set | Container | `__contains__`, `__iter__`, `__len__`, `__le__`, `__lt__`, `__eq__`, `__ne__`, `__gt__`, `__ge__`, `__and__`, `__or__`, `__sub__`, `__xor__` | Yes |
   | Mutable Set | Container | extends Set + `add`, `discard`, `remove`, `pop`, `clear` | Yes |
   | **Pickle Protocol** | | | |
   | Pickling | Serialization | `__reduce__`, `__reduce_ex__`, `__getstate__`, `__setstate__` | Not in v0.5. |
   | **Copy Protocol** | | | |
   | Shallow Copy | Cloning | `__copy__` | Not in v0.5. |
   | Deep Copy | Cloning | `__deepcopy__` | Not in v0.5. |
   | **Class/Type Protocols** | | | |
   | Metaclass | Meta-programming | `__prepare__`, `__mro_entries__` | Never. |
   | Type Checking | Meta-programming | `__instancecheck__`, `__subclasscheck__` | Never. |
   | Class Getitem | Generics | `__class_getitem__` | Never. |
   | **Path-like Protocol** | | | |
   | Path-like | File I/O | `__fspath__` | Not in v0.5. |
   | **Dataclass Protocol** | | | |
   | Dataclass | Data Structures | `__post_init__`, `__eq__`, `__hash__`, `__repr__` (auto-generated) | `__post_init__` not in v0.5., but the others through `Sharpy.Core.Object` probably. |
   | **Match Protocol (Python 3.10+)** | | | |
   | Structural Pattern Matching | Pattern Matching | `__match_args__` | Not in v0.5, but should probably involve C#'s `Deconstruct()`. |
   | **Import Protocol** | | | |
   | Import Hook | Module System | `__import__` | Never. |
   | **Property Protocol** | | | |
   | Properties | Attribute Access | `@property`, `getter`, `setter`, `deleter` | Not in v0.5. |
   | **Weak Reference Protocol** | | | |
   | Weak References | Memory Management | `__weakref__` | Not in v0.5. |
   | **Slots Protocol** | | | |
   | Slots | Memory Optimization | `__slots__` | Never. |
   | **Abstract Base Classes** | | | |
   | ABC | Meta-programming | `@abstractmethod`, `ABCMeta` | Not in v0.5. |
   | **.NET Patterns** | | | |
   | Enumerable | Collection | `IEnumerable`, `IEnumerable<T>`, `GetEnumerator()` | Yes, super-interface of Sharpy's `IIterable` |
   | Enumerator | Iterator | `IEnumerator`, `IEnumerator<T>`, `MoveNext()`, `Current` | Yes, super-interface of Sharpy's `IIterator` |
   | Collection | Collection | `ICollection`, `ICollection<T>`, `Add`, `Remove`, `Count` | Yes |
   | List | Collection | `IList`, `IList<T>`, indexer, `Insert`, `RemoveAt` | Not in v0.5. |
   | Dictionary | Collection | `IDictionary`, `IDictionary<K,V>`, `Keys`, `Values` | Not in v0.5. |
   | Set | Collection | `ISet<T>`, `UnionWith`, `IntersectWith`, `ExceptWith` | Not in v0.5. |
   | Read-only Collection | Collection | `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `IReadOnlyDictionary<K,V>` | Not in v0.5. |
   | Disposable | Resource | `IDisposable`, `Dispose()`, `using` statement | Not in v0.5, and maybe tied to context managers in Sharpy. |
   | Async Disposable | Resource | `IAsyncDisposable`, `DisposeAsync()`, `await using` | Not in v0.5. |
   | Comparable | Comparison | `IComparable`, `IComparable<T>`, `CompareTo()` | Possibly, but not clear what the best options or existing Sharpy interfaces are (probably there is one). |
   | Equatable | Equality | `IEquatable<T>`, `Equals()`, `GetHashCode()` | Yes, through `Sharpy.Core.Object` and custom inheritance from subclasses thereof. |
   | Formattable | Display | `IFormattable`, `ToString(format, provider)` | Not in v0.5. |
   | Cloneable | Cloning | `ICloneable`, `Clone()` | Not in v0.5. |
   | Convertible | Type Conversion | `IConvertible`, `ToInt32()`, `ToDouble()`, etc. | Not in v0.5. |
   | Observable | Events | `IObservable<T>`, `Subscribe()` | Not in v0.5. |
   | Observer | Events | `IObserver<T>`, `OnNext()`, `OnError()`, `OnCompleted()` | Not in v0.5. |
   | Async Enumerable | Async | `IAsyncEnumerable<T>`, `GetAsyncEnumerator()`, `await foreach` | Not in v0.5. |
   | Async Enumerator | Async | `IAsyncEnumerator<T>`, `MoveNextAsync()`, `Current` | Not in v0.5. |
   | Query Provider | LINQ | `IQueryable`, `IQueryable<T>`, `IQueryProvider` | Not in v0.5. |
   | Grouping | LINQ | `IGrouping<K,V>` | Not in v0.5. |
   | Lookup | LINQ | `ILookup<K,V>` | Not in v0.5. |
   | Serializable | Serialization | `[Serializable]`, `ISerializable`, binary/XML/JSON serialization | Not in v0.5. |
   | Notify Property Changed | Data Binding | `INotifyPropertyChanged`, `PropertyChanged` event | Not in v0.5. |
   | Notify Collection Changed | Data Binding | `INotifyCollectionChanged`, `CollectionChanged` event | Not in v0.5. |
   | Service Provider | Dependency Injection | `IServiceProvider`, `GetService()` | Not in v0.5. |
   | Builder Pattern | Design Pattern | Fluent API, method chaining | Not in v0.5. |
   | Factory Pattern | Design Pattern | Static factory methods, `Create()` methods | Not in v0.5. |
   | Nullable Value Type | Type System | `Nullable<T>`, `T?`, `HasValue`, `Value` | Only via `T?` type annotation, no methods or exposed box container yet. |
   | Tuple | Data Structures | `ValueTuple`, deconstruction, named tuples | Yes, essentialy maps to `Sharpy.Core.Tuple` (or rather, the other way around, Sharpy's tuple contains a `ValueTuple`, I think). |
   | Span/Memory | Performance | `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, `ReadOnlyMemory<T>` | Not in v0.5. |
   | String Interpolation | Display | `$"..."`, `FormattableString`, `IFormattable` | Code-generation target of Sharpy f-strings where feasible. |
   | Extension Methods | Language Feature | Static methods with `this` first parameter | Not in v0.5. |
   | LINQ Methods | Query | `Where`, `Select`, `SelectMany`, `GroupBy`, `OrderBy`, etc. | Extensions on Sharpy's `IIterator` interface. |
   | Task/Async Pattern | Async | `Task`, `Task<T>`, `async`/`await`, `ValueTask<T>` | Not in v0.5. |
   | Event Pattern | Events | `event`, `EventHandler`, `EventArgs`, `+=`/`-=` | Not in v0.5. |
   | Indexers | Operators | `this[...]` indexer syntax | Yes, maps to `__getitem__` and `__setitem__` dunders. |
   | Operator Overloading | Operators | `op_Addition`, `op_Equality`, etc. (already supported) | Yes, already supported. |
   | Implicit/Explicit Conversion | Type Conversion | `op_Implicit`, `op_Explicit` | Only for `bool` via dunder `__bool__()` synthesizing to explicit bool conversion operator. |
   | Deconstruction | Pattern Matching | `Deconstruct()` method, `(a, b) = obj` | Not in v0.5. |
   | Range/Index | Operators | `Range`, `Index`, `..` range operator, `^` index-from-end | Can be used to simplify code generation for negative indices in Sharpy, or other range-based item access. |
   | Init-only Properties | Language Feature | `init` accessor | Not in v0.5. |
   | Records | Data Structures | `record` types with value equality | Not in v0.5. |
   | Pattern Matching | Control Flow | `switch` expressions, property patterns, positional patterns | Not in v0.5. |
   | Attributes | Metadata | Custom attributes, reflection-based discovery | Not in v0.5. |

---

## V0.5 Implementation Task List

This section provides a concrete, checkable task list for completing the refactoring work within v0.5 scope. Each task includes specific implementation details, code locations, and acceptance criteria suitable for junior developers.

### Quick Reference

| Phase | Priority | New Files | Modified Files | Estimated Effort |
|-------|----------|-----------|----------------|------------------|
| 1. Primitive Catalog | High | `PrimitiveCatalog.cs`, tests | `OperatorValidator.cs`, `TypeChecker.cs`, `BuiltinRegistry.cs` | 2-3 days |
| 2. Protocol Registry | High | `ProtocolRegistry.cs`, tests | None (foundational) | 1-2 days |
| 3. Protocol Signature Validator | High | `ProtocolSignatureValidator.cs`, tests | `Symbol.cs`, `NameResolver.cs`, `TypeChecker.cs` | 2-3 days |
| 4. Protocol Validator | Medium | `ProtocolValidator.cs`, tests | `TypeChecker.cs` | 2-3 days |
| 5. RoslynEmitter Consolidation | Medium | None | `RoslynEmitter.cs`, `NameMangler.cs` | 1-2 days |
| 6. Type Mapper Consolidation | Low | None | `CodeGen/TypeMapper.cs`, `Discovery/TypeMapper.cs` | 1 day |
| 7. CLR Member Cache | Low | `ClrMemberCache.cs`, tests | `OperatorValidator.cs`, `ProtocolValidator.cs` | 1-2 days |

### Prerequisites

Before starting any phase:
1. Ensure all existing tests pass: `dotnet test`
2. Understand the reference implementations:
   - `OperatorValidator.cs` - pattern for semantic validation
   - `OperatorSignatureValidator.cs` - pattern for signature validation
3. Read the Sharpy.Core interfaces in `src/Sharpy.Core/Collections/Interfaces/`

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests for a specific component
dotnet test --filter "FullyQualifiedName~PrimitiveCatalogTests"
dotnet test --filter "FullyQualifiedName~ProtocolRegistryTests"
dotnet test --filter "FullyQualifiedName~ProtocolSignatureValidatorTests"
dotnet test --filter "FullyQualifiedName~ProtocolValidatorTests"

# Run integration tests (end-to-end)
dotnet test --filter "FullyQualifiedName~Integration"
```

---

### Phase 1: Primitive Catalog (Priority: High)

**Goal**: Replace scattered primitive type checks with an exhaustive, tested registry.

**Why This Matters**: Currently, primitive type checks are scattered across multiple files (`OperatorValidator.cs`, `TypeChecker.cs`, `BuiltinRegistry.cs`, etc.) with duplicate logic that can drift out of sync. Centralizing this in `PrimitiveCatalog` ensures consistency and makes it easy to add new numeric types.

**Files to create/modify**:
| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs` | Create | New registry class |
| `src/Sharpy.Compiler.Tests/Semantic/PrimitiveCatalogTests.cs` | Create | Unit tests |
| `src/Sharpy.Compiler/Semantic/OperatorValidator.cs` | Modify | Replace helper methods |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Modify | Replace helper methods |
| `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` | Modify | Use catalog as source |
| `src/Sharpy.Compiler/Semantic/SemanticType.cs` | Modify | Update `IsAssignableTo()` to use catalog |

**Tasks**:

#### 1.1 Create `PrimitiveCatalog.cs` file structure

Create file at `src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs`:

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Exhaustive registry of primitive types supported by Sharpy.
/// Provides type information, promotion rules, and conversion checking.
/// </summary>
public static class PrimitiveCatalog
{
    // TODO: Implement in subsequent tasks
}
```

- [x] **1.1.1** Define `NumericKind` enum inside the file:
  ```csharp
  public enum NumericKind
  {
      None,           // Not numeric (void, bool, string, char)
      SignedInteger,  // sbyte, short, int, long
      UnsignedInteger,// byte, ushort, uint, ulong
      FloatingPoint,  // float, double
      Decimal         // decimal (128-bit)
  }
  ```

- [x] **1.1.2** Define `PrimitiveInfo` record inside the file:
  ```csharp
  /// <summary>
  /// Describes a primitive type's characteristics.
  /// </summary>
  /// <param name="SharpyName">The name used in Sharpy source code (e.g., "int", "str")</param>
  /// <param name="CSharpName">The C# keyword to emit (e.g., "int", "string")</param>
  /// <param name="ClrType">The .NET runtime type (e.g., typeof(int))</param>
  /// <param name="Kind">The numeric classification</param>
  /// <param name="SizeInBits">Size in bits (8, 16, 32, 64, 128 for decimal)</param>
  /// <param name="IsSigned">True for signed numeric types</param>
  public record PrimitiveInfo(
      string SharpyName,
      string CSharpName,
      Type ClrType,
      NumericKind Kind,
      int SizeInBits,
      bool IsSigned
  );
  ```

- [x] **1.1.3** Create static dictionaries for lookups:
  ```csharp
  private static readonly Dictionary<string, PrimitiveInfo> _bySharpyName = new();
  private static readonly Dictionary<Type, PrimitiveInfo> _byClrType = new();
  ```

- [x] **1.1.4** Add static constructor to populate dictionaries:
  ```csharp
  static PrimitiveCatalog()
  {
      RegisterAll();
  }

  private static void Register(PrimitiveInfo info)
  {
      _bySharpyName[info.SharpyName] = info;
      _byClrType[info.ClrType] = info;
  }

  private static void RegisterAll()
  {
      // TODO: Call Register() for each primitive (task 1.2)
  }
  ```

**Acceptance Criteria**: File compiles with no errors; enums and records are defined.

---

#### 1.2 Register all primitive types

Add registrations inside `RegisterAll()`:

- [x] **1.2.1** Register signed integer types:
  ```csharp
  Register(new PrimitiveInfo("sbyte", "sbyte", typeof(sbyte), NumericKind.SignedInteger, 8, true));
  Register(new PrimitiveInfo("short", "short", typeof(short), NumericKind.SignedInteger, 16, true));
  Register(new PrimitiveInfo("int", "int", typeof(int), NumericKind.SignedInteger, 32, true));
  Register(new PrimitiveInfo("long", "long", typeof(long), NumericKind.SignedInteger, 64, true));
  ```

- [x] **1.2.2** Register unsigned integer types:
  ```csharp
  Register(new PrimitiveInfo("byte", "byte", typeof(byte), NumericKind.UnsignedInteger, 8, false));
  Register(new PrimitiveInfo("ushort", "ushort", typeof(ushort), NumericKind.UnsignedInteger, 16, false));
  Register(new PrimitiveInfo("uint", "uint", typeof(uint), NumericKind.UnsignedInteger, 32, false));
  Register(new PrimitiveInfo("ulong", "ulong", typeof(ulong), NumericKind.UnsignedInteger, 64, false));
  ```

- [x] **1.2.3** Register floating-point types:
  ```csharp
  Register(new PrimitiveInfo("float", "float", typeof(float), NumericKind.FloatingPoint, 32, true));
  Register(new PrimitiveInfo("double", "double", typeof(double), NumericKind.FloatingPoint, 64, true));
  Register(new PrimitiveInfo("decimal", "decimal", typeof(decimal), NumericKind.Decimal, 128, true));
  ```

- [x] **1.2.4** Register non-numeric primitives:
  ```csharp
  Register(new PrimitiveInfo("bool", "bool", typeof(bool), NumericKind.None, 8, false));
  Register(new PrimitiveInfo("char", "char", typeof(char), NumericKind.None, 16, false));
  Register(new PrimitiveInfo("str", "string", typeof(string), NumericKind.None, 0, false));
  Register(new PrimitiveInfo("string", "string", typeof(string), NumericKind.None, 0, false)); // Alias
  ```

- [x] **1.2.5** Register void/None:
  ```csharp
  // NOTE: typeof(void) cannot be used directly in dictionaries as it throws.
  // Use a sentinel or handle void specially. One approach:
  // - Don't register void in _byClrType (skip the ClrType registration)
  // - Only register by name for lookup purposes
  Register(new PrimitiveInfo("None", "void", null!, NumericKind.None, 0, false)); // ClrType is null for void
  Register(new PrimitiveInfo("void", "void", null!, NumericKind.None, 0, false)); // Alias, ClrType is null
  ```

  And update the `Register` method to handle null ClrType:
  ```csharp
  private static void Register(PrimitiveInfo info)
  {
      _bySharpyName[info.SharpyName] = info;
      if (info.ClrType != null)
      {
          _byClrType[info.ClrType] = info;
      }
  }
  ```

**Acceptance Criteria**: 17 primitives registered by name (including aliases). `_bySharpyName.Count >= 17`. `_byClrType` excludes void.

---

#### 1.3 Implement query methods

Add these public static methods to `PrimitiveCatalog`:

- [x] **1.3.1** Basic lookup methods:
  ```csharp
  /// <summary>Returns primitive info for a Sharpy type name, or null if not a primitive.</summary>
  public static PrimitiveInfo? GetByName(string sharpyName)
      => _bySharpyName.GetValueOrDefault(sharpyName);

  /// <summary>Returns primitive info for a CLR type, or null if not a primitive.</summary>
  public static PrimitiveInfo? GetByClrType(Type clrType)
      => _byClrType.GetValueOrDefault(clrType);

  /// <summary>Returns true if the name refers to a registered primitive.</summary>
  public static bool IsPrimitive(string sharpyName)
      => _bySharpyName.ContainsKey(sharpyName);
  ```

- [x] **1.3.2** Numeric classification methods:
  ```csharp
  /// <summary>Returns true if the type is any numeric type (integer, float, or decimal).</summary>
  public static bool IsNumeric(SemanticType type)
  {
      var info = GetPrimitiveInfo(type);
      return info != null && info.Kind != NumericKind.None;
  }

  /// <summary>Returns true if the type is an integer (signed or unsigned).</summary>
  public static bool IsInteger(SemanticType type)
  {
      var info = GetPrimitiveInfo(type);
      return info != null &&
             (info.Kind == NumericKind.SignedInteger || info.Kind == NumericKind.UnsignedInteger);
  }

  /// <summary>Returns true if the type is floating-point (float or double).</summary>
  public static bool IsFloatingPoint(SemanticType type)
  {
      var info = GetPrimitiveInfo(type);
      return info != null && info.Kind == NumericKind.FloatingPoint;
  }
  ```

- [x] **1.3.3** Helper to extract info from `SemanticType`:
  ```csharp
  /// <summary>
  /// Gets PrimitiveInfo from a SemanticType by checking if it's a BuiltinType
  /// and looking up its name or CLR type.
  /// </summary>
  public static PrimitiveInfo? GetPrimitiveInfo(SemanticType type)
  {
      if (type is BuiltinType builtin)
      {
          // Try CLR type first (more reliable)
          if (builtin.ClrType != null && _byClrType.TryGetValue(builtin.ClrType, out var info))
              return info;
          // Fall back to name lookup
          return _bySharpyName.GetValueOrDefault(builtin.Name);
      }
      return null;
  }
  ```

**Acceptance Criteria**: All methods return correct values for `SemanticType.Int`, `SemanticType.Str`, `SemanticType.Bool`.

---

#### 1.4 Implement numeric promotion rules

Reference: [C# Numeric Promotions](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/conversions#implicit-numeric-conversions)

- [x] **1.4.1** Add promotion order constant:
  ```csharp
  // Promotion priority: higher value = wider type
  // When mixing types, the result is the type with higher priority
  private static int GetPromotionPriority(PrimitiveInfo info)
  {
      return info.ClrType switch
      {
          // Decimals don't mix with floats
          _ when info.Kind == NumericKind.Decimal => 100,
          // Floating point: double > float
          Type t when t == typeof(double) => 50,
          Type t when t == typeof(float) => 40,
          // Integers by size and signedness
          Type t when t == typeof(ulong) => 35,
          Type t when t == typeof(long) => 34,
          Type t when t == typeof(uint) => 33,
          Type t when t == typeof(int) => 32,
          Type t when t == typeof(ushort) => 31,
          Type t when t == typeof(short) => 30,
          Type t when t == typeof(byte) => 29,
          Type t when t == typeof(sbyte) => 28,
          _ => 0
      };
  }
  ```

- [x] **1.4.2** Implement `GetPromotedType()`:
  ```csharp
  /// <summary>
  /// Returns the result type when performing arithmetic between two numeric types.
  /// Uses standard .NET numeric promotion rules.
  /// </summary>
  /// <returns>The promoted type, or null if types cannot be combined.</returns>
  public static PrimitiveInfo? GetPromotedType(PrimitiveInfo left, PrimitiveInfo right)
  {
      // Non-numeric types cannot be promoted
      if (left.Kind == NumericKind.None || right.Kind == NumericKind.None)
          return null;

      // Decimal doesn't mix with float/double
      if ((left.Kind == NumericKind.Decimal) != (right.Kind == NumericKind.Decimal))
          return null;

      // Special case: mixing signed and unsigned integers of same size
      // e.g., int + uint -> long (to avoid overflow)
      if (left.Kind != right.Kind &&
          (left.Kind == NumericKind.SignedInteger || left.Kind == NumericKind.UnsignedInteger) &&
          (right.Kind == NumericKind.SignedInteger || right.Kind == NumericKind.UnsignedInteger) &&
          left.SizeInBits == right.SizeInBits)
      {
          // Promote to next larger signed type, or to long if already 32-bit
          var targetSize = left.SizeInBits >= 32 ? 64 : left.SizeInBits * 2;
          return _byClrType.Values.FirstOrDefault(p =>
              p.Kind == NumericKind.SignedInteger && p.SizeInBits == targetSize);
      }

      // Return the type with higher priority
      var leftPriority = GetPromotionPriority(left);
      var rightPriority = GetPromotionPriority(right);

      return leftPriority >= rightPriority ? left : right;
  }

  /// <summary>Overload that accepts SemanticTypes directly.</summary>
  public static SemanticType? GetPromotedType(SemanticType left, SemanticType right)
  {
      var leftInfo = GetPrimitiveInfo(left);
      var rightInfo = GetPrimitiveInfo(right);
      if (leftInfo == null || rightInfo == null)
          return null;

      var promoted = GetPromotedType(leftInfo, rightInfo);
      if (promoted == null)
          return null;

      // Return the matching SemanticType singleton
      return promoted.ClrType switch
      {
          Type t when t == typeof(int) => SemanticType.Int,
          Type t when t == typeof(long) => SemanticType.Long,
          Type t when t == typeof(float) => SemanticType.Float,
          Type t when t == typeof(double) => SemanticType.Double,
          // For null ClrType (void), this shouldn't happen in numeric promotion
          null => SemanticType.Unknown,
          _ => new BuiltinType { Name = promoted.SharpyName, ClrType = promoted.ClrType }
      };
  }
  ```

**Acceptance Criteria**:
- `GetPromotedType(int, int) == int`
- `GetPromotedType(int, long) == long`
- `GetPromotedType(int, float) == float`
- `GetPromotedType(float, double) == double`
- `GetPromotedType(decimal, float) == null` (incompatible)
- `GetPromotedType(int, uint) == long` (mixed signed/unsigned)

---

#### 1.5 Implement conversion checking

- [x] **1.5.1** Implement `CanImplicitlyConvert()`:
  ```csharp
  /// <summary>
  /// Returns true if 'from' can be implicitly converted to 'to' without data loss.
  /// </summary>
  public static bool CanImplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to)
  {
      if (from.ClrType == to.ClrType)
          return true;

      // Non-numeric types only convert to themselves
      if (from.Kind == NumericKind.None || to.Kind == NumericKind.None)
          return false;

      // Decimal only accepts integers, not floats
      if (to.Kind == NumericKind.Decimal)
          return from.Kind == NumericKind.SignedInteger || from.Kind == NumericKind.UnsignedInteger;

      // From decimal: no implicit conversions
      if (from.Kind == NumericKind.Decimal)
          return false;

      // Integer to float/double: always allowed
      if ((from.Kind == NumericKind.SignedInteger || from.Kind == NumericKind.UnsignedInteger) &&
          to.Kind == NumericKind.FloatingPoint)
          return true;

      // Float to double: allowed
      if (from.ClrType == typeof(float) && to.ClrType == typeof(double))
          return true;

      // Integer widening: allowed if target is larger and signedness is compatible
      if ((from.Kind == NumericKind.SignedInteger || from.Kind == NumericKind.UnsignedInteger) &&
          (to.Kind == NumericKind.SignedInteger || to.Kind == NumericKind.UnsignedInteger))
      {
          // Unsigned to signed requires extra bit
          if (!from.IsSigned && to.IsSigned)
              return to.SizeInBits > from.SizeInBits;
          // Signed to unsigned: not implicit
          if (from.IsSigned && !to.IsSigned)
              return false;
          // Same signedness: size must be >=
          return to.SizeInBits >= from.SizeInBits;
      }

      return false;
  }
  ```

- [x] **1.5.2** Implement `CanExplicitlyConvert()`:
  ```csharp
  /// <summary>
  /// Returns true if 'from' can be explicitly cast to 'to' (may lose data).
  /// </summary>
  public static bool CanExplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to)
  {
      // Anything numeric can be explicitly cast to any other numeric
      if (from.Kind != NumericKind.None && to.Kind != NumericKind.None)
          return true;

      // char <-> integer explicit conversions
      if (from.ClrType == typeof(char) &&
          (to.Kind == NumericKind.SignedInteger || to.Kind == NumericKind.UnsignedInteger))
          return true;
      if (to.ClrType == typeof(char) &&
          (from.Kind == NumericKind.SignedInteger || from.Kind == NumericKind.UnsignedInteger))
          return true;

      return CanImplicitlyConvert(from, to);
  }
  ```

**Acceptance Criteria**:
- `CanImplicitlyConvert(int, long) == true`
- `CanImplicitlyConvert(long, int) == false`
- `CanImplicitlyConvert(int, float) == true`
- `CanExplicitlyConvert(float, int) == true`

---

#### 1.6 Write comprehensive tests

Create file at `src/Sharpy.Compiler.Tests/Semantic/PrimitiveCatalogTests.cs`:

- [x] **1.6.1** Test file setup:
  ```csharp
  using Xunit;
  using FluentAssertions;
  using Sharpy.Compiler.Semantic;

  namespace Sharpy.Compiler.Tests.Semantic;

  public class PrimitiveCatalogTests
  {
      // Tests go here
  }
  ```

- [x] **1.6.2** Test all primitives are registered:
  ```csharp
  [Theory]
  [InlineData("int", typeof(int))]
  [InlineData("long", typeof(long))]
  [InlineData("float", typeof(float))]
  [InlineData("double", typeof(double))]
  [InlineData("bool", typeof(bool))]
  [InlineData("str", typeof(string))]
  [InlineData("string", typeof(string))]
  [InlineData("sbyte", typeof(sbyte))]
  [InlineData("byte", typeof(byte))]
  [InlineData("short", typeof(short))]
  [InlineData("ushort", typeof(ushort))]
  [InlineData("uint", typeof(uint))]
  [InlineData("ulong", typeof(ulong))]
  [InlineData("char", typeof(char))]
  [InlineData("decimal", typeof(decimal))]
  public void GetByName_ReturnsCorrectClrType(string name, Type expectedClrType)
  {
      var info = PrimitiveCatalog.GetByName(name);
      info.Should().NotBeNull();
      info!.ClrType.Should().Be(expectedClrType);
  }
  ```

- [x] **1.6.3** Test numeric classification:
  ```csharp
  [Fact]
  public void IsNumeric_ReturnsTrueForNumericTypes()
  {
      PrimitiveCatalog.IsNumeric(SemanticType.Int).Should().BeTrue();
      PrimitiveCatalog.IsNumeric(SemanticType.Long).Should().BeTrue();
      PrimitiveCatalog.IsNumeric(SemanticType.Float).Should().BeTrue();
      PrimitiveCatalog.IsNumeric(SemanticType.Double).Should().BeTrue();
  }

  [Fact]
  public void IsNumeric_ReturnsFalseForNonNumericTypes()
  {
      PrimitiveCatalog.IsNumeric(SemanticType.Bool).Should().BeFalse();
      PrimitiveCatalog.IsNumeric(SemanticType.Str).Should().BeFalse();
      PrimitiveCatalog.IsNumeric(SemanticType.Void).Should().BeFalse();
  }

  [Fact]
  public void IsInteger_CorrectlyClassifiesTypes()
  {
      PrimitiveCatalog.IsInteger(SemanticType.Int).Should().BeTrue();
      PrimitiveCatalog.IsInteger(SemanticType.Long).Should().BeTrue();
      PrimitiveCatalog.IsInteger(SemanticType.Float).Should().BeFalse();
      PrimitiveCatalog.IsInteger(SemanticType.Double).Should().BeFalse();
  }
  ```

- [x] **1.6.4** Test promotion rules:
  ```csharp
  [Theory]
  [InlineData("int", "int", "int")]
  [InlineData("int", "long", "long")]
  [InlineData("int", "float", "float")]
  [InlineData("float", "double", "double")]
  [InlineData("long", "double", "double")]
  [InlineData("byte", "int", "int")]
  [InlineData("int", "uint", "long")]   // Mixed signed/unsigned promotes to larger signed
  [InlineData("short", "ushort", "int")] // 16-bit mixed promotes to 32-bit signed
  public void GetPromotedType_ReturnsCorrectType(string left, string right, string expected)
  {
      var leftInfo = PrimitiveCatalog.GetByName(left)!;
      var rightInfo = PrimitiveCatalog.GetByName(right)!;
      var expectedInfo = PrimitiveCatalog.GetByName(expected)!;

      var result = PrimitiveCatalog.GetPromotedType(leftInfo, rightInfo);
      result.Should().NotBeNull();
      result!.SharpyName.Should().Be(expectedInfo.SharpyName);
  }

  [Fact]
  public void GetPromotedType_ReturnsNullForIncompatibleTypes()
  {
      var decimalInfo = PrimitiveCatalog.GetByName("decimal")!;
      var floatInfo = PrimitiveCatalog.GetByName("float")!;

      PrimitiveCatalog.GetPromotedType(decimalInfo, floatInfo).Should().BeNull();
  }
  ```

- [x] **1.6.5** Test implicit conversion:
  ```csharp
  [Theory]
  [InlineData("int", "long", true)]
  [InlineData("int", "float", true)]
  [InlineData("float", "double", true)]
  [InlineData("long", "int", false)]       // Narrowing
  [InlineData("float", "int", false)]      // Float to int
  [InlineData("int", "uint", false)]       // Signed to unsigned
  [InlineData("byte", "short", true)]      // Unsigned widening
  public void CanImplicitlyConvert_ReturnsExpectedResult(string from, string to, bool expected)
  {
      var fromInfo = PrimitiveCatalog.GetByName(from)!;
      var toInfo = PrimitiveCatalog.GetByName(to)!;

      PrimitiveCatalog.CanImplicitlyConvert(fromInfo, toInfo).Should().Be(expected);
  }
  ```

**Acceptance Criteria**: All tests pass. Run with `dotnet test --filter "FullyQualifiedName~PrimitiveCatalogTests"`.

---

#### 1.7 Refactor `OperatorValidator.cs`

Location: `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`

- [ ] **1.7.1** Replace `IsNumericType()` method (around line 808):

  **Before** (find and delete):
  ```csharp
  private bool IsNumericType(SemanticType type)
  {
      return type == SemanticType.Int ||
             type == SemanticType.Long ||
             type == SemanticType.Float ||
             type == SemanticType.Double;
  }
  ```

  **After** (replace with):
  ```csharp
  private static bool IsNumericType(SemanticType type)
      => PrimitiveCatalog.IsNumeric(type);
  ```

- [ ] **1.7.2** Replace `IsIntegerType()` method (around line 814):

  **Before** (find and delete):
  ```csharp
  private bool IsIntegerType(SemanticType type)
  {
      return type == SemanticType.Int || type == SemanticType.Long;
  }
  ```

  **After** (replace with):
  ```csharp
  private static bool IsIntegerType(SemanticType type)
      => PrimitiveCatalog.IsInteger(type);
  ```

- [ ] **1.7.3** Replace `InferNumericResultType()` method (around line 822):

  **Before** (find and delete):
  ```csharp
  private SemanticType InferNumericResultType(SemanticType left, SemanticType right)
  {
      // Double beats everything
      if (left == SemanticType.Double || right == SemanticType.Double)
          return SemanticType.Double;
      // Float beats int and long
      if (left == SemanticType.Float || right == SemanticType.Float)
          return SemanticType.Float;
      // Long beats int
      if (left == SemanticType.Long || right == SemanticType.Long)
          return SemanticType.Long;
      // Both must be int
      return SemanticType.Int;
  }
  ```

  **After** (replace with):
  ```csharp
  private static SemanticType InferNumericResultType(SemanticType left, SemanticType right)
  {
      var promoted = PrimitiveCatalog.GetPromotedType(left, right);
      if (promoted == null)
      {
          // Log warning for debugging - this shouldn't happen for valid numeric types
          return SemanticType.Unknown;
      }
      return promoted;
  }
  ```

  > **NOTE**: The original implementation didn't handle all primitive types (e.g., byte, short, uint).
  > The `PrimitiveCatalog` version is more complete but may return different results for edge cases.
  > Run `OperatorValidatorTests` to verify no regressions.

- [ ] **1.7.4** Verify no other hard-coded primitive checks remain:
  - Search for `SemanticType.Int`, `SemanticType.Long`, `SemanticType.Float`, `SemanticType.Double` in the file
  - Any remaining should be in comparison contexts (`==`) for specific behavior, not type classification

**Acceptance Criteria**: `OperatorValidatorTests` still pass. Run with `dotnet test --filter "FullyQualifiedName~OperatorValidatorTests"`.

---

#### 1.8 Refactor `TypeChecker.cs`

Location: `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

- [ ] **1.8.1** Search for and replace any `IsNumericType()` calls:
  - Use grep: `grep -n "IsNumericType" src/Sharpy.Compiler/Semantic/TypeChecker.cs`
  - If method exists locally, replace implementation with `PrimitiveCatalog.IsNumeric()`
  - If calls exist, update to `PrimitiveCatalog.IsNumeric(type)`

- [ ] **1.8.2** Search for hard-coded primitive comparisons:
  - Use grep: `grep -n "SemanticType.Int\|SemanticType.Long\|SemanticType.Float\|SemanticType.Double" src/Sharpy.Compiler/Semantic/TypeChecker.cs`
  - Evaluate each occurrence: is it checking "is numeric"? If so, use `PrimitiveCatalog.IsNumeric()`

**Acceptance Criteria**: `TypeCheckerTests` still pass. Run with `dotnet test --filter "FullyQualifiedName~TypeCheckerTests"`.

---

#### 1.9 Consolidate `BuiltinRegistry.cs`

Location: `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`

- [ ] **1.9.1** Update `LoadBuiltins()` to use `PrimitiveCatalog`:

  **Before** (lines 24-40):
  ```csharp
  private void LoadBuiltins()
  {
      // Numeric types
      RegisterType("int", typeof(int), TypeKind.Struct);
      RegisterType("long", typeof(long), TypeKind.Struct);
      RegisterType("float", typeof(float), TypeKind.Struct);
      RegisterType("double", typeof(double), TypeKind.Struct);
      RegisterType("decimal", typeof(decimal), TypeKind.Struct);
      // ... more
  }
  ```

  **After**:
  ```csharp
  private void LoadBuiltins()
  {
      // Register all primitives from PrimitiveCatalog
      foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
      {
          // Skip aliases (registered separately if needed)
          if (name != info.SharpyName) continue;

          // Skip void - it's not a type that can be used in declarations
          if (info.ClrType == null) continue;

          var kind = info.ClrType.IsValueType ? TypeKind.Struct : TypeKind.Class;
          RegisterType(info.SharpyName, info.ClrType, kind);
      }

      // Collections (generic) - not in PrimitiveCatalog
      // NOTE: These use Sharpy.Core types, not System.Collections.Generic!
      // The actual type might be Sharpy.Core.List<T>, not System.Collections.Generic.List<T>
      RegisterType("list", typeof(Sharpy.Core.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
      RegisterType("dict", typeof(Sharpy.Core.Dict<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
      RegisterType("set", typeof(Sharpy.Core.Set<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);

      // Special
      RegisterType("object", typeof(object), TypeKind.Class);

      // Load builtin functions using reflection-based discovery
      LoadBuiltinFunctions();
  }
  ```

  > **NOTE**: The collection types should reference `Sharpy.Core.List<>`, `Sharpy.Core.Dict<,>`, etc.,
  > not `System.Collections.Generic.*`. Verify the actual type names in `Sharpy.Core` before implementing.

- [ ] **1.9.2** Add `GetAllPrimitives()` method to `PrimitiveCatalog`:
  ```csharp
  /// <summary>Returns all registered primitives for iteration.</summary>
  public static IEnumerable<(string Name, PrimitiveInfo Info)> GetAllPrimitives()
      => _bySharpyName.Select(kv => (kv.Key, kv.Value));
  ```

**Acceptance Criteria**: Integration tests still pass. Run with `dotnet test --filter "FullyQualifiedName~Integration"`.

---

### Phase 2: Protocol Registry (Priority: High)

**Goal**: Create a centralized registry mapping Python dunders to Sharpy.Core interfaces.

**Why This Matters**: Protocol dunders like `__len__`, `__iter__`, `__contains__` are currently handled with scattered string literals in `NameMangler.cs`, `RoslynEmitter.cs`, and `TypeChecker.cs`. Centralizing this mapping ensures consistency between semantic analysis and code generation.

**Files to create/modify**:
| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/Semantic/ProtocolRegistry.cs` | Create | New registry class |
| `src/Sharpy.Compiler.Tests/Semantic/ProtocolRegistryTests.cs` | Create | Unit tests |

**Reference**: Sharpy.Core interfaces are in `src/Sharpy.Core/Collections/Interfaces/`:
- `ISized` → `__Len__()` returns `uint`
- `IContainer<T>` → `__Contains__(T)` returns `bool`
- `IIterable<T>` → `__Iter__()` returns `Iterator<T>`
- `ISequence<T>` → `__GetItem__(int)` returns `T`
- `IMutableSequence<T>` → `__SetItem__(int, T)` returns void

**Tasks**:

#### 2.1 Create `ProtocolRegistry.cs` file structure

Create file at `src/Sharpy.Compiler/Semantic/ProtocolRegistry.cs`:

- [ ] **2.1.1** Define `ProtocolKind` enum:
  ```csharp
  namespace Sharpy.Compiler.Semantic;

  /// <summary>
  /// Categorizes protocol dunders by their semantic purpose.
  /// </summary>
  public enum ProtocolKind
  {
      Lifecycle,      // __init__, __del__, __new__
      Container,      // __len__, __contains__, __getitem__, __setitem__, __delitem__
      Iterator,       // __iter__, __next__
      Representation, // __str__, __repr__, __format__
      Comparison,     // __eq__, __ne__, __lt__, __le__, __gt__, __ge__ (note: also in OperatorSignatureValidator)
      Hashing,        // __hash__
      Conversion,     // __bool__, __int__, __float__, __complex__
      Arithmetic,     // Handled by OperatorSignatureValidator, listed for completeness
      Bitwise         // Handled by OperatorSignatureValidator, listed for completeness
  }
  ```

- [ ] **2.1.2** Define `ProtocolInfo` record:
  ```csharp
  /// <summary>
  /// Describes a protocol dunder method and its mappings.
  /// </summary>
  /// <param name="DunderName">Lowercase Sharpy source name (e.g., "__len__")</param>
  /// <param name="Kind">The protocol category</param>
  /// <param name="SharpyCoreInterface">The Sharpy.Core interface name (e.g., "ISized"), or null if no interface</param>
  /// <param name="InterfaceMethodName">PascalCase method in Sharpy.Core (e.g., "__Len__")</param>
  /// <param name="ClrMethodName">The .NET method/property name (e.g., "get_Count")</param>
  /// <param name="ExpectedParamCount">Expected parameter count including 'self'</param>
  /// <param name="ExpectedReturnType">Expected return type name (e.g., "int", "bool", "str"), or null for any</param>
  public record ProtocolInfo(
      string DunderName,
      ProtocolKind Kind,
      string? SharpyCoreInterface,
      string InterfaceMethodName,
      string? ClrMethodName,
      int ExpectedParamCount,
      string? ExpectedReturnType
  );
  ```

- [ ] **2.1.3** Create `ProtocolRegistry` static class skeleton:
  ```csharp
  /// <summary>
  /// Central registry of protocol dunder methods and their mappings to Sharpy.Core interfaces.
  /// </summary>
  public static class ProtocolRegistry
  {
      private static readonly Dictionary<string, ProtocolInfo> _protocols = new();

      static ProtocolRegistry()
      {
          RegisterAllProtocols();
      }

      private static void Register(ProtocolInfo info)
      {
          _protocols[info.DunderName] = info;
      }

      private static void RegisterAllProtocols()
      {
          // TODO: Add registrations in task 2.2
      }
  }
  ```

**Acceptance Criteria**: File compiles with no errors.

---

#### 2.2 Register v0.5 protocol dunders

Add registrations inside `RegisterAllProtocols()`:

- [ ] **2.2.1** Register lifecycle protocols:
  ```csharp
  // Lifecycle - constructor (special handling, no interface)
  Register(new ProtocolInfo(
      DunderName: "__init__",
      Kind: ProtocolKind.Lifecycle,
      SharpyCoreInterface: null, // Constructors don't map to an interface
      InterfaceMethodName: ".ctor", // Internal representation
      ClrMethodName: ".ctor",
      ExpectedParamCount: -1, // -1 means any count (but first must be 'self')
      ExpectedReturnType: "None"
  ));
  ```

- [ ] **2.2.2** Register container protocols:
  ```csharp
  // Container - length
  // NOTE: ExpectedReturnType is "int" for Sharpy source compatibility, but
  // ISized.__Len__() returns uint. PrimitiveCatalog.CanImplicitlyConvert(uint, int)
  // returns false, so codegen must handle the conversion explicitly.
  Register(new ProtocolInfo(
      DunderName: "__len__",
      Kind: ProtocolKind.Container,
      SharpyCoreInterface: "Sharpy.Collections.Interfaces.ISized",
      InterfaceMethodName: "__Len__",
      ClrMethodName: "get_Count", // Property getter
      ExpectedParamCount: 1, // Just 'self'
      ExpectedReturnType: "int" // Sharpy uses int; codegen handles uint<->int conversion
  ));

  // Container - membership test
  Register(new ProtocolInfo(
      DunderName: "__contains__",
      Kind: ProtocolKind.Container,
      SharpyCoreInterface: "Sharpy.Collections.Interfaces.IContainer`1",
      InterfaceMethodName: "__Contains__",
      ClrMethodName: "Contains",
      ExpectedParamCount: 2, // self, item
      ExpectedReturnType: "bool"
  ));

  // Container - get item
  Register(new ProtocolInfo(
      DunderName: "__getitem__",
      Kind: ProtocolKind.Container,
      SharpyCoreInterface: "Sharpy.Collections.Interfaces.ISequence`1",
      InterfaceMethodName: "__GetItem__",
      ClrMethodName: "get_Item", // Indexer getter
      ExpectedParamCount: 2, // self, index
      ExpectedReturnType: null // Returns element type (generic)
  ));

  // Container - set item
  Register(new ProtocolInfo(
      DunderName: "__setitem__",
      Kind: ProtocolKind.Container,
      SharpyCoreInterface: "Sharpy.Collections.Interfaces.IMutableSequence`1",
      InterfaceMethodName: "__SetItem__",
      ClrMethodName: "set_Item", // Indexer setter
      ExpectedParamCount: 3, // self, index, value
      ExpectedReturnType: "None"
  ));
  ```

- [ ] **2.2.3** Register iterator protocols:
  ```csharp
  // Iterator - get iterator
  Register(new ProtocolInfo(
      DunderName: "__iter__",
      Kind: ProtocolKind.Iterator,
      SharpyCoreInterface: "Sharpy.Collections.Interfaces.IIterable`1",
      InterfaceMethodName: "__Iter__",
      ClrMethodName: "GetEnumerator",
      ExpectedParamCount: 1, // Just 'self'
      ExpectedReturnType: null // Returns Iterator<T> (generic)
  ));

  // Iterator - get next item
  Register(new ProtocolInfo(
      DunderName: "__next__",
      Kind: ProtocolKind.Iterator,
      SharpyCoreInterface: null, // Iterator<T> is an abstract class, not interface
      InterfaceMethodName: "__Next__",
      ClrMethodName: null, // No direct CLR equivalent (uses MoveNext + Current)
      ExpectedParamCount: 1, // Just 'self'
      ExpectedReturnType: null // Returns element type (generic)
  ));
  ```

- [ ] **2.2.4** Register representation protocols:
  ```csharp
  // Representation - string conversion
  Register(new ProtocolInfo(
      DunderName: "__str__",
      Kind: ProtocolKind.Representation,
      SharpyCoreInterface: "Sharpy.Core.IStrConvertible",
      InterfaceMethodName: "__Str__",
      ClrMethodName: "ToString",
      ExpectedParamCount: 1, // Just 'self'
      ExpectedReturnType: "str"
  ));

  // Representation - repr (debug string)
  // NOTE: __repr__ maps to a distinct method, not ToString. In Sharpy.Core,
  // __Repr__ should return a debug-style representation. For CLR interop,
  // there is no direct equivalent (DebuggerDisplay is an attribute, not method).
  Register(new ProtocolInfo(
      DunderName: "__repr__",
      Kind: ProtocolKind.Representation,
      SharpyCoreInterface: "Sharpy.Core.IRepresentable",
      InterfaceMethodName: "__Repr__",
      ClrMethodName: null, // No direct CLR equivalent; codegen generates separate method
      ExpectedParamCount: 1, // Just 'self'
      ExpectedReturnType: "str"
  ));
  ```

- [ ] **2.2.5** Register hashing protocol:
  ```csharp
  // Hashing
  Register(new ProtocolInfo(
      DunderName: "__hash__",
      Kind: ProtocolKind.Hashing,
      SharpyCoreInterface: "Sharpy.Core.IHashable",
      InterfaceMethodName: "__Hash__",
      ClrMethodName: "GetHashCode",
      ExpectedParamCount: 1, // Just 'self'
      ExpectedReturnType: "int"
  ));
  ```

- [ ] **2.2.6** Register conversion protocols:
  ```csharp
  // Conversion - boolean
  Register(new ProtocolInfo(
      DunderName: "__bool__",
      Kind: ProtocolKind.Conversion,
      SharpyCoreInterface: "Sharpy.Core.IBoolConvertible",
      InterfaceMethodName: "__Bool__",
      ClrMethodName: "op_Explicit", // Generates explicit bool conversion operator
      ExpectedParamCount: 1, // Just 'self'
      ExpectedReturnType: "bool"
  ));
  ```

**Acceptance Criteria**: 11 protocols registered. `_protocols.Count == 11`.

---

#### 2.3 Implement lookup methods

Add these public methods to `ProtocolRegistry`:

- [ ] **2.3.1** Basic lookups:
  ```csharp
  /// <summary>Returns true if the method name is a registered protocol dunder.</summary>
  public static bool IsProtocolDunder(string methodName)
      => _protocols.ContainsKey(methodName);

  /// <summary>Returns protocol info for a dunder name, or null if not registered.</summary>
  public static ProtocolInfo? GetProtocol(string dunderName)
      => _protocols.GetValueOrDefault(dunderName);

  /// <summary>Returns all registered protocols for iteration.</summary>
  public static IEnumerable<ProtocolInfo> GetAllProtocols()
      => _protocols.Values;
  ```

- [ ] **2.3.2** Reverse lookup (interface → dunder):
  ```csharp
  /// <summary>Returns the dunder name for a Sharpy.Core interface, or null.</summary>
  public static string? GetDunderForInterface(string interfaceName)
  {
      foreach (var protocol in _protocols.Values)
      {
          if (protocol.SharpyCoreInterface == interfaceName)
              return protocol.DunderName;
      }
      return null;
  }
  ```

- [ ] **2.3.3** Combined check with OperatorSignatureValidator:
  ```csharp
  /// <summary>
  /// Returns true if the method name is any dunder (protocol or operator).
  /// Use this to detect all special methods.
  /// </summary>
  /// <remarks>
  /// Note: Some dunders like __eq__, __ne__, __lt__, etc. are in BOTH categories
  /// conceptually (comparison protocol + operator), but are only registered in
  /// OperatorSignatureValidator to avoid duplicate validation. This method will
  /// still return true for them via the operator check.
  /// </remarks>
  public static bool IsAnyDunder(string methodName)
  {
      return IsProtocolDunder(methodName) ||
             OperatorSignatureValidator.IsOperatorDunder(methodName);
  }
  ```

- [ ] **2.3.4** Signature expectation lookup:
  ```csharp
  /// <summary>
  /// Returns the expected signature for a protocol dunder.
  /// </summary>
  /// <returns>Tuple of (expected param count, expected return type name), or null if not a protocol.</returns>
  public static (int ParamCount, string? ReturnType)? GetExpectedSignature(string dunderName)
  {
      if (_protocols.TryGetValue(dunderName, out var info))
      {
          return (info.ExpectedParamCount, info.ExpectedReturnType);
      }
      return null;
  }
  ```

**Acceptance Criteria**: Lookup methods return correct values for all registered protocols.

---

#### 2.4 Write comprehensive tests

Create file at `src/Sharpy.Compiler.Tests/Semantic/ProtocolRegistryTests.cs`:

- [ ] **2.4.1** Test file setup:
  ```csharp
  using Xunit;
  using FluentAssertions;
  using Sharpy.Compiler.Semantic;

  namespace Sharpy.Compiler.Tests.Semantic;

  public class ProtocolRegistryTests
  {
  }
  ```

- [ ] **2.4.2** Test all protocols are registered:
  ```csharp
  [Theory]
  [InlineData("__init__", ProtocolKind.Lifecycle)]
  [InlineData("__len__", ProtocolKind.Container)]
  [InlineData("__contains__", ProtocolKind.Container)]
  [InlineData("__getitem__", ProtocolKind.Container)]
  [InlineData("__setitem__", ProtocolKind.Container)]
  [InlineData("__iter__", ProtocolKind.Iterator)]
  [InlineData("__next__", ProtocolKind.Iterator)]
  [InlineData("__str__", ProtocolKind.Representation)]
  [InlineData("__repr__", ProtocolKind.Representation)]
  [InlineData("__hash__", ProtocolKind.Hashing)]
  [InlineData("__bool__", ProtocolKind.Conversion)]
  public void GetProtocol_ReturnsCorrectKind(string dunderName, ProtocolKind expectedKind)
  {
      var info = ProtocolRegistry.GetProtocol(dunderName);
      info.Should().NotBeNull();
      info!.Kind.Should().Be(expectedKind);
  }
  ```

- [ ] **2.4.3** Test `IsProtocolDunder`:
  ```csharp
  [Fact]
  public void IsProtocolDunder_ReturnsTrueForRegisteredProtocols()
  {
      ProtocolRegistry.IsProtocolDunder("__len__").Should().BeTrue();
      ProtocolRegistry.IsProtocolDunder("__iter__").Should().BeTrue();
      ProtocolRegistry.IsProtocolDunder("__str__").Should().BeTrue();
  }

  [Fact]
  public void IsProtocolDunder_ReturnsFalseForOperatorDunders()
  {
      // Operator dunders are NOT protocol dunders (they're in OperatorSignatureValidator)
      ProtocolRegistry.IsProtocolDunder("__add__").Should().BeFalse();
      ProtocolRegistry.IsProtocolDunder("__sub__").Should().BeFalse();
      ProtocolRegistry.IsProtocolDunder("__eq__").Should().BeFalse();
  }

  [Fact]
  public void IsProtocolDunder_ReturnsFalseForNonDunders()
  {
      ProtocolRegistry.IsProtocolDunder("regular_method").Should().BeFalse();
      ProtocolRegistry.IsProtocolDunder("MyMethod").Should().BeFalse();
  }
  ```

- [ ] **2.4.4** Test signature expectations:
  ```csharp
  [Theory]
  [InlineData("__len__", 1, "int")]
  [InlineData("__contains__", 2, "bool")]
  [InlineData("__str__", 1, "str")]
  [InlineData("__hash__", 1, "int")]
  [InlineData("__bool__", 1, "bool")]
  public void GetExpectedSignature_ReturnsCorrectValues(string dunder, int paramCount, string returnType)
  {
      var result = ProtocolRegistry.GetExpectedSignature(dunder);
      result.Should().NotBeNull();
      result!.Value.ParamCount.Should().Be(paramCount);
      result.Value.ReturnType.Should().Be(returnType);
  }
  ```

- [ ] **2.4.5** Test `IsAnyDunder` combines both registries:
  ```csharp
  [Fact]
  public void IsAnyDunder_CombinesProtocolAndOperatorDunders()
  {
      // Protocol dunders
      ProtocolRegistry.IsAnyDunder("__len__").Should().BeTrue();
      ProtocolRegistry.IsAnyDunder("__str__").Should().BeTrue();

      // Operator dunders (from OperatorSignatureValidator)
      ProtocolRegistry.IsAnyDunder("__add__").Should().BeTrue();
      ProtocolRegistry.IsAnyDunder("__eq__").Should().BeTrue();

      // Non-dunders
      ProtocolRegistry.IsAnyDunder("regular_method").Should().BeFalse();
  }
  ```

**Acceptance Criteria**: All tests pass. Run with `dotnet test --filter "FullyQualifiedName~ProtocolRegistryTests"`.

---

### Phase 3: Protocol Signature Validator (Priority: High)

**Goal**: Validate non-operator dunder signatures at name resolution time, just like `OperatorSignatureValidator` does for operators.

**Why This Matters**: Currently, `__init__` validation is hard-coded in `TypeChecker.cs` (line 168-175). Other protocol dunders like `__len__`, `__str__`, etc. have no signature validation. This phase adds validation at name resolution time to catch errors early.

**Reference Implementation**: Study `OperatorSignatureValidator.cs` - it demonstrates the pattern:
1. `IsOperatorDunder()` - checks if a method name is a recognized operator dunder
2. `ValidateDunderSignature()` - validates parameter count and return type
3. Integration in `NameResolver.cs` (lines 330-349) - calls validator and caches valid methods

**Files to create/modify**:
| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/Semantic/ProtocolSignatureValidator.cs` | Create | Validation class |
| `src/Sharpy.Compiler.Tests/Semantic/ProtocolSignatureValidatorTests.cs` | Create | Unit tests |
| `src/Sharpy.Compiler/Semantic/Symbol.cs` | Modify | Add `ProtocolMethods` to `TypeSymbol` |
| `src/Sharpy.Compiler/Semantic/NameResolver.cs` | Modify | Call validator for protocol dunders |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Modify | Remove hard-coded `__init__` validation |

**Tasks**:

#### 3.1 Create `ProtocolSignatureValidator.cs`

Create file at `src/Sharpy.Compiler/Semantic/ProtocolSignatureValidator.cs`:

- [ ] **3.1.1** File skeleton:
  ```csharp
  using Sharpy.Compiler.Parser.Ast;

  namespace Sharpy.Compiler.Semantic;

  /// <summary>
  /// Validates protocol method (non-operator dunder) signatures for Sharpy types.
  /// Complements OperatorSignatureValidator for non-operator dunders.
  /// </summary>
  public static class ProtocolSignatureValidator
  {
      /// <summary>
      /// Checks if a method name is a recognized protocol dunder method.
      /// </summary>
      public static bool IsProtocolDunder(string methodName)
          => ProtocolRegistry.IsProtocolDunder(methodName);

      /// <summary>
      /// Validates the signature of a protocol dunder method.
      /// Returns a list of semantic errors if the signature is invalid.
      /// </summary>
      public static List<SemanticError> ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)
      {
          var errors = new List<SemanticError>();
          var methodName = funcDef.Name;

          var protocol = ProtocolRegistry.GetProtocol(methodName);
          if (protocol == null)
          {
              // Not a protocol dunder, no validation needed
              return errors;
          }

          // Validate based on protocol-specific rules
          ValidateParameterCount(funcDef, protocol, owningType, errors);
          ValidateReturnType(funcDef, protocol, owningType, errors);
          ValidateSelfParameter(funcDef, protocol, owningType, errors);

          return errors;
      }
  }
  ```

- [ ] **3.1.2** Implement `ValidateParameterCount()`:
  ```csharp
  private static void ValidateParameterCount(
      FunctionDef funcDef,
      ProtocolInfo protocol,
      TypeSymbol owningType,
      List<SemanticError> errors)
  {
      var actualCount = funcDef.Parameters.Count;
      var expectedCount = protocol.ExpectedParamCount;

      // -1 means any count (e.g., __init__ can have any number of params)
      if (expectedCount == -1)
          return;

      if (actualCount != expectedCount)
      {
          var paramDescription = expectedCount == 1 ? "(self)" :
              expectedCount == 2 ? "(self, other)" :
              expectedCount == 3 ? "(self, key, value)" :
              $"({expectedCount} parameters)";

          errors.Add(new SemanticError(
              $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have exactly " +
              $"{expectedCount} parameter{(expectedCount == 1 ? "" : "s")} {paramDescription}, got {actualCount}. " +
              (protocol.SharpyCoreInterface != null
                  ? $"See interface '{protocol.SharpyCoreInterface}' for expected signature."
                  : ""),
              funcDef.LineStart,
              funcDef.ColumnStart));
      }
  }
  ```

- [ ] **3.1.3** Implement `ValidateReturnType()`:
  ```csharp
  private static void ValidateReturnType(
      FunctionDef funcDef,
      ProtocolInfo protocol,
      TypeSymbol owningType,
      List<SemanticError> errors)
  {
      // Skip if no return type expectation (null means any type is valid)
      if (protocol.ExpectedReturnType == null)
          return;

      // Skip if no return type annotation (will be inferred or default to void)
      if (funcDef.ReturnType == null)
          return;

      var actualReturnType = GetTypeAnnotationName(funcDef.ReturnType);

      // Normalize: "None" and "void" are equivalent
      var expectedNormalized = protocol.ExpectedReturnType == "None" ? "void" : protocol.ExpectedReturnType;
      var actualNormalized = actualReturnType == "None" ? "void" : actualReturnType;

      if (!string.Equals(actualNormalized, expectedNormalized, StringComparison.OrdinalIgnoreCase) &&
          !string.Equals(actualReturnType, protocol.ExpectedReturnType, StringComparison.OrdinalIgnoreCase))
      {
          errors.Add(new SemanticError(
              $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must return " +
              $"'{protocol.ExpectedReturnType}', got '{actualReturnType}'. " +
              (protocol.SharpyCoreInterface != null
                  ? $"See interface '{protocol.SharpyCoreInterface}' for expected signature."
                  : ""),
              funcDef.LineStart,
              funcDef.ColumnStart));
      }
  }

  /// <summary>Helper to get string name from TypeAnnotation.</summary>
  private static string GetTypeAnnotationName(TypeAnnotation? typeAnnotation)
  {
      if (typeAnnotation == null)
          return "void";
      // Handle generic types like list[int]
      if (typeAnnotation.TypeArguments.Count > 0)
      {
          var args = string.Join(", ", typeAnnotation.TypeArguments.Select(GetTypeAnnotationName));
          return $"{typeAnnotation.Name}[{args}]";
      }
      return typeAnnotation.Name;
  }
  ```

- [ ] **3.1.4** Implement `ValidateSelfParameter()`:
  ```csharp
  private static void ValidateSelfParameter(
      FunctionDef funcDef,
      ProtocolInfo protocol,
      TypeSymbol owningType,
      List<SemanticError> errors)
  {
      // All protocol dunders must have 'self' as first parameter (except static, but protocols aren't static)
      if (funcDef.Parameters.Count == 0)
      {
          errors.Add(new SemanticError(
              $"Protocol method '{protocol.DunderName}' on '{owningType.Name}' must have 'self' as first parameter",
              funcDef.LineStart,
              funcDef.ColumnStart));
          return;
      }

      if (funcDef.Parameters[0].Name != "self")
      {
          errors.Add(new SemanticError(
              $"First parameter of protocol method '{protocol.DunderName}' on '{owningType.Name}' must be " +
              $"'self', got '{funcDef.Parameters[0].Name}'",
              funcDef.LineStart,
              funcDef.ColumnStart));
      }
  }
  ```

**Acceptance Criteria**: File compiles. Validator correctly returns errors for invalid signatures.

---

#### 3.2 Extend `TypeSymbol` to cache protocol methods

Modify `src/Sharpy.Compiler/Semantic/Symbol.cs`:

- [ ] **3.2.1** Add `ProtocolMethods` dictionary to `TypeSymbol`:

  **Find** (around line 48-70):
  ```csharp
  public record TypeSymbol : Symbol
  {
      public TypeKind TypeKind { get; init; }
      public Type? ClrType { get; init; }
      // ... existing properties ...

      // Operator methods (dunder methods for operators)
      // Maps operator dunder names (e.g., "__add__", "__eq__") to lists of overloads
      public Dictionary<string, List<FunctionSymbol>> OperatorMethods { get; init; } = new();
      // ... rest of TypeSymbol
  }
  ```

  **Add after `OperatorMethods`**:
  ```csharp
      // Protocol methods (non-operator dunders like __len__, __str__, __iter__)
      // Maps protocol dunder names to lists of overloads (usually just one, but allows flexibility)
      public Dictionary<string, List<FunctionSymbol>> ProtocolMethods { get; init; } = new();
  ```

**Acceptance Criteria**: `TypeSymbol` has both `OperatorMethods` and `ProtocolMethods` dictionaries.

---

#### 3.3 Integrate into `NameResolver.cs`

Modify `src/Sharpy.Compiler/Semantic/NameResolver.cs`:

- [ ] **3.3.1** Add protocol validation after operator validation:

  **Find** (around lines 330-349):
  ```csharp
          // Validate and register operator dunder methods
          if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
          {
              var validationErrors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);

              if (validationErrors.Count > 0)
              {
                  // Add all validation errors to the errors list
                  _errors.AddRange(validationErrors);
              }
              else
              {
                  // Signature is valid, add to operator methods cache
                  if (!owningType.OperatorMethods.ContainsKey(method.Name))
                  {
                      owningType.OperatorMethods[method.Name] = new List<FunctionSymbol>();
                  }
                  owningType.OperatorMethods[method.Name].Add(funcSymbol);

                  _logger.LogDebug($"Registered operator method: {owningType.Name}.{method.Name}");
              }
          }
  ```

  **Add after the closing brace of the operator validation block**:
  ```csharp
          // Validate and register protocol dunder methods
          else if (ProtocolSignatureValidator.IsProtocolDunder(method.Name))
          {
              var validationErrors = ProtocolSignatureValidator.ValidateDunderSignature(method, owningType);

              if (validationErrors.Count > 0)
              {
                  // Add all validation errors to the errors list
                  _errors.AddRange(validationErrors);
              }
              else
              {
                  // Signature is valid, add to protocol methods cache
                  if (!owningType.ProtocolMethods.ContainsKey(method.Name))
                  {
                      owningType.ProtocolMethods[method.Name] = new List<FunctionSymbol>();
                  }
                  owningType.ProtocolMethods[method.Name].Add(funcSymbol);

                  _logger.LogDebug($"Registered protocol method: {owningType.Name}.{method.Name}");
              }
          }
  ```

**Acceptance Criteria**: Protocol dunders are validated at name resolution time. Invalid signatures produce semantic errors.

---

#### 3.4 Remove hard-coded `__init__` validation from `TypeChecker.cs`

Modify `src/Sharpy.Compiler/Semantic/TypeChecker.cs`:

- [ ] **3.4.1** Remove or simplify `__init__` special case:

  **Find** (around lines 168-180):
  ```csharp
          // Special case: __init__ always returns None/void
          if (functionDef.Name == "__init__")
          {
              // Validate that __init__ has no return type or -> None
              if (functionDef.ReturnType != null && returnType != SemanticType.Void)
              {
                  AddError($"Constructor '__init__' cannot have return type '{returnType.GetDisplayName()}'. " +
                           "Constructors must have no return type annotation or '-> None'.",
                      functionDef.LineStart, functionDef.ColumnStart);
              }
              returnType = SemanticType.Void;
          }
  ```

  **Replace with** (signature validation is now in `ProtocolSignatureValidator`):
  ```csharp
          // __init__ always returns void (signature validation is in ProtocolSignatureValidator)
          if (functionDef.Name == "__init__")
          {
              returnType = SemanticType.Void;
          }
  ```

**Acceptance Criteria**: `__init__` return type validation now happens in `ProtocolSignatureValidator`. TypeChecker only sets the return type to void.

---

#### 3.5 Write comprehensive tests

Create file at `src/Sharpy.Compiler.Tests/Semantic/ProtocolSignatureValidatorTests.cs`:

- [ ] **3.5.1** Test file setup (follow pattern from `OperatorSignatureValidatorTests.cs`):
  ```csharp
  using Xunit;
  using FluentAssertions;
  using Sharpy.Compiler.Semantic;
  using Sharpy.Compiler.Parser.Ast;

  namespace Sharpy.Compiler.Tests.Semantic;

  public class ProtocolSignatureValidatorTests
  {
      private FunctionDef CreateProtocolMethod(
          string name,
          int paramCount,
          string? returnTypeName = null,
          int lineStart = 1,
          int columnStart = 1)
      {
          var parameters = new List<Parameter>();
          for (int i = 0; i < paramCount; i++)
          {
              parameters.Add(new Parameter
              {
                  Name = i == 0 ? "self" : $"param{i}",
                  Type = new TypeAnnotation { Name = "object" },
                  LineStart = lineStart,
                  ColumnStart = columnStart
              });
          }

          var returnType = returnTypeName != null
              ? new TypeAnnotation { Name = returnTypeName }
              : null;

          return new FunctionDef
          {
              Name = name,
              Parameters = parameters,
              ReturnType = returnType,
              Body = new(),
              LineStart = lineStart,
              ColumnStart = columnStart
          };
      }

      private TypeSymbol CreateTypeSymbol(string name = "TestClass")
      {
          return new TypeSymbol
          {
              Name = name,
              Kind = SymbolKind.Type,
              TypeKind = TypeKind.Class
          };
      }
  }
  ```

- [ ] **3.5.2** Test `IsProtocolDunder`:
  ```csharp
  [Theory]
  [InlineData("__len__", true)]
  [InlineData("__str__", true)]
  [InlineData("__iter__", true)]
  [InlineData("__init__", true)]
  [InlineData("__add__", false)]  // Operator, not protocol
  [InlineData("__eq__", false)]   // Also operator (in OperatorSignatureValidator)
  [InlineData("regular_method", false)]
  public void IsProtocolDunder_ReturnsExpectedResult(string name, bool expected)
  {
      ProtocolSignatureValidator.IsProtocolDunder(name).Should().Be(expected);
  }
  ```

  > **NOTE**: `__eq__` is listed in the Protocol Matrix as "Rich Comparison" but is handled by
  > `OperatorSignatureValidator`, not `ProtocolSignatureValidator`. This is intentional: comparison
  > operators are operators first, protocols second. The test confirms this separation.
  ```

- [ ] **3.5.3** Test valid signatures:
  ```csharp
  [Fact]
  public void ValidateDunderSignature_AcceptsValidLen()
  {
      var func = CreateProtocolMethod("__len__", 1, "int");
      var type = CreateTypeSymbol();

      var errors = ProtocolSignatureValidator.ValidateDunderSignature(func, type);

      errors.Should().BeEmpty();
  }

  [Fact]
  public void ValidateDunderSignature_AcceptsValidStr()
  {
      var func = CreateProtocolMethod("__str__", 1, "str");
      var type = CreateTypeSymbol();

      var errors = ProtocolSignatureValidator.ValidateDunderSignature(func, type);

      errors.Should().BeEmpty();
  }

  [Fact]
  public void ValidateDunderSignature_AcceptsValidContains()
  {
      var func = CreateProtocolMethod("__contains__", 2, "bool");
      var type = CreateTypeSymbol();

      var errors = ProtocolSignatureValidator.ValidateDunderSignature(func, type);

      errors.Should().BeEmpty();
  }
  ```

- [ ] **3.5.4** Test invalid parameter counts:
  ```csharp
  [Fact]
  public void ValidateDunderSignature_RejectsLenWithTwoParams()
  {
      var func = CreateProtocolMethod("__len__", 2, "int");  // Should be 1
      var type = CreateTypeSymbol();

      var errors = ProtocolSignatureValidator.ValidateDunderSignature(func, type);

      errors.Should().ContainSingle();
      errors[0].Message.Should().Contain("1 parameter").And.Contain("got 2");
  }

  [Fact]
  public void ValidateDunderSignature_RejectsContainsWithOneParam()
  {
      var func = CreateProtocolMethod("__contains__", 1, "bool");  // Should be 2
      var type = CreateTypeSymbol();

      var errors = ProtocolSignatureValidator.ValidateDunderSignature(func, type);

      errors.Should().ContainSingle();
      errors[0].Message.Should().Contain("2 parameters").And.Contain("got 1");
  }
  ```

- [ ] **3.5.5** Test invalid return types:
  ```csharp
  [Fact]
  public void ValidateDunderSignature_RejectsLenReturningString()
  {
      var func = CreateProtocolMethod("__len__", 1, "str");  // Should return int
      var type = CreateTypeSymbol();

      var errors = ProtocolSignatureValidator.ValidateDunderSignature(func, type);

      errors.Should().ContainSingle();
      errors[0].Message.Should().Contain("'int'").And.Contain("'str'");
  }

  [Fact]
  public void ValidateDunderSignature_RejectsBoolReturningInt()
  {
      var func = CreateProtocolMethod("__bool__", 1, "int");  // Should return bool
      var type = CreateTypeSymbol();

      var errors = ProtocolSignatureValidator.ValidateDunderSignature(func, type);

      errors.Should().ContainSingle();
      errors[0].Message.Should().Contain("'bool'").And.Contain("'int'");
  }
  ```

- [ ] **3.5.6** Test `__init__` special case:
  ```csharp
  [Fact]
  public void ValidateDunderSignature_AllowsInitWithAnyParamCount()
  {
      // __init__ can have any number of parameters
      var func1 = CreateProtocolMethod("__init__", 1, "None");  // Just self
      var func3 = CreateProtocolMethod("__init__", 3, "None");  // self, arg1, arg2
      var type = CreateTypeSymbol();

      ProtocolSignatureValidator.ValidateDunderSignature(func1, type).Should().BeEmpty();
      ProtocolSignatureValidator.ValidateDunderSignature(func3, type).Should().BeEmpty();
  }

  [Fact]
  public void ValidateDunderSignature_RejectsInitWithReturnType()
  {
      var func = CreateProtocolMethod("__init__", 2, "int");  // Should return None
      var type = CreateTypeSymbol();

      var errors = ProtocolSignatureValidator.ValidateDunderSignature(func, type);

      errors.Should().ContainSingle();
      errors[0].Message.Should().Contain("'None'").And.Contain("'int'");
  }
  ```

**Acceptance Criteria**: All tests pass. Run with `dotnet test --filter "FullyQualifiedName~ProtocolSignatureValidatorTests"`.

---

### Phase 4: Protocol Validator (TypeChecker Integration) (Priority: Medium)

**Goal**: Validate protocol usage at type-checking time. When code calls `len(x)`, validate that `x` has `__len__`. When code uses `for item in x`, validate that `x` has `__iter__`.

**Why This Matters**: Currently, the TypeChecker has scattered checks for protocol conformance. For example, `len()` checks if the argument has a `__len__` method, but this logic is duplicated or incomplete. Centralizing this in `ProtocolValidator` (following the `OperatorValidator` pattern) ensures consistent error messages and enables CLR interop.

**Reference Implementation**: Study `OperatorValidator.cs`:
1. Constructor takes `SymbolTable` and `ICompilerLogger`
2. Has caches for resolved types and CLR operators
3. Has validation methods that return the result type or add errors
4. Is instantiated in `TypeChecker` constructor and called from `CheckExpression()`

**Files to create/modify**:
| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs` | Create | Validation class |
| `src/Sharpy.Compiler.Tests/Semantic/ProtocolValidatorTests.cs` | Create | Unit tests |
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Modify | Instantiate and use validator |

**Tasks**:

#### 4.1 Create `ProtocolValidator.cs`

Create file at `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs`:

- [ ] **4.1.1** File skeleton following `OperatorValidator` pattern:
  ```csharp
  using System.Reflection;
  using Sharpy.Compiler.Logging;

  namespace Sharpy.Compiler.Semantic;

  /// <summary>
  /// Validates protocol usage in Sharpy code, supporting both Sharpy dunder methods
  /// and CLR interface implementations for .NET interop.
  ///
  /// NOTE: This class is NOT thread-safe (same as OperatorValidator).
  /// </summary>
  public class ProtocolValidator
  {
      private readonly SymbolTable _symbolTable;
      private readonly ICompilerLogger _logger;
      private readonly List<SemanticError> _errors = new();

      // Cache for CLR protocol discovery (type -> protocols it supports)
      private readonly Dictionary<Type, HashSet<string>> _clrProtocolCache = new();

      public ProtocolValidator(SymbolTable symbolTable, ICompilerLogger? logger = null)
      {
          _symbolTable = symbolTable;
          _logger = logger ?? NullLogger.Instance;
      }

      /// <summary>Gets the errors collected during protocol validation.</summary>
      public IReadOnlyList<SemanticError> Errors => _errors;

      private void AddError(string message, int line, int column)
      {
          _errors.Add(new SemanticError(message, line, column));
          _logger.LogError(message, line, column);
      }
  }
  ```

- [ ] **4.1.2** Implement `HasProtocol()` for Sharpy types:
  ```csharp
  /// <summary>
  /// Checks if a type has a specific protocol dunder method.
  /// </summary>
  public bool HasProtocol(SemanticType type, string dunderName)
  {
      // Check Sharpy user-defined types
      if (type is UserDefinedType udt && udt.Symbol != null)
      {
          // Check cached protocol methods first
          if (udt.Symbol.ProtocolMethods.ContainsKey(dunderName))
              return true;

          // Also check regular methods (some protocols might not be in cache yet)
          if (udt.Symbol.Methods.Any(m => m.Name == dunderName))
              return true;
      }

      // Check CLR types via reflection
      var clrType = GetClrType(type);
      if (clrType != null)
      {
          return HasClrProtocol(clrType, dunderName);
      }

      // Check generic container types (list[T], dict[K,V], set[T])
      if (type is GenericType generic)
      {
          return generic.Name switch
          {
              "list" => dunderName is "__len__" or "__iter__" or "__contains__" or "__getitem__" or "__setitem__",
              "dict" => dunderName is "__len__" or "__iter__" or "__contains__" or "__getitem__" or "__setitem__",
              "set" => dunderName is "__len__" or "__iter__" or "__contains__",
              "tuple" => dunderName is "__len__" or "__iter__" or "__getitem__",
              _ => false
          };
      }

      // Built-in types have hardcoded protocol support for now
      // TODO: Move to explicit registration in PrimitiveCatalog or BuiltinRegistry
      if (type == SemanticType.Str)
      {
          // Strings support __len__, __iter__, __contains__, __getitem__
          return dunderName is "__len__" or "__iter__" or "__contains__" or "__getitem__";
      }

      return false;
  }

  private Type? GetClrType(SemanticType type)
  {
      return type switch
      {
          BuiltinType builtin => builtin.ClrType,
          UserDefinedType udt => udt.Symbol?.ClrType,
          GenericType generic => generic.GenericDefinition?.ClrType,
          _ => null
      };
  }
  ```

- [ ] **4.1.3** Implement CLR protocol discovery:
  ```csharp
  /// <summary>
  /// Checks if a CLR type supports a protocol by examining its interfaces.
  /// Results are cached per CLR type.
  /// </summary>
  private bool HasClrProtocol(Type clrType, string dunderName)
  {
      // Get or build cache for this type
      if (!_clrProtocolCache.TryGetValue(clrType, out var protocols))
      {
          protocols = DiscoverClrProtocols(clrType);
          _clrProtocolCache[clrType] = protocols;
      }

      return protocols.Contains(dunderName);
  }

  private HashSet<string> DiscoverClrProtocols(Type clrType)
  {
      var protocols = new HashSet<string>();

      // Check implemented interfaces
      var interfaces = clrType.GetInterfaces();

      // IEnumerable<T> or IEnumerable -> __iter__
      if (interfaces.Any(i =>
          i == typeof(System.Collections.IEnumerable) ||
          (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))))
      {
          protocols.Add("__iter__");
      }

      // ICollection<T> or ICollection -> __len__, __contains__
      if (interfaces.Any(i =>
          i == typeof(System.Collections.ICollection) ||
          (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>))))
      {
          protocols.Add("__len__");
          protocols.Add("__contains__");
      }

      // IList<T> or IList -> __getitem__, __setitem__
      if (interfaces.Any(i =>
          i == typeof(System.Collections.IList) ||
          (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))))
      {
          protocols.Add("__getitem__");
          protocols.Add("__setitem__");
      }

      // IDictionary<K,V> -> __getitem__, __setitem__, __contains__, __len__
      if (interfaces.Any(i =>
          i == typeof(System.Collections.IDictionary) ||
          (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))))
      {
          protocols.Add("__getitem__");
          protocols.Add("__setitem__");
          protocols.Add("__contains__");
          protocols.Add("__len__");
      }

      // Any object has __str__ (ToString) and __hash__ (GetHashCode)
      protocols.Add("__str__");
      protocols.Add("__hash__");

      return protocols;
  }
  ```

- [ ] **4.1.4** Implement validation methods for specific protocols:
  ```csharp
  /// <summary>
  /// Validates that a type can be used with len() and returns int.
  /// </summary>
  public SemanticType ValidateLen(SemanticType containerType, int line, int column)
  {
      if (!HasProtocol(containerType, "__len__"))
      {
          AddError(
              $"Type '{containerType.GetDisplayName()}' does not support len() " +
              "(missing '__len__' method). Consider implementing ISized interface.",
              line, column);
          return SemanticType.Unknown;
      }
      return SemanticType.Int;
  }

  /// <summary>
  /// Validates that a type is iterable (for 'for' loops and comprehensions).
  /// Returns the element type if known, otherwise Unknown.
  /// </summary>
  public SemanticType ValidateIteration(SemanticType iterableType, int line, int column)
  {
      if (!HasProtocol(iterableType, "__iter__"))
      {
          AddError(
              $"Type '{iterableType.GetDisplayName()}' is not iterable " +
              "(missing '__iter__' method). Consider implementing IIterable<T> interface.",
              line, column);
          return SemanticType.Unknown;
      }

      // Try to infer element type from generic argument
      if (iterableType is GenericType generic && generic.TypeArguments.Count > 0)
      {
          return generic.TypeArguments[0];
      }

      // For strings, element type is str (single characters)
      if (iterableType == SemanticType.Str)
      {
          return SemanticType.Str;
      }

      return SemanticType.Unknown;
  }

  /// <summary>
  /// Validates the 'in' operator (membership test).
  /// </summary>
  public SemanticType ValidateMembership(
      SemanticType containerType,
      SemanticType itemType,
      int line,
      int column)
  {
      if (!HasProtocol(containerType, "__contains__"))
      {
          AddError(
              $"Type '{containerType.GetDisplayName()}' does not support membership testing " +
              "(missing '__contains__' method). Consider implementing IContainer<T> interface.",
              line, column);
          return SemanticType.Unknown;
      }
      return SemanticType.Bool;
  }

  /// <summary>
  /// Validates indexing access (e.g., x[0]).
  /// Returns the element type if known.
  /// </summary>
  public SemanticType ValidateIndexAccess(
      SemanticType containerType,
      SemanticType indexType,
      int line,
      int column)
  {
      if (!HasProtocol(containerType, "__getitem__"))
      {
          AddError(
              $"Type '{containerType.GetDisplayName()}' does not support indexing " +
              "(missing '__getitem__' method). Consider implementing ISequence<T> interface.",
              line, column);
          return SemanticType.Unknown;
      }

      // Infer element type from generic argument
      if (containerType is GenericType generic && generic.TypeArguments.Count > 0)
      {
          return generic.TypeArguments[0];
      }

      // For strings, indexing returns str
      if (containerType == SemanticType.Str)
      {
          return SemanticType.Str;
      }

      return SemanticType.Unknown;
  }

  /// <summary>
  /// Validates boolean conversion (for if/while conditions).
  /// Returns bool.
  /// </summary>
  public SemanticType ValidateBoolConversion(SemanticType type, int line, int column)
  {
      // All types can be used in boolean context in Python/Sharpy
      // __bool__ is optional - if missing, truthiness is determined by:
      // 1. If __len__ exists, truthy if len > 0
      // 2. Otherwise, always truthy (non-None objects)

      // For now, we don't require __bool__ - this is informational
      // Just return bool, no error
      return SemanticType.Bool;
  }
  ```

**Acceptance Criteria**: File compiles. Validator correctly identifies types that support protocols.

---

#### 4.2 Integrate into `TypeChecker.cs`

- [ ] **4.2.1** Add `ProtocolValidator` field and instantiate in constructor:

  **Find** (around lines 16-20):
  ```csharp
  private readonly AccessValidator _accessValidator;
  private readonly OperatorValidator _operatorValidator;
  private readonly ICompilerLogger _logger;
  ```

  **Add after `_operatorValidator`**:
  ```csharp
  private readonly ProtocolValidator _protocolValidator;
  ```

  **Find** constructor (around lines 37-43):
  ```csharp
  public TypeChecker(SymbolTable symbolTable, SemanticInfo semanticInfo, TypeResolver typeResolver, ICompilerLogger? logger = null)
  {
      _symbolTable = symbolTable;
      // ... existing assignments ...
      _operatorValidator = new OperatorValidator(_symbolTable, _logger);
  }
  ```

  **Add after `_operatorValidator` initialization**:
  ```csharp
      _protocolValidator = new ProtocolValidator(_symbolTable, _logger);
  ```

- [ ] **4.2.2** Add `_protocolValidator.Errors` to combined errors:

  **Find** (around lines 47-55):
  ```csharp
  public IReadOnlyList<SemanticError> Errors
  {
      get
      {
          var allErrors = new List<SemanticError>(_errors);
          allErrors.AddRange(_controlFlowValidator.Errors);
          allErrors.AddRange(_accessValidator.Errors);
          allErrors.AddRange(_operatorValidator.Errors);
          return allErrors;
      }
  }
  ```

  **Add before `return`**:
  ```csharp
          allErrors.AddRange(_protocolValidator.Errors);
  ```

- [ ] **4.2.3** Replace hard-coded `for` loop iteration check:

  **Search for** `for` loop handling in `CheckFor()` method and update to use:
  ```csharp
  var elementType = _protocolValidator.ValidateIteration(iterableType, forStmt.LineStart, forStmt.ColumnStart);
  ```

- [ ] **4.2.4** Replace hard-coded `in` operator check:

  **Search for** `in` operator handling (BinaryOperator.In) and update to use:
  ```csharp
  var resultType = _protocolValidator.ValidateMembership(rightType, leftType, line, column);
  ```

- [ ] **4.2.5** Replace hard-coded indexing check:

  **Search for** subscript/indexing handling and update to use:
  ```csharp
  var elementType = _protocolValidator.ValidateIndexAccess(containerType, indexType, line, column);
  ```

- [ ] **4.2.6** Replace hard-coded `len()` check:

  **Search for** `len` builtin call handling and update to use:
  ```csharp
  var resultType = _protocolValidator.ValidateLen(argType, call.LineStart, call.ColumnStart);
  ```

**Acceptance Criteria**: TypeChecker uses ProtocolValidator for all protocol checks. No hard-coded dunder checks remain.

---

#### 4.3 Write comprehensive tests

Create file at `src/Sharpy.Compiler.Tests/Semantic/ProtocolValidatorTests.cs`:

- [ ] **4.3.1** Test file setup:
  ```csharp
  using Xunit;
  using FluentAssertions;
  using Sharpy.Compiler.Semantic;
  using Sharpy.Compiler.Logging;

  namespace Sharpy.Compiler.Tests.Semantic;

  public class ProtocolValidatorTests
  {
      private ProtocolValidator CreateValidator()
      {
          var symbolTable = new SymbolTable();
          return new ProtocolValidator(symbolTable);
      }
  }
  ```

- [ ] **4.3.2** Test `HasProtocol` for built-in types:
  ```csharp
  [Fact]
  public void HasProtocol_StringSupportsExpectedProtocols()
  {
      var validator = CreateValidator();

      validator.HasProtocol(SemanticType.Str, "__len__").Should().BeTrue();
      validator.HasProtocol(SemanticType.Str, "__iter__").Should().BeTrue();
      validator.HasProtocol(SemanticType.Str, "__contains__").Should().BeTrue();
      validator.HasProtocol(SemanticType.Str, "__getitem__").Should().BeTrue();
  }

  [Fact]
  public void HasProtocol_IntDoesNotSupportContainerProtocols()
  {
      var validator = CreateValidator();

      validator.HasProtocol(SemanticType.Int, "__len__").Should().BeFalse();
      validator.HasProtocol(SemanticType.Int, "__iter__").Should().BeFalse();
  }

  [Fact]
  public void HasProtocol_GenericListSupportsContainerProtocols()
  {
      var validator = CreateValidator();
      var listOfInt = new GenericType
      {
          Name = "list",
          TypeArguments = new List<SemanticType> { SemanticType.Int }
      };

      validator.HasProtocol(listOfInt, "__len__").Should().BeTrue();
      validator.HasProtocol(listOfInt, "__iter__").Should().BeTrue();
      validator.HasProtocol(listOfInt, "__getitem__").Should().BeTrue();
      validator.HasProtocol(listOfInt, "__setitem__").Should().BeTrue();
      validator.HasProtocol(listOfInt, "__contains__").Should().BeTrue();
  }

  [Fact]
  public void HasProtocol_GenericSetDoesNotSupportIndexing()
  {
      var validator = CreateValidator();
      var setOfStr = new GenericType
      {
          Name = "set",
          TypeArguments = new List<SemanticType> { SemanticType.Str }
      };

      validator.HasProtocol(setOfStr, "__getitem__").Should().BeFalse();
      validator.HasProtocol(setOfStr, "__setitem__").Should().BeFalse();
      // But set does support these:
      validator.HasProtocol(setOfStr, "__len__").Should().BeTrue();
      validator.HasProtocol(setOfStr, "__iter__").Should().BeTrue();
      validator.HasProtocol(setOfStr, "__contains__").Should().BeTrue();
  }
  ```

- [ ] **4.3.3** Test `ValidateLen`:
  ```csharp
  [Fact]
  public void ValidateLen_ReturnsIntForString()
  {
      var validator = CreateValidator();

      var result = validator.ValidateLen(SemanticType.Str, 1, 1);

      result.Should().Be(SemanticType.Int);
      validator.Errors.Should().BeEmpty();
  }

  [Fact]
  public void ValidateLen_AddsErrorForInt()
  {
      var validator = CreateValidator();

      var result = validator.ValidateLen(SemanticType.Int, 1, 1);

      result.Should().Be(SemanticType.Unknown);
      validator.Errors.Should().ContainSingle();
      validator.Errors[0].Message.Should().Contain("does not support len()");
  }
  ```

- [ ] **4.3.4** Test `ValidateIteration`:
  ```csharp
  [Fact]
  public void ValidateIteration_InfersElementTypeFromGeneric()
  {
      var validator = CreateValidator();
      var listOfInt = new GenericType
      {
          Name = "list",
          TypeArguments = new List<SemanticType> { SemanticType.Int }
      };

      var result = validator.ValidateIteration(listOfInt, 1, 1);

      result.Should().Be(SemanticType.Int);
      validator.Errors.Should().BeEmpty();
  }

  [Fact]
  public void ValidateIteration_AddsErrorForNonIterable()
  {
      var validator = CreateValidator();

      var result = validator.ValidateIteration(SemanticType.Int, 1, 1);

      result.Should().Be(SemanticType.Unknown);
      validator.Errors.Should().ContainSingle();
      validator.Errors[0].Message.Should().Contain("not iterable");
  }
  ```

- [ ] **4.3.5** Test CLR type discovery:
  ```csharp
  [Fact]
  public void HasProtocol_DiscoversCLRListProtocols()
  {
      var validator = CreateValidator();
      var listType = new BuiltinType
      {
          Name = "List<int>",
          ClrType = typeof(List<int>)
      };

      validator.HasProtocol(listType, "__iter__").Should().BeTrue();
      validator.HasProtocol(listType, "__len__").Should().BeTrue();
      validator.HasProtocol(listType, "__getitem__").Should().BeTrue();
      validator.HasProtocol(listType, "__contains__").Should().BeTrue();
  }

  [Fact]
  public void HasProtocol_DiscoversCLRDictionaryProtocols()
  {
      var validator = CreateValidator();
      var dictType = new BuiltinType
      {
          Name = "Dictionary<string, int>",
          ClrType = typeof(Dictionary<string, int>)
      };

      validator.HasProtocol(dictType, "__getitem__").Should().BeTrue();
      validator.HasProtocol(dictType, "__setitem__").Should().BeTrue();
      validator.HasProtocol(dictType, "__contains__").Should().BeTrue();
      validator.HasProtocol(dictType, "__len__").Should().BeTrue();
  }
  ```

**Acceptance Criteria**: All tests pass. Run with `dotnet test --filter "FullyQualifiedName~ProtocolValidatorTests"`.

---

### Phase 5: RoslynEmitter Consolidation (Priority: Medium)

**Goal**: Replace hard-coded dunder mappings in code generation with registry lookups.

**Why This Matters**: The `RoslynEmitter.cs` and `NameMangler.cs` files contain hard-coded dunder mappings that duplicate information in `ProtocolRegistry`. This creates maintenance burden and risk of drift. By using the registries, adding new protocols requires only registry updates.

**Files to modify**:
| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` | Modify | Use registry lookups |
| `src/Sharpy.Compiler/CodeGen/NameMangler.cs` | Modify | Delegate to registries |

**Tasks**:

#### 5.1 Update `RoslynEmitter.cs` dunder detection

- [ ] **5.1.1** Find and replace hard-coded dunder checks:

  **Search for** (around lines 2310-2363 or search for pattern):
  ```csharp
  // Example of hard-coded check (actual code may vary)
  if (func.Name == "__str__" || func.Name == "__repr__")
  ```

  **Replace with registry-based check**:
  ```csharp
  // Use ProtocolRegistry for protocol dunders
  if (ProtocolRegistry.IsProtocolDunder(func.Name))
  {
      var protocol = ProtocolRegistry.GetProtocol(func.Name);
      // Use protocol info for code generation decisions
  }
  // Use OperatorSignatureValidator for operator dunders
  else if (OperatorSignatureValidator.IsOperatorDunder(func.Name))
  {
      // Handle operator dunder
  }
  ```

- [ ] **5.1.2** Add using directive at top of file:
  ```csharp
  using Sharpy.Compiler.Semantic;  // For ProtocolRegistry, OperatorSignatureValidator
  ```

- [ ] **5.1.3** Update `ShouldGenerateDunderMethod()` (if such method exists):

  **Current pattern** (hard-coded switch):
  ```csharp
  private bool ShouldGenerateDunderMethod(string name)
  {
      return name switch
      {
          "__str__" => true,
          "__repr__" => true,
          "__hash__" => true,
          "__len__" => true,
          // ... more cases
          _ => false
      };
  }
  ```

  **Replace with**:
  ```csharp
  private bool ShouldGenerateDunderMethod(string name)
  {
      // __init__ is always generated (as constructor)
      if (name == "__init__")
          return true;

      // Protocol dunders that map to .NET methods
      return ProtocolRegistry.IsProtocolDunder(name);
  }
  ```

- [ ] **5.1.4** Update `GenerateClassMethod()` to use `ProtocolRegistry.GetProtocol()`:

  **Find** (around lines 981-1002):
  ```csharp
  if (func.Name == "__str__" || func.Name == "__repr__")
  {
      returnType = PredefinedType(Token(SyntaxKind.StringKeyword));
  }
  else if (func.Name == "__hash__")
  {
      returnType = PredefinedType(Token(SyntaxKind.IntKeyword));
  }
  ```

  **Replace with**:
  ```csharp
  var protocol = ProtocolRegistry.GetProtocol(func.Name);
  if (protocol != null && protocol.ExpectedReturnType != null)
  {
      returnType = protocol.ExpectedReturnType switch
      {
          "str" or "string" => PredefinedType(Token(SyntaxKind.StringKeyword)),
          "int" => PredefinedType(Token(SyntaxKind.IntKeyword)),
          "bool" => PredefinedType(Token(SyntaxKind.BoolKeyword)),
          "None" or "void" => PredefinedType(Token(SyntaxKind.VoidKeyword)),
          _ => func.ReturnType != null ? _typeMapper.MapType(func.ReturnType) : returnType
      };
  }
  ```

**Acceptance Criteria**: Code generation still works. Run integration tests: `dotnet test --filter "FullyQualifiedName~Integration"`.

---

#### 5.2 Update `NameMangler.cs` to use registries

Location: `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

- [ ] **5.2.1** Refactor `_dunderMethodMap` to query `ProtocolRegistry`:

  **Current** (around lines 28-42):
  ```csharp
  private static readonly Dictionary<string, string> _dunderMethodMap = new()
  {
      { "__init__", "Constructor" },
      { "__str__", "ToString" },
      { "__repr__", "ToString" },
      { "__eq__", "Equals" },
      { "__hash__", "GetHashCode" },
      { "__getitem__", "GetItem" },
      { "__setitem__", "SetItem" },
      { "__len__", "Length" },
      { "__contains__", "Contains" },
      { "__iter__", "GetEnumerator" },
      { "__bool__", "ToBoolean" },
  };
  ```

  **Option A: Keep static but verify against registry** (simpler):
  ```csharp
  // Keep the map for performance, but add a debug assertion to verify consistency
  #if DEBUG
  static NameMangler()
  {
      // Verify all protocol dunders have mappings
      foreach (var protocol in ProtocolRegistry.GetAllProtocols())
      {
          if (protocol.ClrMethodName != null && !_dunderMethodMap.ContainsKey(protocol.DunderName))
          {
              System.Diagnostics.Debug.WriteLine(
                  $"Warning: Protocol '{protocol.DunderName}' missing from _dunderMethodMap");
          }
      }
  }
  #endif
  ```

  **Option B: Build from registry** (more dynamic):
  ```csharp
  private static readonly Dictionary<string, string> _dunderMethodMap;

  static NameMangler()
  {
      _dunderMethodMap = new Dictionary<string, string>
      {
          // Special cases not in registry
          { "__init__", "Constructor" },
          { "__eq__", "Equals" },  // Also an operator, special handling
      };

      // Add all protocol dunders from registry
      foreach (var protocol in ProtocolRegistry.GetAllProtocols())
      {
          if (protocol.ClrMethodName != null && !_dunderMethodMap.ContainsKey(protocol.DunderName))
          {
              // Extract method name from CLR name (e.g., "get_Count" -> "Count", "ToString" -> "ToString")
              var clrName = protocol.ClrMethodName.StartsWith("get_")
                  ? protocol.ClrMethodName[4..]
                  : protocol.ClrMethodName;
              _dunderMethodMap[protocol.DunderName] = clrName;
          }
      }

      // NOTE: Protocols with null ClrMethodName (like __repr__) are intentionally excluded.
      // They require special codegen handling - __repr__ generates a __Repr__() method,
      // not a mapping to an existing .NET method.
  }
  ```

- [ ] **5.2.2** Add using directive:
  ```csharp
  using Sharpy.Compiler.Semantic;  // For ProtocolRegistry
  ```

**Acceptance Criteria**: `NameManglerTests` still pass. Run: `dotnet test --filter "FullyQualifiedName~NameManglerTests"`.

---

#### 5.3 Fix `__div__` vs `__truediv__` inconsistency

- [ ] **5.3.1** Search for `__div__` in codebase:
  ```bash
  grep -r "__div__" src/Sharpy.Compiler/
  ```

- [ ] **5.3.2** Replace any occurrences with `__truediv__`:
  - Python 3 uses `__truediv__` for `/` operator (returns float)
  - Python 2's `__div__` is deprecated
  - `OperatorSignatureValidator` already uses `__truediv__`

**Acceptance Criteria**: No references to `__div__` in compiler code (only `__truediv__`).

---

#### 5.4 Write verification tests

- [ ] **5.4.1** Add test to verify registry covers all codegen dunders:

  Add to existing test file or create `src/Sharpy.Compiler.Tests/CodeGen/RegistryConsistencyTests.cs`:
  ```csharp
  using Xunit;
  using FluentAssertions;
  using Sharpy.Compiler.Semantic;
  using Sharpy.Compiler.CodeGen;

  namespace Sharpy.Compiler.Tests.CodeGen;

  public class RegistryConsistencyTests
  {
      [Fact]
      public void NameMangler_AllProtocolDundersHaveMappings()
      {
          foreach (var protocol in ProtocolRegistry.GetAllProtocols())
          {
              if (protocol.ClrMethodName != null && protocol.DunderName != "__init__")
              {
                  // NameMangler should recognize this dunder
                  var mangled = NameMangler.Transform(protocol.DunderName, NameContext.Method);

                  // Should not just preserve the dunder name unchanged (except operators)
                  if (!OperatorSignatureValidator.IsOperatorDunder(protocol.DunderName))
                  {
                      mangled.Should().NotBe(protocol.DunderName,
                          $"Protocol '{protocol.DunderName}' should be mapped to a C# name");
                  }
              }
          }
      }

      [Fact]
      public void AllDundersAreRecognizedByRegistry()
      {
          // List of all dunders that appear in codegen
          var codegenDunders = new[]
          {
              "__init__", "__str__", "__repr__", "__hash__",
              "__len__", "__contains__", "__getitem__", "__setitem__",
              "__iter__", "__bool__"
          };

          foreach (var dunder in codegenDunders)
          {
              var isProtocol = ProtocolRegistry.IsProtocolDunder(dunder);
              var isOperator = OperatorSignatureValidator.IsOperatorDunder(dunder);

              (isProtocol || isOperator || dunder == "__init__").Should().BeTrue(
                  $"Dunder '{dunder}' should be recognized by ProtocolRegistry or OperatorSignatureValidator");
          }
      }
  }
  ```

**Acceptance Criteria**: Consistency tests pass, confirming registries cover all codegen needs.

---

### Phase 6: Type Mapper Consolidation (Priority: Low)

**Goal**: Eliminate duplicate type mapping logic between `CodeGen/TypeMapper.cs` and `Discovery/TypeMapper.cs`.

**Why This Matters**: Currently there are TWO classes named `TypeMapper`:
1. `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` - Maps Sharpy types to C# syntax (Roslyn `TypeSyntax`)
2. `src/Sharpy.Compiler/Discovery/TypeMapper.cs` - Maps CLR types to Sharpy `SemanticType`

Both contain overlapping primitive type knowledge. This phase consolidates them to use `PrimitiveCatalog`.

**Files to modify**:
| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` | Modify | Use `PrimitiveCatalog` |
| `src/Sharpy.Compiler/Discovery/TypeMapper.cs` | Modify | Use `PrimitiveCatalog` |
| `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` | Modify | Verify uses catalog |

**Tasks**:

#### 6.1 Audit both TypeMapper classes

- [ ] **6.1.1** Document `CodeGen/TypeMapper.cs` purpose:
  - **Input**: Sharpy `TypeAnnotation` (from AST)
  - **Output**: Roslyn `TypeSyntax` (for C# code generation)
  - **Key method**: `MapType(TypeAnnotation?) -> TypeSyntax`
  - **Data**: `_builtinTypeMap` dictionary (Sharpy name → C# syntax string)

- [ ] **6.1.2** Document `Discovery/TypeMapper.cs` purpose:
  - **Input**: CLR `Type` (from reflection)
  - **Output**: Sharpy `SemanticType`
  - **Key method**: `MapClrTypeToSemanticType(Type) -> SemanticType`
  - **Data**: Hard-coded type checks (`if (clrType == typeof(int))...`)

- [ ] **6.1.3** Identify overlap:
  - Both know about primitive types (int, long, float, double, bool, string)
  - Both need CLR type ↔ Sharpy name mappings
  - `PrimitiveCatalog` already has: `SharpyName`, `CSharpName`, `ClrType`

---

#### 6.2 Update `CodeGen/TypeMapper.cs` to use `PrimitiveCatalog`

- [ ] **6.2.1** Replace `_builtinTypeMap` with `PrimitiveCatalog` lookups:

  **Current** (lines 18-44):
  ```csharp
  private static readonly Dictionary<string, string> _builtinTypeMap = new()
  {
      { "int", "int" },
      { "long", "long" },
      { "float", "float" },
      { "double", "double" },
      { "bool", "bool" },
      // ... more primitives
  };
  ```

  **Replace with**:
  ```csharp
  private static readonly Dictionary<string, string> _builtinTypeMap;

  static TypeMapper()
  {
      _builtinTypeMap = new Dictionary<string, string>();

      // Add all primitives from PrimitiveCatalog
      foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
      {
          if (!_builtinTypeMap.ContainsKey(name))
          {
              _builtinTypeMap[name] = info.CSharpName;
          }
      }

      // Add non-primitive type mappings (collections, etc.)
      _builtinTypeMap["list"] = "global::Sharpy.Core.List";
      _builtinTypeMap["dict"] = "global::Sharpy.Core.Dict";
      _builtinTypeMap["set"] = "global::Sharpy.Core.Set";
      _builtinTypeMap["tuple"] = "System.ValueTuple";
      _builtinTypeMap["object"] = "object";
  }
  ```

- [ ] **6.2.2** Add using directive:
  ```csharp
  using Sharpy.Compiler.Semantic;  // For PrimitiveCatalog
  ```

**Acceptance Criteria**: `TypeMapperTests` (in CodeGen folder) still pass.

---

#### 6.3 Update `Discovery/TypeMapper.cs` to use `PrimitiveCatalog`

- [ ] **6.3.1** Replace hard-coded type checks in `MapTypeInternal()`:

  **Current** (lines 24-34):
  ```csharp
  private SemanticType MapTypeInternal(Type clrType)
  {
      if (clrType == typeof(int)) return SemanticType.Int;
      if (clrType == typeof(long)) return SemanticType.Long;
      if (clrType == typeof(float)) return SemanticType.Float;
      if (clrType == typeof(double)) return SemanticType.Double;
      if (clrType == typeof(bool)) return SemanticType.Bool;
      if (clrType == typeof(string)) return SemanticType.Str;
      if (clrType == typeof(void)) return SemanticType.Void;
      if (clrType == typeof(object)) return SemanticType.Object;
      // ... rest of method
  }
  ```

  **Replace with**:
  ```csharp
  private SemanticType MapTypeInternal(Type clrType)
  {
      // Check PrimitiveCatalog first
      var primitiveInfo = PrimitiveCatalog.GetByClrType(clrType);
      if (primitiveInfo != null)
      {
          // Return the appropriate SemanticType singleton or create BuiltinType
          return primitiveInfo.SharpyName switch
          {
              "int" => SemanticType.Int,
              "long" => SemanticType.Long,
              "float" => SemanticType.Float,
              "double" => SemanticType.Double,
              "bool" => SemanticType.Bool,
              "str" or "string" => SemanticType.Str,
              "void" or "None" => SemanticType.Void,
              _ => new BuiltinType { Name = primitiveInfo.SharpyName, ClrType = clrType }
          };
      }

      // Handle object specifically
      if (clrType == typeof(object)) return SemanticType.Object;

      // Handle arrays, nullables, generics... (existing code)
      // ...
  }
  ```

- [ ] **6.3.2** Add using directive:
  ```csharp
  using Sharpy.Compiler.Semantic;  // For PrimitiveCatalog
  ```

**Acceptance Criteria**: `TypeMapperTests` (in Discovery folder) still pass.

---

#### 6.4 Write consolidation verification tests

- [ ] **6.4.1** Add cross-verification test:

  Create or update `src/Sharpy.Compiler.Tests/Semantic/TypeMappingConsistencyTests.cs`:
  ```csharp
  using Xunit;
  using FluentAssertions;
  using Sharpy.Compiler.Semantic;

  namespace Sharpy.Compiler.Tests.Semantic;

  public class TypeMappingConsistencyTests
  {
      [Fact]
      public void PrimitiveCatalog_CoversAllSemanticTypeSingletons()
      {
          // All SemanticType singletons should be in PrimitiveCatalog
          var singletons = new[]
          {
              ("int", SemanticType.Int),
              ("long", SemanticType.Long),
              ("float", SemanticType.Float),
              ("double", SemanticType.Double),
              ("bool", SemanticType.Bool),
              ("str", SemanticType.Str),
          };

          foreach (var (name, semanticType) in singletons)
          {
              var info = PrimitiveCatalog.GetByName(name);
              info.Should().NotBeNull($"Primitive '{name}' should be in catalog");

              if (semanticType is BuiltinType builtin)
              {
                  info!.ClrType.Should().Be(builtin.ClrType,
                      $"CLR type for '{name}' should match");
              }
          }
      }

      [Fact]
      public void CodeGenTypeMapper_AndDiscoveryTypeMapper_AreConsistent()
      {
          // For each primitive in catalog, verify both mappers agree
          foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
          {
              // Skip void (can't be in SemanticType easily)
              if (info.ClrType == typeof(void)) continue;

              // Discovery mapper: CLR -> SemanticType
              var discoveryMapper = new Discovery.TypeMapper();
              var semanticType = discoveryMapper.MapClrTypeToSemanticType(info.ClrType);

              // The mapped SemanticType should have the expected name
              semanticType.GetDisplayName().Should().BeOneOf(
                  info.SharpyName, name,
                  $"Mapper should map {info.ClrType} to '{info.SharpyName}' or '{name}'");
          }
      }
  }
  ```

**Acceptance Criteria**: Consistency tests pass, confirming that all type mappings are derived from `PrimitiveCatalog`:
- Sharpy name → CLR type (via `PrimitiveCatalog.GetByName()`)
- CLR type → Sharpy name (via `PrimitiveCatalog.GetByClrType()`)
- Sharpy name → C# syntax (via `PrimitiveCatalog` in `CodeGen/TypeMapper`)

> **NOTE**: Tasks 6.5 and 6.6 from an earlier draft were merged into 6.2-6.4 above.

---

### Phase 7: CLR Member Cache Extraction (Priority: Low)

**Goal**: Extract CLR reflection caching from `OperatorValidator` into a reusable service that both `OperatorValidator` and `ProtocolValidator` can use.

**Why This Matters**: `OperatorValidator` has a `_clrOperatorCache` that caches CLR operator methods by type. `ProtocolValidator` needs similar caching for interface implementations. Extracting this to a shared service avoids code duplication and ensures consistent caching strategy.

**Files to create/modify**:
| File | Action | Description |
|------|--------|-------------|
| `src/Sharpy.Compiler/Semantic/ClrMemberCache.cs` | Create | Shared caching service |
| `src/Sharpy.Compiler.Tests/Semantic/ClrMemberCacheTests.cs` | Create | Unit tests |
| `src/Sharpy.Compiler/Semantic/OperatorValidator.cs` | Modify | Use shared cache |
| `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs` | Modify | Use shared cache |

**Tasks**:

#### 7.1 Create `ClrMemberCache.cs`

Create file at `src/Sharpy.Compiler/Semantic/ClrMemberCache.cs`:

- [ ] **7.1.1** File structure:
  ```csharp
  using System.Reflection;
  using System.Collections.Generic;

  namespace Sharpy.Compiler.Semantic;

  /// <summary>
  /// Caches CLR type metadata discovered via reflection.
  /// Thread-safe for read operations after initial population.
  ///
  /// NOTE: Cache is populated lazily per-type. Not designed for cross-compilation reuse.
  /// </summary>
  public class ClrMemberCache
  {
      // Operator methods cache: Type -> (operator name like "op_Addition" -> MethodInfo list)
      private readonly Dictionary<Type, Dictionary<string, List<MethodInfo>>> _operatorCache = new();

      // Interface cache: Type -> set of interface types
      private readonly Dictionary<Type, HashSet<Type>> _interfaceCache = new();

      // Indexer cache: Type -> (has indexer, element type)
      private readonly Dictionary<Type, (bool HasIndexer, Type? ElementType)> _indexerCache = new();

      // Enumerator element type cache: Type -> element type (if IEnumerable<T>)
      private readonly Dictionary<Type, Type?> _enumeratorCache = new();
  }
  ```

- [ ] **7.1.2** Implement operator method discovery (extract from `OperatorValidator`):
  ```csharp
  /// <summary>
  /// Gets operator methods for a CLR type, discovering and caching them if needed.
  /// </summary>
  /// <returns>Dictionary mapping operator names (e.g., "op_Addition") to method overloads.</returns>
  public Dictionary<string, List<MethodInfo>> GetOperatorMethods(Type clrType)
  {
      if (_operatorCache.TryGetValue(clrType, out var cached))
      {
          return cached;
      }

      var operators = DiscoverOperatorMethods(clrType);
      _operatorCache[clrType] = operators;
      return operators;
  }

  private Dictionary<string, List<MethodInfo>> DiscoverOperatorMethods(Type clrType)
  {
      var result = new Dictionary<string, List<MethodInfo>>();

      // Find all static operator methods
      var methods = clrType.GetMethods(BindingFlags.Public | BindingFlags.Static)
          .Where(m => m.IsSpecialName && m.Name.StartsWith("op_"));

      foreach (var method in methods)
      {
          if (!result.ContainsKey(method.Name))
          {
              result[method.Name] = new List<MethodInfo>();
          }
          result[method.Name].Add(method);
      }

      return result;
  }
  ```

- [ ] **7.1.3** Implement interface discovery:
  ```csharp
  /// <summary>
  /// Gets all interfaces implemented by a CLR type (including inherited).
  /// </summary>
  public HashSet<Type> GetImplementedInterfaces(Type clrType)
  {
      if (_interfaceCache.TryGetValue(clrType, out var cached))
      {
          return cached;
      }

      var interfaces = new HashSet<Type>(clrType.GetInterfaces());
      _interfaceCache[clrType] = interfaces;
      return interfaces;
  }

  /// <summary>
  /// Checks if a CLR type implements a specific interface (by generic definition).
  /// </summary>
  public bool ImplementsInterface(Type clrType, Type interfaceType)
  {
      var interfaces = GetImplementedInterfaces(clrType);

      if (interfaceType.IsGenericTypeDefinition)
      {
          return interfaces.Any(i =>
              i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);
      }

      return interfaces.Contains(interfaceType);
  }
  ```

- [ ] **7.1.4** Implement indexer discovery:
  ```csharp
  /// <summary>
  /// Checks if a CLR type has an indexer and returns the element type if so.
  /// </summary>
  public (bool HasIndexer, Type? ElementType) GetIndexerInfo(Type clrType)
  {
      if (_indexerCache.TryGetValue(clrType, out var cached))
      {
          return cached;
      }

      // Look for default property (indexer)
      var defaultMembers = clrType.GetDefaultMembers();
      var indexer = defaultMembers.OfType<PropertyInfo>()
          .FirstOrDefault(p => p.GetIndexParameters().Length > 0);

      var result = indexer != null
          ? (true, indexer.PropertyType)
          : (false, (Type?)null);

      _indexerCache[clrType] = result;
      return result;
  }
  ```

- [ ] **7.1.5** Implement enumerator element type discovery:
  ```csharp
  /// <summary>
  /// Gets the element type for an IEnumerable<T> implementation, or null if not enumerable.
  /// </summary>
  public Type? GetEnumerableElementType(Type clrType)
  {
      if (_enumeratorCache.TryGetValue(clrType, out var cached))
      {
          return cached;
      }

      Type? elementType = null;

      // Check for IEnumerable<T>
      var interfaces = GetImplementedInterfaces(clrType);
      var enumerableInterface = interfaces
          .FirstOrDefault(i => i.IsGenericType &&
                               i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

      if (enumerableInterface != null)
      {
          elementType = enumerableInterface.GetGenericArguments()[0];
      }
      // Check if type is itself IEnumerable<T>
      else if (clrType.IsGenericType &&
               clrType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
      {
          elementType = clrType.GetGenericArguments()[0];
      }

      _enumeratorCache[clrType] = elementType;
      return elementType;
  }
  ```

**Acceptance Criteria**: File compiles. All discovery methods work correctly.

---

#### 7.2 Refactor `OperatorValidator.cs` to use `ClrMemberCache`

- [ ] **7.2.1** Add `ClrMemberCache` field:

  **Find** (around lines 22-24):
  ```csharp
  private readonly Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache = new();
  private readonly Dictionary<(UnaryOperator, SemanticType), SemanticType?> _unaryOpCache = new();
  private readonly Dictionary<Type, Dictionary<string, List<MethodInfo>>> _clrOperatorCache = new();
  ```

  **Replace `_clrOperatorCache` with**:
  ```csharp
  private readonly Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache = new();
  private readonly Dictionary<(UnaryOperator, SemanticType), SemanticType?> _unaryOpCache = new();
  private readonly ClrMemberCache _clrMemberCache;
  ```

- [ ] **7.2.2** Update constructor:

  **Find**:
  ```csharp
  public OperatorValidator(SymbolTable symbolTable, ICompilerLogger? logger = null)
  {
      _symbolTable = symbolTable;
      _logger = logger ?? NullLogger.Instance;
  }
  ```

  **Replace with**:
  ```csharp
  public OperatorValidator(SymbolTable symbolTable, ICompilerLogger? logger = null, ClrMemberCache? clrCache = null)
  {
      _symbolTable = symbolTable;
      _logger = logger ?? NullLogger.Instance;
      _clrMemberCache = clrCache ?? new ClrMemberCache();
  }
  ```

- [ ] **7.2.3** Replace `GetOrCacheClrOperators()` calls:

  **Find** method (search for `GetOrCacheClrOperators`):
  ```csharp
  private Dictionary<string, List<MethodInfo>> GetOrCacheClrOperators(Type clrType)
  {
      // ... implementation
  }
  ```

  **Replace all calls** with:
  ```csharp
  var operators = _clrMemberCache.GetOperatorMethods(clrType);
  ```

  **Delete** the `GetOrCacheClrOperators()` method entirely.

**Acceptance Criteria**: `OperatorValidatorTests` still pass.

---

#### 7.3 Update `ProtocolValidator.cs` to use `ClrMemberCache`

- [ ] **7.3.1** Add `ClrMemberCache` field and update constructor:
  ```csharp
  private readonly ClrMemberCache _clrMemberCache;

  public ProtocolValidator(SymbolTable symbolTable, ICompilerLogger? logger = null, ClrMemberCache? clrCache = null)
  {
      _symbolTable = symbolTable;
      _logger = logger ?? NullLogger.Instance;
      _clrMemberCache = clrCache ?? new ClrMemberCache();
  }
  ```

- [ ] **7.3.2** Update `DiscoverClrProtocols()` to use cache:
  ```csharp
  private HashSet<string> DiscoverClrProtocols(Type clrType)
  {
      var protocols = new HashSet<string>();

      // Use cache for interface discovery
      if (_clrMemberCache.ImplementsInterface(clrType, typeof(IEnumerable<>)) ||
          _clrMemberCache.ImplementsInterface(clrType, typeof(System.Collections.IEnumerable)))
      {
          protocols.Add("__iter__");
      }

      if (_clrMemberCache.ImplementsInterface(clrType, typeof(ICollection<>)) ||
          _clrMemberCache.ImplementsInterface(clrType, typeof(System.Collections.ICollection)))
      {
          protocols.Add("__len__");
          protocols.Add("__contains__");
      }

      // Use cache for indexer discovery
      var (hasIndexer, _) = _clrMemberCache.GetIndexerInfo(clrType);
      if (hasIndexer)
      {
          protocols.Add("__getitem__");
          // Check if indexer has setter
          // (simplified - could cache this too)
      }

      // ... rest of protocol discovery
      return protocols;
  }
  ```

**Acceptance Criteria**: `ProtocolValidatorTests` still pass.

---

#### 7.4 Write tests for `ClrMemberCache`

Create file at `src/Sharpy.Compiler.Tests/Semantic/ClrMemberCacheTests.cs`:

- [ ] **7.4.1** Test operator discovery:
  ```csharp
  using Xunit;
  using FluentAssertions;
  using Sharpy.Compiler.Semantic;

  namespace Sharpy.Compiler.Tests.Semantic;

  public class ClrMemberCacheTests
  {
      [Fact]
      public void GetOperatorMethods_FindsIntOperators()
      {
          var cache = new ClrMemberCache();

          var operators = cache.GetOperatorMethods(typeof(int));

          operators.Should().ContainKey("op_Addition");
          operators.Should().ContainKey("op_Subtraction");
          operators.Should().ContainKey("op_Equality");
      }

      [Fact]
      public void GetOperatorMethods_CachesSameResult()
      {
          var cache = new ClrMemberCache();

          var first = cache.GetOperatorMethods(typeof(int));
          var second = cache.GetOperatorMethods(typeof(int));

          first.Should().BeSameAs(second, "Cache should return same dictionary instance");
      }
  }
  ```

- [ ] **7.4.2** Test interface discovery:
  ```csharp
  [Fact]
  public void GetImplementedInterfaces_FindsListInterfaces()
  {
      var cache = new ClrMemberCache();

      var interfaces = cache.GetImplementedInterfaces(typeof(List<int>));

      interfaces.Should().Contain(typeof(IList<int>));
      interfaces.Should().Contain(typeof(IEnumerable<int>));
      interfaces.Should().Contain(typeof(System.Collections.IEnumerable));
  }

  [Fact]
  public void ImplementsInterface_WorksWithGenericDefinition()
  {
      var cache = new ClrMemberCache();

      cache.ImplementsInterface(typeof(List<int>), typeof(IList<>)).Should().BeTrue();
      cache.ImplementsInterface(typeof(List<int>), typeof(IDictionary<,>)).Should().BeFalse();
  }
  ```

- [ ] **7.4.3** Test indexer discovery:
  ```csharp
  [Fact]
  public void GetIndexerInfo_FindsListIndexer()
  {
      var cache = new ClrMemberCache();

      var (hasIndexer, elementType) = cache.GetIndexerInfo(typeof(List<string>));

      hasIndexer.Should().BeTrue();
      elementType.Should().Be(typeof(string));
  }

  [Fact]
  public void GetIndexerInfo_ReturnsFalseForNonIndexable()
  {
      var cache = new ClrMemberCache();

      var (hasIndexer, _) = cache.GetIndexerInfo(typeof(int));

      hasIndexer.Should().BeFalse();
  }
  ```

- [ ] **7.4.4** Test enumerator element type discovery:
  ```csharp
  [Fact]
  public void GetEnumerableElementType_InfersFromIEnumerable()
  {
      var cache = new ClrMemberCache();

      var elementType = cache.GetEnumerableElementType(typeof(List<double>));

      elementType.Should().Be(typeof(double));
  }

  [Fact]
  public void GetEnumerableElementType_ReturnsNullForNonEnumerable()
  {
      var cache = new ClrMemberCache();

      var elementType = cache.GetEnumerableElementType(typeof(int));

      elementType.Should().BeNull();
  }
  ```

**Acceptance Criteria**: All tests pass. Run with `dotnet test --filter "FullyQualifiedName~ClrMemberCacheTests"`.

---

#### 7.5 Share cache between validators (optional optimization)

- [ ] **7.5.1** Consider creating shared cache in `TypeChecker`:
  ```csharp
  // In TypeChecker constructor
  var sharedClrCache = new ClrMemberCache();
  _operatorValidator = new OperatorValidator(_symbolTable, _logger, sharedClrCache);
  _protocolValidator = new ProtocolValidator(_symbolTable, _logger, sharedClrCache);
  ```

  This ensures both validators share the same cached reflection data.

**Acceptance Criteria**: Integration tests pass with shared cache.

---

## Post-V0.5 Roadmap

Features and protocols marked "Not in v0.5" in the Protocol Matrix above, prioritized:

### High Priority (v0.6)
- [ ] Context Manager protocol (`__enter__`, `__exit__` → `IDisposable` integration)
- [ ] Floor division operator (`//` → `__floordiv__`)
- [ ] Property decorator (`@property` → C# properties)
- [ ] `IList<T>`, `IDictionary<K,V>`, `ISet<T>` .NET interface support

### Medium Priority (v0.7+)
- [ ] Async/await (`async def`, `await`, `IAsyncEnumerable<T>`)
- [ ] Numeric conversion protocols (`__int__`, `__float__`)
- [ ] `__format__` for f-string customization
- [ ] Deconstruction (`Deconstruct()` for tuple unpacking)
- [ ] Abstract Base Classes (`@abstractmethod`)

### Low Priority (v1.0+)
- [ ] Extension methods
- [ ] Events (`event` keyword, `+=`/`-=`)
- [ ] Records
- [ ] Pattern matching (`match` statement)

---

## Design Decisions

4. **TOOLING**: The operator mapping tables in `OperatorValidator` could be exposed for:
   - LSP autocomplete (suggest appropriate dunder when user types an operator)
   - Documentation generation (operator reference tables)
   - Consider extracting to a shared metadata format.

5. **PERFORMANCE**: Current caching strategy (per-compilation dictionaries in validators) is simple and effective; monitor whether this scales to large codebases or if cross-compilation caching (via `Discovery/Caching`) becomes necessary.

6. **BALANCE**: Decide the balance between exhaustively defined primitives vs "learned" framework types: fully enumerate primitives and core Sharpy types; rely on reflection + caching for everything else.

7. **SEMANTIC MODEL**: Consider emitting a machine‑readable "semantic model snapshot" (e.g. JSON of registries and caches) for introspection tests and tooling validation, ensuring future changes don't silently re‑introduce hard‑coded special cases.

---

---

## Appendix: Existing Hard-Coded Locations

This appendix documents specific locations of hard-coded semantics identified during analysis, for reference during implementation. **Update these line numbers after each refactoring phase.**

### A.1 `OperatorValidator.cs` - Primitive Type Checks

**Location**: `src/Sharpy.Compiler/Semantic/OperatorValidator.cs` (around lines 800-850)

**Status**: 🔴 To be refactored in Phase 1

```csharp
// Hard-coded primitive checks - refactor to use PrimitiveCatalog
private bool IsNumericType(SemanticType type)
{
    return type == SemanticType.Int ||
           type == SemanticType.Long ||
           type == SemanticType.Float ||
           type == SemanticType.Double;
}

private bool IsIntegerType(SemanticType type)
{
    return type == SemanticType.Int || type == SemanticType.Long;
}

private SemanticType InferNumericResultType(SemanticType left, SemanticType right)
{
    // Hard-coded promotion rules
    if (left == SemanticType.Double || right == SemanticType.Double)
        return SemanticType.Double;
    // ...
}
```

**Refactor to**: `PrimitiveCatalog.IsNumeric()`, `PrimitiveCatalog.IsInteger()`, `PrimitiveCatalog.GetPromotedType()`

---

### A.2 `RoslynEmitter.cs` - Dunder Method Detection

**Location**: `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs` (around lines 980-1010)

**Status**: 🔴 To be refactored in Phase 5

```csharp
// Hard-coded dunder detection
if (func.Name == "__str__" || func.Name == "__repr__")
{
    returnType = PredefinedType(Token(SyntaxKind.StringKeyword));
}
else if (func.Name == "__eq__")
{
    returnType = PredefinedType(Token(SyntaxKind.BoolKeyword));
}
else if (func.Name == "__hash__")
{
    returnType = PredefinedType(Token(SyntaxKind.IntKeyword));
}
```

**Refactor to**: `ProtocolRegistry.GetProtocol(func.Name)` with `ExpectedReturnType`

---

### A.3 `TypeChecker.cs` - `__init__` Validation

**Location**: `src/Sharpy.Compiler/Semantic/TypeChecker.cs` (around lines 168-180)

**Status**: 🔴 To be refactored in Phase 3

```csharp
// Hard-coded __init__ handling
if (functionDef.Name == "__init__")
{
    if (functionDef.ReturnType != null && returnType != SemanticType.Void)
    {
        AddError($"Constructor '__init__' cannot have return type ...");
    }
    returnType = SemanticType.Void;
}
```

**Refactor to**: Move validation to `ProtocolSignatureValidator`, keep only `returnType = SemanticType.Void`

---

### A.4 `BuiltinRegistry.cs` - Type Registration

**Location**: `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` (around lines 24-40)

**Status**: 🔴 To be refactored in Phase 1

```csharp
// Hard-coded type registration
private void LoadBuiltins()
{
    RegisterType("int", typeof(int), TypeKind.Struct);
    RegisterType("long", typeof(long), TypeKind.Struct);
    RegisterType("float", typeof(float), TypeKind.Struct);
    RegisterType("double", typeof(double), TypeKind.Struct);
    RegisterType("decimal", typeof(decimal), TypeKind.Struct);
    RegisterType("bool", typeof(bool), TypeKind.Struct);
    RegisterType("str", typeof(string), TypeKind.Class);
    // ...
}
```

**Refactor to**: Iterate over `PrimitiveCatalog.GetAllPrimitives()`

---

### A.5 `CodeGen/TypeMapper.cs` - Type Mappings

**Location**: `src/Sharpy.Compiler/CodeGen/TypeMapper.cs` (around lines 18-45)

**Status**: 🔴 To be refactored in Phase 6

```csharp
// Hard-coded type mappings
private static readonly Dictionary<string, string> _builtinTypeMap = new()
{
    { "int", "int" },
    { "long", "long" },
    { "float", "float" },
    { "double", "double" },
    { "bool", "bool" },
    { "byte", "byte" },
    // ...
};
```

**Refactor to**: Build from `PrimitiveCatalog.GetAllPrimitives()` in static constructor

---

### A.6 `Discovery/TypeMapper.cs` - CLR Type Mapping

**Location**: `src/Sharpy.Compiler/Discovery/TypeMapper.cs` (around lines 24-35)

**Status**: 🔴 To be refactored in Phase 6

```csharp
// Hard-coded CLR type checks
private SemanticType MapTypeInternal(Type clrType)
{
    if (clrType == typeof(int)) return SemanticType.Int;
    if (clrType == typeof(long)) return SemanticType.Long;
    if (clrType == typeof(float)) return SemanticType.Float;
    if (clrType == typeof(double)) return SemanticType.Double;
    if (clrType == typeof(bool)) return SemanticType.Bool;
    if (clrType == typeof(string)) return SemanticType.Str;
    if (clrType == typeof(void)) return SemanticType.Void;
    // ...
}
```

**Refactor to**: `PrimitiveCatalog.GetByClrType(clrType)` with fallback

---

### A.7 `NameMangler.cs` - Dunder Method Mappings

**Location**: `src/Sharpy.Compiler/CodeGen/NameMangler.cs` (around lines 28-42)

**Status**: 🔴 To be refactored in Phase 5

```csharp
// Hard-coded dunder to C# method mappings
private static readonly Dictionary<string, string> _dunderMethodMap = new()
{
    { "__init__", "Constructor" },
    { "__str__", "ToString" },
    { "__repr__", "ToString" },  // BUG: __repr__ should NOT map to ToString (same as __str__)
    { "__eq__", "Equals" },
    { "__hash__", "GetHashCode" },
    { "__getitem__", "GetItem" },
    { "__setitem__", "SetItem" },
    { "__len__", "Length" },
    { "__contains__", "Contains" },
    { "__iter__", "GetEnumerator" },
    { "__bool__", "ToBoolean" },
};
```

**Refactor to**: Build from `ProtocolRegistry.GetAllProtocols()` with `ClrMethodName`. Note that `__repr__` has `ClrMethodName: null` and requires special codegen handling to generate a distinct `__Repr__()` method.

---

### A.8 `SemanticType.cs` - Implicit Conversion Rules

**Location**: `src/Sharpy.Compiler/Semantic/SemanticType.cs` (around lines 70-80)

**Status**: 🟡 Consider refactoring in Phase 1

```csharp
// In BuiltinType.IsAssignableTo()
public override bool IsAssignableTo(SemanticType other)
{
    if (base.IsAssignableTo(other)) return true;

    // Handle numeric conversions - hard-coded
    if (this == Int && other == Long) return true;
    if (this == Int && other == Float) return true;
    if (this == Int && other == Double) return true;
    if (this == Float && other == Double) return true;
    if (this == Long && other == Double) return true;

    return false;
}
```

**Refactor to**: `PrimitiveCatalog.CanImplicitlyConvert(from, to)`

---

## Legend

- 🔴 **To be refactored** - Hard-coded logic that should be replaced
- 🟡 **Consider refactoring** - Lower priority or optional
- 🟢 **Completed** - Already refactored (update after each phase)
