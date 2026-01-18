# Successful Dogfood Run

**Timestamp:** 2026-01-18T13:00:12.720444
**Feature Focus:** bool_variables
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test boolean variables with logical operations and control flow

class AccessControl:
    is_admin: bool
    is_authenticated: bool
    has_permission: bool

    def __init__(self, admin: bool, authenticated: bool, permission: bool):
        self.is_admin = admin
        self.is_authenticated = authenticated
        self.has_permission = permission

    def can_access_resource(self) -> bool:
        return self.is_authenticated and (self.is_admin or self.has_permission)

    def get_access_level(self) -> int:
        level: int = 0
        if self.is_authenticated:
            level += 1
        if self.has_permission:
            level += 1
        if self.is_admin:
            level += 2
        return level

# Test case 1: Admin user
admin_user = AccessControl(True, True, True)
print(admin_user.can_access_resource())
print(admin_user.get_access_level())

# Test case 2: Regular authenticated user with permission
regular_user = AccessControl(False, True, True)
print(regular_user.can_access_resource())
print(regular_user.get_access_level())

# Test case 3: Authenticated user without permission
limited_user = AccessControl(False, True, False)
print(limited_user.can_access_resource())
print(limited_user.get_access_level())

# Test case 4: Unauthenticated user
guest_user = AccessControl(False, False, False)
print(guest_user.can_access_resource())
print(guest_user.get_access_level())

# Test boolean negation
is_locked: bool = True
is_open: bool = not is_locked
print(is_open)

# EXPECTED OUTPUT:
# True
# 4
# True
# 2
# False
# 1
# False
# 0
# False
```

## Output

```
Successfully compiled to: /var/folders/6r/tsfytt4x6s1cl4_t14c961040000gn/T/dogfood_test_e225f3b930b14868a9e85f56b9eedb29.exe

=== Running Program ===

True
4
True
2
False
1
False
0
False
```

## Timing

- Generation: 6.66s
- Execution: 1.65s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
