# Skipped Dogfood Run

**Timestamp:** 2026-03-10T14:36:06.590630
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0260]: Cannot return type 'tuple[None, None]' from function expecting 'tuple[list[str], list[int]]'
  --> /tmp/tmpxgo85uis/dogfood_test.spy:18:5
    |
 18 |     return (str_results, int_results)
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** asyncio_gather
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
import asyncio

async def fetch_str(label: str, delay_ms: int) -> str:
    await asyncio.sleep(0.001)
    return f"{label}_data"

async def fetch_int(base: int, mult: int) -> int:
    await asyncio.sleep(0.001)
    return base * mult + len(str(base))

async def collect_results() -> tuple[list[str], list[int]]:
    str_task1 = fetch_str("alpha", 1)
    str_task2 = fetch_str("beta", 2)
    int_task1 = fetch_int(5, 3)
    int_task2 = fetch_int(10, 2)
    str_results = await asyncio.gather(str_task1, str_task2)
    int_results = await asyncio.gather(int_task1, int_task2)
    return (str_results, int_results)

async def main():
    results = await collect_results()
    str_vals = results[0]
    int_vals = results[1]
    print(len(str_vals))
    print(len(int_vals))
    for s in str_vals:
        print(s)
    total: int = 0
    for n in int_vals:
        total += n
    print(total)

```

## Timing

- Generation: 1148.25s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
