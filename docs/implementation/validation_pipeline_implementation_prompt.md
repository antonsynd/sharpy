# Implementation Prompt: Validation Pipeline Completion

## Context

You are implementing the validation pipeline completion for the Sharpy compiler. Sharpy is a statically-typed programming language with Python-like syntax that compiles to C# 9.0.

**Project Location:** `/Users/anton/Documents/github/sharpy`

**Implementation Plan:** Read `/docs/implementation/validation_pipeline_completion_plan.md` for full details.

**Current State:** 
- V2 validators have been migrated: `ControlFlowValidatorV2`, `AccessValidatorV2`, `DefaultParameterValidatorV2`, `ProtocolValidatorV2`, `OperatorValidatorV2`
- The validation pipeline infrastructure exists but is **optional** (legacy mode still works)
- Legacy validators (`OperatorValidator`, `ProtocolValidator`) still return types during type-checking
- All 3415+ tests currently pass

---

## Your Task

Implement the validation pipeline completion in **three phases**. Complete each phase fully before moving to the next. Run tests after each task.

---

## Phase 1: Pipeline Default Enablement

### Task 1.1: Enable Pipeline by Default

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**What to do:**
1. Find the constructor that takes `ValidationPipeline? validationPipeline = null`
2. Change the initialization from:
   ```csharp
   _validationPipeline = validationPipeline;
   _usePipeline = validationPipeline != null;
   ```
   To:
   ```csharp
   _validationPipeline = validationPipeline ?? ValidationPipelineFactory.CreateDefault(logger);
   _usePipeline = true;
   ```
3. Add the necessary `using` statement if not present:
   ```csharp
   using Sharpy.Compiler.Semantic.Validation;
   ```

**Verify:** Run `dotnet test` - all tests should pass.

---

### Task 1.2: Remove Dual Path in Errors Property

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**What to do:**
1. Find the `Errors` property getter
2. Remove the entire `if (!_usePipeline)` block that collects errors from legacy validators
3. The property should now simply return:
   ```csharp
   public IReadOnlyList<SemanticError> Errors
   {
       get
       {
           var allErrors = new List<SemanticError>(_errors);
           allErrors.AddRange(_typeResolver.Errors);
           return allErrors;
       }
   }
   ```
4. Remove the `_usePipeline` field entirely (search for all usages first - should only be in constructor and Errors property now)
5. Update any XML comments that reference legacy behavior

**Verify:** Run `dotnet test` - all tests should pass.

---

### Task 1.3: Audit Entry Points

**Files to check:**
- `src/Sharpy.Compiler/ProjectCompiler.cs`
- `src/Sharpy.Compiler/Compiler.cs`
- Any file containing `new TypeChecker(`

**What to do:**
1. Search the codebase: `grep -r "new TypeChecker(" src/`
2. For each instantiation, verify it doesn't explicitly pass `null` for the pipeline
3. If any test explicitly passes a custom pipeline, that's fine - leave it
4. No changes should be needed if Task 1.1 was done correctly

**Verify:** Run `dotnet test` - all tests should pass.

---

## Phase 2A: Type Inference Separation

### Task 2.1: Create TypeInferenceService

**New File:** `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`

**What to do:**
1. Create the file with the implementation from the plan document (Section Task 2.1)
2. The service should have these public methods:
   - `InferBinaryOpType(BinaryOperator op, SemanticType left, SemanticType right)`
   - `InferUnaryOpType(UnaryOperator op, SemanticType operand)`
   - `InferIterableElementType(SemanticType iterableType)`
   - `InferIndexAccessType(SemanticType container, SemanticType index)`
   - `InferMembershipType(SemanticType container, SemanticType element)`
   - `InferLenType(SemanticType target)`
3. Copy the logic from `OperatorValidator.ValidateBinaryOp` and `ProtocolValidator` but **remove error reporting** - this service only infers types, it doesn't validate

**Key principle:** This service answers "what type does this produce?" not "is this valid?"

**Verify:** The file compiles with `dotnet build src/Sharpy.Compiler`

---

### Task 2.2: Create TypeInferenceService Tests

