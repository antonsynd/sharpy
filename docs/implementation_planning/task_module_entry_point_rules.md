# Task: Module Entry Point Rules Overhaul

**Created:** 2025-01-23
**Priority:** High
**Estimated Effort:** 3-5 days

## Summary

This task implements new entry point rules to resolve the syntactic ambiguity between module-level static members and executable statements. The key changes are:

1. **Entry point files MUST define `def main():`** — No implicit `Main()` from bare statements
2. **Top-level variable declarations require type annotations** — They become static fields
3. **Bare executable statements at module level are errors** — No `print()`, function calls, etc. outside `main()`

## Background

Previously, statements like `x = 5` at module level were ambiguous:
- Could be a static field of the generated module class
- Could be executable code inside an implicit `Main()`

The new rules eliminate this ambiguity while maintaining Pythonic ergonomics inside functions.

## Design Summary

| Location | Construct | Allowed? | Becomes |
|----------|-----------|----------|---------|
| Module level | `name: Type = value` | ✅ | Static field |
| Module level | `const NAME: Type = value` | ✅ | Static const |
| Module level | `name = value` (no annotation) | ❌ Error | — |
| Module level | `print(x)` (bare expression) | ❌ Error | — |
| Module level | `func()` (bare call) | ❌ Error | — |
| Module level | `def func(): ...` | ✅ | Static method |
| Module level | `class Foo: ...` | ✅ | Nested class |
| Entry point | `def main(): ...` | Required | `Main()` |
| Inside functions | `name = value` | ✅ | Local variable (inferred) |

---

## Part 1: Language Specification Updates

### Task 1.1: Update `program_entry_point.md`

**File:** `docs/language_specification/program_entry_point.md`

**Current content describes two options (top-level statements OR main function). Replace with new spec.**

**New content:**

```markdown
# Program Entry Point

## Entry Point Function

Every executable Sharpy program requires a `main()` function as its entry point:

```python
def main():
    print("Hello, World!")
```

The `main()` function:
- Must be defined in the entry point file
- Takes no parameters (command-line args accessed via `system.environment`)
- Has an implicit `None` return type (can also be explicit: `def main() -> None:`)
- Is automatically invoked by the runtime

## Module-Level Declarations

Outside of `main()`, only declarations are allowed at module level:

```python
# ✅ Static fields (type annotation required)
counter: int = 0
config: str = "default"
items: list[int] = []

# ✅ Constants
const MAX_SIZE: int = 1000
const APP_NAME: str = "MyApp"

# ✅ Functions (become static methods)
def helper() -> int:
    return 42

# ✅ Classes, structs, enums, interfaces
class Point:
    x: int
    y: int

# ✅ Function calls in static initializers are allowed
data: str = load_config()  # OK: function call as initializer

# ❌ NOT allowed: bare executable statements
print("loading...")     # ERROR: not allowed at module level
helper()                # ERROR: not allowed at module level
x = 5                   # ERROR: requires type annotation
```

## Complete Example

```python
# app.spy - Entry point file

# Static members (type annotation required)
counter: int = 0
const VERSION: str = "1.0.0"

def increment() -> None:
    counter = counter + 1

class Config:
    debug: bool = False

def main():
    # Inside main(), type inference works normally
    message = f"Version {VERSION}"
    print(message)
    
    increment()
    print(counter)
```

## Non-Entry-Point Modules

Library modules (files that are imported, not executed directly) follow the same rules but do not require a `main()` function:

```python
# utils.spy - Library module

# Static field
call_count: int = 0

def utility() -> int:
    call_count = call_count + 1
    return call_count

# No main() needed - this module is imported, not executed
```

## Migration from Bare Statements

If you have existing code with top-level executable statements, wrap them in a `main()` function:

**Before (no longer valid):**
```python
x = 42
print(x)
result = compute(x)
print(result)
```

**After:**
```python
def main():
    x = 42
    print(x)
    result = compute(x)
    print(result)
```

*Implementation: ✅ Native*
- *`main()` compiles directly to C# `Main()` method*
- *Module-level declarations become static members of the module class*
```

---

### Task 1.2: Update `statements.md`

**File:** `docs/language_specification/statements.md`

**Add new section after "Variable Declaration and Assignment" section (around line 50):**

