## Plan: Reflection-Based Operator Validation with Dunder Method Support (Sharpy as .NET-First, Pythonic Syntax)

Sharpy uses Pythonic syntax and dunder methods, but targets .NET and synthesizes C# operators and overrides (`op_Addition`, `Equals`, `GetHashCode`, etc.) in `RoslynEmitter.TryGenerateOperatorOverload` and related code. Semantic analysis should therefore:

- Treat user-defined Sharpy dunder methods as **first-class .NET operator declarations** for type checking.
- Respect .NET operator and comparison semantics where that makes sense (numeric / boolean rules, CLR operator overloads), while preserving Pythonic syntax and constructs (e.g., `in`, `is`, comprehensions).

The goal is to replace ad‑hoc, “Python-only” operator rules in the type checker with a unified validator that understands:

- Dunder operators on Sharpy types (`__add__`, `__iadd__`, `__eq__`, `__pow__`, …).
- CLR operators on interop types (`op_Addition`, `op_Equality`, etc.).
- Sharpy built-in numeric, boolean, and collection semantics, aligned with .NET where appropriate.

### Steps

1. **Extend `TypeSymbol` with operator metadata**

   - In Symbol.cs, add:
     - `Dictionary<string, List<FunctionSymbol>> OperatorMethods { get; init; } = new();`
   - This cache is **semantic**, not syntax-level: it only contains dunder methods that:
     - Are recognized as operator-like (from a whitelist, e.g. `__add__`, `__sub__`, `__iadd__`, `__eq__`, `__lt__`, `__pow__`, etc.).
     - Pass signature validation (see step 2).
   - It will be used by semantic analysis to resolve operators on `UserDefinedType` instances, mirroring what `RoslynEmitter` does when generating C# operators.

2. **Create `OperatorSignatureValidator` (dunder signatures, .NET-aware)**

   - New file: `src/Sharpy.Compiler/Semantic/OperatorSignatureValidator.cs`.
   - Responsibilities:
     - Maintain a **Sharpy dunder → logical operator role** mapping that matches both:
       - The operator tokens in the parser (`+`, `-`, `**`, `==`, `!=`, `<`, `>`, `&`, `|`, `~`, etc.).
       - The corresponding .NET operator semantics used in codegen (`op_Addition`, `op_Equality`, …).
     - Implement `ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType)` returning `List<SemanticError>`:
       - **Parameter rules**:
         - Instance binary operators (`__add__`, `__sub__`, `__mul__`, `__pow__`, comparisons, etc.): `self` + one other parameter (2 params total).
         - In-place operators (`__iadd__`, `__isub__`, …): same parameter shape as binary operators.
         - Unary operators (`__pos__`, `__neg__`, `__invert__`, logical not if supported): just `self`.
         - Consider static-operator patterns only if we decide to support static methods for operators; otherwise, enforce instance-based signatures consistent with how the emitter uses them.
       - **Return-type rules (aligned with .NET, but expressed in Sharpy types)**:
         - Comparisons (`__eq__`, `__ne__`, `__lt__`, `__gt__`, etc.) must return `bool` (Sharpy `SemanticType.Bool` → `System.Boolean`).
         - Phase 1: all other operator methods are required only to return a non-`None` (non-void) type so they can be used in expressions.
         - Later phases: we may tighten this further using `SemanticType` information (e.g., requiring numeric-like results for arithmetic, integral results for `__invert__`, or results assignable to the operand type for unary numeric operators).
       - Produce targeted error messages, e.g.:
         - `"Operator method '__add__' on 'Vector' must have signature '(self, other: VectorLike) -> VectorLike', got '(self, x, y)'."`
   - This validator is intentionally **Sharpy/.NET aware**: signatures are checked in terms of Sharpy types but with .NET operator expectations in mind.

3. **Populate operator cache in `NameResolver` (dunder whitelist + validation)**

   - In `NameResolver.ResolveMethodDeclaration` (after `owningType.Methods.Add(funcSymbol)`):
     - Detect candidate operator methods using a **whitelist of dunder names** rather than any `__*__`:
       - Arithmetic: `__add__`, `__sub__`, `__mul__`, `__truediv__`, `__floordiv__`, `__mod__`, `__pow__`, etc.
       - Bitwise: `__and__`, `__or__`, `__xor__`, `__lshift__`, `__rshift__`, `__invert__`.
       - In-place: `__iadd__`, `__isub__`, `__imul__`, `__idiv__`, `__ifloordiv__`, `__imod__`, `__ipow__`, `__iand__`, `__ior__`, `__ixor__`, `__ilshift__`, `__irshift__`.
       - Comparisons: `__eq__`, `__ne__`, `__lt__`, `__le__`, `__gt__`, `__ge__`.
       - (Potentially membership / containment dunders if/when they’re supported consistently in codegen.)
     - For each such method:
       - Call `OperatorSignatureValidator.ValidateDunderSignature(methodDef, owningType)`.
       - If no errors:
         - Add the method symbol to `owningType.OperatorMethods[methodDef.Name]` (initializing the list as necessary).
       - If errors:
         - Append them to `_errors` with `methodDef`’s location.
   - **Do not** classify non-operator dunders here (`__init__`, `__str__`, `__repr__`, `__hash__`, lifecycle hooks). Those remain regular methods and, when appropriate, are handled specially by `RoslynEmitter` as overrides.

