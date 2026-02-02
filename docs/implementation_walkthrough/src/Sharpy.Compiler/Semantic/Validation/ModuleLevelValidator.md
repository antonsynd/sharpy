# Walkthrough: ModuleLevelValidator.cs

**Source File**: `src/Sharpy.Compiler/Semantic/Validation/ModuleLevelValidator.cs`

---

## Overview

`ModuleLevelValidator` is an early-stage semantic validator that enforces structural rules for module-level code in Sharpy. It acts as a gatekeeper, catching violations of module organization rules before other validators attempt to process potentially invalid code.

**Role in Pipeline**: This validator runs at **Order 50**, which is very early in the validation pipeline (before signature validation at Order 150). This early execution ensures that downstream validators can assume well-formed module structure.

**Three Core Rules**:
1. Entry point files should have a `main()` function (currently advisory, not enforced)
2. Module-level variable declarations **must** have explicit type annotations (e.g., `x: int = 42`)
3. Bare executable statements (like `print("hello")`, `x = 5`, `if` statements) are **not allowed** at module level

**Why These Rules Exist**:
- **Type annotations**: Without them, the compiler can't determine the static type of module-level variables, breaking type safety
- **No executable statements**: Sharpy compiles to C# classes, and C# doesn't allow executable code at class level. All executable code must be inside methods
- **main() function**: Provides a clear, explicit entry point (though the compiler can synthesize one for backward compatibility)

---

## Class Structure

```csharp
public class ModuleLevelValidator : SemanticValidatorBase
```

### Inheritance

Inherits from `SemanticValidatorBase`, which provides:
- The `ISemanticValidator` interface implementation
- Helper methods: `AddError()` and `AddWarning()` for reporting diagnostics
- Standard pattern for all validators in the pipeline

### Properties

```csharp
public override string Name => "ModuleLevelValidator";
public override int Order => 50;
```

- **Name**: Identifier used for logging and debugging
- **Order**: Execution priority (50 = very early, runs before most validators)

### Fields

```csharp
private ICompilerLogger _logger = NullLogger.Instance;
private SemanticContext _context = null!;
```

- **_logger**: Logging interface for debug output (set during `Validate()`)
- **_context**: Shared semantic context containing symbols, types, and diagnostics (set during `Validate()`)

The `null!` annotation indicates these will be initialized before use (in `Validate()`), telling the compiler to suppress null warnings.

---

## Key Method: Validate()

This is the entry point called by the `ValidationPipeline`. Let's walk through it step-by-step.

### Signature

```csharp
public override void Validate(Module module, SemanticContext context)
```

**Parameters**:
- `module`: The parsed AST (Abstract Syntax Tree) from the Parser
- `context`: Shared state containing symbol tables, type information, and the diagnostic bag for collecting errors

### Implementation Walkthrough

#### Phase 1: Initialization (Lines 25-31)

```csharp
_context = context;
_logger = context.Logger;
_logger.LogDebug("Starting module-level validation");

bool hasMainFunction = false;
var executableStatements = new List<Statement>();
var untypedVariables = new List<VariableDeclaration>();
```

The validator stores the context and logger, then initializes three tracking collections:
- **hasMainFunction**: Flag to detect presence of `main()`
- **executableStatements**: Collects forbidden executable code at module level
- **untypedVariables**: Collects variable declarations missing type annotations

#### Phase 2: AST Traversal and Categorization (Lines 34-83)

```csharp
foreach (var stmt in module.Body)
{
    switch (stmt)
    {
        case FunctionDef funcDef when funcDef.Name == "main":
            hasMainFunction = true;
            break;

        case FunctionDef:
        case ClassDef:
        case StructDef:
        case InterfaceDef:
        case EnumDef:
        case TypeAlias:
        case ImportStatement:
        case FromImportStatement:
            // These are valid module-level declarations
            break;

        case VariableDeclaration varDecl:
            if (varDecl.Type == null && !varDecl.IsConst)
            {
                untypedVariables.Add(varDecl);
            }
            break;

        case ExpressionStatement:
        case Assignment:
        case IfStatement:
        case WhileStatement:
        // ... other executable statement types
            executableStatements.Add(stmt);
            break;

        default:
            // Unknown statement type - treat as executable (conservative)
            executableStatements.Add(stmt);
            break;
    }
}
```

**Pattern Matching Strategy**:

