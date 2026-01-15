# Implementation Plan: Task 0.1.7.3 - `super()` Semantic Validation

## Overview

This task implements semantic validation for `super()` calls in the TypeChecker. The parsing is already complete (Task 0.1.7.2), producing a `SuperExpression` AST node that composes with `MemberAccess` and `FunctionCall` nodes.

## Key Files to Modify

| File | Purpose |
|------|---------|
| `src/Sharpy.Compiler/Semantic/TypeChecker.cs` | Main implementation - add super() validation |
| `src/Sharpy.Compiler.Tests/Semantic/SuperValidationTests.cs` | New test file for super() validation tests |

## Step-by-Step Implementation

### Step 1: Add Method Context Tracking Fields

Add fields to `TypeChecker` to track the current method context:

```csharp
// Add after line 34 (after _inExceptBlock)
private string? _currentMethodName = null;
private bool _currentMethodIsOverride = false;
private bool _currentMethodIsDunder = false;
private int _controlFlowDepth = 0;
private bool _superInitCalled = false;  // Track if super().__init__() was called
```

### Step 2: Add Helper Method for Dunder Detection

Add a helper method (near line 2200):

```csharp
private static bool IsDunderMethod(string name) =>
    name.StartsWith("__") && name.EndsWith("__") && name.Length > 4;
```

### Step 3: Update `CheckFunction` to Set Method Context

Modify `CheckFunction` (starting at line 171) to set the method context fields:

```csharp
// Inside CheckFunction, after entering scope (line 209):
var previousMethodName = _currentMethodName;
var previousMethodIsOverride = _currentMethodIsOverride;
var previousMethodIsDunder = _currentMethodIsDunder;
var previousControlFlowDepth = _controlFlowDepth;
var previousSuperInitCalled = _superInitCalled;

_currentMethodName = functionDef.Name;
_currentMethodIsOverride = functionDef.Decorators.Any(d => d.Name == "override");
_currentMethodIsDunder = IsDunderMethod(functionDef.Name);
_controlFlowDepth = 0;
_superInitCalled = false;

// ... existing body checking code ...

// Before ExitScope (line 323):
_currentMethodName = previousMethodName;
_currentMethodIsOverride = previousMethodIsOverride;
_currentMethodIsDunder = previousMethodIsDunder;
_controlFlowDepth = previousControlFlowDepth;
_superInitCalled = previousSuperInitCalled;
```

### Step 4: Track Control Flow Depth

Increment/decrement `_controlFlowDepth` in control flow statements:

In `CheckIf` (line 695):
```csharp
_controlFlowDepth++;
// ... existing code for checking then/elif/else bodies ...
_controlFlowDepth--;
```

Similarly in `CheckWhile`, `CheckFor`, and `CheckTry`.

### Step 5: Add SuperExpression Case in `CheckExpression`

Add to the switch statement in `CheckExpression` (after line 986):

```csharp
SuperExpression superExpr => CheckSuperExpression(superExpr),
```

### Step 6: Implement `CheckSuperExpression` Method

Add a new method to validate standalone `SuperExpression` (which is always an error since super() must be followed by `.method()`):

```csharp
private SemanticType CheckSuperExpression(SuperExpression superExpr)
{
    // Standalone super() is not valid - must be used as super().method()
    // The parser allows it, but semantically it's invalid
    AddError("super() must be followed by a method call (e.g., super().__init__())",
        superExpr.LineStart, superExpr.ColumnStart);
    return SemanticType.Unknown;
}
```

### Step 7: Update `CheckMemberAccess` for Super Validation

Modify `CheckMemberAccess` (line 1388) to detect and validate `super()` member access:

```csharp
private SemanticType CheckMemberAccess(MemberAccess memberAccess)
{
    // Check for super() usage
    if (memberAccess.Object is SuperExpression superExpr)
    {
        return ValidateSuperMemberAccess(memberAccess, superExpr);
    }

    // ... existing code ...
}
```

### Step 8: Implement `ValidateSuperMemberAccess` Method

Add a comprehensive validation method:

```csharp
private SemanticType ValidateSuperMemberAccess(MemberAccess memberAccess, SuperExpression superExpr)
{
    var memberName = memberAccess.Member;

    // Check 1: Must be inside a class
    if (_currentClass == null)
    {
        AddError("super() cannot be used outside of a class",
            superExpr.LineStart, superExpr.ColumnStart);
        return SemanticType.Unknown;
    }

    // Check 2: Class must have a parent
    if (_currentClass.BaseType == null)
    {
        AddError($"super() cannot be used in class '{_currentClass.Name}' which has no parent class",
            superExpr.LineStart, superExpr.ColumnStart);
        return SemanticType.Unknown;
    }

    // Check 3: Cannot access fields via super()
    var parentField = _currentClass.BaseType.Fields.FirstOrDefault(f => f.Name == memberName);
    if (parentField != null)
    {
        AddError("Cannot access parent fields via super(); only methods are allowed",
            memberAccess.LineStart, memberAccess.ColumnStart);
        return SemanticType.Unknown;
    }

    // Check 4: Validate based on method context
    ValidateSuperContextRules(memberName, superExpr, memberAccess);

    // Look up the method in the parent class and return its type
    var parentMethod = _currentClass.BaseType.Methods.FirstOrDefault(m => m.Name == memberName);
    if (parentMethod == null && memberName == "__init__")
    {
        // __init__ might be in Constructors list
        var parentCtor = _currentClass.BaseType.Constructors.FirstOrDefault();
        if (parentCtor != null)
        {
            var paramTypes = parentCtor.Parameters.Skip(1).Select(p => p.Type).ToList();
            return new FunctionType
            {
                ParameterTypes = paramTypes,
                ReturnType = SemanticType.Void
            };
        }
    }

    if (parentMethod != null)
    {
        var paramTypes = parentMethod.Parameters.Skip(1).Select(p => p.Type).ToList();
        return new FunctionType
        {
            ParameterTypes = paramTypes,
            ReturnType = parentMethod.ReturnType
        };
    }

    AddError($"Parent class '{_currentClass.BaseType.Name}' has no method '{memberName}'",
        memberAccess.LineStart, memberAccess.ColumnStart);
    return SemanticType.Unknown;
}
```

