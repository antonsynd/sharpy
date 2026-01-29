"""
Prompt templates for code generation, validation, and verification.
"""

from pathlib import Path
from typing import Optional


def load_test_fixtures(fixtures_dir: Path) -> dict[str, list[tuple[str, str]]]:
    """Load existing test fixtures organized by category.

    Returns:
        Dict mapping category name to list of (filename, content) tuples.
    """
    fixtures: dict[str, list[tuple[str, str]]] = {}

    if not fixtures_dir.exists():
        return fixtures

    for category_dir in fixtures_dir.iterdir():
        if not category_dir.is_dir() or category_dir.name.startswith("."):
            continue

        category_name = category_dir.name
        fixtures[category_name] = []

        for spy_file in sorted(category_dir.glob("*.spy")):
            try:
                content = spy_file.read_text()
                fixtures[category_name].append((spy_file.stem, content))
            except Exception:
                continue

    return fixtures


def format_fixtures_for_prompt(
    fixtures: dict[str, list[tuple[str, str]]],
    max_examples_per_category: int = 2,
    max_total_chars: int = 3000,
) -> str:
    """Format test fixtures as a prompt section showing what already exists.

    Args:
        fixtures: Dict from load_test_fixtures
        max_examples_per_category: Max number of example files to show per category
        max_total_chars: Maximum total characters for the section

    Returns:
        Formatted string describing existing tests.
    """
    if not fixtures:
        return ""

    parts = []
    parts.append("## Existing Test Coverage (DO NOT duplicate these)")
    parts.append("")
    parts.append("The following tests already exist in the compiler test suite.")
    parts.append(
        "Generate something DIFFERENT from these - explore untested combinations!"
    )
    parts.append("")

    total_chars = 0

    for category, test_files in sorted(fixtures.items()):
        if total_chars > max_total_chars:
            parts.append(f"\n... and more categories (truncated for brevity)")
            break

        # List all test names in this category
        test_names = [name for name, _ in test_files]
        parts.append(
            f"### {category.replace('_', ' ').title()} ({len(test_files)} tests)"
        )
        parts.append(f"Existing tests: {', '.join(test_names)}")

        # Show a couple of examples
        for name, content in test_files[:max_examples_per_category]:
            # Truncate long examples
            if len(content) > 400:
                content = content[:400] + "\n# ... (truncated)"

            example_text = f"\n**{name}.spy:**\n```python\n{content}\n```"
            if total_chars + len(example_text) > max_total_chars:
                break
            parts.append(example_text)
            total_chars += len(example_text)

        parts.append("")

    return "\n".join(parts)


def get_spec_context(spec_dir: Path, phases_file: Path) -> str:
    """Load relevant specification context for prompts."""
    context_parts = []

    # Load phases overview
    if phases_file.exists():
        content = phases_file.read_text()
        # Extract phases 0.1.0 through 0.1.18 (implemented features)
        lines = content.split("\n")
        in_relevant_section = False
        relevant_lines = []

        for line in lines:
            if "## Phase 0.1.0" in line:
                in_relevant_section = True
            elif "## Phase 0.1.19" in line:
                in_relevant_section = False
                break

            if in_relevant_section:
                relevant_lines.append(line)

        if relevant_lines:
            context_parts.append("# Implementation Phases (0.1.0 - 0.1.18)\n\n")
            context_parts.append("\n".join(relevant_lines[:2000]))  # Limit size

    # Load key spec files
    key_specs = [
        "introduction.md",
        "variable_declaration.md",
        "function_definition.md",
        "if_statement.md",
        "for_statement.md",
        "while_statement.md",
        "primitive_types.md",
        "expressions.md",
        "class_definition.md",
        "inheritance.md",
        "interfaces.md",
        "structs.md",
        "enums.md",
        "nullable_types.md",
        "generics.md",
        "type_aliases.md",
        "modules.md",
        # Phase 0.1.11+ features
        "collections.md",
        "comprehensions.md",
        "exception_handling.md",
        "lambda_expressions.md",
        "fstrings.md",
        "dotnet_interop.md",
        # Phase 0.1.15-0.1.18: Optional & Result types
        "tagged_unions_optional.md",
        "tagged_unions_result.md",
        "maybe_expressions.md",
        "try_expressions.md",
    ]

    for spec_name in key_specs:
        spec_path = spec_dir / spec_name
        if spec_path.exists():
            content = spec_path.read_text()
            # Truncate long specs
            if len(content) > 2000:
                content = content[:2000] + "\n... (truncated)"
            context_parts.append(f"\n\n# {spec_name}\n\n{content}")

    return "\n".join(context_parts)