4. **Create `OperatorValidator` (per-compilation, Sharpy+CLR resolution)**

   - New file: `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`.
   - Fields:
     - `SymbolTable _symbolTable;`
     - `ICompilerLogger _logger;`
     - `Dictionary<(SemanticType, BinaryOperator, SemanticType), SemanticType?> _binaryOpCache;`
     - `Dictionary<(UnaryOperator, SemanticType), SemanticType?> _unaryOpCache;`
     - `Dictionary<Type, Dictionary<string, MethodInfo>> _clrOperatorCache;` – keyed by CLR `Type` (from `TypeSymbol.ClrType` or known builtin mappings) and operator method name (`op_Addition`, `op_Equality`, etc.).
   - Public API:
     - `SemanticType ValidateBinaryOp(BinaryOperator op, SemanticType left, SemanticType right, int line, int column);`
     - `SemanticType ValidateUnaryOp(UnaryOperator op, SemanticType operand, int line, int column);`
   - Current status:
     - An initial `OperatorValidator` implementation and comprehensive tests are in place. It resolves Sharpy dunder operators, builtin numeric/collection semantics, and CLR operators via reflection. Remaining work focuses on equality complement synthesis, more advanced overload resolution once method overloading is supported in the symbol table, augmented assignment helpers, and full `TypeChecker` integration.
   - Semantics:
     - **Sharpy-first, .NET-aligned**:
       - For `UserDefinedType`:
         - Resolve via `TypeSymbol.OperatorMethods` (dunder cache).
       - For Sharpy builtin numeric/boolean/collection types:
         - Apply well-defined rules that mirror .NET numeric promotions and boolean logic, while honoring Sharpy syntax (`//`, `**`, `in`, `is`).
       - For interop types and any `UserDefinedType` backed by a CLR `Type` (`TypeSymbol.ClrType`):
         - Use reflection and `_clrOperatorCache` to discover and cache CLR operators.

5. **Operator-to-dunder and CLR mapping + overload resolution**

   - In `OperatorValidator`:
     - Implement `string? BinaryOperatorToDunder(BinaryOperator op)` and `string? UnaryOperatorToDunder(UnaryOperator op)`:
       - Include mappings like:
         - `BinaryOperator.Add → "__add__"`, `BinaryOperator.Subtract → "__sub__"`, `BinaryOperator.Power → "__pow__"`, etc.
         - For comparisons: `__eq__`, `__ne__`, `__lt__`, `__gt__`, `__le__`, `__ge__`.
       - For augmented assignments (see step 8) you’ll also need the in-place names: `AssignmentOperator.PlusAssign → "__iadd__"` etc.
     - Implement `ResolveBestOverload(List<FunctionSymbol> candidates, SemanticType rightType)`:
       - Sharpy semantics: allow **most specific match** where:
         - Exact type match wins over base-type matches.
         - If multiple incomparable candidates are applicable, report ambiguity.
       - This should align with how codegen will look up dunder methods when synthesizing C# operators.
     - Resolution strategy:
       1. **User-defined Sharpy types (`UserDefinedType`)**:
          - Look up `userType.Symbol.OperatorMethods[dunderName]` (if any).
          - Use `ResolveBestOverload` to pick the winning overload and return its return type.
       2. **Sharpy builtins without CLR backing (e.g. `list[int]`, `dict[K,V]`)**:
          - Apply Sharpy’s own operator rules where defined (e.g., containment rules for `in`, indexing, equality semantics).
          - For arithmetic, only allow what Sharpy intends to support on those types (typically none).
       3. **CLR-backed types (`TypeSymbol.ClrType` or primitive Sharpy types mapped to CLR primitives)**:
          - Map Sharpy `BinaryOperator`/`UnaryOperator` to CLR operator method names (`op_Addition`, `op_Subtraction`, `op_Equality`, `op_LogicalNot`, etc.).
          - Use `_clrOperatorCache` to discover applicable `MethodInfo`s once per `Type`.
          - Enforce that the resolved CLR operator’s parameter and return types are compatible with the Sharpy semantic types involved.

