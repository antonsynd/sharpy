using Sharpy.Compiler.Discovery.Caching;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Expands a single <see cref="FunctionSignature"/> with default parameters into multiple
/// overload signatures — one per valid parameter count. Each overload is a separate
/// FunctionSymbol with the exact parameter list (no HasDefault flags).
/// </summary>
/// <remarks>
/// Some methods have return types that vary by overload (e.g., dict.get with 1 param
/// returns Optional[V], while dict.get with 2 params returns V). These special cases
/// are handled by a static lookup table keyed by (typeName, methodName, paramCount).
/// </remarks>
internal static class OverloadExpander
{
    /// <summary>
    /// Special-case return type overrides keyed by (typeName, methodName, paramCount).
    /// The Func receives the original return type and produces the adjusted return type.
    /// </summary>
    private static readonly Dictionary<(string TypeName, string MethodName, int ParamCount), Func<TypeSignature, TypeSignature>> ReturnTypeOverrides = new()
    {
        // dict.get with 1 param (just key) -> Optional[V]
        [("Dict", "get", 1)] = returnType => WrapInOptional(returnType),
        // dict.get with 2 params (key, default) -> V (no change, discovery already returns V)
        [("Dict", "get", 2)] = returnType => returnType,

        // dict.pop with 1 param (just key) -> Optional[V]
        [("Dict", "pop", 1)] = returnType => WrapInOptional(returnType),
        // dict.pop with 2 params (key, default) -> V (no change)
        [("Dict", "pop", 2)] = returnType => returnType,
    };

    /// <summary>
    /// Expands a function signature into multiple overloads based on default parameters.
    /// </summary>
    /// <param name="signature">The function signature, potentially with HasDefault parameters.</param>
    /// <param name="typeName">The declaring type name (e.g., "Dict", "List") for special-case rules.</param>
    /// <returns>
    /// A list of signatures: one for each valid parameter count from required-only up to all params.
    /// If no parameters have defaults, returns a single-element list containing the original signature.
    /// </returns>
    public static List<FunctionSignature> Expand(FunctionSignature signature, string typeName)
    {
        // Find the index where defaults start (first parameter with HasDefault)
        var firstDefaultIndex = -1;
        for (var i = 0; i < signature.Parameters.Count; i++)
        {
            if (signature.Parameters[i].HasDefault)
            {
                firstDefaultIndex = i;
                break;
            }
        }

        // No defaults — return the original as a single overload
        if (firstDefaultIndex < 0)
        {
            return [signature];
        }

        var result = new List<FunctionSignature>();
        var totalParams = signature.Parameters.Count;

        // Generate overloads from required-only up to all params
        for (var paramCount = firstDefaultIndex; paramCount <= totalParams; paramCount++)
        {
            // Strip HasDefault/DefaultValue from expanded parameters since each
            // overload is now a distinct signature with exactly the right param count.
            // Keeping HasDefault would cause the TypeChecker to treat them as optional,
            // leading to ambiguous overload resolution.
            var expandedParams = signature.Parameters.Take(paramCount)
                .Select(p => new ParameterSignature
                {
                    Name = p.Name,
                    Type = p.Type,
                    HasDefault = false,
                    DefaultValue = null,
                    IsVariadic = p.IsVariadic,
                })
                .ToList();

            var overload = new FunctionSignature
            {
                Name = signature.Name,
                Parameters = expandedParams,
                ReturnType = GetReturnType(signature.ReturnType, typeName, signature.Name, paramCount),
                TypeParameters = signature.TypeParameters,
                MethodToken = signature.MethodToken,
            };
            result.Add(overload);
        }

        return result;
    }

    private static TypeSignature GetReturnType(
        TypeSignature originalReturnType, string typeName, string methodName, int paramCount)
    {
        if (ReturnTypeOverrides.TryGetValue((typeName, methodName, paramCount), out var transform))
        {
            return transform(originalReturnType);
        }

        return originalReturnType;
    }

    /// <summary>
    /// Wraps a type signature in Optional (GenericType with name "Optional" and one type argument).
    /// This produces a TypeSignature that ConvertTypeSignature will map to OptionalType.
    /// </summary>
    private static TypeSignature WrapInOptional(TypeSignature inner)
    {
        return new TypeSignature
        {
            Name = "Optional",
            IsGeneric = true,
            TypeArguments = [inner],
        };
    }
}
