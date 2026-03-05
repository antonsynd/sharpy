# Successful Dogfood Run

**Timestamp:** 2026-03-04T16:39:15.411551
**Feature Focus:** property_inheritance
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
class BaseItem:
    value: float
    
    def __init__(self, val: float):
        self.value = val
    
    @virtual
    property get adjusted(self) -> float:
        return self.value

class MultiplierItem(BaseItem):
    factor: float
    
    def __init__(self, val: float, mult: float):
        super().__init__(val)
        self.factor = mult
    
    @override
    property get adjusted(self) -> float:
        return self.value * self.factor

class OffsetItem(BaseItem):
    offset: float
    
    def __init__(self, val: float, off: float):
        super().__init__(val)
        self.offset = off
    
    @override
    property get adjusted(self) -> float:
        return self.value + self.offset

def transform(item: BaseItem) -> float:
    result = item.adjusted * 2.0
    return result

def main():
    base = BaseItem(10.0)
    mult = MultiplierItem(10.0, 2.0)
    offset = OffsetItem(10.0, 5.0)
    
    print(base.adjusted)
    print(mult.adjusted)
    print(offset.adjusted)
    print(transform(base))
    print(transform(mult))
    print(transform(offset))

```

## Output

```
10.0
20.0
15.0
20.0
40.0
30.0
```

## Timing

- Generation: 317.75s
- Execution: 4.85s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
