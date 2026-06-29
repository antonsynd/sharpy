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
                => InferElementTypeUnary(argTypes, typeInference),
            BuiltinNames.Max when argTypes.Count >= 1
                => InferElementTypeUnary(argTypes, typeInference),
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

    private static SemanticType? InferElementTypeUnary(List<SemanticType> argTypes, TypeInferenceService typeInference)
    {
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
