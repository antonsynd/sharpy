# Reflection-Based Operator Validation — TODOs

## Phase 1: Symbol & Name Resolution

- [x] Extend `TypeSymbol` in `src/Sharpy.Compiler/Semantic/Symbol.cs` with `OperatorMethods: Dictionary<string, List<FunctionSymbol>>`.
- [x] Implement `OperatorSignatureValidator` in `src/Sharpy.Compiler/Semantic/OperatorSignatureValidator.cs`:
  - [x] Define dunder → logical operator role mapping (arith, bitwise, comparison, in-place, unary).
  - [x] Implement `ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType) -> List<SemanticError>`.
  - [x] Enforce parameter count rules for unary, binary, and in-place operators.
  - [x] Enforce return-type rules (bool for comparisons; non-void for all other operators in Phase 1, with stricter numeric/integral constraints to be handled in later phases via `SemanticType`-aware validation).
  - [x] Add precise, user-friendly error messages.
- [x] Update `NameResolver.ResolveMethodDeclaration` in `src/Sharpy.Compiler/Semantic/NameResolver.cs`:
  - [x] Detect operator dunders using a whitelist (arith, bitwise, in-place, comparison).
  - [x] Call `OperatorSignatureValidator.ValidateDunderSignature` for each candidate.
  - [x] On success, add method to `owningType.OperatorMethods[methodDef.Name]`.
  - [x] On failure, append `SemanticError`s to `_errors` with correct locations.
  - [x] Ensure non-operator dunders (`__init__`, `__str__`, `__repr__`, `__hash__`, etc.) are not added to `OperatorMethods`.

## Phase 2: OperatorValidator Core

- [x] Create `OperatorValidator` in `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`:
  - [x] Add fields for `_symbolTable`, `_logger`, `_binaryOpCache`, `_unaryOpCache`, `_clrOperatorCache`.
  - [x] Implement public methods:
    - [x] `SemanticType ValidateBinaryOp(BinaryOperator op, SemanticType left, SemanticType right, int line, int column)`.
    - [x] `SemanticType ValidateUnaryOp(UnaryOperator op, SemanticType operand, int line, int column)`.
- [x] Implement mapping helpers:
  - [x] `BinaryOperatorToDunder(BinaryOperator op)` including `Power → __pow__` and all supported operators.
  - [x] `UnaryOperatorToDunder(UnaryOperator op)` for unary dunders.
  - [x] Mapping from Sharpy operators to CLR operator method names (`op_Addition`, `op_Equality`, etc.).
- [x] Implement overload resolution:
  - [x] `ResolveBestOverload(List<FunctionSymbol> candidates, SemanticType rightType)` with most-specific match semantics.
  - [x] Handle ambiguity and report clear errors when multiple overloads are applicable.
- [x] Implement resolution strategy:
  - [x] For `UserDefinedType`, use `TypeSymbol.OperatorMethods` and `ResolveBestOverload`.
  - [x] For Sharpy builtins (e.g., `list[T]`, `dict[K,V]`), implement allowed operator rules (e.g., containment, equality, but no arithmetic).
  - [x] For CLR-backed types (`TypeSymbol.ClrType` / primitive Sharpy types), use `_clrOperatorCache` and reflection to find applicable CLR operators; validate parameter/return compatibility with `SemanticType`s.

## Phase 3: Errors & Special Cases

- [ ] Implement descriptive error messages in `OperatorValidator`:
  - [x] Missing operator on a type (basic message implemented in `ResolveOperatorOverload`; consider enhancing with suggestions for dunder or CLR overloads later).
  - [ ] Ambiguous overloads (depends on richer operator overloading support in the symbol table).
- [x] Implement equality-complement behavior:
  - [x] If only `__eq__` or only `__ne__` exists, synthesize the complement logically for validation (matching `RoslynEmitter`).
