# Nested Types

> **Implementation status:** ✅ Implemented

Sharpy supports nested type declarations — classes, structs, interfaces, and enums defined inside other type bodies. These map directly to C# nested types.

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

Arbitrary nesting depth is supported (class inside class inside class).

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