**New File:** `src/Sharpy.Compiler.Tests/Semantic/TypeInferenceServiceTests.cs`

**What to do:**
Create tests covering:
```csharp
[Fact]
public void InferBinaryOpType_IntPlusInt_ReturnsInt()
{
    var service = new TypeInferenceService(new SymbolTable());
    var result = service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Int, SemanticType.Int);
    Assert.Equal(SemanticType.Int, result);
}

[Fact]
public void InferBinaryOpType_IntDivideInt_ReturnsDouble()
{
    var service = new TypeInferenceService(new SymbolTable());
    var result = service.InferBinaryOpType(BinaryOperator.Divide, SemanticType.Int, SemanticType.Int);
    Assert.Equal(SemanticType.Double, result);
}

[Fact]
public void InferBinaryOpType_StringPlusString_ReturnsString()
{
    var service = new TypeInferenceService(new SymbolTable());
    var result = service.InferBinaryOpType(BinaryOperator.Add, SemanticType.Str, SemanticType.Str);
    Assert.Equal(SemanticType.Str, result);
}

[Fact]
public void InferBinaryOpType_Comparison_ReturnsBool()
{
    var service = new TypeInferenceService(new SymbolTable());
    var result = service.InferBinaryOpType(BinaryOperator.LessThan, SemanticType.Int, SemanticType.Int);
    Assert.Equal(SemanticType.Bool, result);
}

[Fact]
public void InferUnaryOpType_NotAnything_ReturnsBool()
{
    var service = new TypeInferenceService(new SymbolTable());
    var result = service.InferUnaryOpType(UnaryOperator.Not, SemanticType.Int);
    Assert.Equal(SemanticType.Bool, result);
}

[Fact]
public void InferUnaryOpType_NegateInt_ReturnsInt()
{
    var service = new TypeInferenceService(new SymbolTable());
    var result = service.InferUnaryOpType(UnaryOperator.Minus, SemanticType.Int);
    Assert.Equal(SemanticType.Int, result);
}
```

Add more tests for edge cases: nullable types, generic types, user-defined types.

**Verify:** Run `dotnet test --filter "TypeInferenceService"` - all new tests pass.

---

### Task 2.3: Integrate TypeInferenceService into TypeChecker

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**What to do:**
1. Add field:
   ```csharp
   private readonly TypeInferenceService _typeInference;
   ```

2. Initialize in constructor (after `_symbolTable` is set):
   ```csharp
   _typeInference = new TypeInferenceService(_symbolTable, sharedClrCache);
   ```

3. **DO NOT** change `CheckBinaryOp` yet - we'll do that after verifying the service works

**Verify:** Run `dotnet test` - all tests should pass (no behavior change yet).

---

### Task 2.4: Deprecate Legacy Validator Type Methods

**Files:** 
- `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`
- `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs`

**What to do:**
1. Add `[Obsolete]` attribute to methods that return `SemanticType`:

   In `OperatorValidator.cs`:
   ```csharp
   [Obsolete("Use TypeInferenceService.InferBinaryOpType for type inference. This method will be removed in v0.2.")]
   public SemanticType ValidateBinaryOp(...)
   
   [Obsolete("Use TypeInferenceService.InferUnaryOpType for type inference. This method will be removed in v0.2.")]
   public SemanticType ValidateUnaryOp(...)
   ```

   In `ProtocolValidator.cs`:
   ```csharp
   [Obsolete("Use TypeInferenceService for type inference. This method will be removed in v0.2.")]
   public SemanticType ValidateIteration(...)
   
   [Obsolete("Use TypeInferenceService for type inference. This method will be removed in v0.2.")]
   public SemanticType ValidateIndexAccess(...)
   ```

2. You'll see compiler warnings where these are called - that's expected and intentional

**Verify:** Run `dotnet build` - should compile with warnings about obsolete methods.

---

## Phase 2B: Signature Validator Migration

### Task 2.5: Create SignatureValidatorV2

**New File:** `src/Sharpy.Compiler/Semantic/Validation/SignatureValidatorV2.cs`