6. **Error messages and .NET-aligned special cases**

   - Still in `OperatorValidator`, implement Sharpy-friendly but .NET-aware diagnostics:
     - Missing operator:
       - `"Type 'Vector' does not support operator '+'. Implement '__add__(self, other: Vector) -> Vector' or provide a compatible .NET operator overload."`
     - Ambiguous overload:
       - `"Ambiguous overload for operator '+': multiple '__add__' methods on 'Vector' are applicable for argument type 'Vector3'."`
     - Equality complement (Sharpy dunder ↔ C# operator parity):
       - If `__eq__` is present but `__ne__` is not, or vice versa, synthetically treat the complement as existing for validation purposes (mirroring `RoslynEmitter`’s complementary generation of `==`/`!=` operators).
        - **Augmented assignments**:
          - Provide helpers dedicated to augmented assignment validation (e.g. `ValidateAugmentedAssignment`) that:
            - Map `AssignmentOperator` values to in-place dunder names (e.g. `__iadd__`) and their corresponding base `BinaryOperator` values (e.g. `Add`).
            - Prefer in-place dunder resolution on the target type when available.
            - Otherwise, fall back to the corresponding binary operator via `ValidateBinaryOp`.
            - Enforce that the resulting type is assignable to the target type; if not, log a clear error and return `SemanticType.Unknown`.
            - When neither an in-place operator nor a base operator is available (including CLR/builtin operators), log a descriptive error mentioning the augmented operator (e.g. "+=") and the operand types.

7. **Comparison operators and chains (Sharpy syntax, .NET-compatible semantics)**

   - In `TypeChecker`:
     - Update `CheckBinaryOp` for comparison operators to always use `_operatorValidator.ValidateBinaryOp`, not ad-hoc logic.
       - For simple comparisons (`a < b`), use the mapping from step 5; enforce that the final type is `bool`.
     - Update `CheckComparisonChain(ComparisonChain chain)`:
       - Iterate over `(lhs, op, rhs)` pairs using `chain.Operands` and `chain.Operators`.
       - For each comparison:
         - Call `_operatorValidator.ValidateBinaryOp` for the appropriate comparison operator.
         - Verify that the result is `bool` (or `Unknown` only when a prior error already reported).
       - Always return `SemanticType.Bool` for the overall chain expression, matching both Python syntax and .NET-style boolean result.

8. **Integrate `OperatorValidator` into `TypeChecker` (including assignments)**

   - In TypeChecker.cs:
     - Add a field: `private readonly OperatorValidator _operatorValidator;`
     - Initialize it in the constructor with `_symbolTable` and `_logger`.
     - Replace `CheckBinaryOp` implementation so that, for all “real” operators (arith, bitwise, comparisons, logical), it becomes:
       - `return _operatorValidator.ValidateBinaryOp(binOp.Operator, leftType, rightType, binOp.LineStart, binOp.ColumnStart);`
       - Keep any special handling for short-circuit logical ops if needed, but enforce that semantics are consistent with Sharpy’s design.
     - Replace `CheckUnaryOp` body with a call to `_operatorValidator.ValidateUnaryOp`, instead of baked-in numeric/bitwise checks.
     - Remove now-redundant helpers:
       - `InferArithmeticType`, `InferAdditionType`, `ValidateBitwiseOp`, `ValidateComparisonOp`, `ValidateUnaryArithmeticOp`, `ValidateUnaryBitwiseOp`, and any small helpers that now live inside `OperatorValidator`.
     - **Augmented assignment integration**:
       - Update `CheckAssignment(Assignment assignment)`:
         - For `AssignmentOperator.Assign`, keep existing logic (simple assignment and tuple unpacking).
         - For `AssignmentOperator` values corresponding to `+=`, `-=`, `*=`, `/=`, `//=`, `%=`; bitwise and shift assignments, and power assignment:
           - Compute `targetType = CheckExpression(assignment.Target);`
           - Compute `valueType = CheckExpression(assignment.Value);`
           - Delegate augmented operator validation to `_operatorValidator` (e.g. `_operatorValidator.ValidateAugmentedAssignment(...)`):
             - That helper is responsible for preferring in-place dunders, falling back to base binary operators, and enforcing result-type assignability to `targetType`.
           - Rely on `OperatorValidator` to log detailed diagnostics about missing operators or incompatible result types, keeping `TypeChecker` focused on structural assignment rules.

9. **Testing: Sharpy + .NET interop scenarios**

   - New tests:
     - `src/Sharpy.Compiler.Tests/Semantic/OperatorSignatureValidatorTests.cs`:
       - Validate signature rules for all supported dunders:
         - Parameter counts (binary/unary/in-place).
         - Return types for comparisons, unary numeric, and `__invert__`.
         - Inclusion of `__pow__` and in-place variants, ensuring they’re accepted/rejected correctly.
     - `src/Sharpy.Compiler.Tests/Semantic/OperatorValidatorTests.cs`:
       - Pure Sharpy types:
         - User-defined `Vector` with `__add__`, `__iadd__`, etc.; verify:
           - Basic arithmetic operations, augmented assignments, and comparison semantics.
           - Most-specific overload selection (`Vector` vs `VectorLike`).
           - Complement synthesis for `==`/`!=`.
       - CLR interop:
         - Types mapped to CLR types that define `op_Addition`, `op_Equality`, etc.:
           - Confirm reflection-based resolution picks the correct CLR operators, and caching is effective.
       - Comparison chains:
         - Valid and invalid chains, including `is`, `is not`, `in`, `not in` if/when wired into the validator.
       - Power operator:
         - `**` via `__pow__` and via CLR numeric semantics.
       - Augmented assignments:
         - Only `__iadd__` defined.
         - Only `__add__` defined.
         - Both.
         - Neither.
   - Ensure all existing tests in TypeCheckerTests.cs still pass, especially ones around:
     - arithmetic and power, bitwise operations, comparisons, boolean logic,
     - membership / identity expressions, and type narrowing, to avoid regressions.
