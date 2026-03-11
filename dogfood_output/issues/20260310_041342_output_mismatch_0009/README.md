# Issue Report: output_mismatch

**Timestamp:** 2026-03-10T04:06:35.476403
**Type:** output_mismatch
**Feature Focus:** while_loop
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
@abstract
class SearchStrategy:
    @abstract
    def should_keep(self, item: int) -> bool:
        ...

    def search(self, items: list[int]) -> list[int]:
        result: list[int] = []
        idx: int = 0
        while idx < len(items):
            current: int = items[idx]
            if self.should_keep(current):
                result.append(current)
            idx += 1
        return result

class GreaterThan(SearchStrategy):
    threshold: int

    def __init__(self, threshold: int):
        self.threshold = threshold

    @override
    def should_keep(self, item: int) -> bool:
        return item > self.threshold

class EvenFilter:
    def should_keep(self, item: int) -> bool:
        return item % 2 == 0

    def search(self, items: list[int]) -> list[int]:
        result: list[int] = []
        i: int = 0
        while i < len(items):
            if self.should_keep(items[i]):
                result.append(items[i])
            i += 1
        return result

class StringPrefixChecker:
    prefix: str

    def __init__(self, prefix: str):
        self.prefix = prefix

    def matches(self, item: str) -> bool:
        if len(item) < len(self.prefix):
            return False
        i: int = 0
        while i < len(self.prefix):
            if str(item)[i] != str(self.prefix)[i]:
                return False
            i += 1
        return True

def custom_filter(items: list[int], predicate: (int) -> bool) -> list[int]:
    result: list[int] = []
    i: int = 0
    while i < len(items):
        if predicate(items[i]):
            result.append(items[i])
        i += 1
    return result

def factorial(n: int) -> int:
    if n < 0:
        return 0
    result: int = 1
    i: int = 2
    while i <= n:
        result *= i
        i += 1
    return result

def gcd(a: int, b: int) -> int:
    x: int = a
    y: int = b
    while y != 0:
        temp: int = y
        y = x % y
        x = temp
    return x

def is_prime(n: int) -> bool:
    if n < 2:
        return False
    if n == 2:
        return True
    if n % 2 == 0:
        return False
    i: int = 3
    while i * i <= n:
        if n % i == 0:
            return False
        i += 2
    return True

def main():
    numbers: list[int] = [3, 15, 8, 2, 20, 7, 25]
    gt_strategy: GreaterThan = GreaterThan(10)
    filtered_nums: list[int] = gt_strategy.search(numbers)
    print(len(filtered_nums))
    
    idx: int = 0
    while idx < len(filtered_nums):
        print(filtered_nums[idx])
        idx += 1
    
    evens: list[int] = custom_filter(numbers, lambda x: x % 2 == 0)
    print(len(evens))
    
    even_filter: EvenFilter = EvenFilter()
    even_nums: list[int] = even_filter.search(numbers)
    print(len(even_nums))
    
    fact5: int = factorial(5)
    print(fact5)
    
    fact7: int = factorial(7)
    print(fact7)
    
    gcd_result: int = gcd(48, 18)
    print(gcd_result)
    
    gcd_result2: int = gcd(100, 35)
    print(gcd_result2)
    
    primes_found: list[int] = []
    i = 1
    while i <= 30:
        if is_prime(i):
            primes_found.append(i)
        i += 1
    
    print(len(primes_found))
    
    j: int = 0
    while j < len(primes_found):
        print(primes_found[j])
        j += 1
    
    checker: StringPrefixChecker = StringPrefixChecker("app")
    word1: str = "apple"
    word2: str = "banana"
    word3: str = "application"
    
    if checker.matches(word1):
        print(word1)
    if not checker.matches(word2):
        print(word2)
    if checker.matches(word3):
        print(word3)
    
    total: int = 0
    k: int = 1
    while k <= 100:
        if k % 3 == 0:
            total += k
        k += 1
    print(total)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
3
15
20
25
3
2
120
5040
6
5
10
2
3
5
7
11
13
17
19
23
29
apple
banana
application
168

```

### Actual
```
3
15
20
25
3
3
120
5040
6
5
10
2
3
5
7
11
13
17
19
23
29
apple
banana
application
1683
```

## Timing

- Generation: 233.93s
- Execution: 5.60s
