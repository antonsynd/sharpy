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
│  Protocol Interfaces (already defined, need compiler integration):          │
│  ├── IIterable<T>, IIterator<T>  → __iter__, __next__                       │
│  ├── ISized                       → __len__                                 │
│  ├── IContains<T>                 → __contains__                            │
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
│  │   └── SignatureValidator ✅    (shared signature validation)             │
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

4.5. **Align new work with operator validation architecture**
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

5. **Unify and extend reflection‑based type/method discovery**
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

6. **Strengthen and reuse caching infrastructure**
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

7. **Refactor type analysis and checking to use the new services**
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

3. **DECISION NEEDED**: Determine scope of protocol support:
   - Which Python protocols should Sharpy support (iterator, context manager, descriptor, etc.)?
   - Which .NET patterns should be first-class (IDisposable, IEnumerable<T>, async/await)?
   - Document these decisions to guide registry/validator implementation priorities.

   **Protocol/Pattern Support Decision Matrix:**

   | Protocol/Pattern | Category | Dunder Methods / .NET Interface | Support Now? |
   |-----------------|----------|--------------------------------|--------------|
   | **Python Protocols** | | | |
   | Iterator | Container | `__iter__`, `__next__` | Yes, these should be implemented in terms of Sharpy's native `IIterable` and `IIterator` interfaces, which inherit from .NET's `IEnumerable` and `IEnumerator` interfaces. |
   | Container/Sequence | Container | `__len__`, `__getitem__`, `__setitem__`, `__delitem__`, `__contains__` | Yes, there should be Sharpy interfaces for these, which inherit from appropriate .NET ones if they exist. |
   | Context Manager | Resource | `__enter__`, `__exit__` | Yes, but not a v0.5 feature. Requires some special code emission and does not strictly overlap with `IDisposable` in .NET. |
   | Callable | Function-like | `__call__` | No, C#/.NET doesn't support (non-dynamic) callable objects. It could be done later if we emit CIL directly to dispatch this correctly. |
   | Descriptor | Meta-programming | `__get__`, `__set__`, `__delete__` | Never |
   | String Representation | Display | `__str__`, `__repr__` | Yes, natively through inheritance from the `Sharpy.Core.Object` base class, and also via overriding of the .NET native `ToString()` instance method. |
   | Hashing/Equality | Collection | `__hash__`, `__eq__` | Yes, natively through inheritance from the `Sharpy.Core.Object` base class, and also via overriding of the .NET native `GetHashCode()` and `Equals()` instance methods (and related static operators). |
   | Numeric Conversion | Type Conversion | `__int__`, `__float__`, `__complex__`, `__bool__` | `__bool__` for v0.5, implemented as static conversion operator. The others for later. |
   | Rich Comparison | Comparison | `__lt__`, `__le__`, `__gt__`, `__ge__`, `__eq__`, `__ne__` | Yes, via Sharpy comparison interfaces, and for `__eq__` and `__ne__` through `Sharpy.Core.Object` and static operator synthesis. |
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

This section provides a concrete, checkable task list for completing the refactoring work within v0.5 scope.

### Phase 1: Primitive Catalog (Priority: High)

**Goal**: Replace scattered primitive type checks with an exhaustive, tested registry.

**Files to create/modify**:
- `src/Sharpy.Compiler/Semantic/PrimitiveCatalog.cs` (new)
- `src/Sharpy.Compiler.Tests/Semantic/PrimitiveCatalogTests.cs` (new)
- `src/Sharpy.Compiler/Semantic/OperatorValidator.cs` (refactor)
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs` (refactor)
- `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs` (refactor)

**Tasks**:
- [ ] **1.1** Define `PrimitiveCatalog` class with:
  - [ ] `PrimitiveInfo` record: `(string SharpyName, Type ClrType, NumericKind Kind, int Size, bool IsSigned, bool IsNullable)`
  - [ ] `NumericKind` enum: `Integer`, `FloatingPoint`, `Decimal`, `Boolean`, `Character`, `None`
  - [ ] Static dictionary of all supported primitives
- [ ] **1.2** Add exhaustive primitive entries:
  - [ ] Integer types: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong` (and Sharpy aliases)
  - [ ] Floating types: `float`, `double`, `decimal`
  - [ ] Boolean: `bool`
  - [ ] Character: `char`
  - [ ] String: `str` / `string`
  - [ ] None/void: `None`
