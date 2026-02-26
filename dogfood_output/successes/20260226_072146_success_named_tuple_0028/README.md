# Successful Dogfood Run

**Timestamp:** 2026-02-26T07:14:54.840906
**Feature Focus:** named_tuple
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex named tuple test with inheritance, generics, and unpacking
type Point3D = tuple[x: float, y: float, z: float]
type Dimension = tuple[width: float, height: float]
type Bounds = tuple[min: Point3D, max: Point3D]

interface IMeasurable:
    def get_center(self) -> Point3D: ...

@abstract
class Shape:
    dimensions: Dimension

    def __init__(self, dims: Dimension):
        self.dimensions = dims

    @abstract
    def get_bounds(self) -> Bounds: ...

    @virtual
    def get_center(self) -> Point3D:
        cx: float = self.dimensions.width / 2.0
        cy: float = self.dimensions.height / 2.0
        return (x=cx, y=cy, z=0.0)

class Rect3D(Shape, IMeasurable):
    depth: float

    def __init__(self, dims: Dimension, depth: float):
        super().__init__(dims)
        self.depth = depth

    @override
    def get_bounds(self) -> Bounds:
        w: float = self.dimensions.width
        h: float = self.dimensions.height
        d: float = self.depth
        min_pt: Point3D = (x=0.0, y=0.0, z=0.0)
        max_pt: Point3D = (x=w, y=h, z=d)
        return (min=min_pt, max=max_pt)

    @override
    def get_center(self) -> Point3D:
        cx: float = self.dimensions.width / 2.0
        cy: float = self.dimensions.height / 2.0
        cz: float = self.depth / 2.0
        return (x=cx, y=cy, z=cz)

def unpack_point(p: Point3D) -> float:
    px, py, pz = p
    return px + py + pz

def main():
    dims: Dimension = (width=10.0, height=20.0)
    rect: Rect3D = Rect3D(dims, depth=5.0)
    bounds: Bounds = rect.get_bounds()

    print(f"Width: {rect.dimensions.width}")
    print(f"Height: {rect.dimensions.height}")

    min_p: Point3D = bounds.min
    max_p: Point3D = bounds.max

    print(f"Min X: {min_p.x}")
    print(f"Min Y: {min_p.y}")
    print(f"Min Z: {min_p.z}")
    print(f"Max X: {max_p.x}")
    print(f"Max Y: {max_p.y}")
    print(f"Max Z: {max_p.z}")

    center: Point3D = rect.get_center()
    total: float = unpack_point(center)
    print(f"Center sum: {total}")
```

## Output

```
Width: 10.0
Height: 20.0
Min X: 0.0
Min Y: 0.0
Min Z: 0.0
Max X: 10.0
Max Y: 20.0
Max Z: 5.0
Center sum: 17.5
```

## Timing

- Generation: 397.12s
- Execution: 4.56s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