def get_code_generation_prompt(
    spec_context: str,
    feature_focus: str = "general",
    complexity: str = "simple",
    example_snippets: list[str] | None = None,
    existing_fixtures_section: str = "",
) -> str:
    """Generate a prompt for creating Sharpy code.

    Args:
        spec_context: Language specification context.
        feature_focus: The feature area to focus on.
        complexity: Complexity level (simple, medium, complex).
        example_snippets: Optional list of example code snippets.
        existing_fixtures_section: Formatted section showing existing test fixtures.
    """

    examples_section = ""
    if example_snippets:
        examples_section = "\n\n## Example Sharpy Code\n\n"
        for i, snippet in enumerate(example_snippets[:3], 1):
            examples_section += f"### Example {i}\n```python\n{snippet}\n```\n\n"

    complexity_guide = {
        "simple": """
Generate SIMPLE code:
- 5-20 lines total
- 0-1 functions OR 1 simple class
- Basic arithmetic with int variables
- 1-3 print statements showing results
""",
        "medium": """
Generate MEDIUM complexity code:
- 20-40 lines total
- 1-2 functions OR 1-2 classes with methods
- Can use structs, enums, or simple inheritance
- Use ONE control flow type (if/elif/else OR for OR while)
- 3-5 print statements showing intermediate steps
""",
        "complex": """
Generate COMPLEX code:
- 40-70 lines total
- 2-3 classes with inheritance OR interfaces
- Can include: abstract classes, virtual/override methods, structs, enums
- Mix of control flow (if + for, or if + while)
- Can use nullable types, type aliases, generics, or module imports
- 5-8 print statements showing the flow
""",
    }

    return f"""You are generating Sharpy code for compiler testing (dogfooding).

## CRITICAL: Program Entry Point Requirement

Every executable Sharpy program MUST have a `main()` function as its entry point:
- All executable statements (print, variable assignments, function calls) must be inside `main()`
- Only declarations (classes, functions, type aliases, static fields with type annotations) can be at module level
- Module-level variables require explicit type annotations: `counter: int = 0`
- **DO NOT call main() yourself** - Sharpy automatically invokes `main()` at runtime
- Example of WRONG: `def main(): ... \\n main()` - the `main()` call is forbidden at module level

## CRITICAL: Allowed Features (Phases 0.1.0-0.1.18)

### ✅ ALLOWED - Use these features:

#### Program Structure
- **Entry point**: `def main():` is REQUIRED for all executable code
- **Module-level declarations**: classes, functions, constants, static fields (with type annotation)

#### Variables & Types (0.1.3)
- **Variables**: `x: int = 42` or `x = 42` (type inference)
- **Types**: `int`, `str`, `bool`, `float` (primitive types)
- **Nullable types**: `int?`, `str?` with `None` assignment
- **Operators**: `+`, `-`, `*`, `/`, `//`, `%`, `**`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `and`, `or`, `not`
- **Augmented assignment**: `+=`, `-=`, `*=`, `/=`
- **Null coalescing**: `??` (e.g., `name ?? "default"`)
- **Null conditional**: `?.` (e.g., `name?.upper()`)
- **Constants**: `const NAME: int = 42`

#### Control Flow (0.1.4)
- **If statements**: `if`, `elif`, `else` with conditions
- **While loops**: `while condition:`
- **For loops**: `for i in range(n):`, `for i in range(start, end):`, `for i in range(start, end, step):`
- **Break/Continue**: inside loops only

#### Functions (0.1.5)
- **Function definition**: `def name(param: type) -> return_type:`
- **Default parameters**: `def foo(x: int, y: int = 5) -> int:`
- **Keyword arguments**: `foo(x=10, y=20)`
- **Return**: `return value`

#### Classes (0.1.6)
- **Class definition**: `class ClassName:`
- **Fields**: `x: int` inside class body
- **Constructor**: `def __init__(self, params):`
- **Instance methods**: `def method(self) -> type:`
- **Static methods**: methods without `self` parameter (auto-detected)
- **Field access**: `obj.field`, `self.field`

#### Inheritance & Interfaces (0.1.7)
- **Single inheritance**: `class Child(Parent):`
- **Super calls**: `super().__init__(args)` in `__init__`, `super().method()` in `@override` methods
- **Abstract classes**: `@abstract` decorator on class
- **Abstract methods**: `@abstract` decorator + `...` body
- **Virtual methods**: `@virtual` decorator
- **Override methods**: `@override` decorator
- **Final classes/methods**: `@final` decorator
- **Interfaces**: `interface IName:` with method signatures using `...`
- **Multiple interfaces**: `class Foo(IBar, IBaz):`
- **Access modifiers**: `@private`, `@protected`, `@internal` (default is public)
- **IMPORTANT**: Interface types have NO concrete members - you can only call methods declared in the interface. Do NOT access fields like `.value` through interface types.

#### Structs & Enums (0.1.8)
- **Structs**: `struct Name:` (value types, copied on assignment)
- **Enums**: `enum Name:` with explicit values (e.g., `RED = 1`, `PENDING = 0`)
- **Enum values**: `EnumName.VALUE` (e.g., `Color.RED`, `Status.PENDING`)
- **Enum output**: When printed, enums display in PascalCase (e.g., `print(Status.PENDING)` outputs `Pending`)

#### Type System (0.1.9)
- **Nullable types**: `T?` syntax
- **Type narrowing**: `if x is not None:` narrows type
- **Type aliases**: `type UserId = int`
- **Basic generics**: `class Box[T]:`, `def identity[T](x: T) -> T:`
- **Generic constraints**: `[T: IComparable]` - single constraint only
- **Multiple constraints**: NOT SUPPORTED - use single constraint only, do NOT write `[T: A, B]`

#### Module System (0.1.10)
- **Import**: `import module_name`, `import module as alias`
- **From import**: `from module import item1, item2`
- **Import alias**: `from module import Item as Alias`

#### Built-ins
- **Print**: `print(value)` - SINGLE argument only
- **Range**: `range()` in for loops
- **Boolean/None literals**: `True`, `False`, `None`
- **String literals**: `"hello"`, `'world'`

#### F-Strings (0.1.11)
- **F-string interpolation**: `f"Hello {{name}}"`, `f"Result: {{x + y}}"`
- **Format specifiers**: `f"{{value:.2f}}"`, `f"{{num:05d}}"`

#### Collections (0.1.11)
- **List literals**: `nums: list[int] = [1, 2, 3]`
- **Dict literals**: `scores: dict[str, int] = {{"alice": 100, "bob": 85}}`
- **Set literals**: `unique: set[int] = {{1, 2, 3}}`
- **List comprehensions**: `[x * 2 for x in range(10)]`
- **Dict comprehensions**: `{{k: v * 2 for k, v in items.items()}}`
- **Set comprehensions**: `{{x for x in items if x > 0}}`
- **Collection iteration**: `for item in collection:`
- **len()**: `len(collection)` for lists, dicts, sets
- **Indexing**: `collection[index]`, `dict[key]`

#### .NET Interop (0.1.12)
- **Import .NET namespaces**: `from system import Console`
- **Use .NET types**: After importing, use types normally

#### Exception Handling (0.1.13)
- **Try/except**: `try: ... except ExceptionType as e: ...`
- **Try/finally**: `try: ... finally: ...`
- **Try/except/else/finally**: Full exception handling pattern
- **Raise**: `raise ValueError("message")`

#### Lambda Expressions (0.1.14)
- **Lambdas**: `lambda x: x * 2`, `lambda a, b: a + b`
- **Higher-order functions**: Passing lambdas to functions
- **Type inference**: Lambda parameter types are inferred from the expected function type context

#### Optional Types (0.1.15)
- **Optional type**: `x: int? = Some(42)`, `y: int? = Nothing`
- **Optional constructors**: `Some(value)` wraps a value, `Nothing` represents absence
- **Optional methods**: `.unwrap()`, `.unwrap_or(default)`, `.map(lambda v: v * 2)`
- **Type annotation**: `T?` is shorthand for `Optional[T]`

#### Result Types (0.1.16)
- **Result type**: `x: int !str = Ok(42)`, `y: int !str = Err("failed")`
- **Result constructors**: `Ok(value)` for success, `Err(error)` for failure
- **Result methods**: `.unwrap()`, `.unwrap_or(default)`, `.map(fn)`, `.map_err(fn)`
- **Type annotation**: `T !E` is shorthand for `Result[T, E]`

#### Maybe Expression (0.1.17)
- **Maybe**: `maybe nullable_value` converts `T | None` to `T?` (Optional)
- **Usage**: Useful for converting .NET nullable values to Sharpy optionals

#### Try Expression (0.1.18)
- **Try**: `try risky_call()` wraps a call in `Result[T, Exception]`
- **Try with type**: `try[ValueError] int("abc")` catches specific exception type
- **Usage**: Converts exception-throwing code into Result-based error handling

### ❌ FORBIDDEN - Do NOT use these features (not yet implemented):
- **NO main() call at module level**: Do NOT write `main()` after defining it - it's auto-invoked by runtime
- **NO multi-argument print**: `print(a, b, c)` - use multiple `print()` calls
- **NO async/await**: Async programming not implemented
- **NO with statement**: Context managers not implemented
- **NO walrus operator**: `:=` - assignment expressions not implemented
- **NO pattern matching**: `match`/`case` not implemented
- **NO tuple unpacking**: `a, b = 1, 2` - may have issues
- **NO isinstance with tuples**: `isinstance(x, (int, str))` - use `or` instead

### ⚠️ CRITICAL NAMING RULES - Avoid builtin conflicts:
- **NEVER name functions or variables**: `double`, `int`, `str`, `float`, `bool`, `len`, `print`, `range`, `abs`, `min`, `max`, `sum`, `round`, `input`, `type`, `list`, `dict`, `set`, `tuple`, `map`, `filter`, `zip`, `any`, `all`, `sorted`, `reversed`, `enumerate`, `chr`, `ord`, `hex`, `bin`, `oct`, `hash`, `id`, `open`, `file`, `exit`, `quit`
- Use **descriptive names** like `double_value`, `multiply_by_two`, `calculate_double`, `doubled` instead
- Names like `double` conflict with the `double` type (float64) and will cause type errors

{existing_fixtures_section}

## Task

Generate a **NOVEL** and **UNIQUE** Sharpy program testing: **{feature_focus}**
Complexity level: **{complexity}**

**IMPORTANT**: Your code should explore DIFFERENT patterns, algorithms, or feature combinations
than the existing tests shown above. Be creative! Some ideas:
- Use different variable names and scenarios
- Combine features in new ways
- Test edge cases not covered by existing tests
- Use different numeric values and control flow patterns

{complexity_guide.get(complexity, complexity_guide["simple"])}

{examples_section}

## Output Format

Return ONLY valid Sharpy code with expected output in comments:

```python
# Example: Simple class with method
class Counter:
    value: int

    def __init__(self, start: int):
        self.value = start

    def increment(self) -> None:
        self.value += 1

    def get(self) -> int:
        return self.value

def main():
    c = Counter(10)
    c.increment()
    print(c.get())

# EXPECTED OUTPUT:
# 11
```

IMPORTANT:
- Use ONLY simple print() calls with ONE argument: print(value)
- For multiple values, use multiple print() statements or f-strings: print(f"value: {{x}}")
- Every print() output should appear in EXPECTED OUTPUT
- Keep the code simple and focused on testing the specified feature"""