**What to do:**
1. Create the validator as shown in the plan document (Task 2.4 section)
2. Key points:
   - `Order => 150` (runs early, before type checking)
   - Delegates to existing `OperatorSignatureValidator.ValidateDunderSignature` and `ProtocolSignatureValidator.ValidateDunderSignature`
   - Converts their `List<SemanticError>` results to `AddError()` calls

**Verify:** The file compiles with `dotnet build src/Sharpy.Compiler`

---

### Task 2.6: Create SignatureValidatorV2 Tests

**New File:** `src/Sharpy.Compiler.Tests/Semantic/Validation/SignatureValidatorV2Tests.cs`

**What to do:**
Create tests:
```csharp
public class SignatureValidatorV2Tests
{
    [Fact]
    public void Validate_OperatorWithWrongParamCount_ReportsError()
    {
        var code = @"
class Foo:
    def __add__(self):  # Missing 'other' parameter
        pass
";
        var (module, context) = SetupValidation(code);
        var validator = new SignatureValidatorV2();
        
        validator.Validate(module, context);
        
        Assert.True(context.Diagnostics.HasErrors);
        Assert.Contains(context.Diagnostics.GetErrors(), 
            e => e.Message.Contains("must have exactly 2 parameters"));
    }

    [Fact]
    public void Validate_ProtocolWithWrongParamCount_ReportsError()
    {
        var code = @"
class Foo:
    def __len__(self, extra):  # Extra parameter
        return 0
";
        var (module, context) = SetupValidation(code);
        var validator = new SignatureValidatorV2();
        
        validator.Validate(module, context);
        
        Assert.True(context.Diagnostics.HasErrors);
    }

    [Fact]
    public void Validate_ValidSignatures_NoErrors()
    {
        var code = @"
class Foo:
    def __add__(self, other: Foo) -> Foo:
        return self
    
    def __len__(self) -> int:
        return 0
";
        var (module, context) = SetupValidation(code);
        var validator = new SignatureValidatorV2();
        
        validator.Validate(module, context);
        
        Assert.False(context.Diagnostics.HasErrors);
    }
    
    // Helper method - adapt from existing V2 validator tests
    private (Module, SemanticContext) SetupValidation(string code) { ... }
}
```

**Verify:** Run `dotnet test --filter "SignatureValidatorV2"` - all tests pass.

---

### Task 2.7: Add SignatureValidatorV2 to Pipeline

**File:** `src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs`

**What to do:**
1. Add the new validator to `CreateDefault()`:
   ```csharp
   public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
   {
       return new ValidationPipeline(logger)
           .AddValidator(new SignatureValidatorV2())         // Order: 150 - NEW
           .AddValidator(new DefaultParameterValidatorV2())  // Order: 250
           .AddValidator(new ControlFlowValidatorV2())       // Order: 400
           .AddValidator(new AccessValidatorV2())            // Order: 450
           .AddValidator(new ProtocolValidatorV2())          // Order: 500
           .AddValidator(new OperatorValidatorV2())          // Order: 500
           ;
   }
   ```

**Verify:** Run `dotnet test` - all tests should pass.

---

### Task 2.8: Remove Signature Validation from NameResolver

