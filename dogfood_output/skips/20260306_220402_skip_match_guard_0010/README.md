# Skipped Dogfood Run

**Timestamp:** 2026-03-06T21:59:43.113210
**Skip Reason:** Repeated identical compiler error (likely compiler bug): Compilation errors:

error[SPY0101]: Expected identifier, got Event
  --> /tmp/tmp9vh8rkys/dogfood_test.spy:6:20
    |
  6 | def classify_event(event: Event, threshold: float) -> str:
    |                    ^^^^^
    |


**Feature Focus:** match_guard
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
union Event:
    case Login(user_id: int)
    case Logout(user_id: int, duration: int)
    case Purchase(amount: float)

def classify_event(event: Event, threshold: float) -> str:
    match event:
        case Login(uid) if uid > 100:
            result = "VIP login"
        case Login(uid) if uid > 0:
            result = "Regular login"
        case Logout(uid, dur) if dur > 3600:
            result = "Long session"
        case Logout(uid, dur) if dur > 0:
            result = "Short session"
        case Purchase(amt) if amt >= threshold:
            result = "High value"
        case Purchase(amt) if amt > 0.0:
            result = "Low value"
        case _:
            result = "Unknown"
    return result

def main():
    e1: Event = Event.Login(42)
    e2: Event = Event.Login(150)
    e3: Event = Event.Logout(1, 7200)
    e4: Event = Event.Purchase(250.0)
    e5: Event = Event.Purchase(50.0)
    print(classify_event(e1, 100.0))
    print(classify_event(e2, 100.0))
    print(classify_event(e3, 100.0))
    print(classify_event(e4, 100.0))
    print(classify_event(e5, 100.0))

```

## Timing

- Generation: 247.87s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