def get_multifile_generation_prompt(
    spec_context: str,
    feature_focus: str = "module_imports",
    complexity: str = "medium",
    example_snippets: list[str] | None = None,
    existing_fixtures_section: str = "",
) -> str:
    """Generate a prompt for creating multi-file Sharpy code with imports.

    Args:
        spec_context: Language specification context.
        feature_focus: The feature area to focus on.
        complexity: Complexity level (simple, medium, complex).
        example_snippets: Optional list of example code snippets.
        existing_fixtures_section: Formatted section showing existing test fixtures.
    """

    examples_section = ""
    if example_snippets:
        examples_section = "\n\n## Example Sharpy Code\n\n"
        for i, snippet in enumerate(example_snippets[:3], 1):
            examples_section += f"### Example {i}\n```python\n{snippet}\n```\n\n"

    complexity_guide = {
        "simple": """
Generate a SIMPLE multi-file project:
- 2 files total: main.spy + one module
- Module has 1-2 simple functions
- main.spy imports and uses those functions
- 2-3 print statements showing results
""",
        "medium": """
Generate a MEDIUM multi-file project:
- 2-3 files total: main.spy + 1-2 modules
- Modules can have classes, functions, or both
- Can use inheritance or interfaces across files
- One module can import from another
- 3-5 print statements showing intermediate steps
""",
        "complex": """
Generate a COMPLEX multi-file project:
- 3-4 files total: main.spy + 2-3 modules
- Modules with classes, interfaces, structs, enums
- Cross-module inheritance or interface implementation
- Complex imports (from module import item1, item2)
- 5-8 print statements showing the flow
""",
    }

    return f"""You are generating a MULTI-FILE Sharpy project for compiler testing (dogfooding).

## CRITICAL: Program Entry Point Requirement

The `main.spy` file MUST have a `main()` function as its entry point:
- All executable statements (print, variable assignments, function calls) must be inside `main()`
- Library modules (non-main.spy files) do NOT need a `main()` function
- Only declarations (classes, functions, type aliases, static fields) can be at module level
- **DO NOT call main() yourself** - Sharpy automatically invokes `main()` at runtime

## CRITICAL: Module System Rules (Phase 0.1.10)

### Import Syntax
- **Import entire module**: `import module_name` (then use `module_name.function()`)
- **Import with alias**: `import module_name as alias`
- **From import**: `from module_name import function1, function2`
- **From import with alias**: `from module_name import Item as Alias`

### Module File Structure
- Each `.spy` file is a module
- Module name = filename without `.spy` extension
- No `__init__.py` needed (not Python!)
- Modules in same directory can import each other
- The entry point file (`main.spy`) MUST have a `main()` function

### Allowed Features (same as single-file, phases 0.1.0-0.1.18)
- Variables, functions, classes, structs, enums, interfaces
- Inheritance, abstract/virtual/override methods
- Nullable types, type aliases, basic generics
- F-strings: `f"Hello {{name}}"`
- Collections: `list[int]`, `dict[str, int]`, `set[int]` with literals
- Comprehensions: `[x * 2 for x in range(10)]`
- Exception handling: `try`, `except`, `finally`, `raise`
- Lambdas: `lambda x: x * 2` (parameter types inferred from context)
- .NET interop: `from system import Console`
- Optional types: `T?`, `Some(value)`, `Nothing`, `.unwrap()`, `.unwrap_or()`
- Result types: `T !E`, `Ok(value)`, `Err(error)`, `.unwrap()`, `.map(fn)`
- Maybe expression: `maybe nullable_value` (converts `T | None` to `T?`)
- Try expression: `try risky_call()` (wraps in `Result[T, Exception]`)

### ❌ FORBIDDEN in module system:
- **NO relative imports**: `from .module import x` - NOT SUPPORTED
- **NO package imports**: `from package.module import x` - NOT SUPPORTED
- **NO star imports**: `from module import *` - NOT SUPPORTED
- **NO async/await**: Not implemented
- **NO with statement**: Context managers not implemented
- **NO walrus operator**: `:=` - Not implemented
- **NO pattern matching**: `match`/`case` - Not implemented

{existing_fixtures_section}

## Task

Generate a **MULTI-FILE** Sharpy project testing: **{feature_focus}**
Complexity level: **{complexity}**

{complexity_guide.get(complexity, complexity_guide["medium"])}

{examples_section}

## Output Format

Return multiple files, each clearly marked with its filename. Use this EXACT format:

```
=== FILE: module_name.spy ===
# Module providing utility functions

def helper_function(x: int) -> int:
    return x * 2

class UtilityClass:
    value: int

    def __init__(self, v: int):
        self.value = v

=== FILE: main.spy ===
# Main entry point - imports from module_name
from module_name import helper_function, UtilityClass

def main():
    result: int = helper_function(5)
    print(result)

    obj = UtilityClass(10)
    print(obj.value)

# EXPECTED OUTPUT:
# 10
# 10
```

CRITICAL RULES:
1. Each file starts with `=== FILE: filename.spy ===`
2. One file MUST be named `main.spy` - this is the entry point
3. EXPECTED OUTPUT comment goes in main.spy ONLY
4. Use ONLY `from module import items` syntax (NOT `import module`)
5. Module names match filenames exactly (without .spy)
6. All print() calls must be in main.spy
7. NO circular imports between modules"""