- [ ] Implement augmented assignment support helpers:
  - [ ] Implement a dedicated helper in `OperatorValidator` (e.g. `ValidateAugmentedAssignment`) that:
    - [ ] Maps `AssignmentOperator` values to in-place dunder names (`__iadd__`, etc.) and corresponding base `BinaryOperator` values.
    - [ ] Prefers in-place dunder resolution on the target type; falls back to the base binary operator via `ValidateBinaryOp` when no in-place dunder is available.
    - [ ] Enforces that the resulting type is assignable to the target type and logs clear errors when it is not.
    - [ ] Produces descriptive errors when no suitable in-place or base operator (including CLR/builtin operators) can be found for the augmented operator.

## Phase 4: TypeChecker Integration

- [ ] Inject `OperatorValidator` into `TypeChecker` in `src/Sharpy.Compiler/Semantic/TypeChecker.cs`:
  - [ ] Add `_operatorValidator` field.
  - [ ] Initialize `_operatorValidator` in the constructor using `_symbolTable` and `_logger`.
- [ ] Replace direct operator logic:
  - [ ] Rewrite `CheckBinaryOp(BinaryOp binOp)` to delegate to `_operatorValidator.ValidateBinaryOp` for all arithmetic, bitwise, comparison, and other non-trivial operators.
  - [ ] Rewrite `CheckUnaryOp(UnaryOp unOp)` to delegate to `_operatorValidator.ValidateUnaryOp`.
  - [ ] Remove old helper methods: `InferArithmeticType`, `InferAdditionType`, `ValidateBitwiseOp`, `ValidateComparisonOp`, `ValidateUnaryArithmeticOp`, `ValidateUnaryBitwiseOp`, and related unused helpers.
- [ ] Integrate comparison chains:
  - [ ] Update `CheckComparisonChain(ComparisonChain chain)` to loop over `(lhs, op, rhs)` pairs, calling `_operatorValidator.ValidateBinaryOp` for each.
  - [ ] Ensure each comparison returns `bool` (or `Unknown` when error already reported) and that the chain expression as a whole remains `SemanticType.Bool`.
- [ ] Integrate augmented assignment semantics:
  - [ ] Update `CheckAssignment(Assignment assignment)` to:
    - [ ] Keep existing logic for simple `=` and tuple unpacking.
    - [ ] For augmented operators (e.g., `+=`, `-=`, `*=`, `/=`, `//=`, `%=` and bitwise/shift/power variants):
      - [ ] Compute `targetType` and `valueType` via `CheckExpression`.
      - [ ] Use `OperatorValidator` (or a helper) to resolve in-place vs base operator and resulting type.
      - [ ] Validate result type assignability to `targetType`; report clear errors when not assignable or operator is missing.

## Phase 5: Tests

- [x] Add `OperatorSignatureValidatorTests` in `src/Sharpy.Compiler.Tests/Semantic/OperatorSignatureValidatorTests.cs`:
  - [x] Test valid/invalid parameter counts for unary, binary, and in-place dunders.
  - [x] Test valid/invalid return types for comparisons, arithmetic, and `__invert__`.
  - [x] Verify `__pow__` and in-place variants are correctly validated.
- [x] Add `OperatorValidatorTests` in `src/Sharpy.Compiler.Tests/Semantic/OperatorValidatorTests.cs`:
  - [x] Pure Sharpy types:
    - [x] User-defined type with `__add__`, `__iadd__`, and comparison dunders; test normal ops, augmented ops, and overload resolution.
    - [x] Ambiguous overload scenarios.
  - [x] CLR interop:
    - [x] Types mapped to CLR types with `op_Addition`, `op_Equality`, etc.; verify reflection-based resolution and caching.
  - [x] Comparison chains:
    - [x] Valid chains, invalid chains, and mixed operators (`<`, `>=`, `==`, etc.).
  - [x] Power operator:
    - [x] `**` via `__pow__` and via CLR numeric semantics.
  - [x] Augmented assignments:
    - [x] Only `__iadd__` defined.
    - [x] Only `__add__` defined.
    - [x] Both defined.
    - [x] Neither defined (expect error).
- [x] Run existing tests in `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs` and ensure they all still pass (especially arithmetic, power, bitwise, comparisons, membership/identity, and type narrowing cases).
