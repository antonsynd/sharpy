# Skipped Dogfood Run

**Timestamp:** 2026-03-03T08:01:33.415096
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0200]: Undefined identifier 'val'. Did you mean 'all'?
  --> /tmp/tmpv5yt4qo4/dogfood_test.spy:13:8
    |
 13 |     if val > 0:
    |        ^^^
    |

error[SPY0200]: Undefined identifier 'val'. Did you mean 'all'?
  --> /tmp/tmpv5yt4qo4/dogfood_test.spy:14:19
    |
 14 |         return Ok(val)
    |                   ^^^
    |

error[SPY0227]: Cannot infer type for 'Ok()' without a type annotation. Add a type annotation like 'x: int !str = Ok(value)'
  --> /tmp/tmpv5yt4qo4/dogfood_test.spy:41:15
    |
 41 |     chained = Ok(5).map(lambda x: x + 1).map(lambda x: x * 2)
    |               ^^^^^
    |


**Feature Focus:** result_type
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
enum ParseError:
    EMPTY_INPUT = 1
    INVALID_FORMAT = 2
    OUT_OF_RANGE = 3

def parse_positive_int(text: str) -> int !ParseError:
    if len(text) == 0:
        return Err(ParseError.EMPTY_INPUT)
    try:
        val = int(text)
    except ValueError:
        return Err(ParseError.INVALID_FORMAT)
    if val > 0:
        return Ok(val)
    return Err(ParseError.OUT_OF_RANGE)

def main():
    # Test basic Result construction
    ok_result: int !str = Ok(42)
    err_result: int !str = Err("failed")
    
    # Test unwrap_or - returns Ok value or default
    print(ok_result.unwrap_or(0))
    print(err_result.unwrap_or(0))
    
    # Test map - transforms Ok value, leaves Err unchanged
    mapped = ok_result.map(lambda x: x * 10)
    print(mapped.unwrap_or(0))
    
    mapped_err = err_result.map(lambda x: x * 10)
    print(mapped_err.unwrap_or(88))
    
    # Test map_err - transforms Err value, leaves Ok unchanged
    changed_ok = ok_result.map_err(lambda e: "error: " + e)
    print(changed_ok.unwrap_or(100))
    
    changed_err = err_result.map_err(lambda e: "mapped: " + e)
    print(changed_err.unwrap_or(99))
    
    # Test chaining map operations
    chained = Ok(5).map(lambda x: x + 1).map(lambda x: x * 2)
    print(chained.unwrap_or(0))
    
    # Test with actual parsing
    inputs: list[str] = ["42", "0", "abc", ""]
    for text in inputs:
        result = parse_positive_int(text)
        # Use map_err to get printable error, then unwrap_or for default
        display = result.map_err(lambda e: e.name)
        print(display.unwrap_or(-1))

```

## Timing

- Generation: 683.93s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