def get_regeneration_prompt(
    spec_context: str,
    feature_focus: str,
    complexity: str,
    previous_code: str,
    validation_error: str,
    attempt: int,
    max_attempts: int,
    example_snippets: Optional[list[str]] = None,
    existing_fixtures_section: str = "",
) -> str:
    """Generate a prompt for retrying code generation with validation feedback.

    Used when previous code generation failed validation. Provides the AI with
    the previous code and the specific validation error so it can fix the issue.

    Args:
        spec_context: Language specification context.
        feature_focus: The feature area to focus on.
        complexity: Complexity level (simple, medium, complex).
        previous_code: The previously generated code that failed validation.
        validation_error: The error message from validation.
        attempt: Current attempt number (1-indexed).
        max_attempts: Maximum number of attempts allowed.
        example_snippets: Optional list of example code snippets.
        existing_fixtures_section: Formatted section showing existing test fixtures.

    Returns:
        Prompt string for regenerating code with feedback.
    """
    examples_section = ""
    if example_snippets:
        examples_section = "\n\n## Example Sharpy Code\n\n"
        for i, snippet in enumerate(example_snippets[:2], 1):
            examples_section += f"### Example {i}\n```python\n{snippet}\n```\n\n"

    return f"""You are regenerating Sharpy code for compiler testing (dogfooding).

## REGENERATION ATTEMPT {attempt}/{max_attempts}

Your previous code FAILED validation. You must fix the issue and regenerate.

## Previous Code That Failed

```python
{previous_code}
```

## Validation Error

```
{validation_error}
```

## Instructions

1. Analyze the validation error carefully
2. Identify which feature(s) are NOT allowed in phases 0.1.0-0.1.18
3. REMOVE or REPLACE the forbidden features
4. Keep the same general logic/intent but use only allowed features

## CRITICAL: Program Entry Point Requirement

Every executable Sharpy program MUST have a `main()` function:
- All executable statements (print, assignments, function calls) must be inside `main()`
- Only declarations (classes, functions, constants) can be at module level
- **DO NOT call main() yourself** - Sharpy automatically invokes `main()` at runtime

## CRITICAL: Allowed Features (Phases 0.1.0-0.1.18)

### ✅ ALLOWED:
- Entry point: `def main():` is REQUIRED for executable code
- Variables: `x: int = 42`
- Types: `int`, `str`, `bool`, `float`, nullable `int?`
- Operators: `+`, `-`, `*`, `/`, `//`, `%`, `**`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `and`, `or`, `not`
- Null operators: `??`, `?.`
- Control flow: `if`/`elif`/`else`, `while`, `for i in range(n)`, `break`, `continue`
- Functions: `def name(param: type) -> return_type:`, default params, keyword args
- Classes: `class Name:`, `__init__`, instance/static methods
- Inheritance: `class Child(Parent):`, `super().__init__()`, `@abstract`, `@virtual`, `@override`
- Interfaces: `interface IName:` with `...` bodies
- Structs: `struct Name:`
- Enums: `enum Name:` with explicit values
- Type aliases: `type UserId = int`
- Basic generics: `class Box[T]:`, `def foo[T](x: T) -> T:`
- Imports: `import module`, `from module import item`
- Built-ins: `print(value)` - SINGLE ARGUMENT ONLY, `range()` in for loops
- F-strings: `f"Hello {{name}}"`, `f"Result: {{x + y}}"`
- Collections: `list[int]`, `dict[str, int]`, `set[int]` with literals `[1,2,3]`, `{{"key": val}}`
- Comprehensions: `[x * 2 for x in range(10)]`
- Exception handling: `try`, `except`, `finally`, `raise`
- Lambdas: `lambda x: x * 2` (parameter types inferred from context)
- .NET interop: `from system import Console`
- Optional types: `T?`, `Some(value)`, `Nothing`, `.unwrap()`, `.unwrap_or(default)`, `.map(fn)`
- Result types: `T !E`, `Ok(value)`, `Err(error)`, `.unwrap()`, `.unwrap_or(default)`, `.map(fn)`
- Maybe expression: `maybe nullable_value` (converts `T | None` to `T?`)
- Try expression: `try risky_call()`, `try[ExceptionType] expr` (wraps in Result)

### ❌ FORBIDDEN (DO NOT USE):
- NO main() call at module level - `main()` is auto-invoked by runtime, do NOT call it yourself
- NO bare executable statements at module level - wrap in `def main():`
- NO multi-argument print: `print(a, b)` - use multiple print() calls or f-strings
- NO async/await - not implemented
- NO with statement - context managers not implemented
- NO walrus operator: `:=` - not implemented
- NO pattern matching: `match`/`case` - not implemented
- NO tuple unpacking: `a, b = 1, 2` - may have issues

{examples_section}

## Task

Regenerate the code for: **{feature_focus}** (complexity: **{complexity}**)

Fix the validation error above. Generate VALID code that does NOT use any forbidden features.

## Output Format

Return ONLY valid Sharpy code with expected output in comments:

```python
# Your fixed code here
...
# EXPECTED OUTPUT:
# <expected output lines>
```

IMPORTANT:
- Use ONLY simple print() calls with ONE argument
- For multiple values, use multiple print() statements or f-strings: print(f"value: {{x}}")
- Every print() output should appear in EXPECTED OUTPUT"""


