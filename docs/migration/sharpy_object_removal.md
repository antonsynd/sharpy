# Sharpy.Object Removal Migration

This document tracks the migration away from `Sharpy.Object` base class and Sharpy-specific interfaces.

## Files Referencing `Sharpy.Object` as Base Class

- `src/Sharpy.Core/Partial.List/List.cs`
- `src/Sharpy.Core/Partial.Set/Set.cs`
- `src/Sharpy.Core/Dict.cs`

## Files Referencing Sharpy-Specific Interfaces

### Protocol Interfaces (`ISized`, `IContainer`, `IIterable`, etc.)

| File | Interfaces Used |
|------|-----------------|
| `src/Sharpy.Core/All.cs` | IIterable |
| `src/Sharpy.Core/Any.cs` | IIterable |
| `src/Sharpy.Core/Bool.cs` | IBoolConvertible |
| `src/Sharpy.Core/Collections/Exports.cs` | ISized, IContainer, IIterable, IReversible |
| `src/Sharpy.Core/Collections/Interfaces/ICollection.cs` | ISized, IContainer, IIterable |
| `src/Sharpy.Core/Collections/Interfaces/IContainer.cs` | (definition) |
| `src/Sharpy.Core/Collections/Interfaces/IIterable.cs` | (definition) |
| `src/Sharpy.Core/Collections/Interfaces/IMapping.cs` | ISized, IContainer, IIterable |
| `src/Sharpy.Core/Collections/Interfaces/IMappingView.cs` | ISized |
| `src/Sharpy.Core/Collections/Interfaces/IMutableMapping.cs` | IMapping |
| `src/Sharpy.Core/Collections/Interfaces/IReversible.cs` | (definition) |
| `src/Sharpy.Core/Collections/Interfaces/ISequence.cs` | ISized, IContainer, IIterable, IReversible |
| `src/Sharpy.Core/Collections/Interfaces/ISized.cs` | (definition) |
| `src/Sharpy.Core/Dict.cs` | ISized, IContainer, IIterable, IRepresentable, IBoolConvertible, IHashable |
| `src/Sharpy.Core/EnumerableExtensions.cs` | IIterable |
| `src/Sharpy.Core/Enumerate.cs` | IIterable |
| `src/Sharpy.Core/Filter.cs` | IIterable |
| `src/Sharpy.Core/IBoolConvertible.cs` | (definition) |
| `src/Sharpy.Core/IEquatable.cs` | (definition) |
| `src/Sharpy.Core/IHashable.cs` | (definition) |
| `src/Sharpy.Core/IIdentifiable.cs` | (definition) |
| `src/Sharpy.Core/IRepresentable.cs` | (definition) |
| `src/Sharpy.Core/IStrConvertible.cs` | (definition) |
| `src/Sharpy.Core/Iter.cs` | IIterable |
| `src/Sharpy.Core/IterableLinqExtensions.cs` | IIterable |
| `src/Sharpy.Core/Itertools/Additional.cs` | IIterable |
| `src/Sharpy.Core/Itertools/Cycle.cs` | IIterable |
| `src/Sharpy.Core/Len.cs` | ISized |
| `src/Sharpy.Core/ListConversion.cs` | IIterable |
| `src/Sharpy.Core/Map.cs` | IIterable |
| `src/Sharpy.Core/Max.cs` | IIterable |
| `src/Sharpy.Core/Min.cs` | IIterable |
| `src/Sharpy.Core/Operator/Not.cs` | IBoolConvertible |
| `src/Sharpy.Core/Operator/Truth.cs` | IBoolConvertible |
| `src/Sharpy.Core/Partial.Iterator/Iterator.cs` | IIterable |
| `src/Sharpy.Core/Partial.Object/Object.cs` | IIdentifiable, IBoolConvertible, IRepresentable, IStrConvertible, IHashable |
| `src/Sharpy.Core/Reversed.cs` | IReversible, IIterable |
| `src/Sharpy.Core/SetConversion.cs` | IIterable |
| `src/Sharpy.Core/Slice.cs` | ISized |
| `src/Sharpy.Core/Sorted.cs` | IIterable |
| `src/Sharpy.Core/Sum.cs` | IIterable |
| `src/Sharpy.Core/TupleConversion.cs` | IIterable |
| `src/Sharpy.Core/Zip.cs` | IIterable |

## Migration Target

All Sharpy-specific interfaces will be replaced with standard .NET interfaces:

| Sharpy Interface | .NET Replacement |
|-----------------|------------------|
| `ISized` | `ICollection<T>.Count` or `IReadOnlyCollection<T>.Count` |
| `IContainer<T>` | `ICollection<T>.Contains` |
| `IIterable<T>` | `IEnumerable<T>` |
| `IReversible<T>` | Check for `IList<T>` and iterate backwards |
| `IBoolConvertible` | `operator true/false` |
| `IRepresentable` | `ToString()` override |
| `IStrConvertible` | `ToString()` override |
| `IHashable` | `GetHashCode()` override |
| `IIdentifiable` | Remove (use `object.ReferenceEquals`) |
| `IEquatable<T>` (Sharpy) | `System.IEquatable<T>` |

## Unity/.NET Standard 2.1 Compatibility Notes

- `IReadOnlySet<T>` is NOT available in .NET Standard 2.1 (requires .NET 5+)
- `System.Collections.Frozen.FrozenSet<T>` is NOT available (requires .NET 8+)
- Use `ImmutableHashSet<T>` from `System.Collections.Immutable` for FrozenSet implementation
