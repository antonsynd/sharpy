# Issue Report: compilation_failed

**Timestamp:** 2026-01-17T09:44:49.662716
**Type:** compilation_failed
**Feature Focus:** nullable_types
**Complexity:** complex
**Backend:** claude

## Generated Sharpy Code

```python
# Complex nullable types test with inheritance and interfaces

interface IIdentifiable:
    def get_id(self) -> int?:
        ...

@abstract
class Entity:
    id: int?
    name: str?
    
    def __init__(self, id: int?, name: str?):
        self.id = id
        self.name = name
    
    @abstract
    def describe(self) -> str:
        ...
    
    @virtual
    def get_display_name(self) -> str:
        result: str = self.name ?? "Unknown"
        return result

class User(Entity, IIdentifiable):
    email: str?
    score: int?
    
    def __init__(self, id: int?, name: str?, email: str?, score: int?):
        super().__init__(id, name)
        self.email = email
        self.score = score
    
    @override
    def describe(self) -> str:
        return "User"
    
    @override
    def get_display_name(self) -> str:
        base_name: str = super().get_display_name()
        return base_name
    
    def get_id(self) -> int?:
        return self.id
    
    def get_score_or_default(self) -> int:
        return self.score ?? 0

def process_user(user: User?) -> None:
    if user is None:
        print("No user provided")
        return
    
    print("Processing user")
    user_id: int? = user.get_id()
    
    if user_id is not None:
        print(user_id)
    else:
        print("No ID")
    
    display: str = user.get_display_name()
    print(display)
    
    score: int = user.get_score_or_default()
    print(score)

def main():
    print("Testing nullable types")
    
    user1: User = User(42, "Alice", "alice@test.com", 100)
    process_user(user1)
    
    user2: User = User(None, None, None, None)
    process_user(user2)
    
    user3: User? = None
    process_user(user3)

main()

# EXPECTED OUTPUT:
# Testing nullable types
# Processing user
# 42
# Alice
# 100
# Processing user
# No ID
# Unknown
# 0
# No user provided
```

## Error

```
Compilation failed:
  Cannot have module-level executable statements when a 'main' function is defined. The main function is automatically invoked as the entry point.

```

## Timing

- Generation: 12.46s
- Execution: 0.91s