**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`

**What to do:**
1. Find `ResolveMethodDeclaration` method
2. Find the blocks that call `OperatorSignatureValidator.ValidateDunderSignature` and `ProtocolSignatureValidator.ValidateDunderSignature`
3. **Remove** the validation calls and error handling, but **keep** the registration logic

Before:
```csharp
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    var validationErrors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);
    if (validationErrors.Count > 0)
    {
        _errors.AddRange(validationErrors);
    }
    else
    {
        // Registration logic
        if (!owningType.OperatorMethods.TryGetValue(method.Name, out var overloads))
        {
            overloads = new List<FunctionSymbol>();
            owningType.OperatorMethods[method.Name] = overloads;
        }
        overloads.Add(funcSymbol);
    }
}
```

After:
```csharp
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    // Signature validation moved to SignatureValidatorV2 in validation pipeline
    // Here we just register the method
    if (!owningType.OperatorMethods.TryGetValue(method.Name, out var overloads))
    {
        overloads = new List<FunctionSymbol>();
        owningType.OperatorMethods[method.Name] = overloads;
    }
    overloads.Add(funcSymbol);
}
```

4. Do the same for `ProtocolSignatureValidator` calls

**Important:** This changes error timing - errors that appeared during name resolution now appear during validation. This should be fine, but watch for test failures.

**Verify:** Run `dotnet test` - all tests should pass.

---

## Phase 3: Testing and State Management

### Task 3.1: Create AstTraversalContext

**New File:** `src/Sharpy.Compiler/Semantic/Validation/AstTraversalContext.cs`

**What to do:**
Create the class as shown in the plan document (Task 3.2 section). Key features:
- Stack-based tracking of `CurrentClass`, `CurrentFunction`, loop state
- `IDisposable` pattern for automatic cleanup with `using` statements
- Thread-safe for future parallel compilation

```csharp
namespace Sharpy.Compiler.Semantic.Validation;

public class AstTraversalContext
{
    private readonly Stack<TypeSymbol?> _classStack = new();
    private readonly Stack<FunctionSymbol?> _functionStack = new();
    private readonly Stack<bool> _loopStack = new();

    public TypeSymbol? CurrentClass => _classStack.Count > 0 ? _classStack.Peek() : null;
    public FunctionSymbol? CurrentFunction => _functionStack.Count > 0 ? _functionStack.Peek() : null;
    public bool InLoop => _loopStack.Count > 0 && _loopStack.Peek();
    public int LoopDepth => _loopStack.Count(l => l);

    public IDisposable EnterClass(TypeSymbol? symbol)
    {
        _classStack.Push(symbol);
        return new StackPopper<TypeSymbol?>(_classStack);
    }

    public IDisposable EnterFunction(FunctionSymbol? symbol)
    {
        _functionStack.Push(symbol);
        return new StackPopper<FunctionSymbol?>(_functionStack);
    }

    public IDisposable EnterLoop()
    {
        _loopStack.Push(true);
        return new StackPopper<bool>(_loopStack);
    }

    private class StackPopper<T> : IDisposable
    {
        private readonly Stack<T> _stack;
        public StackPopper(Stack<T> stack) => _stack = stack;
        public void Dispose() => _stack.Pop();
    }
}
```

**Verify:** The file compiles.

---

### Task 3.2: Add AstTraversalContext to SemanticContext

**File:** `src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs`

**What to do:**
1. Add the property:
   ```csharp
   /// <summary>
   /// Centralized AST traversal state for validators.
   /// </summary>
   public AstTraversalContext Traversal { get; } = new();
   ```

2. You can optionally deprecate the existing state properties:
   ```csharp
   [Obsolete("Use Traversal.CurrentClass instead")]
   public TypeSymbol? CurrentClass { get; set; }
   ```
   
   But this is optional - leaving both is fine for now.

**Verify:** Run `dotnet build` - should compile.

---

### Task 3.3: Create Integration Tests

**New File:** `src/Sharpy.Compiler.Tests/Integration/ValidationPipelineIntegrationTests.cs`

**What to do:**
Create comprehensive integration tests as shown in the plan document (Task 3.1 section). Must include:

1. **Error ordering test** - verify errors appear in validator order
2. **Multi-file test** - verify cross-file validation works
3. **Max errors test** - verify pipeline stops at limit
4. **Existing fixture tests** - verify existing test fixtures still pass

Use existing test helpers from the codebase. Look at how other integration tests are structured in `src/Sharpy.Compiler.Tests/Integration/`.

**Verify:** Run `dotnet test --filter "ValidationPipelineIntegration"` - all tests pass.

---

### Task 3.4: Add Pipeline Ordering Tests

**File:** `src/Sharpy.Compiler.Tests/Semantic/Validation/ValidationPipelineTests.cs`

**What to do:**
Add or update tests to verify:
```csharp
[Fact]
public void DefaultPipeline_HasAllValidators()
{
    var pipeline = ValidationPipelineFactory.CreateDefault();
    var validators = pipeline.Validators.ToList();

    Assert.Equal(6, validators.Count);
    Assert.Contains(validators, v => v is SignatureValidatorV2);
    Assert.Contains(validators, v => v is DefaultParameterValidatorV2);
    Assert.Contains(validators, v => v is ControlFlowValidatorV2);
    Assert.Contains(validators, v => v is AccessValidatorV2);
    Assert.Contains(validators, v => v is ProtocolValidatorV2);
    Assert.Contains(validators, v => v is OperatorValidatorV2);
}