1. **Check for `main()` function**: Uses a guard clause (`when funcDef.Name == "main"`) to identify the entry point
2. **Allow declarations**: Functions, classes, structs, interfaces, enums, type aliases, and imports are always valid
3. **Check variable declarations**:
   - Requires type annotation (`varDecl.Type != null`)
   - **Exception**: Constants (`varDecl.IsConst`) can infer types from their initializers
4. **Flag executable statements**: Any statement that performs runtime actions (assignments, control flow, function calls)
5. **Conservative fallback**: Unknown statement types are treated as executable (fail-safe approach)

**Key Distinction**:
- `x: int = 42` is a `VariableDeclaration` ✓ (allowed with type annotation)
- `x = 42` is an `Assignment` statement ✗ (executable, not allowed)

#### Phase 3: Report Untyped Variables (Lines 86-91)

```csharp
foreach (var varDecl in untypedVariables)
{
    AddError(_context,
        $"Top-level variable '{varDecl.Name}' requires a type annotation",
        varDecl.LineStart, varDecl.ColumnStart);
}
```

Every module-level variable without a type annotation gets an error. This ensures the compiler knows the static type of all module-level state.

**Example Error**:
```python
# This code:
counter = 0  # Missing type annotation

# Produces:
# Error at line 1, col 0: Top-level variable 'counter' requires a type annotation

# Fix:
counter: int = 0
```

#### Phase 4: Conditional Rejection of Executable Statements (Lines 93-109)

This is the most nuanced part of the validator:

```csharp
bool shouldRejectExecutableStatements = hasMainFunction || !_context.IsEntryPoint;

if (shouldRejectExecutableStatements && executableStatements.Count > 0)
{
    foreach (var stmt in executableStatements)
    {
        AddError(_context,
            "Executable statements are not allowed at module level",
            stmt.LineStart, stmt.ColumnStart);
    }
}
```

**Decision Logic**:

| Scenario | Has `main()`? | Is Entry Point? | Reject Executables? | Rationale |
|----------|---------------|-----------------|---------------------|-----------|
| Entry point with `main()` | ✓ | ✓ | **YES** | Code should be inside `main()`, not scattered at module level |
| Entry point without `main()` | ✗ | ✓ | **NO** | Backward compatibility: compiler will synthesize `Main()` and wrap statements |
| Library module | N/A | ✗ | **YES** | Library code can't have executable statements (they'd never run) |

**Why the complexity?**

- **Entry points with `main()`**: If the user defines `main()`, they're using the modern pattern. Bare statements would be confusing—should they run before or after `main()`? Best to require everything inside `main()`.

- **Entry points without `main()`**: For backward compatibility with older Sharpy code that looks like Python scripts. The code generator will automatically wrap these statements in a synthesized `Main()` method.

- **Library modules**: These modules will be imported by other code. Executable statements at module level make no sense—when would they run? This is always an error.

#### Phase 5: Entry Point Guidance (Lines 111-124)

```csharp
// Entry point files should have a main() function (warning-level guidance)
// Note: We only provide guidance for entry point files (context.IsEntryPoint = true)
// For now, this is not a hard error for backward compatibility
// Uncomment below to enable strict enforcement:
// if (_context.IsEntryPoint && !hasMainFunction)
// {
//     if (executableStatements.Count == 0 && untypedVariables.Count == 0)
//     {
//         AddError(_context,
//             "Entry point file requires a 'main()' function",
//             module.LineStart, module.ColumnStart);
//     }
// }
```

**Currently Disabled**: This commented-out code would enforce the rule "entry points MUST have a `main()` function" strictly. It's disabled for backward compatibility with existing Sharpy code.

**When Enabled**: This would catch entry point files that have only declarations (no executable code, no `main()` function), which are likely mistakes.

---

## Dependencies

### Internal Dependencies

1. **Parser AST** (`Sharpy.Compiler.Parser.Ast`)
   - `Module`: Root AST node containing `Body` (list of statements)
   - Statement types: `FunctionDef`, `ClassDef`, `VariableDeclaration`, `Assignment`, `IfStatement`, etc.
   - Each statement has `LineStart` and `ColumnStart` for error reporting

2. **Logging** (`Sharpy.Compiler.Logging`)
   - `ICompilerLogger`: Interface for debug/info logging
   - `NullLogger`: No-op logger for when logging is disabled

