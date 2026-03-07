# Issue Report: output_mismatch

**Timestamp:** 2026-03-07T05:58:20.176539
**Type:** output_mismatch
**Feature Focus:** nullable_types
**Complexity:** medium
**Backend:** klaude

## Generated Sharpy Code

```python
# Test: Nullable types with user profile processing pipeline
# Tests: null coalescing (??), null conditional (?.), type narrowing,
#         nullable fields in classes, and chained operations

class UserProfile:
    name: str
    age: int?
    email: str?
    backup_email: str?
    
    def __init__(self, name: str):
        self.name = name
        self.age = None()
        self.email = None()
        self.backup_email = None()
    
    def get_primary_contact(self) -> str:
        # Null coalescing chain
        return self.email ?? self.backup_email ?? "no_contact@example.com"
    
    def get_display_age(self) -> int:
        # Type narrowing with is not None
        if self.age is not None:
            return self.age
        return 0

def create_profile_with_age(name: str, age: int) -> UserProfile:
    profile = UserProfile(name)
    profile.age = Some(age)
    return profile

def process_profile(profile: UserProfile) -> str:
    # Multiple nullable operations in one expression
    age_display: str = "unknown"
    if profile.age is not None:
        age_display = str(profile.age)
    
    # Build display string using null coalescing
    display: str = f"{profile.name} ({age_display})"
    
    # Access potentially nullable with fallback
    contact: str = profile.email ?? "N/A"
    display = display + f" - {contact}"
    
    return display

def main():
    # Create profiles with different combinations of nullable fields
    alice = create_profile_with_age("Alice", 30)
    
    # Direct field access and modification
    bob = UserProfile("Bob")
    bob.age = Some(25)
    bob.email = Some("bob@test.com")
    
    # Profile with minimal data
    carol = UserProfile("Carol")
    
    # Test null coalescing chains
    print(alice.get_primary_contact())
    
    # Test type narrowing in method
    print(alice.get_display_age())
    
    # Test with full processing
    print(process_profile(alice))
    print(process_profile(bob))
    print(process_profile(carol))
    
    # Test with only backup email set
    dave = UserProfile("Dave")
    dave.backup_email = Some("dave_backup@test.com")
    print(dave.get_primary_contact())

```

## Error

```
AI explicitly reported mismatch
```

## Output Comparison

### Expected
```
no_contact@example.com
30
Alice (30) - N/A
Bob (25) - bob@test.com
Carol (0) - N/A
dave_backup@test.com

```

### Actual
```
no_contact@example.com
30
Alice (30) - N/A
Bob (25) - bob@test.com
Carol (unknown) - N/A
dave_backup@test.com
```

## Timing

- Generation: 99.68s
- Execution: 4.76s
