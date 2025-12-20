## Enumerations

### Simple Enums

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

enum HttpMethod:
    GET = "GET"
    POST = "POST"
    PUT = "PUT"
    DELETE = "DELETE"

# Usage
favorite = Color.RED
if favorite == Color.RED:
    print("Red is your favorite")

# Access underlying value
value = favorite.value  # 1
name = favorite.name    # "RED"
```

**Rules:**
- All cases must have explicit constant values
- All values must be of the same type, either an integer type or the `str` type.
- Enums must have at least one case

**Enum Iteration and Methods:**

```python
enum Color:
    RED = 1
    GREEN = 2
    BLUE = 3

# Iterate over all enum values
for color in Color:
    print(f"{color.name} = {color.value}")
# Output:
# RED = 1
# GREEN = 2
# BLUE = 3

# Get all values as a list
all_colors: list[Color] = list(Color)

# Get all names
names: list[str] = [c.name for c in Color]  # ["RED", "GREEN", "BLUE"]

# Get all values
values: list[int] = [c.value for c in Color]  # [1, 2, 3]
```

**Note:** Simple enums (non-tagged unions) cannot have custom methods. For enums with methods, use tagged unions.

*Implementation:*
- *Integer enums: ✅ Native - C# `enum`*
- *String enums: 🔄 Lowered - Static class with string constants*
- *`.name` property: 🔄 Lowered - `Enum.GetName()` or lookup*

---