- [ ] **1.3** Implement numeric promotion rules:
  - [ ] `GetPromotedType(PrimitiveInfo left, PrimitiveInfo right) -> PrimitiveInfo`
  - [ ] Encode standard .NET numeric widening rules
  - [ ] Handle mixed integer/float promotions
- [ ] **1.4** Implement conversion checking:
  - [ ] `CanImplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to) -> bool`
  - [ ] `CanExplicitlyConvert(PrimitiveInfo from, PrimitiveInfo to) -> bool`
- [ ] **1.5** Add query methods:
  - [ ] `IsNumeric(SemanticType type) -> bool`
  - [ ] `IsInteger(SemanticType type) -> bool`
  - [ ] `IsFloatingPoint(SemanticType type) -> bool`
  - [ ] `GetPrimitiveInfo(string name) -> PrimitiveInfo?`
  - [ ] `GetPrimitiveInfo(Type clrType) -> PrimitiveInfo?`
- [ ] **1.6** Write comprehensive tests:
  - [ ] Test all primitive registrations are present
  - [ ] Test promotion rules match .NET spec
  - [ ] Test conversion rules match expected behavior
  - [ ] Test edge cases (max int sizes, float precision)
- [ ] **1.7** Refactor `OperatorValidator`:
  - [ ] Replace `IsNumericType()` with `PrimitiveCatalog.IsNumeric()`
  - [ ] Replace `IsIntegerType()` with `PrimitiveCatalog.IsInteger()`
  - [ ] Replace `InferNumericResultType()` with `PrimitiveCatalog.GetPromotedType()`
- [ ] **1.8** Refactor `TypeChecker`:
  - [ ] Replace `IsNumericType()` with `PrimitiveCatalog.IsNumeric()`
  - [ ] Remove redundant primitive checks
- [ ] **1.9** Consolidate `BuiltinRegistry` type registration:
  - [ ] Use `PrimitiveCatalog` as source of truth for primitive types
  - [ ] Remove duplicate type definitions

### Phase 2: Protocol Registry (Priority: High)

**Goal**: Create a centralized registry mapping Python dunders to Sharpy.Core interfaces.

**Files to create/modify**:
- `src/Sharpy.Compiler/Semantic/ProtocolRegistry.cs` (new)
- `src/Sharpy.Compiler.Tests/Semantic/ProtocolRegistryTests.cs` (new)

**Tasks**:
- [ ] **2.1** Define `ProtocolRegistry` class with:
  - [ ] `ProtocolInfo` record: `(string DunderName, string SharpyInterface, string DotNetInterface?, string ClrMethodName, ProtocolKind Kind)`
  - [ ] `ProtocolKind` enum: `Container`, `Iterator`, `Conversion`, `Representation`, `Comparison`, `Arithmetic`, `Bitwise`
- [ ] **2.2** Register v0.5 protocols:
  - [ ] Container: `__len__` → `ISized.__Len__()` → `Count`
  - [ ] Container: `__getitem__` → `ISequence<T>.__GetItem__()` → indexer
  - [ ] Container: `__setitem__` → `IMutableSequence<T>.__SetItem__()` → indexer
  - [ ] Container: `__contains__` → `IContains<T>.__Contains__()` → `Contains()`
  - [ ] Iterator: `__iter__` → `IIterable<T>.__Iter__()` → `GetEnumerator()`
  - [ ] Iterator: `__next__` → `Iterator<T>.__Next__()` → (via enumerator pattern)
  - [ ] Representation: `__str__` → `IStrConvertible.__Str__()` → `ToString()`
  - [ ] Representation: `__repr__` → `IRepresentable.__Repr__()` → `ToString()` (debug)
  - [ ] Hashing: `__hash__` → `IHashable.__Hash__()` → `GetHashCode()`
  - [ ] Conversion: `__bool__` → `IBoolConvertible.__Bool__()` → explicit `operator bool`
