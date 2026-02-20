# Successful Dogfood Run

**Timestamp:** 2026-02-19T08:40:21.479943
**Feature Focus:** dict_literal
**Complexity:** simple
**Backend:** klaude

## Generated Sharpy Code

```python
# Simple dict literal with integer keys and string values
def main():
    http_codes: dict[int, str] = {200: "OK", 404: "Not Found", 500: "Server Error"}
    print(http_codes[200])
    print(http_codes[404])
    msg: str = http_codes[500]
    print(msg)

# EXPECTED OUTPUT:
# OK
# Not Found
# Server Error
```

## Output

```
OK
Not Found
Server Error
```

## Timing

- Generation: 181.71s
- Execution: 4.34s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
