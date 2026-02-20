# Successful Dogfood Run

**Timestamp:** 2026-02-19T20:38:14.850609
**Feature Focus:** bool_variables
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Boolean variables with short-circuit evaluation and logical operators
class PermissionChecker:
    has_read: bool
    has_write: bool
    has_execute: bool

    def __init__(self, read: bool, write: bool, execute: bool):
        self.has_read = read
        self.has_write = write
        self.has_execute = execute

    def can_modify(self) -> bool:
        return self.has_read and self.has_write

    def has_any_permission(self) -> bool:
        return self.has_read or self.has_write or self.has_execute

    def is_read_only(self) -> bool:
        return self.has_read and not self.has_write and not self.has_execute

def main():
    admin = PermissionChecker(True, True, True)
    viewer = PermissionChecker(True, False, False)
    editor = PermissionChecker(True, True, False)

    print(admin.can_modify())
    print(admin.has_any_permission())
    print(admin.is_read_only())

    print(viewer.can_modify())
    print(viewer.has_any_permission())
    print(viewer.is_read_only())

    print(editor.can_modify())
    print(editor.is_read_only())

# EXPECTED OUTPUT:
# True
# True
# False
# False
# True
# True
# True
# False
```

## Output

```
True
True
False
False
True
True
True
False
```

## Timing

- Generation: 256.33s
- Execution: 4.17s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
