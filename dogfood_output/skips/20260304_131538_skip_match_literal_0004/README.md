# Skipped Dogfood Run

**Timestamp:** 2026-03-04T12:56:49.245474
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

Type errors:
error[SPY0220]: Pattern type 'None' is incompatible with scrutinee type 'int?'
  --> /tmp/tmpy7sbwilq/dogfood_test.spy:39:14
    |
 39 |         case None:
    |              ^^^^
    |

Validation errors:
error[SPY0266]: Function 'is_truthy' must return a value of type 'str' in all code paths
  --> /tmp/tmpy7sbwilq/dogfood_test.spy:30:1
    |
 30 | def is_truthy(x: bool) -> str:
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** match_literal
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test match statement with literal patterns
# Covers integers, strings, booleans, and None literals

def describe_number(x: int) -> str:
    match x:
        case 0:
            return "zero"
        case 1:
            return "one"
        case 42:
            return "the answer"
        case -1:
            return "negative one"
        case _:
            return "some other number"

def describe_status(status: str) -> str:
    match status:
        case "ok":
            return "all good"
        case "error":
            return "something went wrong"
        case "pending":
            return "please wait"
        case "":
            return "empty string"
        case _:
            return "unknown status"

def is_truthy(x: bool) -> str:
    match x:
        case True:
            return "it's true"
        case False:
            return "it's false"

def maybe_value(x: int?) -> str:
    match x:
        case None:
            return "no value"
        case _:
            return "has value: " + str(x.unwrap())

def main():
    # Test integer literals
    print(describe_number(0))
    print(describe_number(1))
    print(describe_number(42))
    print(describe_number(-1))
    print(describe_number(100))
    
    # Test string literals
    print(describe_status("ok"))
    print(describe_status("error"))
    print(describe_status("pending"))
    print(describe_status(""))
    print(describe_status("unknown"))
    
    # Test boolean literals
    print(is_truthy(True))
    print(is_truthy(False))
    
    # Test None literal
    result1: int? = None()
    result2: int? = Some(42)
    print(maybe_value(result1))
    print(maybe_value(result2))

```

## Timing

- Generation: 1113.37s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
