# Successful Dogfood Run

**Timestamp:** 2026-03-07T04:22:31.537848
**Feature Focus:** async_with_generators
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
import asyncio

class ManagedSequence:
    items: list[int]
    active: bool
    
    def __init__(self):
        self.items = [10, 20, 30, 40, 50]
        self.active = False
    
    async def __aenter__(self) -> ManagedSequence:
        self.active = True
        print("sequence active")
        return self
    
    async def __aexit__(self):
        self.active = False
        print("sequence closed")
    
    def fetch_values(self) -> int:
        for item in self.items:
            if self.active:
                yield item

async def main():
    result: int = 0
    
    async with ManagedSequence() as seq:
        # Generator yields while async context is held
        for value in seq.fetch_values():
            result += value
            print(result)
            await asyncio.sleep(0.01)
    
    print("total")
    print(result)

```

## Output

```
sequence active
10
30
60
100
150
sequence closed
total
150
```

## Timing

- Generation: 136.74s
- Execution: 4.86s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
