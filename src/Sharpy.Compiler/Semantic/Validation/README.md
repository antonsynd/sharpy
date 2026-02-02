# Semantic Validation

This directory contains the validation pipeline for semantic analysis.

## Overview

Sharpy uses a pluggable validation pipeline that runs after basic type checking.
Each validator implements `ISemanticValidator` and handles a specific category
of semantic rules.

## Key Files

- `ValidationPipeline.cs` - Orchestrates validators in sequence
- `ValidationPipelineFactory.cs` - Creates configured pipelines
- `ISemanticValidator.cs` - Interface for all validators
- `SemanticContext.cs` - Shared context passed to validators
- `AstTraversalContext.cs` - Tracks traversal state

### Validators

- `ModuleLevelValidator.cs` - Entry point rules, module-level type annotations
- `DecoratorValidator.cs` - Decorator usage validation
- `SignatureValidator.cs` - Function/method signature checks (dunders, protocols)
- `DefaultParameterValidator.cs` - Default parameter constraints
- `ControlFlowValidatorV3.cs` - CFG-based control flow analysis
- `AccessValidator.cs` - Member access validation
- `ProtocolValidator.cs` - Protocol method validation (__len__, __iter__, etc.)
- `OperatorValidator.cs` - Binary/unary operator type checking

## Architecture

```
TypeChecker
    |
ValidationPipeline
    |
[Validator1] -> [Validator2] -> [Validator3] -> ...
    |
DiagnosticBag (errors/warnings)
```

Each validator:
1. Receives `SemanticContext` with symbol table and type info
2. Traverses relevant AST nodes
3. Reports errors via `DiagnosticBag`
4. Returns success/failure

## Validation Responsibility Split

Semantic validation is split between **TypeChecker** and the **ValidationPipeline**.
The split is based on whether a validation rule needs type inference context or can
operate as a self-contained AST analysis.

### TypeChecker (type-inference-coupled)

These validations live in `TypeChecker` because they are tightly coupled to the
type inference walk and depend on in-progress type resolution state:

- **Type mismatches** — assignment/return type compatibility (SHP0220, SHP0221)
- **Callable validation** — argument count/type checking (SHP0224, SHP0225)
- **`super()` calls** — inheritance chain validation
- **Enum/struct rules** — member access, construction patterns
- **Return type checking** — function return type consistency
- **Override validation** — method signature compatibility with base class
- **Operator type checking** — binary/unary operator applicability (SHP0222, SHP0223)

### ValidationPipeline (self-contained AST analyses)

These validations run after type checking as independent AST passes.
They do not require in-progress type inference state:

- **Module-level rules** — entry point validation, top-level type annotations (ModuleLevelValidator)
- **Decorator usage** — valid decorator targets and known decorators (DecoratorValidator)
- **Signature checks** — dunder method signatures, protocol conformance (SignatureValidator)
- **Default parameters** — mutable defaults, non-constant defaults (DefaultParameterValidator)
- **Control flow** — unreachable code, missing returns, break/continue outside loops (ControlFlowValidatorV3)
- **Member access** — private member access from outside class (AccessValidator)
- **Protocol methods** — __len__/__iter__/etc. signature validation (ProtocolValidator)
- **Operator validation** — unsupported operators for known types (OperatorValidator, SHP0402)

### Deduplication

Some validations overlap between TypeChecker and the ValidationPipeline—notably
operator errors where TypeChecker reports SHP0222 (InvalidBinaryOperation) and
OperatorValidator reports SHP0402 (UnsupportedOperator) for the same expression.

Deduplication is handled in `TypeChecker.CheckModule()` when merging pipeline
diagnostics. The merge logic:

1. Takes a snapshot of existing diagnostics before running the pipeline
2. After the pipeline runs, collects only newly-added diagnostics
3. Builds a set of positions `(line, column)` from existing operator errors
4. Skips pipeline-added operator errors whose position matches an existing error

This position-based deduplication ensures users see only one error per problematic
operator expression, regardless of whether it was caught by TypeChecker or the pipeline.

## Adding a New Validator

1. Create `MyValidator.cs` implementing `ISemanticValidator`
2. Register in `ValidationPipelineFactory.CreateDefault()`
3. Add tests in `Sharpy.Compiler.Tests/Semantic/`

```csharp
public class MyValidator : ISemanticValidator
{
    public string Name => "MyValidator";

    public bool Validate(SemanticContext context, DiagnosticBag diagnostics)
    {
        // Traverse AST, check rules, report errors
        return !diagnostics.HasErrors;
    }
}
```

## Design Notes

- Validators run after `TypeChecker.CheckModule()` completes
- Order matters: control flow runs after type checking
- Validators use the unified `SemanticContext`
- ControlFlowValidatorV3 uses CFG analysis for more precise control flow checking
