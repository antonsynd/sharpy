# Nested Types

> **Implementation status:** ✅ Implemented

Sharpy supports nested type declarations — classes, structs, interfaces, enums, unions, delegates, and type aliases defined inside other type bodies. These map directly to C# nested types (a nested alias lowers to a `using` alias / inline expansion and emits no runtime member of its own).

## Syntax

```python
class LinkedList:
    @public
    class Node:
        value: int
        next: LinkedList.Node?

        def __init__(self, value: int):
            self.value = value
            self.next = None

    head: LinkedList.Node?

    def __init__(self):
        self.head = None
```

## Access Semantics

- **Default access**: Nested types default to `@private` (matching C# nested type defaults, Axiom 1)
- Use `@public`, `@protected`, `@internal`, or `@private` decorators to change access
- Nested types can access `@private` members of the enclosing type (matching C# semantics)
- External code references nested types via dot notation: `Outer.Inner`

## Supported Nesting

All combinations of type-in-type nesting are supported:

| Nested Type | Inside Class | Inside Struct | Inside Interface |
|-------------|-------------|---------------|-----------------|
| class       | ✅          | ✅            | ✅              |
| struct      | ✅          | ✅            | ✅              |
| interface   | ✅          | ✅            | ✅              |
| enum        | ✅          | ✅            | ✅              |
| union       | ✅          | ✅            | ✅              |
| delegate    | ✅          | ✅            | ✅              |
| type alias  | ✅          | ✅            | ✅              |

Arbitrary nesting depth is supported (class inside class inside class).

A nested **union**'s cases are constructed through the qualified case name,
`Host.Union.Case(…)` (the union is itself a nested type of the host, and each
case is a nested type of the union). A nested **union** with data-carrying cases
is supported in every host, including an interface. A nested **delegate** is
referenced as `Host.Delegate` in annotations and invoked like any delegate. A
nested **type alias** is referenced bare inside the host body and qualified,
`Host.Alias`, from outside (see [Type Aliases — Class-level aliases](type_aliases.md#class-level-aliases)).

The following program declares a union, a delegate, and a type alias nested in a
single class, then constructs and uses each from outside the host:

```python
class Geometry:
    type Scalar = float

    union Shape:
        case Circle(r: Scalar)
        case Rect(w: Scalar, h: Scalar)

    delegate AreaFn(s: Geometry.Shape) -> Scalar

    def area(self, s: Geometry.Shape) -> Scalar:
        match s:
            case Circle(r):
                return 3.0 * r * r
            case Rect(w, h):
                return w * h


def main() -> None:
    g: Geometry = Geometry()
    fn: Geometry.AreaFn = g.area
    print(fn(Geometry.Shape.Circle(2.0)))   # 12.0
    print(fn(Geometry.Shape.Rect(3.0, 5.0)))  # 15.0
```

## Type References

Use dot notation to reference nested types in annotations:

```python
node: LinkedList.Node = LinkedList.Node(42)
```

## Construction

Nested type constructors use the qualified name:

```python
item: Container.Item = Container.Item("widget")
```

## C# Emission

```csharp
public class LinkedList
{
    private class Node  // private by default
    {
        public int Value;
        // ...
    }

    // ...
}
```

## Restrictions

- **Static nesting only**: No Java-style inner class `outer` reference
- Nested types are statically nested — they do not capture an implicit reference to an enclosing instance

## See Also

- [Classes](classes.md)
- [Structs](structs.md)
- [Interfaces](interfaces.md)
- [Enums](enums.md)