```markdown
## Module-Level Declaration Rules

At module level (outside any function or class), variable declarations have additional constraints:

### Type Annotation Required

Module-level variables MUST have explicit type annotations:

```python
# ✅ Valid module-level declarations
counter: int = 0
name: str = "default"
items: list[int] = []
data: Config? = None

# ❌ Invalid - no type annotation at module level
x = 42                  # ERROR: requires type annotation
name = "hello"          # ERROR: requires type annotation
```

**Rationale:** This rule eliminates ambiguity between static field declarations and executable statements, which would otherwise look identical syntactically.

### No Executable Statements

Bare expression statements are not allowed at module level:

```python
# ❌ NOT allowed at module level
print("hello")          # ERROR: executable statement not allowed
my_function()           # ERROR: executable statement not allowed
obj.method()            # ERROR: executable statement not allowed
1 + 2                   # ERROR: executable statement not allowed

# ✅ Move into main() or another function
def main():
    print("hello")      # OK inside function
    my_function()       # OK inside function
```

### Function Calls in Initializers

Function calls ARE allowed as part of a variable initializer:

```python
# ✅ Valid - function call is part of initialization
config: Config = load_config()
data: list[int] = generate_data()
timestamp: int = get_current_time()
```

### Inside Functions

Inside functions (including `main()`), type inference works normally:

```python
def main():
    x = 42              # ✅ OK - inferred as int
    name = "hello"      # ✅ OK - inferred as str
    result = compute()  # ✅ OK - inferred from return type
```
```

---

### Task 1.3: Update `phases.md` References

**File:** `docs/implementation_planning/phases.md`

**Search and update these sections:**

1. **Phase 0.1.1** (around line 109): Change "Support for top-level statements" to "Support for top-level declarations"

2. **Phase 0.1.2** (around line 172-174): Update entry point generation description:
   ```markdown
   1. **Entry Point Generation**
      - `main()` function required in entry point files
      - Module-level declarations become static fields
      - Generated class structure for module
   ```

3. Update any code examples in phases.md that show bare top-level statements.

---

## Part 2: Test Fixture Updates

### Task 2.1: Categorize Test Fixtures

**Directory:** `src/Sharpy.Compiler.Tests/Integration/TestFixtures/`

Based on search results, the following test files need updates:

#### Files WITHOUT `main()` (need main() added + move statements inside):

