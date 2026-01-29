# Successful Dogfood Run

**Timestamp:** 2026-01-29T00:06:43.690117
**Feature Focus:** f_string_basic
**Complexity:** simple
**Backend:** claude

## Generated Sharpy Code

```python
# Test f-string interpolation with variables and expressions

def main():
    name: str = "Alice"
    age: int = 25
    score: int = 87
    
    print(f"Name: {name}")
    print(f"Age: {age}")
    print(f"Score: {score}")
    print(f"{name} is {age} years old")
    print(f"Next year: {age + 1}")
    print(f"Double score: {score * 2}")

# EXPECTED OUTPUT:
# Name: Alice
# Age: 25
# Score: 87
# Alice is 25 years old
# Next year: 26
# Double score: 174
```

## Output

```
Name: Alice
Age: 25
Score: 87
Alice is 25 years old
Next year: 26
Double score: 174
```

## Timing

- Generation: 4.18s
- Execution: 1.52s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
