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
            BuiltinNames.Min when argTypes.Count == 1
                => InferElementTypeUnary(argTypes, typeInference),
            BuiltinNames.Max when argTypes.Count == 1
                => InferElementTypeUnary(argTypes, typeInference),
            BuiltinNames.Enumerate when argTypes.Count is 1 or 2
                => InferEnumerate(argTypes, typeInference),
            BuiltinNames.Zip when argTypes.Count == 2
                => InferZip2(argTypes, typeInference),
            BuiltinNames.Zip when argTypes.Count == 3
                => InferZip3(argTypes, typeInference),
            BuiltinNames.Map when argTypes.Count == 2
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

    private static SemanticType? InferZip2(List<SemanticType> argTypes, TypeInferenceService typeInference)
    {
        var elem1 = typeInference.InferIterableElementType(argTypes[0]);
        var elem2 = typeInference.InferIterableElementType(argTypes[1]);
        if (elem1 == null || elem2 == null)
            return null;
        return new GenericType
        {
            Name = BuiltinNames.Iterator,
            TypeArguments = new List<SemanticType>
            {
                new TupleType { ElementTypes = new List<SemanticType> { elem1, elem2 } }
            }
        };
    }

    private static SemanticType? InferZip3(List<SemanticType> argTypes, TypeInferenceService typeInference)
    {
        var elem1 = typeInference.InferIterableElementType(argTypes[0]);
        var elem2 = typeInference.InferIterableElementType(argTypes[1]);
        var elem3 = typeInference.InferIterableElementType(argTypes[2]);
        if (elem1 == null || elem2 == null || elem3 == null)
            return null;
        return new GenericType
        {
            Name = BuiltinNames.Iterator,
            TypeArguments = new List<SemanticType>
            {
                new TupleType { ElementTypes = new List<SemanticType> { elem1, elem2, elem3 } }
            }
        };
    }

    private static SemanticType? InferMap(List<SemanticType> argTypes)
    {
        // map(fn, iterable) -> Iterator[TOut] where TOut is the return type of fn
        if (argTypes[0] is FunctionType funcType)
        {
            return new GenericType
            {
                Name = BuiltinNames.Iterator,
                TypeArguments = new List<SemanticType> { funcType.ReturnType }
            };
        }
        return null;
    }
}
