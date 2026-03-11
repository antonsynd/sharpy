# Successful Dogfood Run

**Timestamp:** 2026-03-10T17:41:49.561618
**Feature Focus:** containment_test
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Test containment operators with various collections and edge cases
def main():
    # Test list containment with mixed searches
    items: list[int] = [5, 10, 15, 20, 25]
    
    # Test set containment
    unique_chars: set[str] = {"a", "e", "i", "o", "u"}
    
    # Test dict key containment
    scores: dict[str, int] = {"alice": 95, "bob": 87, "carol": 92}
    
    # First value in list
    print(5 in items)
    
    # Last value in list  
    print(25 in items)
    
    # Missing value in list
    print(99 not in items)
    
    # Vowel in set
    print("e" in unique_chars)
    
    # Consonant not in vowel set
    print("b" not in unique_chars)
    
    # Key in dict
    print("alice" in scores)
    
    # Missing key in dict
    print("dave" not in scores)

```

## Output

```
True
True
True
True
True
True
True
```

## Timing

- Generation: 50.02s
- Execution: 5.04s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
