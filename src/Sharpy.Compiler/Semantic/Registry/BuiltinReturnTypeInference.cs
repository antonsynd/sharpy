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
}
