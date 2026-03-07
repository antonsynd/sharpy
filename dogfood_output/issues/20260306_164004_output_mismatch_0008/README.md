# Issue Report: output_mismatch

**Timestamp:** 2026-03-06T16:25:35.079940
**Type:** output_mismatch
**Feature Focus:** tuple_unpacking_nested
**Complexity:** complex
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Complex nested tuple unpacking with type aliases and inheritance
# Combines named tuples, matrix operations, and class-based data sources
# FIXED: Use explicit tuple types without named fields for compatibility

type Point2D = tuple[float, float]
type Point3D = tuple[float, float, float]
type DataPair = tuple[int, str]

class DataSource:
    @virtual
    def get_pairs(self) -> list[tuple[DataPair, DataPair]]:
        first: DataPair = (0, "zero")
        second: DataPair = (1, "one")
        result: list[tuple[DataPair, DataPair]] = [(first, second)]
        return result

class CustomSource(DataSource):
    offset: int
    
    def __init__(self, offset: int):
        self.offset = offset
    
    @override
    def get_pairs(self) -> list[tuple[DataPair, DataPair]]:
        first: DataPair = (self.offset, "start")
        second: DataPair = (self.offset + 10, "end")
        result: list[tuple[DataPair, DataPair]] = [(first, second)]
        return result

def extract_coordinates(pt: Point3D) -> Point3D:
    x_val: float = pt[0]
    y_val: float = pt[1]
    z_val: float = pt[2]
    return_pt: Point3D = (x_val, y_val, z_val)
    return return_pt

def main():
    # Level 1: Matrix extraction with nested unpacking
    matrix: tuple[tuple[int, int], tuple[int, int]] = ((1, 2), (3, 4))
    row0: tuple[int, int] = matrix[0]
    row1: tuple[int, int] = matrix[1]
    a00: int = row0[0]
    a01: int = row0[1]
    a10: int = row1[0]
    a11: int = row1[1]
    print(a00)
    print(a01)
    print(a10)
    print(a11)
    
    # Level 2: Triple-nested tuple decomposition
    triple: tuple[tuple[int, int], tuple[int, int], tuple[int, int]] = ((5, 6), (7, 8), (9, 10))
    t1: tuple[int, int] = triple[0]
    t2: tuple[int, int] = triple[1]
    t3: tuple[int, int] = triple[2]
    t1a: int = t1[0]
    t1b: int = t1[1]
    t2a: int = t2[0]
    t2b: int = t2[1]
    t3a: int = t3[0]
    t3b: int = t3[1]
    result: int = t1a + t2b + t3a
    print(result)
    
    # Level 3: Named point unpacking (use index access)
    origin: Point2D = (0.0, 0.0)
    target: Point2D = (50.0, 100.0)
    o_x: float = origin[0]
    o_y: float = origin[1]
    t_x: float = target[0]
    t_y: float = target[1]
    print(o_x)
    print(t_y)
    
    # Level 4: Swapping nested tuples (using temp variable)
    left: tuple[int, int] = (100, 200)
    right: tuple[int, int] = (300, 400)
    # Swap using temporary variable
    temp: tuple[int, int] = left
    left = right
    right = temp
    print(left[0])
    print(right[1])
    
    # Level 5: Class inheritance with tuple returns
    source = CustomSource(5)
    pairs: list[tuple[DataPair, DataPair]] = source.get_pairs()
    pair0: tuple[DataPair, DataPair] = pairs[0]
    pair_a: DataPair = pair0[0]
    pair_b: DataPair = pair0[1]
    id_a: int = pair_a[0]
    name_a: str = pair_a[1]
    id_b: int = pair_b[0]
    name_b: str = pair_b[1]
    print(id_a)
    print(id_b)
    print(name_a)
    
    # Level 6: Mixed depth nested tuples
    complex_data: tuple[Point3D, tuple[int, int]] = ((1.0, 2.0, 3.0), (10, 20))
    point_part: Point3D = complex_data[0]
    int_part: tuple[int, int] = complex_data[1]
    p_x: float = point_part[0]
    p_y: float = point_part[1]
    p_z: float = point_part[2]
    i1: int = int_part[0]
    i2: int = int_part[1]
    print(p_z)
    sum_result: int = i1 + i2
    print(sum_result)
    
    # Level 7: Extract and repack
    returned_pt: Point3D = extract_coordinates((7.5, 8.5, 9.5))
    py: float = returned_pt[1]
    print(py)

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
1
2
3
4
20
0.0
100.0
300
400
5
15
start
3.0
30
8.5

```

### Actual
```
1
2
3
4
22
0.0
100.0
300
200
5
15
start
3.0
30
8.5
```

## Timing

- Generation: 675.72s
- Execution: 4.60s