3. **Semantic Infrastructure**
   - `SemanticValidatorBase`: Base class providing helper methods
   - `SemanticContext`: Shared state across all validators
     - `IsEntryPoint`: Boolean flag indicating if this is the main executable file
     - `Diagnostics`: Diagnostic bag for collecting errors/warnings
     - `Logger`: Logger instance

### Upstream Dependencies

- **Lexer**: Tokenizes source code (runs first)
- **Parser**: Builds AST from tokens (runs second)
- This validator operates on the AST produced by the Parser

### Downstream Impact

- **Other Validators**: Can assume module-level code is well-formed (no bare executables, all variables are typed)
- **CodeGen (RoslynEmitter)**: Can safely emit C# code knowing module structure is valid

---

## Patterns and Design Decisions

### 1. Single-Pass Traversal

The validator makes a single pass over `module.Body`, categorizing statements as it goes. This is efficient and straightforward.

**Alternative Considered**: Multi-pass approach (first find `main()`, then check statements). Rejected because single-pass is simpler and sufficient.

### 2. Conservative Unknown Statement Handling

```csharp
default:
    // Unknown statement type - treat as executable
    executableStatements.Add(stmt);
    break;
```

When encountering an unknown statement type, the validator treats it as executable (and thus invalid at module level). This is a **fail-safe** design: if new statement types are added to the AST, they'll be caught here rather than silently allowed.

### 3. Separation of Concerns

The validator doesn't:
- Resolve names (that's `NameResolver`'s job)
- Check types (that's `TypeChecker`'s job)
- Generate code (that's `RoslynEmitter`'s job)

It only validates **structure**: what kinds of statements are allowed where.

### 4. Backward Compatibility Strategy

The validator balances modern best practices (requiring `main()`) with backward compatibility (allowing bare statements in entry points without `main()`).

**Migration Path**:
1. **Current**: Soft enforcement, warnings only
2. **Future**: Uncomment lines 115-123 to make `main()` required
3. **Eventually**: Remove synthesized `Main()` generation from code generator

### 5. Early Execution Order

```csharp
public override int Order => 50;
```

By running early (Order 50), this validator acts as a **structural firewall**. It prevents later validators from encountering invalid module structures, which could cause crashes or confusing error messages.

**Order Hierarchy** (typical):
- 50: Module-level validation (this validator)
- 100: Name resolution
- 150: Signature validation
- 200: Type resolution
- 300: Type checking

---

## Debugging Tips

### 1. Check Validator Execution Order

If module-level errors aren't being caught, verify the validator is registered in the pipeline:

```csharp
// In ValidationPipelineFactory or similar
pipeline.AddValidator(new ModuleLevelValidator());
```

### 2. Enable Debug Logging

Set the logger to debug level to see validation progress:

```csharp
_logger.LogDebug("Starting module-level validation");
```

Output will show:
- When the validator starts
- Which statements are being categorized
- How many errors were found

### 3. Common Failure Modes

**"Why isn't my variable declaration being caught?"**
- Check if it's parsed as `VariableDeclaration` vs `Assignment`
- `x: int = 5` is a declaration ✓
- `x = 5` is an assignment ✗

**"Why is my const being flagged?"**
- Constants are exempt from type annotation requirements (line 55: `!varDecl.IsConst`)
- `const MAX = 100` is allowed

**"Why isn't my entry point requiring main()?"**
- The strict enforcement is currently disabled (lines 115-123 are commented out)
- This is intentional for backward compatibility

### 4. Testing Specific Scenarios

Use the test file `ModuleLevelValidatorTests.cs` as a reference for expected behavior. Key test categories:

- **Entry point rules**: `EntryPointWithMain_NoErrors()`, `EntryPointWithoutMain_NoErrorForBackwardCompatibility()`
- **Type annotations**: `ModuleLevelWithTypeAnnotation_NoErrors()`, `ModuleLevelConstWithoutTypeAnnotation_NoErrors()`
- **Executable statements**: `ModuleLevelPrint_Error()`, `ModuleLevelForLoop_Error()`
- **Valid declarations**: `ClassDefinition_NoErrors()`, `StructDefinition_NoErrors()`

### 5. Interpreting Error Messages

**"Top-level variable 'x' requires a type annotation"**
- Fix: Add a type annotation: `x: int = 5`

**"Executable statements are not allowed at module level"**
- If you have `main()`: Move the statement inside `main()`
- If it's a variable: Change from `x = 5` to `x: int = 5`
- If it's a library module: Remove the executable statement entirely

