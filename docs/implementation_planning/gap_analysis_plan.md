# Sharpy Compiler Gap Analysis: Implementation Plan

## Executive Summary

This document outlines the implementation plan for two compiler gaps identified during code review:

1. **Multi-file imports: Transitive dependency tracking is incomplete**
2. **Generic type inference: Implicit inference not implemented (must use explicit `func[T](arg)`)**

Both gaps require incremental work across multiple compiler phases. This plan provides tasks suitable for junior engineers or Claude Sonnet with checkboxes for progress tracking.

---

## Gap 1: Multi-File Imports - Transitive Dependency Tracking

### Current State

The compiler has basic multi-file compilation support:
- `ImportResolver.cs` resolves imports and detects circular dependencies
- `DependencyGraphBuilder.cs` / `DependencyGraph.cs` track file-level dependencies  
- `ProjectCompiler.cs` processes files in dependency order

**What's Working:**
- Direct imports (`import utils` or `from utils import func`)
- Circular import detection
- Build order based on direct dependencies
- Module symbol table population

**What's Incomplete:**
1. **Transitive symbol visibility**: When `main.spy` imports `utils.spy` which imports `base.spy`, symbols from `base.spy` are not always properly resolved in `main.spy` when accessed through `utils`
2. **Re-exported symbols**: Package `__init__.spy` files that re-export from submodules don't fully propagate types
3. **Cross-module type resolution**: Types defined in transitively imported modules may not resolve correctly for inheritance, type annotations, or type checking
4. **Semantic binding for transitive imports**: The `SemanticBinding` storage for import metadata may be incomplete for multi-hop imports

### Implementation Tasks

#### Phase 1: Investigation & Test Infrastructure
- [x] **Task 1.1**: Create failing test cases that demonstrate the transitive import issue
  ```
  File: src/Sharpy.Compiler.Tests/ProjectCompilationTests.cs
  Add test: TransitiveImports_ReExportedTypes_ResolveCorrectly()
  Add test: TransitiveImports_NestedPackages_TypesVisible()
  Add test: TransitiveImports_ThreeLevelChain_SymbolsAccessible()
  ```
  **Verification:** `dotnet test --filter "TransitiveImports"`
  **Commit:** "test: Add failing tests for transitive import scenarios"

- [x] **Task 1.2**: Add debug logging to trace import resolution path
  ```
  File: src/Sharpy.Compiler/Semantic/ImportResolver.cs
  - Add logging for each step of module loading
  - Log symbol propagation through re-exports
  - Log final exported symbols dictionary
  ```
  **Verification:** Run compiler with `--verbose` flag on multi-file project
  **Commit:** "debug: Add verbose logging for import resolution tracing"

#### Phase 2: Fix Symbol Propagation for Re-exports
- [x] **Task 2.1**: Enhance `ResolveFromImport` to track original defining module
  ```
  File: src/Sharpy.Compiler/Semantic/ImportResolver.cs
  Method: ResolveFromImport()
  
  Issue: When `__init__.spy` does `from submodule import SomeClass`, the
  re-exported symbol loses its original DefiningModule reference.
  
  Fix: Ensure CreateReExportSymbol() preserves DefiningModule chain
  ```
  **Verification:** Debug test showing symbol.DefiningModule is set correctly
  **Commit:** "fix: Preserve DefiningModule in re-exported symbols"

- [x] **Task 2.2**: Update `ExtractExportedSymbol` to handle transitive exports
  ```
  File: src/Sharpy.Compiler/Semantic/ImportResolver.cs
  Method: ExtractExportedSymbol()
  
  When processing FromImportStatement in a module, ensure the symbols
  being re-exported include full type information from the original module.
  ```
  **Verification:** Test that TypeSymbol includes all fields/methods after re-export
  **Commit:** "fix: Include complete type info in transitive exports"

- [x] **Task 2.3**: Store transitive import metadata in SemanticBinding
  ```
  File: src/Sharpy.Compiler/Semantic/SemanticBinding.cs
  
  Add: Dictionary<string, List<string>> TransitiveImportChain
  - Maps module path to list of transitively imported module paths
  - Enables code generation to add correct `using` statements
  ```
  **Verification:** Unit test verifying TransitiveImportChain populated correctly
  **Commit:** "feat: Track transitive import chains in SemanticBinding"

