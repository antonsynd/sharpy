# Skipped Dogfood Run

**Timestamp:** 2026-03-10T12:56:48.711561
**Skip Reason:** Sharpy compiler error after 3 attempts: Compilation errors:

error[SPY0224]: Function expects 1 arguments but got 0
  --> /tmp/tmpsv4ra7_c/dogfood_test.spy:5:28
    |
  5 |         async for value in source():
    |                            ^^^^^^^^
    |

error[SPY0248]: Cannot override 'transform' because the base class method in 'AbstractStreamProcessor' is not marked @virtual or @abstract. Add @virtual to the method in the base class.
  --> /tmp/tmpsv4ra7_c/dogfood_test.spy:28:5
    |
 28 |     async def transform(self, source: (int) -> int) -> int:
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0224]: Function expects 1 arguments but got 0
  --> /tmp/tmpsv4ra7_c/dogfood_test.spy:29:28
    |
 29 |         async for value in source():
    |                            ^^^^^^^^
    |

error[SPY0220]: Cannot pass argument of type '() -> IAsyncEnumerable[int]' to parameter of type '(int) -> int'
  --> /tmp/tmpsv4ra7_c/dogfood_test.spy:46:42
    |
 46 |     async for val in processor.transform(source_a2.generator):
    |                                          ^^^^^^^^^^^^^^^^^^^
    |

error[SPY0220]: Cannot assign type 'IAsyncEnumerable[int]' to variable of type 'int'
  --> /tmp/tmpsv4ra7_c/dogfood_test.spy:50:5
    |
 50 |     gen_b: int = source_b.generator()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |

error[SPY0220]: Cannot assign type 'IAsyncEnumerable[int]' to variable of type 'int'
  --> /tmp/tmpsv4ra7_c/dogfood_test.spy:51:5
    |
 51 |     gen_c: int = source_c.generator()
    |     ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    |


**Feature Focus:** async_generator
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
import asyncio

class AbstractStreamProcessor[T]:
    async def transform(self, source: (T) -> T) -> T:
        async for value in source():
            await asyncio.sleep(0.001)
            yield value * 2

class NumberSource:
    start: int
    step: int

    def __init__(self, start: int, step: int) -> None:
        self.start = start
        self.step = step

    async def generator(self) -> int:
        current: int = self.start
        count: int = 0
        while count < 3:
            await asyncio.sleep(0.001)
            yield current
            current += self.step
            count += 1

class DoublingProcessor(AbstractStreamProcessor[int]):
    @override
    async def transform(self, source: (int) -> int) -> int:
        async for value in source():
            await asyncio.sleep(0.001)
            yield value * 2

async def main() -> None:
    source_a: NumberSource = NumberSource(1, 2)
    source_b: NumberSource = NumberSource(10, 5)
    source_c: NumberSource = NumberSource(100, 50)

    print("Original:")
    async for val in source_a.generator():
        print(val)

    processor: DoublingProcessor = DoublingProcessor()
    source_a2: NumberSource = NumberSource(1, 2)

    print("Processed:")
    async for val in processor.transform(source_a2.generator):
        print(val)

    print("Merged:")
    gen_b: int = source_b.generator()
    gen_c: int = source_c.generator()
    async for val in gen_b:
        print(val)
    async for val in gen_c:
        print(val)

```

## Timing

- Generation: 499.39s

## Notes

This iteration was skipped because the generated code didn't pass validation.
This is typically due to the AI generating code with unsupported features
or syntax that doesn't match the Sharpy spec (phases 0.1.0-0.2.6).

This output is saved for inspection to help improve prompting.