---

## Contribution Guidelines

### When to Modify This File

1. **Adding new statement types to the AST**
   - Update the `switch` statement to categorize the new type
   - Add tests to verify behavior

2. **Changing module-level rules**
   - Example: Requiring `main()` in all entry points
   - Uncomment lines 115-123 and add tests

3. **Improving error messages**
   - Make messages more specific or actionable
   - Include suggestions for fixes

4. **Supporting new language features**
   - Example: Module-level decorators, module-level expressions
   - Evaluate if they should be allowed and update categorization logic

### What NOT to Change

1. **Don't add type checking logic here**
   - Type checking belongs in `TypeChecker` or specialized validators
   - This validator should only check structure

2. **Don't modify the Order value without careful consideration**
   - Order 50 ensures this runs before name resolution
   - Changing it could break the pipeline

3. **Don't make it more lenient without team discussion**
   - These rules enforce critical constraints for C# code generation
   - Relaxing them could break downstream components

### Adding New Module-Level Rules

If you need to add a new module-level rule:

1. **Consider if it belongs here**
   - Is it about structure (what statements are allowed)?
   - Or is it about semantics (what those statements mean)?
   - Structure → This validator
   - Semantics → Different validator

2. **Add tracking during traversal**
   ```csharp
   var newRuleViolations = new List<Statement>();
   ```

3. **Categorize during switch statement**
   ```csharp
   case NewStatementType:
       newRuleViolations.Add(stmt);
       break;
   ```

4. **Report errors after traversal**
   ```csharp
   foreach (var stmt in newRuleViolations)
   {
       AddError(_context, "Description of rule violation",
                stmt.LineStart, stmt.ColumnStart);
   }
   ```

5. **Add comprehensive tests**
   - Valid cases (no errors)
   - Invalid cases (errors reported)
   - Edge cases (empty files, etc.)

### Testing Changes

Always add tests to `ModuleLevelValidatorTests.cs`:

```csharp
[Fact]
public void YourNewRule_ValidCase_NoErrors()
{
    var code = @"
// Valid code here
";
    var (module, context) = Parse(code, isEntryPoint: true);
    var validator = new ModuleLevelValidator();
    validator.Validate(module, context);

    Assert.False(context.Diagnostics.HasErrors);
}

[Fact]
public void YourNewRule_InvalidCase_Error()
{
    var code = @"
// Invalid code here
";
    var (module, context) = Parse(code, isEntryPoint: true);
    var validator = new ModuleLevelValidator();
    validator.Validate(module, context);

    Assert.True(context.Diagnostics.HasErrors);
    Assert.Contains(context.Diagnostics.GetErrors(),
        e => e.Message.Contains("expected error message"));
}
```

---

## Cross-References

### Related Validators

- **`SignatureValidator.cs`** (Order 150): Validates function signatures after module structure is confirmed valid
- **`ControlFlowValidator.cs`**: Validates control flow statements (runs later in pipeline)
- **`ISemanticValidator.cs`**: Base interface and abstract class definition

### Related Pipeline Components

- **`ValidationPipeline.cs`**: Orchestrates all validators, runs them in order
- **`ValidationPipelineFactory.cs`**: Registers and configures validators
- **Parser (`Parser.cs`)**: Produces the AST this validator operates on
- **CodeGen (`RoslynEmitter.cs`)**: Consumes validated AST to generate C#

### Related Semantic Components

- **`SemanticContext.cs`**: Shared state container for all validators
- **`SymbolTable.cs`**: Stores resolved symbols (used by later validators)
- **`TypeChecker.cs`**: Validates types (runs after this validator)

### Specification Documents

- **`docs/language_specification/module_system.md`**: Defines module structure and import rules
- **`docs/language_specification/module_resolution.md`**: Name conversion rules for modules

### Test Files

- **`src/Sharpy.Compiler.Tests/Semantic/Validation/ModuleLevelValidatorTests.cs`**: Comprehensive test suite
- **`src/Sharpy.Compiler.Tests/Semantic/Validation/ValidationPipelineTests.cs`**: Integration tests showing validator pipeline behavior

---

## Example Usage

Here's how the validator is used in practice:

