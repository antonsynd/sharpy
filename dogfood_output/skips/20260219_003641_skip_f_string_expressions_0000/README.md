# Skipped Dogfood Run

**Timestamp:** 2026-02-19T00:23:57.025595
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0599]: Internal error: generated C# contains 2 syntax error(s): Identifier expected; Identifier expected. This is a compiler bug -- please report it.
  --> /tmp/tmp0gdcxxb5/dogfood_test.spy


**Feature Focus:** f_string_expressions
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
def main():
    # Basic f-string with variable
    name: str = "World"
    print(f"Hello, {{name}}!")

    # F-string with arithmetic expression
    x: int = 10
    y: int = 20
    print(f"{{x}} + {{y}} = {{x + y}}")
    print(f"{{x}} * {{y}} = {{x * y}}")

    # F-string with function call
    nums: list[int] = [1, 2, 3, 4, 5]
    print(f"Length of nums: {{len(nums)}}")
    print(f"Sum of nums: {{sum(nums)}}")

    # F-string with format specifiers
    pi: float = 3.14159265
    print(f"Pi to 2 decimals: {{pi:.2f}}")
    print(f"Pi to 4 decimals: {{pi:.4f}}")
    print(f"Pi in scientific: {{pi:.2e}}")

    # Integer formatting
    n: int = 42
    print(f"Number padded: {{n:05d}}")
    print(f"Number hex: {{n:x}}")

    # F-string with method calls
    message: str = "hello world"
    print(f"Uppercase: {{message.upper()}}")
    print(f"Capitalized: {{message.capitalize()}}")

    # F-string with conditional logic (using if statement)
    score: int = 85
    status: str = "Fail"
    if score >= 60:
        status = "Pass"
    print(f"Score: {{score}} - {{status}}")

    # F-string with nested field access
    person: dict[str, str] = {"name": "Alice", "city": "NYC"}
    print(f"{{person.get(\"name\")}} lives in {{person.get(\"city\")}}")

    # F-string with list indexing
    items: list[str] = ["apple", "banana", "cherry"]
    print(f"First item: {{items[0]}}")
    print(f"Last item: {{items[2]}}")

    # Float default formatting (ensures decimal point)
    price: float = 25.0
    print(f"Price: {{price}} dollars")

    # Complex expression mixing variables and operations
    a: int = 5
    b: int = 3
    result: float = (a * b + 10) / 2.0
    print(f"Result of ({{a}} * {{b}} + 10) / 2 = {{result}}")
    print(f"Result squared: {{result ** 2:.2f}}")

    # F-string with range (using list comprehension)
    range_nums: list[int] = [i for i in range(10)]
    total: int = sum(range_nums)
    print(f"Sum of 0-9: {{total}}")

    # Multiple calculations in one f-string
    print(f"{{a}} < {{b}}? {{a < b}}, {{a}} > {{b}}? {{a > b}}")

    # String operations in f-string
    word: str = "Sharpy"
    print(f"{{word}} has {{len(word)}} letters, reversed: {{word[::-1]}}")

    # EXPECTED OUTPUT:
    # Hello, World!
    # 10 + 20 = 30
    # 10 * 20 = 200
    # Length of nums: 5
    # Sum of nums: 15
    # Pi to 2 decimals: 3.14
    # Pi to 4 decimals: 3.1416
    # Pi in scientific: 3.14e+00
    # Number padded: 00042
    # Number hex: 2a
    # Uppercase: HELLO WORLD
    # Capitalized: Hello world
    # Score: 85 - Pass
    # Alice lives in NYC
    # First item: apple
    # Last item: cherry
    # Price: 25.0 dollars
    # Result of (5 * 3 + 10) / 2 = 12.5
    # Result squared: 156.25
    # Sum of 0-9: 45
    # 5 < 3? False, 5 > 3? True
    # Sharpy has 6 letters, reversed: yprahS
```

## Timing

- Generation: 746.31s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.1.18).

This output is saved for inspection to help improve prompting.