#### Phase 3: Fix Type Resolution for Transitive Types
- [x] **Task 3.1**: Update TypeResolver to search transitive modules
  ```
  File: src/Sharpy.Compiler/Semantic/TypeResolver.cs
  
  Issue: When resolving type annotations that reference types from 
  transitively imported modules, the type may not be found.
  
  Fix: Extend type lookup to include DefiningModule chain
  ```
  **Verification:** Test case with type annotation using transitively imported type
  **Commit:** "fix: TypeResolver searches transitive import chain"

- [x] **Task 3.2**: Fix cross-module inheritance resolution
  ```
  File: src/Sharpy.Compiler/Semantic/NameResolver.cs
  Method: ResolveInheritance()
  
  Issue: When a class inherits from a type in a transitively imported module,
  base type resolution may fail.
  
  Fix: Look up base types through import chain, not just direct symbol table
  ```
  **Verification:** Test with `class C(B)` where B is in transitively imported module
  **Commit:** "fix: Cross-module inheritance resolves through import chain"

- [x] **Task 3.3**: Update ProjectCompiler type collection for transitive visibility
  ```
  File: src/Sharpy.Compiler/Project/ProjectCompiler.cs
  Method: CollectTypeDeclarations()
  
  Ensure that when collecting type declarations, types from transitively
  imported modules are registered in the shared symbol table with their
  full module path for disambiguation.
  ```
  **Verification:** Multi-file project with deep package hierarchy compiles
  **Commit:** "fix: Type collection includes transitive module types"

#### Phase 4: Code Generation for Transitive Imports
- [x] **Task 4.1**: Generate correct `using` statements for transitive dependencies
  ```
  File: src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs
  
  Issue: Generated C# may be missing `using` statements for namespaces
  from transitively imported modules.
  
  Fix: Use TransitiveImportChain from SemanticBinding to generate all
  required `using` statements.
  ```
  **Verification:** Generated C# compiles without "type not found" errors
  **Commit:** "fix: Emit using statements for transitive dependencies"

- [x] **Task 4.2**: Handle namespace qualification for transitive types
  ```
  File: src/Sharpy.Compiler/CodeGen/RoslynEmitter.TypeDeclarations.cs
  
  When emitting type references that come from transitive imports,
  ensure they are fully qualified or have proper using statements.
  ```
  **Verification:** No CS0246 errors in generated code
  **Commit:** "fix: Fully qualify transitively imported type references"

#### Phase 5: Integration Testing
- [x] **Task 5.1**: Verify calculator_app sample project compiles and runs
  ```
  Path: samples/calculator_app/
  
  Run: sharpyc --project calculator.spyproj
  Verify: No compilation errors, executable runs correctly
  ```
  **Verification:** `./bin/Debug/net9.0/Calculator` runs without errors
  **Commit:** "test: Verify calculator_app multi-file sample"

- [x] **Task 5.2**: Add comprehensive multi-file test suite
  ```
  File: src/Sharpy.Compiler.Tests/Integration/MultiFileCompilationTests.cs
  
  Tests:
  - Three-level package hierarchy
  - Diamond dependency pattern (A→B, A→C, B→D, C→D)
  - Re-exports with type aliases
  - Cross-package inheritance
  ```
  **Verification:** All new integration tests pass
  **Commit:** "test: Add comprehensive multi-file compilation tests"

---

## Gap 2: Generic Type Inference - Implicit Inference Not Implemented

### Current State

The compiler supports explicit generic type arguments:
```python
result = identity[int](42)      # Works - explicit type argument
x = first[str](items)           # Works - explicit type argument
```

But does NOT support implicit type inference:
```python
result = identity(42)           # Should infer T=int, currently errors
x = first(items)                # Should infer T from items, currently errors
```

**Code Locations:**
- `TypeChecker.Expressions.cs:CheckFunctionCall()` - handles explicit generics via `GenericFunctionType`
- `TypeChecker.Expressions.cs:CheckIndexAccess()` - creates `GenericFunctionType` for `func[T]`
- `SemanticType.cs:GenericFunctionType` - represents instantiated generic function

**What's Missing:**
- Type argument inference from argument types
- Constraint checking during inference
- Error messages for failed inference
- Partial inference (some args explicit, some inferred) - spec says not supported

