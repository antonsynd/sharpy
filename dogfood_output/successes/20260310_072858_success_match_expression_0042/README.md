# Successful Dogfood Run

**Timestamp:** 2026-03-10T07:24:21.766889
**Feature Focus:** match_expression
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex match expressions with inheritance, enums, and guard clauses

enum Category:
    TINY = 1
    SMALL = 2
    MEDIUM = 3
    LARGE = 4
    HUGE = 5

@abstract
class Widget:
    @abstract
    def weight(self) -> float: ...

class Button(Widget):
    _weight: float
    def __init__(self, w: float):
        self._weight = w
    @override
    def weight(self) -> float:
        return self._weight

class Slider(Widget):
    _weight: float
    _length: float
    def __init__(self, w: float, l: float):
        self._weight = w
        self._length = l
    @override
    def weight(self) -> float:
        return self._weight
    def length(self) -> float:
        return self._length

class Panel(Widget):
    items: list[Widget]
    _base: float
    def __init__(self, b: float):
        self._base = b
        self.items = []
    @override
    def weight(self) -> float:
        total = self._base
        for i in self.items:
            total += i.weight()
        return total
    def add(self, w: Widget) -> None:
        self.items.append(w)

def categorize(w: Widget) -> Category:
    return match w:
        case Button() as b if b.weight() < 10.0: Category.TINY
        case Button() as b if b.weight() < 50.0: Category.SMALL
        case Button(): Category.MEDIUM
        case Slider() as s if s.length() > 100.0: Category.LARGE
        case Slider(): Category.MEDIUM
        case Panel() as p if len(p.items) == 0: Category.TINY
        case Panel() as p if len(p.items) > 5: Category.HUGE
        case Panel(): Category.SMALL
        case _: Category.MEDIUM

def main():
    b1 = Button(5.0)
    b2 = Button(30.0)
    b3 = Button(75.0)
    s1 = Slider(40.0, 50.0)
    s2 = Slider(60.0, 200.0)
    p1 = Panel(10.0)
    p2 = Panel(10.0)
    p2.add(b1)
    p2.add(b2)
    
    items: list[Widget] = [b1, b2, b3, s1, s2, p1, p2]
    for item in items:
        cat = categorize(item)
        wt = item.weight()
        print(f"{cat.value},{wt}")

```

## Output

```
1,5.0
2,30.0
3,75.0
3,40.0
4,60.0
1,10.0
2,45.0
```

## Timing

- Generation: 265.15s
- Execution: 5.45s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
