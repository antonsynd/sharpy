## Plan: Refactor Hard‑Coded Type/Operator Semantics

This plan focuses on systematically identifying all hard‑coded Sharpy/.NET/C# type and operator knowledge in `Sharpy.Compiler`, then replacing ad‑hoc logic with: (1) explicit, exhaustively tested registries (for closed sets like primitive numeric types), and (2) reflection/metadata‑driven discovery with caching (for framework/library types, methods, and operators). It leans on existing discovery and caching infrastructure (e.g. `Discovery/OverloadIndex*`, `CachedModuleDiscovery`, `BuiltinRegistry`, `TypeMapper`) and aims to centralize all "semantic knowledge" behind a few well‑defined services so the parser/semantic analyzer/codegen no longer scatter hard‑coded decisions.

This is an architectural refactor, not a behavior change project: the end state should preserve current user‑visible semantics (modulo clearly documented bug fixes), while making it significantly easier to add new Sharpy/Core features and .NET interop without touching many call sites.

## Status

**Note:** A significant portion of this refactor has been completed through the reflection-based operator validation work (see [reflection_based_operator_validation.md](reflection_based_operator_validation.md) and [reflection_based_operator_validation.tasks.md](reflection_based_operator_validation.tasks.md)). Specifically:

- Operator validation has been centralized in `OperatorValidator`
- Dunder method validation and caching infrastructure exists in `OperatorSignatureValidator` and `TypeSymbol.OperatorMethods`
- CLR operator discovery via reflection is implemented with caching (`_clrOperatorCache`)
- TypeChecker integration is complete for operators and augmented assignments

This plan has been updated to:

1. Remove/consolidate steps that are now complete
2. Focus on remaining hard‑coded semantics (primitives, collections, protocols, non-operator methods)
3. Align terminology and architecture with the existing operator infrastructure

### Steps

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

4. **TOOLING**: The operator mapping tables in `OperatorValidator` could be exposed for:
   - LSP autocomplete (suggest appropriate dunder when user types an operator)
   - Documentation generation (operator reference tables)
   - Consider extracting to a shared metadata format.

5. **PERFORMANCE**: Current caching strategy (per-compilation dictionaries in validators) is simple and effective; monitor whether this scales to large codebases or if cross-compilation caching (via `Discovery/Caching`) becomes necessary.

6. **BALANCE**: Decide the balance between exhaustively defined primitives vs "learned" framework types: fully enumerate primitives and core Sharpy types; rely on reflection + caching for everything else.

7. **SEMANTIC MODEL**: Consider emitting a machine‑readable "semantic model snapshot" (e.g. JSON of registries and caches) for introspection tests and tooling validation, ensuring future changes don't silently re‑introduce hard‑coded special cases.
