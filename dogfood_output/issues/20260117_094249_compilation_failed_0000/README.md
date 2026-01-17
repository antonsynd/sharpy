# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T09:42:24.086959
**Type:** compilation_failed
**Feature Focus:** for_range_start_end
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Complex test for for_range_start_end with inheritance and interfaces

interface IProcessor:
    def process(self, start: int, end: int) -> int:
        ...

@abstract
class BaseCalculator:
    name: str
    
    def __init__(self, name: str):
        self.name = name
    
    @abstract
    def calculate(self, start: int, end: int) -> int:
        ...
    
    @virtual
    def display_name(self) -> None:
        print(self.name)

class SumCalculator(BaseCalculator, IProcessor):
    multiplier: int
    
    def __init__(self, name: str, multiplier: int):
        super().__init__(name)
        self.multiplier = multiplier
    
    @override
    def calculate(self, start: int, end: int) -> int:
        total: int = 0
        for i in range(start, end):
            total += i * self.multiplier
        return total
    
    def process(self, start: int, end: int) -> int:
        return self.calculate(start, end)

class EvenSumCalculator(BaseCalculator, IProcessor):
    def __init__(self, name: str):
        super().__init__(name)
    
    @override
    def calculate(self, start: int, end: int) -> int:
        total: int = 0
        for i in range(start, end):
            if i % 2 == 0:
                total += i
        return total
    
    def process(self, start: int, end: int) -> int:
        return self.calculate(start, end)
    
    @override
    def display_name(self) -> None:
        print("EvenSum")

def run_processor(proc: IProcessor, start: int, end: int) -> int:
    return proc.process(start, end)

sum_calc = SumCalculator("BasicSum", 2)
sum_calc.display_name()
result1: int = sum_calc.calculate(1, 6)
print(result1)

even_calc = EvenSumCalculator("EvenOnly")
even_calc.display_name()
result2: int = even_calc.calculate(0, 10)
print(result2)

result3: int = run_processor(sum_calc, 5, 10)
print(result3)

result4: int = run_processor(even_calc, 1, 8)
print(result4)

count: int = 0
for j in range(10, 15):
    if j % 3 == 0:
        count += 1
print(count)

# EXPECTED OUTPUT:
# BasicSum
# 30
# EvenSum
# 20
# 70
# 12
# 2
```

## Error

```
Assembly compilation failed:
  dogfood_test.cs(65,21): error CS0103: The name 'i' does not exist in the current context
  dogfood_test.cs(66,25): error CS0103: The name 'i' does not exist in the current context
  dogfood_test.cs(68,45): error CS0103: The name 'i' does not exist in the current context

```

## Timing

- Generation: 10.53s
- Execution: 1.25s