### Implementation Tasks

#### Phase 1: Design & Test Infrastructure
- [x] **Task 1.1**: Document the inference algorithm design
  ```
  File: docs/implementation_planning/generic_inference_design.md
  
  Content:
  1. Inference algorithm (constraint-based unification)
  2. Inference order (left-to-right arguments)
  3. Type constraint satisfaction
  4. Error reporting strategy
  5. Edge cases (no inference possible, ambiguous)
  ```
  **Verification:** Design doc reviewed
  **Commit:** "docs: Add generic type inference design document"

- [ ] **Task 1.2**: Create failing test cases for inference
  ```
  File: src/Sharpy.Compiler.Tests/Semantic/GenericInferenceTests.cs
  
  Tests:
  - InferTypeFromSingleArgument()
  - InferTypeFromMultipleArguments_AllSame()
  - InferTypeFromGenericContainer()
  - InferMultipleTypeParameters()
  - InferenceFailsWithNoArguments()
  - InferenceFailsWithAmbiguousTypes()
  - InferenceWithConstraints()
  ```
  **Verification:** Tests fail with expected error about explicit type args required
  **Commit:** "test: Add failing tests for generic type inference"

#### Phase 2: Core Inference Engine
- [ ] **Task 2.1**: Create `GenericTypeInferenceService` class
  ```
  File: src/Sharpy.Compiler/Semantic/GenericTypeInferenceService.cs
  
  Class: GenericTypeInferenceService
  
  Methods:
  - InferTypeArguments(FunctionSymbol genericFunc, List<SemanticType> argTypes) 
      -> List<SemanticType>? (null = inference failed)
  - UnifyTypeParameter(TypeParameter param, SemanticType concreteType)
      -> SemanticType? (null = unification failed)
  - CheckConstraints(TypeParameter param, SemanticType inferredType) -> bool
  
  Algorithm:
  1. Create empty substitution map: TypeParam -> SemanticType
  2. For each parameter in function signature:
     - If parameter type contains type parameter T
     - And corresponding argument has concrete type C
     - Attempt to unify T with C
  3. If all type parameters are bound, check constraints
  4. Return substituted types or null on failure
  ```
  **Verification:** Unit tests for UnifyTypeParameter pass
  **Commit:** "feat: Add GenericTypeInferenceService with core unification"

- [ ] **Task 2.2**: Implement type unification for simple cases
  ```
  File: src/Sharpy.Compiler/Semantic/GenericTypeInferenceService.cs
  
  Handle unification cases:
  1. TypeParameter vs concrete type (T ↔ int) → bind T=int
  2. Generic vs Generic (list[T] ↔ list[int]) → bind T=int
  3. Function types ((T) -> T ↔ (int) -> int) → bind T=int
  
  private SemanticType? Unify(SemanticType formal, SemanticType actual, 
                              Dictionary<string, SemanticType> substitutions)
  ```
  **Verification:** Tests for simple type unification pass
  **Commit:** "feat: Implement type unification for generic inference"

- [ ] **Task 2.3**: Handle multiple type parameters
  ```
  File: src/Sharpy.Compiler/Semantic/GenericTypeInferenceService.cs
  
  For function: def convert[T, U](value: T, f: (T) -> U) -> U
  Call: convert("42", int.parse)
  
  Inference:
  1. value: T ↔ "42": str → T=str
  2. f: (T)->U ↔ int.parse: (str)->int → verify T=str, bind U=int
  3. Return type U → int
  ```
  **Verification:** Multi-parameter inference tests pass
  **Commit:** "feat: Support multiple type parameter inference"

- [ ] **Task 2.4**: Implement constraint checking
  ```
  File: src/Sharpy.Compiler/Semantic/GenericTypeInferenceService.cs
  
  For: def find_max[T: IComparable[T]](items: list[T]) -> T
  Call: find_max([1, 2, 3])
  
  1. Infer T=int from list[T] ↔ list[int]
  2. Check: does int implement IComparable[int]?
  3. Pass → return int; Fail → inference error
  
  Method: CheckConstraint(TypeParameter param, SemanticType type)
  - Check interface constraints
  - Check class/struct constraints
  - Check multiple constraints (T: IFoo & IBar)
  ```
  **Verification:** Constraint checking tests pass
  **Commit:** "feat: Add constraint checking to generic inference"

