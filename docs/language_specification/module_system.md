# Module System

## Package Structure

Packages are directories containing an optional `__init__.spy` file:

```
project/
    utils/
        __init__.spy      # Optional, can be empty
        helpers.spy
        math/
            __init__.spy
            vectors.spy
```

The `__init__.spy` file can re-export symbols for convenient imports:

```python
# utils/__init__.spy
from utils.helpers import format_string, parse_input
from utils.math.vectors import Vector2, Vector3
```

## Circular Import Handling

Circular imports are resolved through forward references in type annotations:

```python
# module_a.spy
from module_b import ClassB  # Forward reference for type annotation

class ClassA:
    other: ClassB  # OK - used only as type annotation

    def use_b(self, b: ClassB) -> None:
        b.method()
```

**How Forward References Work:**

Sharpy resolves imports in two phases:
1. **Type declaration phase**: Type names are registered (forward references allowed)
2. **Type resolution phase**: Full type information is resolved

When an import is used only in type annotations (not at runtime during `import` because runtime imports do not exist in Sharpy), circular references work automatically. No special syntax is needed.

```python
# file: parent.spy
from child import Child  # Works because Child only used in type annotations

class Parent:
    children: list[Child]  # Type annotation - resolved later

    def add_child(self, c: Child) -> None:  # Type annotation
        self.children.append(c)

# file: child.spy
from parent import Parent  # Works because Parent only used in type annotations

class Child:
    parent: Parent?  # Type annotation - resolved later
```

**Rules:**
- Circular references are allowed for type annotations
- Circular references for base classes are **not** allowed
- Import order matters: import for type hints processed before code execution
- If you get circular import errors, restructure to avoid runtime circular dependencies