def get_spec_validation_prompt(code: str, spec_context: str) -> str:
    """Generate a prompt for validating code against the spec."""

    return f"""You are a STRICT Sharpy language specification validator for phases 0.1.0-0.1.18.

## Program Entry Point Requirement

Every executable Sharpy program MUST have a `main()` function:
- All executable statements (print, assignments without type annotation, function calls) must be inside `main()`
- Only declarations are allowed at module level: classes, functions, constants, static fields (with type annotation)
- Example of valid module-level: `counter: int = 0` (static field with type annotation)
- Example of INVALID module-level: `x = 5` (no type annotation, or bare statement)
- **DO NOT call main() yourself** - Sharpy auto-invokes `main()` at runtime
- Example of INVALID: `def main(): ... \\n main()` - the `main()` call is forbidden

## Module Files (Library Modules)

**IMPORTANT**: Library modules that are IMPORTED by other files do NOT require a `main()` function.
Only the entry point file (main.spy or a single executable file) needs `main()`.
If the code contains ONLY declarations (classes, functions, constants) and no executable statements,
it is a library module and is VALID without `main()`.

## ALLOWED Features (Phases 0.1.0-0.1.18):

### Program Structure
- Entry point: `def main():` is REQUIRED for executable code
- Module-level declarations: classes, functions, constants, static fields (with type annotation)

### Variables & Types (0.1.3)
- Variable declaration: `x: int = 42` or `x = 42` (inference)
- Primitive types: `int`, `str`, `bool`, `float`
- Nullable types: `int?`, `str?`, etc.
- Constants: `const NAME: int = 42`

### Operators (0.1.3)
- Arithmetic: `+`, `-`, `*`, `/`, `//`, `%`, `**`
- Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`
- Logical: `and`, `or`, `not`
- Assignment: `=`, `+=`, `-=`, `*=`, `/=`
- Null coalescing: `??`
- Null conditional: `?.`

### Control Flow (0.1.4)
- If: `if condition:` / `elif condition:` / `else:`
- While: `while condition:`
- For: `for i in range(n):`, `for i in range(start, end):`, `for i in range(start, end, step):`
- Break/continue inside loops
- Pass statement

### Functions (0.1.5)
- Definition: `def name(param: type) -> return_type:`
- Default parameters: `def foo(x: int = 5) -> int:`
- Keyword arguments: `foo(x=10, y=20)`
- Return statement: `return value`

### Classes (0.1.6)
- Class definition: `class Name:`
- Fields: `x: int` in class body
- Constructor: `def __init__(self, ...):`
- Instance methods: `def method(self) -> type:`
- Static methods: methods without `self` parameter
- Field access: `obj.field`, `self.field`

### Inheritance & Interfaces (0.1.7)
- Single inheritance: `class Child(Parent):`
- Super calls: `super().__init__(...)`, `super().method()`
- Abstract classes: `@abstract` decorator
- Abstract methods: `@abstract` + `...` body
- Virtual methods: `@virtual` decorator
- Override methods: `@override` decorator
- Final: `@final` decorator
- Interfaces: `interface IName:` with `...` method bodies
- Multiple interfaces: `class Foo(IBar, IBaz):`
- Access modifiers: `@private`, `@protected`, `@internal`

### Structs & Enums (0.1.8)
- Structs: `struct Name:` with fields and methods
- Enums: `enum Name:` with explicit values (e.g., `RED = 1`)
- Enum access: `EnumName.VALUE`
- Enum output: When printed, displays PascalCase (e.g., `print(Status.PENDING)` outputs `Pending`)

### Type System (0.1.9)
- Nullable types: `T?` syntax
- Type narrowing: `if x is not None:`
- Type aliases: `type UserId = int`
- Basic generics: `class Box[T]:`, `def foo[T](x: T) -> T:`
- Generic constraints: `[T: IComparable]`

### Module System (0.1.10)
- Import: `import module`, `import module as alias`
- From import: `from module import item1, item2`
- Import alias: `from module import Item as Alias`

### Built-ins
- `print(value)` - single argument only
- `range()` in for loops only

### Literals
- Integer: `42`, `-10`
- Float: `3.14`
- String: `"hello"`, `'world'`
- F-strings: `f"Hello {{name}}"`, `f"Result: {{x + y}}"`
- Boolean: `True`, `False`
- None: `None`

### Collections (0.1.11)
- List literals: `nums: list[int] = [1, 2, 3]`
- Dict literals: `scores: dict[str, int] = {{"alice": 100}}`
- Set literals: `unique: set[int] = {{1, 2, 3}}`
- List comprehensions: `[x * 2 for x in range(10)]`
- Dict/Set comprehensions: `{{k: v for k, v in items}}`
- Collection iteration: `for item in collection:`
- len(): `len(collection)`
- Indexing: `collection[index]`, `dict[key]`

### .NET Interop (0.1.12)
- Import .NET namespaces: `from system import Console`
- Use .NET types after importing

### Exception Handling (0.1.13)
- Try/except: `try: ... except ExceptionType as e: ...`
- Try/finally: `try: ... finally: ...`
- Raise: `raise ValueError("message")`

### Lambda Expressions (0.1.14)
- Lambdas: `lambda x: x * 2`, `lambda a, b: a + b`
- Higher-order functions
- Lambda parameter types inferred from expected function type context

### Optional Types (0.1.15)
- Optional type annotation: `T?` or `Optional[T]`
- Optional constructors: `Some(value)`, `Nothing`
- Optional methods: `.unwrap()`, `.unwrap_or(default)`, `.map(fn)`

### Result Types (0.1.16)
- Result type annotation: `T !E` or `Result[T, E]`
- Result constructors: `Ok(value)`, `Err(error)`
- Result methods: `.unwrap()`, `.unwrap_or(default)`, `.map(fn)`, `.map_err(fn)`

### Maybe Expression (0.1.17)
- Maybe: `maybe expr` converts `T | None` to `T?` (Optional)

### Try Expression (0.1.18)
- Try: `try expr` wraps in `Result[T, Exception]`
- Try with type: `try[ExceptionType] expr` catches specific exception

## FORBIDDEN Features (NOT in phases 0.1.0-0.1.18):

❌ Calling main() at module level - main() is auto-invoked, do NOT call it yourself - REJECT
❌ Bare executable statements at module level (must be in `main()`) - REJECT
❌ Multi-argument print: `print(a, b, c)` - REJECT (use multiple print calls or f-strings)
❌ Async/await: `async def`, `await` - REJECT (not implemented)
❌ Context managers: `with` statement - REJECT (not implemented)
❌ Walrus operator: `:=` - REJECT (not implemented)
❌ Pattern matching: `match`/`case` - REJECT (not implemented)
❌ Tuple unpacking: `a, b = 1, 2` - REJECT (may have issues)
❌ isinstance with tuple: `isinstance(x, (int, str))` - REJECT

## Code to Validate

```python
{code}
```

## Validation Task

Scan the code line by line. If ANY forbidden feature is used, mark as INVALID.

## Response Format

If ALL features are from the allowed list:
```
VALID
The code uses only features from phases 0.1.0-0.1.18.
```

If ANY forbidden feature is found:
```
INVALID
Reason: [specific forbidden feature found]
Line: [line number]
Found: [the problematic code]
```

BE VERY STRICT. When in doubt, reject the code."""


