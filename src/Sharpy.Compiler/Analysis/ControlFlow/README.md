# Control Flow Graph (CFG) Infrastructure

## Overview

The CFG infrastructure provides control flow analysis capabilities for the Sharpy compiler. It is used for:

- **Return path analysis**: Ensuring functions return values in all code paths
- **Unreachable code detection**: Identifying code after return/raise/break/continue
- **Loop control validation**: Ensuring break/continue are only inside loops
- **Future: Async state machine generation**: Splitting code at await boundaries
- **Future: Pattern matching exhaustiveness**: Analyzing all possible execution paths

## Architecture

```
┌─────────────────────┐
│   FunctionDef AST   │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ ControlFlowGraph    │
│      Builder        │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│  ControlFlowGraph   │
│  - Entry/Exit       │
│  - BasicBlocks      │
│  - Terminators      │
└─────────┬───────────┘
          │
          ▼
┌─────────────────────┐
│ ControlFlowAnalysis │
│  - FindMissing      │
│  - Reachability     │
│  - LoopValidation   │
└─────────────────────┘
```

## Key Classes

- **BasicBlock**: A sequence of statements with single entry/exit
- **BlockTerminator**: Describes how control leaves a block (branch, return, throw, etc.)
- **ControlFlowGraph**: The complete CFG with entry/exit blocks
- **ControlFlowGraphBuilder**: Constructs CFG from AST
- **ControlFlowAnalysis**: Analysis utilities

## Usage Example

```csharp
var builder = new ControlFlowGraphBuilder();
var cfg = builder.Build(functionDef);

// Check return paths
var missingReturns = ControlFlowAnalysis.FindMissingReturnPaths(cfg);
if (missingReturns.Length > 0)
{
    // Report error: function doesn't return in all paths
}

// Check for unreachable code
var unreachable = ControlFlowAnalysis.FindUnreachableCode(cfg);
foreach (var info in unreachable)
{
    // Report warning: unreachable code
}
```

## Future Enhancements

1. **Async/Await Support**: Identify await boundaries for state machine generation
2. **Pattern Matching**: Exhaustiveness checking for match statements
3. **Definite Assignment**: Ensure variables are assigned before use
4. **Data Flow Analysis**: Track variable values through the CFG
