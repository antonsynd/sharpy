# Skipped Dogfood Run

**Timestamp:** 2026-03-10T14:11:47.542535
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0103]: Expected end of statement, got Identifier
  --> /tmp/tmp6iuats7m/dogfood_test.spy:34:5
    |
 34 |     valid_count = 0
    |     ^^^^^^^^^^^
    |


**Feature Focus:** union_matching
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
union ProcessResult:
    case Success(value: str)
    case Failure(code: int)
    case Timeout()
    case Cancelled()

def classify_duration(result: ProcessResult) -> str:
    match result:
        case Success(v):
            if v != "":
                return "valid"
            else:
                return "empty"
        case Failure(c):
            if c >= 500:
                return "server_error"
            else:
                return "client_error"
        case Timeout():
            return "timed_out"
        case Cancelled():
            return "cancelled"
    return ""

def main():
    results = [
        ProcessResult.Success("data"),
        ProcessResult.Failure(404),
        ProcessResult.Failure(503),
        ProcessResult.Timeout(),
        ProcessResult.Success(""),
        ProcessResult.Cancelled()
    ]
    valid_count = 0
    error_count = 0
    other_count = 0
    for r in results:
        category = classify_duration(r)
        if category == "valid":
            valid_count += 1
        elif category == "empty" or category == "cancelled":
            other_count += 1
        else:
            error_count += 1
        print(category)
    print(valid_count)
    print(error_count)
    print(other_count)

```

## Timing

- Generation: 517.86s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
