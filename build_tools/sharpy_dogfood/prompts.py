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
        # Extract phases 0.1.0 through 0.1.10
        lines = content.split("\n")
        in_relevant_section = False
        relevant_lines = []

        for line in lines:
            if "## Phase 0.1.0" in line:
                in_relevant_section = True
            elif "## Phase 0.1.11" in line:
                in_relevant_section = False
                break

            if in_relevant_section:
                relevant_lines.append(line)

        if relevant_lines:
            context_parts.append("# Implementation Phases (0.1.0 - 0.1.10)\n\n")
            context_parts.append("\n".join(relevant_lines[:1500]))  # Limit size

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
    example_snippets: list[str] = None,
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

## CRITICAL: Allowed Features (Phases 0.1.0-0.1.10 ONLY)

### ✅ ALLOWED - Use these features:

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

#### Structs & Enums (0.1.8)
- **Structs**: `struct Name:` (value types, copied on assignment)
- **Enums**: `enum Name:` with explicit values
- **Enum values**: `EnumName.VALUE`

#### Type System (0.1.9)
- **Nullable types**: `T?` syntax
- **Type narrowing**: `if x is not None:` narrows type
- **Type aliases**: `type UserId = int`
- **Basic generics**: `class Box[T]:`, `def identity[T](x: T) -> T:`
- **Generic constraints**: `[T: IComparable]`

#### Module System (0.1.10)
- **Import**: `import module_name`, `import module as alias`
- **From import**: `from module import item1, item2`
- **Import alias**: `from module import Item as Alias`

#### Built-ins
- **Print**: `print(value)` - SINGLE argument only
- **Range**: `range()` in for loops
- **Boolean/None literals**: `True`, `False`, `None`
- **String literals**: `"hello"`, `'world'`

### ❌ FORBIDDEN - Do NOT use these features (v0.1.11+):
- **NO f-strings**: `f"hello {{x}}"` is NOT allowed yet
- **NO multi-argument print**: `print(a, b, c)` - use multiple `print()` calls
- **NO string concatenation**: `"a" + "b"` may not work reliably
- **NO lists/dicts/sets literals**: `[]`, `{{}}`, `set()` - collections are v0.1.11
- **NO list comprehensions**: `[x for x in items]` - v0.1.11
- **NO try/except**: exception handling is v0.1.13
- **NO lambdas**: lambda expressions are v0.1.14
- **NO .NET interop imports**: `from system import ...` is v0.1.12
- **NO isinstance with tuples**: `isinstance(x, (int, str))` - use `or` instead
- **NO ternary expressions**: `x if cond else y`
- **NO multiple assignment**: `a, b = 1, 2`
- **NO walrus operator**: `:=`

### ⚠️ NAMING RULES - Avoid builtin conflicts:
- **Do NOT name functions** `double`, `int`, `str`, `float`, `bool`, `len`, `print`, `range`, `abs`, `min`, `max`, `sum`, `round`, `input`, `type`, `list`, `dict`, `set`, `tuple`, `map`, `filter`, `zip`, `any`, `all`, `sorted`, `reversed`, `enumerate`, `chr`, `ord`, `hex`, `bin`, `oct`, `hash`, `id`, `open`, `file`, `exit`, `quit`
- Use **descriptive names** like `double_value`, `multiply_by_two`, `calculate_double` instead

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

c = Counter(10)
c.increment()
print(c.get())

# EXPECTED OUTPUT:
# 11
```

IMPORTANT:
- Use ONLY simple print() calls with ONE argument: print(value)
- For multiple values, use multiple print() statements
- NO string formatting, concatenation, or f-strings
- Every print() output should appear in EXPECTED OUTPUT
- Keep the code simple and focused on testing the specified feature"""


def get_spec_validation_prompt(code: str, spec_context: str) -> str:
    """Generate a prompt for validating code against the spec."""

    return f"""You are a STRICT Sharpy language specification validator for phases 0.1.0-0.1.10.

## ALLOWED Features (Phases 0.1.0-0.1.10):

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
- String: `"hello"`, `'world'` (simple, no f-strings)
- Boolean: `True`, `False`
- None: `None`

## FORBIDDEN Features (NOT in phases 0.1.0-0.1.10):

❌ f-strings: `f"text {{var}}"` - REJECT
❌ Multi-argument print: `print(a, b, c)` - REJECT (use multiple print calls)
❌ Lists/dicts/sets literals: `[]`, `{{}}`, `set()` - REJECT (v0.1.11)
❌ List comprehensions: `[x for x in items]` - REJECT (v0.1.11)
❌ Try/except/raise - REJECT (v0.1.13)
❌ Lambda expressions - REJECT (v0.1.14)
❌ .NET interop imports: `from system import ...` - REJECT (v0.1.12)
❌ String concatenation: `"a" + "b"` - REJECT
❌ Ternary expressions: `x if cond else y` - REJECT
❌ Multiple assignment: `a, b = 1, 2` - REJECT
❌ Walrus operator: `:=` - REJECT
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
The code uses only features from phases 0.1.0-0.1.10.
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
