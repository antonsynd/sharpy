# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T04:19:20.916329
**Type:** output_mismatch
**Feature Focus:** generic_type_alias
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Generic type aliases with various arities and concrete instantiation
type Predicate[T] = (T) -> bool
type Mapper[T, U] = (T) -> U
type Container[T] = tuple[list[T], int]

@abstract
class Processor[T]:
    _prefix: str
    
    def __init__(self, prefix: str):
        self._prefix = prefix
    
    @abstract
    def transform(self, item: str) -> str:
        ...
    
    property get prefix(self) -> str:
        return self._prefix

class StringProcessor(Processor[str]):
    @override
    def transform(self, item: str) -> str:
        return f"{self._prefix}{item.upper()}"

def process_list[T, U](items: list[T], pred: Predicate[T], mapper: Mapper[T, U]) -> Container[U]:
    matched: list[U] = []
    for item in items:
        if pred(item):
            matched.append(mapper(item))
    return (matched, len(matched))

def main():
    data: list[str] = ["hi", "hello", "ok", "magnificent"]
    processor: StringProcessor = StringProcessor(">")
    
    long_check: Predicate[str] = lambda s: len(s) > 5
    len_mapper: Mapper[str, int] = lambda s: len(s)
    
    result: Container[int] = process_list[str, int](data, long_check, len_mapper)
    lengths, count = result
    
    print(processor.prefix)
    print(count)
    print(len(lengths))
    
    # Use explicit StringProcessor for transform
    for s in data:
        if len(s) > 3:
            print(processor.transform(s))

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
>
2
2
>HELLO
>MAGNIFICENT

```

### Actual
```
>
1
1
>HELLO
>MAGNIFICENT
```

## Timing

- Generation: 411.46s
- Execution: 5.20s