[Fact]
public void DefaultPipeline_ValidatorsInCorrectOrder()
{
    var pipeline = ValidationPipelineFactory.CreateDefault();
    var orders = pipeline.Validators.Select(v => v.Order).ToList();
    
    // Should be sorted
    Assert.Equal(orders.OrderBy(o => o).ToList(), orders);
    
    // Signature validator should be first
    Assert.Equal(150, orders[0]);
}
```

**Verify:** Run `dotnet test --filter "ValidationPipeline"` - all tests pass.

---

## Final Verification

After completing all tasks:

1. Run the full test suite:
   ```bash
   dotnet test
   ```
   All 3415+ tests should pass.

2. Run a manual compilation test:
   ```bash
   cd src/Sharpy.Cli
   dotnet run -- compile ../path/to/test/file.spy
   ```

3. Check for any remaining `_usePipeline` references:
   ```bash
   grep -r "_usePipeline" src/
   ```
   Should return nothing.

4. Check for obsolete method warnings:
   ```bash
   dotnet build 2>&1 | grep -i obsolete
   ```
   Should show warnings for `ValidateBinaryOp`, `ValidateUnaryOp`, etc.

---

## Troubleshooting

**If tests fail after Task 1.1/1.2:**
- The pipeline may be reporting errors that weren't reported before
- Check if V2 validators are stricter than legacy validators
- Compare error messages between branches

**If tests fail after Task 2.8 (removing signature validation from NameResolver):**
- Error timing changed - errors now appear later in compilation
- Some tests may check error counts at specific phases
- Update test expectations if the error content is the same

**If you're unsure about existing behavior:**
- Create a git branch before changes
- Run tests on unchanged code first
- Compare error messages before/after

---

## Files Created/Modified Summary

**New Files:**
- `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`
- `src/Sharpy.Compiler/Semantic/Validation/SignatureValidatorV2.cs`
- `src/Sharpy.Compiler/Semantic/Validation/AstTraversalContext.cs`
- `src/Sharpy.Compiler.Tests/Semantic/TypeInferenceServiceTests.cs`
- `src/Sharpy.Compiler.Tests/Semantic/Validation/SignatureValidatorV2Tests.cs`
- `src/Sharpy.Compiler.Tests/Integration/ValidationPipelineIntegrationTests.cs`

**Modified Files:**
- `src/Sharpy.Compiler/Semantic/TypeChecker.cs`
- `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`
- `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs`
- `src/Sharpy.Compiler/Semantic/NameResolver.cs`
- `src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs`
- `src/Sharpy.Compiler/Semantic/Validation/SemanticContext.cs`
- `src/Sharpy.Compiler.Tests/Semantic/Validation/ValidationPipelineTests.cs`

---

## Commit Strategy

Make incremental commits after each task:

```
git commit -m "feat(validation): enable pipeline by default in TypeChecker"
git commit -m "refactor(validation): remove dual-path error collection"
git commit -m "feat(semantic): add TypeInferenceService for operator/protocol type inference"
git commit -m "test(semantic): add TypeInferenceService tests"
git commit -m "refactor(validation): deprecate type-returning methods in legacy validators"
git commit -m "feat(validation): add SignatureValidatorV2"
git commit -m "test(validation): add SignatureValidatorV2 tests"
git commit -m "refactor(validation): move signature validation from NameResolver to pipeline"
git commit -m "feat(validation): add AstTraversalContext for centralized state"
git commit -m "test(integration): add validation pipeline integration tests"
```

This allows easy rollback if issues are discovered.