#### Phase 3: Integration with TypeChecker
- [ ] **Task 3.1**: Modify `CheckFunctionCall` to attempt inference
  ```
  File: src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs
  Method: CheckFunctionCall()
  
  Before current logic:
  
  // If calling a generic function without explicit type args, try inference
  if (call.Function is Identifier id)
  {
      var symbol = _symbolTable.Lookup(id.Name);
      if (symbol is FunctionSymbol funcSym && funcSym.IsGeneric)
      {
          // Try to infer type arguments from call arguments
          var inferredArgs = _genericInference.InferTypeArguments(funcSym, argTypes);
          if (inferredArgs != null)
          {
              // Create GenericFunctionType as if explicit args were provided
              var genericFuncType = new GenericFunctionType
              {
                  FunctionSymbol = funcSym,
                  TypeArguments = inferredArgs
              };
              // Continue with existing GenericFunctionType handling
          }
          else
          {
              AddError("Cannot infer type arguments; use explicit syntax func[T](...)",
                  call.LineStart, call.ColumnStart);
          }
      }
  }
  ```
  **Verification:** `identity(42)` compiles with inferred type
  **Commit:** "feat: TypeChecker attempts generic inference before explicit"

- [ ] **Task 3.2**: Store inferred types in SemanticInfo for code generation
  ```
  File: src/Sharpy.Compiler/Semantic/SemanticInfo.cs
  
  Add: Dictionary<FunctionCall, List<SemanticType>> InferredTypeArguments
  
  File: src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs
  
  When inference succeeds, store:
  _semanticInfo.SetInferredTypeArguments(call, inferredArgs);
  ```
  **Verification:** SemanticInfo contains inferred types after type checking
  **Commit:** "feat: Store inferred type arguments in SemanticInfo"

- [ ] **Task 3.3**: Add helpful error messages for inference failures
  ```
  File: src/Sharpy.Compiler/Semantic/GenericTypeInferenceService.cs
  
  Return diagnostic info with failure:
  - "Cannot infer type parameter 'T' from argument of type 'object'"
  - "Conflicting inferred types for 'T': 'int' vs 'str'"
  - "Inferred type 'int' does not satisfy constraint 'IComparable[T]'"
  
  Struct: InferenceResult { Success, InferredTypes?, ErrorMessage? }
  ```
  **Verification:** Clear error messages shown for inference failures
  **Commit:** "feat: Descriptive error messages for inference failures"

#### Phase 4: Code Generation Support
- [ ] **Task 4.1**: Emit inferred type arguments in generated C#
  ```
  File: src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs
  
  When emitting generic function call:
  1. Check SemanticInfo for InferredTypeArguments
  2. If present, emit explicit type arguments in C#:
     identity(42) → Identity<int>(42)
  
  (C# will also infer, but explicit is safer for edge cases)
  ```
  **Verification:** Generated C# has explicit type arguments
  **Commit:** "feat: Emit inferred type arguments in code generation"

- [ ] **Task 4.2**: Handle inference for generic method calls
  ```
  File: src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs
  Method: CheckMemberAccess() / CheckFunctionCall()
  
  For: obj.generic_method(arg)
  Where: def generic_method[T](self, x: T) -> T
  
  Apply same inference logic to method calls
  ```
  **Verification:** Generic method calls infer types correctly
  **Commit:** "feat: Generic inference for instance method calls"

#### Phase 5: Edge Cases & Polish
- [ ] **Task 5.1**: Handle inference with nullable types
  ```
  For: def maybe[T](x: T?) -> T?
  Call: maybe(some_nullable_int)
  
  Inference: T? ↔ int? → T=int
  ```
  **Verification:** Nullable type inference tests pass
  **Commit:** "feat: Generic inference handles nullable types"

- [ ] **Task 5.2**: Handle inference with collection types
  ```
  For: def first[T](items: list[T]) -> T
  Call: first([1, 2, 3])
  
  Inference: list[T] ↔ list[int] → T=int
  ```
  **Verification:** Collection type inference tests pass
  **Commit:** "feat: Generic inference extracts type args from collections"

