# Issue Report: output_mismatch

**Timestamp:** 2026-02-24T06:19:26.689268
**Type:** output_mismatch
**Feature Focus:** interface_implementation
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
interface IResizable:
    def resize(self, scale: float) -> None: ...

interface IDescribable:
    def describe(self) -> str: ...
    property label: str

class AdaptiveBox(IResizable, IDescribable):
    property width: float = 10.0
    property height: float = 10.0
    property label: str = "box"

    def __init__(self, w: float, h: float, name: str):
        self.width = w
        self.height = h
        self.label = name

    def resize(self, scale: float) -> None:
        self.width = self.width * scale
        self.height = self.height * scale

    def describe(self) -> str:
        return f"{self.label}: {self.width:.1f} x {self.height:.1f}"

def make_square(item: IResizable, size: float) -> None:
    if size > 0.0:
        item.resize(size / 10.0)

def main():
    box = AdaptiveBox(20.0, 30.0, "widget")
    print(box.describe())
    make_square(box, 50.0)
    print(box.describe())
    if box.width == box.height:
        print("is_square")
    else:
        print("not_square")
    print(box.label)

# EXPECTED OUTPUT:
# widget: 20.0 x 30.0
# widget: 50.0 x 50.0
# is_square
# widget
```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
widget: 20.0 x 30.0
widget: 50.0 x 50.0
is_square
widget

```

### Actual
```
widget: 20.0 x 30.0
widget: 100.0 x 150.0
not_square
widget
```

## Timing

- Generation: 233.55s
- Execution: 4.61s
