# Debugger Validation Test Fixture

This multi-file project exercises cross-module calls, class methods, control flow, and exception handling to validate that `#line` directives produce correct debugger behavior.

## Files

- `main.spy` - Entry point: imports from `models` and `utils`, exercises cross-module calls
- `models.spy` - Class definition with constructor, method, and if/else branching
- `utils.spy` - Functions with for loops, try/except, and exception handling

## Debugger Testing Checklist

### Prerequisites

1. Build the project with debug symbols enabled (default for `dotnet build`)
2. Open the project in VS Code or Rider
3. Ensure the Sharpy VS Code extension or Rider plugin is installed

### VS Code

#### Breakpoint Setting
- [ ] Set a breakpoint on `main.spy` line 6 (`upper_items = transform_all(items)`)
- [ ] Set a breakpoint on `utils.spy` line 3 (`for item in items:`)
- [ ] Set a breakpoint on `models.spy` line 10 (`if self._age >= 18:`)
- [ ] Set a breakpoint on `utils.spy` line 9 (`return a // b`)
- [ ] Verify all breakpoints show as solid red circles (not hollow/unverified)

#### Stepping Through Cross-Module Calls
- [ ] Start debugging from `main.spy`
- [ ] Step into `transform_all(items)` on line 6 — should land in `utils.spy` line 1
- [ ] Step through the for loop in `utils.spy` — verify line numbers advance correctly
- [ ] Step out — should return to `main.spy` line 7 (`for item in upper_items:`)
- [ ] Continue to the `Person("Alice", 30)` call on line 10
- [ ] Step into the constructor — should land in `models.spy` line 5
- [ ] Step into `p.greet()` — should land in `models.spy` line 10
- [ ] Verify the if branch is taken (age 30 >= 18)

#### Exception Handling Flow
- [ ] Continue to `safe_divide(10, 0)` on line 14
- [ ] Step into — should land in `utils.spy` line 8 (`try:`)
- [ ] Step through — should enter except block at line 10
- [ ] Verify the ZeroDivisionError is caught, not propagated

#### Variable Inspection
- [ ] At `main.spy` line 7 breakpoint: verify `items` shows `["hello", "world", "sharpy"]`
- [ ] At `main.spy` line 7: verify `upper_items` shows `["HELLO", "WORLD", "SHARPY"]`
- [ ] At `models.spy` line 10: verify `self._name` shows `"Alice"` and `self._age` shows `30`
- [ ] At `utils.spy` line 9: verify `a` shows `10` and `b` shows `0`

#### Call Stack Verification
- [ ] When stopped inside `transform_all`: call stack shows `utils.spy` then `main.spy`
- [ ] When stopped inside `greet`: call stack shows `models.spy` then `main.spy`
- [ ] When stopped inside `safe_divide`: call stack shows `utils.spy` then `main.spy`
- [ ] All call stack frames reference `.spy` file names, not `.cs` file names

### JetBrains Rider

#### Breakpoint Setting
- [ ] Set breakpoints on the same lines as the VS Code checklist above
- [ ] Verify breakpoints are recognized (solid red circles in the gutter)

#### Stepping Through Cross-Module Calls
- [ ] Start debugging from `main.spy`
- [ ] Use Step Into (F7) to enter `transform_all` — should navigate to `utils.spy`
- [ ] Use Step Over (F8) to advance through the for loop
- [ ] Use Step Out (Shift+F8) to return to `main.spy`
- [ ] Step into `Person()` constructor and `greet()` method — should navigate to `models.spy`

#### Exception Handling Flow
- [ ] Step into `safe_divide(10, 0)` — should navigate to `utils.spy`
- [ ] Verify the exception is caught by the except handler
- [ ] If "Break on exception" is enabled, verify it shows `ZeroDivisionError` with correct source location

#### Variable Inspection
- [ ] Verify local variables are visible in the Variables panel at each breakpoint
- [ ] Verify object fields (`self._name`, `self._age`) are inspectable in `models.spy`
- [ ] Verify list contents are inspectable in `main.spy`

#### Call Stack Verification
- [ ] All frames in the Frames panel reference `.spy` file names
- [ ] Clicking a frame navigates to the correct `.spy` file and line
- [ ] No frames reference generated `.cs` files

### Verification Summary

| Check | VS Code | Rider |
|-------|---------|-------|
| Breakpoints hit on correct lines | | |
| Step Into crosses module boundaries | | |
| Step Out returns to caller correctly | | |
| Call stack shows .spy file names | | |
| Call stack shows correct line numbers | | |
| Variables visible at breakpoints | | |
| Object fields inspectable | | |
| Exception caught at correct location | | |