- [ ] **2.3** Add lookup methods:
  - [ ] `GetProtocolForDunder(string dunderName) -> ProtocolInfo?`
  - [ ] `GetDunderForInterface(string interfaceName) -> string?`
  - [ ] `IsProtocolDunder(string methodName) -> bool`
  - [ ] `GetExpectedSignature(string dunderName) -> (int paramCount, string? returnType)`
- [ ] **2.4** Integrate with existing interfaces:
  - [ ] Map each entry to actual `Sharpy.Core` interface types
  - [ ] Verify all interfaces exist in `Sharpy.Core` (add stubs if missing)
- [ ] **2.5** Write comprehensive tests:
  - [ ] Test all v0.5 protocols are registered
  - [ ] Test dunder → interface lookups
  - [ ] Test interface → dunder reverse lookups
  - [ ] Test signature expectations

### Phase 3: Protocol Signature Validator (Priority: High)

**Goal**: Validate non-operator dunder signatures like `OperatorSignatureValidator` does for operators.

**Files to create/modify**:
- `src/Sharpy.Compiler/Semantic/ProtocolSignatureValidator.cs` (new)
- `src/Sharpy.Compiler.Tests/Semantic/ProtocolSignatureValidatorTests.cs` (new)
- `src/Sharpy.Compiler/Semantic/NameResolver.cs` (integrate)
- `src/Sharpy.Compiler/Semantic/Symbol.cs` (extend `TypeSymbol`)

**Tasks**:
- [ ] **3.1** Create `ProtocolSignatureValidator` class:
  - [ ] Use `ProtocolRegistry` for protocol metadata
  - [ ] Static method: `ValidateDunderSignature(FunctionDef func, TypeSymbol owningType) -> List<SemanticError>`
  - [ ] Static method: `IsProtocolDunder(string methodName) -> bool`
- [ ] **3.2** Implement validation rules per protocol:
  - [ ] `__len__`: 1 param (`self`), returns `int`
  - [ ] `__getitem__`: 2 params (`self`, index), returns element type
  - [ ] `__setitem__`: 3 params (`self`, index, value), returns `None`
  - [ ] `__contains__`: 2 params (`self`, item), returns `bool`
  - [ ] `__iter__`: 1 param (`self`), returns iterator type
  - [ ] `__next__`: 1 param (`self`), returns element type (or raises `StopIteration`)
  - [ ] `__str__`: 1 param (`self`), returns `str`
  - [ ] `__repr__`: 1 param (`self`), returns `str`
  - [ ] `__hash__`: 1 param (`self`), returns `int`
  - [ ] `__bool__`: 1 param (`self`), returns `bool`
  - [ ] `__init__`: any params, returns `None` (existing validation in `TypeChecker`)
- [ ] **3.3** Add descriptive error messages:
  - [ ] Include expected vs actual parameter count
  - [ ] Include expected vs actual return type
  - [ ] Suggest the corresponding Sharpy.Core interface
- [ ] **3.4** Extend `TypeSymbol`:
  - [ ] Add `ProtocolMethods: Dictionary<string, FunctionSymbol>` (parallel to `OperatorMethods`)
- [ ] **3.5** Integrate into `NameResolver`:
  - [ ] In `ResolveMethodDeclaration`, check if method is a protocol dunder
  - [ ] Call `ProtocolSignatureValidator.ValidateDunderSignature()`
  - [ ] On success, add to `TypeSymbol.ProtocolMethods`
  - [ ] On failure, add errors to `_errors`
- [ ] **3.6** Write comprehensive tests:
  - [ ] Test valid signatures for each protocol dunder
  - [ ] Test invalid parameter counts
  - [ ] Test invalid return types
  - [ ] Test error message quality

### Phase 4: Protocol Validator (TypeChecker Integration) (Priority: Medium)

**Goal**: Validate protocol usage at type-checking time (e.g., `len(x)` requires `x` to have `__len__`).

