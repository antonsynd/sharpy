# Issue Report: compilation_failed

**Timestamp:** 2026-03-10T06:14:10.907754
**Type:** compilation_failed
**Feature Focus:** try_except_basic
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex try/except test with class hierarchy, multiple exception types,
# and exception handling in processing pipelines

class DataError(Exception):
    pass

class ValidationError(Exception):
    pass

class RangeError(Exception):
    pass

@abstract
class DataSource:
    @abstract
    def fetch(self) -> int: ...

    @abstract
    def get_name(self) -> str: ...

class ReliableSource(DataSource):
    value: int
    name: str

    def __init__(self, name: str, value: int):
        self.name = name
        self.value = value

    @override
    def fetch(self) -> int:
        return self.value

    @override
    def get_name(self) -> str:
        return self.name

class UnreliableSource(DataSource):
    name: str
    fail_count: int

    def __init__(self, name: str):
        self.name = name
        self.fail_count = 0

    @override
    def fetch(self) -> int:
        self.fail_count += 1
        if self.fail_count % 2 == 0:
            raise RuntimeError("Simulated failure")
        return self.fail_count * 10

    @override
    def get_name(self) -> str:
        return self.name

@abstract
class Validator:
    @virtual
    def validate(self, value: int) -> bool:
        return value >= 0

class RangedValidator(Validator):
    min_val: int
    max_val: int

    def __init__(self, min_val: int, max_val: int):
        self.min_val = min_val
        self.max_val = max_val

    def __init__(self):
        self.__init__(0, 100)

    @override
    def validate(self, value: int) -> bool:
        if value < self.min_val:
            raise ValidationError("below minimum")
        if value > self.max_val:
            raise ValidationError("above maximum")
        return True

def process_value(source: DataSource, validator: Validator) -> int:
    result: int
    value: int

    try:
        value = source.fetch()
    except RuntimeError:
        return -1

    try:
        validator.validate(value)
    except ValidationError:
        if value < 0:
            result = 0
        else:
            result = 100
    else:
        result = value

    try:
        if result > 50:
            raise RangeError("too large")
        return result
    except RangeError:
        print("RangeError caught")
        return 50
    finally:
        print(source.get_name())

    return -3

def batch_process(sources: list[DataSource]) -> list[int]:
    results: list[int] = []
    validator = RangedValidator()

    for i in range(len(sources)):
        try:
            source = sources[i]
            result = process_value(source, validator)
            results.append(result)
        except IndexError:
            print("Index error")
        except Exception:
            print("Unexpected")

    return results

def main():
    reliable1 = ReliableSource("Reliable_A", 42)
    reliable2 = ReliableSource("Reliable_B", 75)
    unreliable = UnreliableSource("Unreliable")

    sources: list[DataSource] = [reliable1, unreliable, reliable2, unreliable]
    results = batch_process(sources)

    print(len(results))
    for i in range(len(results)):
        print(results[i])

```

## Error

```
Assembly compilation failed:

error[CS1729]: 'DogfoodTest.RangeError' does not contain a constructor that takes 1 arguments
  --> /tmp/tmpqptqx81s/dogfood_test.spy:102:27
     |
 102 |             raise RangeError("too large")
     |                           ^
     |

error[CS0165]: Use of unassigned local variable 'result'
  --> /tmp/tmpqptqx81s/dogfood_test.spy:101:17
     |
 101 |         if result > 50:
     |                 ^
     |

error[CS1729]: 'DogfoodTest.ValidationError' does not contain a constructor that takes 1 arguments
  --> /tmp/tmpqptqx81s/dogfood_test.spy:76:27
    |
 76 |             raise ValidationError("below minimum")
    |                           ^
    |

error[CS1729]: 'DogfoodTest.ValidationError' does not contain a constructor that takes 1 arguments
  --> /tmp/tmpqptqx81s/dogfood_test.spy:78:27
    |
 78 |             raise ValidationError("above maximum")
    |                           ^
    |


```

## Compiler Output

```
warning[SPY0450]: Unreachable code detected
  --> /tmp/tmpqptqx81s/dogfood_test.spy:110:5
     |
 110 |     return -3
     |     ^^^^^^^^^
     |

warning[SPY0450]: Unreachable code detected
  --> /tmp/tmpqptqx81s/dogfood_test.spy:108:9
     |
 108 |         print(source.get_name())
     |         ^^^^^^^^^^^^^^^^^^^^^^^^
     |


```

## Generated C#

```csharp
warning[SPY0450]: Unreachable code detected
  --> /tmp/tmpqptqx81s/dogfood_test.spy:110:5
     |
 110 |     return -3
     |     ^^^^^^^^^
     |

warning[SPY0450]: Unreachable code detected
  --> /tmp/tmpqptqx81s/dogfood_test.spy:108:9
     |
 108 |         print(source.get_name())
     |         ^^^^^^^^^^^^^^^^^^^^^^^^
     |

Generated C# code written to: /tmp/tmpqptqx81s/dogfood_test.cs

```

## Timing

- Generation: 256.93s
- Execution: 5.07s
