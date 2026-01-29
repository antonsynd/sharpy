# Successful Dogfood Run

**Timestamp:** 2026-01-29T00:05:56.143998
**Feature Focus:** bool_variables
**Complexity:** medium
**Backend:** claude

## Generated Sharpy Code

```python
# Test boolean variables with validation system

class ValidationResult:
    is_valid: bool
    is_complete: bool
    has_errors: bool
    
    def __init__(self, valid: bool, complete: bool, errors: bool):
        self.is_valid = valid
        self.is_complete = complete
        self.has_errors = errors
    
    def is_approved(self) -> bool:
        return self.is_valid and self.is_complete and not self.has_errors
    
    def needs_review(self) -> bool:
        return not self.is_valid or self.has_errors

class FormValidator:
    def validate_submission(self, has_name: bool, has_email: bool, has_consent: bool) -> ValidationResult:
        valid = has_name and has_email
        complete = has_name and has_email and has_consent
        errors = not has_name or not has_email
        
        return ValidationResult(valid, complete, errors)

def main():
    validator = FormValidator()
    
    result1 = validator.validate_submission(True, True, True)
    print(result1.is_approved())
    print(result1.needs_review())
    
    result2 = validator.validate_submission(True, False, True)
    print(result2.is_approved())
    print(result2.needs_review())
    
    result3 = validator.validate_submission(True, True, False)
    print(result3.is_approved())
    print(result3.needs_review())

# EXPECTED OUTPUT:
# True
# False
# False
# True
# False
# False
```

## Output

```
True
False
False
True
False
False
```

## Timing

- Generation: 9.00s
- Execution: 1.46s

## Converting to Integration Test

To convert this to an integration test, run:

```bash
python -m sharpy_dogfood convert <this_directory_name>
```
