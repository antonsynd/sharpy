# Validation Pipeline Completion Plan

**Date:** January 2025  
**Status:** Implementation Ready  
**Prerequisites:** V2 validator migration (completed)  
**Relates To:** `architecture_review_and_recommendations.md`, `architecture_review_addendum_future_features.md`

---

## Overview

This document provides a detailed implementation plan for completing the validation pipeline migration. The work consolidates semantic validation under a unified architecture, preparing the codebase for future phases (CompilerServices, CompilationUnit model, parallel compilation).

### Goals

1. **Make the validation pipeline the default and only path** – eliminate dual-mode complexity
2. **Separate type inference from validation** – clean architectural boundary
3. **Complete validator migration** – include signature validators
4. **Ensure robustness** – comprehensive testing and state management
5. **Prepare for future architecture** – align with CompilerServices and CompilationUnit plans

### Non-Goals

- Deleting legacy validator classes (deferred to post-stabilization)
- Full CompilerServices implementation (separate phase)
- Immutable AST migration (separate phase per Rec #7)

---

## Architecture Alignment

This plan is designed to complement, not conflict with, the broader architecture recommendations:

| Architecture Rec | How This Plan Aligns |
|------------------|---------------------|
| **#1: Validation Pipeline** | Completes the consolidation started with V2 validators |
| **#2: CompilerServices** | Type inference service is a precursor to full CompilerServices |
| **#3: Pre-compute CodeGenInfo** | Clean separation enables future CodeGenInfo attachment |
| **#5: Service Layer** | Type inference service follows the service pattern |
| **#7: Immutable AST** | Stateless validators prepare for immutable data flow |
| **#8: Dependency Graph** | Clean validation enables future incremental compilation |

---

## Task Breakdown

### Phase 1: Pipeline Default Enablement (P1)

**Objective:** Make the validation pipeline the default and only code path.

#### Task 1.1: Enable Pipeline by Default in TypeChecker

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Current State:**
```csharp
public TypeChecker(
    SymbolTable symbolTable,
    SemanticInfo semanticInfo,
    TypeResolver typeResolver,
    ICompilerLogger? logger = null,
    ValidationPipeline? validationPipeline = null)  // Optional, null = legacy mode
{
    _validationPipeline = validationPipeline;
    _usePipeline = validationPipeline != null;
    // ...
}
```

**Target State:**
```csharp
public TypeChecker(
    SymbolTable symbolTable,
    SemanticInfo semanticInfo,
    TypeResolver typeResolver,
    ICompilerLogger? logger = null,
    ValidationPipeline? validationPipeline = null)  // null = use default pipeline
{
    _validationPipeline = validationPipeline ?? ValidationPipelineFactory.CreateDefault(logger);
    _usePipeline = true;  // Always true now
    // ...
}
```

**Implementation Steps:**

1. Update `TypeChecker` constructor to create default pipeline when none provided
2. Change `_usePipeline` to always be `true` (or remove the field entirely)
3. Run full test suite to identify any failures
4. Fix any tests that were implicitly relying on legacy behavior

**Estimated Effort:** 1-2 hours  
**Risk:** Low (tests will catch regressions)

---

#### Task 1.2: Remove Dual Path in TypeChecker.Errors

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Current State:**
```csharp
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        var allErrors = new List<SemanticError>(_errors);
        allErrors.AddRange(_typeResolver.Errors);

        if (!_usePipeline)
        {
            // DEPRECATED: Legacy behavior
            allErrors.AddRange(_controlFlowValidator.Errors);
            allErrors.AddRange(_accessValidator.Errors);
            allErrors.AddRange(_operatorValidator.Errors);
            allErrors.AddRange(_protocolValidator.Errors);
            allErrors.AddRange(_defaultParameterValidator.Errors);
        }
        return allErrors;
    }
}
```

**Target State:**
```csharp
public IReadOnlyList<SemanticError> Errors
{
    get
    {
        // All errors now flow through the pipeline's DiagnosticBag
        var allErrors = new List<SemanticError>(_errors);
        allErrors.AddRange(_typeResolver.Errors);
        // V2 validator errors are merged in CheckModule via pipeline
        return allErrors;
    }
}
```

**Implementation Steps:**

1. Remove the `if (!_usePipeline)` branch entirely
2. Remove the `_usePipeline` field if no longer needed elsewhere
3. Update XML documentation to reflect the new behavior
4. Verify all tests pass

**Estimated Effort:** 30 minutes  
**Risk:** Low (dependent on Task 1.1 completion)

---

#### Task 1.3: Update All Entry Points

**Files:**
- `src/Sharpy.Compiler/ProjectCompiler.cs`
- `src/Sharpy.Compiler/Compiler.cs`
- Any test helpers that create `TypeChecker` instances

**Implementation Steps:**

1. Search for all `new TypeChecker(` instantiations
2. Remove explicit `ValidationPipeline` parameter where it was passed (now automatic)
3. For tests that need custom pipelines, ensure they still work
4. Update `ProjectCompiler` to optionally accept a pipeline factory for customization

**Example Update in ProjectCompiler:**
```csharp
// Before
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);

// After (no change needed if constructor defaults are correct)
var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, _logger);
// Pipeline is now automatically created
```

**Estimated Effort:** 1 hour  
**Risk:** Low

---

### Phase 2: Type Inference Separation (P2)

**Objective:** Extract type inference logic from legacy validators into a dedicated service, enabling clean separation of validation (error detection) from type computation.

#### Task 2.1: Create TypeInferenceService

**New File:** `src/Sharpy.Compiler/Semantic/TypeInferenceService.cs`

This service centralizes type inference for operators and protocols, replacing the type-returning methods in legacy validators.

```csharp
namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Provides type inference for operators and protocol operations.
/// This is a precursor to the full CompilerServices layer (Rec #5).
/// 
/// Design notes:
/// - Stateless: all state comes from SymbolTable and SemanticInfo
/// - Cacheable: results can be memoized in SemanticInfo
/// - Thread-safe: no mutable instance state (future parallel compilation)
/// </summary>
public class TypeInferenceService
{
    private readonly SymbolTable _symbolTable;
    private readonly ClrMemberCache _clrCache;

    public TypeInferenceService(SymbolTable symbolTable, ClrMemberCache? clrCache = null)
    {
        _symbolTable = symbolTable;
        _clrCache = clrCache ?? new ClrMemberCache();
    }

    /// <summary>
    /// Infers the result type of a binary operation.
    /// Does NOT report errors - that's the validator's job.
    /// </summary>
    public SemanticType InferBinaryOpType(
        BinaryOperator op,
        SemanticType leftType,
        SemanticType rightType)
    {
        // Logical operators always return bool
        if (op is BinaryOperator.And or BinaryOperator.Or)
            return SemanticType.Bool;

        // Identity/membership operators return bool
        if (op is BinaryOperator.Is or BinaryOperator.IsNot 
            or BinaryOperator.In or BinaryOperator.NotIn)
            return SemanticType.Bool;

        // Null coalescing returns non-nullable left or right type
        if (op == BinaryOperator.NullCoalesce)
            return InferNullCoalesceType(leftType, rightType);

        // Comparison operators return bool
        if (IsComparisonOperator(op))
            return SemanticType.Bool;

        // Arithmetic/bitwise - resolve via dunder or CLR
        return ResolveArithmeticResultType(op, leftType, rightType);
    }

    /// <summary>
    /// Infers the result type of a unary operation.
    /// </summary>
    public SemanticType InferUnaryOpType(UnaryOperator op, SemanticType operandType)
    {
        if (op == UnaryOperator.Not)
            return SemanticType.Bool;

        // Numeric negation/positive preserves type
        if (op is UnaryOperator.Minus or UnaryOperator.Plus)
        {
            if (IsNumericType(operandType))
                return operandType;
        }

        // Bitwise not on int returns int
        if (op == UnaryOperator.BitwiseNot && operandType == SemanticType.Int)
            return SemanticType.Int;

        return ResolveUnaryResultType(op, operandType);
    }

    /// <summary>
    /// Infers the element type when iterating over a collection.
    /// Used for 'for x in collection' type inference.
    /// </summary>
    public SemanticType InferIterableElementType(SemanticType iterableType)
    {
        // Generic collections
        if (iterableType is GenericType generic)
        {
            return generic.Name switch
            {
                "list" or "set" => generic.TypeArguments.FirstOrDefault() ?? SemanticType.Unknown,
                "dict" => generic.TypeArguments.FirstOrDefault() ?? SemanticType.Unknown, // Keys
                "tuple" => SemanticType.Unknown, // Heterogeneous, can't infer single type
                _ => SemanticType.Unknown
            };
        }

        // String iteration yields str (single characters are strings in Sharpy)
        if (iterableType == SemanticType.Str)
            return SemanticType.Str;

        // Range yields int
        if (iterableType is UserDefinedType udt && udt.Symbol?.Name == "range")
            return SemanticType.Int;

        // Check for __iter__ protocol on user types
        if (iterableType is UserDefinedType userType && userType.Symbol != null)
        {
            return InferIteratorElementType(userType);
        }

        return SemanticType.Unknown;
    }

    /// <summary>
    /// Infers the result type of an index access (obj[key]).
    /// </summary>
    public SemanticType InferIndexAccessType(SemanticType containerType, SemanticType indexType)
    {
        if (containerType is GenericType generic)
        {
            return generic.Name switch
            {
                "list" => generic.TypeArguments.FirstOrDefault() ?? SemanticType.Unknown,
                "dict" => generic.TypeArguments.Skip(1).FirstOrDefault() ?? SemanticType.Unknown,
                "tuple" => SemanticType.Unknown, // Would need literal index for precise type
                _ => SemanticType.Unknown
            };
        }

        if (containerType == SemanticType.Str)
            return SemanticType.Str;

        // Check __getitem__ on user types
        if (containerType is UserDefinedType userType && userType.Symbol != null)
        {
            return InferGetItemType(userType, indexType);
        }

        return SemanticType.Unknown;
    }

    /// <summary>
    /// Infers the result type of a membership test (x in container).
    /// Always returns bool, but validates the container supports __contains__.
    /// </summary>
    public SemanticType InferMembershipType(SemanticType containerType, SemanticType elementType)
    {
        // Membership tests always return bool if valid
        return SemanticType.Bool;
    }

    /// <summary>
    /// Infers the result type of len(obj).
    /// </summary>
    public SemanticType InferLenType(SemanticType targetType)
    {
        // len() always returns int if valid
        return SemanticType.Int;
    }

    #region Private Helpers

    private SemanticType InferNullCoalesceType(SemanticType leftType, SemanticType rightType)
    {
        if (leftType is NullableType nullable)
        {
            // Result is non-nullable if right is non-nullable
            return rightType is NullableType ? leftType : nullable.UnderlyingType;
        }
        // If left isn't nullable, validator will report error, but we still infer
        return leftType;
    }

    private SemanticType ResolveArithmeticResultType(
        BinaryOperator op, SemanticType left, SemanticType right)
    {
        // Numeric promotion rules
        if (IsNumericType(left) && IsNumericType(right))
        {
            // Division always returns float/double
            if (op == BinaryOperator.Divide)
                return SemanticType.Double;

            // Floor division returns int
            if (op == BinaryOperator.FloorDivide)
                return SemanticType.Int;

            // Other arithmetic: promote to wider type
            return PromoteNumericTypes(left, right);
        }

        // String concatenation
        if (left == SemanticType.Str && right == SemanticType.Str && op == BinaryOperator.Add)
            return SemanticType.Str;

        // String repetition
        if (left == SemanticType.Str && right == SemanticType.Int && op == BinaryOperator.Multiply)
            return SemanticType.Str;

        // List concatenation
        if (left is GenericType gl && gl.Name == "list" &&
            right is GenericType gr && gr.Name == "list" && op == BinaryOperator.Add)
            return left; // Preserve left's type parameter

        // Check user-defined operators
        if (left is UserDefinedType udt)
            return ResolveUserDefinedOperator(udt, op, right);

        return SemanticType.Unknown;
    }

    private SemanticType ResolveUnaryResultType(UnaryOperator op, SemanticType operand)
    {
        if (operand is UserDefinedType udt && udt.Symbol != null)
        {
            var dunderName = op switch
            {
                UnaryOperator.Minus => "__neg__",
                UnaryOperator.Plus => "__pos__",
                UnaryOperator.BitwiseNot => "__invert__",
                _ => null
            };

            if (dunderName != null && udt.Symbol.OperatorMethods.TryGetValue(dunderName, out var methods))
            {
                // Return the return type of the first matching overload
                return methods.FirstOrDefault()?.ReturnType ?? SemanticType.Unknown;
            }
        }

        return SemanticType.Unknown;
    }

    private SemanticType ResolveUserDefinedOperator(
        UserDefinedType type, BinaryOperator op, SemanticType rightType)
    {
        var dunderName = BinaryOperatorToDunder(op);
        if (dunderName == null || type.Symbol == null)
            return SemanticType.Unknown;

        if (type.Symbol.OperatorMethods.TryGetValue(dunderName, out var methods))
        {
            // Find matching overload (simplified - first match)
            var method = methods.FirstOrDefault();
            return method?.ReturnType ?? SemanticType.Unknown;
        }

        return SemanticType.Unknown;
    }

    private SemanticType InferIteratorElementType(UserDefinedType type)
    {
        if (type.Symbol?.ProtocolMethods.TryGetValue("__iter__", out var iterMethods) == true)
        {
            var iterMethod = iterMethods.FirstOrDefault();
            if (iterMethod?.ReturnType is UserDefinedType iteratorType)
            {
                // Look for __next__ on the iterator type
                if (iteratorType.Symbol?.ProtocolMethods.TryGetValue("__next__", out var nextMethods) == true)
                {
                    return nextMethods.FirstOrDefault()?.ReturnType ?? SemanticType.Unknown;
                }
            }
        }
        return SemanticType.Unknown;
    }

    private SemanticType InferGetItemType(UserDefinedType type, SemanticType indexType)
    {
        if (type.Symbol?.ProtocolMethods.TryGetValue("__getitem__", out var methods) == true)
        {
            return methods.FirstOrDefault()?.ReturnType ?? SemanticType.Unknown;
        }
        return SemanticType.Unknown;
    }

    private bool IsNumericType(SemanticType type)
    {
        return type == SemanticType.Int
            || type == SemanticType.Long
            || type == SemanticType.Float
            || type == SemanticType.Float32
            || type == SemanticType.Double;
    }

    private bool IsComparisonOperator(BinaryOperator op)
    {
        return op is BinaryOperator.Equal or BinaryOperator.NotEqual
            or BinaryOperator.LessThan or BinaryOperator.LessThanOrEqual
            or BinaryOperator.GreaterThan or BinaryOperator.GreaterThanOrEqual;
    }

    private SemanticType PromoteNumericTypes(SemanticType left, SemanticType right)
    {
        // Promotion order: int < long < float32 < float < double
        var typeOrder = new Dictionary<SemanticType, int>
        {
            [SemanticType.Int] = 1,
            [SemanticType.Long] = 2,
            [SemanticType.Float32] = 3,
            [SemanticType.Float] = 4,
            [SemanticType.Double] = 5
        };

        var leftOrder = typeOrder.GetValueOrDefault(left, 0);
        var rightOrder = typeOrder.GetValueOrDefault(right, 0);

        return leftOrder >= rightOrder ? left : right;
    }

    private string? BinaryOperatorToDunder(BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => "__add__",
            BinaryOperator.Subtract => "__sub__",
            BinaryOperator.Multiply => "__mul__",
            BinaryOperator.Divide => "__truediv__",
            BinaryOperator.FloorDivide => "__floordiv__",
            BinaryOperator.Modulo => "__mod__",
            BinaryOperator.Power => "__pow__",
            BinaryOperator.BitwiseAnd => "__and__",
            BinaryOperator.BitwiseOr => "__or__",
            BinaryOperator.BitwiseXor => "__xor__",
            BinaryOperator.LeftShift => "__lshift__",
            BinaryOperator.RightShift => "__rshift__",
            _ => null
        };
    }

    #endregion
}
```

**Implementation Steps:**

1. Create the new file with the service class
2. Add comprehensive XML documentation
3. Write unit tests for all inference methods
4. Ensure CLR type handling matches legacy behavior

**Estimated Effort:** 4-6 hours  
**Risk:** Medium (must match existing type inference behavior exactly)

---

#### Task 2.2: Integrate TypeInferenceService into TypeChecker

**File:** `src/Sharpy.Compiler/Semantic/TypeChecker.cs`

**Changes:**

1. Add `TypeInferenceService` as a field:
```csharp
private readonly TypeInferenceService _typeInference;
```

2. Initialize in constructor:
```csharp
var sharedClrCache = new ClrMemberCache();
_typeInference = new TypeInferenceService(_symbolTable, sharedClrCache);
```

3. Update `CheckBinaryOp` to use the service for type inference:
```csharp
private SemanticType CheckBinaryOp(BinaryOp binOp)
{
    // ... validation code unchanged ...
    
    // Use TypeInferenceService for type computation
    return _typeInference.InferBinaryOpType(binOp.Operator, leftType, rightType);
}
```

4. Update `CheckUnaryOp`, `CheckIndexAccess`, and other methods similarly

**Estimated Effort:** 2-3 hours  
**Risk:** Medium (regression risk in type inference)

---

#### Task 2.3: Deprecate Type-Returning Methods in Legacy Validators

**Files:**
- `src/Sharpy.Compiler/Semantic/OperatorValidator.cs`
- `src/Sharpy.Compiler/Semantic/ProtocolValidator.cs`

**Changes:**

Add `[Obsolete]` attributes to methods that return types:

```csharp
/// <summary>
/// Validates a binary operation and returns the result type.
/// </summary>
[Obsolete("Use TypeInferenceService.InferBinaryOpType for type inference. " +
          "Use OperatorValidatorV2 for validation. This method will be removed in v0.2.")]
public SemanticType ValidateBinaryOp(...)
```

This provides a migration path without breaking existing code.

**Estimated Effort:** 30 minutes  
**Risk:** Low

---

### Phase 2B: Signature Validator Migration (P2)

**Objective:** Migrate signature validators to the V2 pipeline pattern.

#### Task 2.4: Create SignatureValidatorV2

**New File:** `src/Sharpy.Compiler/Semantic/Validation/SignatureValidatorV2.cs`

This validator combines operator and protocol signature validation into a single pass.

```csharp
namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Validates dunder method signatures for both operators and protocols.
/// Runs early in the pipeline (after name resolution, before type checking).
/// 
/// Combines the functionality of:
/// - OperatorSignatureValidator (operator dunders)
/// - ProtocolSignatureValidator (protocol dunders)
/// </summary>
public class SignatureValidatorV2 : SemanticValidatorBase
{
    public override string Name => "SignatureValidator";
    public override int Order => 150; // After name resolution (100), before type checking (300)

    public override void Validate(Module module, SemanticContext context)
    {
        foreach (var stmt in module.Body)
        {
            ValidateStatement(stmt, context);
        }
    }

    private void ValidateStatement(Statement stmt, SemanticContext context)
    {
        switch (stmt)
        {
            case ClassDef classDef:
                ValidateClassSignatures(classDef, context);
                break;
            case StructDef structDef:
                ValidateStructSignatures(structDef, context);
                break;
        }
    }

    private void ValidateClassSignatures(ClassDef classDef, SemanticContext context)
    {
        // Look up the TypeSymbol
        var typeSymbol = context.SymbolTable.Lookup(classDef.Name) as TypeSymbol;
        if (typeSymbol == null) return;

        foreach (var stmt in classDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                ValidateMethodSignature(method, typeSymbol, context);
            }
        }
    }

    private void ValidateStructSignatures(StructDef structDef, SemanticContext context)
    {
        var typeSymbol = context.SymbolTable.Lookup(structDef.Name) as TypeSymbol;
        if (typeSymbol == null) return;

        foreach (var stmt in structDef.Body)
        {
            if (stmt is FunctionDef method)
            {
                ValidateMethodSignature(method, typeSymbol, context);
            }
        }
    }

    private void ValidateMethodSignature(FunctionDef method, TypeSymbol owningType, SemanticContext context)
    {
        // Check if it's an operator dunder
        if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
        {
            ValidateOperatorSignature(method, owningType, context);
        }
        // Check if it's a protocol dunder
        else if (ProtocolSignatureValidator.IsProtocolDunder(method.Name))
        {
            ValidateProtocolSignature(method, owningType, context);
        }
    }

    private void ValidateOperatorSignature(FunctionDef method, TypeSymbol owningType, SemanticContext context)
    {
        // Delegate to existing static validator, convert errors to diagnostics
        var errors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);
        foreach (var error in errors)
        {
            AddError(context, error.Message, error.Line, error.Column);
        }
    }

    private void ValidateProtocolSignature(FunctionDef method, TypeSymbol owningType, SemanticContext context)
    {
        var errors = ProtocolSignatureValidator.ValidateDunderSignature(method, owningType);
        foreach (var error in errors)
        {
            AddError(context, error.Message, error.Line, error.Column);
        }
    }
}
```

**Implementation Steps:**

1. Create the new validator file
2. Reuse existing static validator logic (don't duplicate)
3. Add to `ValidationPipelineFactory.CreateDefault()`
4. Write unit tests

**Estimated Effort:** 2-3 hours  
**Risk:** Low (wrapping existing validated logic)

---

#### Task 2.5: Update ValidationPipelineFactory

**File:** `src/Sharpy.Compiler/Semantic/Validation/ValidationPipelineFactory.cs`

```csharp
public static ValidationPipeline CreateDefault(ICompilerLogger? logger = null)
{
    return new ValidationPipeline(logger)
        // Signature validation (early, after name resolution)
        .AddValidator(new SignatureValidatorV2())         // Order: 150
        // Pre-type-checking validators
        .AddValidator(new DefaultParameterValidatorV2())  // Order: 250
        // Post-type-checking validators
        .AddValidator(new ControlFlowValidatorV2())       // Order: 400
        .AddValidator(new AccessValidatorV2())            // Order: 450
        .AddValidator(new ProtocolValidatorV2())          // Order: 500
        .AddValidator(new OperatorValidatorV2())          // Order: 500
        ;
}
```

**Estimated Effort:** 15 minutes  
**Risk:** Low

---

#### Task 2.6: Remove Signature Validation from NameResolver

**File:** `src/Sharpy.Compiler/Semantic/NameResolver.cs`

Currently, `NameResolver.ResolveMethodDeclaration` calls both signature validators:

```csharp
// Validate and register operator dunder methods
if (OperatorSignatureValidator.IsOperatorDunder(method.Name))
{
    var validationErrors = OperatorSignatureValidator.ValidateDunderSignature(method, owningType);
    // ... error handling ...
}
```

**Changes:**

1. Remove the signature validation calls from `NameResolver`
2. Keep only the registration logic (adding to `OperatorMethods`/`ProtocolMethods` dictionaries)
3. Validation now happens in `SignatureValidatorV2` during pipeline execution

**Note:** This is a timing change. Previously, signature errors appeared during name resolution. Now they appear during validation pipeline execution. Ensure error messages remain clear about the source.

**Estimated Effort:** 1 hour  
**Risk:** Medium (timing change may affect error ordering)

---

### Phase 3: Testing and State Management (P3)

**Objective:** Ensure robustness through comprehensive testing and proper state tracking.

#### Task 3.1: Add Integration Tests for Full Pipeline

**New File:** `src/Sharpy.Compiler.Tests/Integration/ValidationPipelineIntegrationTests.cs`

```csharp
namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Integration tests that exercise the full validation pipeline
/// with realistic multi-file projects.
/// </summary>
public class ValidationPipelineIntegrationTests : IClassFixture<TestProjectFixture>
{
    private readonly TestProjectFixture _fixture;

    public ValidationPipelineIntegrationTests(TestProjectFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Pipeline_ReportsErrorsInCorrectOrder()
    {
        // Arrange: Code with multiple error types
        var code = @"
class Foo:
    def __add__(self):  # Wrong param count (signature error)
        x = 1
        break  # Control flow error
        return x

def test():
    f = Foo()
    f._private_field  # Access error
";
        
        // Act
        var errors = CompileAndGetErrors(code);

        // Assert: Errors should appear in validation order
        Assert.True(errors.Count >= 3);
        // Signature errors first (Order: 150)
        // Then control flow (Order: 400)
        // Then access (Order: 450)
    }

    [Fact]
    public void Pipeline_HandlesMultiFileProject()
    {
        // Arrange: Multi-file project with cross-file dependencies
        var files = new Dictionary<string, string>
        {
            ["base.spy"] = @"
class Base:
    def method(self) -> int:
        return 42
",
            ["derived.spy"] = @"
from base import Base

class Derived(Base):
    @override
    def method(self) -> int:
        return super().method() + 1
",
            ["main.spy"] = @"
from derived import Derived

def main():
    d = Derived()
    print(d.method())
"
        };

        // Act
        var result = CompileProject(files);

        // Assert
        Assert.True(result.Success);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Pipeline_StopsOnMaxErrors()
    {
        // Arrange: Code with many errors
        var code = GenerateCodeWithManyErrors(200);
        var pipeline = ValidationPipelineFactory.CreateDefault();

        // Act
        var context = CreateContext();
        context.MaxErrors = 50;
        pipeline.Validate(ParseModule(code), context);

        // Assert: Should stop around max errors
        Assert.True(context.Diagnostics.ErrorCount <= 55); // Some tolerance
    }

    [Theory]
    [InlineData("cross_module_inheritance/interface_inheritance_chain")]
    [InlineData("cross_module_inheritance/three_level_class_inheritance")]
    [InlineData("module_imports/complex_type_relationships")]
    public void Pipeline_PassesExistingFixtures(string fixturePath)
    {
        // These are existing test fixtures that should pass
        var result = CompileFixture(fixturePath);
        Assert.True(result.Success, $"Fixture {fixturePath} failed: {string.Join(", ", result.Errors)}");
    }

    // Helper methods...
}
```

**Additional Test Cases:**

1. **Error deduplication** – Same error shouldn't appear twice
2. **Error location accuracy** – Line/column numbers are correct
3. **Cross-validator interaction** – One validator's state doesn't affect another
4. **Empty module** – Pipeline handles edge cases
5. **Syntax error recovery** – Pipeline doesn't crash on malformed AST

**Estimated Effort:** 4-6 hours  
**Risk:** Low (testing, not changing behavior)

---

#### Task 3.2: Centralize AST Traversal State Management

**Objective:** Ensure all V2 validators track state consistently.

**New File:** `src/Sharpy.Compiler/Semantic/Validation/AstTraversalContext.cs`

```csharp
namespace Sharpy.Compiler.Semantic.Validation;

/// <summary>
/// Tracks AST traversal state for validators.
/// Ensures consistent state management across all validators.
/// 
/// This is a step toward the visitor pattern mentioned in Rec #7,
/// which would fully centralize traversal in an immutable AST world.
/// </summary>
public class AstTraversalContext
{
    private readonly Stack<TypeSymbol?> _classStack = new();
    private readonly Stack<FunctionSymbol?> _functionStack = new();
    private readonly Stack<bool> _loopStack = new();

    public TypeSymbol? CurrentClass => _classStack.Count > 0 ? _classStack.Peek() : null;
    public FunctionSymbol? CurrentFunction => _functionStack.Count > 0 ? _functionStack.Peek() : null;
    public bool InLoop => _loopStack.Count > 0 && _loopStack.Peek();
    public int LoopDepth => _loopStack.Count(l => l);
    public int FunctionDepth => _functionStack.Count;

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

    public IDisposable EnterNonLoop()
    {
        _loopStack.Push(false);
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

**Update SemanticContext:**

```csharp
public class SemanticContext
{
    // ... existing fields ...

    /// <summary>
    /// Centralized AST traversal state.
    /// Validators should use this instead of tracking their own state.
    /// </summary>
    public AstTraversalContext Traversal { get; } = new();
}
```

**Update Validators to Use Centralized State:**

Example in `ControlFlowValidatorV2`:

```csharp
private void ValidateFunction(FunctionDef funcDef, SemanticContext context)
{
    var funcSymbol = context.SymbolTable.Lookup(funcDef.Name) as FunctionSymbol;
    
    using (context.Traversal.EnterFunction(funcSymbol))
    {
        foreach (var stmt in funcDef.Body)
        {
            ValidateStatement(stmt, context);
        }
    }
}

private void ValidateWhile(WhileStatement whileStmt, SemanticContext context)
{
    ValidateExpression(whileStmt.Test, context);
    
    using (context.Traversal.EnterLoop())
    {
        foreach (var stmt in whileStmt.Body)
        {
            ValidateStatement(stmt, context);
        }
    }
}
```

**Implementation Steps:**

1. Create `AstTraversalContext` class
2. Add to `SemanticContext`
3. Update all V2 validators to use the centralized state
4. Remove duplicate state tracking from individual validators
5. Verify tests still pass

**Estimated Effort:** 3-4 hours  
**Risk:** Medium (changing state management)

---

#### Task 3.3: Add Validator Ordering Tests

**File:** `src/Sharpy.Compiler.Tests/Semantic/Validation/ValidationPipelineTests.cs`

Add tests to verify validator ordering is correct:

```csharp
[Fact]
public void DefaultPipeline_HasCorrectValidatorOrder()
{
    var pipeline = ValidationPipelineFactory.CreateDefault();
    var validators = pipeline.Validators.ToList();

    // Verify expected validators are present
    Assert.Contains(validators, v => v is SignatureValidatorV2);
    Assert.Contains(validators, v => v is DefaultParameterValidatorV2);
    Assert.Contains(validators, v => v is ControlFlowValidatorV2);
    Assert.Contains(validators, v => v is AccessValidatorV2);
    Assert.Contains(validators, v => v is ProtocolValidatorV2);
    Assert.Contains(validators, v => v is OperatorValidatorV2);

    // Verify ordering
    var orders = validators.Select(v => v.Order).ToList();
    Assert.Equal(orders.OrderBy(o => o).ToList(), orders);
}

[Fact]
public void DefaultPipeline_SignatureValidatorRunsFirst()
{
    var pipeline = ValidationPipelineFactory.CreateDefault();
    var first = pipeline.Validators.First();
    
    Assert.IsType<SignatureValidatorV2>(first);
}
```

**Estimated Effort:** 1 hour  
**Risk:** Low

---

## Implementation Schedule

| Week | Tasks | Deliverables |
|------|-------|--------------|
| **Week 1** | Phase 1 (Tasks 1.1-1.3) | Pipeline enabled by default, dual path removed |
| **Week 2** | Phase 2A (Tasks 2.1-2.3) | TypeInferenceService created and integrated |
| **Week 2** | Phase 2B (Tasks 2.4-2.6) | SignatureValidatorV2 complete |
| **Week 3** | Phase 3 (Tasks 3.1-3.3) | Integration tests, centralized state |

**Total Estimated Effort:** 20-28 hours

---

## Verification Checklist

Before considering this work complete:

- [ ] All 3415+ tests pass
- [ ] No `if (!_usePipeline)` branches remain in TypeChecker
- [ ] `ValidationPipelineFactory.CreateDefault()` includes all validators
- [ ] TypeInferenceService handles all operator/protocol type inference
- [ ] Legacy validator type-returning methods are marked `[Obsolete]`
- [ ] Integration tests cover multi-file projects
- [ ] Error messages are unchanged from user perspective
- [ ] No performance regression (benchmark if needed)

---

## Future Considerations

This work prepares for:

1. **CompilerServices Layer (Rec #5):** `TypeInferenceService` is the first service; others will follow the same pattern.

2. **CompilationUnit Model (Rec #4):** Clean validation enables per-file compilation units with their own diagnostics.

3. **Parallel Compilation (Addendum #7-8):** Stateless validators and centralized state enable thread-safe execution.

4. **LSP Integration (Addendum #11-12):** Pipeline can be re-run incrementally; error-tolerant parsing can be added later.

---

## Appendix A: File Change Summary

| File | Change Type | Description |
|------|-------------|-------------|
| `TypeChecker.cs` | Modify | Default pipeline, remove dual path, integrate TypeInferenceService |
| `TypeChecker.Expressions.cs` | Modify | Use TypeInferenceService for operator/protocol type inference |
| `ValidationPipelineFactory.cs` | Modify | Add SignatureValidatorV2 |
| `SemanticContext.cs` | Modify | Add AstTraversalContext |
| `NameResolver.cs` | Modify | Remove signature validation (keep registration) |
| `OperatorValidator.cs` | Modify | Add [Obsolete] to type-returning methods |
| `ProtocolValidator.cs` | Modify | Add [Obsolete] to type-returning methods |
| `TypeInferenceService.cs` | **New** | Type inference for operators/protocols |
| `SignatureValidatorV2.cs` | **New** | V2 wrapper for signature validation |
| `AstTraversalContext.cs` | **New** | Centralized traversal state |
| `ValidationPipelineIntegrationTests.cs` | **New** | Integration tests |

---

## Appendix B: Rollback Plan

If issues arise after deployment:

1. **Revert to Legacy Mode:**
   - Change `_usePipeline = true` back to `_usePipeline = validationPipeline != null`
   - Revert `TypeChecker` constructor to not create default pipeline

2. **Keep TypeInferenceService:**
   - The service can coexist with legacy validators
   - Gradually migrate callers

3. **Feature Flag Option:**
   - Add `CompilerOptions.UseValidationPipeline` flag
   - Allow per-compilation control

The modular design ensures individual tasks can be reverted independently if needed.
