"""
Prompt templates for code generation, validation, and verification.
"""

from pathlib import Path
from typing import Optional


def get_spec_context(spec_dir: Path, phases_file: Path) -> str:
    """Load relevant specification context for prompts."""
    context_parts = []

    # Load phases overview
    if phases_file.exists():
        content = phases_file.read_text()
        # Extract phases 0.1.0 through 0.1.5
        lines = content.split("\n")
        in_relevant_section = False
        relevant_lines = []

        for line in lines:
            if "## Phase 0.1.0" in line:
                in_relevant_section = True
            elif "## Phase 0.1.6" in line:
                in_relevant_section = False
                break

            if in_relevant_section:
                relevant_lines.append(line)

        if relevant_lines:
            context_parts.append("# Implementation Phases (0.1.0 - 0.1.5)\n\n")
            context_parts.append("\n".join(relevant_lines[:500]))  # Limit size

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
) -> str:
    """Generate a prompt for creating Sharpy code."""

    examples_section = ""
    if example_snippets:
        examples_section = "\n\n## Example Sharpy Code\n\n"
        for i, snippet in enumerate(example_snippets[:3], 1):
            examples_section += f"### Example {i}\n```python\n{snippet}\n```\n\n"

    complexity_guide = {
        "simple": """
Generate SIMPLE code:
- 5-15 lines total
- 0-1 functions
- Basic arithmetic with int variables
- 1-3 print statements showing results
""",
        "medium": """
Generate MEDIUM complexity code:
- 15-30 lines total
- 1-2 functions that may call each other
- Use ONE control flow type (if/elif/else OR for OR while)
- 3-5 print statements showing intermediate steps
""",
        "complex": """
Generate COMPLEX code:
- 30-50 lines total
- 2-3 functions
- Mix of control flow (if + for, or if + while)
- 5-8 print statements showing the flow
""",
    }

    return f"""You are generating Sharpy code for compiler testing (dogfooding).

## CRITICAL: Allowed Features (Phases 0.1.0-0.1.5 ONLY)

### ✅ ALLOWED - Use these features:
- **Variables**: `x: int = 42` or `x = 42` (type inference)
- **Types**: `int`, `str`, `bool`, `float` only (no `int32`, no nullable `?`)
- **Operators**: `+`, `-`, `*`, `/`, `//`, `%`, `==`, `!=`, `<`, `<=`, `>`, `>=`, `and`, `or`, `not`
- **Augmented assignment**: `+=`, `-=`, `*=`, `/=`
- **If statements**: `if`, `elif`, `else` with conditions
- **While loops**: `while condition:`
- **For loops**: `for i in range(n):`, `for i in range(start, end):`, `for i in range(start, end, step):`
- **Functions**: `def name(param: type) -> return_type:` with positional parameters
- **Return**: `return value`
- **Print**: `print(value)` - SINGLE argument only, NO multiple args
- **Pass**: `pass` statement
- **Break/Continue**: inside loops only
- **Constants**: `const NAME: int = 42`
- **String literals**: `"hello"`, `'world'` (simple strings only)
- **Boolean literals**: `True`, `False`
- **None literal**: `None` (but no nullable types)

### ❌ FORBIDDEN - Do NOT use these features:
- **NO f-strings**: `f"hello {{x}}"` is NOT allowed - use separate `print()` calls
- **NO multi-argument print**: `print(a, b, c)` is NOT allowed - use multiple `print()` calls
- **NO string concatenation with +**: `"a" + "b"` may not work yet
- **NO default parameters**: `def foo(x: int = 5)` is NOT allowed yet
- **NO keyword arguments**: `foo(name="value")` is NOT allowed yet
- **NO classes/structs**: no `class`, `struct`, `interface`
- **NO lists/dicts/sets**: no `[]`, `{{}}`, `set()`
- **NO imports**: no `import`, `from`
- **NO try/except**: no exception handling
- **NO lambdas**: no `lambda`
- **NO type aliases**: no `type X = Y`
- **NO nullable types**: no `int?`, `str?`
- **NO recursion**: keep functions non-recursive for simplicity
- **NO nested functions**: define all functions at top level

## Task

Generate a novel, valid Sharpy program testing: **{feature_focus}**
Complexity level: **{complexity}**

{complexity_guide.get(complexity, complexity_guide["simple"])}

{examples_section}

## Output Format

Return ONLY valid Sharpy code with expected output in comments:

```python
# Example: Simple arithmetic test
x: int = 10
y: int = 20
result: int = x + y
print(result)

# EXPECTED OUTPUT:
# 30
```

IMPORTANT:
- Use ONLY simple print() calls with ONE argument: print(value)
- For multiple values, use multiple print() statements
- NO string formatting, concatenation, or f-strings
- Every print() output should appear in EXPECTED OUTPUT
- Keep the code simple and focused on testing the specified feature"""


def get_spec_validation_prompt(code: str, spec_context: str) -> str:
    """Generate a prompt for validating code against the spec."""

    return f"""You are a STRICT Sharpy language specification validator for phases 0.1.0-0.1.5.

## ALLOWED Features (Phases 0.1.0-0.1.5):

### Variables & Types
- Variable declaration: `x: int = 42` or `x = 42` (inference)
- Primitive types ONLY: `int`, `str`, `bool`, `float`
- Constants: `const NAME: int = 42`

### Operators
- Arithmetic: `+`, `-`, `*`, `/`, `//`, `%`
- Comparison: `==`, `!=`, `<`, `<=`, `>`, `>=`
- Logical: `and`, `or`, `not`
- Assignment: `=`, `+=`, `-=`, `*=`, `/=`

### Control Flow
- If: `if condition:` / `elif condition:` / `else:`
- While: `while condition:`
- For: `for i in range(n):`, `for i in range(start, end):`, `for i in range(start, end, step):`
- Break/continue inside loops

### Functions
- Definition: `def name(param: type) -> return_type:`
- Positional parameters with explicit types
- Return statement: `return value`

### Built-ins
- `print(value)` - single argument only, no multi-arg print
- `range()` in for loops only

### Literals
- Integer: `42`, `-10`
- Float: `3.14`
- String: `"hello"`, `'world'` (simple, no f-strings)
- Boolean: `True`, `False`
- None: `None`

## FORBIDDEN Features (NOT in phases 0.1.0-0.1.5):

❌ f-strings: `f"text {{var}}"` - REJECT
❌ Multi-argument print: `print(a, b, c)` - REJECT (use multiple print calls)
❌ Default parameters: `def foo(x: int = 5)` - REJECT
❌ Keyword arguments: `foo(name="value")` - REJECT
❌ Nullable types: `int?`, `str?` - REJECT
❌ Classes/structs/interfaces/enums - REJECT
❌ Lists/dicts/sets: `[]`, `{{}}`, `set()` - REJECT
❌ Imports: `import`, `from` - REJECT
❌ Try/except/raise - REJECT
❌ Lambda expressions - REJECT
❌ Type aliases - REJECT
❌ String concatenation: `"a" + "b"` - REJECT
❌ String formatting methods - REJECT
❌ Ternary expressions: `x if cond else y` - REJECT
❌ List comprehensions - REJECT
❌ Multiple assignment: `a, b = 1, 2` - REJECT
❌ Walrus operator: `:=` - REJECT

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
The code uses only features from phases 0.1.0-0.1.5.
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
        return "\n".join(expected_lines).strip()
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