```csharp
// Build the validation pipeline
var pipeline = new ValidationPipeline(logger);
pipeline.AddValidator(new ModuleLevelValidator());
pipeline.AddValidator(new SignatureValidator());
// ... other validators

// Create semantic context
var context = new SemanticContext(symbolTable, semanticInfo, typeResolver)
{
    IsEntryPoint = true  // This is the main executable file
};

// Run validation
var diagnostics = pipeline.Validate(module, context);

// Check results
if (diagnostics.HasErrors)
{
    foreach (var error in diagnostics.GetErrors())
    {
        Console.WriteLine($"Error at {error.Line}:{error.Column}: {error.Message}");
    }
}
```

### Example: Valid Entry Point

```python
# game.spy (entry point file)

# Module-level variable with type annotation
score: int = 0

# Class definition (allowed)
class Player:
    name: str
    health: int

# Entry point function
def main():
    player: Player = Player()
    player.name = "Hero"
    player.health = 100
    print(f"Player: {player.name}")
```

✓ **Passes validation**: Has `main()`, typed variable, no bare executables

### Example: Invalid Entry Point

```python
# bad_game.spy (entry point file)

# ERROR: Missing type annotation
score = 0

# Class is fine
class Player:
    name: str

# ERROR: Bare executable statement
print("Starting game...")

def main():
    pass
```

✗ **Fails validation**:
- Line 3: "Top-level variable 'score' requires a type annotation"
- Line 10: "Executable statements are not allowed at module level"

### Example: Library Module

```python
# utils.spy (library module, not an entry point)

# Constants can omit type annotations
const MAX_RETRIES = 3

# Helper function (allowed)
def retry(operation: callable) -> any:
    for i in range(MAX_RETRIES):
        try:
            return operation()
        except:
            continue
    raise Exception("Max retries exceeded")
```

✓ **Passes validation**: Library modules can have declarations, but no executable statements

---

## Frequently Asked Questions

### Q: Why can't I have bare executable statements at module level?

**A**: Sharpy compiles to C# classes. In C#, you can't have executable code directly in a class body—it must be inside methods. The `main()` function becomes a static method, and all executable code must be inside it.

```csharp
// Generated C# (simplified)
public static class MyModule
{
    // Module-level variables become static fields
    public static int Counter = 0;

    // You CAN'T do this in C#:
    // Console.WriteLine("Hello");  // ← Not allowed at class level!

    // You MUST do this:
    public static void Main()
    {
        Console.WriteLine("Hello");  // ← Must be inside a method
    }
}
```

### Q: Why do module-level variables need type annotations?

**A**: Without type annotations, the compiler can't determine the static type. In functions, we can infer types from usage, but module-level variables are initialized before any code runs, so there's no usage context to infer from.

```python
# Without type annotation:
x = 42  # Is this int? float? any?

# With type annotation:
x: int = 42  # Clearly an int
```

### Q: Why are constants exempt from type annotations?

**A**: Constants have literal initializers, and literals have unambiguous types:

```python
const MAX = 100      # Clearly int (integer literal)
const NAME = "app"   # Clearly str (string literal)
const PI = 3.14      # Clearly float (float literal)
```

The type can be safely inferred from the literal value.

### Q: What happens if I don't have a `main()` function in an entry point?

**A**: Currently, the compiler synthesizes a `Main()` method and wraps any bare statements inside it (for backward compatibility). However, this behavior may be deprecated in the future—it's best practice to always define an explicit `main()` function.

### Q: Can I have a `main()` function in a library module?

**A**: Yes, but it won't be called. Only the entry point file's `main()` becomes the executable entry point. Library modules can have a `main()` for testing purposes, but it won't run automatically.

### Q: Why Order 50?

**A**: The validator needs to run before name resolution and type checking, but it doesn't need to be the absolute first validator. Order 50 leaves room for validators that might need to run even earlier (e.g., syntax tree transformations at Order 10-40) while ensuring structural validation happens before semantic analysis.

---

## Summary

`ModuleLevelValidator` is a **structural firewall** that ensures Sharpy modules conform to the constraints imposed by C# code generation. By running early in the validation pipeline (Order 50), it catches violations before they can confuse downstream validators or the code generator.

**Key Takeaways**:
- Enforces three critical rules: typed variables, no bare executables, and (advisorily) `main()` functions
- Uses single-pass traversal with pattern matching for efficiency
- Balances modern best practices with backward compatibility
- Serves as a foundation that other validators can rely on
- Simple, focused design: validates structure, not semantics

When working with this validator, remember: **Structure first, semantics later**. This validator ensures the "shape" of the code is correct, allowing other validators to focus on the "meaning".
