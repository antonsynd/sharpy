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

### Validators (V2 = current generation)

- `ModuleLevelValidatorV2.cs` - Entry point rules, module-level type annotations
- `DecoratorValidatorV2.cs` - Decorator usage validation
- `SignatureValidatorV2.cs` - Function/method signature checks (dunders, protocols)
- `DefaultParameterValidatorV2.cs` - Default parameter constraints
- `ControlFlowValidatorV2.cs` / `V3.cs` - Control flow analysis
- `AccessValidatorV2.cs` - Member access validation
- `ProtocolValidatorV2.cs` - Protocol method validation (__len__, __iter__, etc.)
- `OperatorValidatorV2.cs` - Binary/unary operator type checking

## Architecture

```
TypeChecker
    ↓
ValidationPipeline
    ↓
[Validator1] → [Validator2] → [Validator3] → ...
    ↓
DiagnosticBag (errors/warnings)
```

Each validator:
1. Receives `SemanticContext` with symbol table and type info
2. Traverses relevant AST nodes
3. Reports errors via `DiagnosticBag`
4. Returns success/failure

## Adding a New Validator

1. Create `MyValidatorV2.cs` implementing `ISemanticValidator`
2. Register in `ValidationPipelineFactory.CreateDefault()`
3. Add tests in `Sharpy.Compiler.Tests/Semantic/`

```csharp
public class MyValidatorV2 : ISemanticValidator
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
- V2 validators use the unified `SemanticContext`
- V3 validators add additional capabilities (e.g., CFG analysis)