- [ ] **Task 5.3**: Handle return type only generic (requires explicit)
  ```
  For: def create_empty[T]() -> list[T]
  Call: create_empty()  # Cannot infer - no arguments
  
  Error: "Type parameter 'T' cannot be inferred; no arguments provide type information"
  Suggest: "Use explicit syntax: create_empty[int]()"
  ```
  **Verification:** Clear error for non-inferable cases
  **Commit:** "feat: Clear errors for return-type-only generics"

- [ ] **Task 5.4**: Integration test with real-world patterns
  ```
  File: src/Sharpy.Compiler.Tests/Integration/GenericInferenceIntegrationTests.cs
  
  Tests:
  - Identity function
  - Collection operations (first, last, filter, map)
  - Generic class instantiation
  - Chained generic calls
  ```
  **Verification:** All integration tests pass
  **Commit:** "test: Add generic inference integration tests"

---

## Testing Strategy

### Unit Tests
Each task should have corresponding unit tests added before implementation (TDD approach):
- Tests go in corresponding `*.Tests` project
- Use xUnit assertions with FluentAssertions
- Mock dependencies where appropriate

### Integration Tests  
Multi-file tests using temporary directories:
```csharp
var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
try {
    // Create test files
    // Run compiler
    // Assert results
} finally {
    Directory.Delete(tempDir, true);
}
```

### Verification Commands
```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter "FullyQualifiedName~TransitiveImports"
dotnet test --filter "FullyQualifiedName~GenericInference"

# Run with verbose output
dotnet test -v detailed

# Build and test specific project
dotnet build src/Sharpy.Compiler
dotnet test src/Sharpy.Compiler.Tests
```

---

## Risk Assessment

### Gap 1: Multi-File Imports
- **Risk Level:** Medium
- **Complexity:** Moderate - touches multiple compiler phases
- **Dependencies:** None - can be implemented independently
- **Estimated Effort:** 2-3 days

### Gap 2: Generic Type Inference
- **Risk Level:** Medium-High
- **Complexity:** High - new algorithm, many edge cases
- **Dependencies:** Requires solid type system foundation
- **Estimated Effort:** 4-5 days

### Mitigation Strategies
1. Implement Gap 1 first (simpler, unblocks real projects)
2. For Gap 2, start with simple cases and expand
3. Add extensive test coverage before implementation
4. Use feature flags if needed to ship partial progress

---

## Definition of Done

### Gap 1: Multi-File Imports
- [ ] All new test cases pass
- [ ] calculator_app sample compiles and runs
- [ ] No regressions in existing tests
- [ ] Debug logging added for troubleshooting
- [ ] Documentation updated

### Gap 2: Generic Type Inference  
- [ ] All new test cases pass
- [ ] `identity(42)` compiles (basic inference)
- [ ] `first([1,2,3])` compiles (collection inference)
- [ ] Clear errors for non-inferable cases
- [ ] No regressions in explicit generic syntax
- [ ] Language spec examples work as documented

---

## Appendix: Relevant Files

### Multi-File Imports
```
src/Sharpy.Compiler/Semantic/ImportResolver.cs
src/Sharpy.Compiler/Semantic/ModuleResolver.cs
src/Sharpy.Compiler/Semantic/SemanticBinding.cs
src/Sharpy.Compiler/Project/DependencyGraph.cs
src/Sharpy.Compiler/Project/DependencyGraphBuilder.cs
src/Sharpy.Compiler/Project/ProjectCompiler.cs
src/Sharpy.Compiler/CodeGen/RoslynEmitter.CompilationUnit.cs
```

### Generic Type Inference
```
src/Sharpy.Compiler/Semantic/TypeChecker.Expressions.cs
src/Sharpy.Compiler/Semantic/TypeInferenceService.cs
src/Sharpy.Compiler/Semantic/SemanticType.cs
src/Sharpy.Compiler/Semantic/Symbol.cs
src/Sharpy.Compiler/CodeGen/RoslynEmitter.Expressions.cs
```

### Test Files
```
src/Sharpy.Compiler.Tests/ProjectCompilationTests.cs
src/Sharpy.Compiler.Tests/Semantic/TypeCheckerTests.cs
src/Sharpy.Compiler.Tests/Semantic/GenericInferenceTests.cs (new)
src/Sharpy.Compiler.Tests/Integration/MultiFileCompilationTests.cs (new)
```