**Files to create/modify**:
- `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs` (new)
- `src/Sharpy.Compiler.Tests/Semantic/ProtocolValidatorTests.cs` (new)
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs` (integrate)

**Tasks**:
- [ ] **4.1** Create `ProtocolValidator` class:
  - [ ] Constructor: `(SymbolTable symbolTable, ICompilerLogger logger)`
  - [ ] Cache structure similar to `OperatorValidator._clrOperatorCache`
- [ ] **4.2** Implement protocol conformance checking:
  - [ ] `HasProtocol(SemanticType type, string dunderName) -> bool`
  - [ ] `ValidateIndexAccess(SemanticType containerType, SemanticType indexType, int line, int col) -> SemanticType` (return type)
  - [ ] `ValidateMembership(SemanticType containerType, SemanticType itemType, int line, int col) -> SemanticType` (bool)
  - [ ] `ValidateIteration(SemanticType iterableType, int line, int col) -> SemanticType` (element type)
  - [ ] `ValidateLen(SemanticType containerType, int line, int col) -> SemanticType` (int)
  - [ ] `ValidateBoolConversion(SemanticType type, int line, int col) -> SemanticType` (bool)
- [ ] **4.3** Implement CLR protocol discovery (reflection-based):
  - [ ] Check if CLR type implements `IEnumerable<T>`, `ICollection<T>`, etc.
  - [ ] Map .NET interfaces to Sharpy protocol capabilities
  - [ ] Cache results per CLR type
- [ ] **4.4** Integrate into `TypeChecker`:
  - [ ] Replace hard-coded `__len__` check in `len()` builtin call handling
  - [ ] Replace hard-coded `__contains__` check in `in` operator handling
  - [ ] Replace hard-coded `__iter__` check in `for` loop handling
  - [ ] Replace hard-coded `__getitem__`/`__setitem__` in indexing expressions
- [ ] **4.5** Handle error cases:
  - [ ] "Type 'X' does not support indexing (missing `__getitem__`)"
  - [ ] "Type 'X' is not iterable (missing `__iter__`)"
  - [ ] "Cannot use 'in' operator with type 'X' (missing `__contains__`)"
- [ ] **4.6** Write comprehensive tests:
  - [ ] Test Sharpy types with protocol methods
  - [ ] Test CLR types that implement appropriate interfaces
  - [ ] Test error messages for missing protocols

### Phase 5: RoslynEmitter Consolidation (Priority: Medium)

**Goal**: Replace hard-coded dunder mappings in codegen with registry lookups.

**Files to modify**:
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.cs`
- `src/Sharpy.Compiler/CodeGen/NameMangler.cs`

**Tasks**:
- [ ] **5.1** Replace hard-coded dunder detection:
  - [ ] Use `ProtocolRegistry.IsProtocolDunder()` instead of string literals
  - [ ] Use `OperatorSignatureValidator.IsOperatorDunder()` for operator dunders
- [ ] **5.2** Replace hard-coded `IsMethodNonOperatorDunder()`:
  - [ ] Current: switch statement with `"__str__"`, `"__repr__"`, `"__hash__"`, etc.
  - [ ] New: `ProtocolRegistry.IsProtocolDunder(name) || name == "__init__"`
- [ ] **5.3** Replace hard-coded `GetOperatorMethodName()`:
  - [ ] Use `ProtocolRegistry.GetProtocolForDunder()` to get CLR method name
  - [ ] Fall back to `OperatorValidator` mappings for operators
- [ ] **5.4** Consolidate `NameMangler.DunderMappings`:
  - [ ] Merge with `ProtocolRegistry` data
  - [ ] Or have `NameMangler` consult `ProtocolRegistry`
- [ ] **5.5** Write tests verifying codegen uses registry:
  - [ ] Test that adding new protocol to registry enables codegen without code changes

### Phase 6: Type Mapper Consolidation (Priority: Low)

**Goal**: Eliminate duplicate type mapping logic between `CodeGen/TypeMapper` and `Discovery/TypeMapper`.

**Files to modify**:
- `src/Sharpy.Compiler/CodeGen/TypeMapper.cs`
- `src/Sharpy.Compiler/Discovery/TypeMapper.cs`
- `src/Sharpy.Compiler/Semantic/BuiltinRegistry.cs`

