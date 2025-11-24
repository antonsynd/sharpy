## Plan: Refactor Hard‑Coded Type/Operator Semantics

This plan focuses on systematically identifying all hard‑coded Sharpy/.NET/C# type and operator knowledge in `Sharpy.Compiler`, then replacing ad‑hoc logic with: (1) explicit, exhaustively tested registries (for closed sets like primitive numeric types), and (2) reflection/metadata‑driven discovery with caching (for framework/library types, methods, and operators). It leans on existing discovery and caching infrastructure (e.g. `Discovery/OverloadIndex*`, `CachedModuleDiscovery`, `BuiltinRegistry`, `TypeMapper`) and aims to centralize all “semantic knowledge” behind a few well‑defined services so the parser/semantic analyzer/codegen no longer scatter hard‑coded decisions.

This is an architectural refactor, not a behavior change project: the end state should preserve current user‑visible semantics (modulo clearly documented bug fixes), while making it significantly easier to add new Sharpy/Core features and .NET interop without touching many call sites.

### Steps

1. **Inventory and classify hard‑coded semantics**
  - Scan `Semantic/` (`BuiltinRegistry`, `SemanticType`, `TypeResolver`, `TypeChecker`, `NameResolver`, `ModuleRegistry`, `AccessValidator`, `ControlFlowValidator`) and `CodeGen/TypeMapper` for explicit type/operator/method names and special‑case branches.
  - Do the same for `Discovery/TypeMapper`, `OverloadIndex*`, any reflection‑based helpers, and any direct `System.*` `Type`/`MethodInfo` references in other folders that encode semantic knowledge.
   - Classify every hard‑coded piece as: (A) closed finite set (e.g. integer types, comparison operators), (B) Sharpy language intrinsic (e.g. `len`, `print`, dunder protocol names), or (C) framework/library dependent (e.g. `System.Collections.Generic.Dictionary<,>` or LINQ support).

2. **Introduce centralized semantic description services**
  - Define a single “builtin description” service (e.g. `BuiltinTypeModel`/`IntrinsicRegistry`) that describes Sharpy primitives, containers, common dunder protocols, and their relationships to .NET types, instead of scattering this knowledge across `BuiltinRegistry`, `TypeMapper`, and `TypeChecker`.
   - Define an “operator/protocol model” that maps:
     - Sharpy operator tokens → semantic operation kinds → dunder names → resolution rules.
   - Ensure all other components (type checker, codegen, overload resolution) depend on these services instead of hard‑coding their own copies.

3. **Replace partial hard‑coded sets with exhaustive registries**
  - For .NET primitive and numeric types, define exhaustive tables in a dedicated module (e.g. `Semantic/PrimitiveCatalog`), owned by the semantic layer but independent of any particular `SemanticType` representation:
     - e.g. all integer types (`sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `nint`, `nuint`), floats (`float`, `double`, `decimal` if supported), `bool`, `char`, etc.
   - Encode: Sharpy surface name, underlying .NET `Type`, numeric kind (integer/float), size, signedness, and promotion/implicit conversion rules.
  - Add unit tests in `Sharpy.Compiler.Tests` (likely under a new `Semantic/PrimitiveCatalogTests` namespace) verifying:
     - The registry is exhaustive for supported primitives.
     - Conversions and promotions match a reference table.
   - Refactor any scattered primitive checks (e.g. “if type is int or long…”) to query the registry.

4. **Consolidate and formalize dunder/operator mapping**
  - Create a central operator registry (e.g. `Semantic/OperatorRegistry`) that:
     - Enumerates all Sharpy operators (arithmetic, comparison, bitwise, logical, containment, indexing, call, attribute access, unary, etc.).
     - Maps each to:
       - Primary dunder name(s) (`__add__`, `__radd__`, `__eq__`, etc.),
       - Commutativity/associativity flags,
       - Expected operand and result categories (numeric, bool, sequence, mapping, iterable).
  - Refactor type checking and overload resolution to:
     - Ask the registry for candidate methods and operation semantics instead of hard‑coding dunder name strings.
   - Add tests to ensure the mapping covers all defined operators in the language grammar and is internally consistent.

5. **Unify and extend reflection‑based type/method discovery**
  - In `Discovery/` (e.g. `TypeMapper`, `OverloadIndexBuilder`, `OverloadIndex`), define a single abstraction (e.g. `IRuntimeTypeMetadataProvider`) for “runtime type metadata provider” responsible for:
     - Mapping `SemanticType` / Sharpy symbol → underlying .NET `Type` or `MethodInfo`/`PropertyInfo`.
     - Scanning assemblies (Sharpy.Core, user assemblies) for overloads, extension methods, and usable operators.
   - Use reflection to discover:
     - Available methods/operators on .NET backing types (including dunder‑like protocols Sharpy cares about).
     - Generic arities, constraints, and attributes relevant to the type system.
   - Design this metadata provider to be the one place where any new framework types or patterns are added, eliminating scattered “if type.FullName == …” checks.

6. **Strengthen and reuse caching infrastructure**
  - Extend `Discovery/Caching` (`AssemblyIdentity`, `OverloadIndex`, `OverloadIndexCache`) to:
     - Cache not only overload sets, but also:
       - Type metadata for Sharpy/Core and user assemblies (e.g. resolved `Type` objects, method/field signatures).
       - Builtin/primitive mapping tables (precomputed reflection results for primitives and Sharpy.Core types).
   - Ensure caches are keyed by:
     - Assembly identity (name, version, culture, public key token).
     - Compiler configuration (target framework, language version if relevant).
   - Make operator and method resolution always go through cached indexes rather than performing new reflection.

7. **Refactor type analysis and checking to use the new services**
  - In `TypeResolver`, `TypeChecker`, `SemanticType`, and `BuiltinRegistry`:
     - Replace direct knowledge of .NET types (string literals like `"System.Int32"`, special‑case `if` trees) with calls into:
       - Primitive catalog for numeric/primitive classification and conversions.
       - Operator registry for operator semantics and dunder mapping.
       - Metadata provider/overload index for actual method/constructor/operator resolution.
   - For class/interface inheritance and protocols:
     - Define a single “protocol/intf model” describing structural vs nominal requirements and the mapping to .NET interfaces/attributes.
     - Update `ControlFlowValidator` and `AccessValidator` to use protocol information instead of custom checks.

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
   - Execute this plan in small, vertical slices (e.g. “numeric primitives only”, “equality operators only”), each with its own tests and clear rollback path, instead of a single large branch.
   - After each major slice, run all existing tests in `Sharpy.Compiler.Tests` and a representative subset of `Sharpy.Core.Tests` / integration snippets to confirm behavior parity.
   - For any intentional behavior changes (e.g. fixing long‑standing bugs or aligning with Python/.NET semantics), capture them explicitly in `docs/status/` and update or add tests that encode the new contract.

### Further Considerations

1. Decide the balance between exhaustively defined primitives vs “learned” framework types: fully enumerate primitives and core Sharpy types; rely on reflection + caching for everything else.
2. Clarify how much Python‑compatibility vs .NET idioms Sharpy should model in its operator/protocol set, as this shapes the operator/dunder registry and which dunder names are considered “first‑class”.
3. Consider emitting a machine‑readable “semantic model snapshot” (e.g. JSON of registries and caches) for introspection tests and tooling validation, ensuring future changes don’t silently re‑introduce hard‑coded special cases.
4. Decide early whether the new registries live purely in the semantic layer or are also consumable by tooling (LSP, future analyzers); this affects how much they need to be versioned and kept backwards‑compatible.
