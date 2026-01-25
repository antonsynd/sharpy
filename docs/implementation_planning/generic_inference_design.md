# Generic Type Inference Design Document

## Overview

This document describes the design for implicit generic type argument inference in Sharpy. Currently, the compiler requires explicit type arguments for generic functions:

```python
result = identity[int](42)      # Works - explicit type argument
```

This design enables implicit inference:

```python
result = identity(42)           # Should infer T=int
```

## Inference Algorithm

### 1. Constraint-Based Unification

The inference algorithm uses **constraint-based unification**, which works as follows:

1. **Collect Constraints**: For each type parameter, collect constraints from argument types
2. **Unify**: Attempt to unify each type parameter with concrete types from arguments
3. **Verify Constraints**: Check that inferred types satisfy any type constraints
4. **Substitute**: Apply the inferred types to the function signature

### 2. Inference Order

Type parameters are inferred **left-to-right** from arguments:

```python
def convert[T, U](value: T, converter: (T) -> U) -> U:
    return converter(value)

# Call: convert("42", int.parse)
# Step 1: value="42" → T binds to str
# Step 2: converter=int.parse : (str)->int → verify T=str, bind U=int
# Result: T=str, U=int
```

### 3. Type Unification Rules

The unification algorithm handles these cases:

| Formal Type | Actual Type | Result |
|-------------|-------------|--------|
| `T` | `int` | Bind T=int |
| `list[T]` | `list[int]` | Bind T=int |
| `dict[K, V]` | `dict[str, int]` | Bind K=str, V=int |
| `(T) -> U` | `(str) -> int` | Bind T=str, U=int |
| `T?` | `int?` | Bind T=int |
| `T` | `T` (already bound) | Verify consistent |

### 4. Unification Algorithm

```
function Unify(formal: Type, actual: Type, bindings: Map<string, Type>) -> bool:
    if formal is TypeParameter:
        name = formal.Name
        if name in bindings:
            # Already bound - check consistency
            return bindings[name] == actual
        else:
            # Bind the type parameter
            bindings[name] = actual
            return true

    if formal is GenericType and actual is GenericType:
        if formal.Name != actual.Name:
            return false
        if formal.TypeArguments.Count != actual.TypeArguments.Count:
            return false
        for i in range(formal.TypeArguments.Count):
            if not Unify(formal.TypeArguments[i], actual.TypeArguments[i], bindings):
                return false
        return true

    if formal is FunctionType and actual is FunctionType:
        # Unify parameter types (contravariant position)
        for i in range(formal.ParameterTypes.Count):
            if not Unify(formal.ParameterTypes[i], actual.ParameterTypes[i], bindings):
                return false
        # Unify return type (covariant position)
        return Unify(formal.ReturnType, actual.ReturnType, bindings)

    if formal is NullableType and actual is NullableType:
        return Unify(formal.UnderlyingType, actual.UnderlyingType, bindings)

    # No type parameters - types must match
    return formal == actual
```

## Type Constraint Satisfaction

After inferring types, verify constraints:

```python
def find_max[T: IComparable[T]](items: list[T]) -> T:
    ...

# Call: find_max([1, 2, 3])
# Infer: T=int
# Verify: int implements IComparable[int]? Yes → Success
```

### Constraint Checking Algorithm

```
function CheckConstraints(typeParam: TypeParameter, inferredType: Type) -> bool:
    for constraint in typeParam.Constraints:
        match constraint:
            InterfaceConstraint(iface):
                # Substitute type parameters in interface
                concreteIface = Substitute(iface, {typeParam.Name: inferredType})
                if not inferredType.Implements(concreteIface):
                    return false

            ClassConstraint:
                if inferredType.IsValueType:
                    return false

            StructConstraint:
                if not inferredType.IsValueType:
                    return false

    return true
```

## Error Reporting Strategy

### 1. Cannot Infer (No Arguments)

```python
def create_empty[T]() -> list[T]:
    return []

create_empty()  # ERROR
```