def get_output_verification_prompt(
    code: str,
    expected_output: str,
    actual_output: str,
) -> str:
    """Generate a prompt for verifying output correctness."""

    return f"""Compare the expected and actual outputs from a Sharpy program.

## Code

```python
{code}
```

## Expected Output

```
{expected_output}
```

## Actual Output

```
{actual_output}
```

## Task

Determine if the actual output matches the expected output.

Consider:
- Whitespace differences (trailing spaces, newlines) should be IGNORED
- **Floating-point precision**: Minor differences in decimal representation are ACCEPTABLE
  - Example: "5.14" and "5.140000000000001" are EQUIVALENT (IEEE 754 precision)
  - Example: "7.85" and "7.8500000000000005" are EQUIVALENT
  - Compare floating-point numbers to ~10 significant figures
- String content should be exact
- Integer values should match exactly

## Response Format

Respond with EXACTLY one of:

```
MATCH
Outputs are equivalent.
```

OR

```
MISMATCH
Difference: [describe the specific difference]
Expected: [what was expected]
Got: [what was received]
```
"""


def get_test_uniqueness_prompt(
    code: str,
    existing_tests: list[tuple[str, str]],
) -> str:
    """Generate a prompt for checking if a test provides unique value.

    Args:
        code: The generated Sharpy code to evaluate.
        existing_tests: List of (test_name, test_code) tuples from the same category.

    Returns:
        Prompt string for uniqueness evaluation.
    """
    existing_section = ""
    if existing_tests:
        existing_section = "## Existing Tests in This Category\n\n"
        for name, content in existing_tests[:10]:  # Limit to 10 examples
            # Truncate long tests
            if len(content) > 300:
                content = content[:300] + "\n# ... (truncated)"
            existing_section += f"### {name}\n```python\n{content}\n```\n\n"
    else:
        existing_section = (
            "## Existing Tests\n\nNo existing tests in this category yet.\n"
        )

    return f"""Evaluate whether this Sharpy test provides unique value compared to existing tests.

## New Test Code

```python
{code}
```

{existing_section}

## Evaluation Criteria

A test is UNIQUE and worth adding if it:
- Tests a different feature combination than existing tests
- Uses a meaningfully different algorithm or pattern
- Covers an edge case not tested by existing tests
- Tests a feature at a different complexity level

A test is a DUPLICATE and should be skipped if it:
- Tests the same feature combination with only trivial differences (variable names, numeric values)
- Is structurally identical to an existing test
- Doesn't add meaningful coverage beyond what already exists

## Response Format

Respond with EXACTLY one of:

```
UNIQUE
Reason: [brief explanation of what new coverage this provides]
```

OR

```
DUPLICATE
Reason: [which existing test(s) already cover this]
```
"""


