# Successful Dogfood Run

**Timestamp:** 2026-03-08T06:28:31.212744
**Feature Focus:** optional_type
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Optional chaining, transformation, and narrowing in pipeline
# Demonstrates combining ??, .map(), .unwrap_or(), and type narrowing

def fetch_config() -> int?:
    return Some(25)

def describe_value(opt: int?) -> str:
    if opt is not None:
        return f"value={opt}"
    return "missing"

def main():
    cfg = fetch_config()
    
    # ?? provides default only if None
    primary = cfg ?? 10
    print(primary)
    
    # Map transforms the wrapped value
    doubled = cfg.map(lambda x: x * 2).unwrap_or(0)
    print(doubled)
    
    # Type narrowing in function
    print(describe_value(cfg))
    
    # Testing with actual None
    empty: int? = None()
    fallback = empty ?? 50
    print(fallback)
    
    # Map on None propagates None
    result = empty.map(lambda x: x + 5).unwrap_or(-1)
    print(result)

```

## Output

```
25
50
value=25
50
-1
```

## Timing

- Generation: 266.44s
- Execution: 5.37s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