**Tasks**:
- [ ] **6.1** Audit both `TypeMapper` classes:
  - [ ] Document what each does
  - [ ] Identify overlap and differences
- [ ] **6.2** Decide consolidation strategy:
  - [ ] Option A: Merge into single `TypeMapper` in shared location
  - [ ] Option B: Keep separate but share underlying data via `PrimitiveCatalog`
  - [ ] Option C: Have codegen's `TypeMapper` delegate to discovery's
- [ ] **6.3** Implement chosen strategy
- [ ] **6.4** Ensure all type lookups go through centralized source:
  - [ ] Sharpy name → CLR type
  - [ ] CLR type → Sharpy name
  - [ ] Sharpy name → C# syntax
- [ ] **6.5** Use `PrimitiveCatalog` for primitive type mappings
- [ ] **6.6** Write tests for consolidated type mapping

### Phase 7: CLR Member Cache Extraction (Priority: Low)

**Goal**: Extract CLR reflection caching from `OperatorValidator` into a reusable service.

**Files to create/modify**:
- `src/Sharpy.Compiler/Semantic/ClrMemberCache.cs` (new, extracted from `OperatorValidator`)
- `src/Sharpy.Compiler/Semantic/OperatorValidator.cs` (refactor to use cache)
- `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs` (use cache)

**Tasks**:
- [ ] **7.1** Extract `ClrMemberCache` class:
  - [ ] `GetOperatorMethods(Type clrType) -> Dictionary<string, MethodInfo>`
  - [ ] `GetInterfaceImplementations(Type clrType) -> List<Type>`
  - [ ] `HasIndexer(Type clrType) -> bool`
  - [ ] `GetEnumeratorElementType(Type clrType) -> Type?`
- [ ] **7.2** Implement caching:
  - [ ] Per-type caching for all member kinds
  - [ ] Lazy population on first access
  - [ ] Thread-safe if needed for future parallel compilation
- [ ] **7.3** Refactor `OperatorValidator`:
  - [ ] Replace `_clrOperatorCache` with `ClrMemberCache.GetOperatorMethods()`
- [ ] **7.4** Use in `ProtocolValidator`:
  - [ ] For checking .NET interface implementations
  - [ ] For indexer and enumerator discovery
- [ ] **7.5** Write tests:
  - [ ] Test caching behavior (same result on repeated calls)
  - [ ] Test various CLR types (primitives, collections, custom)

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

## Appendix: Existing Hard-Coded Locations

This appendix documents specific locations of hard-coded semantics identified during analysis, for reference during implementation.

### `OperatorValidator.cs` (Lines 808-843)
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
```

### `RoslynEmitter.cs` (Lines 2310-2363)
```csharp
// Hard-coded dunder detection - refactor to use ProtocolRegistry
"__str__" => true,     // ToString() override
"__repr__" => true,    // ToString() override
"__hash__" => true,    // GetHashCode() override
"__len__" => true,     // Length property/method
"__contains__" => true, // Contains() method
"__getitem__" => true, // Indexer get
"__setitem__" => true, // Indexer set
"__iter__" => true,    // GetEnumerator()
```

### `TypeChecker.cs` (Lines 168-175)
```csharp
// Hard-coded __init__ handling - should use ProtocolRegistry
if (functionDef.Name == "__init__")
{
    // Validate that __init__ has no return type or -> None
    if (returnType != SemanticType.None && returnType != SemanticType.Unknown)
    {
        AddError($"Constructor '__init__' cannot have return type ...");
    }
}
```

### `BuiltinRegistry.cs` (Lines 24-40)
```csharp
// Hard-coded type registration - should use PrimitiveCatalog
RegisterType("int", typeof(int), TypeKind.Struct);
RegisterType("long", typeof(long), TypeKind.Struct);
RegisterType("float", typeof(float), TypeKind.Struct);
// ...
```

### `CodeGen/TypeMapper.cs` (Lines 18-44)
```csharp
// Hard-coded type mappings - should use shared PrimitiveCatalog
private static readonly Dictionary<string, string> _builtinTypeMap = new()
{
    { "int", "int" },
    { "long", "long" },
    // ...
};
```