def extract_expected_output(code: str) -> Optional[str]:
    """Extract expected output from code comments."""
    lines = code.split("\n")
    in_expected_block = False
    expected_lines = []

    for line in lines:
        stripped = line.strip()

        # Check for start of expected output block
        if "EXPECTED OUTPUT" in stripped.upper() or "EXPECTED:" in stripped.upper():
            in_expected_block = True
            continue

        # Check for end of expected output block
        if in_expected_block:
            if stripped.startswith("#"):
                # Remove the comment marker
                output_line = (
                    stripped[1:].strip() if stripped[1:2] == " " else stripped[1:]
                )
                # Empty comment lines might be intentional empty output
                expected_lines.append(output_line)
            elif stripped and not stripped.startswith("#"):
                # Non-comment, non-empty line ends the block
                break

    if expected_lines:
        # Join lines and strip trailing whitespace from each line,
        # but preserve the structure. Add trailing newline since print() adds one.
        result = "\n".join(expected_lines).rstrip()
        return result + "\n" if result else None
    return None


def extract_code_block(response: str) -> Optional[str]:
    """Extract code from a response that might have markdown formatting."""
    import re

    # Try to find code block with python/sharpy markers
    pattern = r"```(?:python|sharpy)?\s*\n(.*?)```"
    matches = re.findall(pattern, response, re.DOTALL)

    if matches:
        # Return the longest match (most likely the main code)
        return max(matches, key=len).strip()

    # If no code block, check if the entire response looks like code
    lines = response.strip().split("\n")
    if lines and not any(line.startswith("```") for line in lines):
        # Check if it looks like code (has def, print, assignments, etc.)
        code_indicators = ["def ", "print(", "= ", "if ", "for ", "while ", "#"]
        if any(indicator in response for indicator in code_indicators):
            return response.strip()

    return None