**basics/** (7 files):
- `hello_world.spy` - Has bare `print()`
- `arithmetic.spy` - Has `print()` calls at module level
- `type_inference.spy` - Has `x = 42` style declarations AND `print()` calls
- `comparison_operators.spy` - Likely has module-level statements
- `comparison_operators_0013.spy` - Likely has module-level statements
- `integer_variables_0002.spy` - Check for bare assignments
- `bool_variables_0003.spy` - Check for bare assignments
- `float_variables_0009.spy` - Check for bare assignments
- `arithmetic_operators.spy` - Check for module-level statements
- `augmented_assignment_*.spy` (4 files) - Check for module-level statements

**control_flow/** (6 files):
- `if_elif_else.spy` - Has function calls at module level
- `while_loop.spy` - Check for module-level statements
- `for_loop_range.spy` - Check for module-level statements
- `break_continue.spy` - Check for module-level statements
- `break_continue_0003.spy` - Check
- `for_range_with_step_0004.spy` - Check

**functions/** (8 files):
- `fibonacci_recursive.spy` - Has `print(result)` at module level
- `fibonacci_iterative.spy` - Check
- `default_params.spy` - Check
- `comparison_functions.spy` - Check
- `function_*.spy` (4 files) - Check for module-level statements

**classes/** (most files):
- `class_with_init.spy` (in `classes/` folder) - Has extensive module-level code
- `class_instance_methods*.spy` - Check
- `class_static_methods*.spy` - Check
- `class_*.spy` - Check all for module-level statements
- `inheritance_with_override.spy` - Check
- `augmented_assignment.spy` - Check
- `shape_calculator.spy` - Check
- `nested_if_in_loop.spy` - Check
- `bool_access_control.spy` - Check

**inheritance/** (10 files):
- `abstract_class_*.spy` - Check all
- `class_inheritance_0000.spy` - Check
- `super_init_call_*.spy` - Check all
- `virtual_override_*.spy` - Check all
- `super_grandparent_method.spy` - Check

**type_system/** (3 files):
- `generic_class_0005.spy` - Has module-level statements
- `null_conditional_0005.spy` - Check
- `null_coalescing_0003.spy` - Check

**structs_enums/** (1 file):
- `struct_definition_0001.spy` - Check

**structs/** (1 file):
- `struct_point_value_semantics.spy` - Check

**enums/** (3 files):
- `enum_*.spy` - Check all

**type_shorthand/** (1 file):
- `list_shorthand.spy` - Check

**interfaces/** (5 files - some already have main):
- `interface_implementation_*.spy` - Check each
- `interface_streamlined_0010.spy` - Check

#### Files WITH `main()` (only need annotation fixes if any):
- `interfaces/interface_method_return_type_0001.spy` ✅
- `interfaces/interface_generic_method_0002.spy` ✅
- `access_modifiers/access_modifiers.spy` ✅
- `class_with_init/class_with_init.spy` ✅
- `interface_definition/interface_definition.spy` ✅
- `generic_function/generic_function.spy` ✅
- `errors/main_function_with_call.spy` - Error test (needs update)
- `errors/main_function_with_statements.spy` - Error test (needs update)
- `cross_module_inheritance/*/main.spy` ✅ (4 files)
- `module_imports/*/main.spy` ✅ (3 files)
- `classes/class_inheritance.spy` ✅
- `imports/*/main.spy` ✅ (3 files)

#### Multi-file Test Suites (special handling):
These test suites have entry point files (`main.spy`) but non-entry-point modules:
- `cross_module_inheritance/*` - Check library modules for bare statements
- `module_imports/*` - Check library modules
- `imports/*` - Check library modules

---

### Task 2.2: Update Test Fixtures - Pattern

For each file requiring updates, apply this transformation:

**Before:**
```python
# some_test.spy
x = 42
y: int = 10
print(x + y)
result = compute(x)
print(result)
```

**After:**
```python
# some_test.spy
def main():
    x = 42
    y: int = 10
    print(x + y)
    result = compute(x)
    print(result)
```

**For files with classes/functions + executable code:**

**Before:**
```python
class Point:
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

p = Point(3, 4)
print(p.x)
```

**After:**
```python
class Point:
    x: int
    y: int
    
    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

def main():
    p = Point(3, 4)
    print(p.x)
```

---

### Task 2.3: Update Error Test Fixtures

**File:** `errors/main_function_with_statements.spy`

**Current behavior:** Errors because main() exists with other executable statements
**New behavior:** Will still error, but now ALSO because `y = helper()` lacks type annotation

Update the error test and expected output:

```python
# Error test: Module-level executable statements are not allowed

x: int = 42
print(x)  # ERROR: executable statement not allowed at module level

def helper() -> int:
    return 10

y = helper()  # ERROR: requires type annotation at module level

def main() -> None:
    print("In main")
```

**Expected errors (update `.error` file):**
- Line 4: Executable statements are not allowed at module level
- Line 9: Top-level variable 'y' requires a type annotation

---

**File:** `errors/main_function_with_call.spy`

**Current behavior:** Errors because main() is called explicitly
**New behavior:** Same error - executable statement at module level

```python
# Error test: main() cannot be called at module level

def main() -> None:
    print("Hello from main")

main()  # ERROR: executable statement not allowed at module level
```

---

### Task 2.4: Add New Error Test Fixtures

Create new error test cases:

**File:** `errors/missing_main_entry_point.spy`
```python
# Error test: Entry point file without main() function

x: int = 42

def helper() -> int:
    return x

# ERROR: Entry point file requires a 'main()' function
```

**File:** `errors/missing_main_entry_point.error`
```
Entry point file requires a 'main()' function
```

---

**File:** `errors/module_level_no_type_annotation.spy`
```python
# Error test: Module-level variable without type annotation

x = 42  # ERROR: requires type annotation

def main():
    print(x)
```

**File:** `errors/module_level_no_type_annotation.error`
```
Top-level variable 'x' requires a type annotation
```

---

**File:** `errors/module_level_executable_statement.spy`
```python
# Error test: Bare executable statement at module level

counter: int = 0

print("initializing")  # ERROR: not allowed

def main():
    print("running")
```

**File:** `errors/module_level_executable_statement.error`
```
Executable statements are not allowed at module level
```

---

## Part 3: Snippet and Sample Updates

### Task 3.1: Update Snippets

**Directory:** `snippets/`

Review and update all `.spy` files. Key files to check:

- `hello.spy` - Already has main() ✅
- `demo.spy` - Already has main() ✅
- `simple_example.spy` - Has module-level for loop and assignments, needs main()
- `type_inference.spy` (if exists) - Needs main() wrapper
- All other `.spy` files

### Task 3.2: Update Samples

**Directory:** `samples/`

- `type_system_showcase.spy` - Has main() but also calls `main()` at end, remove the call
- `dotnet_interop_example.spy` - Check and update

---

## Part 4: README and Documentation Updates

### Task 4.1: Update README.md

**File:** `README.md`

Update the "Language Highlights" section (around line 25):

**Current:**
```python
# Static typing with inference
x: int = 42           # Explicit type
y = 42                # Inferred as int
```

**Updated:**
```python
# Static typing with inference
def main():
    x: int = 42       # Explicit type
    y = 42            # Inferred as int inside functions

# Module-level requires type annotation
counter: int = 0      # Static field
```

Update the "Your First Program" section (around line 73):
```python
# hello.spy
def greet(name: str) -> str:
    return f"Hello, {name}!"

def main():
    message = greet("World")
    print(message)
```
(This already looks correct, verify it matches new spec)

### Task 4.2: Update CodeGen README

**File:** `src/Sharpy.Compiler/CodeGen/README.md`

Update line ~29:
```markdown
2. **Module-Level Declarations**
   - Entry point files require `main()` function
   - Module-level variables require type annotations
   - Top-level declarations become static fields
```

---

## Part 5: Compiler Implementation

### Task 5.1: Add Semantic Analysis Validation

**Location:** Create new validator or add to existing

**File:** `src/Sharpy.Compiler/Semantic/Validation/ModuleLevelValidator.cs` (new file)

```csharp
/// <summary>
/// Validates module-level declaration rules:
/// 1. Entry point files must have a main() function
/// 2. Module-level variables require type annotations
/// 3. Bare executable statements are not allowed at module level
/// </summary>
public class ModuleLevelValidator : IValidator
{
    public void Validate(SemanticContext context)
    {
        var module = context.Module;
        bool hasMainFunction = false;
        
        foreach (var stmt in module.Statements)
        {
            // Check for main function
            if (stmt is FunctionDef funcDef && funcDef.Name == "main")
            {
                hasMainFunction = true;
                continue;
            }
            
            // Allow: class, struct, interface, enum, function definitions
            if (stmt is ClassDef or StructDef or InterfaceDef or EnumDef or FunctionDef)
            {
                continue;
            }
            
            // Allow: import statements
            if (stmt is ImportStatement or FromImportStatement)
            {
                continue;
            }
            
            // Check variable declarations for type annotations
            if (stmt is VariableDeclaration varDecl)
            {
                if (varDecl.TypeAnnotation == null && !varDecl.IsConst)
                {
                    context.AddError(
                        $"Top-level variable '{varDecl.Name}' requires a type annotation",
                        varDecl.Span,
                        helpText: $"Add a type annotation: `{varDecl.Name}: <type> = ...`\n" +
                                  "Or move inside `def main():` if this is executable code"
                    );
                }
                continue;
            }
            
            // All other statements are errors at module level
            context.AddError(
                "Executable statements are not allowed at module level",
                stmt.Span,
                helpText: "Move this inside `def main():` to execute at program start"
            );
        }
        
        // Check for missing main() in entry point files
        if (context.IsEntryPoint && !hasMainFunction)
        {
            context.AddError(
                "Entry point file requires a 'main()' function",
                module.Span,
                helpText: "Add `def main():` to define the program entry point"
            );
        }
    }
}
```

### Task 5.2: Register Validator in Pipeline

**File:** `src/Sharpy.Compiler/Semantic/ValidationPipeline.cs`

Add the new validator to the pipeline (early, before other semantic checks):

```csharp
_validators.Insert(0, new ModuleLevelValidator());
```

### Task 5.3: Update RoslynEmitter

**File:** `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`

The emitter code that generates implicit Main() from executable statements needs to be updated. After the semantic analysis catches errors, the emitter should:

1. Remove the code that generates Main() from bare statements
2. Only emit Main() when there's an explicit `main()` function
3. Module-level VariableDeclarations become static fields (already works)

**Key changes around line 165-200:**

```csharp
// OLD: Generate a Main method if no user-defined main and this is entry point
// NEW: Only emit Main if there's an explicit main() function
if (hasMainFunction)
{
    // User defined main() - already handled as a regular function
    // It will be transformed to Main() by name mangling
}
else if (_context.IsEntryPoint)
{
    // Error case - should have been caught by ModuleLevelValidator
    // Entry point without main() is an error
    // Don't generate anything, errors will prevent compilation
}

// Remove the implicit Main generation from executableStatements
```

### Task 5.4: Update Error Messages

Ensure all error messages follow the format:

```
error: <message>
  --> <file>:<line>:<column>
   |
 N | <source line>
   | ^^^^^
   |
   = help: <suggestion>
```

---

## Part 6: Testing

### Task 6.1: Add Unit Tests for ModuleLevelValidator

**File:** `src/Sharpy.Compiler.Tests/Semantic/ModuleLevelValidatorTests.cs`

```csharp
[Fact]
public void EntryPoint_WithoutMain_ReportsError()
{
    var source = @"
x: int = 42
def helper() -> int:
    return x
";
    var errors = CompileWithErrors(source, isEntryPoint: true);
    Assert.Contains(errors, e => e.Contains("requires a 'main()' function"));
}

[Fact]
public void ModuleLevel_VariableWithoutTypeAnnotation_ReportsError()
{
    var source = @"
x = 42
def main():
    print(x)
";
    var errors = CompileWithErrors(source, isEntryPoint: true);
    Assert.Contains(errors, e => e.Contains("requires a type annotation"));
}

[Fact]
public void ModuleLevel_ExecutableStatement_ReportsError()
{
    var source = @"
counter: int = 0
print(counter)
def main():
    pass
";
    var errors = CompileWithErrors(source, isEntryPoint: true);
    Assert.Contains(errors, e => e.Contains("not allowed at module level"));
}

[Fact]
public void ModuleLevel_FunctionCallInInitializer_IsAllowed()
{
    var source = @"
def get_value() -> int:
    return 42

value: int = get_value()

def main():
    print(value)
";
    var errors = CompileWithErrors(source, isEntryPoint: true);
    Assert.Empty(errors);
}

[Fact]
public void NonEntryPoint_WithoutMain_IsAllowed()
{
    var source = @"
helper_count: int = 0

def utility() -> int:
    return helper_count
";
    var errors = CompileWithErrors(source, isEntryPoint: false);
    Assert.Empty(errors);
}
```

### Task 6.2: Update Existing Tests

Search for tests that rely on implicit Main() generation and update them:

```bash
grep -r "executableStatements" src/Sharpy.Compiler.Tests/
grep -r "IsEntryPoint" src/Sharpy.Compiler.Tests/
```

---

## Part 7: Verification Checklist

Before marking complete, verify:

- [ ] All language spec docs updated
- [ ] All test fixtures compile and pass
- [ ] All error test fixtures produce expected errors
- [ ] All snippets run correctly
- [ ] All samples run correctly
- [ ] README examples are accurate
- [ ] New unit tests pass
- [ ] Existing tests updated and passing
- [ ] `dotnet test` passes completely
- [ ] Manual testing with `sharpyc run` works

---

## Appendix: Quick Reference for File Changes

### Files to Create
- `docs/language_specification/program_entry_point.md` (rewrite)
- `src/Sharpy.Compiler/Semantic/Validation/ModuleLevelValidator.cs`
- `src/Sharpy.Compiler.Tests/Semantic/ModuleLevelValidatorTests.cs`
- `errors/missing_main_entry_point.spy` + `.error`
- `errors/module_level_no_type_annotation.spy` + `.error`
- `errors/module_level_executable_statement.spy` + `.error`

### Files to Modify (Docs)
- `docs/language_specification/statements.md`
- `docs/implementation_planning/phases.md`
- `src/Sharpy.Compiler/CodeGen/README.md`
- `README.md`

### Files to Modify (Code)
- `src/Sharpy.Compiler/Semantic/ValidationPipeline.cs`
- `src/Sharpy.Compiler/CodeGen/RoslynEmitter.ModuleClass.cs`

### Files to Modify (Tests - ~90+ files)
- All `.spy` files in `TestFixtures/` without main()
- All `.expected` files that need output updates
- Error test files in `errors/`

### Files to Modify (Snippets/Samples)
- `snippets/simple_example.spy`
- `samples/type_system_showcase.spy`
- Possibly others in `snippets/`
