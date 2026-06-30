using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Registry;

/// <summary>
/// Data-driven dispatch for builtin function return type inference.
/// Replaces the if-chain in TypeChecker with a centralized lookup.
/// </summary>
internal static class BuiltinReturnTypeInference
{
    /// <summary>
    /// Tries to infer the return type of a builtin function call.
    /// Returns null if the function is not registered or inference fails.
    /// </summary>
    public static SemanticType? InferReturnType(
        string functionName,
        List<SemanticType> argTypes,
        TypeInferenceService typeInference)
    {
        return functionName switch
        {
            BuiltinNames.Len when argTypes.Count == 1
                => typeInference.InferLenType(argTypes[0]) ?? SemanticType.Unknown,
            BuiltinNames.Hash when argTypes.Count == 1
                => typeInference.InferHashType(argTypes[0]) ?? SemanticType.Unknown,
            BuiltinNames.Reversed when argTypes.Count == 1
                => InferReversed(argTypes, typeInference),
            BuiltinNames.Sorted when argTypes.Count >= 1
                => InferSorted(argTypes, typeInference),
            BuiltinNames.Min when argTypes.Count >= 1
                => InferMinMaxReturnType(argTypes, typeInference),
            BuiltinNames.Max when argTypes.Count >= 1
                => InferMinMaxReturnType(argTypes, typeInference),
            BuiltinNames.Enumerate when argTypes.Count is 1 or 2
                => InferEnumerate(argTypes, typeInference),
            BuiltinNames.Zip when argTypes.Count >= 2
                => InferZip(argTypes, typeInference),
            BuiltinNames.Map when argTypes.Count >= 2
                => InferMap(argTypes),
            _ => null
        };
    }

    private static SemanticType? InferReversed(List<SemanticType> argTypes, TypeInferenceService typeInference)
    {
        var elementType = typeInference.InferReversedElementType(argTypes[0]);
        if (elementType != null)
            return new GenericType
            {
                Name = BuiltinNames.Iterator,
                TypeArguments = new List<SemanticType> { elementType }
            };
        return null;
    }

    private static SemanticType? InferSorted(List<SemanticType> argTypes, TypeInferenceService typeInference)
    {
        var elementType = typeInference.InferIterableElementType(argTypes[0]);
        if (elementType != null)
            return new GenericType
            {
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType> { elementType }
            };
        return null;
    }

    private static SemanticType? InferMinMaxReturnType(List<SemanticType> argTypes, TypeInferenceService typeInference)
    {
        // Variadic value form (#1010): min(a, b, ...) / max(a, b, ...) over 2+ positional
        // arguments of the SAME type returns that value type (e.g. min(2, 3) -> int,
        // min("foo", "bar") -> str, min([1, 2], [3, 4]) -> list[int]). Requiring all positional
        // args to share a type distinguishes this from the iterable + positional-default form
        // (min(iterable, default), where the two args differ in type), whose result is still the
        // iterable's element type. Without this, the value form falls through to overload
        // resolution and leaks the open generic parameter T into composition (arithmetic,
        // annotations, lambda return types feeding map()/list()).
        if (argTypes.Count >= 2 && argTypes[0] is not UnknownType)
        {
            var first = argTypes[0];
            bool allSame = true;
            for (int i = 1; i < argTypes.Count; i++)
            {
                if (!Equals(argTypes[i], first))
                {
                    allSame = false;
                    break;
                }
            }
            if (allSame)
                return first;
        }

        // Mixed-numeric value form (#1014): min(2, 3.0) -> float64. The all-same-type branch
        // above misses mixed numerics, which would otherwise fall through to the iterable
        // element type (null for a scalar) and leak the open generic parameter T into
        // composition. Fold the positional arg types through the same numeric promotion the
        // binary operators use. GetPromotedType returns null for any non-numeric arg (a real
        // iterable, str, etc.) or an unsafe mix (long + ulong), so the iterable and
        // iterable+default forms still fall through to InferIterableElementType below.
        //
        // Axiom precedence (.NET > types > Python): the result is the promoted type, so
        // min(2, 3.0) is float64 (prints 2.0), diverging from Python's unpromoted 2.
        if (argTypes.Count >= 2)
        {
            SemanticType? promoted = argTypes[0];
            for (int i = 1; i < argTypes.Count && promoted is not null and not UnknownType; i++)
                promoted = PrimitiveCatalog.GetPromotedType(promoted, argTypes[i]);
            if (promoted is not null and not UnknownType)
                return promoted;
        }

        // Iterable form: min(iterable) / max(iterable) -> element type.
        return typeInference.InferIterableElementType(argTypes[0]);
    }

    private static SemanticType? InferEnumerate(List<SemanticType> argTypes, TypeInferenceService typeInference)
    {
        var elementType = typeInference.InferIterableElementType(argTypes[0]);
        if (elementType == null)
            return null;
        return new GenericType
        {
            Name = BuiltinNames.Iterator,
            TypeArguments = new List<SemanticType>
            {
                new TupleType { ElementTypes = new List<SemanticType> { BuiltinType.Int, elementType } }
            }
        };
    }

    private static SemanticType? InferZip(List<SemanticType> argTypes, TypeInferenceService typeInference)
    {
        var elementTypes = new List<SemanticType>(argTypes.Count);
        foreach (var arg in argTypes)
        {
            var elem = typeInference.InferIterableElementType(arg);
            if (elem == null)
                return null;
            elementTypes.Add(elem);
        }
        return new GenericType
        {
            Name = BuiltinNames.Iterator,
            TypeArguments = new List<SemanticType>
            {
                new TupleType { ElementTypes = elementTypes }
            }
        };
    }

    private static SemanticType? InferMap(List<SemanticType> argTypes)
    {
        // Handles both the single- and multi-iterable forms:
        //   map(fn, iterable)            -> Iterator[TOut]
        //   map(fn, a, b[, strict=...])  -> Iterator[TOut]
        //   map(fn, a, b, c[, strict=…]) -> Iterator[TOut]
        // The mapper is always argTypes[0]; the element type is its return type
        // irrespective of how many iterables follow (extra positional iterables and
        // a trailing positional `strict` bool are ignored, and a keyword `strict`
        // never appears in argTypes). Mirrors the >= 2 dispatch precedent used by zip.
        if (argTypes[0] is FunctionType funcType)
        {
            return new GenericType
            {
                Name = BuiltinNames.Iterator,
                TypeArguments = new List<SemanticType> { funcType.ReturnType }
            };
        }

        // Handle generic functions (e.g., str constructor passed as mapper)
        if (argTypes[0] is GenericFunctionType genFuncType && genFuncType.FunctionSymbol != null)
        {
            return new GenericType
            {
                Name = BuiltinNames.Iterator,
                TypeArguments = new List<SemanticType> { genFuncType.FunctionSymbol.ReturnType }
            };
        }

        return null;
    }
}