def extract_multifile_code(response: str) -> Optional[dict[str, str]]:
    """Extract multiple files from a response with file markers.

    Parses responses in the format:
    ```
    === FILE: module_name.spy ===
    <code>

    === FILE: main.spy ===
    <code>
    ```

    Args:
        response: The AI response potentially containing multiple files.

    Returns:
        Dictionary mapping filename to code content, or None if parsing fails.
        Returns None if no valid multi-file structure is found.
    """
    import re

    # First, try to extract from code blocks
    code_block_pattern = r"```(?:python|sharpy)?\s*\n(.*?)```"
    code_blocks = re.findall(code_block_pattern, response, re.DOTALL)

    # Use the code blocks if found, otherwise use the whole response
    content = "\n".join(code_blocks) if code_blocks else response

    # Pattern to match file markers: === FILE: filename.spy ===
    file_pattern = r"===\s*FILE:\s*([a-zA-Z_][a-zA-Z0-9_]*\.spy)\s*===\s*\n"

    # Find all file markers and their positions
    markers = list(re.finditer(file_pattern, content, re.IGNORECASE))

    if not markers:
        return None

    files: dict[str, str] = {}

    for i, match in enumerate(markers):
        filename = match.group(1).lower()  # Normalize filename to lowercase
        start_pos = match.end()

        # Find the end of this file's content (start of next file or end of content)
        if i + 1 < len(markers):
            end_pos = markers[i + 1].start()
        else:
            end_pos = len(content)

        # Extract and clean the code
        code = content[start_pos:end_pos].strip()

        # Remove any trailing file markers that might have been included
        code = re.sub(r"\n===\s*FILE:.*$", "", code, flags=re.IGNORECASE)

        if code:
            files[filename] = code

    # Validate: must have at least 2 files and one must be main.spy
    if len(files) < 2:
        return None

    if "main.spy" not in files:
        return None

    return files


def extract_expected_output_from_multifile(files: dict[str, str]) -> Optional[str]:
    """Extract expected output from the main.spy file in a multi-file project.

    Args:
        files: Dictionary mapping filename to code content.

    Returns:
        The expected output string, or None if not found.
    """
    if "main.spy" not in files:
        return None

    return extract_expected_output(files["main.spy"])
