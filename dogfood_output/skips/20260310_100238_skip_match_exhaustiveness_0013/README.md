# Skipped Dogfood Run

**Timestamp:** 2026-03-10T09:49:49.492008
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0266]: Function 'process' must return a value of type 'str' in all code paths
  --> /tmp/tmpzo5oqpko/dogfood_test.spy:13:5
    |
 13 |     def process(self, response: ApiResponse) -> str:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0266]: Function 'classify_response' must return a value of type 'str' in all code paths
  --> /tmp/tmpzo5oqpko/dogfood_test.spy:37:1
    |
 37 | def classify_response(is_success: bool, has_data: bool) -> str:
    | ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** match_exhaustiveness
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test match exhaustiveness with enums, tuples, and type patterns
# Tests exhaustive matching across different type categories using valid syntax
enum StatusCode:
    OK = 200
    NOT_FOUND = 404
    ERROR = 500
    TIMEOUT = 504
union ApiResponse:
    case Success(data: str)
    case Failure(code: StatusCode, message: str)
    case Retryable(attempts: int)
class ResponseHandler:
    def process(self, response: ApiResponse) -> str:
        # Match statement (not expression) - each case has its own block
        match response:
            case Success(d):
                return f"Data: {d}"
            case Failure(c, m):
                return f"Error {c.value}: {m}"
            case Retryable(a):
                return f"Retry after {a}"
class StatusAnalyzer:
    def analyze_batch(self, statuses: list[StatusCode]) -> dict[str, int]:
        stats: dict[str, int] = {"ok": 0, "client_error": 0, "server_error": 0}
        for s in statuses:
            # Exhaustive match on enum
            match s:
                case StatusCode.OK:
                    stats["ok"] += 1
                case StatusCode.NOT_FOUND:
                    stats["client_error"] += 1
                case StatusCode.ERROR:
                    stats["server_error"] += 1
                case StatusCode.TIMEOUT:
                    stats["server_error"] += 1
        return stats
def classify_response(is_success: bool, has_data: bool) -> str:
    # Exhaustive match on bool tuple
    match (is_success, has_data):
        case (True, True):
            return "valid_data"
        case (True, False):
            return "empty_success"
        case (False, True):
            return "failed_with_data"
        case (False, False):
            return "failed_no_data"
def main():
    handler = ResponseHandler()
    # Create different union cases
    r1 = ApiResponse.Success("payload")
    r2 = ApiResponse.Failure(StatusCode.NOT_FOUND, "missing")
    r3 = ApiResponse.Retryable(3)
    print(handler.process(r1))
    print(handler.process(r2))
    print(handler.process(r3))
    # Test enum exhaustiveness via iteration
    analyzer = StatusAnalyzer()
    codes: list[StatusCode] = [StatusCode.OK, StatusCode.ERROR, StatusCode.OK, StatusCode.TIMEOUT]
    result = analyzer.analyze_batch(codes)
    print(result["ok"])
    print(result["server_error"])
    # Test tuple exhaustiveness
    print(classify_response(True, True))
    print(classify_response(False, True))
    print(classify_response(True, False))
    print(classify_response(False, False))

```

## Timing

- Generation: 752.06s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