Error: `Type parameter 'T' cannot be inferred; no arguments provide type information. Use explicit syntax: create_empty[int]()`

### 2. Conflicting Inferred Types

```python
def pair[T](a: T, b: T) -> tuple[T, T]:
    return (a, b)

pair(1, "hello")  # ERROR
```

Error: `Conflicting types for type parameter 'T': inferred 'int' from argument 1, but 'str' from argument 2`

### 3. Constraint Not Satisfied

```python
def find_max[T: IComparable[T]](items: list[T]) -> T:
    ...

class NotComparable:
    pass

find_max([NotComparable()])  # ERROR
```

Error: `Inferred type 'NotComparable' does not satisfy constraint 'IComparable[NotComparable]' for type parameter 'T'`

### 4. Ambiguous Types

When multiple valid inferences exist, prefer:
1. Exact match over implicit conversion
2. Report error if truly ambiguous

## Edge Cases

### Return-Type-Only Generic

Cannot infer from return context alone:

```python
def create_empty[T]() -> list[T]:
    return []

# Cannot infer T - no arguments
items: list[int] = create_empty()  # ERROR: must use create_empty[int]()
```

### Nullable Type Inference

```python
def maybe[T](x: T?) -> T?:
    return x

maybe(None)  # Cannot infer T from None
maybe(42)    # T=int (from int, not int?)
```

### Collection Type Inference

```python
def first[T](items: list[T]) -> T:
    return items[0]

first([1, 2, 3])  # T=int (inferred from list literal element types)
first([])         # Cannot infer T from empty list
```

### Chained Generic Calls

```python
def identity[T](x: T) -> T:
    return x

def process[U](x: U) -> U:
    return identity(x)  # T inferred as U (propagate type parameter)
```

## Implementation Phases

### Phase 1: Core Infrastructure
- Create `GenericTypeInferenceService` class
- Implement basic unification for simple cases (T ↔ concrete type)

### Phase 2: Complex Unification
- Handle generic containers (list[T] ↔ list[int])
- Handle function types ((T) -> U ↔ (str) -> int)
- Handle multiple type parameters

### Phase 3: Constraint Checking
- Implement interface constraint checking
- Implement class/struct constraint checking
- Handle multiple constraints (T: IFoo & IBar)

### Phase 4: TypeChecker Integration
- Modify `CheckFunctionCall` to attempt inference
- Store inferred types in `SemanticInfo`
- Generate helpful error messages

### Phase 5: Code Generation
- Emit explicit type arguments in generated C#
- Handle generic method calls

## Non-Goals (Out of Scope)

1. **Partial type argument specification**: `convert[str]("42", ...)` is not supported
2. **Return type inference**: Cannot infer from expected return type context
3. **Bidirectional inference**: Only left-to-right inference from arguments
4. **Lambda parameter inference**: Lambda parameter types must be explicit

## API Design

### GenericTypeInferenceService

```csharp
public class GenericTypeInferenceService
{
    /// <summary>
    /// Attempt to infer type arguments for a generic function call.
    /// Returns null if inference fails.
    /// </summary>
    public InferenceResult InferTypeArguments(
        FunctionSymbol genericFunc,
        List<SemanticType> argumentTypes);
}

public record InferenceResult
{
    public bool Success { get; init; }
    public List<SemanticType>? InferredTypes { get; init; }
    public string? ErrorMessage { get; init; }
    public InferenceErrorKind? ErrorKind { get; init; }
}

public enum InferenceErrorKind
{
    NoArgumentsForTypeParameter,
    ConflictingTypes,
    ConstraintNotSatisfied,
    AmbiguousTypes
}
```

## Testing Strategy

### Unit Tests
- Unification for each type category
- Constraint checking for each constraint type
- Error message generation

### Integration Tests
- End-to-end compilation with inferred types
- Generated C# has correct explicit type arguments
- Chained generic calls
- Collection operations (first, filter, map)