### Step 9: Implement Context Rules Validation

```csharp
private void ValidateSuperContextRules(string calledMethodName, SuperExpression superExpr, MemberAccess memberAccess)
{
    // Check for chained super() - super().super() is not allowed
    // This would be MemberAccess(Object: MemberAccess(Object: SuperExpression, Member: X), Member: "super")
    // But since super() is a function call, chained would be detected differently
    // Actually, super().super() would parse as MemberAccess(Object: FunctionCall(Function: MemberAccess(...)), Member: "super")

    if (_currentMethodName == null)
    {
        AddError("super() cannot be used outside of a method",
            superExpr.LineStart, superExpr.ColumnStart);
        return;
    }

    // Case 1: Inside __init__
    if (_currentMethodName == "__init__")
    {
        if (calledMethodName != "__init__")
        {
            AddError("super() in __init__ can only call super().__init__(...)",
                memberAccess.LineStart, memberAccess.ColumnStart);
        }
        else if (_controlFlowDepth > 0)
        {
            AddError("super().__init__() must be the first statement in the constructor, not inside control flow",
                superExpr.LineStart, superExpr.ColumnStart);
        }
        else if (_superInitCalled)
        {
            AddError("super().__init__() can only be called once",
                superExpr.LineStart, superExpr.ColumnStart);
        }
        return;
    }

    // Case 2: Inside @override method
    if (_currentMethodIsOverride)
    {
        // In @override methods, can call same method name
        // OR if it's a dunder override, can call other dunders (cross-dunder)
        if (calledMethodName != _currentMethodName)
        {
            if (!(_currentMethodIsDunder && IsDunderMethod(calledMethodName)))
            {
                AddError($"super() in @override method must call super().{_currentMethodName}(...)",
                    memberAccess.LineStart, memberAccess.ColumnStart);
            }
        }
        return;
    }

    // Case 3: Inside dunder method (not __init__, not @override)
    if (_currentMethodIsDunder)
    {
        // Dunder methods can call any dunder via super()
        if (!IsDunderMethod(calledMethodName))
        {
            AddError("super() in dunder method must call a dunder method (e.g., super().__eq__(...))",
                memberAccess.LineStart, memberAccess.ColumnStart);
        }
        return;
    }

    // Case 4: Regular method - super() not allowed
    AddError("super() cannot be used in regular methods; only in __init__, @override, or dunder methods",
        superExpr.LineStart, superExpr.ColumnStart);
}
```

### Step 10: Track super().__init__() Calls

Add logic to track when `super().__init__()` is called in CheckFunctionCall or after returning from ValidateSuperMemberAccess:

```csharp
// In CheckFunctionCall, after validating a super() call:
if (call.Function is MemberAccess ma && ma.Object is SuperExpression && ma.Member == "__init__")
{
    _superInitCalled = true;
}
```

## Tests to Add

Create `src/Sharpy.Compiler.Tests/Semantic/SuperValidationTests.cs`:

### Valid Usage Tests (should pass without errors)
1. `super().__init__(...)` in `__init__` method
2. `super().method()` in `@override method` with same name
3. `super().__eq__(...)` in `__eq__` dunder method
4. Cross-dunder: `super().__lt__(...)` in `__le__` dunder method with `@override`
5. `super().__init__()` as first statement (not in control flow)

### Invalid Usage Tests (should produce errors)
1. `super()` in free function → "cannot be used outside of a class"
2. `super()` in class with no parent → "no parent class"
3. `super()` in regular method → "only in __init__, @override, or dunder"
4. `super().field` accessing parent field → "only methods are allowed"
5. `super().__init__()` in `__init__` inside `if` block → "not inside control flow"
6. `super().other_method()` in `__init__` → "can only call super().__init__()"
7. `super().wrong_name()` in `@override def process()` → "must call super().process()"
8. `super().non_dunder()` in dunder method → "must call a dunder method"

## Potential Risks

1. **Field tracking complexity**: The `_controlFlowDepth` tracking must be carefully managed across all control flow statements (if/while/for/try).

2. **First statement validation**: Detecting if `super().__init__()` is the "first statement" requires checking the statement index in the method body, not just control flow depth. Current approach may miss cases like:
   ```python
   def __init__(self):
       x = 1
       super().__init__()  # Not first statement, but not in control flow
   ```
   This needs additional tracking or validation during body traversal.

3. **Chained super() detection**: The AST structure makes `super().super()` detection non-trivial since `super()` returns a FunctionType, not a SuperExpression.

4. **@override without parent method**: Should validate that the parent class actually has the method being overridden.

## Questions to Clarify

1. **First statement strictness**: Should `super().__init__()` be strictly the first statement, or just not inside control flow? The spec says "first statement unconditionally" but the implementation hint focuses on control flow.

2. **Multiple super() calls**: In non-__init__ methods, is calling super() multiple times allowed? (e.g., calling super() twice in an @override method)

3. **Static methods**: Should super() in static methods produce a specific error message different from regular methods?
