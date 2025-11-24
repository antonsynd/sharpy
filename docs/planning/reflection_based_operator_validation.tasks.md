# Reflection-Based Operator Validation — TODOs

## Phase 1: Symbol & Name Resolution

- [x] Extend `TypeSymbol` in `src/Sharpy.Compiler/Semantic/Symbol.cs` with `OperatorMethods: Dictionary<string, List<FunctionSymbol>>`.
- [ ] Implement `OperatorSignatureValidator` in `src/Sharpy.Compiler/Semantic/OperatorSignatureValidator.cs`:
  - [ ] Define dunder → logical operator role mapping (arith, bitwise, comparison, in-place, unary).
  - [ ] Implement `ValidateDunderSignature(FunctionDef funcDef, TypeSymbol owningType) -> List<SemanticError>`.
  - [ ] Enforce parameter count rules for unary, binary, and in-place operators.
  - [ ] Enforce return-type rules (bool for comparisons, numeric for arithmetic, integral for `__invert__`, non-void for others).
  - [ ] Add precise, user-friendly error messages.
- [ ] Update `NameResolver.ResolveMethodDeclaration` in `src/Sharpy.Compiler/Semantic/NameResolver.cs`:
  - [ ] Detect operator dunders using a whitelist (arith, bitwise, in-place, comparison).
  - [ ] Call `OperatorSignatureValidator.ValidateDunderSignature` for each candidate.
  - [ ] On success, add method to `owningType.OperatorMethods[methodDef.Name]`.
  - [ ] On failure, append `SemanticError`s to `_errors` with correct locations.
  - [ ] Ensure non-operator dunders (`__init__`, `__str__`, `__repr__`, `__hash__`, etc.) are not added to `OperatorMethods`.

## Phase 2: OperatorValidator Core

- [ ] Create `OperatorValidator` in `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`:
  - [ ] Add fields for `_symbolTable`, `_logger`, `_binaryOpCache`, `_unaryOpCache`, `_clrOperatorCache`.
  - [ ] Implement public methods:
    - [ ] `SemanticType ValidateBinaryOp(BinaryOperator op, SemanticType left, SemanticType right, int line, int column)`.
    - [ ] `SemanticType ValidateUnaryOp(UnaryOperator op, SemanticType operand, int line, int column)`.
- [ ] Implement mapping helpers:
  - [ ] `BinaryOperatorToDunder(BinaryOperator op)` including `Power → __pow__` and all supported operators.
  - [ ] `UnaryOperatorToDunder(UnaryOperator op)` for unary dunders.
  - [ ] Mapping from Sharpy operators to CLR operator method names (`op_Addition`, `op_Equality`, etc.).
- [ ] Implement overload resolution:
  - [ ] `ResolveBestOverload(List<FunctionSymbol> candidates, SemanticType rightType)` with most-specific match semantics.
  - [ ] Handle ambiguity and report clear errors when multiple overloads are applicable.
- [ ] Implement resolution strategy:
  - [ ] For `UserDefinedType`, use `TypeSymbol.OperatorMethods` and `ResolveBestOverload`.
  - [ ] For Sharpy builtins (e.g., `list[T]`, `dict[K,V]`), implement allowed operator rules (e.g., containment, equality, but no arithmetic).
  - [ ] For CLR-backed types (`TypeSymbol.ClrType` / primitive Sharpy types), use `_clrOperatorCache` and reflection to find applicable CLR operators; validate parameter/return compatibility with `SemanticType`s.

## Phase 3: Errors & Special Cases

- [ ] Implement descriptive error messages in `OperatorValidator`:
  - [ ] Missing operator on a type (suggest dunder implementation or CLR overload).
  - [ ] Ambiguous overloads.
- [ ] Implement equality-complement behavior:
  - [ ] If only `__eq__` or only `__ne__` exists, synthesize the complement logically for validation (matching `RoslynEmitter`).
- [ ] Implement augmented assignment support helpers:
  - [ ] Map `AssignmentOperator` to in-place dunder (`__iadd__`, etc.) and base `BinaryOperator`.
  - [ ] Prefer in-place dunder; fall back to base operator; enforce result assignability to the target type.

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

- [ ] Add `OperatorSignatureValidatorTests` in `src/Sharpy.Compiler.Tests/Semantic/OperatorSignatureValidatorTests.cs`:
  - [ ] Test valid/invalid parameter counts for unary, binary, and in-place dunders.
  - [ ] Test valid/invalid return types for comparisons, arithmetic, and `__invert__`.
  - [ ] Verify `__pow__` and in-place variants are correctly validated.
- [ ] Add `OperatorValidatorTests` in `src/Sharpy.Compiler.Tests/Semantic/OperatorValidatorTests.cs`:
  - [ ] Pure Sharpy types:
    - [ ] User-defined type with `__add__`, `__iadd__`, and comparison dunders; test normal ops, augmented ops, and overload resolution.
    - [ ] Ambiguous overload scenarios.
  - [ ] CLR interop:
    - [ ] Types mapped to CLR types with `op_Addition`, `op_Equality`, etc.; verify reflection-based resolution and caching.
  - [ ] Comparison chains:
    - [ ] Valid chains, invalid chains, and mixed operators (`<`, `>=`, `==`, etc.).
  - [ ] Power operator:
    - [ ] `**` via `__pow__` and via CLR numeric semantics.
  - [ ] Augmented assignments:
    - [ ] Only `__iadd__` defined.
    - [ ] Only `__add__` defined.
    - [ ] Both defined.
    - [ ] Neither defined (expect error).
- [ ] Run existing tests in `src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs` and ensure they all still pass (especially arithmetic, power, bitwise, comparisons, membership/identity, and type narrowing cases).
