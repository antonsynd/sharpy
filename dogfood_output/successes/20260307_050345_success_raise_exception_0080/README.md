# Successful Dogfood Run

**Timestamp:** 2026-03-07T05:01:16.790950
**Feature Focus:** raise_exception
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Exception raising and handling with custom validation
# Features: Custom exception class, conditional raises, try/except/else

class ValidationError(Exception):
    field: str
    message: str
    
    def __init__(self, field: str, message: str):
        self.field = field
        self.message = message

def validate_score(value: int, field_name: str) -> int:
    if value < 0:
        raise ValueError(f"Score cannot be negative: {value}")
    if value > 100:
        raise ValidationError(field_name, f"Score exceeds maximum: {value}")
    return value

def process_scores(a: int, b: int) -> int:
    total: int = 0
    # Declare variables before try block so they're visible in else
    validated_a: int = 0
    validated_b: int = 0
    try:
        validated_a = validate_score(a, "math")
        validated_b = validate_score(b, "science")
    except ValueError as e:
        print("ValueError caught")
        return -1
    except ValidationError as err:
        print(f"ValidationError: {err.field}")
        return -2
    else:
        total = validated_a + validated_b
        print("Validation passed")
        return total

def main():
    # Test 1: Valid scores
    result1: int = process_scores(75, 85)
    print(result1)
    
    # Test 2: Negative score (ValueError)
    result2: int = process_scores(-5, 50)
    print(result2)
    
    # Test 3: Score too high (ValidationError)
    result3: int = process_scores(50, 150)
    print(result3)

```

## Output

```
Validation passed
160
ValueError caught
-1
ValidationError: science
-2
```

## Timing

- Generation: 133.02s
- Execution: 4.61s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
