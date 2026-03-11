# Successful Dogfood Run

**Timestamp:** 2026-03-10T15:58:34.372523
**Feature Focus:** tuple_types
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Complex named tuple types with geometric calculations
type Point = tuple[x: float, y: float]
type Dimension = tuple[width: float, height: float]
type PlacedRect = tuple[position: Point, size: Dimension, id: int]

class RectangleManager:
    rectangles: list[PlacedRect]
    next_id: int
    
    def __init__(self):
        self.rectangles = []
        self.next_id = 1
    
    def create(self, x: float, y: float, w: float, h: float) -> PlacedRect:
        pos: Point = (x=x, y=y)
        dim: Dimension = (width=w, height=h)
        rect: PlacedRect = (position=pos, size=dim, id=self.next_id)
        self.next_id += 1
        self.rectangles.append(rect)
        return rect
    
    def find_by_id(self, search_id: int) -> PlacedRect?:
        i: int = 0
        while i < len(self.rectangles):
            r: PlacedRect = self.rectangles[i]
            if r.id == search_id:
                return Some(r)
            i += 1
        return None()
    
    def bounding_box(self) -> tuple[min: Point, max: Point]:
        if len(self.rectangles) == 0:
            return (min=(x=0.0, y=0.0), max=(x=0.0, y=0.0))
        
        min_x: float = self.rectangles[0].position.x
        min_y: float = self.rectangles[0].position.y
        max_x: float = min_x + self.rectangles[0].size.width
        max_y: float = min_y + self.rectangles[0].size.height
        
        i: int = 1
        while i < len(self.rectangles):
            r: PlacedRect = self.rectangles[i]
            x1: float = r.position.x
            y1: float = r.position.y
            x2: float = x1 + r.size.width
            y2: float = y1 + r.size.height
            
            if x1 < min_x:
                min_x = x1
            if y1 < min_y:
                min_y = y1
            if x2 > max_x:
                max_x = x2
            if y2 > max_y:
                max_y = y2
            
            i += 1
        
        return (min=(x=min_x, y=min_y), max=(x=max_x, y=max_y))
    
    def total_area(self) -> float:
        total: float = 0.0
        for rect in self.rectangles:
            total += rect.size.width * rect.size.height
        return total

def main():
    manager = RectangleManager()
    
    # Create rectangles with named tuples
    r1: PlacedRect = manager.create(0.0, 0.0, 10.0, 5.0)
    r2: PlacedRect = manager.create(5.0, 3.0, 8.0, 4.0)
    
    # Access nested named tuple fields
    print(r1.position.x)
    print(r1.size.height)
    print(r2.id)
    
    # Find by id and access named tuple fields
    found: PlacedRect? = manager.find_by_id(1)
    if found is not None:
        print(found.position.y)
        print(found.size.width)
    else:
        print("missing")
    
    # Calculate total area
    print(manager.total_area())
    
    # Get bounding box as named tuples
    bounds: tuple[min: Point, max: Point] = manager.bounding_box()
    print(bounds.min.x)
    print(bounds.max.x)

```

## Output

```
0.0
5.0
2
0.0
10.0
82.0
0.0
13.0
```

## Timing

- Generation: 181.44s
- Execution: 5.41s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
